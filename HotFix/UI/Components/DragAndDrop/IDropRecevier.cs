using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EOperateLocation
{
    // 玩家物品区域
    InventoryArea,
    // Equips
    EquipArea,
    // 背包格子
    BagSlot,
}

public interface IDropRecevier
{
    /// <returns>true = 已接收并处理成功（物品已从来源移走）；false = 未处理或失败</returns>
    public bool OnDropEnd(EOperateLocation from, ItemBase item);
}
