using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NPCUtil
{
    public static void TrySummonNPC(Vector3 position, Quaternion rotation)
    {
        
        bool isGirl = Util.GenBoolByPercent(0.8f);
        var res = ResourceHelp.LoadPrefab("NPC");
        res.GetComponent<CharacterBase>().isGirl = isGirl;
        res.GetComponent<NPC>().SetName(Context.fanNames.RandomElement());

        res.transform.SetPositionAndRotation(position, rotation);
        var cloth = GenerateRandomCloth(isGirl);

        // 延迟到下一帧进行，防止没有初始化
        Context.updateProxy.RegisterDelayTask(()=>{ res.GetComponent<NPC>().ApplyCloth(cloth); }, 0.02f);
    }

    /// <summary>
    /// 生成一套衣服
    /// </summary>
    /// <param name="isGirl">true=女性，false=男性</param>
    /// <returns>一套衣服（CharacterEquipCollection）：头发/上衣/下衣/鞋子/脸部/手/袜子，没有的部位为 null</returns>
    public static CharacterEquipCollection GenerateRandomCloth(bool isGirl)
    {
        GenderEquipCollection collection = isGirl ? Context.Item.clothes.female : Context.Item.clothes.male;

        var result = new CharacterEquipCollection();

        // 处理顺序：上衣先确立"系列"，后续部位尽量匹配同系列；
        // 找不到同系列就随机选一件，并以其系列继续匹配剩余部位
        // （如 Girl0018_Upper → 找 Girl0018_Bottom/Shoe/Hair；某部位没有 Girl0018 就随机到
        //   Girl0056_Bottom，后续再找 Girl0056_Shoe/Hair... 直到所有部位处理完）
        string currentSeries = null;
        ProcessSlot(collection, EClothType.UPPER,    result, ref currentSeries); // 上衣
        ProcessSlot(collection, EClothType.BOTTOM,   result, ref currentSeries); // 下衣
        ProcessSlot(collection, EClothType.SHOE,     result, ref currentSeries); // 鞋子
        ProcessSlot(collection, EClothType.HAIR,     result, ref currentSeries); // 头发
        ProcessSlot(collection, EClothType.FACE,     result, ref currentSeries); // 脸部
        ProcessSlot(collection, EClothType.HAND,     result, ref currentSeries); // 手
        ProcessSlot(collection, EClothType.STOCKING, result, ref currentSeries); // 袜子

        return result;
    }

    /// <summary>为单个部位挑选衣服并写入集合；该部位没有可用衣服时保持 null</summary>
    private static void ProcessSlot(GenderEquipCollection collection, EClothType slot,
                                    CharacterEquipCollection result, ref string currentSeries)
    {
        List<ClothBase> list = GetSlotList(collection, slot);
        if (list == null || list.Count == 0) return;

        // 过滤掉无法穿戴（prefab 为空）的条目
        List<ClothBase> usable = list.FindAll(d => !string.IsNullOrEmpty(d.prefabPath));
        if (usable.Count == 0) return;

        // 优先找当前系列的；找不到就随机（随机结果成为新系列）
        List<ClothBase> candidates = usable;
        if (!string.IsNullOrEmpty(currentSeries))
        {
            string series = currentSeries; // lambda 不能捕获 ref 参数，先拷贝
            List<ClothBase> sameSeries = usable.FindAll(d => string.Equals(GetSeries(d), series, StringComparison.OrdinalIgnoreCase));
            if (sameSeries.Count > 0) candidates = sameSeries;
        }

        ClothBase pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        result.SetSlot(slot, pick.GetACopy());
        currentSeries = GetSeries(pick);
    }

    /// <summary>按部位取对应性别的服装列表</summary>
    private static List<ClothBase> GetSlotList(GenderEquipCollection collection, EClothType slot)
    {
        switch (slot)
        {
            case EClothType.HAIR:     return collection.hair;
            case EClothType.UPPER:    return collection.upper;
            case EClothType.BOTTOM:   return collection.bottom;
            case EClothType.SHOE:     return collection.shoe;
            case EClothType.FACE:     return collection.face;
            case EClothType.HAND:     return collection.hand;
            case EClothType.STOCKING: return collection.stocking;
            default:                  return null;
        }
    }

    /// <summary>
    /// 系列 = prefab 文件名第一个下划线之前的部分。
    /// 命名规则参考 clothing_index.json：Girl0018_Upper / Girl0018_Shoes / Girl0000_Stockings_Long
    /// → 系列均为 Girl0018 / Girl0000。
    /// </summary>
    private static string GetSeries(ClothBase data)
    {
        string prefab = data.prefabPath;
        int slash = prefab.LastIndexOf('/');
        string fileName = slash >= 0 ? prefab.Substring(slash + 1) : prefab;
        int underscore = fileName.IndexOf('_');
        return underscore > 0 ? fileName.Substring(0, underscore) : fileName;
    }
}
