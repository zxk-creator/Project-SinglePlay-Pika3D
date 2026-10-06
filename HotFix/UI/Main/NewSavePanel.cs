using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

// UnityEngine 和 UnityEngine.UIElements 里都有 PointerType，
// 两边都被 using 了，所以起个别名消歧义
using UiePointerType = UnityEngine.UIElements.PointerType;

public class NewSavePanel : UIBase
{
    private sealed class Section
    {
        public string Name;
        public EClothType Slot;
    }

    private static readonly Section[] Sections =
    {
        new Section { Name = "头发", Slot = EClothType.HAIR },
        new Section { Name = "脸部", Slot = EClothType.FACE },
        new Section { Name = "上衣", Slot = EClothType.UPPER },
        new Section { Name = "手部", Slot = EClothType.HAND },
        new Section { Name = "下装", Slot = EClothType.BOTTOM },
        new Section { Name = "袜子", Slot = EClothType.STOCKING },
        new Section { Name = "鞋子", Slot = EClothType.SHOE },
        new Section { Name = "眼镜", Slot = EClothType.GLASSES },
        new Section { Name = "耳饰", Slot = EClothType.EARRING },
    };

    private sealed class Anim
    {
        public VisualElement El;
        public float FromY;
        public float Start;
        public float Duration;
    }

    private const string RenderTextureAddress = "ChangeAvatarRT";
    private const float IntroDuration = 0.75f;
    private const float VerticalWheelStep = 3f;
    private const float MinThumbHeight = 36f;

    // 触摸拖内容区时的起拖阈值，避免点一下图标就变成拖拽
    private const float DragThreshold = 8f;

    // 男/女各记一份已选外观。初始为空 → 打开时不高亮任何图标。
    private readonly Dictionary<bool, Dictionary<EClothType, ClothBase>> picked
        = new Dictionary<bool, Dictionary<EClothType, ClothBase>>
        {
            { true,  new Dictionary<EClothType, ClothBase>() },
            { false, new Dictionary<EClothType, ClothBase>() },
        };

    private readonly Dictionary<EClothType, VisualElement> wornIcons
        = new Dictionary<EClothType, VisualElement>();

    private readonly List<Anim> anims = new List<Anim>();

    private VisualElement root;
    private VisualElement closetList;
    private VisualElement previewRt;

    private VisualElement closetViewport;
    private VisualElement closetContent;
    private VisualElement closetThumb;
    private VisualElement closetTrack;
    private float closetOffset;

    private UTKAcceptButton btnGirl;
    private UTKAcceptButton btnBoy;

    private ChangeAvatarCharacter preview;
    private AsyncOperationHandle<RenderTexture> rtHandle;

    private bool isGirl = true;
    private bool introPlaying;
    private float introTime;
    private System.Action tick;

    /// <summary>玩家输入的角色名，随输入实时更新</summary>
    public string playerName = string.Empty;

    /// <summary>玩家输入的世界名，随输入实时更新</summary>
    public string worldName = string.Empty;

    /// <summary>
    /// 玩家当前选中的每个部位的衣服 ID（按 Sections 的顺序，一个部位至多一个）。
    /// 每次点击服装后立即重建，切性别会切换成那个性别的选择结果。
    /// </summary>
    public readonly List<int> selectedClothIds = new List<int>();

    // 旋转拖动条
    private VisualElement rotateTrack;
    private VisualElement rotateThumb;
    private float rotation;        // 已施加的角度，0-360
    private float initYaw;         // 角色默认朝向的 Y 角，作为 0 度基准
    private bool hasInitYaw;
    private SaveMenu saveMenu;

