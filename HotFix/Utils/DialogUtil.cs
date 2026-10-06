using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using PK;

public static class DialogUtil
{
    /// <summary>
    /// 编译Lua对话脚本。
    ///
    /// Lua 文件约定（函数调用式）：
    ///   function dialog()
    ///       local choice = { type = 1, choice = { {"有病", "label2"}, {"你有中二病吗", "label3"} } }
    ///       return {
    ///           sessionName = "对话名称",
    ///           label1 = { texts = {
    ///               { speaker = 0, text = "你好" },           -- text 也可以是字符串数组
    ///               { speaker = 1, text = {"???", "你怎么回事？"} },
    ///               choice,                                   -- 选项条目（必须含 type 和 choice）
    ///           } },
    ///           label2 = { texts = { ... } },
    ///       }
    ///   end
    ///
    /// 注意：dialog 必须是【全局】函数（不能是 local function），C# 侧会自己找到它并调用，
    /// 不需要在文件末尾 return dialog()。
    ///
    /// 规则（违规一律抛异常）：
    /// - 根表必须有 sessionName（string）
    /// - label1 是强制入口
    /// - 每个 label 必须有 texts，且为从 1 连续编号的非空数组
    /// - 每个 texts 成员必须是 table：
    ///     * 无 type  → 普通文本：speaker（0=对面,1=我，数字）和 text（string 或 string 数组）必填
    ///     * 有 type  → 选项：type 必须为 1，choice 必须为 {提示词, 目标label} 数组
    /// - 所有选项目的 label 必须真实存在
    /// - Lua 语法/运行错误由MoonSharp抛出
    /// </summary>
    public static DialogData CompileDialog(string luaCode)
    {
        if (string.IsNullOrEmpty(luaCode))
            throw new ArgumentException("Lua 代码不能为空");

        if (Context.luaEnv == null)
            throw new InvalidOperationException("Context.luaEnv 未初始化");

        // 执行 Lua：语法/运行错误直接抛出，不捕获、不掩盖
        Context.luaEnv.env.DoString(luaCode);

        // 找到全局函数 dialog() 并调用，返回值即根表。
        // local function 在这里拿不到（chunk 执行完局部符号就没了），会抛异常。
        DynValue dialogFuncValue = Context.luaEnv.env.Globals.Get("dialog");
        if (dialogFuncValue.IsNil() || dialogFuncValue.Type != DataType.Function)
            throw new ArgumentException("Lua 脚本必须定义全局函数 dialog()（注意：不能是 local function）");

        DynValue result = Context.luaEnv.env.Call(dialogFuncValue);

        if (result.Type != DataType.Table)
            throw new ArgumentException($"dialog() 必须 return 一个 table（根表），实际返回 {result.Type}");

        Table root = result.Table;

        // ---- 必填sessionName ----
        DynValue sessionNameValue = root.Get("sessionName");
        if (sessionNameValue.IsNil())
            throw new ArgumentException("根表缺少 'sessionName' 字段");
        if (sessionNameValue.Type != DataType.String)
            throw new ArgumentException($"'sessionName' 必须是 string，实际是 {sessionNameValue.Type}");
        string sessionName = sessionNameValue.String;

        // ---- 强制入口：label1 ----
        DynValue label1Value = root.Get("label1");
        if (label1Value.IsNil())
            throw new ArgumentException("根表必须包含 'label1'（对话入口）");

        Dictionary<string, List<DialogNode>> allNodes = new Dictionary<string, List<DialogNode>>();
        Dictionary<string, DialogNode> entryNodes = new Dictionary<string, DialogNode>();
        // 临时存储 Choice -> 目标 label（第二阶段再绑定为节点）
        Dictionary<Choice, string> choiceGotoMap = new Dictionary<Choice, string>();

        // ---- 遍历根表所有 label ----
        foreach (TablePair pair in root.Pairs)
        {
            if (pair.Key.Type != DataType.String)
                throw new ArgumentException($"根表的 key 必须是 string，发现 {pair.Key.Type} 类型的 key");
            string label = pair.Key.String;

            if (label == "sessionName")
                continue;

            if (pair.Value.Type != DataType.Table)
                throw new ArgumentException($"label '{label}' 必须是 table，实际是 {pair.Value.Type}");
            Table labelTable = pair.Value.Table;

            // texts 必填且为数组
            DynValue textsValue = labelTable.Get("texts");
            if (textsValue.IsNil() || textsValue.Type != DataType.Table)
                throw new ArgumentException($"label '{label}' 缺少 'texts' 字段（或不是数组）");
            Table textsTable = textsValue.Table;

            int textCount = ValidateArray(textsTable, $"label '{label}' 的 texts");
            if (textCount == 0)
                throw new ArgumentException($"label '{label}' 的 texts 不能为空数组");

            List<DialogNode> nodeList = new List<DialogNode>();
            DialogNode prevNode = null;

            for (int i = 1; i <= textCount; i++)
            {
                DynValue itemValue = textsTable.Get(i);
                if (itemValue.Type != DataType.Table)
                    throw new ArgumentException($"label '{label}' 的 texts[{i}] 必须是 table，实际是 {itemValue.Type}");
                Table item = itemValue.Table;

                DynValue typeValue = item.Get("type");

                if (typeValue.IsNil())
                {
                    // ---- 普通文本条目：speaker + text 必填 ----
                    DynValue speakerValue = item.Get("speaker");
                    if (speakerValue.IsNil())
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 缺少 'speaker' 字段");
                    if (speakerValue.Type != DataType.Number)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 的 'speaker' 必须是数字，实际是 {speakerValue.Type}");
                    int speaker = (int)speakerValue.Number;
                    if (speaker != 0 && speaker != 1)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 的 'speaker' 只能是 0（对面）或 1（我），实际是 {speaker}");

                    DynValue textValue = item.Get("text");
                    if (textValue.IsNil())
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 缺少 'text' 字段");

                    // text：允许单个 string，或 string 数组
                    List<string> lines = new List<string>();
                    if (textValue.Type == DataType.String)
                    {
                        lines.Add(textValue.String);
                    }
                    else if (textValue.Type == DataType.Table)
                    {
                        int lineCount = ValidateArray(textValue.Table, $"label '{label}' 的 texts[{i}].text");
                        if (lineCount == 0)
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].text 不能为空数组");
                        for (int j = 1; j <= lineCount; j++)
                        {
                            DynValue lineValue = textValue.Table.Get(j);
                            if (lineValue.Type != DataType.String)
                                throw new ArgumentException($"label '{label}' 的 texts[{i}].text[{j}] 必须是 string，实际是 {lineValue.Type}");
                            lines.Add(lineValue.String);
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"label '{label}' 的 texts[{i}].text 必须是 string 或 string 数组，实际是 {textValue.Type}");
                    }

                    foreach (string line in lines)
                    {
                        DialogNode node = new DialogNode();
                        // 0=对面(对方)，1=我(玩家) —— 以你本条消息的规格为准
                        node.isOtherSpeak = (speaker == 0);
                        node.content = line;
                        node.choices = null;

                        if (prevNode != null)
                            prevNode.next = node;

                        nodeList.Add(node);
                        prevNode = node;
                    }
                }
                else
                {
                    // ---- 选项条目：type 必填且为 1，choice 必填 ----
                    if (typeValue.Type != DataType.Number)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 的 'type' 必须是数字，实际是 {typeValue.Type}");
                    int type = (int)typeValue.Number;
                    if (type != 1)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}] 的 type={type} 暂不支持（当前只支持 type=1）");

                    DynValue choiceValue = item.Get("choice");
                    if (choiceValue.IsNil() || choiceValue.Type != DataType.Table)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}]（type=1 选项）缺少 'choice' 数组");
                    Table choiceTable = choiceValue.Table;

                    int choiceCount = ValidateArray(choiceTable, $"label '{label}' 的 texts[{i}].choice");
                    if (choiceCount == 0)
                        throw new ArgumentException($"label '{label}' 的 texts[{i}].choice 不能为空数组");

                    DialogNode choiceNode = new DialogNode();
                    choiceNode.isOtherSpeak = false;
                    choiceNode.content = "";
                    choiceNode.choices = new List<Choice>();

                    for (int j = 1; j <= choiceCount; j++)
                    {
                        DynValue choiceItemValue = choiceTable.Get(j);
                        if (choiceItemValue.Type != DataType.Table)
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}] 必须是 table，实际是 {choiceItemValue.Type}");
                        Table choiceItem = choiceItemValue.Table;

                        int pairCount = ValidateArray(choiceItem, $"label '{label}' 的 texts[{i}].choice[{j}]");
                        if (pairCount != 2)
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}] 必须正好是 [提示词, 目标label] 两个元素，实际 {pairCount} 个");

                        DynValue hintValue = choiceItem.Get(1);
                        DynValue targetValue = choiceItem.Get(2);

                        if (hintValue.Type != DataType.String)
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}][1]（提示词）必须是 string，实际是 {hintValue.Type}");
                        if (targetValue.Type != DataType.String)
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}][2]（目标 label）必须是 string，实际是 {targetValue.Type}");

                        if (string.IsNullOrEmpty(hintValue.String))
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}][1]（提示词）不能为空字符串");
                        if (string.IsNullOrEmpty(targetValue.String))
                            throw new ArgumentException($"label '{label}' 的 texts[{i}].choice[{j}][2]（目标 label）不能为空字符串");

                        Choice choice = new Choice();
                        choice.choiceHint = hintValue.String;
                        choiceGotoMap[choice] = targetValue.String;
                        choiceNode.choices.Add(choice);
                    }

                    if (prevNode != null)
                        prevNode.next = choiceNode;

                    nodeList.Add(choiceNode);
                    prevNode = choiceNode;
                }
            }

            allNodes[label] = nodeList;
            entryNodes[label] = nodeList[0]; // 每个 label 的第一个节点是入口
        }

        // ---- 第二阶段：校验所有选项目的 label 存在并绑定为节点 ----
        foreach (KeyValuePair<string, List<DialogNode>> kv in allNodes)
        {
            foreach (DialogNode node in kv.Value)
            {
                if (node.choices == null)
                    continue;

                foreach (Choice choice in node.choices)
                {
                    if (!choiceGotoMap.TryGetValue(choice, out string targetLabel))
                        throw new InvalidOperationException($"内部错误：choice '{choice.choiceHint}'（label '{kv.Key}'）缺少目标映射");

                    if (!entryNodes.TryGetValue(targetLabel, out DialogNode targetNode))
                        throw new ArgumentException($"choice '{choice.choiceHint}'（label '{kv.Key}'）指向不存在的 label '{targetLabel}'");

                    choice.nextDialog = targetNode;
                }
            }
        }

        // ---- 组装 ----
        DialogData data = new DialogData();
        data.sessionName = sessionName;
        data.dialogNodes = allNodes;
        data.entryLabel = "label1";   // 强制入口
        data.entryNode = entryNodes["label1"];

        Log.Info("编译Lua脚本成功！");
        return data;
    }

    /// <summary>
    /// 校验 Lua 表是"从 1 连续编号的数组"，返回元素个数。
    /// 出现空洞、乱序、非数字 key、非整数 key 一律抛异常（严禁静默跳过）。
    /// </summary>
    static int ValidateArray(Table t, string context)
    {
        List<int> keys = new List<int>();

        foreach (TablePair pair in t.Pairs)
        {
            if (pair.Key.Type == DataType.Number)
            {
                double d = pair.Key.Number;
                if (d != Math.Floor(d) || d < 1)
                    throw new ArgumentException($"{context}：数组下标必须是正整数，发现 {d}");
                keys.Add((int)d);
            }
            else
            {
                throw new ArgumentException($"{context}：数组不允许非数字 key（发现 '{pair.Key.CastToString()}'）");
            }
        }

        keys.Sort();
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] != i + 1)
                throw new ArgumentException($"{context}：数组下标必须从 1 连续编号，当前出现空洞");
        }

        return keys.Count;
    }
}
