using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogPanel : UIBase, IInputAcceptable
{
    private TMP_Text tmp_who;
    // 实现逐渐出现的组件
    private TypewriterTMP writer;
    private string DialogButtonPath = "DialogButton";
    private DialogData currentSessionDialog;
    public DialogPanel(DialogData currentSessionDialog) : base("Dialog")
    {
        this.currentSessionDialog = currentSessionDialog;
        writer = GetTargetComponent<TypewriterTMP>();
        // 播放入口对话
        writer.Play(currentSessionDialog.entryNode.content);
        Context.updateProxy.RegisterNewTask(Update,int.MaxValue);
    }

    public override void Destroy()
    {
        base.Destroy();
        
    }


    void Update()
    {
        
    }
}
