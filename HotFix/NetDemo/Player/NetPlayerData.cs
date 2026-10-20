using LiteNetLib;
using LiteNetLib.Utils;
using PKSv;
using System.IO;
using UnityEngine;

namespace PKWeb;

// 纯数据，两端共用
public class NetPlayerData : INetSerializable
{
    // 名字，唯一标识
    public string playerName;
    // 存档数据
    public SaveData playerData;
    // TODO: 是否是主机（依赖他进行判断服务器是否应该终止），尚未实现
    public bool isHost = false;

    // 位置
    public Vector3 position;
    // 旋转
    public Quaternion rotation = Quaternion.identity;
    // 当前所在场景（服务端用它做过滤：只把人发给同场景的玩家）
    public ESceneType scene = ESceneType.NONE;

    // 服务端：该玩家的连接
    // 客户端：填成我连服务器的那条连接
    public NetPeer clientConnect;
    // 是否死了，死了不能执行一些操作
    public bool isDead = false;

    public NetPlayerData() { }

    public NetPlayerData(SaveData data, NetPeer peer)
    {
        playerData = data;
        playerName = data.playerName;
        clientConnect = peer;
    }

    // 带位置与旋转创建：服务端建玩家时用，会随这个对象发给所有客户端
    public NetPlayerData(SaveData data, NetPeer peer, Vector3 pos, Quaternion rot, ESceneType inScene)
        : this(data, peer)
    {
        position = pos;
        rotation = rot;
        scene = inScene;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerName);
        writer.Put(position.x);
        writer.Put(position.y);
        writer.Put(position.z);
        writer.Put(rotation.x);
        writer.Put(rotation.y);
        writer.Put(rotation.z);
        writer.Put(rotation.w);
        writer.Put((int)scene);

        var dataWriter = new NetDataWriter();
        playerData.Serialize(dataWriter);
        byte[] dataBytes = dataWriter.CopyData();

        writer.Put(dataBytes.Length);
        writer.Put(new System.ReadOnlySpan<byte>(dataBytes));
        // clientConnect 不写
    }

    public void Deserialize(NetDataReader reader)
    {
        playerName = reader.GetString();
        position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        scene = (ESceneType)reader.GetInt();

        int len = reader.GetInt();
        byte[] dataBytes = new byte[len];
        reader.GetBytes(dataBytes, len);

        playerData = new SaveData();
        playerData.Deserialize(new NetDataReader(dataBytes));
        // clientConnect 由调用方填（Deserialize 拿不到 netPeer）
    }

    public override string ToString() => playerName;
}