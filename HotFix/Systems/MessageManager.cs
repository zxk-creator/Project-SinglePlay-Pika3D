using System;
using System.Collections.Generic;
using PK;

public class MessageManager
{
    public void Command(string command)
    {
        if (string.IsNullOrEmpty(command)) return;
        string trimmed = command.Trim();
        if (!trimmed.StartsWith("/")) return;

        string[] parts = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) return;

        string cmd = parts[0].ToLower();
        if (cmd == "/enter")
        {
            if (parts.Length < 2)
            {
                Log.Error("命令缺少参数：/enter <SceneName>");
                return;
            }
            EnterScene(parts[1]);
        }
        else if (cmd == "/give")
        {
            if (parts.Length < 2)
            {
                Log.Error("命令缺少参数：/give <ItemId>");
                return;
            }
            GiveItem(parts[1]);
        }
        else if (cmd == "/summonnpc")
        {
            UnityEngine.Vector3 targetPosition = Context.localPlayer.transform.position
                         + Context.localPlayer.transform.forward * 2f;
            UnityEngine.Quaternion rotation = Context.localPlayer.transform.rotation;
            NPCUtil.TrySummonNPC(targetPosition, rotation);
        }
        else if (cmd == "/summonfurniture")
        {
            if (parts.Length < 2)
            {
                Log.Error("命令缺少参数：/summonfurniture <FurnitureId>");
                return;
            }
            UnityEngine.Vector3 targetPosition = Context.localPlayer.transform.position
                         + Context.localPlayer.transform.forward * 2f;
            UnityEngine.Quaternion rotation = Context.localPlayer.transform.rotation;

            int id;
            int.TryParse(parts[1],out id);
            if (id == 0)
            {
                Log.Error("输入了非法字符！应该为数字");
                return;
            }

            ItemBase item = null;

            bool founded = false;
            foreach (var list in Context.Item.allItems.Values)
            {
                founded = ScanList(list, id, out item);
                if (founded)
                {
                    Log.Info("找到了");
                    break;
                }
            }

            FurnitureBase furniture = item as FurnitureBase;
            if (furniture == null)
            {
                Log.Error(id + "并非是一个家具物品或ID不存在！请使用give生成");
                return;
            }

            Util.SummonFuriture(furniture, targetPosition, rotation);
        }
        else if (cmd == "/kill")
        {
            Context.localPlayer.Dead();
        }
        else if (cmd == "/showcomputer")
        {
            new ComputerPanel().Show();
        }
        else if (cmd == "/showphone")
        {
            new PhonePanel().Show();
        }
        else
        {
            Log.Error("未知命令：" + parts[0]);
        }
    }

    private void EnterScene(string sceneName)
    {
        foreach (var pair in Context.sc.scenes)
        {
            if (string.Equals(pair.Value, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                Context.sc.EnterNewScene(pair.Key, true);
                return;
            }
        }
        Log.Error("未找到场景：" + sceneName);
    }

    private void GiveItem(string idStr)
    {
        if (!int.TryParse(idStr, out int id))
        {
            Log.Error("物品ID格式错误：" + idStr);
            return;
        }

        if (!TryFindItem(id, out ItemBase template))
        {
            Log.Error("未找到ID为" + id + "的物品");
            return;
        }

        ItemBase item = template.GetACopy();

        ItemBase remain = Context.localPlayer.inventory.TryPutIntoBag(item);
        if (remain != null)
        {
            Log.Warn("背包空间不足，" + item.itemName + "未能完全放入");
        }
        else
        {
            Log.Info("已给予物品：" + item.itemName + "（ID:" + id + "）");
        }
    }

    private bool TryFindItem(int id, out ItemBase item)
    {
        item = null;
        if (Context.Item.allItems != null)
        {
            foreach (var list in Context.Item.allItems.Values)
            {
                if (ScanList(list, id, out item)) return true;
            }
        }
        if (ScanGender(Context.Item.clothes.male, id, out item)) return true;
        if (ScanGender(Context.Item.clothes.female, id, out item)) return true;
        return false;
    }

    private bool ScanGender(GenderEquipCollection collection, int id, out ItemBase item)
    {
        item = null;
        if (ScanList(collection.hair, id, out item)) return true;
        if (ScanList(collection.glasses, id, out item)) return true;
        if (ScanList(collection.earring, id, out item)) return true;
        if (ScanList(collection.upper, id, out item)) return true;
        if (ScanList(collection.hand, id, out item)) return true;
        if (ScanList(collection.bottom, id, out item)) return true;
        if (ScanList(collection.face, id, out item)) return true;
        if (ScanList(collection.stocking, id, out item)) return true;
        if (ScanList(collection.shoe, id, out item)) return true;
        return false;
    }

    private bool ScanList(IEnumerable<ItemBase> list, int id, out ItemBase item)
    {
        item = null;
        if (list == null) return false;
        foreach (var it in list)
        {
            if (it.id == id)
            {
                item = it;
                return true;
            }
        }
        return false;
    }
}
