using LiteNetLib.Utils;

namespace PKWeb;

public class PlayerCountRequest : INetSerializable
{
    public void Deserialize(NetDataReader reader)
    { }

    public void Serialize(NetDataWriter writer)
    { }
}

public class PlayerCountResponse : INetSerializable
{
    public int count;
    public string[] playerNames;
    // 客户端反序列化成可读数据
    public void Deserialize(NetDataReader r)
    {
        count = r.GetInt();
        playerNames = r.GetStringArray();
    }

    // 服务端发给客户端，序列化的数据
    public void Serialize(NetDataWriter w)
    {
        w.Put(count);
        w.PutArray(playerNames);
    }
}
