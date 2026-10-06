using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using PK;
using PKSv;
using UnityEngine;

namespace PKWeb;

// 挂在 GameObject 上，代表一个服务端。服务器就是一个独立的，不会依赖游戏运行的一个东西，所有东西都在他内部完成
public class ServerConnect : MonoBehaviour
{
    public int port { get; private set; }
    public LiteNetLib.NetManager serverConnect { get; private set; }
    public NetPacketProcessor processor { get; private set; }
    public RoomMgr roomMgr { get; private set; }
    public SaveData hostingWorld;

    // 服务端是否正在运行（Start 成功之后、Stop 之前）
    public bool isRunning { get; private set; }

    private Action onConnectedSuccess;

    /// <summary>
    /// 传入SaveData，实则是仅读取他的世界名称字段，其他的用不到
    /// </summary>
    /// <param name="port"></param>
    /// <param name="world"></param>
    public void Init(int port, SaveData world)
    {
        this.port = port;
        hostingWorld = world;
    }

    public bool StartServer(Action onConnectedSuccess)
    {
        this.onConnectedSuccess = onConnectedSuccess;

        roomMgr = new RoomMgr(hostingWorld);

        var listener = new EventBasedNetListener();
        processor = new NetPacketProcessor();
        RegisterServerEvents(listener);

        serverConnect = new LiteNetLib.NetManager(listener)
        {
            ChannelsCount = 2,
            AutoRecycle = true
        };

        if (!serverConnect.Start(port))
        {
            Debug.LogError($"启动失败：端口 {port} 可能被占用");
            isRunning = false;
            return false;
        }

        isRunning = true;
        Debug.Log($"服务端已启动，端口 {port}。");
        onConnectedSuccess?.Invoke();
        return true;
    }

    void Update()
    {
        if (!isRunning) return;
        serverConnect.PollEvents();
    }

    void OnDestroy()
    {
        serverConnect?.Stop();
        isRunning = false;
        roomMgr?.RemoveAllPlayer();
        // 必须注销游戏内时钟：updateProxy 持有 TickTime 委托，会拽着 RoomMgr 不放。
        // 不注销的话时钟会一直跑，RoomMgr（连同 currentWorld）也回收不掉。
        roomMgr?.StopTimer();
    }

    // 事件注册
    private void RegisterServerEvents(EventBasedNetListener listener)
    {
        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
        listener.NetworkReceiveEvent += OnNetworkReceive;

        processor.SubscribeNetSerializable<PlayerCountRequest, NetPeer>(OnPlayerCountRequest);
        processor.SubscribeNetSerializable<LoginRequest, NetPeer>(OnLoginRequest);
        processor.SubscribeNetSerializable<ClientEnterNewScene, NetPeer>(OnClientEnterNewScene);
        processor.SubscribeNetSerializable<LocationSynchronize, NetPeer>(OnLocationSync);
    }

    // ---------- 连接事件 ----------
    private void OnConnectionRequest(ConnectionRequest request)
    {
        request.Accept();
    }

    /// <summary>
    /// 客户端发来的位置同步信号
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="peer"></param>
    private void OnLocationSync(LocationSynchronize msg, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error($"位置同步失败：找不到 peer {peer} 对应的玩家，这不该发生！");
            return;
        }

        // 更新记录，后面有人进来时能拿到最新的位置和朝向
        player.position = msg.location;
        player.rotation = msg.rotation;

        var outMsg = new OtherPlayerLocationSync
        {
            playerId = player.playerId,
            location = msg.location,
            rotation = msg.rotation,
            currentAnimation = msg.currentAnimation,
        };

        var w = new NetDataWriter();
        processor.WriteNetSerializable(w, ref outMsg);

