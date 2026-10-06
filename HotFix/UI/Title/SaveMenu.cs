using System;
using System.Collections.Generic;
using PKSv;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveMenu : UIBase
{
    private const int SlotCount = 5;

    // 每次读档最多显示的衣服图标数，防止整行溢出
    private const int MaxClothIcons = 8;

    private Button btnClose;
    private Button btnExport;
    private Button btnBack;

    private VisualElement root;
    private VisualElement panel;
    private VisualElement scrim;
    private VisualElement header;
    private VisualElement footer;
    private VisualElement[] floaters;

    private VisualElement slotList;
    private Label metaTotalSlots;

    private const float Duration = 0.88f;
    private const float StartDropPx = 1000f;

    private bool animating;
    private float animTime;
    private System.Action tick;

    public SaveMenu() : base("SaveMenu", isUTK: true)
    {
        root = document.rootVisualElement;

        btnClose = root.Q<Button>("btn-close");
        btnExport = root.Q<Button>("btn-export");
        btnBack = root.Q<Button>("btn-back");

        btnClose.clicked += OnCloseClicked;
        btnExport.clicked += OnNewSlotClicked;
        btnBack.clicked += OnBackClicked;

        panel = root.Q<VisualElement>("menu-panel");
        scrim = root.Q<VisualElement>("scrim");
        header = root.Q<VisualElement>("panel-header");
        footer = root.Q<VisualElement>("panel-footer");

        slotList = root.Q<VisualElement>("slot-list");
        metaTotalSlots = root.Q<Label>("meta-total-slots");

        floaters = new[]
        {
            root.Q<VisualElement>("float-a"),
            root.Q<VisualElement>("float-b"),
            root.Q<VisualElement>("float-c"),
            root.Q<VisualElement>("float-d"),
        };

        BuildSlots();
    }

    // 卡槽从SvMgr读盘，逐个渲染
    public void BuildSlots()
    {
        slotList.Clear();

        try
        {
            int used = 0;

            for (int i = 0; i < SlotCount; i++)
            {
                SaveData data = SvMgr.Read(i);

                if (data == null)
                {
                    slotList.Add(BuildEmptySlot(i));
                }
                else
                {
                    used++;
                    slotList.Add(BuildLoadedSlot(i, data));
                }
            }

            if (metaTotalSlots != null)
                metaTotalSlots.text = $"已用 {used} / {SlotCount}";
        }
        catch (Exception e)
        {
            new MessageBoxOk().ShowMsg("读取到了损坏的存档" + e.ToString() + "，无法继续读档！请尝试清理游戏数据后重进游戏！");
        }
    }

    // 有存档
    private VisualElement BuildLoadedSlot(int slotIndex, SaveData data)
    {
        var card = NewCardBase(slotIndex, "slot-card--active");

        card.Add(BuildThumbCaption($"存档 {slotIndex + 1}"));

        var body = new VisualElement();
        body.AddToClassList("slot-body");

        // 第一行：世界名 + 角色名
        var top = new VisualElement();
        top.AddToClassList("slot-body__top");

        var nameBox = new VisualElement();
        nameBox.AddToClassList("slot-body__name");

        var index = new Label(NonEmpty(data.worldName, "未命名世界"));
        index.AddToClassList("slot-index");
        nameBox.Add(index);

        var charName = new Label(NonEmpty(data.playerName, "未命名角色"));
        charName.AddToClassList("char-name");
        nameBox.Add(charName);
        top.Add(nameBox);

        body.Add(top);

        // 第二行：统计
        var stats = new VisualElement();
        stats.AddToClassList("stat-row");
        stats.Add(NewStat("stat__icon--money", "物品", $"{CountItems(data)} 件"));
        stats.Add(NewStat("stat__icon--heart", "穿着", $"{ClothCount(data)} 件"));
        body.Add(stats);

        // 第三行：穿的服装图标 + 保存时间
        var info = new VisualElement();
        info.AddToClassList("slot-info-row");
        info.Add(BuildClothIcons(data));
        info.Add(NewText(FileTimeOf(slotIndex), "slot-timestamp"));
        body.Add(info);

        card.Add(body);

        // 右侧操作列（.slot-card 是横向布局，这是 thumb / body 之外的第三列）
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        // 捕获当前槽位和它的 SaveData，点击时一起交给外部
        int capturedSlot = slotIndex;
        SaveData capturedData = data;

        var startButton = new UTKAcceptButton { text = "开始" };
        startButton.AddToClassList("btn");
        startButton.AddToClassList("btn--sky");
        startButton.clicked += () => OnStartGameClicked(capturedSlot, capturedData);
        actions.Add(startButton);

        var deleteButton = new UTKCancelButton { text = "删除" };
        deleteButton.AddToClassList("btn");
        deleteButton.AddToClassList("btn--danger");
        deleteButton.clicked += () => OnDeleteSlotClicked(capturedSlot, capturedData);
        actions.Add(deleteButton);

        card.Add(actions);

        return card;
    }

    // 空存档
    private VisualElement BuildEmptySlot(int slotIndex)
    {
        var card = NewCardBase(slotIndex, "slot-card--empty");

        var thumb = new VisualElement();
        thumb.AddToClassList("slot-thumb");

        var holder = new VisualElement();
        holder.AddToClassList("thumb-placeholder");

        var text = new Label("空存档位");
        text.AddToClassList("thumb-placeholder__text");
        holder.Add(text);

        thumb.Add(holder);
        card.Add(thumb);

        var body = new VisualElement();
        body.AddToClassList("slot-body");

        var nameBox = new VisualElement();
        nameBox.AddToClassList("slot-body__name");

        var index = new Label($"SLOT {slotIndex + 1:00}");
        index.AddToClassList("slot-index");
        nameBox.Add(index);

        var title = new Label("尚未使用");
        title.AddToClassList("char-name");
        nameBox.Add(title);
        body.Add(nameBox);

        body.Add(NewText("这个位置还没有存档，点右下角「新建存档」开始。", "slot-timestamp"));

        card.Add(body);
        return card;
    }

    // 卡牌骨架
    private static VisualElement NewCardBase(int slotIndex, string stateClass)
    {
        var card = new VisualElement();
        card.AddToClassList("slot-card");
        card.AddToClassList(stateClass);
        card.AddToClassList($"slot-card--slot{slotIndex % 3 + 1}");
        return card;
    }

    private static VisualElement BuildThumbCaption(string place)
    {
        var thumb = new VisualElement();
        thumb.AddToClassList("slot-thumb");

        var caption = new VisualElement();
        caption.AddToClassList("thumb-caption");

        var pin = new VisualElement();
        pin.AddToClassList("thumb-pin");
        caption.Add(pin);

        var label = new Label(place);
        label.AddToClassList("thumb-place");
        caption.Add(label);

        thumb.Add(caption);
        return thumb;
    }

    // 已穿衣服的图标（可能为空，返回一个空容器）
    private static VisualElement BuildClothIcons(SaveData data)
    {
        var row = new VisualElement();
        row.AddToClassList("slot-cloth-row");

        CharacterEquipCollection e = data.equipedCloth;
        int shown = 0;

        AddClothIcon(row, e?.Hair,    ref shown);
        AddClothIcon(row, e?.Glasses, ref shown);
        AddClothIcon(row, e?.Earring, ref shown);
        AddClothIcon(row, e?.Upper,   ref shown);
        AddClothIcon(row, e?.Hands,   ref shown);
        AddClothIcon(row, e?.Bottom,  ref shown);
        AddClothIcon(row, e?.Face,    ref shown);
        AddClothIcon(row, e?.Stock,   ref shown);
        AddClothIcon(row, e?.Shoes,   ref shown);

        if (shown == 0)
            row.Add(NewText("没有穿着任何服装", "slot-cloth-empty"));

        return row;
    }

    private static void AddClothIcon(VisualElement row, ClothBase cloth, ref int shown)
    {
        if (cloth == null || shown >= MaxClothIcons) return;

        var icon = new VisualElement();
        icon.AddToClassList("slot-cloth-icon");
        icon.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>(cloth.iconPath));
        row.Add(icon);

        shown++;
    }

    private static Label NewText(string text, string className)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private static Label NewBadge(string text, string tone)
    {
        var badge = new Label(text);
        badge.AddToClassList("badge");
        badge.AddToClassList($"badge--{tone}");
        return badge;
    }

    private static VisualElement NewStat(string iconClass, string label, string value)
    {
        var stat = new VisualElement();
        stat.AddToClassList("stat");

        var icon = new VisualElement();
        icon.AddToClassList("stat__icon");
        icon.AddToClassList(iconClass);
        stat.Add(icon);

        stat.Add(NewText(label, "stat__label"));
        stat.Add(NewText(value, "stat__value"));

        return stat;
    }

    private static string NonEmpty(string value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;

    private static int ClothCount(SaveData data)
    {
        CharacterEquipCollection e = data.equipedCloth;
        if (e == null) return 0;

        int count = 0;
        if (e.Hair    != null) count++;
        if (e.Glasses != null) count++;
        if (e.Earring != null) count++;
        if (e.Upper   != null) count++;
        if (e.Hands   != null) count++;
        if (e.Bottom  != null) count++;
        if (e.Face    != null) count++;
        if (e.Stock   != null) count++;
        if (e.Shoes   != null) count++;
        return count;
    }

    private static int CountItems(SaveData data)
    {
        int total = data.bagpackItems?.Count ?? 0;
        total += data.handItems?.Count ?? 0;
        if (data.equipedBag != null) total++;
        if (data.currentEquipedTool != null) total++;
        return total;
    }

    private static string FileTimeOf(int slotIndex)
    {
        string full = PKSv.Path.GetSavePath(SvMgr.svFileNames[slotIndex]);

        if (!System.IO.File.Exists(full)) return "—";

        return System.IO.File.GetLastWriteTime(full).ToString("yyyy/MM/dd HH:mm");
    }

    // 点了存档后干什么
    private void OnStartGameClicked(int slotIndex, SaveData saveData)
    {
        new SelectGameModePanel(saveData).Show();
    }

    // 点了某个存档槽的「删除」之后干什么
    private void OnDeleteSlotClicked(int slotIndex, SaveData saveData)
    {
        new MessageBoxOKCancel().ShowOkCancel(() =>
        {
            SvMgr.Delete(slotIndex);
            // 删除后立刻刷新
            BuildSlots();
        }, () => { }, "你确定要删除这个存档吗？");
    }

    // 入场动画
    public override void Show()
    {
        base.Show();
        BuildSlots();
        PlayEnter();
    }

    private void PlayEnter()
    {
        StopTick();

        animating = true;
        animTime = 0f;
        Apply(0f);

        tick = Tick;
        Context.updateProxy.RegisterNewTask(tick, Duration + 0.1f);
    }

    private void Tick()
    {
        if (!animating) return;

        animTime += Time.unscaledDeltaTime;
        Apply(Mathf.Clamp01(animTime / Duration));

        if (animTime >= Duration)
        {
            Apply(1f);
            ClearInline();
            StopTick();
        }
    }

    private void StopTick()
    {
        animating = false;

        if (tick != null)
        {
            Context.updateProxy.DestoryTask(tick);
            tick = null;
        }
    }

    private void Apply(float p)
    {
        if (panel != null)
        {
            panel.style.translate = new Translate(0f, Mathf.Lerp(StartDropPx, 0f, Ease(0f, 0.50f, p)));
            float scale = Mathf.Lerp(0.74f, 1f, Ease(0f, 0.58f, p));
            panel.style.scale = new Scale(new Vector2(scale, scale));
            panel.style.rotate = new Rotate(new Angle(Mathf.Lerp(-3.2f, 0f, Ease(0f, 0.62f, p)), AngleUnit.Degree));
            panel.style.opacity = Mathf.Lerp(0f, 1f, Ease(0f, 0.20f, p));
        }

        if (scrim != null)
        {
            scrim.style.opacity = Mathf.Lerp(0f, 1f, Ease(0f, 0.34f, p));
        }

        if (header != null)
        {
            header.style.translate = new Translate(0f, Mathf.Lerp(-28f, 0f, Ease(0.46f, 0.78f, p)));
            header.style.opacity = Mathf.Lerp(0f, 1f, Ease(0.46f, 0.70f, p));
        }

        if (footer != null)
        {
            footer.style.translate = new Translate(0f, Mathf.Lerp(32f, 0f, Ease(0.54f, 0.84f, p)));
            footer.style.opacity = Mathf.Lerp(0f, 1f, Ease(0.54f, 0.78f, p));
        }

        if (floaters != null)
        {
            for (int i = 0; i < floaters.Length; i++)
            {
                if (floaters[i] == null) continue;

                float from = 0.34f + i * 0.05f;
                floaters[i].style.translate = new Translate(0f, Mathf.Lerp(52f, 0f, Ease(from, from + 0.26f, p)));
                floaters[i].style.opacity = Mathf.Lerp(0f, 1f, Ease(from, from + 0.22f, p));
            }
        }
    }

    private void ClearInline()
    {
        Clear(panel);
        Clear(scrim);
        Clear(header);
        Clear(footer);

        if (floaters != null)
        {
            foreach (var f in floaters) Clear(f);
        }
    }

    private static void Clear(VisualElement e)
    {
        if (e == null) return;

        e.style.translate = StyleKeyword.Null;
        e.style.scale = StyleKeyword.Null;
        e.style.rotate = StyleKeyword.Null;
        e.style.opacity = StyleKeyword.Null;
        e.style.left = StyleKeyword.Null;
    }

    private static float Ease(float from, float to, float p)
    {
        if (to <= from) return p >= to ? 1f : 0f;

        float x = Mathf.Clamp01((p - from) / (to - from));
        return 1f - (1f - x) * (1f - x);
    }

    public override void Destroy()
    {
        StopTick();
        base.Destroy();
    }

    private void OnCloseClicked()
    {
        StopTick();
        Hide();
    }

    private void OnNewSlotClicked()
    {
        if (PKSv.SvMgr.FindEmptySlot() == -1)
        {
            new PromptMessage("没有可用存档插槽了！请尝试删除一些后再试。").Show();
            return;
        }
        new NewSavePanel(this).Show();
    }

    private void OnBackClicked()
    {
        StopTick();
        Hide();
    }
}