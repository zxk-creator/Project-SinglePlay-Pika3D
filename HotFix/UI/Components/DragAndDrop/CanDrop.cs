using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;

// 挂上这个组件，InventoryItem就可以拖拽到这里从背包中消失并丢弃到地上。
public class CanDrop : MonoBehaviour, IDropRecevier
{
    public bool OnDropEnd(EOperateLocation from, ItemBase item)
    {
        if (Util.CheckNull(item)) return false;

        InventorySystem inv = Context.localPlayer?.inventory;
        if (inv == null) return false;

        switch (from)
        {
            case EOperateLocation.InventoryArea:
                {
                    if (!inv.RemoveFromBag(item)) return false;
                    DropToGround(item);
                    inv.bagPanel?.RefreshEquip();
                    Log.Info("已丢弃物品到地面：" + item.itemName);
                    return true;
                }
            case EOperateLocation.EquipArea:
                {
                    if (item is ToolBase tool)
                    {
                        if (!ReferenceEquals(inv.currentEquipedTool, tool)) return false;
                        inv.RemoveTool();
                        DropToGround(item);
                        inv.bagPanel?.RefreshEquip();
                        Log.Info("已卸下并丢弃到地面：" + item.itemName);
                        return true;
                    }

                    EClothType slot = FindEquipSlot(inv, item);
                    if (slot == EClothType.NONE) return false;
                    inv.RemoveCloth(slot);
                    DropToGround(item);
                    inv.bagPanel?.RefreshEquip();
                    Log.Info("已脱下装备并丢弃到地面：" + item.itemName);
                    return true;
                }
            case EOperateLocation.BagSlot:
                {
                    Log.Warn("不能丢弃背着的背包：" + item.itemName);
                    return false;
                }
        }

        return false;
    }

    private EClothType FindEquipSlot(InventorySystem inv, ItemBase item)
    {
        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            if (ReferenceEquals(inv.equips.GetSlot(slot), item)) return slot;
        }
        return EClothType.NONE;
    }

    private void DropToGround(ItemBase item)
    {
        LocalPlayer player = Context.localPlayer;
        Vector3 position = player.transform.position + player.transform.forward * 2f + Vector3.up * 0.2f;
        InventorySystem.TryGenerateDropItem(item, position);
    }
}
