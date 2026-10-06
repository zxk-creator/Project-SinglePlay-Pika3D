using LiteNetLib;
using LiteNetLib.Utils;
using PKSv;
using System.IO;
using UnityEngine;

namespace PKWeb;

// 纯数据，两端共用
public class NetPlayerData : INetSerializable
{
    // 玩家名字哈希，唯一标识
    public long playerId = 0;
    // 名字
    public string playerName;
    // 完整存档数据（装备、背包等）
    public SaveData playerData;

    // 位置
    public Vector3 position;
    // 旋转
    public Quaternion rotation = Quaternion.identity;
    // 当前所在场景（服务端用它做过滤：只把人发给同场景的玩家）
    public ESceneType scene = ESceneType.NONE;

    // 服务端：该玩家的连接
    // 客户端：填成"我连服务器的那条连接"
    public NetPeer clientConnect;

    public NetPlayerData() { }

    public NetPlayerData(SaveData data, NetPeer peer)
    {
        playerData = data;
        playerName = data.playerName;
        playerId = Compute(data.playerName);
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
        writer.Put(playerId);
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
        playerId = reader.GetLong();
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

    public static long Compute(string name)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(name);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }
            return (long)hash;
        }
    }
}