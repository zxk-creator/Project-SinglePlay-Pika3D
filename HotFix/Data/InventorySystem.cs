using System;
using System.Collections.Generic;
using UnityEngine;
using PK;

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

// 角色的背包系统后端。不仅适用于玩家角色，还适用于NPC
public class InventorySystem
{
    // 装备插槽
    public CharacterEquipCollection equips {get; private set;} = new CharacterEquipCollection();
    
    // 背包，自身就代表了装备的那个背包，因此可以为null
    public BagpackBase BagSpace {get; private set;}
    // 默认背包容量，直接使用默认值。
    public BagpackBase HandSpace {get; private set;} = new BagpackBase();
    public ToolBase currentEquipedTool {get; private set;}
    public BagPanel bagPanel {get; private set;}
    public Equip equipFrontend {get; private set;}
    private CharacterBase character;

    public InventorySystem(Equip equipFrontend, CharacterBase character)
    {
        this.equipFrontend = equipFrontend;
        this.character = character;
    }

    public void SetCharacterEquips(CharacterEquipCollection newEquips)
    {
        if (newEquips == null) newEquips = new CharacterEquipCollection();
        equips = newEquips;
        ApplyEquipsToRender();
    }

    /// <summary>
    /// 换装接口。可选：是否强制替换角色服装
    /// </summary>
    /// <param name="cloth">新衣服</param>
    /// <param name="bForceReplace">直接替换，原始服装直接消失</param>
    /// <returns>是否换装成功</returns>
    public bool ChangeCloth(ClothBase cloth, bool bForceReplace = false)
    {
        if (equipFrontend == null)
        {
            Log.Error("Equip 组件未初始化");
            return false;
        }

        if (cloth == null)
        {
            Log.NullPtr("ChangeCloth");
            return false;
        }

        if (cloth.clothType == EClothType.NONE)
        {
            Log.Error($"ChangeCloth: 衣服 {cloth.itemName} 的部位类型不合法 {cloth.clothType}");
            return false;
        }

        EClothType slot = cloth.clothType;
        ItemBase old = equips.GetSlot(slot);
        Log.Info($"[Inv] ChangeCloth slot={slot} clothId={cloth.id} oldIsNull={(old == null)} oldId={(old != null ? old.id : -1)} force={bForceReplace}");

        if (old != null)
        {
            equips.SetSlot(slot, null);
            if (!bForceReplace)
            {
                ItemBase remain = TryPutIntoBag(old);
                if (remain != null)
                {
                    equips.SetSlot(slot, old);
                    Log.Info("背包空间不足，无法更换装备：" + cloth.itemName);
                    return false;
                }
            }
            // 先恢复默认着装
            equipFrontend.Remove(slot);
        }

        // 再穿上新装备
        equips.SetSlot(slot, cloth);
        equipFrontend.Wear(cloth);
        Log.Info($"[Inv] ChangeCloth done slot={slot} equipsUpperId={(equips.Upper != null ? equips.Upper.id : -1)}");
        return true;
    }

    public void RemoveCloth(EClothType slot)
    {
        equips.SetSlot(slot, null);
        equipFrontend.Remove(slot);
    }

    /// <summary>
    /// 根据衣物引用卸下装备，若该衣物当前穿戴在某个插槽中，则将其移除并尝试放入背包。
    /// 若背包空间不足，则操作作废，保持原穿戴状态并提示。
    /// </summary>
    /// <param name="cloth">要卸下的衣物引用</param>
    /// <returns>卸下成功返回 true；未穿戴该衣物或背包空间不足返回 false</returns>
    public bool RemoveClothByReference(ClothBase cloth)
    {
        if (cloth == null)
        {
            Log.NullPtr("RemoveClothByReference");
            return false;
        }

        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            ItemBase current = equips.GetSlot(slot);
            if (current == cloth)
            {
                equips.SetSlot(slot, null);

                ItemBase remain = TryPutIntoBag(cloth);
                if (remain == null)
                {
                    equipFrontend?.Remove(slot);
                    return true;
                }
                else
                {
                    equips.SetSlot(slot, cloth);
                    Log.Info("无空间可放，无法卸下装备：" + cloth.itemName);
                    return false;
                }
            }
        }

