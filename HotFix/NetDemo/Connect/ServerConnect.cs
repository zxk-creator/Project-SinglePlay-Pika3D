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

    // 缓存的发送缓冲区
    private readonly NetDataWriter sendWriter = new NetDataWriter();

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
        processor.SubscribeNetSerializable<ServerPlayerDeath, NetPeer>(OnPlayerDeath);
        processor.SubscribeNetSerializable<Commands, NetPeer>(OnCommandReceived);
        processor.SubscribeNetSerializable<ClientEquipClothIntent, NetPeer>(OnClientEquipACloth);
        processor.SubscribeNetSerializable<ClientTakeOffClothIntent, NetPeer>(OnClientTakeOffACloth);
        processor.SubscribeNetSerializable<ClientEquipToolIntent, NetPeer>(OnClientEquipATool);
        processor.SubscribeNetSerializable<ClientTakeOffBagIntent, NetPeer>(OnClientTakeOffABag);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////换装开始/////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    
    private void OnClientTakeOffACloth(ClientTakeOffClothIntent req, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error("服务端列表里没有这个玩家！这不该发生！");
            return;
        }

        var saveData = player.playerData;

        // 尝试卸下，内部会尝试放回背包
        bool success = saveData.RemoveCloth(req.targetSlot);

        // 无论成功与否，都发送最新状态，确保客户端状态与服务器一致
        SendInventorySync(player, peer);

        if (success)
            BroadcastInventorySyncToOthers(player);
    }

    private void OnClientEquipACloth(ClientEquipClothIntent req, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error("服务端列表里没有这个玩家！这不该发生！");
            return;
        }

        var saveData = player.playerData;

        // 校验客户端发的物品是否合法
        if (req.clothToEquip == null)
        {
            // 非法请求的话发回最新状态覆盖客户端的错误
            SendInventorySync(player, peer);
            return;
        }

        // 校验玩家真的拥有这件衣服吗，按id在背包里找
        ClothBase actualCloth = null;
        foreach (var item in saveData.GetAllItems())
        {
            if (item is ClothBase c && c.id == req.clothToEquip.id)
            {
                actualCloth = c;
                break;
            }
        }

        // 客户端数据不同步，发正确数据覆盖
        if (actualCloth == null)
        {
            SendInventorySync(player, peer);
            return;
        }

        // 尝试换装，内部会自动处理旧装备放回背包
        bool success = saveData.ChangeCloth(actualCloth);
        if (!success)
        {
            SendInventorySync(player, peer);
            return;
        }

        // 成功：同步给自己、广播给别人
        SendInventorySync(player, peer);
        BroadcastInventorySyncToOthers(player);
    }

    private void OnClientEquipATool(ClientEquipToolIntent req, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error("服务端列表里没有这个玩家！这不该发生！");
            return;
        }

        var saveData = player.playerData;

        if (req.toolToEquip == null)
        {
            // 传 null 表示卸下手上的工具，内部会把工具放回背包
            saveData.SetTool(null);
            SendInventorySync(player, peer);
            BroadcastInventorySyncToOthers(player);
            return;
        }

        ToolBase actualTool = null;
        foreach (var item in saveData.GetAllItems())
        {
            if (item is ToolBase t && t.id == req.toolToEquip.id)
            {
                actualTool = t;
                break;
            }
        }

        if (actualTool == null)
        {
            SendInventorySync(player, peer);
            return;
        }

        // 尝试装备，内部会把旧工具放回背包
        bool success = saveData.SetTool(actualTool);
        if (!success)
        {
            SendInventorySync(player, peer);
            return;
        }

        SendInventorySync(player, peer);
        BroadcastInventorySyncToOthers(player);
    }

    private void OnClientTakeOffABag(ClientTakeOffBagIntent req, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            Log.Error("服务端列表里没有这个玩家！这不该发生！");
            return;
        }

        var saveData = player.playerData;

        if (saveData.equipedBag != null && saveData.TryPutIntoBag(saveData.equipedBag) == null)
            saveData.equipedBag = null;

        SendInventorySync(player, peer);
        BroadcastInventorySyncToOthers(player);
    }

    // 客户端不会发送背包物品同步请求
    // 下面都是辅助方法
    private void SendInventorySync(NetPlayerData player, NetPeer targetPeer)
    {
        var sync = new ServerInventorySync
        {
            playerName = player.playerName,
            isGirl = player.playerData.isGirl,
            equipedCloth = player.playerData.equipedCloth,
            handSpace = player.playerData.handSpace,
            equipedBag = player.playerData.equipedBag,
            currentEquipedTool = player.playerData.currentEquipedTool
        };

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref sync);
        targetPeer.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastInventorySyncToOthers(NetPlayerData player)
    {
        var sync = new ServerInventorySync
        {
            playerName = player.playerName,
            isGirl = player.playerData.isGirl,
            equipedCloth = player.playerData.equipedCloth,
            handSpace = player.playerData.handSpace,
            equipedBag = player.playerData.equipedBag,
            currentEquipedTool = player.playerData.currentEquipedTool
        };

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref sync);

        var others = roomMgr.GetAllPlayerNetPeers();
        foreach (var p in others)
        {
            p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////换装结束/////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    
    /// <summary>
    /// 服务端收到客户端发来的命令执行请求
    /// </summary>
    private void OnCommandReceived(Commands commands, NetPeer peer)
    {
        var player = roomMgr.FindPlayerByPeer(peer);

        string command = commands.command;
        if (string.IsNullOrEmpty(command))
        {
            Log.Info("接收到了空或无效命令");
            return;
        }

        string[] parts = command.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) return;

        // 统一转小写
        string cmd = parts[0].ToLower();

        // TODO: 目前任何客户端都能执行命令，后续将会全部迁移到服务端
        // TODO: 客户端发送命令，服务端执行后把结果以消息的形式传给客户端。
        switch (cmd)
        {
            case "/kill":
            {
                    if (player == null)
                    {
                        Log.Error("找不到发命令的玩家");
                        break;
                    }
                    // 发送死亡信号
                    var msg = new ServerPlayerDeath
                    {
                        playerName = player.playerName,
                        deathVelocity = Vector3.zero,
                        deathAngularVelocity = Vector3.zero,
                    };

                    sendWriter.Reset();
                    processor.WriteNetSerializable(sendWriter, ref msg);

                    var peers = roomMgr.GetAllPlayerNetPeers();
                    foreach (var p in peers)
                        p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
                    break;
            }
            case "/give":
                {
                    if (player == null)
                    {
                        Log.Error("找不到发命令的玩家");
                        break;
                    }

                    // parts[0] 是 "/give"，parts[1] 是物品 ID
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int itemId))
                    {
                        Log.Info("命令格式错误，应为：/give <ItemId>");
                        break;
                    }

                    // 查模板
                    ItemBase newItem = Context.Item.GetItemCopyById(itemId);
                    if (newItem == null)
                    {
                        Log.Info($"未找到ID为 {itemId} 的物品");
                        break;
                    }

                    // 塞进玩家背包
                    ItemBase remain = player.playerData.TryPutIntoBag(newItem);
                    if (remain != null)
                    {
                        Log.Warn($"玩家 {player.playerName} 背包已满，无法放入 {newItem.itemName}");
                        break;
                    }

                    // 同步背包状态
                    SendInventorySync(player, peer);
                    Log.Info($"已给予 {player.playerName} 物品：{newItem.itemName} (ID:{itemId})");
                    break;
                }
            case "":
                {
                    
                    break;
                }
            default:
                {
                    Log.Info("未知命令" + command);
                    break;
                }
        }
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
            playerName = player.playerName,
            location = msg.location,
            rotation = msg.rotation,
            currentAnimation = msg.currentAnimation,
        };

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref outMsg);

        // 只发给同场景的人：不同场景的人根本看不到他，发了也是白费流量
        var others = roomMgr.GetScenePeers(player.scene, peer);
        foreach (var p in others)
            p.Send(sendWriter, 1, DeliveryMethod.Sequenced);
    }

    private void OnPlayerDeath(ServerPlayerDeath death, NetPeer peer)
    {
        // 找到玩家，设置为死亡
        var player = roomMgr.FindPlayerByPeer(peer);
        if (player == null) return;

        player.isDead = true;

        // 广播给所有客户端
        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref death);

        var peers = roomMgr.GetAllPlayerNetPeers();
        foreach (var p in peers)
            p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
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
            var exitMsg = new ServerPlayerExitGame { playerName = player.playerName };

            sendWriter.Reset();
            processor.WriteNetSerializable(sendWriter, ref exitMsg);

            foreach (var p in roomMgr.GetScenePeers(fromScene, peer))
                p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);

            // 移除玩家
            roomMgr.RemovePlayerByPeer(peer);
            return;
        }

        // 换了场景：通知"原来场景"的人把他移除，通知"新场景"的人把他加进来。
        // 两边都要发，因为收到的人要按自己当前场景决定是移除还是生成。
        var enterMsg = new ServerOtherPlayerEnterNewScene { newPlayerData = player };

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref enterMsg);

        var targets = new List<NetPeer>();
        // 原场景的人：把他移除（但如果他只是从主菜单回来，那边原本就没人看到他，没人可发）
        if (fromScene != ESceneType.MAIN_MENU)
            targets.AddRange(roomMgr.GetScenePeers(fromScene, peer));
        // 新场景的人：把他生成出来
        if (player.scene != fromScene)
            targets.AddRange(roomMgr.GetScenePeers(player.scene, peer));

        foreach (var p in targets)
            p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Debug.Log($"已连接: {peer}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Debug.Log($"已断开: {peer}  原因={info.Reason}");

        // 广播之前必须先拿到玩家数据
        NetPlayerData player = roomMgr.FindPlayerByPeer(peer);
        if (player == null)
        {
            roomMgr.RemovePlayerByPeer(peer);
            return;
        }

        // 直接关窗口掉线时不会有EnterNewScene，所以这里也要通知同场景的人把他移除，否则别人那边会一直留着他
        var exitMsg = new ServerPlayerExitGame { playerName = player.playerName };

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref exitMsg);

        foreach (var p in roomMgr.GetScenePeers(player.scene, peer))
            p.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);

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

        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref response);
        peer.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
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
                sendWriter.Reset();
                processor.WriteNetSerializable(sendWriter, ref respond);
                peer.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);
                return;
            }
        }

        // 找出生点（读配置）
