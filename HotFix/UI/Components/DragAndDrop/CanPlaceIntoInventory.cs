using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;

// 挂上就可以卸载已穿戴装备
public class CanPlaceIntoInventory : MonoBehaviour, IDropRecevier
{
    public bool OnDropEnd(EOperateLocation from, ItemBase item)
    {
        switch (from)
        {
            case EOperateLocation.InventoryArea: return false;
            // 卸下装备
            case EOperateLocation.EquipArea:
                {
                    if (item is ToolBase tool)
                    {
                        var inv = Context.localPlayer.inventory;
                        // 先尝试放回背包，放不下保持装备状态不丢
                        ItemBase remain = inv.TryPutIntoBag(tool);
                        if (remain != null)
                        {
                            Log.Warn("背包空间不足，无法卸下工具：" + tool.itemName);
                            return false;
                        }
                        inv.RemoveTool();
                        inv.bagPanel.RefreshEquip();
                        return true;
                    }

                    var ifIsCloth = item as ClothBase;
                    if (ifIsCloth == null) return false;
                    // 卸下成功才刷新装备栏，失败（背包满）保持原状
                    bool ok = Context.localPlayer.inventory.RemoveClothByReference(ifIsCloth);
                    if (ok)
                        Context.localPlayer.inventory.bagPanel.RefreshEquip();
                    return ok;
                }
                // 卸下背包
                case EOperateLocation.BagSlot:
                {
                    var bag = item as BagpackBase;
                    if (bag == null)
                    {
                        Log.Error("你似乎把其他物品当作背包背上去了！这不该发生！");
                        return false;
                    }

                    return Context.localPlayer.inventory.SetBag(bag);
                }
        }

        return false;
    }

}
