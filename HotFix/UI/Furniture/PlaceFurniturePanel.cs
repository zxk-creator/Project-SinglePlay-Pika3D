using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceFurniturePanel : UIBase
{
    private static List<FurnitureBase> allFurniture = new List<FurnitureBase>();
    private static List<FurnitureButton> allFurnitureButtons = new List<FurnitureButton>(); 
    GameCancelButton returnButton;

    public PlaceFurniturePanel() : base("PlaceFurniturePanel")
    {
        var parent = FindChildRecursive(UIPrefab.transform, "Content");
        
        // 直接从缓存加载
        foreach (var e in allFurnitureButtons)
        {
            e.Show();
            e.SetParent(parent);
        }

        returnButton = GetTargetComponent<GameCancelButton>("goMainButton");
        returnButton.onClick.AddListener(() => { Hide(); });
    }

    public override void Destroy()
    {
        foreach (var e in allFurnitureButtons)
        {
            e.Hide();
        }
        base.Destroy();
    }

    public static void GenerateAllFurnitureButtons()
    {
        // 加载上下文中记录的所有家具的引用（目前仅有toilet）
        foreach (var e in Context.Item.allItems["toilet"])
        {
            allFurniture.Add(e as FurnitureBase);
        }
        foreach (var e in Context.Item.allItems["bed"])
        {
            allFurniture.Add(e as FurnitureBase);
        }
        foreach (var e in Context.Item.allItems["shower"])
        {
            allFurniture.Add(e as FurnitureBase);
        }
        foreach (var e in Context.Item.allItems["tub"])
        {
            allFurniture.Add(e as FurnitureBase);
        }

        // 每一个都实例化一个按钮出来
        foreach (var f in allFurniture)
        {
            var furniture = new FurnitureButton(f);
            allFurnitureButtons.Add(furniture);
        }
    }
}
