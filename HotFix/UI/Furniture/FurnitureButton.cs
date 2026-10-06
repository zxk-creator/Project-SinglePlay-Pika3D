using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureButton : UIBase
{
    private FurnitureBase currentItem;
    private FurnitureDragAndDrop dragObj;

    public FurnitureButton(FurnitureBase furniture) : base("PlaceFurnitureButton")
    {
        if (Util.CheckNull(furniture)) return;

        
        dragObj = UIPrefab.GetComponentInChildren<FurnitureDragAndDrop>(true);
        currentItem = furniture;
        dragObj.currentItem = currentItem;

        var ItemImage = GetTargetComponent<Image>("ItemImage");
        // 设置Item图标等
        ItemImage.sprite = Resources.Load<Sprite>(furniture.iconPath);
        ItemImage.color = Color.white;
    }

    public override void Hide()
    {
        UIPrefab.SetActive(false);
        UIPrefab.transform.SetParent(canvas.transform);
    }

    public override void Show()
    {
        UIPrefab.SetActive(true);
    }


    // 这里应该是Drag事件，不需要Button了。
}