    public NewSavePanel(SaveMenu saveMenu) : base("AvatarSelector", isUTK: true)
    {
        root = document.rootVisualElement;
        this.saveMenu = saveMenu;

        closetList = root.Q<VisualElement>("closet-list");
        previewRt = root.Q<VisualElement>("preview-rt");

        MakeClosetScrollable(
            root.Q<VisualElement>("closet-scroll"),
            root.Q<VisualElement>("closet-content"),
            root.Q<VisualElement>("closet-track"),
            root.Q<VisualElement>("closet-thumb"));

        btnGirl = root.Q<UTKAcceptButton>("btn-girl");
        btnBoy = root.Q<UTKAcceptButton>("btn-boy");

        btnGirl.clicked += () => SetGender(true);
        btnBoy.clicked += () => SetGender(false);
        root.Q<Button>("btn-done").clicked += OnDoneClicked;
        root.Q<Button>("btn-back").clicked += OnBackClicked;

        preview = Object.FindFirstObjectByType<ChangeAvatarCharacter>();

        if (preview == null)
        {
            PK.Log.Error("AvatarSelector: 场景里找不到 ChangeAvatarCharacter");
            return;
        }

        // 面板的性别必须跟场景里 ChangeAvatarPlayer 的实际性别一致，
        // 否则衣柜列的是女装、预览却是男角色 —— 首帧就已经对不上。
        isGirl = preview.IsGirl;

        MakeRotationSlider();

        LoadRenderTexture();
        ApplyGenderButtons();
        BuildCloset();
    }

    // 拖动块：在轨道范围内拖动，把 0-360 映射成角色的 Y 旋转
    private void MakeRotationSlider()
    {
        rotateTrack = root.Q<VisualElement>("preview-rotate");
        rotateThumb = root.Q<VisualElement>("rotate-thumb");

        rotateTrack.AddManipulator(new ThumbDragManipulator(
            useXAxis: true, PointerKind.Any, OnRotateDragged));

        rotateTrack.RegisterCallback<GeometryChangedEvent>(_ => RefreshRotateThumb());
    }

    private void OnRotateDragged(float dragPixels)
    {
        // 第一次拖动时才记基准朝向，避免构造函数阶段读到还没建好的骨架旋转
        if (!hasInitYaw)
        {
            hasInitYaw = true;
            initYaw = preview.transform.eulerAngles.y;
        }

        SetRotation(rotation + dragPixels * (360f / RotateTravel()));
    }

    private float RotateTravel()
    {
        return Mathf.Max(1f, rotateTrack.layout.width - rotateThumb.layout.width);
    }

    private void SetRotation(float degrees)
    {
        rotation = Mathf.Repeat(degrees, 360f);

        preview.SetCharacterRotation(initYaw + rotation);
        RefreshRotateThumb();
    }

    private void RefreshRotateThumb()
    {
        if (rotateTrack == null || rotateThumb == null) return;

        rotateThumb.style.left = Mathf.Lerp(0f, RotateTravel(), rotation / 360f);
    }

