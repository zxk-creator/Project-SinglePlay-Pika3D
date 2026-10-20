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
        copy.itemType = itemType;
        return copy;
    }

    public override bool IsSameAs(ItemBase item)
    {
        if (!base.IsSameAs(item)) return false;

        var other = item as ToolBase;
        if (other == null) return false;

        return toolType == other.toolType
            && currentDurability == other.currentDurability
            && level == other.level
            && maxDurability == other.maxDurability;
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

        // 下面就是去数据库查找的代码
        ToolBase template = Context.Item.GetItemCopyById(id) as ToolBase;
        if (template == null)
        {
            PK.Log.Error($"[ToolBase] 反序列化找不到模板 id={id}");
            return;
        }

        // 模板字段（从数据库补）
        iconPath = template.iconPath;
        description = template.description;
        value = template.value;
        itemName = template.itemName;
        occupiedSpace = template.occupiedSpace;
        quality = template.quality;
        canStack = template.canStack;
        itemType = template.itemType;
        prefabPath = template.prefabPath;
        maxDurability = template.maxDurability;
        toolType = template.toolType;
    }
}
