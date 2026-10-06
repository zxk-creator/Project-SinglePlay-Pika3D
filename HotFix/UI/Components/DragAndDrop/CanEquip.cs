using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 挂上这个，UI就有了拖拽到此装备到玩家身上的效果
public class CanEquip : MonoBehaviour, IDropRecevier
{
    public bool OnDropEnd(EOperateLocation from,ItemBase item)
    {
        switch (from)
        {
            // 来自装备区域。直接装备或切换
            case EOperateLocation.InventoryArea:
                {
                    if (item is ToolBase tool)
                    {
                        if (!Context.localPlayer.inventory.SetTool(tool)) return false;
                        Context.localPlayer.inventory.RemoveFromBag(tool);
                        Context.localPlayer.inventory.bagPanel.RefreshEquip();
                        return true;
                    }

                    var isCloth = item as ClothBase;
                    if (isCloth == null) return false;
                    // 换装失败（如背包满、旧装备放不回）→ 中止，物品保留不消失
                    if (!Context.localPlayer.inventory.ChangeCloth(isCloth)) return false;
                    Context.localPlayer.inventory.RemoveFromBag(isCloth);
                    Context.localPlayer.inventory.bagPanel.RefreshEquip();

                    return true;
                }
                // 自己挂自己，没反应处理
            case EOperateLocation.EquipArea:
                {
                    return false;
                }
                // 来自背包，没反应
                case EOperateLocation.BagSlot:
                {
                    return false;
                }
        }

        return false;
    }
}
