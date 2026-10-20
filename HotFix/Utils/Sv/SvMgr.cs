using LiteNetLib.Utils;
using PK;
using PKWeb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace PKSv;

// 存档系统，自定义二进制格式。世界和玩家合并
public static class SvMgr
{
    // 密匙
    private static readonly byte[] Key =
    {
        0x3A, 0x7F, 0x91, 0xC2, 0x08, 0xD4, 0x6E, 0x55,
        0xB1, 0x02, 0x9A, 0x3C, 0x47, 0xDE, 0x80, 0x1F,
        0x62, 0xA5, 0x39, 0x8B, 0x14, 0xCD, 0x7E, 0xF0,
        0x21, 0x56, 0xBB, 0x43, 0x99, 0x0D, 0xE7, 0x38,
    };

    // 硬编码的存档哈希值，只支持五个存档
    public static readonly string[] svFileNames = [
        "3f7a9c2e8b1d4f605a3c9e7b2d8f1460",
        "c81b5e3a9f207d4c6b1e8a3f5c9d0274",
        "7e2d9f4b1a8c305e6f4b2d7a9c3e8150",
        "a94f1c7e3b605d298f4a1c7e3b605d28",
        "5b8e2f6a1c9d407b3e5a8f2c6d1b9047"
    ];

    // 开头魔数
    private static readonly byte[] magic = Encoding.ASCII.GetBytes("KKGM");
    private const int currentVersion = 1;

    // 从磁盘上读
    public static SaveData Read(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= svFileNames.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        byte[] file = Path.ReadSave(svFileNames[slotIndex]);
        if (file == null) return null;

        var br = new NetDataReader(file);

        // 魔数校验
        // 长度前缀用 int（不是 PutBytesWithLength 的 ushort）：
        // ushort 上限 65535 且溢出是静默截断，存档（含整个背包）很容易超，会写坏档。
        int magicLen = br.GetInt();
        byte[] magic = new byte[magicLen];
        br.GetBytes(magic, magicLen);

        if (!EncryptUtil.BytesEqual(magic, SvMgr.magic))
            throw new InvalidDataException("魔数不匹配，不是本游戏的存档");

        // 版本校验
        int version = br.GetInt();
        if (version != currentVersion)
            throw new InvalidDataException($"不支持的存档版本：{version}");

        // 密文
        int cipherLen = br.GetInt();
        if (cipherLen <= 0 || cipherLen > br.AvailableBytes)
            throw new InvalidDataException("密文长度非法");

        byte[] cipher = new byte[cipherLen];
        br.GetBytes(cipher, cipherLen);

        // 解密
        byte[] plain = EncryptUtil.Decrypt(Key, cipher);

        // 反序列化
        var data = new SaveData();
        data.Deserialize(new NetDataReader(plain));
        return data;
    }

    // 写入到磁盘上，注意，slotIndex是直接写入，不会提示！要想检测请在游戏里面检测。
    public static void Write(SaveData data, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= svFileNames.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        // 目前的明文
        byte[] plain;

        var pw = new NetDataWriter();
        data.Serialize(pw);
        plain = pw.CopyData();

        byte[] cipher = EncryptUtil.Encrypt(Key, plain);

        var bw = new NetDataWriter();
        bw.Put(magic.Length);                 // int 长度前缀
        bw.Put(magic);                        // "KKGM"
        bw.Put(currentVersion);               // int
        bw.Put(cipher.Length);                // int 长度前缀
        bw.Put(cipher);                       // IV + 密文

        // 写入到磁盘
        Path.WriteSave(svFileNames[slotIndex], bw.CopyData());
    }

    public static int FindEmptySlot()
    {
        for (int i = 0; i < svFileNames.Length; i++)
        {
            if (Path.ReadSave(svFileNames[i]) == null)
                return i;
        }
        return -1;
    }

    public static void Delete(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= svFileNames.Length)
            return;

        Path.DeleteSave(svFileNames[slotIndex]);
    }
}

