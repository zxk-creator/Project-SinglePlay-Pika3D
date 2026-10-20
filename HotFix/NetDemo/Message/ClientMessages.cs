using UnityEngine;
using LiteNetLib.Utils;
using PKWeb;

/// 包含了所有的，客户端发给服务端的信息

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

/// <summary>
/// 客户端发给服务端的换装请求（通过服务端状态回应同步）
/// </summary>
public class ClientEquipClothIntent : INetSerializable
{
    // 要装备的物品（客户端自己校验是不是ClothBase，如果不是ClothBase就不好发这个）
    public ClothBase clothToEquip;
    public void Serialize(NetDataWriter writer)
    {
        if (clothToEquip == null)
        {
            writer.Put(false);
        }
        else
        {
            writer.Put(true);
            clothToEquip.Serialize(writer);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        if (reader.GetBool())
        {
            clothToEquip = new ClothBase();
            clothToEquip.Deserialize(reader);
        }
    }

}

/// <summary>
/// 客户端发给服务端卸下装备的请求（通过服务端状态回应同步）
/// </summary>
public class ClientTakeOffClothIntent : INetSerializable
{
    // 要卸下的装备插槽
    public EClothType targetSlot;
    public void Deserialize(NetDataReader reader)
    {
        targetSlot = (EClothType)reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((int)targetSlot);
    }
}

/// <summary>
/// 客户端尝试装备/卸下一个工具（通过服务端状态回应同步）
/// </summary>
public class ClientEquipToolIntent : INetSerializable
{
    // 要装备的工具（客户端自己校验是不是ToolBase，如果不是ToolBase就不好发这个）
    // 传null表示卸下手上的工具
    public ToolBase toolToEquip;

    public void Serialize(NetDataWriter writer)
    {
        if (toolToEquip == null)
        {
            writer.Put(false);
        }
        else
        {
            writer.Put(true);
            toolToEquip.Serialize(writer);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        if (reader.GetBool())
        {
            toolToEquip = new ToolBase();
            toolToEquip.Deserialize(reader);
        }
    }
}

/// <summary>
/// 客户端发给服务端卸下背着的背包的请求（通过服务端状态回应同步）
/// </summary>
public class ClientTakeOffBagIntent : INetSerializable
{
    public void Serialize(NetDataWriter writer)
    {
    }

    public void Deserialize(NetDataReader reader)
    {
    }
}

/// <summary>
/// 客户端向服务端发送的复活消息
/// </summary>
public class ClientRevive : INetResponseReceivable, INetSerializable
{
    public uint requestId { get; set; }

    public void Serialize(NetDataWriter w)
    {
        w.Put(requestId);
    }

    public void Deserialize(NetDataReader r)
    {
        requestId = r.GetUInt();
    }
}