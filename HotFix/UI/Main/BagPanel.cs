using UnityEngine;
using UnityEngine.UI;
using PK;

// 背包界面
public class BagPanel : UIBase,IInputAcceptable
{
    private GameCancelButton exitButton;
    private Transform HandSpace;
    private Transform BagSpace;
    private Transform bagEquipTransform;
    private InventoryItem hairItem;
    private InventoryItem faceItem;
    private InventoryItem earringItem;
    private InventoryItem glassesItem;
    private InventoryItem upperItem;
    private InventoryItem bottomItem;
    private InventoryItem handsItem;
    private InventoryItem stockItem;
    private InventoryItem shoesItem;
    private InventoryItem toolItem;
    private InventorySystem playerInventory;

    public BagPanel(InventorySystem playerInventory) : base(R.Path.BagPanelPath)
    {
        this.playerInventory = playerInventory;
        Init(true);
    }

    public void Init(bool bisFirstBuild)
    {
        if (UIPrefab == null)
        {
            Log.Error("背包面板 prefab 未创建，Init 中止");
            return;
        }

        if (!bisFirstBuild)
        {
            ClearChildren(HandSpace);
            ClearChildren(BagSpace);
            ClearChildren(bagEquipTransform);

            if (UIPrefab != null)
                ClearChildren(FindChildRecursive(UIPrefab.transform, "ClothLayoutGroup"));
        }

        BuildContent();
    }

    private void BuildContent()
    {
        // 回填面板引用
        playerInventory.SetBagPanel(this);

        exitButton = GetTargetComponent<GameCancelButton>("ReturnButton");
        exitButton.onClick.RemoveListener(ExitBagPanel);
        exitButton.onClick.AddListener(ExitBagPanel);
        HandSpace = FindChildRecursive(UIPrefab.transform, "GridLayoutGroupHand");
        if (HandSpace == null) Log.Error("未找到GridLayoutGroupHand!");
        BagSpace = FindChildRecursive(UIPrefab.transform, "GridLayoutGroupBag");
        if (BagSpace == null) Log.Error("未找到GridLayoutGroupBag!");
        bagEquipTransform = FindChildRecursive(UIPrefab.transform, "LayoutGroupBagInfo");
        if (bagEquipTransform == null) Log.Error("未找到LayoutGroupBagInfo用于显示背包详情！");

        // 先实例化一个用来显示装备的背包Slot出来
        var bagEquipSlot = new BagSlot(playerInventory.BagSpace,this, EOperateLocation.InventoryArea);
        bagEquipSlot.UIPrefab.transform.SetParent(bagEquipTransform);

        // 根据背包内物品实例化手部物品UI
        foreach (var item in playerInventory.HandSpace.items)
        {
            var itemBox = new InventoryItem(item, this, EOperateLocation.InventoryArea);
            itemBox.UIPrefab.transform.SetParent(HandSpace);
        }
        // 设置父级的Preferred Size
        var handcom = GetTargetComponent<LayoutElement>("HandSpace");
        if (playerInventory.HandSpace.items.Count >= 1)
        {
            handcom.preferredHeight = 100;
        }
        else
        {
            handcom.preferredHeight = 0;
        }
        

        // 实例化背包UI。
        // BagSpace 为 null 是正常状态（当前没穿戴任何背包），此时没有格子要填，
        // 高度按 0 处理，与上面 HandSpace 的处理方式保持一致。
        if (playerInventory.BagSpace != null)
        {
            foreach (var item in playerInventory.BagSpace.items)
            {
                var itemBox = new InventoryItem(item, this, EOperateLocation.InventoryArea);
                itemBox.UIPrefab.transform.SetParent(BagSpace);
            }

            GetTargetComponent<LayoutElement>("BagpackSpace").preferredHeight = playerInventory.BagSpace.items.Count * 100;
        }
        else
        {
            GetTargetComponent<LayoutElement>("BagpackSpace").preferredHeight = 0;
        }

        var equips = playerInventory.equips;
        // 实例化LeftArea的装备UI
        var parent = FindChildRecursive(UIPrefab.transform, "ClothLayoutGroup");

        (hairItem = new InventoryItem(equips.Hair, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (faceItem = new InventoryItem(equips.Face, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (earringItem = new InventoryItem(equips.Earring, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (glassesItem = new InventoryItem(equips.Glasses, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (upperItem = new InventoryItem(equips.Upper, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (bottomItem = new InventoryItem(equips.Bottom, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (handsItem = new InventoryItem(equips.Hands, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (stockItem = new InventoryItem(equips.Stock, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (shoesItem = new InventoryItem(equips.Shoes, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
        (toolItem = new InventoryItem(playerInventory.currentEquipedTool, this, EOperateLocation.EquipArea)).UIPrefab.transform.SetParent(parent, false);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.SetParent(null, false);
            Object.Destroy(child.gameObject);
        }
    }

    public void RefreshEquip()
    {
        if (Util.CheckNull(playerInventory)) return;

        hairItem.SetItem(playerInventory.equips.Hair);
        faceItem.SetItem(playerInventory.equips.Face);
        earringItem.SetItem(playerInventory.equips.Earring);
        glassesItem.SetItem(playerInventory.equips.Glasses);
        upperItem.SetItem(playerInventory.equips.Upper);
        bottomItem.SetItem(playerInventory.equips.Bottom);
        stockItem.SetItem(playerInventory.equips.Stock);
        shoesItem.SetItem(playerInventory.equips.Shoes);
        handsItem.SetItem(playerInventory.equips.Hands);
        toolItem.SetItem(playerInventory.currentEquipedTool);

        // 同时刷新整个界面
        Init(false);
    }

    public void OnOpenBagPressed()
    {
        Hide();
    }

    private void ExitBagPanel()
    {
        Context.um.Pop(this);
        Destroy();
    }
}
