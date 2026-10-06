using LiteNetLib.Utils;
using PKWeb;

// 服务端广播：有新玩家进入。带位置，客户端据此把这个人生成到场景里
public class NewPlayerEntered : INetSerializable
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
