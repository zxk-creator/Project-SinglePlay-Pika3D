using LiteNetLib.Utils;
using System.IO;

public enum EToolType
{
    Pickaxe,
    Axe,
    Sword,
    FishingRod
}

public class ToolBase : ItemBase
{
    // 工具模型在 Resources 下的路径（不含扩展名），如 hotfix/equips/avatarextensionpart/Sword_0013
    public string prefabPath;
    // 耐久度（也就是能用多少下）
    public float maxDurability = 400.0f;
    public float currentDurability;
    // 等级，后续计算采集效率等都会用它计算
    public float level = 1.0f;
    public EToolType toolType;

    public ToolBase()
    {
        itemType = EItemType.ToolBase;
        currentDurability = maxDurability;
        canStack = false;
    }

    public override ItemBase GetACopy()
    {
        var copy = new ToolBase();
        copy.canStack = canStack;
        copy.currentStackCount = currentStackCount;
        copy.description = description;
        copy.iconPath = iconPath;
        copy.quality = quality;
        copy.itemName = itemName;
        copy.occupiedSpace = occupiedSpace;
        copy.value = value;
        copy.prefabPath = prefabPath;
        copy.maxDurability = maxDurability;
        copy.currentDurability = currentDurability;
        copy.level = level;
        copy.toolType = toolType;
        copy.id = id;
        return copy;
    }

    public override void Serialize(NetDataWriter bw)
    {
        base.Serialize(bw);
        bw.Put((int)toolType);
        bw.Put(currentDurability);
        bw.Put(level);
    }

    public override void Deserialize(NetDataReader br)
    {
        base.Deserialize(br);
        toolType = (EToolType)br.GetInt();
        currentDurability = br.GetFloat();
        level = br.GetFloat();
    }
}