        // 只发给同场景的人：不同场景的人根本看不到他，发了也是白费流量
        var others = GetScenePeers(player.scene, peer);
        foreach (var p in others)
            p.Send(w, 1, DeliveryMethod.Sequenced);
    }

    /// <summary>
    /// 客户端尝试进入一个场景
    /// </summary>
    private void OnClientEnterNewScene(ClientEnterNewScene req, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error("玩家" + peer.ToString() + "不存在！这不该发生！");
            return;
        }

        ESceneType fromScene = player.scene;
        player.scene = req.scene;

        if (req.scene == ESceneType.MAIN_MENU)
        {
            // 回主菜单等价于退出游戏：通知同场景的人把他移除
            var exitMsg = new PlayerExitGame { playerId = player.playerId };

            var w = new NetDataWriter();
            processor.WriteNetSerializable(w, ref exitMsg);

            foreach (var p in GetScenePeers(fromScene, peer))
                p.Send(w, 0, DeliveryMethod.ReliableOrdered);

            // 移除玩家
            roomMgr.RemovePlayerByPeer(peer);
            return;
        }

        // 换了场景：通知"原来场景"的人把他移除，通知"新场景"的人把他加进来。
        // 两边都要发，因为收到的人要按自己当前场景决定是移除还是生成。
        var enterMsg = new OtherPlayerEnterNewScene { newPlayerData = player };

        var w2 = new NetDataWriter();
        processor.WriteNetSerializable(w2, ref enterMsg);

        var targets = new List<NetPeer>();
        // 原场景的人：把他移除（但如果他只是从主菜单回来，那边原本就没人看到他，没人可发）
        if (fromScene != ESceneType.MAIN_MENU)
            targets.AddRange(GetScenePeers(fromScene, peer));
        // 新场景的人：把他生成出来
        if (player.scene != fromScene)
            targets.AddRange(GetScenePeers(player.scene, peer));

        foreach (var p in targets)
            p.Send(w2, 0, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>取指定场景里除 exclude 之外的所有玩家连接</summary>
    private List<NetPeer> GetScenePeers(ESceneType scene, NetPeer exclude)
    {
        var result = new List<NetPeer>();
        if (!roomMgr.roomAndPlayers.TryGetValue(scene, out var players)) return result;

        foreach (var p in players)
        {
            if (p.clientConnect == exclude) continue;
            result.Add(p.clientConnect);
        }
        return result;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Debug.Log($"已连接: {peer}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Debug.Log($"已断开: {peer}  原因={info.Reason}");

        // 广播之前必须先拿到玩家数据（RemovePlayerByPeer 之后就从列表里没了）
        NetPlayerData player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            roomMgr.RemovePlayerByPeer(peer);
            return;
        }

        // 直接关窗口 / 掉线时不会有 EnterNewScene(MAIN_MENU)，
        // 所以这里也要通知同场景的人把他移除，否则别人那边会一直留着他
        var exitMsg = new PlayerExitGame { playerId = player.playerId };

        var w = new NetDataWriter();
        processor.WriteNetSerializable(w, ref exitMsg);

        foreach (var p in GetScenePeers(player.scene, peer))
            p.Send(w, 0, DeliveryMethod.ReliableOrdered);

        roomMgr.RemovePlayerByPeer(peer);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        processor.ReadAllPackets(reader, peer);
    }



    // ---------- 消息订阅 ----------
    /// <summary>
    /// 客户端发来的请求玩家数量命令
    /// </summary>
    /// <param name="req">空响应包</param>
    /// <param name="peer">连接对象</param>
    public void OnPlayerCountRequest(PlayerCountRequest req, NetPeer peer)
    {
        var response = new PlayerCountResponse();
        response.count = roomMgr.GetPlayerCount();
        response.playerNames = roomMgr.GetPlayerNames();

        var w = new NetDataWriter();
        processor.WriteNetSerializable(w, ref response);
        peer.Send(w, 0, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>
    /// 服务端收到登录请求要怎么处理
    /// </summary>
    /// <param name="req">客户端发来的响应包</param>
    /// <param name="peer">和客户端的连接对象</param>
    public void OnLoginRequest(LoginRequest req, NetPeer peer)
    {
        Debug.Log("收到登录请求，玩家名：" + req.player.playerName);

        // 对比玩家名，如果没有重名玩家，则让他进来。
        var names = roomMgr.GetPlayerNames();
        foreach (var name in names)
        {
            if (name == req.player.playerName)
            {
                // 说明有重名玩家，拒绝连接
                var respond = new LoginResponse();
                respond.serverResponse = LoginResponse.Status.REJECTED_SAME_NAME;
                var w = new NetDataWriter();
                processor.WriteNetSerializable(w, ref respond);
                peer.Send(w, 0, DeliveryMethod.ReliableOrdered);
                return;
            }
        }

        // 找出生点（读配置）
        var start = Context.net.spawnPoint[ESceneType.CITY];


        // 校验通过，可以连接，把玩家放入RoomMgr，并发送ok包
        // 这里有个关键细节：先发送所有玩家的登录信息包，再把新玩家添加到场景管理，防止客户端收到包含自己玩就的包
        // 为后续客户端预测打下基础，不是全部都是netPlayer，相机对着的是我们操控的。
        // 我们操控的就是本地玩家，那个玩家的netPlayer对我们不可见
        var player = new NetPlayerData(req.player, peer, start, Quaternion.identity, ESceneType.CITY);
        var ok = new LoginResponse { 
            serverResponse = LoginResponse.Status.ACCEPTED, 
            spawnPoint = start,
            enterScene = ESceneType.CITY,
            // 默认玩家是直接出生在城市里的，只发城市里的人
            scenePlayers = GetScenePlayers(ESceneType.CITY)
        };
        var w2 = new NetDataWriter();
        processor.WriteNetSerializable(w2, ref ok);
        peer.Send(w2, 0, DeliveryMethod.ReliableOrdered);


        // 真正添加到场景管理器中
        roomMgr.AddPlayer(ESceneType.CITY, player);


        // 有新人进来了，广播给同场景的其他人（带上他的位置、朝向、着装）
        // 用 GetScenePeers 排除新进入的这个玩家自己，防止他自己出问题
        var entered = new NewPlayerEntered { newPlayerData = player };
        var w3 = new NetDataWriter();
        processor.WriteNetSerializable(w3, ref entered);
        var otherPeers = GetScenePeers(ESceneType.CITY, peer);
        foreach (var p in otherPeers)
        {
            p.Send(w3, 0 ,DeliveryMethod.ReliableSequenced);
        }
    }

    /// <summary>取指定场景里的所有玩家数据（拷贝一份，避免发送期间被 AddPlayer 改到）</summary>
    private List<NetPlayerData> GetScenePlayers(ESceneType scene)
    {
        var result = new List<NetPlayerData>();
        if (!roomMgr.roomAndPlayers.TryGetValue(scene, out var players)) return result;

        result.AddRange(players);
        return result;
    }

    // 关闭和所有人的连接（但不关闭服务器）
    public void DisconnectAll()
    {
        serverConnect.DisconnectAll();
        roomMgr.RemoveAllPlayer();
    }

    // 彻底关闭服务器
    public void StopServer()
    {
        serverConnect.Stop();
        roomMgr.RemoveAllPlayer();
        roomMgr.StopTimer();
        isRunning = false;
    }
}