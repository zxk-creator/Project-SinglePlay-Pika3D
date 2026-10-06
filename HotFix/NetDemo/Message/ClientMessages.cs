using LiteNetLib.Utils;
using PKWeb;

/// 包含了所有的，发到客户端的信息

// 客户端发给服务端，告知服务端我要进入新场景
// 为什么不需要玩家ID，名称之类的，因为服务端通过NetPeer就能制动是谁。
public class ClientEnterNewScene : INetSerializable
{
    public ESceneType scene;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((int)scene);
    }

    public void Deserialize(NetDataReader reader)
    {
        scene = (ESceneType)reader.GetInt();
    }
}

// 服务端发给其他客户端，某人进了新场景。带上他的完整数据，
// 收到的人直接看 scene 和 scenePlayers 里有没有他来决定"生成"还是"移除"。
// 用 playerId 必须用 long：它是 NetPlayerData.Compute 出来的 64 位哈希，int 装不下。
public class OtherPlayerEnterNewScene : INetSerializable
{
    public NetPlayerData newPlayerData;

    public void Serialize(NetDataWriter writer)
    {
        newPlayerData.Serialize(writer);
    }

    public void Deserialize(NetDataReader reader)
    {
        newPlayerData = new NetPlayerData();
        newPlayerData.Deserialize(reader);
    }
}

// 玩家退出游戏消息，仅服务端发给客户端，客户端直接给服务端发EnterNewScene(Main_Menu)即可
public class PlayerExitGame : INetSerializable
{
    public long playerId;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerId);
    }

    public void Deserialize(NetDataReader reader)
    {
        playerId = reader.GetLong();
    }
}