    private void LoadRenderTexture()
    {
        rtHandle = Addressables.LoadAssetAsync<RenderTexture>(RenderTextureAddress);
        rtHandle.WaitForCompletion();

        var rt = rtHandle.Result;
        previewRt.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));

        preview.renderTexture = rt;
        preview.renderView.targetTexture = rt;
    }

    private void SetGender(bool wantGirl)
    {
        if (isGirl == wantGirl) return;

        isGirl = wantGirl;

        preview.SetGender(wantGirl);

        ApplyGenderButtons();
        BuildCloset();

        // 男女各有一份选择记录，切性别后 ID 列表要跟着换
        RebuildSelectedIds();
    }

    private void ApplyGenderButtons()
    {
        btnGirl.EnableInClassList("gender-button--on", isGirl);
        btnBoy.EnableInClassList("gender-button--on", !isGirl);
    }

    private static List<ClothBase> GetSlotList(GenderEquipCollection g, EClothType slot)
    {
        switch (slot)
        {
            case EClothType.HAIR:     return g.hair;
            case EClothType.FACE:     return g.face;
            case EClothType.GLASSES:  return g.glasses;
            case EClothType.EARRING:  return g.earring;
            case EClothType.UPPER:    return g.upper;
            case EClothType.HAND:     return g.hand;
            case EClothType.BOTTOM:   return g.bottom;
            case EClothType.STOCKING: return g.stocking;
            case EClothType.SHOE:     return g.shoe;
            default:                  return null;
        }
    }

    // 角色名 / 世界名 输入区。
    // 放在滚动区内部的最上面，跟着衣柜一起滚 —— 所以每次重建衣柜都要重新加上，
    // 并且用 playerName / worldName 回填，避免切性别重建时把玩家已输入的内容清掉。
    private void BuildNamingBlock()
    {
        var block = new VisualElement();
        block.AddToClassList("naming-block");

        block.Add(BuildNamingField("角色名", playerName, value => playerName = value, 12));
        block.Add(BuildNamingField("世界名", worldName, value => worldName = value, 16));

        closetList.Add(block);
    }

    private static VisualElement BuildNamingField(string label, string initial,
                                                  System.Action<string> onChanged, int maxLength)
    {
        var row = new VisualElement();
        row.AddToClassList("naming-field");

        var caption = new Label(label);
        caption.AddToClassList("naming-label");
        row.Add(caption);

        var input = new TextField { value = initial ?? string.Empty, maxLength = maxLength };
        input.AddToClassList("naming-input");
        input.textEdition.placeholder = $"请输入{label}";

        input.RegisterValueChangedCallback(evt => onChanged(evt.newValue));

        row.Add(input);
        return row;
    }

    private void BuildCloset()
    {
        closetList.Clear();
        wornIcons.Clear();

        BuildNamingBlock();

        GenderEquipCollection gender = isGirl ? Context.Item.clothes.female : Context.Item.clothes.male;
        Dictionary<EClothType, ClothBase> chosen = picked[isGirl];

        foreach (Section section in Sections)
        {
            List<ClothBase> cloths = GetSlotList(gender, section.Slot);

            var block = new VisualElement();
            block.AddToClassList("closet-section");

            var head = new VisualElement();
            head.AddToClassList("section-head");

            var accent = new VisualElement();
            accent.AddToClassList("section-accent");
            head.Add(accent);

            var name = new Label(section.Name);
            name.AddToClassList("section-name");
            head.Add(name);

            var count = new Label(cloths.Count.ToString());
            count.AddToClassList("section-count");
            head.Add(count);

            block.Add(head);

            // 横向条：clip 裁剪 + row 位移，底部配一条和衣柜同款的横向滚动条
            var clip = new VisualElement();
            clip.AddToClassList("section-strip");

            var row = new VisualElement();
            row.AddToClassList("strip-row");

            if (cloths.Count == 0)
            {
                var empty = new Label("暂无");
                empty.AddToClassList("cloth-empty");
                row.Add(empty);
            }
            else
            {
                chosen.TryGetValue(section.Slot, out ClothBase worn);

                foreach (ClothBase cloth in cloths)
                {
                    row.Add(BuildClothIcon(section.Slot, cloth, worn));
                }
            }

            var hTrack = new VisualElement();
            hTrack.AddToClassList("strip-track");

            var hThumb = new VisualElement();
            hThumb.AddToClassList("strip-thumb");
            hTrack.Add(hThumb);

            new StripView(clip, row, hTrack, hThumb);

            clip.Add(row);
            block.Add(clip);
            block.Add(hTrack);
            closetList.Add(block);
        }
    }

    /// <summary>
    /// 一条横向条：clip 裁剪、row 位移、底部滚动条可拖。
    /// 位移是它自己的字段，不放在面板的字典里 ——
    /// 否则 BuildCloset 清空字典时，上一批条的回调若还活着（指针捕获期间）
    /// 就会读到不存在的键而抛 KeyNotFoundException。
    /// </summary>
    private sealed class StripView
    {
        private readonly VisualElement clip;
        private readonly VisualElement row;
        private readonly VisualElement track;
        private readonly VisualElement thumb;

        private float offset;

        public StripView(VisualElement parentClip, VisualElement contentRow,
                         VisualElement scrollTrack, VisualElement scrollThumb)
        {
            clip = parentClip;
            row = contentRow;
            track = scrollTrack;
            thumb = scrollThumb;

            // 拖滑块：鼠标和触摸都行，读 X 位移
            thumb.AddManipulator(
                new ThumbDragManipulator(useXAxis: true, PointerKind.Any, OnDragged));

            // 直接拖内容区：只认触摸（手指），鼠标不给拖，避免误拖又和点击抢事件
            clip.AddManipulator(
                new ThumbDragManipulator(useXAxis: true, PointerKind.Touch, OnDraggedContent, DragThreshold));

            // 内容尺寸确定后刷新滑块长度
            row.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        private float MaxOffset => Mathf.Max(0f, row.layout.width - clip.layout.width);

        private void OnDragged(float dragPixels)
        {
            float travel = Mathf.Max(1f, track.layout.width - thumb.layout.width);
            ScrollTo(offset + dragPixels * MaxOffset / travel);
        }

        // 手指拖内容：1:1 跟手，天然是"内容跟着手指走"的手感
        private void OnDraggedContent(float dragPixels)
        {
            ScrollTo(offset - dragPixels);
        }

        private void ScrollTo(float offset)
        {
            offset = Mathf.Clamp(offset, 0f, MaxOffset);
            row.style.translate = new Translate(-offset, 0f);
            Refresh();
        }

        private void Refresh()
        {
            float clipW = clip.layout.width;
            float rowW = row.layout.width;
            float trackW = track.layout.width;

            if (rowW <= clipW || rowW <= 0f || trackW <= 0f)
            {
                thumb.style.display = DisplayStyle.None;
                offset = 0f;
                row.style.translate = new Translate(0f, 0f);
                return;
            }

            thumb.style.display = DisplayStyle.Flex;

            float thumbW = Mathf.Max(MinThumbHeight, trackW * (clipW / rowW));
            thumb.style.width = thumbW;
            thumb.style.left = Mathf.Lerp(0f, trackW - thumbW, offset / (rowW - clipW));
        }
    }

    // 外层衣柜：手写纵向滚动。
    // 引擎的「纵向里嵌横向」嵌套滚动有已登记 bug，会跳变，所以整条链路自己实现：
    // 内容靠 translate 移动，右侧滚动条是自绘的，不依赖 ScrollView。
    private void MakeClosetScrollable(VisualElement viewport, VisualElement content,
                                      VisualElement track, VisualElement thumb)
    {
        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            ScrollCloset(evt.delta.y * VerticalWheelStep);
            evt.StopPropagation();
        });

        // 纵向滚动条：鼠标和触摸都能拖，读 Y 位移
        thumb.AddManipulator(new ThumbDragManipulator(useXAxis: false, PointerKind.Any, dragPixels =>
        {
            float trackH = track.layout.height;
            float thumbH = thumb.layout.height;
            float travel = Mathf.Max(1f, trackH - thumbH);

            // 滑块走的像素 -> 内容应滚动的像素
            float ratio = MaxClosetOffset() / travel;
            ScrollCloset(dragPixels * ratio);
        }));

        content.RegisterCallback<GeometryChangedEvent>(_ => UpdateClosetScrollbar());

        closetViewport = viewport;
        closetContent = content;
        closetThumb = thumb;
        closetTrack = track;
    }

    private void ScrollCloset(float delta)
    {
        closetOffset = Mathf.Clamp(closetOffset + delta, 0f, MaxClosetOffset());
        ApplyClosetScroll();
    }

    private float MaxClosetOffset()
    {
        return Mathf.Max(0f, closetContent.layout.height - closetViewport.layout.height);
    }

    private void ApplyClosetScroll()
    {
        closetContent.style.translate = new Translate(0f, -closetOffset);
        UpdateClosetScrollbar();
    }

    // 滑块尺寸/位置按 视口:内容 的比例算，与浏览器滚动条一个道理
    private void UpdateClosetScrollbar()
    {
        float view = closetViewport.layout.height;
        float content = closetContent.layout.height;
        float trackH = closetTrack.layout.height;

        if (content <= view || content <= 0f || trackH <= 0f)
        {
            closetThumb.style.display = DisplayStyle.None;
            closetOffset = 0f;
            closetContent.style.translate = new Translate(0f, 0f);
            return;
        }

        closetThumb.style.display = DisplayStyle.Flex;

        float thumbH = Mathf.Max(MinThumbHeight, trackH * (view / content));
        closetThumb.style.height = thumbH;
        closetThumb.style.top = Mathf.Lerp(0f, trackH - thumbH, closetOffset / (content - view));
    }

    /// <summary>
    /// 滚动条滑块拖拽。
    /// 必须告诉它读哪个轴：横向要读 X 位移，纵向读 Y 位移。
    /// 还要告诉它认哪种输入设备：触摸合成出来的 PointerDownEvent，
    /// button 不保证是 0，所以不能用 button 判断，得看 pointerType。
    /// threshold > 0 时要做拖拽判定，避免和"点击"抢事件。
    /// </summary>
    private enum PointerKind { Any, Touch }

    private sealed class ThumbDragManipulator : PointerManipulator
    {
        private readonly bool horizontal;
        private readonly PointerKind kind;
        private readonly System.Action<float> move;
        private readonly float threshold;

        private bool pressed;
        private bool dragging;
        private float lastPos;
        private float accumulated;

        public ThumbDragManipulator(bool useXAxis, PointerKind acceptedKind,
                                    System.Action<float> onMove, float dragThreshold = 0f)
        {
            horizontal = useXAxis;
            kind = acceptedKind;
            move = onMove;
            threshold = dragThreshold;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCancelEvent>(OnCancel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCancelEvent>(OnCancel);
        }

        private bool Accepts(IPointerEvent evt)
        {
            if (kind == PointerKind.Any) return true;
            return evt.pointerType == UiePointerType.touch;
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (!Accepts(evt)) return;
            if (evt.pointerType != UiePointerType.touch && evt.button != 0) return;

            pressed = true;
            dragging = threshold <= 0f;
            accumulated = 0f;
            lastPos = horizontal ? evt.position.x : evt.position.y;

            if (dragging) target.CapturePointer(evt.pointerId);
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (!pressed) return;

            float pos = horizontal ? evt.position.x : evt.position.y;
            float delta = pos - lastPos;
            lastPos = pos;

            if (!dragging)
            {
                accumulated += delta;

                if (Mathf.Abs(accumulated) < threshold) return;

                dragging = true;
                target.CapturePointer(evt.pointerId);
            }

            move(delta);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            EndDrag(evt.pointerId);
        }

        private void OnCancel(PointerCancelEvent evt)
        {
            EndDrag(evt.pointerId);
        }

        private void EndDrag(int pointerId)
        {
            pressed = false;

            if (!dragging) return;

            dragging = false;
            if (target.HasPointerCapture(pointerId))
            {
                target.ReleasePointer(pointerId);
            }
        }
    }

    private VisualElement BuildClothIcon(EClothType slot, ClothBase cloth, ClothBase worn)
    {
        var icon = new UTKAcceptButton { name = $"cloth-{cloth.id}" };
        icon.AddToClassList("cloth-item");
        icon.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>(cloth.iconPath));

        if (worn != null && worn.id == cloth.id)
        {
            icon.AddToClassList("cloth-item--worn");
            wornIcons[slot] = icon;
        }

        icon.clicked += () => Pick(slot, cloth, icon);
        return icon;
    }

    private void Pick(EClothType slot, ClothBase cloth, VisualElement icon)
    {
        if (wornIcons.TryGetValue(slot, out var previous))
        {
            previous.RemoveFromClassList("cloth-item--worn");
        }

        wornIcons[slot] = icon;
        icon.AddToClassList("cloth-item--worn");

        picked[isGirl][slot] = cloth;

        RebuildSelectedIds();

        preview.Wear(cloth);
    }

    // 按 Sections 的顺序收集当前性别下已选的衣服 ID。
    // 用 Sections 的顺序而不是字典遍历，保证每次结果顺序一致（便于外部对位比较）。
    private void RebuildSelectedIds()
    {
        selectedClothIds.Clear();

        Dictionary<EClothType, ClothBase> chosen = picked[isGirl];

        foreach (Section section in Sections)
        {
            if (chosen.TryGetValue(section.Slot, out ClothBase cloth) && cloth != null)
            {
                selectedClothIds.Add(cloth.id);
            }
        }
    }

    public override void Show()
    {
        base.Show();

        ResetToDefaultOutfit();
        PlayIntro();
    }

    // 每次进入选人界面：清掉预览角色身上的装备，重新应用默认着装
    private void ResetToDefaultOutfit()
    {
        preview.RestoreDefaultOutfit();

        // 上一次进来选过的衣服要忘掉，否则衣柜高亮的是旧选择
        picked[true].Clear();
        picked[false].Clear();

        ApplyGenderButtons();
        BuildCloset();
    }

    private void PlayIntro()
    {
        StopTick();

        anims.Clear();
        Collect(root.Q<VisualElement>("preview-pane"), 56f, 0f, 0.44f);
        Collect(root.Q<VisualElement>("closet-pane"), 72f, 0.10f, 0.52f);
        Collect(root.Q<VisualElement>("footer"), 40f, 0.22f, 0.46f);

        introPlaying = true;
        introTime = 0f;
        ApplyIntro();

        tick = Tick;
        Context.updateProxy.RegisterNewTask(tick, IntroDuration + 0.1f);
    }

    private void Collect(VisualElement el, float fromY, float start, float duration)
    {
        if (el == null) return;

        anims.Add(new Anim
        {
            El = el,
            FromY = fromY,
            Start = start,
            Duration = duration,
        });
    }

    private void Tick()
    {
        if (!introPlaying) return;

        introTime += Time.unscaledDeltaTime;
        ApplyIntro();

        if (introTime >= IntroDuration)
        {
            FinishIntro();
        }
    }

    private void ApplyIntro()
    {
        foreach (Anim a in anims)
        {
            float x = Mathf.Clamp01((introTime - a.Start) / a.Duration);
            float eased = 1f - (1f - x) * (1f - x);

            a.El.style.translate = new Translate(0f, Mathf.Lerp(a.FromY, 0f, eased));
            a.El.style.opacity = eased;
        }
    }

    private void FinishIntro()
    {
        foreach (Anim a in anims)
        {
            a.El.style.translate = StyleKeyword.Null;
            a.El.style.opacity = StyleKeyword.Null;
        }

        anims.Clear();
        StopTick();
    }

    private void StopTick()
    {
        introPlaying = false;

        if (tick != null)
        {
            Context.updateProxy.DestoryTask(tick);
            tick = null;
        }
    }

    private void OnDoneClicked()
    {
        // 检查名字和世界是否为空
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName))
        {
            new PromptMessage("世界或玩家名称为空！").Show();
            return;
        }

        int emptySlot = PKSv.SvMgr.FindEmptySlot();
        if (emptySlot == -1)
        {
            new MessageBoxOk().ShowMsg("没有剩余的存档插槽，请删除一个后再试！");
            return;
        }

        StopTick();

        // 实例化一个saveData出来，给存档系统用于写入到磁盘
        var saveData = new PKSv.SaveData
        {
            bagpackItems = null,
            currentEquipedTool = null,
            equipedBag = null,
            handItems = null,
            playerName = playerName,
            worldName = worldName,
            // 性别必须跟着一起存：下面 equipedCloth 是按 isGirl 那一套挑的，
            // 读档时如果不还原性别，会拿男装 id 去女装列表里找，部位全对不上
            isGirl = isGirl
        };

        foreach (Section section in Sections)
        {
            if (!picked[isGirl].TryGetValue(section.Slot, out ClothBase cloth) || cloth == null) continue;

            saveData.equipedCloth.SetSlot(section.Slot, cloth.GetACopy() as ClothBase);
        }

        PKSv.SvMgr.Write(saveData, emptySlot);
        saveMenu?.BuildSlots();

        Hide();
    }

    private void OnBackClicked()
    {
        StopTick();
        Hide();
    }

    public override void Destroy()
    {
        StopTick();

        if (rtHandle.IsValid())
        {
            Addressables.Release(rtHandle);
        }

        base.Destroy();
    }
}
