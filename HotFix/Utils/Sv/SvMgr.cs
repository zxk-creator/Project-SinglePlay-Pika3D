using LiteNetLib.Utils;
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

    // 玩家穿的衣服
    public CharacterEquipCollection equipedCloth = new CharacterEquipCollection();
    // 手（可能为 null）
    public List<ItemBase> handItems;
    // 包里的东西（可能为 null）
    public List<ItemBase> bagpackItems;
    // 装备的包（可能为 null）
    public BagpackBase equipedBag;
    // 当前拿在手上的工具（可能为 null）
    public ItemBase currentEquipedTool;
    // 玩家性别
    public bool isGirl = true;

    public void Serialize(NetDataWriter bw)
    {
        bw.Put(playerName);
        bw.Put(worldName);
        bw.Put(isGirl);

        WriteClothCollection(bw, equipedCloth);
        WriteItemList(bw, handItems);
        WriteItemList(bw, bagpackItems);
        WriteItem(bw, equipedBag);
        WriteItem(bw, currentEquipedTool);
    }

    public void Deserialize(NetDataReader br)
    {
        playerName = br.GetString();
        worldName = br.GetString();
        isGirl = br.GetBool();

        equipedCloth = ReadClothCollection(br);
        handItems = ReadItemList(br);
        bagpackItems = ReadItemList(br);
        equipedBag = ReadItem(br) as BagpackBase;
        currentEquipedTool = ReadItem(br);
    }

    private static void WriteItem(NetDataWriter bw, ItemBase item)
    {
        if (item == null) { bw.Put(false); return; }
        bw.Put(true);
        bw.Put((int)item.itemType);
        item.Serialize(bw);
    }

    private static ItemBase ReadItem(NetDataReader br)
    {
        if (!br.GetBool()) return null;
        EItemType type = (EItemType)br.GetInt();
        ItemBase item = ItemBase.NewItem(type);
        item.Deserialize(br);
        return item;
    }

    // 物品列表（可能为 null）
    private static void WriteItemList(NetDataWriter bw, List<ItemBase> list)
    {
        if (list == null) { bw.Put(false); return; }
        bw.Put(true);
        bw.Put(list.Count);
        for (int i = 0; i < list.Count; i++)
            WriteItem(bw, list[i]);
    }

    private static List<ItemBase> ReadItemList(NetDataReader br)
    {
        if (!br.GetBool()) return null;
        int count = br.GetInt();
        var list = new List<ItemBase>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadItem(br));
        return list;
    }

    private static void WriteClothCollection(NetDataWriter bw, CharacterEquipCollection e)
    {
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

    private static CharacterEquipCollection ReadClothCollection(NetDataReader br)
    {
        var e = new CharacterEquipCollection();

        e.SetSlot(EClothType.HAIR,     ReadItem(br));
        e.SetSlot(EClothType.GLASSES,  ReadItem(br));
        e.SetSlot(EClothType.EARRING,  ReadItem(br));
        e.SetSlot(EClothType.UPPER,    ReadItem(br));
        e.SetSlot(EClothType.HAND,     ReadItem(br));
        e.SetSlot(EClothType.BOTTOM,   ReadItem(br));
        e.SetSlot(EClothType.FACE,     ReadItem(br));
        e.SetSlot(EClothType.STOCKING, ReadItem(br));
        e.SetSlot(EClothType.SHOE,     ReadItem(br));

        return e;
    }
}
