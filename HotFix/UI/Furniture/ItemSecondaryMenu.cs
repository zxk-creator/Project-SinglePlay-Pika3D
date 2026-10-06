using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSecondaryMenu : UIBase
{
    // 默认就有
    private GameAcceptButton showDetailBtn;
    // 装备，对于装备才有
    private GameAcceptButton equipBtn;
    // 打开商店才有
    private GameAcceptButton saleBtn;
    // 默认就有
    private GameAcceptButton dropBtn;
    // 售卖，打开商店才有
    private GameAcceptButton sellBtn;
    public ItemSecondaryMenu() : base("BagSecondaryMenu")
    {
        
    }
}
