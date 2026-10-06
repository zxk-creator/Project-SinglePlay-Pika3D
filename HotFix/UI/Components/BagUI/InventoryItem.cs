using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PK;

public class InventoryItem : UIBase
{
    protected ItemBase ownedItem;
    protected TMP_Text ItemNameText;
    protected TMP_Text CountText;
    protected Image ItemImage;
    protected InventoryDragAndDrop dragobj;

    public InventoryItem(ItemBase ownedItem, BagPanel bagPanelRef,EOperateLocation owner,string UIPrefabPath = null) : base(string.IsNullOrEmpty(UIPrefabPath) ? R.Path.InventoryItem : UIPrefabPath)
    {
        UIPrefab.SetActive(true);

        dragobj = UIPrefab.GetComponentInChildren<InventoryDragAndDrop>(true);
        dragobj.draggingItem = ownedItem;
        dragobj.bagPanelRef = bagPanelRef;
        dragobj.owner = owner;
        dragobj.inventoryItemRef = this;

        ItemNameText = GetTargetComponent<TMP_Text>("ItemNameText");
        CountText = GetTargetComponent<TMP_Text>("CountText");
        ItemImage = GetTargetComponent<Image>("ItemImage");

        if (ownedItem == null)
        {
            Log.Info("此格子内没有放任何物品！已返回");
            return;
        }

        ItemNameText.SetText(ownedItem.itemName);
        ItemNameText.color = Util.ItemLevelColor.GetColor(ownedItem.quality);
        if (ownedItem.currentStackCount > 1)
        {
            CountText.SetText(ownedItem.currentStackCount.ToString());
        }
        ItemImage.sprite = Resources.Load<Sprite>(ownedItem.iconPath);
        ItemImage.color = Color.white;

        // 传给DragAndDrop让他知道操作的谁
        dragobj.draggingItem = ownedItem;

        this.ownedItem = ownedItem;

        // 遍历层级设置不可遮挡射线
        
    }

    public void SetItem(ItemBase newItem)
    {
        if (newItem == null)
        return;

        ItemNameText.SetText(newItem.itemName);
        ItemNameText.color = Util.ItemLevelColor.GetColor(newItem.quality);
        if (newItem.currentStackCount > 1)
            CountText.SetText(newItem.currentStackCount.ToString());
        else
            CountText.SetText("");
        ownedItem = newItem;
        ItemImage.sprite = Resources.Load<Sprite>(newItem.iconPath);
        ItemImage.color = Color.white;
        dragobj.draggingItem = ownedItem;
    }

    public override void Show()
    {
        Log.Warn("不应该调用这个！");
    }

    public override void Hide()
    {
        Log.Warn("不应该调用这个！");
    }
}
