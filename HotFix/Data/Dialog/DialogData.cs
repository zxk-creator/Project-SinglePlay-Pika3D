using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using PK;

// 每一个对话Lua文件编译后都变成了这个
public class DialogData
{
    public string sessionName;
    // {对话节点名：label节点列表}
    public Dictionary<string,List<DialogNode>> dialogNodes;
    // 入口标签名（强制为 "label1"）
    public string entryLabel;
    // 入口节点（label1 的第一个节点）
    public DialogNode entryNode;
}

// 普通的，一个用于展示对话的节点，也是最终编译后的唯一成果
public class DialogNode
{
    // 是否是对面说话？
    public bool isOtherSpeak = false;
    // 显示用的内容
    public string content;
    public DialogNode next;
    // 如果有，那么nextDialog即便是有也不生效，取而代之显示这些选项
    public List<Choice> choices;
}

public class Choice
{
    public string choiceHint;
    // 下一段对话
    public DialogNode nextDialog;
}

// 想要执行，就得创建一个这个实例
public class DialogInterpreter
{
    private DialogData dialog;
    private DialogNode currentNode;

    /// <summary>
    /// 对话是否已结束（currentNode 为 null）。
    /// 调用方用它区分"正常结束（关面板）"和"选择节点（显示选项）"，
    /// 避免只靠 PlayCurrent 返回的 dialogtext==null 判断而被选择节点误导。
    /// </summary>
    public bool IsEnd { get { return currentNode == null; } }

    public DialogInterpreter(DialogData dialogData)
    {
        if (dialogData == null)
        {
            Log.Error("DialogInterpreter：构造时传入的 dialogData 为 null！");
            dialog = null;
            currentNode = null;
            return;
        }

        if (dialogData.dialogNodes == null || dialogData.dialogNodes.Count == 0)
        {
            Log.Error($"DialogInterpreter：dialogData('{dialogData.sessionName}') 的 dialogNodes 为空（未经过 CompileDialog？）");
        }

        if (dialogData.entryNode == null)
        {
            Log.Error($"DialogInterpreter：dialogData('{dialogData.sessionName}') 缺少入口节点 entryNode（未经过 CompileDialog？）");
            dialog = dialogData;
            currentNode = null;
            return;
        }

        dialog = dialogData;
        currentNode = dialogData.entryNode;
    }

    /// <summary>
    /// 取当前要播放的内容。
    /// 返回值语义：
    ///   - 普通文本节点：dialogtext 有值，choices 为 null
    ///   - 选择节点：dialogtext 为 null，choices 非空（调用方应展示选项并等待 SelectChoice）
    ///   - 对话结束：三项全为 null/false（等价于 IsEnd == true）
    /// </summary>
    public (string dialogtext, bool isOtherSpeak, List<Choice> choices) PlayCurrent()
    {
        // 没节点了：正常结束
        if (currentNode == null) return (null, false, null);

        // 选择节点：choices 非空
        if (currentNode.choices != null && currentNode.choices.Count > 0)
        {
            return (null, false, currentNode.choices);
        }

        // 非预期：choices 存在但为空列表（编译期已保证至少一个选项，说明数据被改坏）
        if (currentNode.choices != null)
        {
            Log.Error($"PlayCurrent：遇到选项列表为空的 choice 节点（content='{currentNode.content ?? ""}'），按对话结束处理");
            currentNode = null;
            return (null, false, null);
        }

        // 非预期：文本节点content为null
        if (currentNode.content == null)
        {
            Log.Error("PlayCurrent：文本节点 content 为 null（数据损坏），按对话结束处理");
            currentNode = null;
            return (null, false, null);
        }

        string res = currentNode.content;
        bool isOtherSpeak = currentNode.isOtherSpeak;
        currentNode = currentNode.next; // 走到链尾则 currentNode 为 null，下次调用即"结束"
        return (res, isOtherSpeak, null);
    }

    // 选择某个选项后，调用这个让解释程序知道下一个该播放谁
    public void SelectChoice(Choice choice)
    {
        if (choice == null)
        {
            Log.Error("SelectChoice：传入的 choice为null！");
            return;
        }

        if (currentNode == null)
        {
            Log.Error("SelectChoice：对话已结束，却仍然调用SelectChoice！");
            return;
        }

        if (currentNode.choices == null || currentNode.choices.Count == 0)
        {
            Log.Error($"SelectChoice：当前节点并非选择节点（content='{currentNode.content ?? ""}'），这不该发生！");
            return;
        }

        var res = currentNode.choices.Find(element => element == choice);
        if (res == null)
        {
            Log.Error($"SelectChoice：当前选项列表里找不到该选项（hint='{choice.choiceHint ?? ""}'）检查传入的是否是 PlayCurrent 返回的同一引用！");
            return;
        }

        if (res.nextDialog == null)
        {
            Log.Error($"SelectChoice：选项『{res.choiceHint}』的目标节点为 null（数据损坏），按对话结束处理");
            currentNode = null;
            return;
        }

        currentNode = res.nextDialog;
    }
}
