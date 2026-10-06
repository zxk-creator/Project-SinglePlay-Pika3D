using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;

public class CanReplaceBag : MonoBehaviour, IDropRecevier
{
    public bool OnDropEnd(EOperateLocation from, ItemBase item)
    {
        switch (from)
        {
            case EOperateLocation.InventoryArea:
                {
                    var ifIsBag = item as BagpackBase;
                    if (ifIsBag == null) return false;
                    Context.localPlayer.inventory.SetBag(ifIsBag);
                    break;
                }
                // 背包不可能来自装备区域。
                case EOperateLocation.EquipArea:
                {
                    Log.Error("尝试将背包从装备区移动过去！这不该发生！");
                    return false;
                }
                // 自己换自己，不执行任何操作
                case EOperateLocation.BagSlot:
                {
                    return false;
                }
        }

        Log.Error("您可能传入了错误的枚举类型导致穿透");
        return false;
    }

}
