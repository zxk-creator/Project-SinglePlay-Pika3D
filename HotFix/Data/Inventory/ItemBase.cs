using System.IO;
using LiteNetLib.Utils;

public enum EItemLevel
{
    COMMOM,   // 白
    UNCOMMOM, // 绿
    RARE,     // 蓝
    EPIC,     // 紫
    GOLD,     // 金
    LEGENDARY // 红
}

public enum EItemType
{
    ItemBase,
    ToolBase,
    HairBase,
    FurnitureBase,
    FoodBase,
    ClothBase,
    BagpackBase,
}

/// <summary>
/// 所有物品的基类。占用空间计量标准：猜测他在2x2背包中占据的四方格大小，合起来就是容量。
/// </summary>
public class ItemBase : INetSerializable
{
    public int id;
    // 2D图片精灵路径
    public string iconPath;
    public string description;
    // 售卖价值
    public int value;
    public string itemName;
    // 《单个》物品占据的背包容量
    public int occupiedSpace = 1;
    // 当前这个物品的堆叠数量
    public int currentStackCount = 1;
    public EItemLevel quality = EItemLevel.COMMOM;
    // 是否可堆叠
    public bool canStack = true;

    public EItemType itemType = EItemType.ItemBase;

    public ItemBase() {}

    public virtual void Deserialize(NetDataReader br)
    {
        id = br.GetInt();
        currentStackCount = br.GetInt();
    }

    public virtual ItemBase GetACopy()
    {
        var copy = new ItemBase
        {
            canStack = canStack,
            currentStackCount = currentStackCount,
            description = description,
            iconPath = iconPath,
            quality = quality,
            itemName = itemName,
            occupiedSpace = occupiedSpace,
            value = value,
            id = id
        };

        return copy;
    }

    public virtual void Serialize(NetDataWriter bw)
    {
        bw.Put(id);
        bw.Put(currentStackCount);
    }

    public static ItemBase NewItem(EItemType type)
    {
        ItemBase item = type switch
        {
            EItemType.ItemBase => new ItemBase(),
            EItemType.ToolBase => new ToolBase(),
            EItemType.HairBase => new HairBase(),
            EItemType.FurnitureBase => new FurnitureBase(),
            EItemType.FoodBase => new FoodBase(),
            EItemType.ClothBase => new ClothBase(),
            EItemType.BagpackBase => new BagpackBase(),
            _ => throw new IOException("未知物品类型：" + type),
        };
        item.itemType = type;
        return item;
    }
}
