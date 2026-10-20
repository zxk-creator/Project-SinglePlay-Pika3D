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
                    if (item is ToolBase)
                    {
                        // 传 null 表示卸下手上的工具
                        Context.net.Send(new ClientEquipToolIntent());
                        return false;
                    }

                    var ifIsCloth = item as ClothBase;
                    if (ifIsCloth == null) return false;

                    var takeOffClothIntent = new ClientTakeOffClothIntent()
                    {
                        targetSlot = ifIsCloth.clothType
                    };
                    Context.net.Send(takeOffClothIntent);
                    return false;
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

                    Context.net.Send(new ClientTakeOffBagIntent());
                    return false;
                }
        }

        return false;
    }

}
