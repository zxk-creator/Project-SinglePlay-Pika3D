using System;
using LiteNetLib;
using LiteNetLib.Utils;
using PKSv;
using UnityEngine;

namespace PKWeb;

// 挂在 GameObject 上，一个客户端只能有一个连接。依赖游戏运行状态
public class ClientConnect : MonoBehaviour
{
    enum ClientState { Connecting, Connected, Disconnected }

    public string connectedIP { get; private set; }
    public int connectedPort { get; private set; }
    public NetPeer netPeer { get; private set; }
    public NetPacketProcessor processor { get; private set; }
    public SaveData plyData;

    // 网络层是否已启动
    public bool isRunning { get; private set; }

    // 是否正在连接 / 等待服务器应答（Start 之后、收到 LoginResponse 或断开之前）
    public bool isConnecting { get; private set; }

    private LiteNetLib.NetManager netMgr;
    private ClientState connectState = ClientState.Disconnected;
    private Action<Vector3, ESceneType> onConnectionSuccess;
    private Action<string> onConnectionLost;

    // 复用的发送缓冲区
    private readonly NetDataWriter sendWriter = new NetDataWriter();

    // 由外部调用
    public void Init(string IP, int port, SaveData player)
    {
        connectedIP = IP;
        connectedPort = port;
        plyData = player;
    }

    private Action<ServerCommonResponse> _onCommonMessageResponse;

    /// <summary>
    /// 启动客户端
    /// </summary>
    /// <param name="onConnectionSuccess">服务器接收了你的连接，允许你加入，要执行的事件</param>
    /// <param name="onConnectionLost">连接丢失，要执行的事件</param>
    /// <returns>是否成功</returns>
    public bool StartClient(Action<Vector3, ESceneType> onConnectionSuccess, Action<string> onConnectionLost, Action<ServerCommonResponse> onCommonMessageResponse)
    {
        this.onConnectionSuccess = onConnectionSuccess;
        this.onConnectionLost = onConnectionLost;
        connectState = ClientState.Connecting;
        isConnecting = true;

        var listener = new EventBasedNetListener();
        processor = new NetPacketProcessor();
        RegisterClientEvents(listener);

        netMgr = new LiteNetLib.NetManager(listener)
        {
            ChannelsCount = 2,
            AutoRecycle = true,
        };

        if (!netMgr.Start())
        {
            Debug.LogError("客户端启动失败");
            isConnecting = false;
            return false;
        }

        isRunning = true;

        netPeer = netMgr.Connect(connectedIP, connectedPort, "key");
        if (netPeer == null)
        {
            Debug.LogError($"连接失败：{connectedIP}:{connectedPort}（地址无法解析）");
            netMgr.Stop();
            isRunning = false;
            isConnecting = false;
            return false;
        }

        Debug.Log($"正在连接 {connectedIP}:{connectedPort} ...");

        _onCommonMessageResponse = onCommonMessageResponse;
        return true;
    }

    void Update()
    {
        if (!isRunning || netMgr == null) return;
        netMgr.PollEvents();
    }

    void OnDestroy()
    {
        netMgr?.Stop();
        isRunning = false;
        isConnecting = false;
    }

    // 事件注册
    private void RegisterClientEvents(EventBasedNetListener listener)
    {
        listener.NetworkReceiveEvent += OnNetworkReceive;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;

        processor.SubscribeNetSerializable<PlayerCountResponse>(OnPlayerCountResponse);
        processor.SubscribeNetSerializable<LoginResponse>(OnLoginResponse);
        processor.SubscribeNetSerializable<NewPlayerEntered>(OnNewPlayerEntered);
        processor.SubscribeNetSerializable<ServerOtherPlayerEnterNewScene>(OnOtherPlayerEnterNewScene);
        processor.SubscribeNetSerializable<ServerPlayerExitGame>(OnOtherPlayerExitGame);
        processor.SubscribeNetSerializable<OtherPlayerLocationSync>(OnOtherPlayerLocationSync);
        processor.SubscribeNetSerializable<ServerPlayerDeath>(OnPlayerDeath);
        processor.SubscribeNetSerializable<ServerInventorySync>(OnInventorySync);
        processor.SubscribeNetSerializable<ServerCommonResponse>(OnServerCommonResponse);
    }

