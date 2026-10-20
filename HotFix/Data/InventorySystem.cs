using System;
using System.Collections.Generic;
using UnityEngine;
using PK;
using PKSv;

public class CharacterEquipCollection
{
    public ClothBase Hair {get; private set;}
    public ClothBase Upper { get; private set; }
    public ClothBase Bottom { get; private set; }
    public ClothBase Stock { get; private set; }
    public ClothBase Shoes { get; private set; }
    public ClothBase Hands { get; private set; }
    public ClothBase Earring { get; private set; }
    public ClothBase Face { get; private set; }
    public ClothBase Glasses { get; private set; }

    public void SetCloth(CharacterEquipCollection newEquips)
    {
        if (newEquips == null) return;
        Hair = newEquips.Hair;
        Upper = newEquips.Upper;
        Bottom = newEquips.Bottom;
        Stock = newEquips.Stock;
        Shoes = newEquips.Shoes;
        Hands = newEquips.Hands;
        Earring = newEquips.Earring;
        Face = newEquips.Face;
        Glasses = newEquips.Glasses;
    }

    /// <summary>
    /// 比较两套装备的所有槽位是否真的一模一样（外观、状态、染色等）
    /// </summary>
    public bool IsSameAs(CharacterEquipCollection other)
    {
        if (other == null) return false;

        bool IsSlotSame(ClothBase a, ClothBase b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.IsSameAs(b);
        }

        return IsSlotSame(Hair, other.Hair)
            && IsSlotSame(Glasses, other.Glasses)
            && IsSlotSame(Earring, other.Earring)
            && IsSlotSame(Upper, other.Upper)
            && IsSlotSame(Hands, other.Hands)
            && IsSlotSame(Bottom, other.Bottom)
            && IsSlotSame(Face, other.Face)
            && IsSlotSame(Stock, other.Stock)
            && IsSlotSame(Shoes, other.Shoes);
    }

    public void SetSlot(EClothType slot, ItemBase cloth)
    {
        switch (slot)
        {
            case EClothType.HAIR:     Hair = cloth as ClothBase; break;
            case EClothType.UPPER:    Upper = cloth as ClothBase; break;
            case EClothType.BOTTOM:   Bottom = cloth as ClothBase; break;
            case EClothType.STOCKING: Stock = cloth as ClothBase; break;
            case EClothType.SHOE:     Shoes = cloth as ClothBase; break;
            case EClothType.HAND:     Hands = cloth as ClothBase; break;
            case EClothType.EARRING:  Earring = cloth as ClothBase; break;
            case EClothType.FACE:     Face = cloth as ClothBase; break;
            case EClothType.GLASSES:  Glasses = cloth as ClothBase; break;
        }
    }

    public ItemBase GetSlot(EClothType slot)
    {
        switch (slot)
        {
            case EClothType.HAIR:     return Hair;
            case EClothType.UPPER:    return Upper;
            case EClothType.BOTTOM:   return Bottom;
            case EClothType.STOCKING: return Stock;
            case EClothType.SHOE:     return Shoes;
            case EClothType.HAND:     return Hands;
            case EClothType.EARRING:  return Earring;
            case EClothType.FACE:     return Face;
            case EClothType.GLASSES:  return Glasses;
            default:                  return null;
        }
    }
}

/// <summary>
/// 角色背包系统，读写绑定的SaveData
/// </summary>
public class InventorySystem
{
    /// <summary>绑定的存档数据</summary>
    public SaveData saveData { get; private set; }

    // 是否已经完成过首次渲染，首次渲染要先铺默认装
    private bool hasBound;

    /// <summary>绑定存档数据并刷新装备，工具与背包界面</summary>
    public void BindSaveData(SaveData data)
    {
        if (data == null) return;

        // 先比较是否都一样，都一样不用执行任何操作
        if (data.IsSameAs(saveData)) return;

        saveData = data;

        if (saveData.equipedCloth == null)
            saveData.equipedCloth = new CharacterEquipCollection();

        if (saveData.handSpace == null)
            saveData.handSpace = new BagpackBase();

        // 首次渲染先铺一遍默认装，后面只处理有变化的槽位
        if (!hasBound)
        {
            hasBound = true;
            equipFrontend.ApplyDefaultOutfit();
        }

        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            ItemBase cloth = saveData.equipedCloth.GetSlot(slot);

            if (cloth == null)
            {
                if (equipFrontend.HasWornCloth(slot))
                    equipFrontend.Remove(slot);
                continue;
            }

            equipFrontend.Wear((ClothBase)cloth);
        }

        equipFrontend.SetTool(saveData.currentEquipedTool as ToolBase);

        if (!character.isNPC && bagPanel != null)
            bagPanel.Init(false);
    }

    /// <summary>当前装备</summary>
    public CharacterEquipCollection equips
    {
        get
        {
            if (saveData.equipedCloth == null)
                saveData.equipedCloth = new CharacterEquipCollection();
            return saveData.equipedCloth;
        }
        private set => saveData.equipedCloth = value ?? new CharacterEquipCollection();
    }

    /// <summary>主背包，可能为 null</summary>
    public BagpackBase BagSpace
    {
        get => saveData.equipedBag;
        private set => saveData.equipedBag = value;
    }

    /// <summary>手部背包，恒非 null</summary>
    public BagpackBase HandSpace
    {
        get
        {
            if (saveData.handSpace == null)
                saveData.handSpace = new BagpackBase();
            return saveData.handSpace;
        }
        private set => saveData.handSpace = value ?? new BagpackBase();
    }

    /// <summary>当前手上的工具，可能为 null</summary>
    public ToolBase currentEquipedTool
    {
        get => saveData.currentEquipedTool as ToolBase;
        private set => saveData.currentEquipedTool = value;
    }

    /// <summary>背包界面</summary>
    public BagPanel bagPanel {get; private set;}

    /// <summary>装备渲染组件</summary>
    public Equip equipFrontend {get; private set;}
    private CharacterBase character;

    /// <summary>绑定存档数据并持有渲染组件</summary>
    public InventorySystem(Equip equipFrontend, CharacterBase character, SaveData saveData)
    {
        this.equipFrontend = equipFrontend;
        this.character = character;
        BindSaveData(saveData);
    }

    /// <summary>绑定背包界面</summary>
    public void SetBagPanel(BagPanel newPanel)
    {
        if (Util.CheckNull(newPanel)) return;

        bagPanel = newPanel;
    }
}
