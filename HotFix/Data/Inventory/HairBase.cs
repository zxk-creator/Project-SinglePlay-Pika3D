using LiteNetLib.Utils;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 具体的类用数据资产运行时动态生成
public class HairBase : ClothBase
{
    public HairBase()
    {
        itemType = EItemType.HairBase;
        canStack = false;
    }

    // 头发的染色
    public Color HairColor;
    public GameObject MeshPrefab;
    public EClothType targetSlot = EClothType.HAIR;

    public override ItemBase GetACopy()
    {
        var copy = new HairBase();
        copy.canStack = canStack;
        copy.currentStackCount = currentStackCount;
        copy.description = description;
        copy.iconPath = iconPath;
        copy.quality = quality;
        copy.itemName = itemName;
        copy.occupiedSpace = occupiedSpace;
        copy.value = value;
        copy.HairColor = HairColor;
        copy.MeshPrefab = MeshPrefab;
        copy.addedCharm = addedCharm;
        copy.targetSlot = targetSlot;
        copy.id = id;
        copy.itemType = itemType;
        copy.clothType = clothType;
        copy.canColored = canColored;
        copy.prefabPath = prefabPath;
        copy.conflictPart = conflictPart != null ? new List<EClothType>(conflictPart) : null;
        copy.SetOdorLevel(OdorLevel);
        copy.SetDurability(Durability);

        return copy;
    }

    public override void Serialize(NetDataWriter bw)
    {
        base.Serialize(bw);
        bw.Put(HairColor.r);
        bw.Put(HairColor.g);
        bw.Put(HairColor.b);
        bw.Put(HairColor.a);
    }

    public override void Deserialize(NetDataReader br)
    {
        base.Deserialize(br);
        HairColor = new Color(br.GetFloat(), br.GetFloat(), br.GetFloat(), br.GetFloat());
    }
}