public enum ESaveLocation
{
    DEFAULT,
}

// 存档数据。玩家和世界绑定在一起，不再分开
public class SaveData : INetSerializable
{
    public string playerName;
    public string worldName;

    public CharacterEquipCollection equipedCloth = new CharacterEquipCollection();
    public BagpackBase handSpace = new BagpackBase();
    public BagpackBase equipedBag;
    public ItemBase currentEquipedTool;
    public bool isGirl = true;

    public void Serialize(NetDataWriter bw)
    {
        bw.Put(playerName);
        bw.Put(worldName);
        bw.Put(isGirl);

        WriteClothCollection(bw, equipedCloth);

        if (handSpace == null)
        {
            PK.Log.Error("[SaveData] handSpace 为 null，已按空手部背包写入");
            handSpace = new BagpackBase();
        }
        handSpace.Serialize(bw);

        bw.Put(equipedBag != null);
        if (equipedBag != null) equipedBag.Serialize(bw);

        WriteItem(bw, currentEquipedTool);
    }

    public void Deserialize(NetDataReader br)
    {
        playerName = br.GetString();
        worldName = br.GetString();
        isGirl = br.GetBool();

        equipedCloth = ReadClothCollection(br);

        handSpace = new BagpackBase();
        handSpace.Deserialize(br);

        if (br.GetBool())
        {
            equipedBag = new BagpackBase();
            equipedBag.Deserialize(br);
        }
        else equipedBag = null;

        currentEquipedTool = ReadItem(br);
    }

    /// <summary>
    /// 比较两份存档数据的所有状态字段是否完全一致（用于脏标记检查，避免重复同步或写盘）
    /// </summary>
    public bool IsSameAs(SaveData other)
    {
        if (other == null)
            return false;

        if (playerName != other.playerName) return false;
        if (worldName != other.worldName) return false;
        if (isGirl != other.isGirl) return false;

        // equipedCloth
        if (equipedCloth == null != (other.equipedCloth == null)) return false;
        if (equipedCloth != null && !equipedCloth.IsSameAs(other.equipedCloth)) return false;

        // handSpace
        if (handSpace == null != (other.handSpace == null)) return false;
        if (handSpace != null && !handSpace.IsSameAs(other.handSpace)) return false;

        // equipedBag
        if (equipedBag == null != (other.equipedBag == null)) return false;
        if (equipedBag != null && !equipedBag.IsSameAs(other.equipedBag)) return false;

        // currentEquipedTool
        if (currentEquipedTool == null != (other.currentEquipedTool == null)) return false;
        if (currentEquipedTool != null && !currentEquipedTool.IsSameAs(other.currentEquipedTool)) return false;

        return true;
    }

    /// <summary>直接替换整套装备</summary>
    public bool SetCharacterEquips(CharacterEquipCollection newEquips)
    {
        CharacterEquipCollection target = newEquips ?? new CharacterEquipCollection();

        var dropped = new List<ItemBase>();
        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            if (slot == EClothType.NONE) continue;

            ItemBase old = equipedCloth.GetSlot(slot);
            if (old == null) continue;
            if (ReferenceEquals(old, target.GetSlot(slot))) continue;
            if (dropped.Exists(x => ReferenceEquals(x, old))) continue;

            dropped.Add(old);
        }

        var incoming = new List<ItemBase>();
        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            if (slot == EClothType.NONE) continue;

            ItemBase cloth = target.GetSlot(slot);
            if (cloth == null) continue;

            if (!(cloth is ClothBase targetCloth) || targetCloth.clothType != slot)
            {
                string actualClothType = (cloth as ClothBase) != null ? (cloth as ClothBase).clothType.ToString() : "非ClothBase";
                PK.Log.Error($"[SaveData] SetCharacterEquips 槽位 {slot} 的物品 {cloth.itemName} 类型为 {cloth.itemType}、clothType 为 {actualClothType}，与槽位不符，替换已取消");
                return false;
            }

