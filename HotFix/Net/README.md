# LiteNetLib（Pikatown3D 内嵌版）快速上手

> 本文档仅依据 `Assets/Scripts/Net/` 目录内的源码整理，描述的是**仓库中当前这份代码**的实际行为，不是上游 LiteNetLib 官方文档的翻译。
> 本文档为概览与上手；逐成员签名请看同目录下的 [`API_Reference.md`](./API_Reference.md)。

---

## 1. 这是什么

一套纯 C# 的 **UDP 可靠/不可靠传输库**，以源码形式内嵌在 Unity 工程里。

| 项目 | 值 |
| --- | --- |
| 命名空间 | `LiteNetLib`、`LiteNetLib.Utils`、`LiteNetLib.Layers` |
| 程序集 | `LiteNetLib`（由 `LiteNetLib.asmdef` 定义） |
| 依赖 | 无（`references: []`），仅依赖 .NET/Unity 基础库 |
| 不安全的代码 | **已开启**（`allowUnsafeCode: true`，`FastBitConverter` / `NativeSocket` 使用指针） |
| 自动被引用 | `autoReferenced: true`（无需手动在 asmdef 中加引用即可使用） |
| 传输层 | UDP（`SocketType.Dgram`），支持 IPv4 / IPv6 |
| 源文件头 | **没有**许可证/版权头，文件直接从 `using` 或 `#if` 开始 |
| 版本号 | 源码中**不存在**版本字符串；如需版本请以外部记录为准 |

---

## 2. 关键设计：两套并行的类型

这是读懂本库最重要的一点——目录里存在**两套平行的类型族**，它们之间是**普通继承关系**，不是别名、不是 `partial`、也不是废弃包装：

```
LiteNetManager  (public partial class, IEnumerable<LiteNetPeer>)   ← 基础实现（"lite" 版）
      ▲
      │ 继承
NetManager      (public class, IEnumerable<NetPeer>)               ← 增强版：多通道 + NTP

LiteNetPeer     (public class : IPEndPoint)                        ← 基础实现（"lite" 版）
      ▲
      │ 继承
NetPeer         (public class : LiteNetPeer)                       ← 增强版：多通道
```

配套的请求类型同理：

```
LiteConnectionRequest (public class)
      ▲
ConnectionRequest     (public class : LiteConnectionRequest)
```

### 两者差异

| | `LiteNetManager` + `LiteNetPeer` | `NetManager` + `NetPeer` |
| --- | --- | --- |
| 通道（channel） | 仅通道 0 | **1–64 个通道**（`ChannelsCount`） |
| 收包事件回调 | `OnNetworkReceive(peer, reader, deliveryMethod)` | `OnNetworkReceive(peer, reader, **channelNumber**, deliveryMethod)` |
| 发送 API | `Send(data, deliveryMethod)` | 额外有 `Send(data, **channelNumber**, deliveryMethod)` |
| `SendToAll` | 无通道参数 | 额外有一整套带 `channelNumber` 的重载 |
| NTP 对时 | 不支持（`ProcessNtpRequests` 为空实现） | 支持（`CreateNtpRequest` + `OnNtpResponse`） |
| 监听接口 | `ILiteNetEventListener` | `INetEventListener`（**彼此独立，不构成继承关系**） |
| 事件监听辅助类 | `EventBasedLiteNetListener` | `EventBasedNetListener` |
| 连接请求类型 | `LiteConnectionRequest` | `ConnectionRequest` |

### 实现机制

`LiteNetManager` 通过 **4 个 `protected virtual` 工厂方法**决定创建哪种具体对象，`NetManager` 覆写它们以产出增强版对象：

```csharp
protected virtual LiteNetPeer CreateOutgoingPeer(IPEndPoint, int id, byte connectNum, ReadOnlySpan<byte> connectData)
protected virtual LiteNetPeer CreateIncomingPeer(LiteConnectionRequest request, int id)
protected virtual LiteNetPeer CreateRejectPeer(IPEndPoint, int id)
protected virtual LiteConnectionRequest CreateConnectionRequest(IPEndPoint, NetPacketRequestPacket requestPacket)
```