    private void OnServerCommonResponse(ServerCommonResponse response)
    {
        _onCommonMessageResponse?.Invoke(response);
    }

    // ---------- 连接事件 ----------
    /// <summary>
    /// Send后，客户端收到后，就会触发此回调
    /// </summary>
    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        processor.ReadAllPackets(reader);
    }

    private void OnOtherPlayerLocationSync(OtherPlayerLocationSync msg)
    {
        var player = Context.sc.scenePlayers.Find(p => p.netPlayer.playerName == msg.playerName);
        if (player == null)
        {
            PK.Log.Error($"位置同步时场景里找不到playerName={msg.playerName}，这不该发生！");
            return;
        }

        player.SetTransform(msg.location, msg.rotation);
        player.SetAnimation(msg.currentAnimation);
    }

    private void OnPlayerDeath(ServerPlayerDeath death)
    {
        // 首先对比死的人是不是自己
        var localPly = Context.localPlayer;
        // 死的人是自己，让自己死
        if (localPly.playerSaveData.playerName == death.playerName)
        {
            localPly.Dead();
        }
        Context.sc.MarkPlayerDead(death.playerName, death.deathVelocity, death.deathAngularVelocity);
    }

    /// <summary>
    /// 服务器连接成功出发的回调
    /// </summary>
    private void OnPeerConnected(NetPeer peer)
    {
        connectState = ClientState.Connected;
        Debug.Log($"[+] 已连接到服务器: {peer}，开始发送登录请求");

        sendWriter.Reset();
        var req = new LoginRequest();

        // 调试阶段：直接替换为一个调试用背包
#if UNITY_EDITOR
        plyData.SetBag(Context.Item.GetItemCopyById(1425) as BagpackBase);
#endif
        // 向服务器发送背包同步请求（SaveData里面已经包含），这是唯一客户端权威的地方，因为数据存在本地，必须先传到服务器上
        // 后续都将是服务端权威，任何操作都要经过服务端批准。
        req.player = plyData;
        processor.WriteNetSerializable(sendWriter, ref req);
        peer.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
    }

    // 其他玩家退出游戏的消息
    private void OnOtherPlayerExitGame(ServerPlayerExitGame message)
    {
        string playerName = message.playerName;
        // 把他从场景中移除
        Context.sc.RemovePlayerByName(playerName);
    }

    /// <summary>
    /// 连接没了，被动断开的
    /// </summary>
    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        connectState = ClientState.Disconnected;
        isConnecting = false;
        Debug.Log($"[-] 与服务器断开: 原因={info.Reason}");
        
        // 如果是主动断开连接，不会触发连接丢失事件
        if (info.Reason == DisconnectReason.DisconnectPeerCalled)
            return;
        onConnectionLost.Invoke(info.Reason.ToString());
    }

    // ---------- 消息订阅 ----------
    /// <summary>
    /// 接收到服务端返回的玩家数量包
    /// </summary>
    /// <param name="r">返回的包</param>
    public void OnPlayerCountResponse(PlayerCountResponse r)
    {
        Debug.Log($"人数：{r.count}");
        foreach (var n in r.playerNames) Debug.Log($"  - {n}");
    }
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////换装开始/////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void OnInventorySync(ServerInventorySync msg)
    {
        var localPlayer = Context.localPlayer;
        if (localPlayer == null)
        {
            PK.Log.Error("OnInventorySync没有本地玩家，这不该发生！");
            return;
        }

        var saveData = localPlayer.playerSaveData;
        if (saveData == null)
        {
            PK.Log.Error("OnInventorySync时本地玩家没有SaveData，已忽略");
            return;
        }

        if (msg.playerName != saveData.playerName)
        {
            var netPlayer = Context.sc.FindPlayerByName(msg.playerName);
            if (netPlayer == null)
            {
                #if UNITY_EDITOR
                PK.Log.Info("OnInventorySync找不到玩家：" + msg.playerName + "如果是不同场景的人物请忽略");
                #endif
                return;
            }

            netPlayer.SetOutfit(msg.equipedCloth, msg.isGirl);

            if (msg.currentEquipedTool is ToolBase remoteTool)
                netPlayer.equipSystem.SetTool(remoteTool);
            else
                netPlayer.equipSystem.ClearTool();

            return;
        }

        // 必须造新对象传进去，否则 SetSaveData 内部的相等比较会因同一引用直接短路
        var incoming = new SaveData
        {
            playerName = saveData.playerName,
            worldName = saveData.worldName,
            isGirl = msg.isGirl,
            equipedCloth = msg.equipedCloth,
            handSpace = msg.handSpace,
            equipedBag = msg.equipedBag,
            currentEquipedTool = msg.currentEquipedTool
        };

        localPlayer.SetSaveData(incoming);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////换装结束/////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    /// <summary>
    /// 服务端返回的登录请求响应体
    /// </summary>
    /// <param name="resp">响应体包</param>
    public void OnLoginResponse(LoginResponse resp)
    {
        switch (resp.serverResponse)
        {
            case LoginResponse.Status.ACCEPTED:
                Debug.Log("登录成功，可以继续游玩");
                isConnecting = false;

                // 回填场景玩家（除了当前操控的玩家本身），位置和旋转都按服务端给的摆
                foreach (var v in resp.scenePlayers)
                {
                    Context.sc.SpawnANetPlayer(v);
                }

                // 进入场景。必须 sendToServer=true：
                // 服务端要据此把"这个 peer 在哪个场景"记下来，后面才有办法只把同场景的人发给我
                Context.sc.EnterNewScene(resp.enterScene, true);

                // 生成玩家，然后使其DontDestroyOnLoad
                Context.sc.SpawnALocalPlayer(plyData, resp.spawnPoint);
                if (Context.localPlayer == null)
                {
                    PK.Log.Error("[ClientConnect] 本地玩家生成失败，无法进入游戏");
                    Disconnect();
                    return;
                }

                // 使得本地玩家不被销毁，我们手动销毁
                DontDestroyOnLoad(Context.localPlayer);
            
                // 回调：说不定有一些操作需要额外实现
                onConnectionSuccess.Invoke(resp.spawnPoint, resp.enterScene);
                break;

                // TODO: 这里有bug：断开连接后会导致各种诡异问题，需要检查
            case LoginResponse.Status.REJECT_PLAYERFULL:
                Debug.LogWarning("服务器已满！");
                Disconnect();
                return;
            case LoginResponse.Status.REJECTED_SAME_NAME:
                Debug.LogWarning("有相同名字的玩家，登陆失败。请尝试更改玩家姓名后再进入");
                Disconnect();
                return;
            default:
                return;
        }
    }

    /// <summary>
    /// 玩家新进入，服务端传过来的事件响应
    /// </summary>
    public void OnNewPlayerEntered(NewPlayerEntered newPly)
    {
        NetPlayerData p = newPly.newPlayerData;
        Debug.Log($"新玩家进入：{p.playerName}");

        // 只关心和我同一个场景的人
        if (p.scene != Context.sc.currentScene) return;

        Context.sc.SpawnANetPlayer(p);
    }

    /// <summary>
    /// 有人换了场景。他进的是我当前场景就把他生成出来，否则（原来在我这）把他移除
    /// </summary>
    public void OnOtherPlayerEnterNewScene(ServerOtherPlayerEnterNewScene pkt)
    {
        NetPlayerData p = pkt.newPlayerData;

        bool inMyScene = p.scene == Context.sc.currentScene;
        var existing = Context.sc.scenePlayers.Find(x => x.netPlayer.playerName == p.playerName);

        if (inMyScene)
        {
            if (existing != null) return;   // 已经在了
            Context.sc.SpawnANetPlayer(p);
        }
        else if (existing != null)
        {
            Context.sc.RemovePlayerByName(p.playerName);
        }
    }

    /// <summary>
    /// 主动断开连接，细节：不会触发onConnectionLost钩子事件
    /// </summary>
    public void Disconnect()
    {
        netMgr?.Stop();
        isRunning = false;
        isConnecting = false;
    }
}
