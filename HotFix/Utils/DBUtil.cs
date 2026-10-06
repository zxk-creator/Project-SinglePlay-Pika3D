using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using PK;

public static class DBUtil
{
    // Addressables 里物品数据库的地址
    private const string DatabaseAddress = "GameAllItems";

    private static readonly string[] ToolTags =
    {
        "pickaxe", "axe", "sword", "fishingrod"
    };

    private static readonly string[] FurnitureTags =
    {
        "toilet", "bed", "shower", "tub"
    };

    private static readonly string[] ClothSlots =
    {
        "hair", "glasses", "earring", "upper", "hand",
        "bottom", "face", "stocking", "shoe"
    };

    private class ItemRowBase
    {
        public int id { get; set; }
        public int quality { get; set; }
        public string itemName { get; set; }
        public string description { get; set; }
        public string iconPath { get; set; }
        public int value { get; set; }
        public int occupiedSpace { get; set; }
        public int currentStackCount { get; set; }
        public int canStack { get; set; }
    }

    private class StuffRow : ItemRowBase
    {
    }

    private class BagRow : ItemRowBase
    {
        public float capacity { get; set; }
    }

    private class ToolRow : ItemRowBase
    {
        public string prefabPath { get; set; }
        public int toolType { get; set; }
        public float maxDurability { get; set; }
        public float level { get; set; }
    }

    private class FurnitureRow : ItemRowBase
    {
        public string prefabPath { get; set; }
        public int interactType { get; set; }
        public int canInteract { get; set; }
        public int canRecycle { get; set; }
    }

    private class ClothRow
    {
        public int id { get; set; }
        public int type { get; set; }
        public int quality { get; set; }
        public int clothType { get; set; }
        public string itemName { get; set; }
        public string description { get; set; }
        public string iconPath { get; set; }
        public int value { get; set; }
        public string prefabPath { get; set; }
        public float addedCharm { get; set; }
        public int occupiedSpace { get; set; }
        public int currentStackCount { get; set; }
        public int canStack { get; set; }
        public int canColored { get; set; }
        public string conflictPart { get; set; }
        public int noOutline { get; set; }
    }

    public static Dictionary<string, List<ItemBase>> ParseItem()
    {
        var result = new Dictionary<string, List<ItemBase>>();
        var conn = OpenDatabase();
        if (conn == null) return result;

        using (conn)
        {
            foreach (StuffRow row in conn.Query<StuffRow>("select * from stuff order by quality, id"))
            {
                var item = new ItemBase();
                FillCommon(item, row);
                Add(result, R.JsonName.jsonStuffNode, item);
            }

            foreach (BagRow row in conn.Query<BagRow>("select * from bag order by quality, id"))
            {
                var item = new BagpackBase(row.capacity);
                FillCommon(item, row);
                Add(result, R.JsonName.jsonBagNode, item);
            }

            foreach (ToolRow row in conn.Query<ToolRow>("select * from tools order by quality, id"))
            {
                if (row.toolType < 0 || row.toolType >= ToolTags.Length) continue;
                var tool = new ToolBase
                {
                    prefabPath = row.prefabPath ?? "",
                    toolType = (EToolType)row.toolType,
                    maxDurability = row.maxDurability,
                    level = row.level
                };
                tool.currentDurability = tool.maxDurability;
                FillCommon(tool, row);
                Add(result, ToolTags[row.toolType], tool);
            }

            foreach (FurnitureRow row in conn.Query<FurnitureRow>("select * from furniture order by quality, id"))
            {
                if (row.interactType < 1 || row.interactType > FurnitureTags.Length) continue;
                var item = new FurnitureBase
                {
                    prefabPath = row.prefabPath ?? "",
                    interactType = ToInteractableType(row.interactType),
                    canRecycle = row.canRecycle != 0,
                    canInteract = row.canInteract != 0
                };
                FillCommon(item, row);
                Add(result, FurnitureTags[row.interactType - 1], item);
            }
        }
        return result;
    }

    public static void ParseClothing()
    {
        var allClothes = new List<ItemBase>();
        var conn = OpenDatabase();
        if (conn != null)
        {
            using (conn)
            {
                Context.Item.clothes.male = ParseClothGender(conn, "male_cloth", allClothes);
                Context.Item.clothes.female = ParseClothGender(conn, "female_cloth", allClothes);
            }
        }
        else
        {
            Context.Item.clothes.male = EmptyGender();
            Context.Item.clothes.female = EmptyGender();
        }

        if (Context.Item.allItems == null)
        {
            Log.Error("Context.Item.allItems为空。这不该发生");
            return;
        }
        Context.Item.allItems[R.JsonName.jsonClothNode] = new List<ItemBase>(allClothes);
    }