`NetManager` 同时用 `new` 关键字**遮蔽**基类返回 `LiteNetPeer` 的成员，改成返回 `NetPeer`：

- `Connect(...)` 的全部重载
- `FirstPeer`
- `GetPeers(List<NetPeer>, ConnectionState)` / `GetConnectedPeers(List<NetPeer>)`
- `GetEnumerator()`

### 选择建议

- **只需要单通道**、想把 API 面缩到最小 → 用 `LiteNetManager` + `ILiteNetEventListener`。
- **需要多通道做 QoS 隔离**（例如：可靠指令走通道 0、频繁状态同步走通道 1）或需要 NTP 对时 → 用 `NetManager` + `INetEventListener`。

> ⚠️ 注意：两者**不要混用监听接口**。`NetManager` 内部会把事件按 `INetEventListener` 派发；`LiteNetManager` 按 `ILiteNetEventListener` 派发。传入不匹配的监听对象会静默收不到回调（`NetManager` 的构造函数里给基类传的是 `null`）。

---

## 3. 三种运行模式

| 模式 | 启动方式 | 驱动方式 | 适用场景 |
| --- | --- | --- | --- |
| **自动线程模式**（默认） | `Start(...)` | 库自己起后台线程 `_logicThread` 循环收包，`UpdateTime`（默认 15ms）为周期 | 普通客户端/服务端 |
| **手动模式** | `StartInManualMode(...)` | 每帧**必须**调用 `PollEvents()` 收包 + `ManualUpdate(elapsedMs)` 更新与发送 | 单线程服务器、需要与游戏主循环严格同步 |
| **非同步事件模式** | `Start(...)` 后设 `UnsyncedEvents = true` | 事件在接收线程里**立即**回调，无需 `PollEvents()` | 追求最低延迟，回调内不能碰 Unity API |

`UnsyncedReceiveEvent` / `UnsyncedDeliveryEvent` 可以把"接收事件"和"送达事件"**单独**切到立即回调，而其余事件仍走 `PollEvents()` 队列。

> Unity 使用提示：非手动模式下事件默认在后台线程排队，需要每帧调用 `PollEvents()` 才会派发到主线程，**这是唯一能安全访问 Unity API 的时机**。

> ⚠️ **排错第一步**：`NetDebug` 的日志方法标了 `[Conditional("DEBUG_MESSAGES")]`（`WriteForce` 还可用 `DEBUG`），**未定义这两个宏时所有日志调用会被编译期整体剔除**，看起来就像"库完全静默"。排查连接/收发问题时，请在 Player Settings 的 Scripting Define Symbols 里加上 `DEBUG_MESSAGES`。
> 另：`WriteError` **没有** `[Conditional]`，所以"错误级"日志默认始终保留。

---

## 4. 最小可运行示例

### 4.1 服务端

```csharp
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;

public class Server
{
    private NetManager _netManager;

    public void Start(int port = 9050)
    {
        var listener = new EventBasedNetListener();

        listener.ConnectionRequestEvent += request =>
        {
            // 最多接受 100 个连接；这里也可以 request.Reject()
            if (request.AcceptIfKey("SomeSecretKey") == null)
                return; // key 不匹配，已自动拒绝
        };

        listener.PeerConnectedEvent += peer =>
            UnityEngine.Debug.Log($"已连接: {peer.EndPoint}");

        listener.PeerDisconnectedEvent += (peer, info) =>
            UnityEngine.Debug.Log($"断开: {peer.EndPoint}, 原因={info.Reason}");

        listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            var msg = reader.GetString();
            UnityEngine.Debug.Log($"[ch{channel}] {msg}");
            reader.Recycle(); // 不设置 AutoRecycle 时必须手动回收
        };

        _netManager = new NetManager(listener)
        {
            AutoRecycle = true,          // 自动回收 reader，省去手动 Recycle
            UnconnectedMessagesEnabled = false,
            IPv6Enabled = true,
            DisconnectTimeout = 5000,
        };

        if (!_netManager.Start(port))
            UnityEngine.Debug.LogError("端口占用或启动失败");
    }

    public void Update() => _netManager.PollEvents();   // 每帧调用

    public void Stop() => _netManager.Stop();
}
```

