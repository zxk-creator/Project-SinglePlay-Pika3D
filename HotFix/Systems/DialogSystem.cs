using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogSystem
{
    // 路人角色随机选择的对话(根目录)
    public string femaleNPCDialogs = "Dialog/FemaleNPC";
    public string maleNPCDialogs = "Dialog/MaleNPC";

    /// <summary>
    /// 调用这些函数，前提是玩家必须存在！
    /// </summary>
    /// <returns>对话数据</returns>
    public DialogData GetRandomFemaleDialogs()
    {
        var femaleDialogs = Resources.LoadAll<TextAsset>(maleNPCDialogs);
        var dialogs = new List<DialogData>();
        foreach (var d in femaleDialogs)
        {
            dialogs.Add(DialogUtil.CompileDialog(d.text));
        }

        return dialogs.RandomElement();
    }

    public DialogData GetRandomMaleDialogs()
    {
        var maleDialogs = Resources.LoadAll<TextAsset>(maleNPCDialogs);
        var dialogs = new List<DialogData>();
        foreach (var d in maleDialogs)
        {
            dialogs.Add(DialogUtil.CompileDialog(d.text));
        }

        return dialogs.RandomElement();
    }
}
