using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PK;

public class PromptMessage : UIBase
{
    public string msg;


    public PromptMessage(string msg) : base("PromptMessageItem")
    {
        GetTargetComponent<TMP_Text>("InfoText").text = msg;
        Context.updateProxy.RegisterDelayTask(Hide, 5);
        this.msg = msg;
    }

    // 会在终端中打印日志。
    public override void Show()
    {
        Log.Info(msg);
        SetStackOrder(9999);
        SetVisible(true);
        SetInteractable(true);

        UIPrefab.SetActive(true);
    }

    public override void Hide()
    {
        Object.Destroy(UIPrefab);
    }
}