### 4.2 客户端

```csharp
using LiteNetLib;
using LiteNetLib.Utils;

public class Client
{
    private NetManager _netManager;
    private NetPeer _serverPeer;

    public void Start()
    {
        var listener = new EventBasedNetListener();

        listener.PeerConnectedEvent += peer =>
        {
            _serverPeer = peer;

            var writer = new NetDataWriter();
            writer.Put("Hello from client!");
            // 通道 0，可靠有序
            peer.Send(writer, 0, DeliveryMethod.ReliableOrdered);
        };

        listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            // 处理服务器消息...
            reader.Recycle();
        };

        _netManager = new NetManager(listener) { AutoRecycle = true };
        _netManager.Start();

        // key 会在服务端 AcceptIfKey 里校验
        _netManager.Connect("127.0.0.1", 9050, "SomeSecretKey");
    }

    public void Update() => _netManager.PollEvents();
    public void Stop() => _netManager.Stop();
}
```

### 4.3 不使用事件派发（实现接口，略快）

```csharp
public class MyListener : INetEventListener
{
    public void OnPeerConnected(NetPeer peer) { }
    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }
    public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError) { }
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) { }
    public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnConnectionRequest(ConnectionRequest request) { }
    // 以下三个在接口里已有默认空实现，可按需覆写：
    // OnMessageDelivered / OnNtpResponse / OnPeerAddressChanged
}
```

---

## 5. 数据读写：`NetDataWriter` / `NetDataReader`

所有业务数据都靠这一对类序列化。`Put` 系列写入，`Get` 系列读取，**读写顺序必须严格一致**。

```csharp
var writer = new NetDataWriter();   // 也可 new NetDataWriter(true, 128) 指定初始容量
writer.Put(42);                     // int
writer.Put("玩家A");                 // string（UTF-8 + 长度前缀）
writer.Put(3.14f);                  // float
writer.Put((byte)7);
peer.Send(writer, 0, DeliveryMethod.ReliableOrdered);
```

```csharp
listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
{
    int id = reader.GetInt();
    string name = reader.GetString();
    float x = reader.GetFloat();
};
```

要点：

- **不是一个 `Put` 一个 `Get` 就够**：数组、`Guid`、`IPEndPoint` 等复合类型有专门的成对方法。
- `Put`/`Get` 多字节整数使用**主机字节序**（通过 `FastBitConverter` 直写内存）。
- `NetDataWriter` 支持 `Reset()` 复用，避免反复分配（`ReuseSet` 模式）；`NetDataReader` 有 `Position` 与错误位置跟踪。
- 详细方法清单见 `API_Reference.md` 的对应章节。

---

## 6. 通道与投递方式

### 投递方式 `DeliveryMethod`

| 枚举值 | 数值 | 语义 | 可否分片 |
| --- | --- | --- | --- |
| `Unreliable` | 4 | 不可靠、可能丢失、可能重复、**可能乱序** | 否（受 MTU 限制） |
| `ReliableUnordered` | 0 | 可靠、不重复、**可能乱序** | 是 |
| `Sequenced` | 1 | 不可靠、不重复、**按序到达** | 否 |
| `ReliableOrdered` | 2 | 可靠、不重复、**按序到达** | 是 |
| `ReliableSequenced` | 3 | 仅保证**最后一个包**到达（中间包可丢）、不重复、按序 | **不可分片** |

