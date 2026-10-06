using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum EClothType
{
    // 适用于没有冲突
    NONE,
    HAIR,
    GLASSES,
    EARRING,
    UPPER,
    HAND,
    BOTTOM,
    FACE,
    STOCKING,
    SHOE,
}


// 具体的类用数据资产运行时动态生成
public class ClothBase : ItemBase
{
    public ClothBase()
    {
        itemType = EItemType.ClothBase;
    }

    // clothing_index.json 的 conflictPart 编号 -> EClothType（0-8 有效，其余为 NONE）
    public static EClothType ConvertConflictPart(int jsonValue)
    {
        switch (jsonValue)
        {
            case 0: return EClothType.HAIR;
            case 1: return EClothType.EARRING;
            case 2: return EClothType.GLASSES;
            case 3: return EClothType.FACE;
            case 4: return EClothType.UPPER;
            case 5: return EClothType.BOTTOM;
            case 6: return EClothType.STOCKING;
            case 7: return EClothType.SHOE;
            case 8: return EClothType.HAND;
            default: return EClothType.NONE;
        }
    }

    // conflictPart 编号列表 -> EClothType 列表（过滤 NONE 与重复；空/缺失返回 null）
    public static List<EClothType> ConvertConflictParts(List<int> jsonValues)
    {
        if (jsonValues == null || jsonValues.Count == 0) return null;
        var list = new List<EClothType>();
        foreach (int v in jsonValues)
        {
            EClothType e = ConvertConflictPart(v);
            if (e != EClothType.NONE && !list.Contains(e))
                list.Add(e);
        }
        return list.Count > 0 ? list : null;
    }

    // 异味度
    public float OdorLevel { get; private set; }
    public List<EClothType> conflictPart;
    public EClothType clothType;
    /// <summary>是否可染色：衣服为 false，头发为 true</summary>
    public bool canColored;
    public string prefabPath;
    public float addedCharm = 0;
    public void SetOdorLevel(float newValue)
    {
        OdorLevel = Mathf.Clamp(newValue, 1, 1000);
    }
    // 耐久度百分比
    public float Durability {get; private set;} = 10;
    public void SetDurability(float newValue)
    {
        Durability = newValue;
    }

    public override ItemBase GetACopy()
    {
        var copy = new ClothBase();
        copy.canStack = canStack;
        copy.currentStackCount = currentStackCount;
        copy.description = description;
        copy.iconPath = iconPath;
        copy.quality = quality;
        copy.itemName = itemName;
        copy.occupiedSpace = occupiedSpace;
        copy.value = value;
        copy.clothType = clothType;
        copy.canColored = canColored;
        copy.prefabPath = prefabPath;
        copy.addedCharm = addedCharm;
        copy.conflictPart = conflictPart != null ? new List<EClothType>(conflictPart) : null;
        copy.OdorLevel = OdorLevel;
        copy.Durability = Durability;
        copy.id = id;
        copy.itemType = itemType;

        return copy;
    }

    public override void Serialize(NetDataWriter bw)
    {
        base.Serialize(bw);
        bw.Put(OdorLevel);
        bw.Put(Durability);
    }

    public override void Deserialize(NetDataReader br)
    {
        base.Deserialize(br);
        OdorLevel = br.GetFloat();
        Durability = br.GetFloat();
    }
}