#if UNITY_EDITOR
        var start = Context.net.spawnPoint[ESceneType.TEST];
#else
        var start = Context.net.spawnPoint[ESceneType.CITY];
#endif

        // 校验通过，可以连接，把玩家放入RoomMgr，并发送ok包
        // 这里有个关键细节：先发送所有玩家的登录信息包，再把新玩家添加到场景管理，防止客户端收到包含自己玩就的包
        // 为后续客户端预测打下基础，不是全部都是netPlayer，相机对着的是我们操控的。
        // 我们操控的就是本地玩家，那个玩家的netPlayer对我们不可见

#if UNITY_EDITOR
        var playerData = new NetPlayerData(req.player, peer, start, Quaternion.identity, ESceneType.TEST);
#else
        var playerData = new NetPlayerData(req.player, peer, start, Quaternion.identity,  ESceneType.CITY);
#endif
        var ok = new LoginResponse
        {
            serverResponse = LoginResponse.Status.ACCEPTED,
            spawnPoint = start,
#if UNITY_EDITOR
            enterScene = ESceneType.TEST,
#else
            enterScene = ESceneType.CITY,
#endif
#if UNITY_EDITOR
            scenePlayers = roomMgr.roomAndPlayers.TryGetValue(ESceneType.TEST, out var ps) ? new List<NetPlayerData>(ps) : new List<NetPlayerData>(),
#else
            // 默认玩家是直接出生在城市里的，只发城市里的人
            scenePlayers = roomMgr.roomAndPlayers.TryGetValue(ESceneType.CITY, out var ps) ? new List<NetPlayerData>(ps) : new List<NetPlayerData>(),
#endif
        };
        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref ok);
        peer.Send(sendWriter, 0, DeliveryMethod.ReliableOrdered);


        // 真正添加到场景管理器中
#if UNITY_EDITOR
        roomMgr.AddPlayer(ESceneType.TEST, playerData);
#else
        roomMgr.AddPlayer(ESceneType.CITY, playerData);
#endif

        // 有新人进来了，广播给同场景的其他人（带上他的位置、朝向、着装）
        // 用GetScenePeers排除新进入的这个玩家自己，防止他自己出问题
        var entered = new NewPlayerEntered { newPlayerData = playerData };
        sendWriter.Reset();
        processor.WriteNetSerializable(sendWriter, ref entered);
#if UNITY_EDITOR
        var otherPeers = roomMgr.GetScenePeers(ESceneType.TEST, peer);
#else
        var otherPeers = roomMgr.GetScenePeers(ESceneType.CITY, peer);
#endif
        foreach (var p in otherPeers)
        {
            p.Send(sendWriter, 0, DeliveryMethod.ReliableSequenced);
        }
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
