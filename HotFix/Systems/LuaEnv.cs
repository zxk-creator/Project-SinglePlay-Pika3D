using System;
using System.Collections;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;
using PK;

public class LuaEnv
{
    public Script env {get; private set;}
    public List<DialogData> dialogDatas = new List<DialogData>(10);
    public LuaEnv()
    {
        env = new Script();
        env.Globals["Log"] = (Action<string>)((msg) => Log.Info(msg));
        env.Globals["contains"] = new Func<double,bool>((id) =>
        {
            var allItems = Context.localPlayer.inventory.GetAllItems();
            foreach (var i in allItems)
            {
                if (i.id == id) return true;
            }
            return false;
        });
    }

    public DialogData Run(string source)
    {
        return DialogUtil.CompileDialog(source);
    }
    
    public void ReCompileAllDialog()
    {
        TextAsset[] dialogs = Resources.LoadAll<TextAsset>("Dialog");
        foreach (var d in dialogs)
        {
            var content = d.text;
            dialogDatas.Add(DialogUtil.CompileDialog(content));
        } 
   }
}
