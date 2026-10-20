using LiteNetLib.Utils;
using PKWeb;
using UnityEngine;

/// <summary>
/// 服务端发给客户端的ACK消息
/// </summary>
public class ServerCommonResponse : INetSerializable
{
    public uint requestId;
    // 客户端发给服务端请忽略此字段，服务端发给客户端请自动填充此字段
    public bool success;

    public void Serialize(NetDataWriter w)
    {
        w.Put(requestId);
        w.Put(success);
    }

    public void Deserialize(NetDataReader r)
    {
        requestId = r.GetUInt();
        success = r.GetBool();
    }
}

///////////////////////////////////////// 背包 ////////////////////////////////////////////////////////

/// <summary>
/// 仅限服务端发给客户端，客户端背包同步请在LoginResponse里面发送
/// 每次玩家身上装备变了，新增了物品等都会发送这个背包同步信息
/// 如果playerName==，则同步自己身上的物品，如果!=，则会找场景中名字==的角色，按需取用内容并同步他的外观
/// </summary>
public class ServerInventorySync : INetSerializable
{
    public string playerName;
    public bool isGirl;
    public CharacterEquipCollection equipedCloth;

    public BagpackBase handSpace;
    public BagpackBase equipedBag;
    public ItemBase currentEquipedTool;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerName);
        writer.Put(isGirl);

        // 装备槽位（带状态）
        WriteItem(writer, equipedCloth.Hair);
        WriteItem(writer, equipedCloth.Upper);
        WriteItem(writer, equipedCloth.Bottom);
        WriteItem(writer, equipedCloth.Stock);
        WriteItem(writer, equipedCloth.Shoes);
        WriteItem(writer, equipedCloth.Hands);
        WriteItem(writer, equipedCloth.Earring);
        WriteItem(writer, equipedCloth.Face);
        WriteItem(writer, equipedCloth.Glasses);

        // 手部背包：永远非null，直接写
        handSpace.Serialize(writer);

        // 主背包：可能null，用bool标记
        writer.Put(equipedBag != null);
        if (equipedBag != null) equipedBag.Serialize(writer);

        // 工具：可能 null
        WriteItem(writer, currentEquipedTool);
    }

    public void Deserialize(NetDataReader reader)
    {
        playerName = reader.GetString();
        isGirl = reader.GetBool();

        equipedCloth = new CharacterEquipCollection();
        equipedCloth.SetSlot(EClothType.HAIR, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.UPPER, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.BOTTOM, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.STOCKING, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.SHOE, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.HAND, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.EARRING, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.FACE, ReadItem(reader) as ClothBase);
        equipedCloth.SetSlot(EClothType.GLASSES, ReadItem(reader) as ClothBase);

        // 手部背包
        handSpace = new BagpackBase();
        handSpace.Deserialize(reader);

        // 主背包
        if (reader.GetBool())
        {
            equipedBag = new BagpackBase();
            equipedBag.Deserialize(reader);
        }
        else
        {
            equipedBag = null;
        }

        currentEquipedTool = ReadItem(reader);
    }

    // 通用物品（带完整状态）

    private static void WriteItem(NetDataWriter writer, ItemBase item)
    {
        if (item == null) { writer.Put(false); return; }
        writer.Put(true);
        writer.Put((int)item.itemType);
        item.Serialize(writer);
    }

    private static ItemBase ReadItem(NetDataReader reader)
    {
        if (!reader.GetBool()) return null;
        EItemType type = (EItemType)reader.GetInt();
        ItemBase item = ItemBase.NewItem(type);
        item.Deserialize(reader);
        return item;
    }
}


// 服务端发给其他客户端，某人进了新场景。带上他的完整数据，
// 收到的人直接看 scene 和 scenePlayers 里有没有他来决定"生成"还是"移除"。
public class ServerOtherPlayerEnterNewScene : INetSerializable
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
public class ServerPlayerExitGame : INetSerializable
{
    public string playerName;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerName);
    }

    public void Deserialize(NetDataReader reader)
    {
        playerName = reader.GetString();
    }
}

// 注意：死只可能发生在服务端，服务端计算伤害。客户端无权发生死亡。所以这是服务端发到客户端的
// 死亡原因由服务端知道，服务端确定，没有别的死亡原因
// 玩家死亡的消息，客户端和服务端通用
// 每个客户端都会收到这个消息，收到后查看死的是不是自己，如果是则显示死亡特效，如果不是则显示其他玩家死亡特效。
public class ServerPlayerDeath : INetSerializable
{
    public string playerName;
    // 客户端玩家死的时候的速度（用于防止玩家死亡瞬间戛然而止）
    public Vector3 deathVelocity;
    public Vector3 deathAngularVelocity;


    public void Serialize(NetDataWriter writer)
    {
        writer.Put(playerName);
        writer.Put(deathVelocity.x);
        writer.Put(deathVelocity.y);
        writer.Put(deathVelocity.z);
        writer.Put(deathAngularVelocity.x);
        writer.Put(deathAngularVelocity.y);
        writer.Put(deathAngularVelocity.z);
    }

    public void Deserialize(NetDataReader reader)
    {
        playerName = reader.GetString();
        deathVelocity = new Vector3(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat());
        deathAngularVelocity = new Vector3(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat());
    }
}