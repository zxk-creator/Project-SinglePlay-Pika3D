using LiteNetLib.Utils;
using System.Collections.Generic;
using UnityEngine;
using PK;
using System.IO;

public class BagpackBase : ItemBase
{
    public float capacity { get; private set; } = 5;
    public List<ItemBase> items { get; private set; } = new List<ItemBase>(0);

    public BagpackBase(float capacity)
    {
        this.capacity = capacity;
        canStack = false;
        itemType = EItemType.BagpackBase;
    }

    public BagpackBase()
    {
        itemType = EItemType.BagpackBase;
    }

    public void SetBagCapacity(float newValue)
    {
        capacity = Mathf.Clamp(newValue, 1, 1000);
    }

    public ItemBase TryPutIntoBag(ItemBase newItem)
    {
        if (newItem == null)
        {
            Log.NullPtr("TryPutIntoBag");
            return newItem;
        }

        if (newItem == this)
        {
            Log.Warn("不能把背包放入自身");
            return newItem;
        }

        if (newItem is BagpackBase newBag && newBag.ContainsBag(this))
        {
            Log.Warn("不能把包含当前背包的背包放入，会形成循环套包");
            return newItem;
        }

        int itemSpace = GetItemTotalSpace(newItem);
        float freeSpace = capacity - GetCurrentOccupiedSpace();

        if (!newItem.canStack)
        {
            if (itemSpace <= freeSpace)
            {
                items.Add(newItem);
                return null;
            }
            return newItem;
        }

        if (itemSpace <= freeSpace)
        {
            ItemBase same = FindSameStackable(newItem);
            if (same != null) same.currentStackCount += newItem.currentStackCount;
            else items.Add(newItem);
            return null;
        }

        int fit = (int)(freeSpace / newItem.occupiedSpace);
        if (fit <= 0) return newItem;

        int originalCount = newItem.currentStackCount;
        newItem.currentStackCount = fit;
        ItemBase same2 = FindSameStackable(newItem);
        if (same2 != null) same2.currentStackCount += fit;
        else items.Add(newItem);

        ItemBase remainder = newItem.GetACopy();
        remainder.currentStackCount = originalCount - fit;
        Log.Warn("背包空间不足，只放入了" + fit + "个" + newItem.itemName + "，剩余" + remainder.currentStackCount + "个放不下");
        return remainder;
    }

    public int GetCurrentOccupiedSpace()
    {
        int occupied = 0;
        foreach (var i in items)
        {
            occupied += GetItemTotalSpace(i);
        }
        return occupied;
    }

    public bool ContainsBag(BagpackBase bag)
    {
        foreach (var i in items)
        {
            if (i == bag) return true;
            if (i is BagpackBase b && b.ContainsBag(bag)) return true;
        }
        return false;
    }

    private int GetItemTotalSpace(ItemBase item)
    {
        if (item is BagpackBase bag) return bag.occupiedSpace + bag.GetCurrentOccupiedSpace();
        return item.occupiedSpace * item.currentStackCount;
    }

    private ItemBase FindSameStackable(ItemBase item)
    {
        return items.Find(i => i.itemName == item.itemName && i.canStack);
    }

    public bool RemoveItem(ItemBase item)
    {
        return items.Remove(item);
    }

    public override ItemBase GetACopy()
    {
        var copy = new BagpackBase();
        copy.canStack = canStack;
        copy.currentStackCount = currentStackCount;
        copy.description = description;
        copy.iconPath = iconPath;
        copy.quality = quality;
        copy.itemName = itemName;
        copy.occupiedSpace = occupiedSpace;
        copy.value = value;
        copy.capacity = capacity;
        copy.id = id;
        foreach (var i in items)
        {
            copy.items.Add(i.GetACopy());
        }

        return copy;
    }

    /// <summary>
    /// （慎用）直接用一批物品替换背包内容
    /// </summary>
    /// <param name="newItems"></param>
    public void ReplaceItems(List<ItemBase> newItems)
    {
        items.Clear();
        if (newItems != null)
            items.AddRange(newItems);
    }

    public override void Serialize(NetDataWriter bw)
    {
        base.Serialize(bw);

        bw.Put(items.Count);
        foreach (var item in items)
        {
            bw.Put((int)item.itemType);
            item.Serialize(bw);
        }
    }

    public override void Deserialize(NetDataReader br)
    {
        base.Deserialize(br);

        int count = br.GetInt();
        items.Clear();
        for (int i = 0; i < count; i++)
        {
            EItemType type = (EItemType)br.GetInt();
            ItemBase item = NewItem(type);
            item.Deserialize(br);
            items.Add(item);
        }
    }
}
