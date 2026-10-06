# LiteNetLib API 参考（Pikatown3D 内嵌版本）

> **来源声明**：本文档**完全**依据 `Assets/Scripts/Net/` 目录内的源码整理（38 个 `.cs` 文件）。
> 与上游 LiteNetLib 官方文档可能存在差异，**一切以本目录源码为准**。
> 上手教程与架构概览见同目录的 [`README.md`](./README.md)。

---

## 目录

1. [总览与类型索引](#1-总览与类型索引)
2. [`NetConstants` 与 `DeliveryMethod`](#2-netconstants-与-deliverymethod)
3. [监听接口与事件类型](#3-监听接口与事件类型)
   - [`UnconnectedMessageType`](#31-unconnectedmessagetype)
   - [`DisconnectReason`](#32-disconnectreason)
   - [`DisconnectInfo`](#33-disconnectinfo)
   - [`ILiteNetEventListener`](#34-ilistenneteventlistener--ilistenneteventlistener)
   - [`INetEventListener`](#35-ineteventlistener)
   - [`EventBasedLiteNetListener`](#36-eventbasedlitenetlistener)
   - [`EventBasedNetListener`](#37-eventbasednetlistener)
4. [`LiteNetManager`](#4-litenetmanager)
5. [`NetManager`](#5-netmanager)
6. [`LiteNetPeer`](#6-litenetpeer)
7. [`NetPeer`](#7-netpeer)
8. [`LiteConnectionRequest` / `ConnectionRequest`](#8-liconnectionrequest--connectionrequest)
9. [`NatPunchModule` 与 NAT 打洞](#9-natpunchmodule-与-nat-打洞)
10. [`NetDataWriter`](#10-netdatawriter)
11. [`NetDataReader`](#11-netdatareader)
12. [`NetPacketReader`](#12-netpacketreader)
13. [`NetSerializer`](#13-netserializer)
14. [`NetPacketProcessor`](#14-netpacketprocessor)
15. [`INetSerializable`](#15-inetserializable)
16. [`NetPacket` 与协议头布局](#16-netpacket-与协议头布局)
17. [通道实现](#17-通道实现)
18. [`PacketLayerBase` 与内置层](#18-packetlayerbase-与内置层)
19. [`CRC32C` / `FastBitConverter`](#19-crc32c--fastbitconverter)
20. [NTP 对时](#20-ntp-对时)
21. [`NatPunchModule` 内部包](#21-natpunchmodule-内部包)
22. [工具类：`NetUtils` / `NetDebug` / `NetStatistics`](#22-工具类netutils--netdebug--netstatistics)
23. [平台适配：`NativeSocket` / `PausedSocketFix` / `PooledPacket` / `Trimming` / `Preserve`](#23-平台适配)
24. [附录 A：条件编译符号总表](#附录-a条件编译符号总表)
25. [附录 B：文件清单](#附录-b文件清单)

---

## 1. 总览与类型索引

| 命名空间 | 类型 | 可见性 | 说明 |
| --- | --- | --- | --- |
| `LiteNetLib` | `LiteNetManager` | public partial class | 核心管理器（4 个 partial 文件） |
| `LiteNetLib` | `NetManager : LiteNetManager` | public class | 增强管理器：多通道 + NTP |
| `LiteNetLib` | `LiteNetPeer : IPEndPoint` | public class | 基础 peer |
| `LiteNetLib` | `NetPeer : LiteNetPeer` | public class | 增强 peer：多通道 |
| `LiteNetLib` | `LiteConnectionRequest` | public class | 连接请求基类 |
| `LiteNetLib` | `ConnectionRequest : LiteConnectionRequest` | public class | 连接请求（返回 `NetPeer`） |
| `LiteNetLib` | `ILiteNetEventListener` | public interface | 基础监听接口 |
| `LiteNetLib` | `INetEventListener : ILiteNetEventListener` | public interface | 增强监听接口 |
| `LiteNetLib` | `EventBasedLiteNetListener` | public class | 事件式监听器（基础） |
| `LiteNetLib` | `EventBasedNetListener` | public class | 事件式监听器（增强） |
| `LiteNetLib` | `DeliveryMethod : byte` | public enum | 投递方式 |
| `LiteNetLib` | `ConnectionState : byte` | public enum | 连接状态位标志 |
| `LiteNetLib` | `DisconnectReason` | public enum | 断开原因 |
| `LiteNetLib` | `UnconnectedMessageType` | public enum | 无连接消息类型 |
| `LiteNetLib` | `DisconnectInfo` | public struct | 断开附加信息 |
| `LiteNetLib` | `NetConstants` | public static class | 协议常量 |
| `LiteNetLib` | `NetDebug` | public static class | 日志 |
| `LiteNetLib` | `NetLogLevel` | public enum | 日志级别 |
| `LiteNetLib` | `INetLogger` | public interface | 自定义日志接口 |
| `LiteNetLib` | `InvalidPacketException : ArgumentException` | public class | 坏包异常 |
| `LiteNetLib` | `TooBigPacketException : InvalidPacketException` | public class | 包过大异常 |
| `LiteNetLib` | `NetUtils` | public static class | 地址/网卡工具 |
| `LiteNetLib` | `LocalAddrType` | public enum | 本地地址类型 |
| `LiteNetLib` | `NetStatistics` | public sealed class | 流量统计 |
| `LiteNetLib` | `NetPacketReader : NetDataReader` | public sealed class | 收包读取器 |
| `LiteNetLib` | `PooledPacket` | public readonly ref struct | 池化包句柄 |
| `LiteNetLib` | `NatPunchModule` | public sealed class | NAT 打洞模块 |
| `LiteNetLib` | `INatPunchListener` | public interface | NAT 事件接口 |
| `LiteNetLib` | `EventBasedNatPunchListener` | public class | NAT 事件式监听器 |
| `LiteNetLib` | `NatAddressType` | public enum | NAT 地址类型 |
| `LiteNetLib` | `NetConnectRequestPacket` | public sealed class | 连接请求包（公开用于校验） |
| `LiteNetLib` | `NetEvent` | public sealed class | 内部事件对象 |
| `LiteNetLib` | `PausedSocketFix` | public class | 移动端暂停恢复 |
| `LiteNetLib.Utils` | `NetDataWriter` | public class | 序列化写入器 |
| `LiteNetLib.Utils` | `NetDataReader` | public class | 反序列化读取器 |
| `LiteNetLib.Utils` | `NetSerializer` | public class | 自动序列化器 |
| `LiteNetLib.Utils` | `NetPacketProcessor` | public class | 包类型路由/订阅 |
| `LiteNetLib.Utils` | `INetSerializable` | public interface | 自定义序列化契约 |
| `LiteNetLib.Utils` | `FastBitConverter` | public static class | 快速字节转换 |
| `LiteNetLib.Utils` | `CRC32C` | public static class | CRC32C 校验 |
| `LiteNetLib.Utils` | `NtpPacket` | public class | NTP 包 |
| `LiteNetLib.Utils` | `NtpLeapIndicator` / `NtpMode` | public enum | NTP 枚举 |
| `LiteNetLib.Utils` | `PreserveAttribute : Attribute` | public class | 防裁剪标记 |
| `LiteNetLib.Utils` | `InvalidTypeException` / `ParseException` | public class | 序列化异常 |
| `LiteNetLib.Layers` | `PacketLayerBase` | public abstract class | 包处理层基类 |
| `LiteNetLib.Layers` | `Crc32cLayer : PacketLayerBase` | public sealed class | 内置 CRC 层 |
| `LiteNetLib.Layers` | `XorEncryptLayer : PacketLayerBase` | public class | 内置 XOR 加密层 |

**内部类型**（`internal`，外部不可直接使用，仅在本文档中说明其行为）：
`BaseChannel`、`ReliableChannel`、`SequencedChannel`、`NetPacket`、`PacketProperty`、`NetConnectAcceptPacket`、`NtpRequest`、`NativeSocket`、`NetworkSorter`、`Trimming`、`MergedPacketUserData`，以及 `LiteNetManager` 内部的 `ConnectRequestResult` / `DisconnectResult` / `ShutdownResult` / `NetPeerEnumerator<T>`（后者为 public）。

---

## 2. `NetConstants` 与 `DeliveryMethod`

### `DeliveryMethod : byte`

投递语义枚举。**数值有含义**：低 2 位即通道类型索引，`LiteNetPeer`/`NetPeer` 用 `channelNumber * NetConstants.ChannelTypeCount + (int)deliveryMethod` 定位通道状态机。

| 成员 | 值 | 语义 | 可分片 |
| --- | --- | --- | --- |
| `Unreliable` | 4 | 不可靠。可能丢失、可能重复、**可能乱序** | ❌ |
| `ReliableUnordered` | 0 | 可靠。不丢、不重复、**可能乱序** | ✅ |
| `Sequenced` | 1 | 不可靠。可能丢失、不重复、**按序到达** | ❌ |
| `ReliableOrdered` | 2 | 可靠有序。不丢、不重复、**按序到达** | ✅ |
| `ReliableSequenced` | 3 | 只保证**最后一个包**送达（中间包可丢）、不重复、按序。**不可分片** | ❌ |

### `NetConstants`（`public static class`）

**公开常量**

| 成员 | 类型 | 值 | 说明 |
| --- | --- | --- | --- |
| `DefaultWindowSize` | `const int` | 64 | 可靠通道默认窗口大小（包数） |
| `SocketBufferSize` | `const int` | 1048576（1 MB） | UDP socket 收发缓冲区大小 |
| `SocketTTL` | `const int` | 255 | UDP 包的 TTL |
| `HeaderSize` | `const int` | 1 | 基础包头（`PacketProperty`）字节数 |
| `ChanneledHeaderSize` | `const int` | 4 | 顺序/可靠包头：含 `HeaderSize` + Sequence + ChannelId |
| `FragmentHeaderSize` | `const int` | 6 | 分片附加头：FragmentId + FragmentPart + FragmentsTotal |
| `FragmentedHeaderTotalSize` | `const int` | 10 | = `ChanneledHeaderSize + FragmentHeaderSize` |
| `MaxSequence` | `const ushort` | 32768 | 序列号回绕上限 |
| `HalfMaxSequence` | `const ushort` | 16384 | 用于序列号比较与回绕判断 |
| `MaxConnectionNumber` | `const byte` | 4 | `NetPacket.ConnectionNumber` 的最大值；用于区分同一 `IPEndPoint` 的不同连接实例，丢弃网络抖动带来的旧连接迟到包 |

**公开静态只读字段**

| 成员 | 类型 | 值 | 说明 |
| --- | --- | --- | --- |
| `InitialMtu` | `static readonly int` | `PossibleMtu[0]` = **1024** | 新连接在 MTU 探测前使用的起始 MTU |
| `MaxPacketSize` | `static readonly int` | `PossibleMtu[last]` = 1500 − 68 = **1432** | 库允许的最大包尺寸 |
| `MaxUnreliableDataSize` | `static readonly int` | `MaxPacketSize - HeaderSize` = **1431** | 单个不可靠包的最大负载 |

> `PossibleMtu`（internal）候选值：`1024`、`1232-68`、`1460-68`、`1472-68`、`1492-68`、`1500-68`。
> 其中 `MaxUdpHeaderSize = 68` 为 internal 常量。

---

## 4. `LiteNetManager`

```csharp
namespace LiteNetLib
public partial class LiteNetManager : IEnumerable<LiteNetPeer>
```

**4 个 partial 文件**：

| 文件 | 行数 | 职责 |
| --- | ---: | --- |
| `LiteNetManager.cs` | 1672 | 主体：配置字段、Start/Stop、Connect、事件队列与派发、SendToAll、Disconnect |
| `LiteNetManager.Socket.cs` | 726 | socket 创建与配置、收发循环、MTU 探测、分片重组、SendRaw* |
| `LiteNetManager.HashSet.cs` | 323 | peer 集合哈希表（`AddPeer`/`RemovePeer`/`TryGetPeer`） |
| `LiteNetManager.PacketPool.cs` | 82 | `NetPacket` 对象池 |

### 4.1 配置字段（全部 public，可读写）

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `UnconnectedMessagesEnabled` | `bool` | `false` | 是否接收无连接消息 |
| `NatPunchEnabled` | `bool` | `false` | 是否处理 NAT 打洞消息 |
| `UpdateTime` | `int` | `15` | 逻辑更新与发送周期（毫秒）。Windows 上受 `Thread.Sleep` 精度限制，更低值收益有限 |
| `PingInterval` | `int` | `1000` | 延迟探测与连接检查间隔（毫秒） |
| `DisconnectTimeout` | `int` | `5000` | 超过此时长未收到任何包（含库内保活包）则关闭连接（毫秒） |
| `SimulatePacketLoss` | `bool` | `false` | 模拟丢包（仅 DEBUG 或 `SIMULATE_NETWORK`） |
| `SimulateLatency` | `bool` | `false` | 模拟延迟（仅 DEBUG 或 `SIMULATE_NETWORK`） |
| `SimulationPacketLossChance` | `int` | `10` | 丢包概率（百分比 1–100） |
| `SimulationMinLatency` | `int` | `30` | 模拟最小**往返**延迟（毫秒）；实际单向为一半 |
| `SimulationMaxLatency` | `int` | `100` | 模拟最大**往返**延迟（毫秒）；实际单向为一半 |
| `UnsyncedEvents` | `bool` | `false` | 事件是否在接收线程立即回调（不经过 `PollEvents`） |
| `UnsyncedReceiveEvent` | `bool` | `false` | 仅接收事件立即回调 |
| `UnsyncedDeliveryEvent` | `bool` | `false` | 仅送达事件立即回调 |
| `BroadcastReceiveEnabled` | `bool` | `false` | 是否接收广播包 |
| `ReconnectDelay` | `int` | `500` | 初始连接重试间隔（毫秒） |
| `MaxConnectAttempts` | `int` | `10` | 最大连接尝试次数 |
| `ReuseAddress` | `bool` | `false` | socket `ReuseAddress` 选项 |
| `DontRoute` | `bool` | `false` | 仅本地子网投递（UDP `DontRoute`） |
| `EnableStatistics` | `bool` | `false` | 是否统计流量 |
| `MaxFragmentsCount` | `ushort` | `ushort.MaxValue` | 可靠通道分片数上限 |
| `AutoRecycle` | `bool` | `false` | 是否在回调后自动回收 `NetPacketReader` |
| `IPv6Enabled` | `bool` | `true` | 是否启用 IPv6 |
| `MtuOverride` | `int` | `0` | 强制 MTU（0 = 不强制）。**会忽略 MTU 探测** |
| `MtuDiscovery` | `bool` | `false` | 是否自动探测 MTU。源码注释警告：部分路由器会破坏 MTU 探测并导致连接失败 |
| `UseNativeSockets` | `bool` | `false` | 直调原生 socket 收发（仅 Windows/Linux），显著提速并降低 GC 压力 |
| `DisconnectOnUnreachable` | `bool` | `false` | 收到 Host/NetworkUnreachable 时是否断开（0.9.x 旧行为为 `true`） |
| `AllowPeerAddressChange` | `bool` | `false` | 允许对端 IP 变化（LTE/WiFi 切换）。**仅服务端使用** |
| `MaxPacketPerManualReceive` | `int` | `256` | 每次手动收包上限；`0` = 无限（包过多时可能造成明显延迟） |

### 4.2 只读属性

```csharp
public readonly NetStatistics Statistics = new NetStatistics();   // 所有连接的统计（字段，非属性）
public NatPunchModule NatPunchModule { get; }                    // 懒加载（Lazy<NatPunchModule>）
public bool IsRunning { get; }                                   // socket 监听 + 更新线程是否运行
public int  LocalPort { get; private set; }                      // 本地端口
public LiteNetPeer FirstPeer { get; }                            // 首个 peer（客户端模式常用）
public int  ConnectedPeersCount { get; }                         // Interlocked.Read 读取
public int  ExtraPacketSizeForLayer { get; }                     // 当前 PacketLayerBase 的额外开销字节数，无层时为 0
```

> `NetStatistics Statistics` 是 **`public readonly` 字段**，而 `ConnectedPeersCount` 是属性。

### 4.3 构造函数

```csharp
#if UNITY_SOCKET_FIX
public LiteNetManager(ILiteNetEventListener listener, PacketLayerBase extraPacketLayer = null, bool useSocketFix = true)
#else
public LiteNetManager(ILiteNetEventListener listener, PacketLayerBase extraPacketLayer = null)
#endif
```

> ⚠️ `UNITY_2018_3_OR_NEWER` 时该文件顶部会本地 `#define UNITY_SOCKET_FIX`，因此在任何 Unity 2018.3+ 工程里**都存在第三个参数 `useSocketFix = true`**。
> `listener` 也可实现送达事件接口（源码注释原文："also can implement IDeliveryEventListener"，但**该接口在本目录中不存在**）。
> `extraPacketLayer`：所有互连的 manager **必须使用相同的层**。

### 4.4 启动 / 停止

```csharp
public bool Start();                                                  // => Start(0)
public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port);
public bool Start(string addressIPv4, string addressIPv6, int port);  // 内部 ResolveAddress 后转 IPAddress 版
public bool Start(int port);                                          // => Start(IPAddress.Any, IPAddress.IPv6Any, port)

public bool StartInManualMode(IPAddress addressIPv4, IPAddress addressIPv6, int port);
public bool StartInManualMode(string addressIPv4, string addressIPv6, int port);
public bool StartInManualMode(int port);

public void Stop();                          // => Stop(true)
public void Stop(bool sendDisconnectMessages);
public void TriggerUpdate();                 // => _updateTriggerEvent.Set()（异步立即触发一次更新）
```

- `Start` 返回 `bool`，失败（如端口占用）返回 `false`。
- **手动模式**下应使用 `ManualReceive`（源码注释如此表述）+ `ManualUpdate(...)`，而不是 `PollEvents`。
- `Stop(bool)`：先对所有 peer 调 `Shutdown`，再 `CloseSocket()`，最后（非手动模式）`_logicThread.Join()` 并清理 peer 集合与事件队列。内部会临时把 `SimulateLatency` 置 `false`，避免断开包被延迟模拟拦截。

### 4.5 驱动与事件泵

```csharp
public void PollEvents();
public void ManualUpdate(float elapsedMilliseconds);
```

`PollEvents()` 的三条分支：

1. **手动模式**：对 v4、v6 socket 各调一次 `ManualReceive`，再 `ProcessDelayedPackets()`，然后**直接 return**（不走事件队列）。
2. **`UnsyncedEvents == true`**：直接 return（事件已在接收线程派发完）。
3. 否则：从 `_pendingEventHead` 摘下整条链表（`lock (_eventLock)` 内），逐个 `ProcessEvent`。**派发在主线程/调用线程**。

`ManualUpdate(float elapsedMilliseconds)`：**仅在手动模式下生效**（非手动模式直接 return）。遍历所有 peer，超时的移除、否则 `peer.Update(elapsedMs)`，最后 `ProcessNtpRequests(elapsedMs)`。

### 4.6 连接

```csharp
public LiteNetPeer Connect(string address, int port, string key);
public LiteNetPeer Connect(string address, int port, NetDataWriter connectionData);
public LiteNetPeer Connect(IPEndPoint target, string key);
public LiteNetPeer Connect(IPEndPoint target, NetDataWriter connectionData);
public LiteNetPeer Connect(IPEndPoint target, ReadOnlySpan<byte> connectionData);
```

**返回值语义**（源码注释）：新连接返回新 peer；已连接返回旧 peer；若该地址已有 `ConnectionRequest` 待处理则返回 **`null`**。

- `_isRunning == false` 时抛 **`InvalidOperationException("Client is not running")`**。
- `string` 版本中 DNS 解析失败（`NetUtils.MakeEndPoint` 抛异常）**不会抛出**，而是创建一条 `Disconnect` 事件（`reason = UnknownHost`）并返回 `null`。
- 已有 peer 处于 `Connected` / `Outgoing` → 直接返回该 peer；其它状态 → 递增 `ConnectionNumber` 并 `RemovePeer(peer, true)` 后重连。
- 在 `_requestsDict` 中已存在同一 endpoint 的请求 → 返回 `null`。

### 4.7 发送

```csharp
public void SendToAll(NetDataWriter writer, DeliveryMethod options);
public void SendToAll(byte[] data, DeliveryMethod options);
public void SendToAll(byte[] data, int start, int length, DeliveryMethod options);
public void SendToAll(NetDataWriter writer, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(byte[] data, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(byte[] data, int start, int length, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(ReadOnlySpan<byte> data, DeliveryMethod options);
public void SendToAll(ReadOnlySpan<byte> data, DeliveryMethod options, LiteNetPeer excludePeer);

public bool SendUnconnectedMessage(ReadOnlySpan<byte> message, IPEndPoint remoteEndPoint);
public bool SendUnconnectedMessage(byte[] message, IPEndPoint remoteEndPoint);
public bool SendUnconnectedMessage(byte[] message, int start, int length, IPEndPoint remoteEndPoint);
public bool SendUnconnectedMessage(NetDataWriter writer, IPEndPoint remoteEndPoint);
public bool SendUnconnectedMessage(NetDataWriter writer, string address, int port);
```

- 以上 `SendToAll` 全部走**通道 0**（源码注释明示"channel - 0"）；遍历 peer 时持有 `_peersLock` 读锁。
- `SendUnconnectedMessage(NetDataWriter, string, int)` 的注释警告：**每次调用都会新建 `IPEndPoint` 并同步做 DNS 查询**；每帧调用时应自行缓存 `IPEndPoint`。
- `SendUnconnectedMessage(ReadOnlySpan<byte>, IPEndPoint)` 会**新建包并拷贝**数据，故不需要 CRC（注释："No need for CRC here, SendRaw does that"）。

### 4.8 断开

```csharp
public void DisconnectAll();
public void DisconnectAll(byte[] data, int start, int count);
public void DisconnectAll(ReadOnlySpan<byte> data);
public void DisconnectPeerForce(LiteNetPeer peer);
public void DisconnectPeer(LiteNetPeer peer);
public void DisconnectPeer(LiteNetPeer peer, byte[] data);
public void DisconnectPeer(LiteNetPeer peer, NetDataWriter writer);
public void DisconnectPeer(LiteNetPeer peer, byte[] data, int start, int count);
public void DisconnectPeer(LiteNetPeer peer, ReadOnlySpan<byte> data);
```

- `DisconnectAll` / `DisconnectPeer` 走**非强制路径**：状态置 `ShutdownRequested`，按 `DisconnectTimeout` 持续发送不可靠断开包，peer 会滞留以忽略旧会话的迟到包。
- `DisconnectPeerForce` **立即**置为 `Disconnected` 且不发送通知。
- 附加数据大小**必须 ≤ MTU − 8**。
- 内部最终都会调 `peer.Shutdown(data, force)`；`WasConnected` 时递减 `_connectedPeersCount` 并创建 `Disconnect` 事件（reason = `DisconnectPeerCalled`）。

### 4.9 查询

```csharp
public int  GetPeersCount(ConnectionState peerState);              // 按位与匹配
public void GetPeers(List<LiteNetPeer> peers, ConnectionState peerState);   // 会先 peers.Clear()
public void GetConnectedPeers(List<LiteNetPeer> peers);            // => GetPeers(peers, Connected)
public LiteNetPeer GetPeerById(int id);                            // 见 LiteNetManager.HashSet.cs
public bool TryGetPeerById(int id, out LiteNetPeer peer);          // 见 LiteNetManager.HashSet.cs
public NetPeerEnumerator<LiteNetPeer> GetEnumerator();
```

`ConnectionState` 可按**位标志**组合使用（`(netPeer.ConnectionState & peerState) != 0`）。
`GetEnumerator()` 允许 `foreach (var peer in manager)` 直接遍历 peer 链表。

### 4.10 嵌套类型 `NetPeerEnumerator<T>`

```csharp
public struct NetPeerEnumerator<T> : IEnumerator<T> where T : LiteNetPeer
{
    public NetPeerEnumerator(T p);
    public void Dispose();                 // 空实现
    public bool MoveNext();                // 沿 NextPeer 前进
    public void Reset();                   // 抛 NotSupportedException
    public T Current { get; }
    object IEnumerator.Current { get; }
}
```

### 4.11 受保护虚工厂（供子类替换 peer/request 类型）

```csharp
protected virtual LiteNetPeer CreateOutgoingPeer(IPEndPoint remoteEndPoint, int id, byte connectNum, ReadOnlySpan<byte> connectData);
protected virtual LiteNetPeer CreateIncomingPeer(LiteConnectionRequest request, int id);
protected virtual LiteNetPeer CreateRejectPeer(IPEndPoint remoteEndPoint, int id);
protected virtual LiteConnectionRequest CreateConnectionRequest(IPEndPoint remoteEndPoint, NetConnectRequestPacket requestPacket);

protected virtual void ProcessEvent(NetEvent evt);
protected virtual void ProcessNtpRequests(float elapsedMilliseconds);   // 基类为空实现
internal virtual bool CustomMessageHandle(NetPacket packet, IPEndPoint remoteEndPoint);   // 基类返回 false
```

`NetManager` 覆写前 4 个以产出 `NetPeer` / `ConnectionRequest`。

### 4.12 内部事件机制（理解线程模型的关键）

```csharp
private void CreateEvent(NetEvent.EType type, LiteNetPeer peer = null, IPEndPoint remoteEndPoint = null,
    SocketError errorCode = 0, int latency = 0, DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed,
    LiteConnectionRequest connectionRequest = null, DeliveryMethod deliveryMethod = DeliveryMethod.Unreliable,
    byte channelNumber = 0, NetPacket readerSource = null, object userData = null);
```

- 事件对象从 `_netEventPoolHead` 空闲链表复用（`lock (_eventLock)`）。
- `type == Connect` → `Interlocked.Increment(ref _connectedPeersCount)`。
- `type == MessageDelivered` → 单独改用 `UnsyncedDeliveryEvent` 决定是否同步派发。
- `if (unsyncEvent || _manualMode) ProcessEvent(evt); else` 入队到 `_pendingEventHead/Tail`。
- 派发后的回收逻辑：`if (emptyData) RecycleEvent(evt); else if (AutoRecycle) evt.DataReader.RecycleInternal();`
  （`emptyData` 由 `evt.DataReader.IsNull` 判定。）

### 4.13 收包协议分派（`HandleMessageReceived`）

顺序：`EnableStatistics` 计数 → `CustomMessageHandle` → `extraPacketLayer.ProcessInboundPacket` → `packet.Verify()` → 按 `Property` 分派。

**无连接类**（不查 peer）

| `PacketProperty` | 处理 |
| --- | --- |
| `ConnectRequest` | 校验 `GetProtocolId == ProtocolId(13)`，不符则回 `InvalidProtocol` |
| `Broadcast` | `BroadcastReceiveEnabled` 时创建 `Broadcast` 事件，否则丢弃 |
| `UnconnectedMessage` | `UnconnectedMessagesEnabled` 时创建 `ReceiveUnconnected` 事件，否则丢弃 |
| `NatMessage` | `NatPunchEnabled` 时交由 `NatPunchModule.ProcessMessage` |

**连接类**

| `PacketProperty` | 处理 |
| --- | --- |
| `ConnectRequest` | `NetConnectRequestPacket.FromData` → `ProcessConnectRequest` |
| `PeerNotFound` | 见下方"地址变化/peer 丢失" |
| `InvalidProtocol` | 若 peer 处于 `Outgoing` → 强制断开（reason = `InvalidProtocol`） |
| `Disconnect` | `peer.ProcessDisconnect`，必要时强制断开（`RemoteConnectionClose` / `ConnectionRejected`），并回 `ShutdownOk` |
| `ConnectAccept` | `peer.ProcessConnectAccept` 成功则创建 `Connect` 事件 |
| 其它（默认） | 有 peer → `peer.ProcessPacket(packet)`；无 peer → 回 `PeerNotFound` |

**地址变化（漫游）**：`PeerNotFound` 且 `AllowPeerAddressChange` 时，从包中取出 `PeerId`/`ConnectionTime`/`ConnectionNumber` 与本地 peer 匹配，命中则 `peer.InitiateEndPointChange()` 并创建 `PeerAddressChanged` 事件；事件派发时会用写锁把 peer 从哈希集合中移除、`FinishEndPointChange`、再重新加入。

### 4.14 partial：socket 层（`LiteNetManager.Socket.cs`）

**公开成员**

```csharp
public static readonly bool IPv6Support;
public int ReceivePollingTime = 50000;          // 微秒，0.05 秒
public short Ttl { get; internal set; }
public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool manualMode);
public bool SendBroadcast(NetDataWriter writer, int port);
public bool SendBroadcast(byte[] data, int port);
public bool SendBroadcast(byte[] data, int start, int length, int port);
```

**静态构造函数中的 IPv6 能力探测优先级**

```csharp
#if DISABLE_IPV6
    IPv6Support = false;
#elif !UNITY_2019_1_OR_NEWER && !UNITY_2018_4_OR_NEWER && (!UNITY_EDITOR && ENABLE_IL2CPP)
    IPv6Support = Socket.OSSupportsIPv6 && int.Parse(version.Remove(version.IndexOf('f')).Split('.')[2]) >= 6;
#else
    IPv6Support = Socket.OSSupportsIPv6;
#endif
```

**`Start(...)` 的启动顺序**

```csharp
if (IsRunning && NotConnected == false) return false;   // 已在运行 → 直接失败
NotConnected = false;
_manualMode = manualMode;
UseNativeSockets = UseNativeSockets && NativeSocket.IsSupported;   // 平台不支持时自动降级
_udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
if (!BindSocket(_udpSocketv4, new IPEndPoint(addressIPv4, port))) return false;
LocalPort = ((IPEndPoint)_udpSocketv4.LocalEndPoint).Port;
// 仅当 IPv6Support && IPv6Enabled 才创建 v6 socket，并用同一 LocalPort
```

> 若 `IPv6Support && IPv6Enabled` 为 false，则**完全不创建 v6 socket**；**IPv6 `DualMode` 从不启用**（仅在 `AddressAlreadyInUse` 回退分支里被显式设为 `false`）。

**`BindSocket` 的 socket 配置（精确顺序）**

```csharp
socket.ReceiveTimeout = 500;    // 毫秒
socket.SendTimeout    = 500;    // 毫秒
socket.ReceiveBufferSize = NetConstants.SocketBufferSize;   // 1 MiB
socket.SendBufferSize    = NetConstants.SocketBufferSize;   // 1 MiB
socket.Blocking = true;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    socket.IOControl(SioUdpConnreset, new byte[] { 0 }, null);   // 关闭 SIO_UDP_CONNRESET（try/catch 忽略）

try {
    socket.ExclusiveAddressUse = !ReuseAddress;
    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, ReuseAddress);
    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, DontRoute);
} catch { /* Unity+IL2CPP 会抛异常，忽略 */ }

if (ep.AddressFamily == AddressFamily.InterNetwork)   // 仅 IPv4 socket
{
    Ttl = NetConstants.SocketTTL;                     // 255
    socket.EnableBroadcast = true;
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        socket.DontFragment = true;                   // macOS 除外
}
```

其它行为：

| 场景 | 行为 |
| --- | --- |
| `AddressFamilyNotSupported` | `return true`（注释 `//hack for iOS (Unity3D)`） |
| `AddressAlreadyInUse` 且为 IPv6 | `socket.DualMode = false; socket.Bind(ep);` 后视为成功 |
| IPv6 加入组播组 | 仅当 `!UNITY_SOCKET_FIX` 时执行 `AddMembership`（组地址 `ff02::1`） |
| 任何绑定失败 | `NetDebug.WriteError($"[B]Bind exception: {ex}, errorCode: {ex.SocketErrorCode}")` |
| 非手动模式 | 启动 `ReceiveThread({LocalPort})`（后台线程），并按需启动 `LogicThread` |
| `UNITY_SWITCH` | `Ttl` getter 返回 `0`，setter 为空操作 |

**`ReceivePollingTime`**：默认 `50000` **微秒**（0.05 秒），同时用于 `Socket.Poll(ReceivePollingTime, SelectMode.SelectRead)` 与 `Socket.Select(..., ReceivePollingTime)`。

**接收缓冲**：每次收包都从池中取一个 `NetConstants.MaxPacketSize`（**1432 字节**）的 `NetPacket`。

**三条收包路径**

| 路径 | 说明 |
| --- | --- |
| `ReceiveLogic()`（托管，后台线程） | 单 socket 用 `Poll`；双 socket 用 `Socket.Select` |
| `NativeReceiveLogic()`（原生，后台线程） | 用 `NativeSocket.RecvFrom` + 手工解析 sockaddr（family、port、IPv6 scope） |
| `ManualReceive`（手动模式） | 由 `PollEvents()` 调用，每次最多 `MaxPacketPerManualReceive`（256）个包；`0` 表示不限 |

**`ProcessError` 的错误分类（收包侧）**

| `SocketErrorCode` | 行为 |
| --- | --- |
| `NotConnected` | `NotConnected = true; return true;`（**停止接收循环**，iOS 后台恢复场景） |
| `Interrupted` / `NotSocket` / `OperationAborted` | `return true`（停止循环） |
| `ConnectionReset` / `MessageSize` / `TimedOut` / `NetworkReset` / `WouldBlock` | **静默忽略**，`return false` |
| 其它 | `NetDebug.WriteError($"[R]Error code: {(int)ex.SocketErrorCode} - {ex}")` + 创建 `Error` 事件，`return false` |

循环级捕获：`SocketException` → `ProcessError`；`ObjectDisposedException` → 直接 return（socket 已关闭）；`ThreadAbortException` → 直接 return；其它 `Exception` → `WriteError("[NM] SocketReceiveThread error: " + e)`。

**发送侧错误映射（`SendRawCore`）**

| `SocketError` | 返回值 / 行为 |
| --- | --- |
| `NoBufferSpaceAvailable` / `Interrupted` | `0` |
| `MessageSize` | `0`，并记 trace `[SRD] 10040, datalen: {length}` |
| `HostUnreachable` / `NetworkUnreachable` | `DisconnectOnUnreachable` 时强制断开该 peer，创建 `Error` 事件，返回 `-1` |
| `Shutdown` | 创建 `Error` 事件，返回 `-1` |
| 其它 | `WriteError`，返回 `-1` |
| 非 socket 异常 | 记录日志，返回 `0` |

> `SendRaw` 在 `!_isRunning` 时返回 `0`。启用 `PacketLayerBase` 时会先 `PoolGetPacket(length + ExtraPacketSizeForLayer)` 并把它交给 `ProcessOutBoundPacket`。
> `SendRawAndRecycle(packet, ep)` = `SendRaw(packet.RawData, 0, packet.Size, ep)` 后**无条件** `PoolRecycle(packet)`。
> `SendBroadcast` 会向 `IPAddress.Broadcast`（v4）与 `ff02::1`（v6）各发一份，`finally` 中回收。

**路径 MTU 探测（per-peer）**

```csharp
private const int MtuCheckDelay = 1000;          // 毫秒
private const int MaxMtuCheckAttempts = 4;
```

```csharp
internal void ResetMtu()
{
    _finishMtu = !NetManager.MtuDiscovery;                       // 未开启探测则直接结束
    if (NetManager.MtuOverride > 0) OverrideMtu(NetManager.MtuOverride);
    else SetMtu(0);                                              // 从 PossibleMtu[0] = 1024 开始
}
private void SetMtu(int mtuIdx)
    { _mtuIdx = mtuIdx; _mtu = NetConstants.PossibleMtu[mtuIdx] - NetManager.ExtraPacketSizeForLayer; }
```

- 探测每 **1000 ms** 发一次 `MtuCheck`，最多 **4 次**后放弃（`_finishMtu = true`）。
- 探测包**本身就有候选 MTU 那么大**，并在偏移 `1` 与 `Size - 4` 两处写入该值。
- 收到 `MtuCheck`：把同一池化包改属性为 `MtuOk` 原样回发，并重置 `_mtuCheckAttempts`。
- 收到 `MtuOk`：仅当 `receivedMtu > _mtu` 且正好等于 `PossibleMtu[_mtuIdx + 1] - ExtraPacketSizeForLayer` 时 `SetMtu(_mtuIdx + 1)`；到达表尾则 `_finishMtu = true`。
- 触发分片的阈值：`length + headerSize > mtu`（`headerSize = NetPacket.GetHeaderSize(property)`）。
- `MtuOverride` 只设 `_mtu` 与 `_finishMtu`，**不改 `_mtuIdx`**。

### 4.15 partial：peer 集合哈希表（`LiteNetManager.HashSet.cs`）

```csharp
public LiteNetPeer GetPeerById(int id);                       // id 越界返回 null（无锁读取 _peersArray）
public bool TryGetPeerById(int id, out LiteNetPeer peer);
protected bool ContainsPeer(LiteNetPeer item);
protected bool RemovePeerFromSet(LiteNetPeer peer);
protected bool AddPeerToSet(LiteNetPeer value);
protected readonly ReaderWriterLockSlim _peersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
protected volatile LiteNetPeer _headPeer;
```

- 内部实现是**精简版 HashSet**（源码注释：`//minimal hashset class from dotnet with some optimizations`）：`_buckets` + `Slot[] _slots`（`Slot { HashCode; Next; Value; }`），`_freeList` 复用空闲槽，桶值编码为"槽下标 + 1"（`0` 表示空）。
- 初始容量 `HashSetGetPrime(0)` = **3**；扩容到 `HashSetGetPrime(2 * _count)` 并**完整 rehash**；上限 `MaxPrimeArrayLength = 0x7FFFFFC3`。
- `_peersArray`（id → peer 表）初始 32，按需倍增直到 `peer.Id < newSize`。
- `NET8_0_OR_GREATER` 下端点哈希为 `(UseNativeSockets ? endPoint.GetHashCode() : endPoint.Serialize().GetHashCode()) & Lower31BitMask`，否则 `endPoint.GetHashCode() & Lower31BitMask`。
- 另一个 `TryGetPeer(SocketAddress, out …)` 重载带注释 `//only used for NET8`，但**并未包在 `#if` 内**（无条件编译）。
- 锁：只有 `_peersLock`（读锁用于查询，写锁用于增删/清空，地址变化用"可升级读锁 + 写锁"）。**本文件没有 `lock` 语句、没有 `Interlocked`。**
- ⚠️ `GetPeerById` 无锁读取 `_peersArray`；`ContainsPeer` / `RemovePeerFromSet` / `AddPeerToSet` 自身不加锁。

### 4.16 partial：包池（`LiteNetManager.PacketPool.cs`）

```csharp
public int PacketPoolSize = 1000;
public int PoolCount { get; }
internal NetPacket PoolGetPacket(int size);
internal void PoolRecycle(NetPacket packet);
```

**精确语义**

- `PoolGetPacket(size)`：
  - `size > NetConstants.MaxPacketSize`(1432) → **直接 `new NetPacket(size)`，不进池**。
  - 否则在 `lock (_poolLock)` 内弹出 `_poolHead`；池空则 `new NetPacket(size)`。
  - 锁外设 `packet.Size = size`；**仅当** `packet.RawData.Length < size` 时才重新分配 `RawData`（否则复用旧缓冲，因此 **`RawData.Length` 可以大于 `Size`**）。
- `PoolRecycle(packet)`：
  - `packet.RawData.Length > MaxPacketSize || _poolCount >= PacketPoolSize` → **丢弃不入池**（注释 `//Don't pool big packets. Save memory`）。
  - 否则先 `packet.RawData[0] = 0;`（注释 `//Clean fragmented flag` —— 实际清空了整个首字节：property、connection number、fragment 位）。
  - 再在 `lock (_poolLock)` 内压入空闲链表。
- 辅助：`PoolGetWithData(property, data, start, length)`、`PoolGetWithProperty(property, size)`、`PoolGetWithProperty(property)`（均 internal/private）。
- ⚠️ `PoolCount` 与 `PacketPoolSize` 的比较**在锁外读 `_poolCount`**（源码如此，意图未说明）。

→ 理解 `NetPacketReader.RecycleInternal()` 会调 `_manager.PoolRecycle(_packet)` 即可。

---

## 5. `NetManager`

```csharp
namespace LiteNetLib
public class NetManager : LiteNetManager, IEnumerable<NetPeer>
```

> 类注释："More feature rich network manager with adjustable channels count"

### 5.1 新增/遮蔽成员

```csharp
public byte ChannelsCount { get; set; }        // 1..64，越界抛 ArgumentException("Channels count must be between 1 and 64")
public new NetPeer FirstPeer { get; }           // => (NetPeer)_headPeer

public new NetPeer Connect(string address, int port, string key);
public new NetPeer Connect(string address, int port, NetDataWriter connectionData);
public new NetPeer Connect(IPEndPoint target, string key);
public new NetPeer Connect(IPEndPoint target, NetDataWriter connectionData);
public new NetPeer Connect(IPEndPoint target, ReadOnlySpan<byte> connectionData);

public void GetPeers(List<NetPeer> peers, ConnectionState peerState);
public void GetConnectedPeers(List<NetPeer> peers);
public new NetPeerEnumerator<NetPeer> GetEnumerator();
IEnumerator<NetPeer> IEnumerable<NetPeer>.GetEnumerator();
```

**构造**

```csharp
public NetManager(INetEventListener listener, PacketLayerBase extraPacketLayer = null)
    : base(null, extraPacketLayer)
```

> ⚠️ **基类收到的 listener 是 `null`**。`NetManager` 自己保存 `_netEventListener` 并在覆写的 `ProcessEvent` 中派发。因此**监听器类型必须是 `INetEventListener`**；传入 `ILiteNetEventListener` 无法编译，而若绕过编译检查也会静默收不到回调。

### 5.2 带通道的 `SendToAll`（全套重载）

```csharp
public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options);
public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options);
public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer);
public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options);
public void SendToAll(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod options);
public void SendToAll(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer);
```

> **注意**：这些是 `SendToAll` 的新重载（不是 `new` 遮蔽），因为它们与基类重载的签名不同。内部调用 `((NetPeer)netPeer).Send(data, start, length, channelNumber, options)`。

### 5.3 NTP 对时

```csharp
public void CreateNtpRequest(IPEndPoint endPoint);
public void CreateNtpRequest(string ntpServerAddress, int port);
public void CreateNtpRequest(string ntpServerAddress);      // 使用 NtpRequest.DefaultPort = 123
```

请求存放在 `ConcurrentDictionary<IPEndPoint, NtpRequest> _ntpRequests`；`ProcessNtpRequests` 每帧驱动 `NtpRequest.Send`，`NeedToKill` 时移除。
响应在 `CustomMessageHandle` 中拦截：包长 < 48 直接丢弃（日志 "NTP response too short"）；成功则移除请求并回调 `INetEventListener.OnNtpResponse(NtpPacket)`。

### 5.4 覆写的虚成员

```csharp
protected override LiteNetPeer CreateOutgoingPeer(IPEndPoint, int, byte, ReadOnlySpan<byte>);   // new NetPeer(...)
protected override LiteNetPeer CreateIncomingPeer(LiteConnectionRequest, int);                  // new NetPeer(...)
protected override LiteNetPeer CreateRejectPeer(IPEndPoint, int);                               // new NetPeer(...)
protected override LiteConnectionRequest CreateConnectionRequest(IPEndPoint, NetConnectRequestPacket);  // new ConnectionRequest(...)
protected override void ProcessEvent(NetEvent evt);        // 按 INetEventListener 派发（OnNetworkReceive 带 channelNumber）
protected override void ProcessNtpRequests(float elapsedMilliseconds);
internal override bool CustomMessageHandle(NetPacket packet, IPEndPoint remoteEndPoint);
```

`NetPeer` 的通道数组大小为 `netManager.ChannelsCount * NetConstants.ChannelTypeCount`。

### 5.5 其它 public 成员（来自各 partial 文件）

```csharp
// LiteNetManager.Socket.cs
public static readonly bool IPv6Support;        // 由 private static LiteNetManager() 赋值
public int ReceivePollingTime = 50000;          // 轮询超时（微秒，0.05 秒）；增大可略提性能但会拖慢 Stop
public short Ttl { get; internal set; }         // get => _udpSocketv4.Ttl（UNITY_SWITCH 下为 0）
public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool manualMode);
public bool SendBroadcast(NetDataWriter writer, int port);
public bool SendBroadcast(byte[] data, int port);
public bool SendBroadcast(byte[] data, int start, int length, int port);

// LiteNetManager.PacketPool.cs
public int PacketPoolSize = 1000;               // 包池上限（发送量大时提高）
public int PoolCount { get; }

// LiteNetManager.HashSet.cs
public LiteNetPeer GetPeerById(int id);                       // 不存在返回 null
public bool TryGetPeerById(int id, out LiteNetPeer peer);
```

> `IPv6Support` 是本库**唯一的 public static 成员**（除 `NetConstants` / `NetUtils` / `NetDebug` 等静态类外）。`NetManager.cs`、`NetPeer.cs` 中没有 public static 成员。

---

## 6. `LiteNetPeer`

```csharp
namespace LiteNetLib
public class LiteNetPeer : IPEndPoint        // 基类是 System.Net.IPEndPoint
```

> ⚠️ **重要**：`LiteNetPeer` **继承自 `System.Net.IPEndPoint`**，所以 peer 对象**本身就是端点**。`Address`、`Port` 来自基类；代码中因此存在 `remoteEndPoint is LiteNetPeer` 这种判断（`LiteNetManager.cs:889`）。本类 override 了 `Serialize()` 与 `GetHashCode()`。
> 类注释："Network peer. Main purpose is sending messages to specific peer."

### 6.1 `ConnectionState`（`[Flags] public enum : byte`）

| 成员 | 源码表达式 | 数值 |
| --- | --- | ---: |
| `Outgoing` | `1 << 1` | 2 |
| `Connected` | `1 << 2` | 4 |
| `ShutdownRequested` | `1 << 3` | 8 |
| `Disconnected` | `1 << 4` | 16 |
| `EndPointChange` | `1 << 5` | 32 |
| `Any` | `Outgoing \| Connected \| ShutdownRequested \| EndPointChange` | 46 |

> ⚠️ **`Disconnected` 不在 `Any` 里**。直接写 `1 << 0` 的位（值 1）**没有被任何成员使用**。
> 各成员在源码中**没有** `<summary>` 文本。
> 用法要点：`GetPeersCount` / `GetPeers` 按位与匹配（`(state & peerState) != 0`），可组合。

### 6.2 构造函数（**全部 `internal`，外部不能 `new`**）

```csharp
internal LiteNetPeer(LiteNetManager netManager, IPEndPoint remoteEndPoint, int id);
    // 入站连接构造；_connectionState = ConnectionState.Connected

internal LiteNetPeer(LiteNetManager netManager, IPEndPoint remoteEndPoint, int id, byte connectNum, ReadOnlySpan<byte> connectData);
    // "Connect to"；_connectTime = DateTime.UtcNow.Ticks；状态 Outgoing；构造并发送 NetConnectRequestPacket

internal LiteNetPeer(LiteNetManager netManager, LiteConnectionRequest request, int id);
    // "Accept" 入站；从 request.InternalPacket 读取 ConnectionTime/ConnectionNumber/PeerId，
    // 构造并发送 NetConnectAcceptPacket；状态 Connected
```

### 6.3 public 字段

| 类型 | 名称 | 修饰 | 初值 | 说明 |
| --- | --- | --- | --- | --- |
| `LiteNetManager` | `NetManager` | `public readonly` | 构造函数赋值 | "Peer parent NetManager" |
| `int` | `Id` | `public readonly` | 构造函数赋值 | "Peer id can be used as key in your dictionary of peers" |
| `float` | `ResendFixedDelay` | `public` | `25.0f` | 重传延迟的固定部分（毫秒） |
| `float` | `ResendRttMultiplier` | `public` | `2.1f` | 重传延迟中 RTT 的乘数 |
| `object` | `Tag` | `public` | `null` | 应用自定义的连接附带数据 |
| `NetStatistics` | `Statistics` | `public readonly` | `new NetStatistics()`（构造函数赋值） | peer 级流量统计 |

> `internal volatile LiteNetPeer NextPeer;`、`internal LiteNetPeer PrevPeer;`、`internal byte[] NativeAddress;` 均为 internal（不属公开 API，但 `NetPeerEnumerator` 依赖 `NextPeer`）。

### 6.4 public 属性（全部只读）

```csharp
public ConnectionState ConnectionState { get; }      // 当前连接状态
public int RemoteId { get; private set; }            // 默认 0，由服务端分配
public int Ping { get; }                             // => _avgRtt / 2，单向延迟（毫秒）
public int RoundTripTime { get; }                    // => _avgRtt，往返时间（毫秒）
public int Mtu { get; }                              // => _mtu，不分片的最大 UDP 包大小
public long RemoteTimeDelta { get; }                 // => _remoteDelta（ticks，不精确；正数表示远端时间 > 本地）
public DateTime RemoteUtcTime { get; }               // => new DateTime(DateTime.UtcNow.Ticks + _remoteDelta)
public float TimeSinceLastPacket { get; }            // => _timeSinceLastPacket，距上次收包的时间（毫秒，含库内部包）

protected virtual int ChannelsCount { get; }         // 基类返回 1；NetPeer 覆写为 NetManager.ChannelsCount
```

> `RemoteTimeDelta` / `RemoteUtcTime` 的注释明确写了"不精确"（用于大致对时）。

### 6.5 public 方法

```csharp
// Address/Endpoint 相关（override 自 IPEndPoint）
public override SocketAddress Serialize();          // 返回缓存的 _cachedSocketAddr
public override int GetHashCode();                  // 返回缓存的 _cachedHashCode

// 队列查询
public int GetPacketsCountInReliableQueue(bool ordered);   // 通道 0；空通道返回 0

// 池化零拷贝发送
public PooledPacket CreatePacketFromPool(DeliveryMethod deliveryMethod);
public void SendPooledPacket(PooledPacket packet, int userDataSize);
public int GetMaxSinglePacketSize(DeliveryMethod options); // => _mtu - NetPacket.GetHeaderSize(...)

// Send —— 固定通道 0
public void Send(byte[] data, DeliveryMethod deliveryMethod);
public void Send(NetDataWriter dataWriter, DeliveryMethod deliveryMethod);
public void Send(byte[] data, int start, int length, DeliveryMethod options);
public void Send(ReadOnlySpan<byte> data, DeliveryMethod deliveryMethod);

// SendWithDeliveryEvent —— 固定通道 0
public void SendWithDeliveryEvent(byte[] data, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(byte[] data, int start, int length, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(NetDataWriter dataWriter, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(ReadOnlySpan<byte> data, DeliveryMethod deliveryMethod, object userData);

// 断开（转发到 NetManager 的对应重载）
public void Disconnect(byte[] data);
public void Disconnect(NetDataWriter writer);
public void Disconnect(byte[] data, int start, int count);
public void Disconnect(ReadOnlySpan<byte> data);
public void Disconnect();

// 扩展点
protected void SendInternal(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod deliveryMethod, object userData);
protected virtual void UpdateChannels();
internal virtual BaseChannel CreateChannel(byte channelNumber);
```

### 6.6 异常与静默行为（**易踩坑**）

| 场景 | 行为 |
| --- | --- |
| 非 `ReliableOrdered`/`ReliableUnordered` 调用 `SendWithDeliveryEvent` | 抛 `ArgumentException("Delivery event will work only for ReliableOrdered/Unordered packets")` |
| 不可靠/`ReliableSequenced` 包超过 MTU | 抛 `TooBigPacketException("Unreliable or ReliableSequenced packet size exceeded maximum of {mtu - headerSize} bytes, Check allowed size by GetMaxSinglePacketSize()")` |
| 分片数超过 `NetManager.MaxFragmentsCount` | 抛 `TooBigPacketException("Data was split in {totalPackets} fragments, which exceeds {MaxFragmentsCount}")` |
| **未连接**（`_connectionState != ConnectionState.Connected`）或 `channelNumber >= ChannelsCount` | **静默 return**：不发送、**不抛错、不报错** |
| `CreateChannel(byte)` 通道号非法 | 抛 `Exception("Invalid channel type")` |

> ⚠️ 最后两条尤其重要：**未连接时 `Send` 会静默丢弃**，不会抛异常也不会回调。排查"发不出去"时先检查 `peer.ConnectionState == ConnectionState.Connected`。

### 6.7 分片发送（`SendInternal`）

```csharp
int packetFullSize = mtu - headerSize;                                    // headerSize 通常为 4（Channeled）
int packetDataSize = packetFullSize - NetConstants.FragmentHeaderSize;    // 再减 6
int totalPackets   = length / packetDataSize + (length % packetDataSize == 0 ? 0 : 1);
ushort currentFragmentId = (ushort)Interlocked.Increment(ref _fragmentId); // 每 peer 单调递增
```

每个分片都是**独立池化**的 `NetPacket`：`PoolGetPacket(headerSize + sendLength + FragmentHeaderSize)`，设置 `FragmentId`/`FragmentPart`/`FragmentsTotal`，`MarkFragmented()`（置 `RawData[0] |= 0x80`），负载从 `FragmentedHeaderTotalSize`(10) 偏移开始拷贝，最后 `channel.AddToQueue(p)`。

**分片不会被合并**：`ReliableChannel.GetNextOutgoingPacket` 开头就是 `if (packet.IsFragmented) break;`。

### 6.8 分片重组（`AddReliablePacket`）

| 检查 | 失败行为 |
| --- | --- |
| `FragmentsTotal == 0 \|\| > MaxFragmentsCount` | 回收 + `NetDebug.WriteError("Invalid FragmentsTotal: …")` |
| `FragmentPart >= FragmentsTotal` | 回收 + `WriteError("FragmentPart … >= FragmentsTotal …")` |
| 暂存条目数 ≥ `MaxFragmentsInWindow(32) × ChannelsCount × FragmentedChannelsCount(2)` | 回收并丢弃 |
| 与已有条目 `FragmentsTotal` / `ChannelId` 不一致 | 回收 + `WriteError("Fragment metadata mismatch")` |
| 同一 `FragmentPart` 重复 | 回收 + `WriteError("Invalid fragment packet")` |

重组完成后：`PoolGetPacket(TotalSize)` 作为结果包，逐片 `Buffer.BlockCopy` 并**立即回收每个分片**，然后
`NetManager.CreateReceiveEvent(resultingPacket, method, (byte)(channelId / NetConstants.ChannelTypeCount), 0, this)` —— **注意这里 header 偏移传 0**（重组数据没有包头）。
非分片路径传 `NetConstants.ChanneledHeaderSize`(4)。
另有 `_deliveredFragments` 字典用于重复投递抑制。

### 6.9 重传延迟计算

```csharp
public float ResendFixedDelay   = 25.0f;   // 毫秒
public float ResendRttMultiplier = 2.1f;
// LiteNetPeer 内部：_resendDelay = ResendFixedDelay + _avgRtt * ResendRttMultiplier;
// 初始值 27.0f
```

`ReliableChannel` / `SequencedChannel` 都用 `peer.ResendDelay * TimeSpan.TicksPerMillisecond` 与 `UtcNow.Ticks` 比较来决定重发，**没有定时器、没有独立线程**。

### 6.10 线程安全

| 机制 | 保护对象 |
| --- | --- |
| `lock (_shutdownLock)` | `Shutdown` 全程持有，保证状态迁移原子 |
| `lock (_unreliableChannelLock)` | `_unreliableChannel`、`_unreliablePendingCount` 及双缓冲交换 |
| `lock (_mtuMutex)` | `_mtuIdx`、`SetMtu` |
| `Interlocked.Exchange(ref _timeSinceLastPacket, …)` | 收包/接受连接/shutdown 时重置；`Update` 中累加 |
| `Interlocked.Increment(ref _fragmentId)` | 分片 id |

源码注释标注的并发点（逐字）：
`//multithreaded variable` 出现在 `CreatePacketFromPool` 的 `int mtu = _mtu;` 与 `SendInternal` 的 `//Save mtu for multithread` 处。

> ⚠️ `_connectionState` **本身不是 volatile**；`Update`（逻辑线程）/`ProcessPacket`（接收线程）/`Shutdown` 依赖上述锁与 `Interlocked` 组合。**完整线程模型在源码注释中未给出承诺**。

### 6.11 `#if` 条件

| 位置 | 条件 | 效果 |
| --- | --- | --- |
| 文件头 1-3 | `DEBUG` | `#define STATS_ENABLED` |
| 266-270 / 307-311 | `NET8_0_OR_GREATER` | `_cachedHashCode` 取值：`NetManager.UseNativeSockets ? base.GetHashCode() : _cachedSocketAddr.GetHashCode()`；否则 `base.GetHashCode()` |

### 6.12 同文件内的 internal 枚举

```csharp
internal enum ConnectRequestResult { None, P2PLose, Reconnection, NewConnection }  // 0..3
internal enum DisconnectResult      { None, Reject, Disconnect }                    // 0..2
internal enum ShutdownResult        { None, Success, WasConnected }                 // 0..2
```

---

## 7. `NetPeer`

```csharp
namespace LiteNetLib
public class NetPeer : LiteNetPeer
```

> 类注释："Improved LiteNetPeer with full multi-channel support"

### 7.1 构造函数（**全部 `internal`**）

```csharp
internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id);
    // ⚠️ 此重载**不初始化** _channels（保持 null）

internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id, byte connectNum, ReadOnlySpan<byte> connectData);
    // _channels = new BaseChannel[netManager.ChannelsCount * NetConstants.ChannelTypeCount]

internal NetPeer(NetManager netManager, LiteConnectionRequest request, int id);
    // _channels = new BaseChannel[netManager.ChannelsCount * NetConstants.ChannelTypeCount]
```

> ⚠️ 第一个重载（用于"拒绝连接"的临时 peer）**不会创建通道数组**，因此它不应该被用于发送数据。

### 7.2 覆写

```csharp
protected override int ChannelsCount => ((NetManager)NetManager).ChannelsCount;   // 把基类常量 1 替换为可调通道数
protected override void UpdateChannels();
internal override void ProcessChanneled(NetPacket packet);
internal override void AddToReliableChannelSendQueue(BaseChannel channel);
internal override BaseChannel CreateChannel(byte idx);
```

### 7.3 带通道号的 public 方法

```csharp
public void Send(NetDataWriter dataWriter, byte channelNumber, DeliveryMethod deliveryMethod);
public void Send(byte[] data, byte channelNumber, DeliveryMethod deliveryMethod);
public void Send(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod deliveryMethod);
public void Send(byte[] data, int start, int length, byte channelNumber, DeliveryMethod deliveryMethod);

public void SendWithDeliveryEvent(byte[] data, byte channelNumber, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(byte[] data, int start, int length, byte channelNumber, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(NetDataWriter dataWriter, byte channelNumber, DeliveryMethod deliveryMethod, object userData);
public void SendWithDeliveryEvent(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod deliveryMethod, object userData);

public PooledPacket CreatePacketFromPool(DeliveryMethod deliveryMethod, byte channelNumber);
public int GetPacketsCountInReliableQueue(byte channelNumber, bool ordered);
public new int GetPacketsCountInReliableQueue(bool ordered);   // 遮蔽基类同名同签名方法，等价于通道 0
```

> ⚠️ **注意 `Send` 的重载关系**：`LiteNetPeer.Send(byte[], DeliveryMethod)` 与 `NetPeer.Send(byte[], byte, DeliveryMethod)` 是**不同签名**，因此**没有被遮蔽**——在 `NetPeer` 上两套都能调用。而 `GetPacketsCountInReliableQueue(bool)` 是同签名，所以用了 `public new`。
> `channelNumber` 取值范围：`0 .. ChannelsCount - 1`（源码注释写 "number of channel 0-63"）。

### 7.4 通道索引与惰性创建

```csharp
// 索引公式
channelNumber * NetConstants.ChannelTypeCount + (byte)deliveryMethod
// 即：通道数组长度 = ChannelsCount * 4，每个通道为 4 种投递方式各留一个槽
```

```csharp
// CreateChannel：按 (DeliveryMethod)(idx % ChannelTypeCount) 选择实现
ReliableUnordered   → new ReliableChannel(this, false, idx)
ReliableOrdered     → new ReliableChannel(this, true,  idx)
Sequenced           → new SequencedChannel(this, false, idx)
ReliableSequenced   → new SequencedChannel(this, true,  idx)
```

创建用无锁惰性初始化：

```csharp
Interlocked.CompareExchange(ref _channels[idx], newChannel, null);
```

> ⚠️ 竞争失败时多余创建的 `newChannel` 会被**直接丢弃，源码未回收**。
> `ProcessChanneled`：`packet.ChannelId >= _channels.Length` → `PoolRecycle` 并返回；`Property == Ack` 时允许通道未创建（不新建）。

### 7.5 发送队列

```csharp
private readonly ConcurrentQueue<BaseChannel> _channelSendQueue = new ConcurrentQueue<BaseChannel>();
```

`LiteNetPeer` 把待发通道加入该队列（`AddToReliableChannelSendQueue`）；`UpdateChannels()` 按 `Count` 快照逐次 `TryDequeue`，对 `channel.SendAndCheckQueue()` 返回 `true`（仍有待发）的通道**重新入队**。

---

## 8. `LiteConnectionRequest` / `ConnectionRequest`

```csharp
namespace LiteNetLib
internal enum ConnectionRequestResult   // 底层 int
{
    None = 0,
    Accept = 1,
    Reject = 2,
    RejectForce = 3
}

public class LiteConnectionRequest          // 非 sealed、非 abstract，无基类、无接口
public class ConnectionRequest : LiteConnectionRequest
```

### 8.1 成员

```csharp
public NetDataReader Data { get; }        // => InternalPacket.Data（握手负载读取器）
public readonly IPEndPoint RemoteEndPoint;

internal ConnectionRequestResult Result { get; private set; }
internal NetConnectRequestPacket InternalPacket;      // 字段（非属性）
```

> 构造函数是 **`internal`**，用户代码无法自行创建，只能从 `OnConnectionRequest` 回调中拿到。

### 8.2 接受

```csharp
// LiteConnectionRequest
public LiteNetPeer AcceptIfKey(string key);
public LiteNetPeer Accept();

// ConnectionRequest
public new NetPeer AcceptIfKey(string key) => (NetPeer)base.AcceptIfKey(key);
public new NetPeer Accept()                => (NetPeer)base.Accept();
```

`AcceptIfKey(string key)` 的精确行为：

1. `TryActivate()`（`Interlocked.CompareExchange(ref _used, 1, 0) == 0`）——**单次激活闩锁**；已激活过则返回 `null`。
2. 读取 `Data.GetString()` 与 `key` 做**序数字符串相等**比较（无哈希、非时序安全）。
3. 相等 → `Result = Accept`；**任何异常**（例如数据不足）→ `NetDebug.WriteError("[AC] Invalid incoming data")`。
4. 若 `Result == Accept` → 返回 `_listener.OnConnectionSolved(this, ReadOnlySpan<byte>.Empty)`。
5. 否则 → `Result = Reject`，调 `OnConnectionSolved(this, ReadOnlySpan<byte>.Empty)` 后返回 `null`。

> ⚠️ **key 比较的是 `Data` 里的第一个字符串**，即客户端 `Connect(..., key)` 写入的内容。因此 `Connect` 用 `string` 版本时 key 就是第一个字符串。

### 8.3 拒绝

```csharp
public void Reject(ReadOnlySpan<byte> rejectData, bool force);        // 核心实现
public void Reject(byte[] rejectData, int start, int length, bool force);
public void Reject(byte[] rejectData, int start, int length);
public void RejectForce(byte[] rejectData, int start, int length);
public void RejectForce();
public void RejectForce(byte[] rejectData);
public void RejectForce(NetDataWriter rejectData);
public void RejectForce(ReadOnlySpan<byte> rejectData);
public void Reject();
public void Reject(byte[] rejectData);
public void Reject(NetDataWriter rejectData);
public void Reject(ReadOnlySpan<byte> rejectData);
```

**`force` 语义**（源码注释）：

- `force = true`（`RejectForce*`）：**立即移除请求**；若 `rejectData` 非空还会发送拒绝包。
- `force = false`（`Reject*`）：**创建一个临时 peer**，由它持续发送拒绝包并在内存中**滞留到超时**，以处理迟到的包。

核心实现：

```csharp
public void Reject(ReadOnlySpan<byte> rejectData, bool force)
{
    if (!TryActivate()) return;
    Result = force ? ConnectionRequestResult.RejectForce : ConnectionRequestResult.Reject;
    _listener.OnConnectionSolved(this, rejectData);
}
```

> **无任何参数有默认值**，**无 `[Obsolete]`**。

### 8.4 请求去重

```csharp
internal void UpdateRequest(NetConnectRequestPacket connectRequest);
```

仅当新包的 `ConnectionTime` 更大，或时间相同但 `ConnectionNumber` 不同时才替换 `InternalPacket`，否则忽略。

---

## 9. `NatPunchModule` 与 NAT 打洞

```csharp
namespace LiteNetLib

public enum NatAddressType        // 底层 int
{
    Internal = 0,
    External = 1
}

public interface INatPunchListener
{
    void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
    void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token);
}

public class EventBasedNatPunchListener : INatPunchListener
{
    public delegate void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
    public delegate void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token);
    public event OnNatIntroductionRequest NatIntroductionRequest;
    public event OnNatIntroductionSuccess NatIntroductionSuccess;
}

public sealed class NatPunchModule
{
    public const int MaxTokenLength = 256;      // 传给 NetSerializer 的最大字符串长度
    public bool UnsyncedEvents = false;         // public 可变字段

    public void Init(INatPunchListener listener);
    public void NatIntroduce(IPEndPoint hostInternal, IPEndPoint hostExternal,
                             IPEndPoint clientInternal, IPEndPoint clientExternal, string additionalInfo);
    public void PollEvents();
    public void SendNatIntroduceRequest(string host, int port, string additionalInfo);
    public void SendNatIntroduceRequest(IPEndPoint masterServerEndPoint, string additionalInfo);
}
```

> ⚠️ **不存在** `NatPunch(...)` 方法，也**不存在** `Poll()`；公开的轮询 API 是 **`PollEvents()`**。
> `NatPunchModule` 由 `LiteNetManager.NatPunchModule` 懒加载暴露；构造函数是 `internal`。

### 9.1 公开 API 细节

- `Init(listener)`：仅赋值 `_natPunchListener`。
- `PollEvents()`：若 `UnsyncedEvents` 或 `_natPunchListener == null` 或两个队列都空则**立即返回**；否则先清空 `_successEvents` 回调 `OnNatIntroductionSuccess`，再清空 `_requestEvents` 回调 `OnNatIntroductionRequest`。
- `SendNatIntroduceRequest(IPEndPoint, string)`：用 `NetUtils.GetLocalIp(LocalAddrType.IPv4)`；为空、或目标是 IPv6 时回退到 `GetLocalIp(LocalAddrType.IPv6)`；然后把 `NatIntroduceRequestPacket { Internal = MakeEndPoint(networkIp, _socket.LocalPort), Token = additionalInfo }` 发给 master server。
- `NatIntroduce(...)`：中介端入口。构造一个 `NatIntroduceResponsePacket { Token = additionalInfo }`，先填入 host 的 Internal/External 对并发给 `clientExternal`；再填入 client 的 Internal/External 对并发给 `hostExternal`。

### 9.2 状态机（由收包驱动，源码中**没有**显式状态字段）

```
Idle
 └─ 客户端调用 SendNatIntroduceRequest ──▶ master
     master 调用 NatIntroduce ──▶ 双方各收到 NatIntroduceResponsePacket
         └─ 收到响应的一方：
             1. 向内网地址发送 NatPunchPacket
             2. TTL = 2，向外部地址发送 1 字节 PacketProperty.Empty 探测（"router hack"）
             3. TTL 恢复为 NetConstants.SocketTTL (255)
             4. IsExternal = true，再向外部地址发送 NatPunchPacket
                 └─ 对端收到 NatPunchPacket ──▶ OnNatPunch
                     └─ 回调 OnNatIntroductionSuccess(endpoint, Internal/External, token)
                         └─ 应用层据此调用 Connect()
```

中介端：收到 `NatIntroduceRequestPacket` → 入队/回调 `OnNatIntroductionRequest`。

### 9.3 Token 处理

- token 是**不透明字符串**，在三种包之间**原样回传**，最终交给监听器。
- ⚠️ **本模块内没有任何 token 校验、比较或认证逻辑**。源码注释把校验责任交给调用方（原文："Release punch success to client; enabling him to Connect() to Sender if token is ok"）。
- 对中介返回包的来源也**没有做认证**。

### 9.4 `UnsyncedEvents` 的两种行为

| `UnsyncedEvents` | 行为 |
| --- | --- |
| `false`（默认） | 事件入 `ConcurrentQueue`，需在主循环调用 `PollEvents()` 派发 |
| `true` | 在**网络线程内联**直接回调监听器 |

### 9.5 内部嵌套类型（均 `private`，非公开 API）

| 类型 | 成员 |
| --- | --- |
| `struct RequestEventData` | `IPEndPoint LocalEndPoint; IPEndPoint RemoteEndPoint; string Token;` |
| `struct SuccessEventData` | `IPEndPoint TargetEndPoint; NatAddressType Type; string Token;` |
| `class NatIntroduceRequestPacket` | `IPEndPoint Internal`、`string Token`（均带 `[Preserve]` 标记的 get/set） |
| `class NatIntroduceResponsePacket` | `IPEndPoint Internal; IPEndPoint External; string Token;` |
| `class NatPunchPacket` | `string Token; bool IsExternal;` |

`#if NET5_0_OR_GREATER`（第 166 行）：给 `Send<T>` 的类型参数加 `[DynamicallyAccessedMembers(Trimming.SerializerMemberTypes)]`，仅影响裁剪器，无运行时行为。

---

## 16. `NetPacket` 与协议头布局

```csharp
namespace LiteNetLib
internal enum PacketProperty : byte      // 注意：不是 [Flags]
internal sealed class NetPacket
```

### 16.1 `PacketProperty` 数值

| 名称 | 值 | 头长度（字节） |
| --- | ---: | ---: |
| `Unreliable` | 0 | 1 |
| `Channeled` | 1 | 4 |
| `ReliableMerged` | 2 | 4 |
| `Ack` | 3 | 4 |
| `Ping` | 4 | 3 |
| `Pong` | 5 | 11 |
| `ConnectRequest` | 6 | 18 |
| `ConnectAccept` | 7 | 15 |
| `Disconnect` | 8 | 9 |
| `UnconnectedMessage` | 9 | 1 |
| `MtuCheck` | 10 | 1 |
| `MtuOk` | 11 | 1 |
| `Broadcast` | 12 | 1 |
| `Merged` | 13 | 1 |
| `ShutdownOk` | 14 | 1 |
| `PeerNotFound` | 15 | 1 |
| `InvalidProtocol` | 16 | 1 |
| `NatMessage` | 17 | 1 |
| `Empty` | 18 | 1 |
| `Total` | 19 | —（哨兵值，`HeaderSizes` 数组长度 / `PropertiesCount`） |

> `HeaderSizes` 表在静态构造函数中一次性构建（`NetUtils.AllocatePinnedUninitializedArray<int>(19)`），规则：
> `Channeled` / `Ack` / `ReliableMerged` → `ChanneledHeaderSize`(4)；
> `Ping` → `HeaderSize + 2`(3)；`ConnectRequest` → 18；`ConnectAccept` → 15；
> `Disconnect` → `HeaderSize + 8`(9)；`Pong` → `HeaderSize + 10`(11)；其余 → `HeaderSize`(1)。

### 16.2 头字节布局（**关键**）

```
byte 0 : bits 0-4 = PacketProperty (mask 0x1F)
         bits 5-6 = ConnectionNumber (mask 0x60)
         bit  7   = IsFragmented    (mask 0x80)
byte 1-2: Sequence        (ushort)
byte 3  : ChannelId       (byte)
byte 4-5: FragmentId      (ushort)
byte 6-7: FragmentPart    (ushort)
byte 8-9: FragmentsTotal  (ushort)
```

→ `ChanneledHeaderSize` = **4**（byte 0-3）、`FragmentHeaderSize` = **6**（byte 4-9）、`FragmentedHeaderTotalSize` = **10**。

> ⚠️ 所有多字节字段都是**宿主端序**（写走 `FastBitConverter.GetBytes` 的裸内存拷贝，读走 `BitConverter.ToUInt16/ToInt32/ToInt64`）；源码**没有**任何端序策略声明。

### 16.3 公开成员

```csharp
public PacketProperty Property { get; set; }   // 读写 byte0 的 bit 0-4
public byte ConnectionNumber { get; set; }     // 读写 byte0 的 bit 5-6
public ushort Sequence { get; set; }           // byte 1-2
public bool IsFragmented { get; }              // (RawData[0] & 0x80) != 0
public void MarkFragmented();                  // RawData[0] |= 0x80
public byte ChannelId { get; set; }            // byte 3
public ushort FragmentId { get; set; }         // byte 4-5
public ushort FragmentPart { get; set; }       // byte 6-7
public ushort FragmentsTotal { get; set; }     // byte 8-9

public byte[] RawData;      // 头 + 负载
public int Size;            // 实际使用长度（含头）
public object UserData;     // 送达通知负载；可能是 MergedPacketUserData
public NetPacket Next;      // 池链表指针

public NetPacket(int size);                              // RawData = new byte[size]; 不写头
public NetPacket(PacketProperty property, int payloadSize);  // 总长 = payloadSize + GetHeaderSize(property)
public static int GetHeaderSize(PacketProperty property);   // 无边界检查
public int HeaderSize { get; }                              // => HeaderSizes[RawData[0] & 0x1F]，无边界检查
public bool Verify();                                       // property >= 19 → false；Size >= headerSize；分片时还需 Size >= headerSize + 6
```

> ⚠️ `ConnectionNumber` 的 setter 是 `RawData[0] = (RawData[0] & 0x9F) | (value << 5)`，**没有对 value 做掩码**；传入 ≥ 4 的值会溢出到 bit 7（源码原样如此）。
> ⚠️ 本文件**不存在** `Encode` / `Decode` / `DecodeHeader` / `RecalculateHeaderSize` / `SetSize` 成员；长度由 `GetHeaderSize` / `HeaderSize` / `Verify` 与构造函数计算。

---

## 17. 通道实现

### 17.1 `BaseChannel`（internal abstract）

```csharp
internal abstract class BaseChannel
{
    protected readonly LiteNetPeer Peer;
    protected readonly Queue<NetPacket> OutgoingQueue = new Queue<NetPacket>(NetConstants.DefaultWindowSize);  // 容量 64

    public int PacketsInQueue { get; }                    // 非同步读取 OutgoingQueue.Count
    protected BaseChannel(LiteNetPeer peer);
    public void AddToQueue(NetPacket packet);             // lock(OutgoingQueue){Enqueue} 后 AddToPeerChannelSendQueue()
    protected void AddToPeerChannelSendQueue();           // Interlocked.CompareExchange，保证只入队一次
    public bool SendAndCheckQueue();                      // 调 SendNextPackets()；false 时重置入队标志
    public abstract bool SendNextPackets();
    public abstract bool ProcessPacket(NetPacket packet);
}
```

> `PacketsInQueue` 是**非同步**读取，仅可用于统计/参考。

### 17.2 `ReliableChannel`（internal sealed : BaseChannel）

**关键常量与字段**

```csharp
private const int MergeHeaderSize = 2;        // 合并包每条 2 字节长度前缀
private const int MergeSizeThreshold = 20;    // 合并阈值（字节）
private const int BitsInByte = 8;
private readonly int _windowSize;             // 固定 = NetConstants.DefaultWindowSize (64)
private readonly NetPacket _outgoingAcks;     // PacketProperty.Ack, payload (_windowSize-1)/8+2 = 9，Size = 13
private readonly PendingPacket[] _pendingPackets;   // 长度 64
private readonly NetPacket[] _receivedPackets;      // 有序通道用，长度 64
private readonly bool[] _earlyReceived;             // 无序通道用，长度 64
```

**嵌套 `private struct PendingPacket`**：`_packet`(NetPacket)、`_timeStamp`(long ticks)、`_isSent`(bool)；`Init(NetPacket)`、`TrySend(long currentTime, LiteNetPeer peer)`、`IsEmpty`、`Clear(LiteNetPeer peer)`、`ToString()`。

**可靠性与顺序（均以源码为证）**

| 机制 | 实现 |
| --- | --- |
| 窗口大小 | **64**；发送方仅在 `NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart) < 64` 时才准入新包；槽位下标 `seq % 64` |
| 重传 | **无线程、无定时器**。`PendingPacket.TrySend` 比较 `DateTime.UtcNow.Ticks - _timeStamp` 与 `peer.ResendDelay * TimeSpan.TicksPerMillisecond` |
| 重传参数 | `peer.ResendDelay`（internal float，毫秒）= `ResendFixedDelay + _avgRtt * ResendRttMultiplier`；初始 `_resendDelay = 27.0f`，`public float ResendFixedDelay = 25.0f`，`public float ResendRttMultiplier = 2.1f` |
| 确认（ACK） | **只有正确认，没有 NAK**。接收方对每个接受的 seq 置 `_outgoingAcks` 位（字节下标 = `ChanneledHeaderSize + ackIdx/8`，位 = `ackIdx%8`）并置 `_mustSendAcks`；`SendNextPackets` 在 `_mustSendAcks` 时发一次 ack 包 |
| 发送方校验 ack | 长度必须等于 `_outgoingAcks.Size`，否则 "[PA]Invalid acks packet size"；`ackWindowStart >= MaxSequence` 或 `RelativeSequenceNumber(_localWindowStart, ackWindowStart) < 0` → "[PA]Bad window start"；`rel >= _windowSize` → "[PA]Old acks" |
| 丢包统计 | 缺失位且槽非空、`EnableStatistics` 时，对 peer 与 manager 各 `IncrementPacketLoss()` |
| 序列号比较 | `NetUtils.RelativeSequenceNumber(number, expected) = (number - expected + 32768 + 16384) % 32768 - 16384` |
| 接收合法性 | `seq >= 32768` → "[RR]Bad sequence"；`relateSeq > windowSize` → "[RR]Bad sequence"；`relate < 0` → "[RR]ReliableInOrder too old"；`relate >= windowSize*2` → "[RR]ReliableInOrder too new" |
| 有序通道 | `seq == _remoteSequence` 立即投递并排空暂存；乱序包暂存在 `_receivedPackets[ackIdx]` |
| 无序通道 | 置 `_earlyReceived[ackIdx]` 后**立即投递**（只去重 + 窗口，不保证顺序） |
| 窗口滑动 | `relate >= windowSize` 时 `newWindowStart = (_remoteWindowStart + relate - _windowSize + 1) % MaxSequence`，推进时清除旧位 |
| 包合并 | `GetNextOutgoingPacket` 把连续的非分片待发包合并为单个 `PacketProperty.ReliableMerged`（每条 2 字节 LE 长度前缀 + 负载，最大负载 = `Peer.Mtu - ChanneledHeaderSize`）；`newSize + 20 > maxPayload && mergePos > 0` 或 `newSize > maxPayload` 时中断 |
| 拆包 | `ProcessIncomingPacket` 对 `ReliableMerged` 按 2 字节长度前缀切分（从 `ChanneledHeaderSize` 起），`size == 0` 或溢出时中止（"[RR]Merged packet corrupted"），逐条重建为 `PacketProperty.Channeled` 并 `Peer.AddReliablePacket(...)` |

> ⚠️ **通道没有任何 `Reset` / `Clear` / `Dispose` API**；通道拆除语义在源码中未说明。`OutgoingQueue` 只会被发送流程逐步取空。

### 17.3 `SequencedChannel`（internal sealed : BaseChannel）

```csharp
public SequencedChannel(LiteNetPeer peer, bool reliable, byte id);
```

- `_reliable` 为真时构造 `_ackPacket = new NetPacket(PacketProperty.Ack, 0) { ChannelId = id }`（`Size = 4`）。
- **发送**：可靠通道且队列为空时，若 `UtcNow.Ticks - _lastPacketSendTime >= Peer.ResendDelay * TicksPerMillisecond` 则**重发 `_lastPacket`**（即**只保留一个包并重传到被确认**）。否则取空队列全部发送，`_localSequence = (_localSequence + 1) % MaxSequence`；可靠运行时**最后一个包保留**（`_lastPacket`、`_lastPacketSendTime`），更早的 `PoolRecycle`。
- **接收**：**直接拒绝分片包**（`return false`）——与 `DeliveryMethod.ReliableSequenced` 文档"不可分片"一致；收到 `Ack` 且 `packet.Sequence == _lastPacket.Sequence` → `_lastPacket = null` 并返回 `false`；否则计算 `relative = RelativeSequenceNumber(packet.Sequence, _remoteSequence)`，仅当 `packet.Sequence < MaxSequence && relative > 0` 时投递，`EnableStatistics` 时给 peer 与 manager 各加 `relative - 1` 的丢包数，并 `_remoteSequence = packet.Sequence`。
- 投递时 `CreateReceiveEvent(packet, _reliable ? DeliveryMethod.ReliableSequenced : DeliveryMethod.Sequenced, (byte)(packet.ChannelId / NetConstants.ChannelTypeCount), NetConstants.ChanneledHeaderSize, Peer)`；可靠时置 `_mustSendAck = true` 并入队发送。
- 返回值 `packetProcessed`。

**通道类型映射差异（重要）**

| 投递方式 | `NetPeer.CreateChannel` | `LiteNetPeer.CreateChannel` |
| --- | --- | --- |
| `ReliableSequenced` | `new SequencedChannel(this, true, idx)` | `new ReliableChannel(this, true, (int)DeliveryMethod.ReliableOrdered)` |
| `Sequenced` | `new SequencedChannel(this, false, idx)` | `new SequencedChannel(this, true, channelNumber)` |

> ⚠️ 这正是"**必须选择 `NetManager` 才能获得正确的多通道语义**"的底层原因：`LiteNetPeer` 把 `ReliableSequenced` 映射到了 `ReliableChannel`（带窗口与合并），而 `NetPeer` 才映射到真正的 `SequencedChannel`。

---

## 21. `NatPunchModule` 内部包

三种内部包都通过 `NetPacketProcessor`（`MaxTokenLength = 256`）序列化，外层统一包在 `PacketProperty.NatMessage` 里：

| 包类型 | 成员 | 方向 |
| --- | --- | --- |
| `NatIntroduceRequestPacket`（private nested） | `IPEndPoint Internal`、`string Token` | 客户端 → master server |
| `NatIntroduceResponsePacket`（private nested） | `IPEndPoint Internal`、`IPEndPoint External`、`string Token` | master server → 客户端 |
| `NatPunchPacket`（private nested） | `string Token`、`bool IsExternal` | 对等端 → 对等端（打洞包） |

消息处理入口：

```csharp
internal void ProcessMessage(IPEndPoint senderEndPoint, NetPacket packet)
    // lock(_cacheReader) { _cacheReader.SetSource(packet.RawData, NetConstants.HeaderSize, packet.Size);
    //                      _netPacketProcessor.ReadAllPackets(_cacheReader, senderEndPoint); }
```

> ⚠️ 源码把 `packet.Size` 作为 `SetSource` 的 `maxSize`（**绝对结束索引**）传入，而数据从偏移 `HeaderSize`(1) 开始——即第 3 个参数语义是"长度"还是"绝对索引"在此处**与 `NetDataReader.SetSource` 的文档语义不一致**，源码未说明。

出站发送：

```csharp
private void Send<T>(T packet, IPEndPoint target) where T : class, new()
    // _cacheWriter.Reset(); Put((byte)PacketProperty.NatMessage);
    // _netPacketProcessor.Write(_cacheWriter, packet);
    // _socket.SendRaw(_cacheWriter.Data, 0, _cacheWriter.Length, target);
```

> 每条 NAT 消息都以单字节 `PacketProperty.NatMessage`（值 17，头长 1）开头。

### 21.1 订阅注册（构造函数内）

```csharp
SubscribeReusable<NatIntroduceResponsePacket>(OnNatIntroductionResponse);
SubscribeReusable<NatIntroduceRequestPacket, IPEndPoint>(OnNatIntroductionRequest);
SubscribeReusable<NatPunchPacket, IPEndPoint>(OnNatPunch);
```

> 用的是 **`SubscribeReusable`**（复用同一实例）—— 注意 §14.9 提到的"残留字段"陷阱。
> `NetPacketProcessor` 的构造参数是 `MaxTokenLength = 256`（转给 `NetSerializer` 作 `maxStringLength`）。

### 21.2 三个处理器的精确行为

**`OnNatIntroductionRequest(req, senderEndPoint)`**（master 侧收到客户端请求）

```csharp
if (UnsyncedEvents)
    _natPunchListener.OnNatIntroductionRequest(req.Internal, senderEndPoint, req.Token);
else
    _requestEvents.Enqueue(new RequestEventData { LocalEndPoint = req.Internal,
        RemoteEndPoint = senderEndPoint, Token = req.Token });
```

**`OnNatIntroductionResponse(req)`**（客户端侧收到中介响应）

```csharp
NetDebug.Write(NetLogLevel.Trace, "[NAT] introduction received");
var punchPacket = new NatPunchPacket { Token = req.Token };
Send(punchPacket, req.Internal);              // "[NAT] internal punch sent to {req.Internal}"

// router hack：TTL=2 的小探测包，用于在 NAT 上打开映射
_socket.Ttl = 2;
_socket.SendRaw(new[] { (byte)PacketProperty.Empty }, 0, 1, req.External);
_socket.Ttl = NetConstants.SocketTTL;         // 恢复为 255

punchPacket.IsExternal = true;
Send(punchPacket, req.External);              // "[NAT] external punch sent to {req.External}"
```

**`OnNatPunch(req, senderEndPoint)`**（对等端收到打洞包）

```csharp
NetDebug.Write(NetLogLevel.Trace, $"[NAT] punch received from {senderEndPoint} - additional info: {req.Token}");
// 源码注释（第 321 行）：//Release punch success to client; enabling him to Connect() to Sender if token is ok
if (UnsyncedEvents)
    _natPunchListener.OnNatIntroductionSuccess(senderEndPoint,
        req.IsExternal ? NatAddressType.External : NatAddressType.Internal, req.Token);
else
    _successEvents.Enqueue(new SuccessEventData { TargetEndPoint = senderEndPoint,
        Type = req.IsExternal ? NatAddressType.External : NatAddressType.Internal, Token = req.Token });
```

### 21.3 `NatIntroduce`（中介端入口）精确行为

```csharp
public void NatIntroduce(IPEndPoint hostInternal, IPEndPoint hostExternal,
                         IPEndPoint clientInternal, IPEndPoint clientExternal, string additionalInfo)
{
    var req = new NatIntroduceResponsePacket { Token = additionalInfo };
    req.Internal = hostInternal;   req.External = hostExternal;
    Send(req, clientExternal);                       // 把 host 的地址对告诉 client
    req.Internal = clientInternal; req.External = clientExternal;
    Send(req, hostExternal);                         // 把 client 的地址对告诉 host
}
```

> **只构造一个包实例并复用**（内容被改写两次）。

### 21.4 `SendNatIntroduceRequest` 精确行为

```csharp
string networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv4);
if (string.IsNullOrEmpty(networkIp) || masterServerEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
    networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv6);

// 发送 NatIntroduceRequestPacket {
//     Internal = NetUtils.MakeEndPoint(networkIp, _socket.LocalPort),
//     Token    = additionalInfo }
```

### 21.5 `PollEvents()` 精确行为

```csharp
if (UnsyncedEvents) return;
if (_natPunchListener == null || (_successEvents.IsEmpty && _requestEvents.IsEmpty)) return;
// 先清空全部 SuccessEventData → OnNatIntroductionSuccess(TargetEndPoint, Type, Token)
// 再清空全部 RequestEventData → OnNatIntroductionRequest(LocalEndPoint, RemoteEndPoint, Token)
```

> **成功事件优先于请求事件**。

---

## 22. 工具类：`NetUtils` / `NetDebug` / `NetStatistics`

### 22.1 `LocalAddrType`（`[Flags]`）

```csharp
[Flags]
public enum LocalAddrType        // 底层 int
{
    IPv4 = 1,
    IPv6 = 2,
    All  = IPv4 | IPv6    // = 3
}
```

### 22.2 `NetUtils`（`public static class`）

```csharp
public static IPEndPoint MakeEndPoint(string hostStr, int port);
public static IPAddress ResolveAddress(string hostStr);
public static IPAddress ResolveAddress(string hostStr, AddressFamily addressFamily);
public static List<string> GetLocalIpList(LocalAddrType addrType);
public static void GetLocalIpList(IList<string> targetList, LocalAddrType addrType);
public static string GetLocalIp(LocalAddrType addrType);
```

**`ResolveAddress(string)` 精确行为**

- `"localhost"` → `IPAddress.Loopback`（IPv4 `127.0.0.1`，**不是** `::1`）。
- 否则先 `IPAddress.TryParse`；失败且 `LiteNetManager.IPv6Support` 为真时，先按 `InterNetworkV6` 解析，为 `null` 再按 `InterNetwork`。
- 最终为 `null` → 抛 **`ArgumentException("Invalid address: " + hostStr)`**。

**`ResolveAddress(string, AddressFamily)`**：`Dns.GetHostEntry(hostStr).AddressList` 中返回**第一个匹配地址族**的项，无匹配返回 `null`；**不捕获 DNS 异常**。

**`GetLocalIpList(IList<string>, LocalAddrType)`（"non alloc version"）**

1. `NetworkInterface.GetAllNetworkInterfaces()`，然后 `Array.Sort(networks, NetworkSorter)`。
   注释说明排序原因：优先 WiFi 而非蜂窝网，因为 "Most cellulars networks seems to be incompatible with NAT Punch"。
2. 跳过 `NetworkInterfaceType.Loopback` 与 `OperationalStatus != Up` 的接口。
3. **跳过 `ipProps.GatewayAddresses.Count == 0` 的地址**（注释："Skip address without gateway"）。
4. 加入匹配地址族的 `UnicastAddresses`。
5. 列表为空时的**回退模式**（注释 "Fallback mode (unity android)"）：`Dns.GetHostEntry(Dns.GetHostName()).AddressList`，按地址族过滤。
6. 整块包在 `catch { }`（注释 "//ignored"）中——**所有异常被吞掉**。
7. 仍为空时：ipv4 加 `"127.0.0.1"`，ipv6 加 `"::1"`。

**`GetLocalIp(LocalAddrType)`**：`lock (IpList) { IpList.Clear(); GetLocalIpList(IpList, addrType); return IpList.Count == 0 ? string.Empty : IpList[0]; }`——返回**排序后的第一项**。

**internal 成员**

```csharp
internal static void PrintInterfaceInfos();                          // 用 NetDebug.WriteForce 输出网卡信息
internal static int RelativeSequenceNumber(int number, int expected); // (number - expected + 32768 + 16384) % 32768 - 16384
internal static T[] AllocatePinnedUninitializedArray<T>(int count) where T : unmanaged;
    // NET5_0_OR_GREATER || NET5_0 → GC.AllocateUninitializedArray<T>(count, true)（pinned）
    // 否则 → new T[count]
```

> **`NetUtils` 中不存在**：IPv4↔IPv6 映射（`MapToIPv6` 等）、子网/掩码/CIDR 辅助、哈希辅助、`BitConverter` 用法、`unsafe`/指针代码。

### 22.3 `NetworkSorter`（internal : `IComparer<NetworkInterface>`）

优先级（注释 "Pick the most obvious choice for the local IP / Ethernet > Wifi > Others > Cellular"）：
`Ethernet` 类 = 3，`Wireless80211` = 2，其它 = 1，蜂窝（`Wman|Wwanpp|Wwanpp2`）= 0；`Compare` 返回优先级**高者在前**。

### 22.4 `NetDebug` 与相关类型

```csharp
public class InvalidPacketException : ArgumentException { public InvalidPacketException(string message); }
public class TooBigPacketException : InvalidPacketException { public TooBigPacketException(string message); }

public enum NetLogLevel      // 底层 int，注意顺序不是按严重程度递增
{
    Warning = 0,
    Error   = 1,
    Trace   = 2,
    Info    = 3
}

public interface INetLogger
{
    void WriteNet(NetLogLevel level, string str, params object[] args);
}

public static class NetDebug
{
    public static INetLogger Logger = null;      // public STATIC 可变字段（不是属性）

    [Conditional("DEBUG_MESSAGES")] internal static void Write(string str);
    [Conditional("DEBUG_MESSAGES")] internal static void Write(NetLogLevel level, string str);
    [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")] internal static void WriteForce(string str);
    [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")] internal static void WriteForce(NetLogLevel level, string str);
    internal static void WriteError(string str);   // 无 [Conditional] → 始终执行
}
```

> ⚠️ **多個 `Conditional` 特性是"或"关系**：`WriteForce` 在 `DEBUG_MESSAGES` **或** `DEBUG` 任一存在时才会生成调用，否则**调用点被编译器整体移除**（参数也不求值）。
> `Write` / `Write(level)` 仅在定义了 `DEBUG_MESSAGES` 时生成。**`DEBUG` 与 `DEBUG_MESSAGES` 在本文件中只作为 `[Conditional]` 字符串出现，从未作为 `#if`**。没有 `NET_LOG`、没有 `TRACE`、没有运行时最低级别字段。
> **`WriteError` 没有 `[Conditional]`，因此始终保留**——这是排查问题时唯一默认可见的日志通道。

`WriteLogic`（private）的全部逻辑都在 `lock (DebugLogLock)` 内：

```csharp
if (Logger == null)
{
#if UNITY_5_3_OR_NEWER
    UnityEngine.Debug.Log(string.Format(str, args));
#else
    Console.WriteLine(str, args);      // ⚠️ 非 Unity 路径没有 string.Format
#endif
}
else
    Logger.WriteNet(logLevel, str, args);
```

> ⚠️ **本类不做级别过滤**：`level` 只被转发给 `INetLogger`，两条控制台路径都会忽略它。
> ⚠️ 非 Unity 路径的 `Console.WriteLine(str, args)` **缺少 `string.Format`**（源码缺陷）。

### 22.5 `NetStatistics`（`public sealed class`）

类注释："Thread-safe counter for network statistics including sent/received packets, bytes, and packet loss."

**私有状态（全部 `private long`）**：`_packetsSent`、`_packetsReceived`、`_bytesSent`、`_bytesReceived`、`_packetLoss`。
**没有 public 字段。**

**只读属性（全部 `Interlocked.Read`）**

| 属性 | 类型 | 含义 |
| --- | --- | --- |
| `PacketsSent` | `long` | 已发包数 |
| `PacketsReceived` | `long` | 已收包数 |
| `BytesSent` | `long` | 已发字节 |
| `BytesReceived` | `long` | 已收字节 |
| `PacketLoss` | `long` | 丢包数 |
| `PacketLossPercent` | `long` | 整数百分比，`sent == 0 ? 0 : loss * 100 / sent`（整数除法，无小数） |

**修改方法（全部 `Interlocked`）**

```csharp
public void IncrementPacketsSent();          // +1
public void IncrementPacketsReceived();      // +1
public void AddBytesSent(long bytesSent);    // +N
public void AddBytesReceived(long bytesReceived);  // +N
public void IncrementPacketLoss();           // +1
public void AddPacketLoss(long packetLoss);  // +N
public void Reset();                         // 对 5 个计数器 Interlocked.Exchange(...,0)
```

**`ToString()`** 返回格式（**注意 Received 在 Sent 之前**）：

```
BytesReceived: {0}
PacketsReceived: {1}
BytesSent: {2}
PacketsSent: {3}
PacketLoss: {4}
PacketLossPercent: {5}
```

**调用点（本目录内）**

| 位置 | 动作 |
| --- | --- |
| `LiteNetPeer.cs:256` | 每 peer 创建 `Statistics = new NetStatistics()` |
| `LiteNetManager.cs:196` | manager 级 `public readonly NetStatistics Statistics` |
| `LiteNetPeer.cs:1180-1181, 1200-1201` / `LiteNetManager.Socket.cs:638-639` | `IncrementPacketsSent` + `AddBytesSent(bytesSent)` |
| `LiteNetManager.cs:840-841, 893-894` | `IncrementPacketsReceived` + `AddBytesReceived(originalPacketSize)` |
| `ReliableChannel.cs:254-255` / `SequencedChannel.cs:91-92` | 对 peer 与 manager 各加丢包 |

> `Reset()` 在 `Assets/Scripts/Net` 内**没有任何调用点**。
> 是否受 `LiteNetManager.EnableStatistics` 门控在源码中未说明（该字段位于 `LiteNetManager.cs:201`）。

---

## 23. 平台适配

### 23.1 `PooledPacket`（`public readonly ref struct`，栈上专用）

```csharp
public readonly ref struct PooledPacket
{
    internal readonly NetPacket _packet;          // 来自 manager 包池的底层包
    internal readonly byte _channelNumber;        // 创建时传入的通道号
    public readonly int MaxUserDataSize;          // "Maximum data size that you can put into such packet"
    public readonly int UserDataOffset;           // "Offset for user data when writing to Data array"
    public byte[] Data { get; }                   // => _packet.RawData（不要改包头！从 UserDataOffset 开始写）

    internal PooledPacket(NetPacket packet, int maxDataSize, byte channelNumber);
}
```

构造函数（**无任何校验**）：

```csharp
UserDataOffset  = _packet.HeaderSize;
_packet.Size    = UserDataOffset;
MaxUserDataSize = maxDataSize - UserDataOffset;
_channelNumber  = channelNumber;
```

使用流程：

```csharp
var p = peer.CreatePacketFromPool(DeliveryMethod.ReliableOrdered, 0);
// 从 p.UserDataOffset 开始写 p.Data，最多写 p.MaxUserDataSize 字节
peer.SendPooledPacket(p, userDataSize);     // 内部：packet._packet.Size = UserDataOffset + userDataSize
```

- `CreatePacketFromPool(deliveryMethod)`（`LiteNetPeer`）：取 `int mtu = _mtu; var packet = NetManager.PoolGetPacket(mtu);`；`Unreliable` → `PacketProperty.Unreliable` 且 offset `0`，否则 `PacketProperty.Channeled` 且 `(byte)deliveryMethod`。
- `SendPooledPacket`：非 `Connected` 直接 return；`Channeled` 走 `CreateChannel(...).AddToQueue(...)`，否则在 `lock (_unreliableChannelLock)` 内进入每 peer 的不可靠队列。
- ⚠️ **源码中没有对 `userDataSize` 与 `MaxUserDataSize` 的边界检查**。
- ⚠️ 因为是 `ref struct`，**不能装箱、不能作为字段、不能跨 `await`**。

### 23.2 `NativeSocket`（`internal static class`）

```csharp
internal static class NativeSocket
{
    public static readonly bool IsSupported = false;   // 静态构造函数中赋值
    public static readonly bool UnixMode    = false;   // 仅 Linux 为 true
    public const int IPv4AddrSize = 16;                // sockaddr_in 大小（字节）
    public const int IPv6AddrSize = 28;                // sockaddr_in6 大小（字节）
    public const int AF_INET  = 2;
    public const int AF_INET6 = 10;

    static unsafe class WinSock { ... }                // LibName = "ws2_32.dll"
    static unsafe class UnixSock { ... }               // LibName = "libc"

    public static int RecvFrom(IntPtr socketHandle, byte[] pinnedBuffer, int len,
                               byte[] socketAddress, ref int socketAddressSize);
    public static unsafe int SendTo(IntPtr socketHandle, byte* pinnedBuffer, int len,
                                    byte[] socketAddress, int socketAddressSize);
    public static SocketError GetSocketError();
    public static SocketException GetSocketException();
    public static short GetNativeAddressFamily(IPEndPoint remoteEndPoint);
}
```

**平台选择是运行时的，不是 `#if`**（文件内**没有**任何预处理指令）：

```csharp
static NativeSocket()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))       { IsSupported = true; UnixMode = true; }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { IsSupported = true; }
}
```

→ **只有 Linux 与 Windows 支持**（macOS 等为 `false`）。`LiteNetManager.Start` 中会强制降级：
`UseNativeSockets = UseNativeSockets && NativeSocket.IsSupported;`

**P/Invoke 签名（逐字）**

```csharp
[DllImport(LibName, SetLastError = true)]
public static extern int recvfrom(
    IntPtr socketHandle,
    [In, Out] byte[] pinnedBuffer,
    [In] int len,
    [In] SocketFlags socketFlags,
    [Out] byte[] socketAddress,
    [In, Out] ref int socketAddressSize);

[DllImport(LibName, SetLastError = true)]
internal static extern int sendto(
    IntPtr socketHandle,
    byte* pinnedBuffer,
    [In] int len,
    [In] SocketFlags socketFlags,
    [In] byte[] socketAddress,
    [In] int socketAddressSize);
```

> 注意嵌套类的可访问性差异：`recvfrom` 是 `public`，`sendto` 是 `internal`（但外层类是 `internal`，两者实际都只在程序集内可见）。

包装方法全部带 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`（除两个错误辅助方法）：

```csharp
public static int RecvFrom(...)   // => UnixMode ? UnixSock.recvfrom(..., 0, ...) : WinSock.recvfrom(..., 0, ...)
public static unsafe int SendTo(...)  // => UnixMode ? UnixSock.sendto(..., 0, ...) : WinSock.sendto(..., 0, ...)
public static SocketError GetSocketError()   // Marshal.GetLastWin32Error()；Unix 下查 errno 表（41 项），否则直接强转
public static SocketException GetSocketException()
public static short GetNativeAddressFamily(IPEndPoint remoteEndPoint)
    // Unix: (short)(InterNetwork ? AF_INET : AF_INET6)；Windows: (short)remoteEndPoint.AddressFamily
```

### 23.3 `PausedSocketFix`（`public class`，整个文件在 `#if UNITY_2018_3_OR_NEWER` 内）

```csharp
private readonly LiteNetManager _netManager;
private readonly IPAddress _ipv4, _ipv6;
private readonly int _port;
private readonly bool _manualMode;
private bool _initialized;

public PausedSocketFix(LiteNetManager netManager, IPAddress ipv4, IPAddress ipv6, int port, bool manualMode);
public void Deinitialize();
private void Application_focusChanged(bool focused);
```

**它解决什么问题**：Unity 2018.3+ 在**应用失去焦点/进入后台**（Android/iOS 生命周期）后 UDP socket 可能失效，此后 socket 会报 `SocketError.NotConnected`。`LiteNetManager.Socket.cs` 的 `ProcessError` 把它记为 `internal bool NotConnected`（字段注释：`// special case in iOS (and possibly android that should be resolved in unity)`）并**终止接收循环**（`case SocketError.NotConnected: NotConnected = true; return true;`）。本类在**焦点恢复**时用原来的绑定地址/端口/`manualMode` 重新 `Start`。

**构造函数**：保存 5 个值；订阅 `Application.focusChanged += Application_focusChanged;` 与 `Application.quitting += Deinitialize;`；置 `_initialized = true;`。

**`Deinitialize()`**：若 `_initialized` 则退订两个 Unity 事件；若 `_netManager.IsRunning` 则 `_netManager.Stop()`；置 `_initialized = false;`。

**`Application_focusChanged(bool focused)` 的判定顺序**（仅在 `focused == true` 时继续）：

```csharp
if (!_initialized) return;
if (!_netManager.IsRunning) return;          // "Was intentionally disconnected at some point."
if (_netManager.NotConnected == false) return;   // "Socket is in working state."
_netManager.Start(_ipv4, _ipv6, _port, _manualMode);
// 失败：NetDebug.WriteError($"[S] Cannot restore connection. Ipv4 {_ipv4}, Ipv6 {_ipv6}, Port {_port}, ManualMode {_manualMode}")
```

**创建/销毁位置**

```csharp
// LiteNetManager.Socket.cs（Start 内）
#if UNITY_SOCKET_FIX
    if (_useSocketFix && _pausedSocketFix == null)
        _pausedSocketFix = new PausedSocketFix(this, addressIPv4, addressIPv6, port, manualMode);
#endif
// LiteNetManager.cs（Stop(bool) 内）
#if UNITY_SOCKET_FIX
    if (_useSocketFix)
    {
        _pausedSocketFix.Deinitialize();
        _pausedSocketFix = null;
    }
#endif
```

> ⚠️ `_pausedSocketFix.Deinitialize()` **没有判空**；此刻是否保证非 null，源码未作说明。

### 23.4 `Trimming`（internal，仅 `NET5_0_OR_GREATER`）

```csharp
internal static class Trimming
{
    internal const DynamicallyAccessedMemberTypes SerializerMemberTypes = PublicProperties | NonPublicProperties;
}
```

供 `NetSerializer` / `NetPacketProcessor` 的 `[DynamicallyAccessedMembers(Trimming.SerializerMemberTypes)]` 标注使用（见附录 A）。位于 `LiteNetLib` 命名空间（`NetPacketProcessor` 通过父命名空间解析到它）。

### 23.5 命名空间归属提醒

| 类型 | 命名空间 |
| --- | --- |
| `NativeSocket`、`Trimming`、`NetEvent`、`NetPacket`、`NetStatistics`、`NetUtils`、`NetDebug`、`NetPacketReader`、`ConnectionRequest`、`NAT/通道/peer/manager` 各类 | `LiteNetLib` |
| `NetDataWriter`、`NetDataReader`、`NetSerializer`、`NetPacketProcessor`、`INetSerializable`、`FastBitConverter`、`CRC32C`、`NtpPacket`、`NtpRequest`、`PreserveAttribute` | `LiteNetLib.Utils` |
| `PacketLayerBase`、`Crc32cLayer`、`XorEncryptLayer` | `LiteNetLib.Layers` |

### 23.6 `PreserveAttribute` 的用途

`NetPunchModule` 的三个内部包类的属性都标了 `[Preserve]`，用于阻止 IL2CPP / 托管代码剥离（Managed Stripping）把它们裁掉——因为这些成员只被反射式序列化器间接使用，静态分析看不到引用。

---

## 20. NTP 对时

### 20.1 `NtpPacket`（`LiteNetLib.Utils`）

```csharp
public class NtpPacket
{
    private static readonly DateTime Epoch = new DateTime(1900, 1, 1);

    public byte[] Bytes { get; }                                 // 固定 48 字节
    public NtpLeapIndicator LeapIndicator { get; }               // (Bytes[0] & 0xC0) >> 6
    public int VersionNumber { get; private set; }               // (Bytes[0] & 0x38) >> 3
    public NtpMode Mode { get; private set; }                    // (NtpMode)(Bytes[0] & 0x07)
    public int Stratum { get; }                                  // Bytes[1]
    public int Poll { get; }                                     // Bytes[2]，log2 秒
    public int Precision { get; }                                // (sbyte)Bytes[3]，log2 秒，带符号
    public TimeSpan RootDelay { get; }                           // 偏移 4，16.16 定点秒
    public TimeSpan RootDispersion { get; }                      // 偏移 8
    public uint ReferenceId { get; }                             // 偏移 12，大端 u32
    public DateTime? ReferenceTimestamp { get; }                 // 偏移 16
    public DateTime? OriginTimestamp { get; }                    // 偏移 24
    public DateTime? ReceiveTimestamp { get; }                   // 偏移 32
    public DateTime? TransmitTimestamp { get; private set; }     // 偏移 40（可写）
    public DateTime? DestinationTimestamp { get; private set; }  // **不上线**，仅本地
    public TimeSpan RoundTripTime { get; }                       // (t1-t0) + (t3-t2)
    public TimeSpan CorrectionOffset { get; }                    // ((t1-t0)-(t3-t2)) / 2

    public NtpPacket();                                          // new byte[48]；Mode=Client、VN=4、TransmitTimestamp=UtcNow
    internal NtpPacket(byte[] bytes);                            // < 48 字节 → ArgumentException("SNTP reply packet must be at least 48 bytes long.", "bytes")
    public static NtpPacket FromServerResponse(byte[] bytes, DateTime destinationTimestamp);
    internal void ValidateRequest();
    internal void ValidateReply();
}
```

**字节/位布局**（固定 **48 字节**）

| 偏移 | 字段 |
| --- | --- |
| 0 | LI = bits 7-6，VN = bits 5-3，Mode = bits 2-0 |
| 1 | `Stratum` |
| 2 | `Poll` |
| 3 | `Precision`（带符号） |
| 4-7 | `RootDelay`（大端 16.16 秒） |
| 8-11 | `RootDispersion` |
| 12-15 | `ReferenceId`（大端 u32） |
| 16-23 | `ReferenceTimestamp` |
| 24-31 | `OriginTimestamp` |
| 32-39 | `ReceiveTimestamp` |
| 40-47 | `TransmitTimestamp` |

`DestinationTimestamp` **不在线格式中**。

**默认值**（新建包）：`NoWarning`(0)、VN 4、`Client`(3)、Stratum/Poll/Precision 0、RootDelay/RootDispersion `Zero`、ReferenceId 0、各时间戳 `null`、`TransmitTimestamp = DateTime.UtcNow`。

**单位**：`Poll`/`Precision` 为 log2 秒；`RootDelay`/`RootDispersion` 为 16.16 定点秒；时间戳为自 **1900-01-01** 起的 32.32 定点秒。

> ⚠️ `RoundTripTime` 与 `CorrectionOffset` 都会先调 `CheckTimestamps()` → 在**请求包**上会抛 `InvalidOperationException`。
> ⚠️ `CorrectionOffset` 的 `/2` 是对 `Ticks` 的**整数除法**（向零截断）。

**校验**（**没有** public `Validate`）

```csharp
internal void ValidateRequest();   // 依次抛：
    // "This is not a request SNTP packet."
    // "Protocol version of the request is not specified."
    // "TransmitTimestamp must be set in request packet."

internal void ValidateReply();     // 依次抛：
    // "This is not a reply SNTP packet."
    // "Protocol version of the reply is not specified."
    // string.Format("Received Kiss-o'-Death SNTP packet with code 0x{0:x}.", ReferenceId)
    // "SNTP server has unsynchronized clock."
    // 然后 CheckTimestamps()
```

`CheckTimestamps()` 的消息：`"Origin timestamp is missing."` / `"Receive timestamp is missing."` / `"Transmit timestamp is missing."` / `"Destination timestamp is missing."`

> ⚠️ **不存在 `ToServerTime` / `ToLocalTime`**（整个 `Assets/Scripts/Net` 内都搜不到）；时间换算只能通过 `CorrectionOffset`。
> ⚠️ XML 文档写 `ReferenceTimestamp`/`OriginTimestamp`/`ReceiveTimestamp` "Gets or sets"，但**代码只有 getter**（文档与实现不一致）。
> ⚠️ 大端辅助函数是"宿主序 `BitConverter` 读取后无条件 `SwapEndianness`"，因此**在小端主机上才正确**。

```csharp
public enum NtpLeapIndicator   // 底层 int
{
    NoWarning = 0,                 // 默认
    LastMinuteHas61Seconds = 1,
    LastMinuteHas59Seconds = 2,
    AlarmCondition = 3
}

public enum NtpMode           // 底层 int
{
    Client = 3,                // 显式赋值
    Server = 4                 // 显式赋值
}
```

### 20.2 `NtpRequest`（`internal sealed class`，其 public 成员实际仅程序集内可见）

```csharp
internal sealed class NtpRequest
{
    private const int ResendTimer = 1000;    // 毫秒
    private const int KillTimer   = 10000;   // 毫秒
    public const int DefaultPort  = 123;     // UDP 端口

    public NtpRequest(IPEndPoint endPoint);  // 不校验 null
    public bool NeedToKill { get; }          // => _killTime >= KillTimer
    public bool Send(Socket socket, float time);
}
```

`Send` 精确行为：

```csharp
_resendTime += time;
_killTime   += time;
if (_resendTime < ResendTimer) return false;
var packet = new NtpPacket();
try {
    int sendCount = socket.SendTo(packet.Bytes, 0, packet.Bytes.Length, SocketFlags.None, _ntpEndPoint);
    return sendCount == packet.Bytes.Length;
} catch { return false; }
```

> `time` 单位是**毫秒**（调用方传 `elapsedMilliseconds`）。
> ⚠️ **代码与注释不符**：`_resendTime` **初始值就等于 `ResendTimer` 且发送成功后从不重置**，因此第一次调用就会通过阈值、之后每次都通过 → 实际是**每个 manager tick 都发一次**，而不是注释所说的"每秒一次"。
> `_killTime` 单调增长，与是否收到回复无关。
> **不存在** `Kill()` / `Poll` / `Receive` / 事件 / 回调 / async —— 完全同步、由外部驱动。

**出站泵**：`LiteNetManager.ProcessNtpRequests` 为空实现（注释 `not used in lite version`）；`NetManager` 覆写为对每个请求 `Send(_udpSocketv4, elapsedMilliseconds)`，并移除 `NeedToKill` 的条目。

**入站派发**（`NetManager.CustomMessageHandle`）：端点匹配 → 回复 < 48 字节则记 trace `NTP response too short: {packet.Size}` 并吞掉；否则拷贝字节、`NtpPacket.FromServerResponse(copiedData, DateTime.UtcNow)`、`ValidateReply()`，`InvalidOperationException` 时记 `NTP response error: {ex.Message}` 并丢弃；成功则从字典移除并回调 `INetEventListener.OnNtpResponse(NtpPacket)`。

**回调签名**

```csharp
void INetEventListener.OnNtpResponse(NtpPacket packet) { }        // 默认空实现
public delegate void OnNtpResponseEvent(NtpPacket packet);        // EventBasedNetListener 内
```

创建入口：`NetManager.CreateNtpRequest(IPEndPoint)` / `(string, int)` / `(string)`（用 `DefaultPort`），存入 `ConcurrentDictionary<IPEndPoint, NtpRequest>`。**重复创建同一端点会被 `TryAdd` 静默忽略。**

---

## 3. 监听接口与事件类型

### 3.1 `UnconnectedMessageType`

```csharp
public enum UnconnectedMessageType
{
    BasicMessage,   // = 0
    Broadcast       // = 1
}
```

在 `OnNetworkReceiveUnconnected` 中区分是普通无连接消息还是广播。由 `HandleMessageReceived` 按 `PacketProperty.Broadcast` / `PacketProperty.UnconnectedMessage` 决定。

### 3.2 `DisconnectReason`

```csharp
public enum DisconnectReason
{
    ConnectionFailed,        // = 0  连接主机失败
    Timeout,                 // = 1  超时
    HostUnreachable,         // = 2
    NetworkUnreachable,      // = 3
    RemoteConnectionClose,   // = 4  远端主动断开
    DisconnectPeerCalled,    // = 5  本地调用 Disconnect 主动断开
    ConnectionRejected,      // = 6  被远端拒绝
    InvalidProtocol,         // = 7
    UnknownHost,             // = 8
    Reconnect,               // = 9
    PeerToPeerConnection,    // = 10
    PeerNotFound             // = 11
}
```

> `UnknownHost` 的典型触发点：`LiteNetManager.Connect(string, int, ...)` 里 DNS 解析 `NetUtils.MakeEndPoint` 抛异常时，会直接创建一条 `Disconnect` 事件并返回 `null`。

### 3.3 `DisconnectInfo`

```csharp
public struct DisconnectInfo
{
    public DisconnectReason Reason;         // 断开原因
    public SocketError SocketErrorCode;     // 仅当原因为 socket 收发错误时有效
    public NetPacketReader AdditionalData;  // 仅当 Reason == RemoteConnectionClose 时可能有数据
}
```

### 3.4 `ILiteNetEventListener` / `INetEventListener`

> ⚠️ **重要纠正**：这两个接口是**彼此独立**的，源码声明为
> `public interface INetEventListener`（第 80 行）和 `public interface ILiteNetEventListener`（第 156 行），
> **`INetEventListener` 并不继承 `ILiteNetEventListener`**。它们各自重复声明了一套语义相近但类型不同的回调（`INetEventListener` 用 `NetPeer` + 带通道号，`ILiteNetEventListener` 用 `LiteNetPeer` + 无通道号）。
> 因此 `NetManager` 与 `LiteNetManager` 的监听器类型**不可互换**。

```csharp
public interface ILiteNetEventListener
{
    void OnPeerConnected(LiteNetPeer peer);                                          // 必须实现
    void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo);       // 必须实现
    void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod); // 必须实现
    void OnConnectionRequest(LiteConnectionRequest request);                         // 必须实现

    // 有默认空实现，可选覆写：
    void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
    void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    void OnNetworkLatencyUpdate(LiteNetPeer peer, int latency) { }
    void OnPeerAddressChanged(LiteNetPeer peer, IPEndPoint previousAddress) { }
    void OnMessageDelivered(LiteNetPeer peer, object userData) { }
}
```

```csharp
public interface INetEventListener          // 注意：不继承 ILiteNetEventListener
{
    // 以下 7 个均无默认实现，实现类必须全部提供：
    void OnPeerConnected(NetPeer peer);
    void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
    void OnNetworkError(IPEndPoint endPoint, SocketError socketError);
    void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod);
    void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);
    void OnNetworkLatencyUpdate(NetPeer peer, int latency);
    void OnConnectionRequest(ConnectionRequest request);

    // 仅以下 3 个有默认空实现，可选覆写：
    void OnMessageDelivered(NetPeer peer, object userData) { }
    void OnNtpResponse(NtpPacket packet) { }                       // 仅增强版有
    void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) { }
}
```

**核心差异（务必注意）**

| | `ILiteNetEventListener` | `INetEventListener` |
| --- | --- | --- |
| 与另一个接口的关系 | 独立接口 | 独立接口（**不继承**前者） |
| `OnNetworkReceive` 签名 | `(LiteNetPeer, NetPacketReader, DeliveryMethod)` | `(NetPeer, NetPacketReader, **byte channelNumber**, DeliveryMethod)` |
| `OnConnectionRequest` 参数 | `LiteConnectionRequest` | `ConnectionRequest` |
| 必须实现的成员数 | 4 | 7 |
| `OnNtpResponse` | 不存在 | 存在 |

> ⚠️ 实现 `INetEventListener` 时即使不关心某些事件，也必须写出空方法体（因为 7 个成员都没有默认实现）。

**回调触发时机（源码事实）**

- 事件默认由后台逻辑线程生成并**入队**（`CreateEvent` → `_pendingEventHead/Tail`），在主线程调用 `PollEvents()` 时逐个 `ProcessEvent` 派发。
- 若 `UnsyncedEvents = true`，**所有**事件在生成线程立即派发；`UnsyncedReceiveEvent` / `UnsyncedDeliveryEvent` 只把接收/送达事件切成立即派发。
- 手动模式（`_manualMode`，由 `StartInManualMode` 设置）下事件也立即派发（`CreateEvent` 里 `if (unsyncEvent || _manualMode)`）。
- 事件派发后是否回收 reader 由 `AutoRecycle` 决定：`if (emptyData) RecycleEvent(evt); else if (AutoRecycle) evt.DataReader.RecycleInternal();`

### 3.5 `EventBasedLiteNetListener` / `EventBasedNetListener`

```csharp
public class EventBasedLiteNetListener : ILiteNetEventListener
public class EventBasedNetListener     : INetEventListener
```

两者都是**显式接口实现 + `event` 转发**的简易监听器（源码注释称其适用于简易场景与基准测试；自己实现接口会略快，因为少一层委托调用）。

`EventBasedLiteNetListener` 的事件与委托（**无 `channelNumber`**）：

| 委托 / 事件 | 签名 |
| --- | --- |
| `OnPeerConnected` / `PeerConnectedEvent` | `(LiteNetPeer peer)` |
| `OnPeerDisconnected` / `PeerDisconnectedEvent` | `(LiteNetPeer peer, DisconnectInfo disconnectInfo)` |
| `OnNetworkError` / `NetworkErrorEvent` | `(IPEndPoint endPoint, SocketError socketError)` |
| `OnNetworkReceive` / `NetworkReceiveEvent` | `(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)` |
| `OnNetworkReceiveUnconnected` / `NetworkReceiveUnconnectedEvent` | `(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)` |
| `OnNetworkLatencyUpdate` / `NetworkLatencyUpdateEvent` | `(LiteNetPeer peer, int latency)` |
| `OnConnectionRequest` / `ConnectionRequestEvent` | `(LiteConnectionRequest request)` |
| `OnDeliveryEvent` / `DeliveryEvent` | `(LiteNetPeer peer, object userData)` |
| `OnPeerAddressChangedEvent` / `PeerAddressChangedEvent` | `(LiteNetPeer peer, IPEndPoint previousAddress)` |

`EventBasedNetListener` 的事件与委托（**`OnNetworkReceive` 多一个 `byte channel`**）：

| 委托 / 事件 | 签名 |
| --- | --- |
| `OnPeerConnected` / `PeerConnectedEvent` | `(NetPeer peer)` |
| `OnPeerDisconnected` / `PeerDisconnectedEvent` | `(NetPeer peer, DisconnectInfo disconnectInfo)` |
| `OnNetworkError` / `NetworkErrorEvent` | `(IPEndPoint endPoint, SocketError socketError)` |
| `OnNetworkReceive` / `NetworkReceiveEvent` | `(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)` |
| `OnNetworkReceiveUnconnected` / `NetworkReceiveUnconnectedEvent` | `(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)` |
| `OnNetworkLatencyUpdate` / `NetworkLatencyUpdateEvent` | `(NetPeer peer, int latency)` |
| `OnConnectionRequest` / `ConnectionRequestEvent` | `(ConnectionRequest request)` |
| `OnDeliveryEvent` / `DeliveryEvent` | `(NetPeer peer, object userData)` |
| `OnNtpResponseEvent` / `NtpResponseEvent` | `(NtpPacket packet)` |
| `OnPeerAddressChangedEvent` / `PeerAddressChangedEvent` | `(NetPeer peer, IPEndPoint previousAddress)` |

**清空订阅者的辅助方法**（两者都有，用于防止监听器持有引用导致泄漏）：

```csharp
public void ClearPeerConnectedEvent();
public void ClearPeerDisconnectedEvent();
public void ClearNetworkErrorEvent();
public void ClearNetworkReceiveEvent();
public void ClearNetworkReceiveUnconnectedEvent();
public void ClearNetworkLatencyUpdateEvent();
public void ClearConnectionRequestEvent();
public void ClearDeliveryEvent();
public void ClearPeerAddressChangedEvent();
public void ClearNtpResponseEvent();          // 仅 EventBasedNetListener
```

> 每个方法体都是把对应 `event` 置为 `null`，签名与命名一一对应。

---

## 10. `NetDataWriter`

```csharp
namespace LiteNetLib.Utils
public class NetDataWriter          // 非 sealed / 非 static / 非 partial，无显式基类、无接口
```

**字段与常量**

```csharp
protected byte[] _data;
protected int    _position;
private const int InitialSize = 64;
private readonly bool _autoResize;
private const int IPv4Size = 4;
private const int IPv6Size = 16;
private const int GuidSize = 16;
internal static readonly UTF8Encoding uTF8Encoding = new UTF8Encoding(false, true);
```

> `uTF8Encoding` 是 **`internal static readonly`**，`false` 表示**不写 BOM**，`true` 表示**遇非法字节序列抛异常**。`NetDataReader` 复用同一实例。
> **注意**：整个 `NetDataWriter.cs` **没有** `#if` 条件编译。

### 10.1 属性与只读视图

```csharp
public int    Capacity { get; }   // => _data.Length
public byte[] Data     { get; }   // => _data（直接暴露内部数组，绕过边界检查）
public int    Length   { get; }   // => _position（已写入字节数）
public ReadOnlySpan<byte> AsReadOnlySpan();   // new ReadOnlySpan<byte>(_data, 0, _position)
```

> `AsReadOnlySpan()` 返回的 span 在**扩容或 `_position` 变化后失效**（源码注释明示）。

### 10.2 构造与工厂

```csharp
public NetDataWriter();                                  // => this(true, 64)
public NetDataWriter(bool autoResize);                   // => this(autoResize, 64)
public NetDataWriter(bool autoResize, int initialSize);  // _data = new byte[initialSize]; _autoResize = autoResize;

public static NetDataWriter FromBytes(byte[] bytes, bool copy);
public static NetDataWriter FromBytes(byte[] bytes, int offset, int length);  // 总是拷贝
public static NetDataWriter FromBytes(Span<byte> bytes);                      // 总是拷贝
public static NetDataWriter FromString(string value);                         // new NetDataWriter() + Put(value)
```

- `FromBytes(bytes, copy: false)` **零拷贝**：直接把 `_data` 指向调用方数组，`Capacity == Length == bytes.Length`。写入副作用由调用方承担。
- `FromBytes(bytes, copy: true)` 等价于构造 64 容量后 `Put(bytes)`。

### 10.3 缓冲增长与位置控制

```csharp
public void ResizeIfNeed(int newSize);
    // if (_data.Length < newSize) Array.Resize(ref _data, Math.Max(newSize, _data.Length * 2));
public void EnsureFit(int additionalSize);
    // if (_data.Length < _position + additionalSize) Array.Resize(ref _data, Math.Max(_position + additionalSize, _data.Length * 2));

public void Reset(int size);        // ResizeIfNeed(size); _position = 0;
public void Reset();                // _position = 0;（不清零内容、不缩容）
public byte[] CopyData();           // 新建 byte[_position] + BlockCopy（堆分配）
public int SetPosition(int position);// 返回旧的 position，可回退以覆写已写数据
```

- 增长策略：扩容到 **`max(需求值, 当前容量 × 2)`**；**没有容量上限**。
- **`_autoResize == false` 时 `Put*` 不会自动扩容**（所有 `Put*` 内的扩容都包在 `if (_autoResize)` 里）；但 `ResizeIfNeed` / `EnsureFit` / `Reset(int)` 是 public 且**无条件**扩容。
- **不存在 `Recycle()`**（只有 `Reset`）。

### 10.4 基础类型写入

```csharp
public void Put(float value);   public void Put(double value);
public void Put(long value);    public void Put(ulong value);
public void Put(int value);     public void Put(uint value);
public void Put(char value);    // 2 字节
public void Put(ushort value);  public void Put(short value);
public void Put(sbyte value);   public void Put(byte value);
public void Put(bool value) => Put((byte)(value ? 1 : 0));   // true→1, false→0
public void Put(Guid value);
```

- 多字节整数统一走 `FastBitConverter.GetBytes`：**按宿主内存原样写出，无任何端序转换**，源码中**不出现 `BitConverter`**，也不检查 `BitConverter.IsLittleEndian`。实际支持平台上即小端。
- `Put(Guid)` 用 `Guid.TryWriteBytes` 的默认混合端序布局（`TryWriteBytes` 的返回值被忽略，失败时位置仍前进 16）；与 `NetDataReader.GetGuid()` 可在本库内往返。
- **不存在压缩整数编码**（无 `PutCompressed` / ZigZag / 7-bit 变长）。

### 10.5 字符串

```csharp
public void Put(string value, int maxLength = 0);
public void PutLargeString(string value);
public void PutArray(string[] value);
public void PutArray(string[] value, int strMaxLength);
```

`Put(string value, int maxLength = 0)` 的**精确行为**：

1. `null` 或空串 → 写 `Put((ushort)0)`（2 字节 0）后返回。
2. `maxLength > 0 && source.Length > maxLength` → **按字符**截断。
3. 预扩容 `maxSize = uTF8Encoding.GetMaxByteCount(source.Length)`；`ResizeIfNeed(_position + maxSize + sizeof(ushort))`。
4. 在 `_position + 2` 处写 UTF-8 负载；`size == 0` → 写 `Put((ushort)0)`；否则先 `Put(checked((ushort)(size + 1)))` 再 `_position += size`。

> ⚠️ **长度头 = UTF-8 字节数 + 1**（不是字节数本身）。字节数 ≥ 65535 时 `checked` 抛 `OverflowException`。
> ⚠️ `maxLength` 限制的是**字符数**，不是字节数。

`PutLargeString(string value)`：用 **4 字节 `int` 长度头**，`null`/空 → 写 `Put(0)`。用于可能超过 65535 字节的字符串。

> **`null` 不可往返**：`null` 与空串序列化后读回均为 `string.Empty`。

### 10.6 数组 / 集合

```csharp
public void PutArray(Array arr, int sz);                       // sz = 单元素字节数
public void PutUnmanagedArray<T>(T[] arr) where T : unmanaged; // => PutSpan(arr.AsSpan())
public unsafe void PutSpan<T>(Span<T> span) where T : unmanaged;

public void PutArray(float[] value);   public void PutArray(double[] value);
public void PutArray(long[] value);    public void PutArray(ulong[] value);
public void PutArray(int[] value);     public void PutArray(uint[] value);
public void PutArray(ushort[] value);  public void PutArray(short[] value);
public void PutArray(bool[] value);
public void PutArray(string[] value);
public void PutArray(string[] value, int strMaxLength);
public void PutArray<T>(T[] value) where T : INetSerializable, new();

public void PutSBytesWithLength(sbyte[] data, int offset, ushort length);
public void PutSBytesWithLength(sbyte[] data);                 // => PutArray(data, 1)
public void PutBytesWithLength(byte[] data, int offset, ushort length);
public void PutBytesWithLength(byte[] data);                   // => PutArray(data, 1)
```

- **所有数组写出格式统一为：2 字节 `ushort` 元素个数头 + 裸负载**。
- `ushort` 转换是**静默截断**（`(ushort)arr.Length`）：长度 > 65535 时回绕，源码未校验。
- `PutSpan<T>` 的 `byteLength = length * sizeof(T)` 是**未检查乘法**（无 `checked`）。
- `null` 数组：`PutSpan`/`PutUnmanagedArray` 因 `AsSpan()` 返回空 span 而等价于写长度 0；`PutArray(string[])` / `PutArray<T>(T[])` 显式写长度 0。
  → **读回是空数组而非 `null`**。
- `PutSBytesWithLength(data, offset, length)` / `PutBytesWithLength(...)` 写入的是**调用方给定的 `length`**，不重新取 `data.Length`；`length > 0` 且 `data == null` 会 `NullReferenceException`。

### 10.7 `IPEndPoint` 写入

```csharp
public void Put(IPEndPoint endPoint);
```

- 格式：**1 字节 family 标志**（`InterNetwork`=0 / `InterNetworkV6`=1）+ 地址字节（4 或 16）+ **2 字节 `ushort` 端口**。
- 不支持的地址族抛 `ArgumentException($"Unsupported address family: {endPoint.AddressFamily}")`。
- `TryWriteBytes` 失败抛 `ArgumentException("Failed to write IP bytes.")`。
- `endPoint == null` → `NullReferenceException`（无判空）。

### 10.8 泛型 / unmanaged / 枚举 / 可序列化对象

```csharp
public unsafe void PutUnmanaged<T>(T value) where T : unmanaged;
public void PutNullableUnmanaged<T>(T? value) where T : unmanaged;
    // Put(hasValue); if (!hasValue) return; PutUnmanaged(value.Value);
public unsafe void PutEnum<T>(T value) where T : unmanaged, Enum;
public void Put<T>(T obj) where T : INetSerializable => obj.Serialize(this);
```

- `PutNullableUnmanaged<T>` 的 null 表示 = **单字节 `false`(0)**。
- `Put<T>(T obj)` 对 `null` → `NullReferenceException`（无判空）。

### 10.9 `_autoResize == false` 时的失败模式

| 写入路径 | 容量不足时抛出的异常 |
| --- | --- |
| 经 `FastBitConverter.GetBytes`（`PutUnmanaged`、`PutEnum`、`PutArray`、`PutSpan` 头、`PutSBytesWithLength` 头） | `IndexOutOfRangeException`（FastBitConverter 显式检查） |
| 经 `Span.CopyTo` / `Encoding.GetBytes`（`Put(ReadOnlySpan<byte>)`、`PutSpan` 负载、`Put(string)`） | `ArgumentException`（BCL 抛出，源码未处理） |

---

## 11. `NetDataReader`

```csharp
namespace LiteNetLib.Utils
public class NetDataReader          // 非 sealed / 非 static / 非 partial，无显式基类、无接口
```

**字段与常量**

```csharp
protected byte[] _data;
protected int    _position;
protected int    _dataSize;   // 绝对结束索引（不是长度！）
protected int    _offset;     // 用户负载起始偏移
private const int IPv4Size = 4;
private const int IPv6Size = 16;
private const int GuidSize = 16;
```

> **`_dataSize` 是绝对结束索引**，不是长度。构造/`SetSource` 时从 `offset` 开始。

### 11.1 属性（全部只读）

```csharp
public byte[] RawData        { get; }   // => _data（不判空、不拷贝）
public int    RawDataSize    { get; }   // => _dataSize（整块缓冲结束索引）
public int    UserDataOffset { get; }   // => _offset
public int    UserDataSize   { get; }   // => _dataSize - _offset
public bool   IsNull         { get; }   // => _data == null
public int    Position       { get; }   // => _position（相对 _data 的绝对索引）
public bool   EndOfData      { get; }   // => _position == _dataSize（严格相等）
public int    AvailableBytes { get; }   // => _dataSize - _position（不钳制，可为负）
```

- 默认构造后 `IsNull == true`，此时 `AvailableBytes == 0`、`EndOfData == true`。
- 越界（如 `SkipBytes` 过大）后 `AvailableBytes` 可为**负数**，且 `EndOfData` 变回 `false`。
- **读者持有源数组的引用，不做拷贝**：外部修改源数组会影响读取结果。
- **不存在** `HasError` / `MaxErrorPosition` / 错误位置追踪 / `Recycle()`。

### 11.2 构造与重初始化

```csharp
public NetDataReader();
public NetDataReader(NetDataWriter writer);                    // => SetSource(writer)
public NetDataReader(byte[] source);                           // => SetSource(source)
public NetDataReader(byte[] source, int offset, int maxSize);  // => SetSource(source, offset, maxSize)

public void SetSource(NetDataWriter dataWriter);   // _position = _offset = 0; _dataSize = writer.Length
public void SetSource(byte[] source);              // _position = _offset = 0; _dataSize = source.Length
public void SetSource(byte[] source, int offset, int maxSize);
```

> ⚠️ `SetSource(byte[], int, int)` 的第三个参数 `maxSize` 被当作**绝对结束索引**（赋给 `_dataSize`），不是"长度"。这正是 `UserDataSize = _dataSize - _offset` 的原因。
> `SetSource(byte[])` 对 `null` 会 `NullReferenceException`（访问 `source.Length`）。三个 `SetSource` 与构造函数都**不校验** `offset`/`maxSize` 合法性。

### 11.3 位置控制

```csharp
public void SkipBytes(int count);       // _position += count;（无边界校验，可为负、可越界）
public void SetPosition(int position);  // _position = position;（无校验）
public void Clear();                    // _position = _offset = _dataSize = 0; _data = null;
```

- `Clear()` 是读者侧唯一的状态重置：**释放对缓冲区的引用**（`_data = null`），不释放数组本身。
- **没有 `Reset()` / `Recycle()`**。

### 11.4 异常语义

```csharp
private void EnsureAvailable(int count);
    // int available = _dataSize - _position;
    // if (count < 0 || available < 0 || count > available) ThrowNotEnoughData(count);

private void ThrowNotEnoughData(int count);
    // throw new InvalidOperationException($"Not enough data to read {count} byte(s). Position={_position}, DataSize={_dataSize}");
```

- 数据不足统一抛 **`InvalidOperationException`**，消息为：
  `Not enough data to read {count} byte(s). Position={_position}, DataSize={_dataSize}`
- 本类**不抛 `ArgumentOutOfRangeException`**。
- ⚠️ **XML 注释与实现不一致**：`GetUnmanaged<T>` / `GetNullableUnmanaged<T>` / `TryGetUnmanaged<T>` 的 `<exception cref="IndexOutOfRangeException">` 与实际行为（`InvalidOperationException`）不符；`TryGetUnmanaged<T>` 实际不抛异常。

### 11.5 基础类型读取

```csharp
// 便捷重载：Get(out T)
public void Get(out byte result);    public void Get(out sbyte result);
public void Get(out bool result);    public void Get(out char result);
public void Get(out ushort result);  public void Get(out short result);
public void Get(out ulong result);   public void Get(out long result);
public void Get(out uint result);    public void Get(out int result);
public void Get(out double result);  public void Get(out float result);
public void Get(out string result);  public void Get(out string result, int maxLength);
public void Get(out Guid result);    public void Get(out IPEndPoint result);

// 具体方法
public byte GetByte();      // EnsureAvailable(1); return _data[_position++];
public sbyte GetSByte() => (sbyte)GetByte();
public bool GetBool() => GetByte() == 1;      // 仅 1 为 true
public char GetChar()   => GetUnmanaged<char>();      // 2 字节
public ushort GetUShort() => GetUnmanaged<ushort>();
public short GetShort()   => GetUnmanaged<short>();
public long GetLong()     => GetUnmanaged<long>();
public ulong GetULong()   => GetUnmanaged<ulong>();
public int GetInt()       => GetUnmanaged<int>();
public uint GetUInt()     => GetUnmanaged<uint>();
public float GetFloat()   => GetUnmanaged<float>();
public double GetDouble() => GetUnmanaged<double>();
```

> ⚠️ **`GetBool()` 是 `== 1` 判断，不是"非零即真"**。而 `GetBoolArray()` / `TryGetBool` 走裸字节重解释，语义**不同**。

多字节读取全部走 `GetUnmanaged<T>`，即**宿主内存布局原样解释**；源码中**从不调用 `BitConverter`**，也**不检查 `BitConverter.IsLittleEndian`**。因此所有实际平台上为小端，文件内**没有**任何大端转换代码。

### 11.6 字符串

```csharp
public string GetString();                     // 2 字节 ushort 头
public string GetString(int maxLength);        // maxLength 为字符数上限；<=0 表示无限制
public string GetLargeString();                // 4 字节 int 头
public string[] GetStringArray();
public string[] GetStringArray(int maxStringLength);
```

长度头语义（与 `Put` 对称）：

- `GetString()`：读 `ushort size` → `actualSize = size - 1`；**`size == 0` 直接返回 `string.Empty`**（不做 `EnsureAvailable`，不移动位置）。
- 编码为 `new UTF8Encoding(false, true)`：**无 BOM、非法字节序列抛异常**（`DecoderFallbackException` / `EncoderFallbackException`）。
- `GetString(int maxLength)`：当 `maxLength > 0` 且 `GetCharCount(...) > maxLength` → 返回 `string.Empty`，**但位置仍前移 `actualSize`**（数据被消费掉）。
- `GetLargeString()`：读 `int size`，`size <= 0` → `string.Empty`；否则 `EnsureAvailable(size)` 后按 UTF-8 解码。

### 11.7 数组 / 集合读取

```csharp
public unsafe T[] GetUnmanagedArray<T>() where T : unmanaged;
public T[] GetArray<T>(ushort size);                          // size = 单元素字节数
public T[] GetArray<T>() where T : INetSerializable, new();
public T[] GetArray<T>(Func<T> constructor) where T : class, INetSerializable;

public bool[] GetBoolArray();      public ushort[] GetUShortArray();
public short[] GetShortArray();    public int[] GetIntArray();
public uint[] GetUIntArray();      public float[] GetFloatArray();
public double[] GetDoubleArray();  public long[] GetLongArray();
public ulong[] GetULongArray();
public sbyte[] GetSBytesWithLength();   // => GetUnmanagedArray<sbyte>()
public byte[]  GetBytesWithLength()  => GetUnmanagedArray<byte>();
```

- 所有数组读取都以 **2 字节 `ushort` 元素个数**开头。
- `GetUnmanagedArray<T>` 标了 `unsafe` 但**方法体内没有任何指针操作**（只用 `MemoryMarshal.Cast` + `ToArray()`）；`ToArray()` 必然堆分配一次。
- `GetArray<T>(ushort size)` 的 `size` 是单元素字节数，元素个数来自流中的 `ushort`。
- `GetArray<T>()` / `GetArray<T>(Func<T>)` **不校验可用字节**，依赖各元素自己的 `Deserialize` 抛错。
- `GetStringArray()` 的边界预检是 `EnsureAvailable(checked(length * sizeof(ushort)))`，即只保证"每个字符串至少 2 字节头"的下限；`checked` 溢出抛 `OverflowException`。

### 11.8 原始字节 / 段 / 剩余数据

```csharp
public ArraySegment<byte> GetBytesSegment(int count);
public ArraySegment<byte> GetRemainingBytesSegment();
public ReadOnlySpan<byte> GetRemainingBytesSpan();
public ReadOnlyMemory<byte> GetRemainingBytesMemory();
public byte[] GetRemainingBytes();
public void GetBytes(byte[] destination, int start, int count);
public void GetBytes(byte[] destination, int count);
```

- `GetRemainingBytes()`：`size == 0` → `Array.Empty<byte>()`；否则 `Buffer.BlockCopy` 新建数组（**堆分配**），位置置为 `_dataSize`。
- 三个 "Segment/Span/Memory" 版本**不调用 `EnsureAvailable`**，只返回视图。

### 11.9 泛型 / 枚举 / 可空 / unmanaged

```csharp
public void Get<T>(out T result) where T : struct, INetSerializable;
public void Get<T>(out T result, Func<T> constructor) where T : class, INetSerializable;
public T Get<T>() where T : struct, INetSerializable;
public T Get<T>(Func<T> constructor) where T : class, INetSerializable;

public unsafe T GetUnmanaged<T>() where T : unmanaged;
public T? GetNullableUnmanaged<T>() where T : unmanaged;
    // bool hasValue = GetBool(); false → null（消耗 1 字节）；true → GetUnmanaged<T>()
public unsafe T GetEnum<T>() where T : unmanaged, Enum => GetUnmanaged<T>();
```

### 11.10 `IPEndPoint` 读取

```csharp
public IPEndPoint GetIPEndPoint();
```

先读 1 字节：**`== 0` → IPv4（4 字节），任何非 0 值 → IPv6（16 字节）**（注释只写 0/1，代码是 `== 0` 判断）。
随后 `EnsureAvailable(size)` → `new IPAddress(new ReadOnlySpan<byte>(_data, _position, size))` → 位置前移 → `new IPEndPoint(address, GetUShort())`（端口 2 字节小端）。

### 11.11 Peek（读但不移动位置）

```csharp
public byte PeekByte();      public sbyte PeekSByte();
public bool PeekBool();      // == 1
public char PeekChar();      public ushort PeekUShort();
public short PeekShort();    public long PeekLong();
public ulong PeekULong();    public int PeekInt();
public uint PeekUInt();      public float PeekFloat();
public double PeekDouble();
public string PeekString();  public string PeekString(int maxLength);
public unsafe T PeekUnmanaged<T>() where T : unmanaged;
public ReadOnlySpan<byte> PeekRemainingBytesSpan();
public ReadOnlyMemory<byte> PeekRemainingBytesMemory();
public byte[] PeekRemainingBytes();
```

- **Peek 系列全部无边界校验、无 `EnsureAvailable`**；越界时由 BCL 抛 `IndexOutOfRangeException` / `NullReferenceException`。
- `PeekString` 使用与 `GetString` 相同的 `size - 1` 头语义与编码，位置不变。

### 11.12 TryGet（不抛异常的读取）

```csharp
public bool TryGetByte(out byte result);      public bool TryGetSByte(out sbyte result);
public bool TryGetBool(out bool result);      public bool TryGetChar(out char result);
public bool TryGetShort(out short result);    public bool TryGetUShort(out ushort result);
public bool TryGetInt(out int result);        public bool TryGetUInt(out uint result);
public bool TryGetLong(out long result);      public bool TryGetULong(out ulong result);
public bool TryGetFloat(out float result);    public bool TryGetDouble(out double result);

public bool TryGetString(out string result);
public bool TryGetStringArray(out string[] result);
public bool TryGetBytesWithLength(out byte[] result);

public unsafe bool TryGetUnmanaged<T>(out T result) where T : unmanaged;
    // AvailableBytes < size → result = default; return false（不移动位置）
```

- 失败时 `TryGetString` 置 `result = null` 并返回 `false`。
- `TryGetStringArray` 在失败时**显式回滚** `_position = startPosition`。
- `TryGetBool` 用的是 `TryGetUnmanaged<bool>`（裸字节重解释），**不是** `GetBool()` 的 `== 1` 语义。

### 11.13 `#if` 条件

`NetDataReader.cs` 中 `#if NET8_0_OR_GREATER` 出现 **3 处**（`GetUnmanaged<T>` / `PeekUnmanaged<T>` / `TryGetUnmanaged<T>`）：
- 新版 → `Unsafe.ReadUnaligned<T>(ref ...)`（无对齐要求）
- 旧版 → `fixed` + `*(T*)ptr`（要求指针可安全解引用）

> 读者侧**没有** Android 专用的非对齐分支（该处理只在 writer 侧的 `FastBitConverter` 中）。

---

## 13. `NetSerializer`

```csharp
namespace LiteNetLib.Utils
public class NetSerializer           // 无基类、无接口
public class InvalidTypeException : ArgumentException   // ctor(string message)
public class ParseException       : Exception           // ctor(string message)
```

**私有状态**

```csharp
private NetDataWriter _writer;                    // 仅供 Serialize<T>(T obj) 复用
private readonly int _maxStringLength;
private readonly Dictionary<Type, CustomType> _registeredTypes = new Dictionary<Type, CustomType>();
```

### 13.1 public 成员

```csharp
public NetSerializer();                       // => this(0)
public NetSerializer(int maxStringLength);

public void RegisterNestedType<T>() where T : struct, INetSerializable;
public void RegisterNestedType<T>(Func<T> constructor) where T : class, INetSerializable;
public void RegisterNestedType<T>(Action<NetDataWriter, T> writer, Func<NetDataReader, T> reader);   // 对 T 无约束

public void Register<T>();
public T Deserialize<T>(NetDataReader reader) where T : class, new();
public bool Deserialize<T>(NetDataReader reader, T target) where T : class, new();
public void Serialize<T>(NetDataWriter writer, T obj) where T : class, new();
public byte[] Serialize<T>(T obj) where T : class, new();
```

- `RegisterNestedType` 用 **`Dictionary.Add`**（不是索引器）→ **重复注册同一类型会抛 `ArgumentException`**。
- `Deserialize<T>(reader)`：**任何读取异常都被吞掉并返回 `null`**；`Deserialize<T>(reader, target)` 同形，失败返回 `false`（target 可能已被部分写入，reader 位置**不回滚**）。
- `RegisterInternal<T>` 抛出的 `InvalidTypeException` 在 try **之外**，因此会正常抛出。
- `Serialize<T>(T obj)` 懒创建并复用**同一个** `_writer`，返回 `_writer.CopyData()`。

### 13.2 `RegisterInternal<T>()` —— 成员发现规则

- 只序列化 **public 实例属性**，顺序取决于 `Type.GetProperties` 的反射顺序（**运行时未保证**）。
- `[IgnoreDataMember]`（`System.Runtime.Serialization`）→ **完全排除该成员**。
- 属性缺少 **public** 的 getter 或 setter → **静默跳过**（不报错）。
- 元素类型判定顺序：`enum` → `string` → `bool` → `byte` → `sbyte` → `short` → `ushort` → `int` → `uint` → `long` → `ulong` → `float` → `double` → `char` → `IPEndPoint` → `Guid` → 已注册的嵌套类型。
- 数组 → `CallType.Array`；`List<>` → `CallType.List`（泛型判定在元素类型判定**之前**）。
- 全部属性成功后才写 `ClassInfo<T>.Instance`，因此注册失败后补注册嵌套类型**可以重试**。

> ⚠️ 源码中**没有**"必须有字段"的检查：一个没有可序列化属性的类型会注册成功并序列化为 **0 字节**（XML 文档里声称会抛异常的 "has no fields" 分支并未实现）。

### 13.3 线格式（由 `NetDataWriter`/`NetDataReader` 决定）

**单值（`CallType.Basic`）**

| 类型 | 字节数 | 备注 |
| --- | ---: | --- |
| `bool` | 1 | `1`/`0`；读取是 `== 1` 判断 |
| `byte` / `sbyte` | 1 | |
| `short` / `ushort` / `char` | 2 | `char` 按 `ushort` 序列化 |
| `int` / `uint` / `float` | 4 | |
| `long` / `ulong` / `double` | 8 | |
| `string` | 2 + N | 头 = **UTF-8 字节数 + 1**；`0` 表示空串 |
| `IPEndPoint` | 1+4/16+2 | family 标志（`0`=IPv4，非 0=IPv6）+ 地址 + `ushort` 端口 |
| `Guid` | 16 | `Guid.TryWriteBytes` 布局（前三段小端、后 8 字节大端） |

- **没有 `Nullable<T>` 支持**：`int?` 之类的成员会被当作未注册类型 → `InvalidTypeException("Unknown property type: System.Nullable\`1[...]")`。
- **没有** `decimal` / `DateTime` / `object` / 字典 / 集合（除 `List<>`）支持。

**数组（`CallType.Array`）** —— 统一为 **2 字节 `ushort` 元素数 + 负载**：

| 元素类型 | 写出 / 读入 |
| --- | --- |
| `bool,ushort,short,int,uint,float,double,long,ulong` | `PutArray` → `PutUnmanagedArray` / `GetUnmanagedArray<T>()` |
| `byte` | `PutBytesWithLength` / `GetBytesWithLength()` |
| `sbyte` | `PutSBytesWithLength` / `GetSBytesWithLength()` |
| `string` | `PutArray(string[], maxLength)` / `GetStringArray(maxLength)` |
| `char` / `IPEndPoint` / `Guid` | `FastCallSpecificAuto` 的逐元素路径 |
| 已注册嵌套类型 | `FastCallStruct`/`FastCallClass`/`FastCallStatic` 的数组路径 |
| **enum** | ❌ 抛 `InvalidTypeException("Unsupported type: Enum[]")` |

> ⚠️ `WriteArrayHelper` 是 `w.Put((ushort)arr.Length)`，**没有判空** → 除 `byte[]`/`sbyte[]` 外的**数组成员为 `null` 时抛 `NullReferenceException`**。

**`List<T>`（`CallType.List`）**

- 信封同为 2 字节 `ushort` 数量；`null` 列表写 `0`，读回是**空列表（非 null）**；读取会复用现有列表（`Add` 补足、`RemoveRange` 收缩、就地重填）。
- ⚠️ **只有已注册的嵌套类型真正支持 List**。`List<int>` / `List<string>` / `List<Guid>` 等**在注册期被接受**（`List<>` 分支先于元素类型判定），但会在**读写期抛** `InvalidTypeException("Unsupported type: List<…>")`。enum 列表抛 `InvalidTypeException("Unsupported type: List<Enum>")`。

**枚举**

- 底层 `byte` → `EnumByteSerializer`（1 字节）；底层 `int` → `EnumIntSerializer`（4 字节）。
- 其它底层类型 → `InvalidTypeException("Not supported enum underlying type: " + underlyingType.Name)`（即 `long`/`short`/`uint` 等枚举均**不支持**）。
- 枚举成员的读写走 `PropertyInfo.SetValue/GetValue` 反射 —— 这是序列化器中**唯一**的按值反射路径。

### 13.4 异常总表

| 场景 | 异常与消息 |
| --- | --- |
| 元素类型非内置且未注册 | `InvalidTypeException("Unknown property type: " + propertyType.FullName)` |
| 枚举底层类型非 `byte`/`int` | `InvalidTypeException("Not supported enum underlying type: " + underlyingType.Name)` |
| 枚举数组 | `InvalidTypeException("Unsupported type: Enum[]")` |
| 枚举列表 | `InvalidTypeException("Unsupported type: List<Enum>")` |
| 内置类型的 `List<T>` | `InvalidTypeException("Unsupported type: List<" + typeof(TProperty) + ">")`（**读写期**才抛） |
| 同一类型重复 `RegisterNestedType` | `Dictionary.Add` 抛 `ArgumentException` |
| 反序列化失败 | 被吞掉：返回 `null` / `false` |

### 13.5 `StringSerializer` 的长度限制

```csharp
_maxLength = maxLength > 0 ? maxLength : short.MaxValue;   // 即 32767
```

→ `new NetSerializer()` / `new NetSerializer(0)` 的写截断与读限制都使用 **32767 字符**。

### 13.6 缓存与线程安全（**重点**）

| 机制 | 说明 |
| --- | --- |
| `ClassInfo<T>.Instance` | **`private` 嵌套泛型类上的 `public static` 字段**，每闭合 `T` 一份，**进程内所有 `NetSerializer` 实例共享** |
| 检查-再赋值 | `RegisterInternal` **无锁**；两线程可能并发构建（后者覆盖，功能等价） |
| `_maxStringLength` 的烘焙 | ⚠️ **第一个**注册该类型的实例会把自己的 `_maxStringLength` 烤进缓存的 `StringSerializer`；之后 `new NetSerializer(其他值)` 会**静默复用**旧限制 |
| `_registeredTypes` | 每实例 `Dictionary<Type, CustomType>`，`Add`/读取**无同步** |
| `_writer` | 单个复用 writer，`Serialize<T>(T obj)` 并发调用会互相破坏缓冲 |
| 锁/原子 | **无 `ConcurrentDictionary`、无 `lock`、无 `Monitor`、无 `Interlocked`** |

→ **`NetSerializer` 仅在单线程、且嵌套类型在首次使用前注册完毕时才安全。**

### 13.7 `#if`

`#if NET5_0_OR_GREATER` 仅包裹 `RegisterInternal<T>`、`Register<T>`、两个 `Deserialize<T>`、两个 `Serialize<T>` 上 `T` 的 `[DynamicallyAccessedMembers(Trimming.SerializerMemberTypes)]` —— 仅影响裁剪器，**无运行时行为差异**。

---

## 14. `NetPacketProcessor`

```csharp
namespace LiteNetLib.Utils
public class NetPacketProcessor          // 无基类、无接口
```

### 14.1 嵌套类型

```csharp
private static class HashCache<T>
{
    public static readonly ulong Id;     // 由静态构造函数一次性计算
}
protected delegate void SubscribeDelegate(NetDataReader reader, object userData);
```

**哈希算法**（FNV-1 64 位，输入是 `typeof(T).ToString()`）：

```csharp
ulong hash = 14695981039346656037UL; // offset
string typeName = typeof(T).ToString();
for (var i = 0; i < typeName.Length; i++)
{ hash ^= typeName[i]; hash *= 1099511628211UL; }   // prime
Id = hash;
```

> ⚠️ 哈希只基于**完整类型名**（含命名空间、泛型反引号形式），**不含程序集身份** → 不同程序集会话中同名类型会产生相同哈希。**重命名或移动包类型会改变线格式 ID**。

### 14.2 字段与构造

```csharp
private readonly NetSerializer _netSerializer;
private readonly Dictionary<ulong, SubscribeDelegate> _callbacks = new Dictionary<ulong, SubscribeDelegate>();

public NetPacketProcessor();                     // => new NetSerializer()
public NetPacketProcessor(int maxStringLength);  // => new NetSerializer(maxStringLength)
```

### 14.3 可覆写扩展点（自定义哈希协议的唯一途径）

```csharp
protected virtual ulong GetHash<T>();                                        // => HashCache<T>.Id
protected virtual SubscribeDelegate GetCallbackFromData(NetDataReader reader);
protected virtual void WriteHash<T>(NetDataWriter writer);                   // => writer.Put(GetHash<T>())

// GetCallbackFromData 实现：
ulong hash = reader.GetULong();
if (!_callbacks.TryGetValue(hash, out var action))
    throw new ParseException("Undefined packet in NetDataReader");
return action;
```

> **没有**公开的"注册哈希"API；要改哈希方案必须派生并覆写，且 `WriteHash<T>` 与 `GetCallbackFromData` 必须保持对称。

### 14.4 public 成员（精确签名）

```csharp
// 嵌套类型注册（直接转发给 NetSerializer）
public void RegisterNestedType<T>() where T : struct, INetSerializable;
public void RegisterNestedType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate);
public void RegisterNestedType<T>(Func<T> constructor) where T : class, INetSerializable;

// 读 / 轮询
public void ReadAllPackets(NetDataReader reader);
public void ReadAllPackets(NetDataReader reader, object userData);
public void ReadPacket(NetDataReader reader);
public void ReadPacket(NetDataReader reader, object userData);

// 写
public void Write<T>(NetDataWriter writer, T packet) where T : class, new();
public void WriteNetSerializable<T>(NetDataWriter writer, ref T packet) where T : INetSerializable;

// 订阅
public void Subscribe<T>(Action<T> onReceive, Func<T> packetConstructor) where T : class, new();
public void Subscribe<T, TUserData>(Action<T, TUserData> onReceive, Func<T> packetConstructor) where T : class, new();
public void SubscribeReusable<T>(Action<T> onReceive) where T : class, new();
public void SubscribeReusable<T, TUserData>(Action<T, TUserData> onReceive) where T : class, new();
public void SubscribeNetSerializable<T, TUserData>(Action<T, TUserData> onReceive, Func<T> packetConstructor) where T : INetSerializable;
public void SubscribeNetSerializable<T>(Action<T> onReceive, Func<T> packetConstructor) where T : INetSerializable;
public void SubscribeNetSerializable<T, TUserData>(Action<T, TUserData> onReceive) where T : INetSerializable, new();
public void SubscribeNetSerializable<T>(Action<T> onReceive) where T : INetSerializable, new();

// 取消订阅 —— 注意方法名是 RemoveSubscription<T>，没有 Unsubscribe
public bool RemoveSubscription<T>();
```

`TUserData` 在**任何重载上都没有约束**，且通过**拆箱强转** `(TUserData)userData` 应用 → 类型不匹配抛 `InvalidCastException`；`ReadPacket(reader)` 传 `null`，因此**值类型的 `TUserData` 会失败**。

### 14.5 `RegisterNestedType` 行为

三个重载都是对 `_netSerializer` 的直通。**必须在首次对包含该成员类型的包调用 `Register<T>()`/`Subscribe<T>()` 之前完成注册**，因为 `NetSerializer` 在首次使用时解析并缓存成员序列化器。

### 14.6 写机制

```csharp
public void Write<T>(NetDataWriter writer, T packet) where T : class, new()
{
    WriteHash<T>(writer);                    // 8 字节 FNV-1 哈希（宿主端序）
    _netSerializer.Serialize(writer, packet);
}
public void WriteNetSerializable<T>(NetDataWriter writer, ref T packet) where T : INetSerializable
{
    WriteHash<T>(writer);
    packet.Serialize(writer);
}
```

> ⚠️ **本文件里没有 `NetPeer`、没有 `DeliveryMethod`、没有任何 `Send` 方法** —— 处理器只负责填好一个 `NetDataWriter`，发送由调用方自己完成。

### 14.7 读 / 派发机制

```csharp
public void ReadAllPackets(NetDataReader reader)              { while (reader.AvailableBytes > 0) ReadPacket(reader); }
public void ReadAllPackets(NetDataReader reader, object userData) { while (reader.AvailableBytes > 0) ReadPacket(reader, userData); }
public void ReadPacket(NetDataReader reader)                  { ReadPacket(reader, null); }
public void ReadPacket(NetDataReader reader, object userData) { GetCallbackFromData(reader)(reader, userData); }
```

| 事实 | 说明 |
| --- | --- |
| 调用时机 | **同步、立即**在 `ReadPacket`/`ReadAllPackets` 内部执行 —— 回调运行在调用者的线程上 |
| 队列/事件 | **没有**队列、没有派发器、没有 C# `event` |
| reader 实例 | 回调收到的是**同一个** `NetDataReader`（已消费完 8 字节哈希） |
| 回收 | `NetPacketProcessor` **不做任何回收**；`Recycle`/`RecycleInternal`/`AutoRecycle`/`NetPacketReader` 在本文件中**完全不出现** |
| 异常 | 回调或反序列化抛出的异常**直接向外传播**，reader 位置**不回滚** |

### 14.8 订阅存储与失败行为

- **存储**：每实例一个 `Dictionary<ulong, SubscribeDelegate>`，键 = 哈希，值 = 单个委托。
- ⚠️ 赋值用**索引器** `_callbacks[GetHash<T>()] = …` → **重复订阅同一类型、或任何 FNV-1 哈希碰撞，都会静默覆盖先前的回调**；先订阅者从此收不到包，**无任何错误或警告**。每个哈希只能存在一个回调。
- **未知哈希**：抛 `ParseException("Undefined packet in NetDataReader")`。
- **哈希被截断**（reader 剩余不足 8 字节）：由 `NetDataReader.EnsureAvailable` 抛 `InvalidOperationException("Not enough data to read 8 byte(s). …")`。
- **嵌套类型未注册**：注册包含它的包类型时抛 `InvalidTypeException("Unknown property type: " + propertyType.FullName)`。
- **移除**：`RemoveSubscription<T>()` 返回是否移除成功；**没有**"全部取消"、**没有**回调令牌。

### 14.9 `Subscribe` / `SubscribeReusable` 的存储委托体（逐字要点）

```csharp
// Subscribe<T>(onReceive, packetConstructor) —— 每个包新建实例
_netSerializer.Register<T>();
_callbacks[GetHash<T>()] = (reader, userData) => {
    var reference = packetConstructor();
    _netSerializer.Deserialize(reader, reference);
    onReceive(reference);
};

// SubscribeReusable<T>(onReceive) —— 复用同一个实例，每包覆盖
_netSerializer.Register<T>();
var reference = new T();
_callbacks[GetHash<T>()] = (reader, userData) => {
    _netSerializer.Deserialize(reader, reference);
    onReceive(reference);
};

// SubscribeNetSerializable<T, TUserData>(onReceive, packetConstructor)
_callbacks[GetHash<T>()] = (reader, userData) => {
    var pkt = packetConstructor();
    pkt.Deserialize(reader);
    onReceive(pkt, (TUserData)userData);
};
```

| 家族 | 是否走 `_netSerializer` | 实例分配 |
| --- | --- | --- |
| `Subscribe*` / `SubscribeReusable*`（`class, new()`） | 是（反射序列化器，会调 `Register<T>()`） | `Subscribe` 每包新建；`SubscribeReusable` 复用**同一个可变实例** |
| `SubscribeNetSerializable*` | 否（包自己序列化） | 带构造器版本每包新建；无参版本复用同一实例 |

> ⚠️ **复用实例的陷阱**：`SubscribeReusable*` 与无参 `SubscribeNetSerializable*` 会把**同一个可变对象**交给每次回调 —— 本次包未覆盖的字段会**残留上次的值**；且重入/多线程使用不安全。

### 14.10 线程安全

- `_callbacks` 是**普通 `Dictionary`，无锁**；`Subscribe*`、`RemoveSubscription`、`ReadPacket`、`ReadAllPackets` **都不是线程安全的**。
- `HashCache<T>.Id` 是 `static readonly` —— 靠 CLR 静态初始化保证线程安全。
- 本文件**没有** `ConcurrentDictionary`、`lock`、`Interlocked`。

### 14.11 `#if`

`#if NET5_0_OR_GREATER` 包裹 `Write<T>`、`Subscribe<T>`、`Subscribe<T,TUserData>`、`SubscribeReusable<T>`、`SubscribeReusable<T,TUserData>` 上 `T` 的 `[DynamicallyAccessedMembers(Trimming.SerializerMemberTypes)]`（仅裁剪器提示）。

---

## 15. `INetSerializable`

```csharp
namespace LiteNetLib.Utils
public interface INetSerializable        // 不继承任何其它接口
{
    /// <summary>Writes the object data into the provided <see cref="NetDataWriter"/>.</summary>
    void Serialize(NetDataWriter writer);

    /// <summary>Reads the object data from the provided <see cref="NetDataReader"/>.</summary>
    void Deserialize(NetDataReader reader);
}
```

类型级注释要点：
- 摘要："Interface for implementing custom data serialization for network transmission."
- remarks："This is the most efficient way to send complex objects as it avoids reflection."

**在本库中的调用点**

| 调用方 | 行为 |
| --- | --- |
| `NetDataWriter.Put<T>(T obj) where T : INetSerializable` | `obj.Serialize(this)` |
| `NetDataWriter.PutArray<T>(T[]) where T : INetSerializable, new()` | 逐元素 `value[i].Serialize(this)` |
| `NetDataReader.Get<T>(out T) where T : struct, INetSerializable` | `default(T)` 后 `result.Deserialize(this)` |
| `NetDataReader.Get<T>(out T, Func<T>) where T : class, INetSerializable` | 由 `constructor` 创建后 `Deserialize` |
| `NetDataReader.GetArray<T>() where T : INetSerializable, new()` | 逐个 `new T()` + `Deserialize(this)` |
| `NetDataReader.GetArray<T>(Func<T>) where T : class` | 逐个 `Get(out result[i], constructor)` |

> 接口**不规定**任何长度前缀、版本号或类型标记；是否自描述完全由实现者决定。

---

## 19. `CRC32C` / `FastBitConverter`

### 19.1 `FastBitConverter`（`LiteNetLib.Utils`）

```csharp
public static class FastBitConverter
{
    public static unsafe void GetBytes<T>(byte[] bytes, int startIndex, T value) where T : unmanaged;
    private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();
}
```

**这是本类唯一公开的成员**。精确行为：

1. `int size = sizeof(T);`
2. 越界检查：`if (bytes.Length < startIndex + size) ThrowIndexOutOfRangeException();`
   → 抛 **`IndexOutOfRangeException`，无消息、无 `paramName`**。
   (`startIndex < 0` **没有**显式检查，由运行时异常兜底。)
3. 写值（三种编译分支）：
   - `NET8_0_OR_GREATER` → `Unsafe.WriteUnaligned<T>(ref bytes[startIndex], value)`，**不要求对齐**。
   - 否则 `fixed (byte* ptr = &bytes[startIndex])`：
     - `UNITY_ANDROID` → `T* valueBuffer = stackalloc T[1] { value }; UnsafeUtility.MemCpy(ptr, valueBuffer, size);`
       （源码注释解释：某些 Android 系统上对非对齐指针执行 `*(T*)ptr = value` 会抛 NRE，故改用 memcpy；用 `stackalloc` 避免 GC 与封送分配。）
     - 其它平台 → `*(T*)ptr = value;`

- **完全不使用 `System.BitConverter`**，也不用 `BinaryPrimitives`。
- **无字节交换、无 `BitConverter.IsLittleEndian` 判断** → 线格式等于宿主端序。

### 19.2 `CRC32C`（`LiteNetLib.Utils`）

```csharp
namespace LiteNetLib.Utils
//Implementation from Crc32.NET
public static class CRC32C
{
    public const int ChecksumSize = 4;          // 字节
    private const uint Poly = 0x82F63B78u;      // 反射形式 Castagnoli（正规形式 0x1EDC6F41）
    private static readonly uint[] Table;       // 16 × 256 = 4096，slicing-by-16
    public static uint Compute(byte[] input, int offset, int length);
}
```

> ⚠️ **本类只有一个方法**：**不存在** `Append` / `ComputeAndAppend`，也没有 `Compute(byte[])` 或 Span 重载。追加校验和是由 `Crc32cLayer` 用 `FastBitConverter.GetBytes` 完成的。

- `Compute(input, offset, length)`：`static uint`，参数**无默认值**，**不修改** `input`；**整个文件没有 `unsafe`、没有 `fixed`**；无边界校验；`length == 0` → 返回 `0`。
- 静态构造函数中的硬件加速选择：

```
#if NETCOREAPP3_0_OR_GREATER || NETCOREAPP3_1 || NET5_0
    if (Sse42.IsSupported) return;          // 此时 Table 保持 null
#endif
#if NET5_0_OR_GREATER || NET5_0
    if (Crc32.IsSupported) return;          // ARM Crc32
#endif
    // 否则用 NetUtils.AllocatePinnedUninitializedArray<uint>(16 * 256) 建立软件表
```

- 三条计算路径：
  1. **SSE4.2**（x86）：`crcLocal = uint.MaxValue`；`Sse42.X64.IsSupported && len > 8` 时按 `ulong` 走 `Sse42.X64.Crc32`，否则 `len > 4` 时按 `uint` 走 `Sse42.Crc32`，余数字节逐字节；最后 `^ uint.MaxValue`。
  2. **ARM**（`Crc32.IsSupported`，含 `Crc32.Arm64`）：同形，走 `ComputeCrc32C`。
  3. **软件 slicing-by-16**：`while (length >= 16)` 用表 0..15 行构建 `a,b,c,d`，`crcLocal = d ^ c ^ b ^ a`；再 `while (--length >= 0) crcLocal = Table[(byte)(crcLocal ^ input[offset++])] ^ crcLocal >> 8;`，最后 `^ uint.MaxValue`。
- 三条路径都是标准 CRC-32C：初值 `0xFFFFFFFF`、末尾异或 `uint.MaxValue`（`"123456789"` → `0xE3069283`）。
- 校验和的端序由 `FastBitConverter.GetBytes` 决定：**宿主端序**（读回用 `BitConverter.ToUInt32`，同为宿主端序 → 两端一致；实际平台均为小端）。

---

## 18. `PacketLayerBase` 与内置层

```csharp
namespace LiteNetLib.Layers

public abstract class PacketLayerBase
{
    public readonly int ExtraPacketSizeForLayer;                  // 本层对出站包额外增加的字节数
    protected PacketLayerBase(int extraPacketSizeForLayer);

    public abstract void ProcessInboundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int length);
    public abstract void ProcessOutBoundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length);
}

public sealed class Crc32cLayer : PacketLayerBase        // ctor: base(CRC32C.ChecksumSize) → ExtraPacketSizeForLayer == 4
public class XorEncryptLayer : PacketLayerBase            // ctor: base(0) → ExtraPacketSizeForLayer == 0
```

> 注意方法名是 `ProcessOutBoundPacket`（**大写 B**）。

### 18.1 契约

| 方面 | 说明 |
| --- | --- |
| 原地修改 | 两个方法都以 `ref byte[] data` 接收真实缓冲并**原地**改写，返回 `void` |
| `length` 语义 | 入站 `length` 与 `NetPacket.Size` 别名；**设为 `0` 即等于丢弃该包**（`LiteNetManager.cs` 中 `if (packet.Size == 0) return;`） |
| `offset` | **只有出站**签名有 `offset`；两个内置调用点都传 **0** |
| `endPoint` | 传 `ref` 以便层改写地址；两个内置层都不改；广播路径传 `null` |
| 出站调用时机 | 在 socket 发送前；`SendRaw` 先 `PoolGetPacket(length + ExtraPacketSizeForLayer)` 并把负载拷到下标 0（`start = 0`）再调层 |
| 入站调用时机 | 统计 → `CustomMessageHandle` → **层** → `packet.Verify()` |

> ⚠️ **每个 manager 只支持一个层实例**（`private readonly PacketLayerBase _extraPacketLayer;`，构造参数 `PacketLayerBase extraPacketLayer = null`）——**没有列表/流水线**，因此**层与层之间的执行顺序在源码中未作规定**。要同时用 CRC + XOR 必须自己写一个合并子类。
> ⚠️ 入站时层运行在 `packet.Verify()` **之前**，所以层必须能容忍"尚未通过 LiteNetLib 自身包头校验"的数据。所有互连的 manager **必须使用相同的层**。

### 18.2 `Crc32cLayer`

**入站（精确）**

```csharp
if (length < NetConstants.HeaderSize + CRC32C.ChecksumSize)      // length < 5
    { NetDebug.WriteError("[NM] DataReceived size: bad!"); length = 0; return; }

checksumPoint = length - 4;
if (CRC32C.Compute(data, 0, checksumPoint) != BitConverter.ToUInt32(data, checksumPoint))
    { NetDebug.Write("[NM] DataReceived checksum: bad!"); length = 0; return; }   // Trace 级

length -= 4;                                                     // 成功：排除尾部 4 字节
```

**出站（精确）**

```csharp
FastBitConverter.GetBytes(data, length, CRC32C.Compute(data, offset, length));
length += CRC32C.ChecksumSize;
```

- **只加 4 字节尾部、不加头部**；校验范围是 `[offset, offset+length)`（入站从绝对下标 0 开始，即**包含 LiteNetLib 自己的包头**）。
- ⚠️ 尾部写在**绝对下标 `length`** 处，因此**仅在 `offset == 0` 时正确**；两个调用点都传 0，`offset != 0` 的行为**源码未作处理/说明**。
- ⚠️ **校验失败或包过短都是"丢弃（`length = 0`）"，从不抛异常**；层内没有 try/catch；没有容量检查（由调用方预留 `ExtraPacketSizeForLayer`）。

### 18.3 `XorEncryptLayer`

```csharp
public XorEncryptLayer();
public XorEncryptLayer(byte[] key);
public XorEncryptLayer(string key);
public void SetKey(string key);          // _byteKey = Encoding.UTF8.GetBytes(key)  —— 无 null/长度校验
public void SetKey(byte[] key);          // 长度不同才重新分配；Buffer.BlockCopy 拷贝（调用方后续改动无效）
```

- **入站**：`if (_byteKey == null) return;` 然后对 `i ∈ [0, length)` 执行 `data[i] ^= _byteKey[i % _byteKey.Length]`（**总是从绝对下标 0 开始**，入站没有 `offset` 参数）。
- **出站**：`if (_byteKey == null) return;` 然后从 `cur = offset` 开始逐字节异或。
- 不加头/尾，**length 从不改变**；重复密钥 XOR，加解密同一操作；两个方向上密钥下标都在区间起点重新从 0 开始。
- ⚠️ **密钥为 `null` → 静默直通（不加密、不丢弃）**。
- ⚠️ **密钥长度为 0 → 取模除零 → `DivideByZeroException`**（源码无防护）。
- **没有 MAC/完整性校验**（XOR 单独使用不具备安全性，仅作混淆用途）。

---

## 附录 A：条件编译符号总表

整个库中**只有 3 处 `#define`**：`UNITY_SOCKET_FIX`（`LiteNetManager.cs:2`、`LiteNetManager.Socket.cs:2`，各由 `#if UNITY_2018_3_OR_NEWER` 守卫）与 `STATS_ENABLED`（`LiteNetPeer.cs:2`，由 `#if DEBUG` 守卫）。其余符号均需外部提供。

| 符号 | 出现位置 | 效果 |
| --- | --- | --- |
| `UNITY_2018_3_OR_NEWER` | `LiteNetManager.cs:1`、`LiteNetManager.Socket.cs:1`、`PausedSocketFix.cs:1`、`NetDebug.cs:50`(用 `UNITY_5_3_OR_NEWER`) | 在前两个文件里本地 `#define UNITY_SOCKET_FIX`；`NetDebug` 据此选 `UnityEngine.Debug.Log` 或 `Console.WriteLine` |
| `UNITY_SOCKET_FIX` | `LiteNetManager.cs:2,293,1497`；`LiteNetManager.Socket.cs:2,21,352,461` | 给 `LiteNetManager` 构造函数增加 `bool useSocketFix = true` 参数；启用 `PausedSocketFix` 的创建/初始化与 `Stop` 时的反初始化 |
| `UNITY_SWITCH` | `LiteNetManager.Socket.cs:47-57` | `Ttl` getter 返回 `0`，setter 变空操作 |
| `UNITY_ANDROID` | `Utils\FastBitConverter.cs:3,32,44` | 引入 `Unity.Collections.LowLevel.Unsafe`；用 `stackalloc T[1]` + `UnsafeUtility.MemCpy` 替代 `*(T*)ptr = value` |
| `UNITY_2019_1_OR_NEWER` / `UNITY_2018_4_OR_NEWER` / `UNITY_EDITOR` / `ENABLE_IL2CPP` | `LiteNetManager.Socket.cs:65` | 静态构造函数分支：老版本 Unity + IL2CPP 时解析 `Application.unityVersion`，次版本 ≥ 6 才启用 IPv6 |
| `DISABLE_IPV6` | `LiteNetManager.Socket.cs:63` | 最高优先级分支，直接 `IPv6Support = false` |
| `DEBUG` | `LiteNetPeer.cs:1`；`LiteNetManager.cs:48,555,764,793,827,1517`；`LiteNetManager.Socket.cs:532` | 在 `LiteNetPeer` 中定义 `STATS_ENABLED`；启用延迟模拟字段与方法 |
| `SIMULATE_NETWORK` | 同上位置 | 与 `DEBUG` 相同的延迟模拟块。**库内未定义**，需由使用方/CI 提供 |
| `STATS_ENABLED` | `LiteNetPeer.cs:2` | 仅在 `DEBUG` 下定义；控制 peer 统计代码 |
| `NET8_0_OR_GREATER` | `LiteNetManager.HashSet.cs:219`；`LiteNetManager.Socket.cs:26,249,583`；`LiteNetPeer.cs:266,307`；`Utils\NetDataReader.cs:668,792,1005`；`Utils\FastBitConverter.cs:27` | 使用 `Span<byte>` 版 `ReceiveFrom`/`SendTo`、`SocketAddress` 哈希、`Unsafe.WriteUnaligned` |
| `NET5_0_OR_GREATER` | `NatPunchModule.cs:166`；`Trimming.cs:1,12`；`NetUtils.cs:209`；`Utils\NetPacketProcessor.cs:122,155,176,197,218`；`Utils\NetSerializer.cs:579,679,694,720,744,758`；`Utils\CRC32C.cs:6,25,87` | 加 `[DynamicallyAccessedMembers]` 裁剪标注；编译 `Trimming` 类；`GC.AllocateUninitializedArray<T>`（pinned）；ARM `Crc32` 内联路径 |
| `NET5_0`（非标准别名） | `NetUtils.cs:209`；`Utils\CRC32C.cs:6,25,87` | 与 `NET5_0_OR_GREATER` 或运算，走同一分支（非 SDK 定义符号） |
| `NETCOREAPP3_0_OR_GREATER` / `NETCOREAPP3_1` | `Utils\CRC32C.cs:1,21,52` | 启用 x86 `Sse42` 硬件 CRC32C 路径 |
| `DEBUG_MESSAGES` | `NetDebug.cs`（作为 `[Conditional]` 字符串，非 `#if`） | **控制是否生成 `NetDebug.Write*` 调用**；未定义则日志在编译期被整体剔除 |
| `TRACE` / `NETSTANDARD*` | — | 库内未使用 |

---

## 附录 B：文件清单

38 个 `.cs` 文件。`LiteNetManager` 是**一个 public 类型分布在 4 个 partial 文件中**。

| 文件 | 字节 | 行数 | 命名空间 | 顶层类型 |
| --- | ---: | ---: | --- | --- |
| `BaseChannel.cs` | 3340 | 83 | `LiteNetLib` | `internal abstract class BaseChannel` |
| `ConnectionRequest.cs` | 9324 | 214 | `LiteNetLib` | `internal enum ConnectionRequestResult`; `public class LiteConnectionRequest`; `public class ConnectionRequest` |
| `INetEventListener.cs` | 26726 | 524 | `LiteNetLib` | `public enum UnconnectedMessageType`; `public enum DisconnectReason`; `public struct DisconnectInfo`; `public interface INetEventListener`; `public interface ILiteNetEventListener`; `public class EventBasedNetListener`; `public class EventBasedLiteNetListener` |
| `InternalPackets.cs` | 5170 | 131 | `LiteNetLib` | `public sealed class NetConnectRequestPacket`; `internal sealed class NetConnectAcceptPacket` |
| `LiteNetManager.cs` | 70787 | 1672 | `LiteNetLib` | `public partial class LiteNetManager`（嵌套 `public struct NetPeerEnumerator<T>`） |
| `LiteNetManager.HashSet.cs` | 11592 | 323 | `LiteNetLib` | `public partial class LiteNetManager` |
| `LiteNetManager.PacketPool.cs` | 2618 | 82 | `LiteNetLib` | `public partial class LiteNetManager` |
| `LiteNetManager.Socket.cs` | 28522 | 726 | `LiteNetLib` | `public partial class LiteNetManager` |
| `LiteNetPeer.cs` | 55625 | 1357 | `LiteNetLib` | `public enum ConnectionState`; `internal enum ConnectRequestResult`; `internal enum DisconnectResult`; `internal enum ShutdownResult`; `public class LiteNetPeer` |
| `NativeSocket.cs` | 10837 | 222 | `LiteNetLib` | `internal static class NativeSocket` |
| `NatPunchModule.cs` | 14047 | 341 | `LiteNetLib` | `public enum NatAddressType`; `public interface INatPunchListener`; `public class EventBasedNatPunchListener`; `public sealed class NatPunchModule` |
| `NetConstants.cs` | 5371 | 124 | `LiteNetLib` | `public enum DeliveryMethod`; `public static class NetConstants` |
| `NetDebug.cs` | 2467 | 92 | `LiteNetLib` | `public class InvalidPacketException`; `public class TooBigPacketException`; `public enum NetLogLevel`; `public interface INetLogger`; `public static class NetDebug` |
| `NetEvent.cs` | 4038 | 107 | `LiteNetLib` | `public sealed class NetEvent`（嵌套 `public enum EType`） |
| `NetManager.cs` | 19222 | 408 | `LiteNetLib` | `public class NetManager : LiteNetManager, IEnumerable<NetPeer>` |
| `NetPacket.cs` | 8066 | 221 | `LiteNetLib` | `internal enum PacketProperty`; `internal sealed class NetPacket` |
| `NetPacketReader.cs` | 1129 | 44 | `LiteNetLib` | `public sealed class NetPacketReader : NetDataReader` |
| `NetPeer.cs` | 13659 | 269 | `LiteNetLib` | `public class NetPeer : LiteNetPeer` |
| `NetStatistics.cs` | 4400 | 123 | `LiteNetLib` | `public sealed class NetStatistics` |
| `NetUtils.cs` | 11508 | 256 | `LiteNetLib` | `public enum LocalAddrType`; `public static class NetUtils`; `internal class NetworkSorter` |
| `PausedSocketFix.cs` | 2171 | 67 | `LiteNetLib` | `public class PausedSocketFix` |
| `PooledPacket.cs` | 1037 | 32 | `LiteNetLib` | `public readonly ref struct PooledPacket` |
| `ReliableChannel.cs` | 16923 | 445 | `LiteNetLib` | `internal sealed class MergedPacketUserData`; `internal sealed class ReliableChannel : BaseChannel` |
| `SequencedChannel.cs` | 4106 | 114 | `LiteNetLib` | `internal sealed class SequencedChannel : BaseChannel` |
| `Trimming.cs` | 351 | 12 | `LiteNetLib` | `internal static class Trimming`（整个文件位于 `#if NET5_0_OR_GREATER` 内） |
| `Layers\Crc32cLayer.cs` | 1403 | 41 | `LiteNetLib.Layers` | `public sealed class Crc32cLayer : PacketLayerBase` |
| `Layers\PacketLayerBase.cs` | 561 | 17 | `LiteNetLib.Layers` | `public abstract class PacketLayerBase` |
| `Layers\XorEncryptLayer.cs` | 1582 | 59 | `LiteNetLib.Layers` | `public class XorEncryptLayer : PacketLayerBase` |
| `Utils\CRC32C.cs` | 5927 | 150 | `LiteNetLib.Utils` | `public static class CRC32C` |
| `Utils\FastBitConverter.cs` | 2387 | 53 | `LiteNetLib.Utils` | `public static class FastBitConverter` |
| `Utils\INetSerializable.cs` | 849 | 23 | `LiteNetLib.Utils` | `public interface INetSerializable` |
| `Utils\NetDataReader.cs` | 46922 | 1029 | `LiteNetLib.Utils` | `public class NetDataReader` |
| `Utils\NetDataWriter.cs` | 28342 | 681 | `LiteNetLib.Utils` | `public class NetDataWriter` |
| `Utils\NetPacketProcessor.cs` | 13805 | 318 | `LiteNetLib.Utils` | `public class NetPacketProcessor` |
| `Utils\NetSerializer.cs` | 33779 | 770 | `LiteNetLib.Utils` | `public class InvalidTypeException`; `public class ParseException`; `public class NetSerializer` |
| `Utils\NtpPacket.cs` | 17784 | 423 | `LiteNetLib.Utils` | `public class NtpPacket`; `public enum NtpLeapIndicator`; `public enum NtpMode` |
| `Utils\NtpRequest.cs` | 2536 | 66 | `LiteNetLib.Utils` | `internal sealed class NtpRequest` |
| `Utils\Preserve.cs` | 597 | 12 | `LiteNetLib.Utils` | `public class PreserveAttribute : Attribute` |

**命名空间分布**：`LiteNetLib` 24 个文件、`LiteNetLib.Layers` 3 个、`LiteNetLib.Utils` 11 个。没有任何文件声明多个命名空间。

### `PreserveAttribute`（`Utils\Preserve.cs`，全文）

```csharp
namespace LiteNetLib.Utils
{
    /// <summary>
    /// PreserveAttribute prevents byte code stripping from removing a class, method, field, or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate, Inherited = false)]
    public class PreserveAttribute : Attribute
    {
    }
}
```

- 类体为空（**空标记特性**，不携带任何元数据参数）。
- `Inherited = false`；未设置 `AllowMultiple`（故默认 `false`）。
- **无任何 `#if` 守卫**，无条件编译。
- 与 `UnityEngine.Scripting.PreserveAttribute` **同名但不同命名空间**，源码未说明二者是否互通。

### `Trimming`（`Utils` 外，`LiteNetLib\Trimming.cs`，仅 `NET5_0_OR_GREATER`）

```csharp
#if NET5_0_OR_GREATER
internal static class Trimming
{
    internal const DynamicallyAccessedMemberTypes SerializerMemberTypes = PublicProperties | NonPublicProperties;
}
#endif
```