            if (incoming.Exists(x => ReferenceEquals(x, cloth))) continue;

            incoming.Add(cloth);
        }

        BagpackBase handCopy = handSpace == null ? new BagpackBase() : handSpace.GetACopy() as BagpackBase;
        BagpackBase bagCopy = equipedBag == null ? null : equipedBag.GetACopy() as BagpackBase;

        if (handCopy == null || (equipedBag != null && bagCopy == null))
        {
            PK.Log.Error("[SaveData] SetCharacterEquips 复制背包失败，替换已取消");
            return false;
        }

        foreach (ItemBase cloth in incoming)
        {
            if (handSpace != null)
            {
                int hi = handSpace.items.FindIndex(x => ReferenceEquals(x, cloth));
                if (hi >= 0 && hi < handCopy.items.Count)
                {
                    handCopy.RemoveItem(handCopy.items[hi]);
                    continue;
                }
            }

            if (equipedBag != null && bagCopy != null)
            {
                int bi = equipedBag.items.FindIndex(x => ReferenceEquals(x, cloth));
                if (bi >= 0 && bi < bagCopy.items.Count)
                    bagCopy.RemoveItem(bagCopy.items[bi]);
            }
        }

        foreach (ItemBase old in dropped)
        {
            ItemBase itemCopy = old.GetACopy();
            if (itemCopy == null)
            {
                PK.Log.Error($"[SaveData] SetCharacterEquips 复制 {old.itemName} 失败，替换已取消");
                return false;
            }

            ItemBase remain = handCopy.TryPutIntoBag(itemCopy);
            if (remain != null && bagCopy != null) remain = bagCopy.TryPutIntoBag(remain);

            if (remain != null)
            {
                PK.Log.Warn($"[SaveData] SetCharacterEquips 背包放不下换下的 {old.itemName}，替换已取消");
                return false;
            }
        }

        var takenOut = new List<ItemBase>();
        foreach (ItemBase cloth in incoming)
        {
            if (TryTakeOutOfBag(cloth))
            {
                takenOut.Add(cloth);
                continue;
            }

            foreach (ItemBase back in takenOut) TryPutIntoBag(back);
            PK.Log.Error($"[SaveData] SetCharacterEquips 无法把 {cloth.itemName} 从背包取出，替换已取消");
            return false;
        }

        CharacterEquipCollection previous = equipedCloth;
        equipedCloth = target;

        var placed = new List<ItemBase>();
        foreach (ItemBase old in dropped)
        {
            ItemBase remain = TryPutIntoBag(old);
            if (remain == null)
            {
                placed.Add(old);
                continue;
            }

            foreach (ItemBase back in placed) RemoveFromBag(back);
            equipedCloth = previous;
            PK.Log.Error($"[SaveData] SetCharacterEquips 预检通过但实际放不下 {old.itemName}，替换已取消");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 换一件衣服。bForceReplace 为 true 时旧衣服直接丢弃（不尝试放回背包）；
    /// 否则尝试放入背包，放不下则失败并回滚。
    /// </summary>
    public bool ChangeCloth(ClothBase cloth, bool bForceReplace = false)
    {
        if (cloth == null || cloth.clothType == EClothType.NONE)
        {
            PK.Log.Error("[SaveData] ChangeCloth 参数非法：cloth 为 null 或 clothType 为 NONE");
            return false;
        }

        EClothType slot = cloth.clothType;
        ItemBase old = equipedCloth.GetSlot(slot);

        if (ReferenceEquals(old, cloth)) return true;

        bool restoreOld = old != null && !bForceReplace;
        bool clothInBag = IsInBag(cloth);

        if (!TryTakeOutOfBag(cloth))
        {
            PK.Log.Error($"[SaveData] ChangeCloth 无法把 {cloth.itemName} 从背包取出，换装已取消");
            return false;
        }

        if (restoreOld && !CanPutAllIntoBag(old))
        {
            bool lost = false;
            if (clothInBag)
            {
                ItemBase back = TryPutIntoBag(cloth);
                lost = back != null;
            }

            if (lost)
                PK.Log.Error($"[SaveData] ChangeCloth 背包放不下换下的 {old.itemName}，且 {cloth.itemName} 放不回背包，{cloth.itemName} 已丢失");
            else
                PK.Log.Warn($"[SaveData] ChangeCloth 背包放不下换下的 {old.itemName}，换装已取消");
            return false;
        }

        if (old != null)
        {
            equipedCloth.SetSlot(slot, null);
            if (restoreOld)
            {
                ItemBase remain = TryPutIntoBag(old);
                if (remain != null)
                {
                    equipedCloth.SetSlot(slot, old);
                    PK.Log.Error($"[SaveData] ChangeCloth 预检通过但实际放不下 {old.itemName}，换装已取消");
                    return false;
                }
            }
        }

        equipedCloth.SetSlot(slot, cloth);
        return true;
    }

    /// <summary>卸下某部位的装备并尝试放入背包，放不下则失败回滚</summary>
    public bool RemoveCloth(EClothType slot)
    {
        ItemBase current = equipedCloth.GetSlot(slot);
        if (current == null)
        {
            PK.Log.Warn($"[SaveData] RemoveCloth 时槽位 {slot} 本来就没有装备");
            return false;
        }

        if (!CanPutAllIntoBag(current))
        {
            PK.Log.Warn($"[SaveData] RemoveCloth 背包放不下卸下的 {current.itemName}，卸下已取消");
            return false;
        }

        equipedCloth.SetSlot(slot, null);
        ItemBase remain = TryPutIntoBag(current);
        if (remain != null)
        {
            equipedCloth.SetSlot(slot, current);
            PK.Log.Error($"[SaveData] RemoveCloth 预检通过但实际放不下 {current.itemName}，卸下已取消");
            return false;
        }
        return true;
    }

    /// <summary>按引用卸下某件衣服</summary>
    public bool RemoveClothByReference(ClothBase cloth)
    {
        if (cloth == null)
        {
            PK.Log.Error("[SaveData] RemoveClothByReference 传入的 cloth 为 null");
            return false;
        }

        foreach (EClothType slot in Enum.GetValues(typeof(EClothType)))
        {
            if (slot == EClothType.NONE) continue;
            if (equipedCloth.GetSlot(slot) == cloth)
                return RemoveCloth(slot);
        }

        PK.Log.Warn($"[SaveData] RemoveClothByReference 在装备里找不到 {cloth.itemName}");
        return false;
    }

    /// <summary>设置主背包（可能 null）。会强制替换背包，不会进行检查等操作，检查在Inventory里。</summary>
    public void SetBag(BagpackBase newBag)
    {
        if (ReferenceEquals(newBag, handSpace))
        {
            PK.Log.Error("[SaveData] SetBag 传入的背包就是 handSpace，会导致同一背包被写入两次，已拒绝");
            return;
        }

        equipedBag = newBag;
    }

    /// <summary>设置手部背包，传 null 则重置成空手部）</summary>
    public void SetHand(BagpackBase newHand)
    {
        if (newHand != null && ReferenceEquals(newHand, equipedBag))
        {
            PK.Log.Error("[SaveData] SetHand 传入的背包就是 equipedBag，会导致同一背包被写入两次，已拒绝");
            return;
        }

        handSpace = newHand ?? new BagpackBase();
    }

    /// <summary>
    /// 尝试将物品放入手部或主背包。
    /// 全部放下返回 null；放不下的剩余部分返回对象。
    /// </summary>
    public ItemBase TryPutIntoBag(ItemBase item)
    {
        if (item == null) return null;

        if (handSpace == null)
        {
            PK.Log.Error("[SaveData] TryPutIntoBag 时 handSpace 为 null，已重置为空手部背包");
            handSpace = new BagpackBase();
        }

        ItemBase remain = handSpace.TryPutIntoBag(item);
        if (remain == null) return null;

        if (equipedBag != null)
            remain = equipedBag.TryPutIntoBag(remain);

        return remain;
    }

    /// <summary>从手部或主背包移除物品</summary>
    public bool RemoveFromBag(ItemBase item)
    {
        if (item == null) return false;

        if (handSpace == null)
        {
            PK.Log.Error("[SaveData] RemoveFromBag 时 handSpace 为 null，已重置为空手部背包");
            handSpace = new BagpackBase();
        }

        if (handSpace.RemoveItem(item)) return true;
        if (equipedBag != null && equipedBag.RemoveItem(item)) return true;
        return false;
    }

    /// <summary>设置手上工具（可传 null 卸下）</summary>
    public bool SetTool(ItemBase tool)
    {
        if (tool != null && !(tool is ToolBase))
        {
            PK.Log.Error($"[SaveData] SetTool 收到的物品 {tool.itemName} 类型为 {tool.itemType}，不是 ToolBase，已拒绝");
            return false;
        }

        if (ReferenceEquals(currentEquipedTool, tool)) return true;

        bool restoreOld = currentEquipedTool != null;
        bool toolInBag = IsInBag(tool);

        if (!TryTakeOutOfBag(tool))
        {
            PK.Log.Error($"[SaveData] SetTool 无法把 {tool.itemName} 从背包取出，换工具已取消");
            return false;
        }

        if (restoreOld && !CanPutAllIntoBag(currentEquipedTool))
        {
            bool lost = false;
            if (toolInBag)
            {
                ItemBase back = TryPutIntoBag(tool);
                lost = back != null;
            }

            if (lost)
                PK.Log.Error($"[SaveData] SetTool 背包放不下卸下的 {currentEquipedTool.itemName}，且 {tool.itemName} 放不回背包，{tool.itemName} 已丢失");
            else
                PK.Log.Error($"[SaveData] SetTool 背包放不下卸下的 {currentEquipedTool.itemName}，换工具已取消");
            return false;
        }

        if (restoreOld)
        {
            ItemBase old = currentEquipedTool;
            currentEquipedTool = null;
            ItemBase remain = TryPutIntoBag(old);
            if (remain != null)
            {
                currentEquipedTool = old;
                PK.Log.Error($"[SaveData] SetTool 预检通过但实际放不下 {old.itemName}，换工具已取消");
                return false;
            }
        }

        currentEquipedTool = tool;
        return true;
    }

    /// <summary>卸下工具</summary>
    public void RemoveTool()
    {
        currentEquipedTool = null;
    }

    /// <summary>拿所有物品（手部背包 + 主背包）</summary>
    public List<ItemBase> GetAllItems()
    {
        var res = new List<ItemBase>();

        if (handSpace == null)
            PK.Log.Error("[SaveData] GetAllItems 时 handSpace 为 null，已按空手部背包处理");
        else
            res.AddRange(handSpace.items);

        if (equipedBag != null) res.AddRange(equipedBag.items);
        return res;
    }

    /// <summary>按 id 找物品，找不到返回 null</summary>
    public ItemBase FindItemById(int id)
    {
        if (handSpace == null)
        {
            PK.Log.Error("[SaveData] FindItemById 时 handSpace 为 null，已只查主背包");
        }
        else
        {
            foreach (var item in handSpace.items)
                if (item != null && item.id == id) return item;
        }

        if (equipedBag != null)
        {
            foreach (var item in equipedBag.items)
                if (item != null && item.id == id) return item;
        }
        return null;
    }

    /// <summary>清空所有物品与装备（新建存档 / 重置时用）</summary>
    public void ClearAllItems()
    {
        equipedCloth = new CharacterEquipCollection();
        handSpace = new BagpackBase();
        equipedBag = null;
        currentEquipedTool = null;
    }

    private bool IsInBag(ItemBase item)
    {
        if (item == null) return false;
        if (handSpace != null && handSpace.items.Contains(item)) return true;
        if (equipedBag != null && equipedBag.items.Contains(item)) return true;
        return false;
    }

    private bool TryTakeOutOfBag(ItemBase item)
    {
        if (item == null) return true;

        if (handSpace != null && handSpace.items.Contains(item))
            return handSpace.RemoveItem(item);

        if (equipedBag != null && equipedBag.items.Contains(item))
            return equipedBag.RemoveItem(item);

        return true;
    }

    public bool CanPutAllIntoBag(ItemBase item)
    {
        if (item == null) return true;

        BagpackBase handCopy = handSpace == null ? new BagpackBase() : handSpace.GetACopy() as BagpackBase;
        if (handCopy == null)
        {
            PK.Log.Error("[SaveData] CanPutAllIntoBag 复制手部背包失败");
            return false;
        }

        BagpackBase bagCopy = equipedBag == null ? null : equipedBag.GetACopy() as BagpackBase;

        ItemBase itemCopy = item.GetACopy();
        if (itemCopy == null)
        {
            PK.Log.Error($"[SaveData] CanPutAllIntoBag 复制物品 {item.itemName} 失败");
            return false;
        }

        ItemBase remain = handCopy.TryPutIntoBag(itemCopy);
        if (remain == null) return true;
        if (bagCopy == null) return false;

        remain = bagCopy.TryPutIntoBag(remain);
        return remain == null;
    }

    private void WriteItem(NetDataWriter bw, ItemBase item)
    {
        if (item == null) { bw.Put(false); return; }
        bw.Put(true);
        bw.Put((int)item.itemType);
        item.Serialize(bw);
    }

    private ItemBase ReadItem(NetDataReader br)
    {
        if (!br.GetBool()) return null;
        EItemType type = (EItemType)br.GetInt();
        ItemBase item = ItemBase.NewItem(type);
        item.Deserialize(br);
        return item;
    }

    private void WriteClothCollection(NetDataWriter bw, CharacterEquipCollection e)
    {
        if (e == null)
            PK.Log.Error("[SaveData] equipedCloth 为 null，已按全空着装写入");

        WriteItem(bw, e?.Hair);
        WriteItem(bw, e?.Glasses);
        WriteItem(bw, e?.Earring);
        WriteItem(bw, e?.Upper);
        WriteItem(bw, e?.Hands);
        WriteItem(bw, e?.Bottom);
        WriteItem(bw, e?.Face);
        WriteItem(bw, e?.Stock);
        WriteItem(bw, e?.Shoes);
    }

    private CharacterEquipCollection ReadClothCollection(NetDataReader br)
    {
        var e = new CharacterEquipCollection();
        ReadClothSlot(e, EClothType.HAIR, br);
        ReadClothSlot(e, EClothType.GLASSES, br);
        ReadClothSlot(e, EClothType.EARRING, br);
        ReadClothSlot(e, EClothType.UPPER, br);
        ReadClothSlot(e, EClothType.HAND, br);
        ReadClothSlot(e, EClothType.BOTTOM, br);
        ReadClothSlot(e, EClothType.FACE, br);
        ReadClothSlot(e, EClothType.STOCKING, br);
        ReadClothSlot(e, EClothType.SHOE, br);
        return e;
    }

    private void ReadClothSlot(CharacterEquipCollection e, EClothType slot, NetDataReader br)
    {
        ItemBase item = ReadItem(br);
        if (item == null) return;

        if (!(item is ClothBase cloth))
        {
            PK.Log.Error($"[SaveData] 槽位 {slot} 读到的物品 {item.itemName} 类型为 {item.itemType}，不是 ClothBase，已丢弃");
            return;
        }

        if (cloth.clothType != slot)
        {
            PK.Log.Error($"[SaveData] 槽位 {slot} 读到的 {cloth.itemName} 其 clothType 为 {cloth.clothType}，与槽位不符，已丢弃");
            return;
        }

        e.SetSlot(slot, cloth);
    }
}