`ReliableSequenced` 适合"只关心最新状态"的同步（如移动位置快照）；`ReliableOrdered` 适合指令/事件流。

### 通道（仅 `NetManager` / `NetPeer`）

`ChannelsCount`（1–64，超出会抛 `ArgumentException`）把可靠通道按类型隔离：

```
通道数组下标 = channelNumber * NetConstants.ChannelTypeCount + (int)DeliveryMethod
```

也就是说**每个通道内部对 4 种投递方式各有独立的状态机**，通道之间互不阻塞（一个通道拥塞不会卡住另一个通道的可靠队列）。

---

## 7. 常见配置项速查（`LiteNetManager` 字段）

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `UpdateTime` | 15 | 逻辑更新与发送周期（毫秒） |
| `PingInterval` | 1000 | 延迟探测/连接检查间隔（毫秒） |
| `DisconnectTimeout` | 5000 | 超过此时长无任何包（含保活）则断开（毫秒） |
| `ReconnectDelay` | 500 | 初始连接重试间隔（毫秒） |
| `MaxConnectAttempts` | 10 | 最大连接尝试次数 |
| `AutoRecycle` | false | 是否在回调后自动回收 `NetPacketReader` |
| `UnconnectedMessagesEnabled` | false | 是否接收无连接消息 |
| `BroadcastReceiveEnabled` | false | 是否接收广播 |
| `NatPunchEnabled` | false | 是否处理 NAT 打洞消息 |
| `IPv6Enabled` | true | 是否启用 IPv6 |
| `UnsyncedEvents` | false | 事件是否在接收线程立即回调 |
| `UnsyncedReceiveEvent` | false | 仅接收事件立即回调 |
| `UnsyncedDeliveryEvent` | false | 仅送达事件立即回调 |
| `MtuOverride` | 0 | 强制 MTU（0 = 自动，会忽略 MTU 探测） |
| `MtuDiscovery` | false | 是否自动探测 MTU |
| `UseNativeSockets` | false | 用原生 socket 直调加速（仅 Windows/Linux） |
| `ReuseAddress` | false | UDP `ReuseAddress` |
| `DontRoute` | false | 仅本地子网投递 |
| `DisconnectOnUnreachable` | false | 收到 Host/NetworkUnreachable 时是否断开 |
| `AllowPeerAddressChange` | false | 是否允许对端 IP 变化（移动网络切换），仅服务端 |
| `EnableStatistics` | false | 是否统计流量 |
| `MaxFragmentsCount` | `ushort.MaxValue` | 可靠通道分片数上限 |
| `MaxPacketPerManualReceive` | 256 | 每次手动收包上限（0 = 无限） |

---

## 8. Unity 平台注意事项