    private static GenderEquipCollection ParseClothGender(SQLiteConnection conn, string table, List<ItemBase> allClothes)
    {
        var slots = new List<ClothBase>[ClothSlots.Length];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new List<ClothBase>();

        foreach (ClothRow row in conn.Query<ClothRow>("select * from " + table + " order by type, id"))
        {
            var cloth = new ClothBase
            {
                id = row.id,
                iconPath = row.iconPath ?? "",
                description = row.description ?? "",
                value = row.value,
                itemName = row.itemName ?? "",
                occupiedSpace = row.occupiedSpace,
                currentStackCount = row.currentStackCount,
                quality = (EItemLevel)row.quality,
                canStack = row.canStack != 0,
                prefabPath = row.prefabPath ?? "",
                clothType = (EClothType)row.clothType,
                canColored = row.canColored != 0,
                addedCharm = row.addedCharm,
                conflictPart = ClothBase.ConvertConflictParts(ParseConflictParts(row.conflictPart))
            };

            if (row.type >= 0 && row.type < slots.Length)
                slots[row.type].Add(cloth);
            allClothes.Add(cloth);
        }

        return new GenderEquipCollection
        {
            hair = slots[0],
            glasses = slots[1],
            earring = slots[2],
            upper = slots[3],
            hand = slots[4],
            bottom = slots[5],
            face = slots[6],
            stocking = slots[7],
            shoe = slots[8]
        };
    }

    private static GenderEquipCollection EmptyGender()
    {
        return new GenderEquipCollection
        {
            hair = new List<ClothBase>(),
            glasses = new List<ClothBase>(),
            earring = new List<ClothBase>(),
            upper = new List<ClothBase>(),
            hand = new List<ClothBase>(),
            bottom = new List<ClothBase>(),
            face = new List<ClothBase>(),
            stocking = new List<ClothBase>(),
            shoe = new List<ClothBase>()
        };
    }

    private static void FillCommon(ItemBase item, ItemRowBase row)
    {
        item.id = row.id;
        item.iconPath = row.iconPath ?? "";
        item.description = row.description ?? "";
        item.value = row.value;
        item.itemName = row.itemName ?? "";
        item.occupiedSpace = row.occupiedSpace;
        item.currentStackCount = row.currentStackCount;
        item.quality = (EItemLevel)row.quality;
        item.canStack = row.canStack != 0;
    }

    private static void Add(Dictionary<string, List<ItemBase>> result, string tag, ItemBase item)
    {
        if (!result.ContainsKey(tag))
            result[tag] = new List<ItemBase>();
        result[tag].Add(item);
    }

    private static List<int> ParseConflictParts(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return null;
        var list = new List<int>();
        foreach (string s in csv.Split(','))
        {
            if (int.TryParse(s, out int v))
                list.Add(v);
        }
        return list.Count > 0 ? list : null;
    }

    private static EInteractableType ToInteractableType(int jsonValue)
    {
        switch (jsonValue)
        {
            case 1: return EInteractableType.Toilet;
            case 2: return EInteractableType.Bed;
            case 3: return EInteractableType.Shower;
            case 4: return EInteractableType.Tub;
            default: return EInteractableType.None;
        }
    }

    private static SQLiteConnection OpenDatabase()
    {
        try
        {
            byte[] bytes = LoadDatabaseBytes();

            if (bytes == null || bytes.Length == 0)
            {
                Log.NoSuchFileOrPath(DatabaseAddress);
                return null;
            }

            // sqlite 原生驱动只吃文件路径，所以把 Addressables 取到的字节写到临时缓存再打开
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "GameAllItems.sqlite3");

            File.WriteAllBytes(path, bytes);

            return new SQLiteConnection(path);
        }
        catch (System.Exception e)
        {
            Log.Error("打开物品数据库失败: " + e.GetType().Name + " " + e.Message);
            return null;
        }
    }

    // Addressable 地址就是 GameAllItems，加载方法和别处完全一致
    // 注意：GameAllItems.sqlite3 由 SQLite 包的 ScriptedImporter 导入成 SQLiteAsset，
    // 不是 TextAsset，所以这里必须按 SQLiteAsset 取，再拿它的 Bytes。
    private static byte[] LoadDatabaseBytes()
    {
        AsyncOperationHandle<SQLiteAsset> handle = Addressables.LoadAssetAsync<SQLiteAsset>(DatabaseAddress);

        try
        {
            handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Log.Error("加载物品数据库失败: " + handle.OperationException?.Message);
                return null;
            }

            return handle.Result.Bytes;
        }
        catch (System.Exception e)
        {
            Log.Error("加载物品数据库异常: " + e.GetType().Name + " " + e.Message);
            return null;
        }
        finally
        {
            Addressables.Release(handle);
        }
    }
}
