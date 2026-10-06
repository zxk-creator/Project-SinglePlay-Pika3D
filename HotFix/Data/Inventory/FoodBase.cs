using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FoodBase : ItemBase
{
    public FoodBase()
    {
        itemType = EItemType.FoodBase;
    }
    
    // 直接加到CharacterProperty的食物中
    public float addedFoodValue;
    // 耐久度（50%以下开始增加毒性和排泄量，逐渐增加，不是固定的一个值）
    public float durability = 100.0f;
    // 每小时（游戏内）衰减的耐久度
    public float attenuate = 1.0f;
    // 毒性（正数，直接减去CharacterProperty中）
    public float position = 0f;

    public override void Serialize(NetDataWriter bw)
    {
        base.Serialize(bw);
        bw.Put(durability);
        bw.Put(position);
    }

    public override void Deserialize(NetDataReader br)
    {
        base.Deserialize(br);
        durability = br.GetFloat();
        position = br.GetFloat();
    }
}