1. **`UNITY_SOCKET_FIX`**：当定义了 `UNITY_2018_3_OR_NEWER` 时，源码首行会自动 `#define UNITY_SOCKET_FIX`，此时 `LiteNetManager` 构造函数多一个参数 `bool useSocketFix = true`。它用 `PausedSocketFix` 处理移动端（Android/iOS）**应用切后台导致 socket 失效**的问题——切回前台时重建 socket 并更新 peer 端点。桌面平台可传 `false`。
2. **原生 socket**：`UseNativeSockets` 仅 Windows/Linux 有效，由 `NativeSocket` 通过 P/Invoke 直调 `recvfrom`/`sendto`，可显著降低 GC 压力；其他平台会被忽略。
3. **IL2CPP / 代码裁剪**：`Utils/Preserve.cs` 里的 `PreserveAttribute` 用于防止裁剪器删掉反射用到的成员；`Trimming.cs`（仅 `NET5_0_OR_GREATER`）声明了序列化器需要的 `DynamicallyAccessedMemberTypes`。
4. **Unity 网络模拟**：`SimulatePacketLoss` / `SimulateLatency` / `SimulationPacketLossChance` / `SimulationMinLatency` / `SimulationMaxLatency` 只在 `DEBUG` 或定义了 `SIMULATE_NETWORK` 时生效（相关方法带 `[Conditional("DEBUG"), Conditional("SIMULATE_NETWORK")]`）。
5. **日志默认是关闭的**：`NetDebug.Write*` 标了 `[Conditional("DEBUG_MESSAGES")]`，**除非显式定义 `DEBUG_MESSAGES`，否则所有日志调用会在编译期被整体剔除**。排查网络问题时第一步就是加上这个宏。
6. **`NetDebug` 的输出目标**：定义了 `UNITY_5_3_OR_NEWER` 时走 `UnityEngine.Debug.Log`，否则走 `Console.WriteLine`，并且支持通过 `NetDebug.Logger` 注入自定义 `INetLogger`。
7. **IPv6 能力探测**（`LiteNetManager.Socket.cs` 静态构造函数）：优先级为 `DISABLE_IPV6`（直接禁用）→ 老版本 Unity + IL2CPP（按 `Application.unityVersion` 次版本号 ≥ 6 判断）→ 否则用 `Socket.OSSupportsIPv6`。
8. **其它平台开关**：`UNITY_SWITCH` 下 TTL 的 getter 返回 0、setter 为空操作；`UNITY_ANDROID` 下 `FastBitConverter` 改用 `UnsafeUtility.MemCpy` 以避免非对齐指针访问异常。

---

## 9. 目录结构

```
Assets/Scripts/Net/
├── LiteNetManager.cs            # 核心管理器（partial 主体）
├── LiteNetManager.Socket.cs     # partial：socket 创建、收发循环、分片、MTU
├── LiteNetManager.HashSet.cs    # partial：peer 集合哈希表
├── LiteNetManager.PacketPool.cs # partial：NetPacket 对象池
├── NetManager.cs                # 增强管理器（多通道 + NTP）
├── LiteNetPeer.cs               # 基础 peer（继承 IPEndPoint）
├── NetPeer.cs                   # 增强 peer（多通道）
├── INetEventListener.cs         # 监听接口 + 事件式监听器 + 枚举/结构
├── NetEvent.cs                  # 内部事件对象
├── ConnectionRequest.cs         # 连接请求（Lite + 增强）
├── NatPunchModule.cs            # NAT 打洞
├── NetConstants.cs              # 协议常量与 DeliveryMethod
├── NetDebug.cs                  # 日志与异常类型
├── NetUtils.cs                  # 地址/网卡工具
├── NetStatistics.cs             # 流量统计
├── NetPacket.cs                 # 包结构与 PacketProperty
├── InternalPackets.cs           # 内部协议包
├── NetPacketReader.cs           # 收包读取器
├── PooledPacket.cs              # 池化包句柄
├── BaseChannel.cs               # 通道基类
├── ReliableChannel.cs           # 可靠通道
├── SequencedChannel.cs          # 顺序（sequenced）通道
├── NativeSocket.cs              # 原生 socket P/Invoke
├── PausedSocketFix.cs           # 移动端暂停恢复
├── Trimming.cs                  # 裁剪相关（NET5+）
├── LiteNetLib.asmdef
├── Utils/                       # 序列化与工具
│   ├── NetDataWriter.cs / NetDataReader.cs
│   ├── NetSerializer.cs / NetPacketProcessor.cs
│   ├── FastBitConverter.cs / CRC32C.cs
│   ├── INetSerializable.cs / Preserve.cs
│   └── NtpPacket.cs / NtpRequest.cs
└── Layers/                      # 可选包处理层
    ├── PacketLayerBase.cs
    ├── Crc32cLayer.cs
    └── XorEncryptLayer.cs
```

---

## 10. 延伸阅读

- 完整 API（每个类型、每个成员的精确签名与语义）：[`API_Reference.md`](./API_Reference.md)
