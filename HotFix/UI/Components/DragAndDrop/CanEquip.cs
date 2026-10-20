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
                    // 如果是工具，则切换工具
                    if (item is ToolBase tool)
                    {
                        // 发送切换工具信号
                        var changeToolIntent = new ClientEquipToolIntent()
                        {
                            toolToEquip = tool
                        };
                        Context.net.Send(changeToolIntent);
                        return false;
                    }

                    var isCloth = item as ClothBase;
                    if (isCloth == null) return false;

                    // 如果是衣服，发送切换衣服的意图
                    var changeClothIntent = new ClientEquipClothIntent()
                    {
                        clothToEquip = isCloth
                    };
                    // 直接发送信号
                    Context.net.Send(changeClothIntent);
                    return false;
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