        return false;
    }

    public void ApplyEquipsToRender()
    {
        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            ItemBase cloth = equips.GetSlot(slot);
            if (cloth == null)
            {
                equipFrontend.Remove(slot);
                continue;
            }

            equipFrontend.Wear((ClothBase)cloth);
        }
    }

    /// <summary>
    /// 尝试将物品放入背包，先尝试放入手持背包（HandSpace），若放不下再尝试放入主背包（BagSpace）。
    /// 放得下返回 null，完全放不下或部分剩余则返回剩余部分的对象。
    /// </summary>
    /// <param name="newItem">要放入的物品</param>
    /// <returns>剩余部分的对象，全部放入则返回 null</returns>
    public ItemBase TryPutIntoBag(ItemBase newItem)
    {
        if (newItem == null)
        {
            Log.NullPtr("TryPutIntoBag");
            return newItem;
        }

        ItemBase remain = HandSpace.TryPutIntoBag(newItem);
        if (remain == null) return null;

        if (BagSpace != null)
        {
            remain = BagSpace.TryPutIntoBag(remain);
            if (remain == null) return null;
        }

        return remain;
    }

    /// <summary>
    /// 设置背包。自带容量检查，放置回去
    /// </summary>
    /// <param name="newBag"></param>
    public bool SetBag(BagpackBase newBag)
    {
        if (newBag == null)
        {
            Log.NullPtr("SetBag");
            return false;
        }

        if (BagSpace == null)
        {
            BagSpace = newBag;
            if (!character.isNPC) {
                bagPanel?.Init(false);
            }
            return true;
        }
        
        if (newBag == BagSpace)
        {
            Log.Warn("不能把当前背着的背包再次背上");
            return false;
        }

        if (BagSpace.ContainsBag(newBag))
        {
            Log.Warn("替换背包不能来自当前背包内部");
            return false;
        }

        bool fromHand = HandSpace.RemoveItem(newBag);

        ItemBase remain = newBag.TryPutIntoBag(BagSpace);
        if (remain != null)
        {
            if (fromHand) HandSpace.items.Add(newBag);
            Log.Warn("新背包空间不足，无法放入当前背包：" + BagSpace.itemName);
            return false;
        }

        BagSpace = newBag;
        if (!character.isNPC){
            bagPanel?.Init(false);
        }
        return true;
    }

    public void SetHand(BagpackBase newBag)
    {
        if (newBag == null)
        {
            Log.NullPtr("SetHand");
            return;
        }

        HandSpace = newBag;
        if (!character.isNPC) {
            bagPanel?.Init(false);
        }
    }

    /// <summary>
    /// 装备工具（斧头/镐子/剑/钓鱼竿等），挂到角色手上并记录到currentEquipedTool。
    /// 可以为null，表示去掉装备
    /// </summary>
    public bool SetTool(ToolBase tool)
    {
        if (tool == null) return false;
        currentEquipedTool = tool;
        equipFrontend.SetTool(tool);
        if (!character.isNPC){
            bagPanel?.Init(false);
        }
        return true;
    }

    /// <summary>卸下当前工具</summary>
    public void RemoveTool()
    {
        currentEquipedTool = null;
        equipFrontend.RemoveTool();
    }

    public bool RemoveFromBag(ItemBase item)
    {
        if (HandSpace.RemoveItem(item)) return true;
        if (BagSpace != null && BagSpace.RemoveItem(item)) return true;
        return false;
    }

    public void SetBagPanel(BagPanel newPanel)
    {
        if (Util.CheckNull(newPanel)) return;

        bagPanel = newPanel;
    }

    // 便捷方法
    public static void TryGenerateDropItem(ItemBase item,Vector3 position)
    {
        if (Util.CheckNull(item)) return;
        var drop = ResourceHelp.LoadPrefab("DropItem");
        if (drop == null)
        {
            Log.Error("加载DropItem预制体失败");
            return;
        }
        var component = drop.GetComponent<PickupableItems>();
        if (component == null)
        {
            Log.Error("DropItem预制体缺少PickupableItems组件");
            UnityEngine.Object.Destroy(drop);
            return;
        }
        component.SetPickupItem(item);
        drop.transform.position = position;
    }

    public List<ItemBase> GetAllItems()
    {
        var res = new List<ItemBase>();
        res.AddRange(HandSpace.items);
        res.AddRange(BagSpace.items);
        return res;
    }
}
