using LiteNetLib.Utils;
using UnityEngine;

// 此时我们不得不使用枚举，说明客户端这边正在播放的动画
/// <summary>
/// 客户端发到服务端的位置同步消息
/// </summary>
public class LocationSynchronize : INetSerializable
{
    // 位置
    public Vector3 location;
    // 朝向
    public Quaternion rotation = Quaternion.identity;
    // 正在播放的动画
    public EAnimationState currentAnimation;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(location.x);
        writer.Put(location.y);
        writer.Put(location.z);
        writer.Put(rotation.x);
        writer.Put(rotation.y);
        writer.Put(rotation.z);
        writer.Put(rotation.w);
        writer.Put((int)currentAnimation);
    }

    public void Deserialize(NetDataReader reader)
    {
        location = new Vector3(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat());
        rotation = new Quaternion(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat());
        currentAnimation = (EAnimationState)reader.GetInt();
    }
}

/// <summary>
/// 服务端转发到其他客户端的位置同步消息
/// </summary>
public class OtherPlayerLocationSync : INetSerializable
{
    public string playerName;
    public Vector3 location;
    public Quaternion rotation = Quaternion.identity;
    public EAnimationState currentAnimation;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerName);
        writer.Put(location.x);
        writer.Put(location.y);
        writer.Put(location.z);
        writer.Put(rotation.x);
        writer.Put(rotation.y);
        writer.Put(rotation.z);
        writer.Put(rotation.w);
        writer.Put((int)currentAnimation);
    }

    public void Deserialize(NetDataReader reader)
    {
        playerName = reader.GetString();
        location = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        currentAnimation = (EAnimationState)reader.GetInt();
    }
}
