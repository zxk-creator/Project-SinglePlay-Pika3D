using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PK;

/// <summary>
/// 运行时换装系统。
///
/// 每个装备 Prefab 内部自带一套 Bip001 骨骼层级，SkinnedMeshRenderer 引用
/// 这些骨骼。本脚本的做法是：
///   1. 实例化装备 Prefab（Resources.Load 或拖拽赋值）
///   2. 把 SMR 的 bones 数组按 名称匹配 重映射到角色身上的真实骨骼
///   3. 角色没有的骨骼（Woman_Skirt_* / HatRoot 等自定义挂点）→ 沿装备层级
///      向上找到最近的"角色骨骼"，把中间链路原样克隆到角色骨架下
///   4. 设置材质参数（见 SetMaterialParameters，按 hotfix/shader 下的
///      ApowoColorMaskShadowCommonShader 字段）
///   5. 清理装备自带的冗余骨骼
///   6. 若开启 bakeToToon：把材质经 ShaderUtil 烘焙成 UTS（Unity Toon Shader）卡通材质
///      （颜色遮罩烤进 _MainTex，替换原材质）
///
/// 把本脚本挂到角色 GameObject 上，在Inspector拖入bonesRoot，注意男女共用同一骨架。
/// 性别和 NPC 标记由外部所有者直接注入；本类不再查找 CharacterBase。
/// 本类仅负责装备的3D外观渲染显示，除此之外没有别的3D渲染方式。
/// </summary>
public class Equip : MonoBehaviour
{
    ////////////////////////////////////////基本身份与默认着装////////////////////////////////////////

    [Tooltip("骨架根（男女共用同一骨架，记得赋值的是Hierarchy里面的）")]
    public Transform bonesRoot;

    [Tooltip("由 LocalPlayer / NPC 在初始化时直接赋值。Equip 不自动查找 CharacterBase。")]
    public bool isGirl = true;
    [Tooltip("由角色所有者注入。")]
    public bool isNPC;

    [Header("女性默认着装")]
    public GameObject femaleDefaultUpper;
    public GameObject femaleDefaultBottom;
    public GameObject femaleDefaultFace;
    public GameObject femaleDefaultHair;
    public GameObject femaleDefaultHand;
    public GameObject femaleDefaultStocking;

    [Header("男性默认着装")]
    public GameObject maleDefaultUpper;
    public GameObject maleDefaultBottom;
    public GameObject maleDefaultFace;
    public GameObject maleDefaultHair;
    public GameObject maleDefaultHand;
    public GameObject maleDefaultStocking;

    [Header("每件装备的染色（_ClothColor / _ClothColor1，默认白色=不改色）")]
    public SlotColors hairColors = new SlotColors { clothColor = Color.white, clothColor1 = Color.white };

    [Header("是否烘焙为卡通材质")]
    [Tooltip("关闭则保持 Apowo 换色材质原样")]
    public bool bakeToToon = true;

    ////////////////////////////////////////换装通知////////////////////////////////////////

    /// <summary>换装完成通知（CharacterAnimation 用它重建衣服动画覆盖，帧末统一触发）</summary>
    public event System.Action OnEquipChanged;

    /// <summary>换装后置位，LateUpdate 统一通知，避免同帧多次换装重复通知</summary>
    private bool animNotifyDirty;

    ////////////////////////////////////////生命周期////////////////////////////////////////

    /// <summary>
    /// 使用者必须调用这个来初始化换装系统
    /// </summary>
    public void Initialize()
    {
        if (bonesRoot == null)
        {
            throw new UnityException("[Equip] bonesRoot 未赋值，请在 Inspector 里拖上骨架根");
        }

        NormalizeSkeleton();

        equipRoot = new GameObject("_EquipRoot").transform;
        equipRoot.SetParent(transform, false);

        SetLayerRecursively(transform, 6);
        Context.boneRef = bonesRoot.gameObject;
    }

    /// <summary>
    /// 换装与骨骼销毁必须落在同一帧，否则Destroy在帧末落地，后续逻辑会把已销毁的骨骼当为null。
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (animNotifyDirty)
        {
            animNotifyDirty = false;
            OnEquipChanged?.Invoke();
        }
        if (meshDirty)
        {
            meshDirty = false;
            // 换装/重新烘焙会重建材质，表情材质缓存必须失效，否则眨眼会写到已经没人用的那份材质上
            eyeMaterial = null;
            WeldBottomStocking(0.01f);
        }
    }

    ////////////////////////////////////////性别与默认着装////////////////////////////////////////

    /// <summary>
    /// 设定性别并按该性别恢复默认着装。
    /// </summary>
    public void SetGender(bool newIsGirl)
    {
        isGirl = newIsGirl;
        ClearBoneCache();
        ApplyDefaultOutfit();
    }

    /// <summary>应用默认着装</summary>
    public void ApplyDefaultOutfit()
    {
        wornCloth.Clear();

        foreach (EClothType slot in System.Enum.GetValues(typeof(EClothType)))
            ClearSlot(slot);

        WearDefault(EClothType.UPPER);
        WearDefault(EClothType.BOTTOM);
        WearDefault(EClothType.FACE);
        WearDefault(EClothType.HAIR);
        WearDefault(EClothType.HAND);
        WearDefault(EClothType.STOCKING);
        ReapplyConflicts();
        SetLayerRecursively(transform, 6);
        animNotifyDirty = true;
    }

    /// <summary>穿上某部位的默认着装。</summary>
    private void WearDefault(EClothType slot)
    {
        GameObject prefab;
        switch (slot)
        {
            case EClothType.UPPER: prefab = isGirl ? femaleDefaultUpper : maleDefaultUpper; break;
            case EClothType.BOTTOM: prefab = isGirl ? femaleDefaultBottom : maleDefaultBottom; break;
            case EClothType.FACE: prefab = isGirl ? femaleDefaultFace : maleDefaultFace; break;
            case EClothType.HAIR: prefab = isGirl ? femaleDefaultHair : maleDefaultHair; break;
            case EClothType.HAND: prefab = isGirl ? femaleDefaultHand : maleDefaultHand; break;
            case EClothType.STOCKING: prefab = isGirl ? femaleDefaultStocking : maleDefaultStocking; break;
            default: prefab = null; break; // GLASSES / EARRING / SHOE 等无默认装
        }

        if (prefab == null) return;
        ApplySlot(slot, prefab, GetSlotColors(slot));
    }

    /// <summary>按部位取对应的染色配置</summary>
    private SlotColors GetSlotColors(EClothType slot)
    {
        switch (slot)
        {
            case EClothType.HAIR: return hairColors;
            default: return new SlotColors();
        }
    }

    ////////////////////////////////////////穿脱接口////////////////////////////////////////

    /// <summary>
    /// 穿上衣服。由 clothType 决定部位、prefabPath 决定模型。
    /// 若布料带 conflictPart，则对应部位的布料不渲染。
    /// 内部会自动做一次比较，如果属性都一样则不会穿上直接返回
    /// </summary>
    public void Wear(ClothBase newCloth)
    {
        if (newCloth == null)
        {
            return;
        }

        // 进行真比较，比较属性。如果属性一样就不换，直接return;

        EClothType slot = newCloth.clothType;
        if (slot == EClothType.NONE)
        {
            Log.Info("指定衣服插槽为空，不会穿上衣服");
            return;
        }

        if (string.IsNullOrEmpty(newCloth.prefabPath))
        {
            Log.Info("prefabPath为空，不会穿上衣服");
            return;
        }

        // 比较是否一样，如果一样则不换
        if (wornCloth.TryGetValue(slot, out ClothBase worn) && worn != null && newCloth.IsSameAs(worn))
        {
            wornCloth[slot] = newCloth;
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(newCloth.prefabPath);
        if (prefab == null)
        {
            WearDefault(slot);
            ReapplyConflicts();
            animNotifyDirty = true; // 恢复默认装也属于换装，必须通知动画系统重建覆盖，否则残留旧衣服动画
            return;
        }

        wornCloth[slot] = newCloth;
        ApplySlot(slot, prefab, GetSlotColors(slot));
        ReapplyConflicts();
        animNotifyDirty = true; // 换装完成 → 帧末通知动画系统重建衣服动画覆盖
    }

    /// <summary>
    /// 脱下某部位并恢复为该部位的默认着装。
    /// </summary>
    public void Remove(EClothType slot)
    {
        wornCloth.Remove(slot);
        ClearSlot(slot);
        WearDefault(slot);
        ReapplyConflicts();
        animNotifyDirty = true;
    }

    /// <summary>
    /// 用一整套装备记录替换当前着装，用于读档或切换角色。为null的部位保持默认
    /// </summary>
    public void ApplyEquipped(CharacterEquipCollection newEquips)
    {
        if (newEquips == null)
        {
            Log.NullPtr("EquipSystem.ApplyEquipped");
            return;
        }

        // 处理单个槽位的变化
        void HandleSlot(EClothType slot)
        {
            ClothBase newCloth = newEquips.GetSlot(slot) as ClothBase;
            wornCloth.TryGetValue(slot, out ClothBase oldCloth);

            if (newCloth == null)
            {
                // 传过来的null代表卸下该装备
                if (oldCloth != null)
                {
                    wornCloth.Remove(slot);
                    ClearSlot(slot);
                }

                // 对于必须穿默认装的部位，卸下后要兜底穿上默认装，防止裸奔
                bool isDefaultRequired = slot == EClothType.UPPER || slot == EClothType.BOTTOM ||
                                         slot == EClothType.FACE || slot == EClothType.HAIR ||
                                         slot == EClothType.HAND || slot == EClothType.STOCKING;

                if (isDefaultRequired && !activeSmrs.ContainsKey(slot))
                {
                    WearDefault(slot);
                }
            }
            else
            {
                // 传非 null 时直接调 Wear。
                // Wear 内部已经通过 IsSameAs 做了判断：如果一样则直接 return，不会重新实例化和重载。
                // 如果不一样，Wear 内部会自动 ClearSlot(slot) 并穿上新的。
                Wear(newCloth);
            }
        }

        // 遍历所有槽位进行对比与更新
        HandleSlot(EClothType.HAIR);
        HandleSlot(EClothType.UPPER);
        HandleSlot(EClothType.BOTTOM);
        HandleSlot(EClothType.STOCKING);
        HandleSlot(EClothType.SHOE);
        HandleSlot(EClothType.HAND);
        HandleSlot(EClothType.EARRING);
        HandleSlot(EClothType.FACE);
        HandleSlot(EClothType.GLASSES);

        // 重新计算冲突并标记重建
        ReapplyConflicts();
        animNotifyDirty = true;
    }

    /// <summary>
    /// 按当前所有布料的 conflictPart 重算隐藏关系
    /// 冲突部位清除，解除的重新渲染，且上装压制下装时跳过下装自身的conflictPart。
    /// </summary>
    private void ReapplyConflicts()
    {
        // 上装是否为压制下装冲突的特殊类型
        bool suppressBottomConflict =
            wornCloth.TryGetValue(EClothType.UPPER, out ClothBase upper)
            && upper.conflictPart != null
            && upper.conflictPart.Contains(EClothType.BOTTOM);

        HashSet<EClothType> hidden = new HashSet<EClothType>();
        foreach (var kv in wornCloth)
        {
            // 特殊规则：上装压制下装冲突时，跳过下装自身的 conflictPart
            if (kv.Key == EClothType.BOTTOM && suppressBottomConflict) continue;

            var conflicts = kv.Value.conflictPart;
            if (conflicts == null) continue;
            foreach (EClothType c in conflicts)
            {
                if (c != EClothType.NONE && c != kv.Key)
                    hidden.Add(c);
            }
        }

        foreach (EClothType slot in hidden)
            ClearSlot(slot);

        foreach (EClothType slot in System.Enum.GetValues(typeof(EClothType)))
        {
            if (slot == EClothType.NONE) continue;
            if (hidden.Contains(slot)) continue;
            if (activeSmrs.ContainsKey(slot)) continue;

            if (wornCloth.TryGetValue(slot, out ClothBase cloth) && !string.IsNullOrEmpty(cloth.prefabPath))
            {
                GameObject prefab = Resources.Load<GameObject>(cloth.prefabPath);
                if (prefab != null)
                {
                    ApplySlot(slot, prefab, GetSlotColors(slot));
                    continue;
                }
            }
            WearDefault(slot);
        }
    }
    ////////////////////////////////////////浴巾造型////////////////////////////////////////

    [Header("男性女性头上的浴巾")]
    public GameObject maleHeadTowel;
    public GameObject femaleHeadTowel;

    /// <summary>浴巾造型前的穿戴快照（仅渲染层恢复用；浴巾期间不修改 wornCloth）</summary>
    private Dictionary<EClothType, ClothBase> bathSnapshot;

    /// <summary>浴巾造型涉及的部位（头发/上衣/下衣/手/袜子/脚）；头脸（FACE）保持不动</summary>
    private static readonly EClothType[] BathSuitSlots = new EClothType[]
    {
        EClothType.HAIR,
        EClothType.UPPER,
        EClothType.BOTTOM,
        EClothType.HAND,
        EClothType.STOCKING,
        EClothType.SHOE,
    };

    /// <summary>
    /// 改成角色外观为洗澡造型
    /// </summary>
    public void ApplyBathSuit()
    {
        if (bathSnapshot == null)
            bathSnapshot = new Dictionary<EClothType, ClothBase>(wornCloth); // 只读快照，供恢复用

        GameObject towel = isGirl ? femaleHeadTowel : maleHeadTowel;
        if (towel == null)
        {
            return;
        }

        ApplySlot(EClothType.HAIR, towel, GetSlotColors(EClothType.HAIR)); // 浴巾当头发（不写记录）

        WearDefault(EClothType.UPPER);
        WearDefault(EClothType.BOTTOM);
        WearDefault(EClothType.HAND);
        WearDefault(EClothType.STOCKING);
        ClearSlot(EClothType.SHOE); // 无默认鞋 → 清空渲染（赤脚/袜子兜底）

        animNotifyDirty = true; // 显示变化 → 帧末通知动画系统重建衣服动画覆盖
    }

    /// <summary>
    /// 恢复浴巾造型之前的穿戴显示
    /// </summary>
    public void ApplyBeforeBathEquipedSuit()
    {
        if (bathSnapshot == null)
        {
            return;
        }

        foreach (EClothType slot in BathSuitSlots)
        {
            if (bathSnapshot.TryGetValue(slot, out ClothBase saved)
                && saved != null && !string.IsNullOrEmpty(saved.prefabPath))
                Wear(saved); // 恢复原穿戴（记录值与浴巾前一致）
            else
                WearDefault(slot); // 原本就是默认着装 → 恢复默认
        }

        bathSnapshot = null;
        animNotifyDirty = true;
    }

    ////////////////////////////////////////穿戴记录与系列////////////////////////////////////////

    /// <summary>当前每个部位穿着的布料，用于冲突解除后重新渲染</summary>
    private readonly Dictionary<EClothType, ClothBase> wornCloth
        = new Dictionary<EClothType, ClothBase>();

    /// <summary>取当前某部位穿着的布料，未穿着返回 null</summary>
    public ClothBase GetWornCloth(EClothType slot)
    {
        wornCloth.TryGetValue(slot, out ClothBase cloth);
        return cloth;
    }

    /// <summary>当前某部位是否穿着布料</summary>
    public bool HasWornCloth(EClothType slot)
    {
        return wornCloth.TryGetValue(slot, out ClothBase cloth) && cloth != null;
    }

    /// <summary>当前某部位穿着的衣服系列（如 Girl0001 / Boy0002）；未穿着或命名不含下划线返回 null。
    /// 优先用布料记录（Wear 的随机装），其次用当前实例名（默认着装也能推出来）。</summary>
    public string GetWornClothSeries(EClothType slot)
    {
        if (wornCloth.TryGetValue(slot, out ClothBase cloth) && cloth != null && !string.IsNullOrEmpty(cloth.prefabPath))
            return GetSeries(cloth.prefabPath);
        if (activeInstanceRoots.TryGetValue(slot, out GameObject root) && root != null)
            return GetSeries(root.name);
        return null;
    }

    /// <summary>系列 = prefab 路径或物体名中，文件名第一个下划线之前的部分（Girl0001_Hair → Girl0001）</summary>
    public static string GetSeries(string prefabPathOrName)
    {
        if (string.IsNullOrEmpty(prefabPathOrName)) return null;
        int slash = prefabPathOrName.LastIndexOf('/');
        string fileName = slash >= 0 ? prefabPathOrName.Substring(slash + 1) : prefabPathOrName;
        int underscore = fileName.IndexOf('_');
        return underscore > 0 ? fileName.Substring(0, underscore) : null;
    }

    ////////////////////////////////////////骨架与骨骼映射////////////////////////////////////////
    /// <summary> {名称: Transform的角色骨骼表}</summary>
    private Dictionary<string, Transform> boneMap;

    /// <summary>骨架相对角色根的向下偏移，对齐地面用</summary>
    private const float SkeletonGroundOffset = 0.126f;

    /// <summary>各部位要搬到角色骨骼上的挂点根名字</summary>
    private static readonly Dictionary<EClothType, string> SlotMountBone = new Dictionary<EClothType, string>
    {
        { EClothType.HAIR,     "HairRoot" },
        { EClothType.BOTTOM,   "Woman_SkirtRoot" },
        { EClothType.GLASSES,  "GlassesRoot" },
        { EClothType.EARRING,  "EarringRoot" },
        { EClothType.FACE,     "FaceRoot" },
        { EClothType.SHOE,     "ShoestRoot" },
        { EClothType.STOCKING, "StockingRoot" },
        { EClothType.HAND,     "HandRoot" },
        { EClothType.UPPER,    "Woman_CoatRoot" },
    };

    /// <summary>各部位挂点对应的角色真实骨骼名</summary>
    private static readonly Dictionary<EClothType, string> SlotMountTarget = new Dictionary<EClothType, string>
    {
        { EClothType.HAIR,     "Bip001 Head" },
        { EClothType.BOTTOM,   "Bip001 Pelvis" },
        { EClothType.GLASSES,  "Bip001 Head" },
        { EClothType.EARRING,  "Bip001 Head" },
        { EClothType.FACE,     "Bip001 Head" },
        { EClothType.SHOE,     "Bip001 R Calf 1" },
        { EClothType.STOCKING, "Bip001 R Calf 1" },
        { EClothType.HAND,     "Bip001 R Hand" },
        { EClothType.UPPER,    "Bip001" },
    };

    /// <summary>
    /// 骨架是预先放在 prefab 里的固定子节点，但它的局部坐标不受代码控制
    /// （拖进来时 Unity 可能保留世界坐标，编辑器里也可能被误动）。
    /// 这里拉回正确位置：局部零位，再往下挪 SkeletonGroundOffset 对齐地面。
    /// </summary>
    public void NormalizeSkeleton()
    {
        if (bonesRoot == null)
        {
            throw new UnityException("[Equip] bonesRoot 未赋值，请在 Inspector 里拖上骨架根");
        }
        bonesRoot.localRotation = Quaternion.identity;
        bonesRoot.localScale = Vector3.one;
        bonesRoot.localPosition = new Vector3(0f, -SkeletonGroundOffset, 0f);
    }

    /// <summary>清除缓存的角色骨骼表。切换性别（骨架上挂着的克隆骨骼会被换掉）后必须调用。</summary>
    public void ClearBoneCache()
    {
        boneMap = null;
    }

    /// <summary>骨架根（男女共用同一骨架；NetPlayer / LocalPlayer 也读它来关掉骨骼物理）</summary>
    public Transform GetBonesRoot()
    {
        return bonesRoot;
    }

    /// <summary>收集角色的所有骨骼 Transform</summary>
    private void BuildBoneMap()
    {
        boneMap = new Dictionary<string, Transform>();

        Transform root = GetBonesRoot();
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (!boneMap.ContainsKey(t.name))
                boneMap[t.name] = t;
        }
    }

    /// <summary>按名字取角色骨架上的骨骼（含未激活）</summary>
    private Transform FindBoneByName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        if (boneMap != null && boneMap.TryGetValue(boneName, out Transform cached)) return cached;

        Transform root = GetBonesRoot();
        if (root == null) root = transform;
        return FindChildRecursive(root, boneName);
    }

    /// <summary>递归查找子节点（含未激活），返回第一个同名 Transform</summary>
    private Transform FindChildRecursive(Transform current, string name)
    {
        if (current == null) return null;
        if (current.name == name) return current;
        for (int i = 0; i < current.childCount; i++)
        {
            Transform res = FindChildRecursive(current.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }

    /// <summary>
    /// 把单个 SMR 引用的骨骼全部注册到角色的骨骼层级中：
    ///   - 标准 Bip001 骨骼 → 直接引用角色的同名骨骼
    ///   - 自定义骨骼（Woman_Skirt_* / HatRoot 等）→ 沿装备层级向上找到最近的
    ///     角色骨骼，把中间链路原样克隆到角色骨架下
    /// 只做重映射，不做清理（多 SMR 装备需全部重映射后再统一清理，避免误删其他 SMR）。
    ///
    /// 【绑上 ≠ 能活到最后】这里写进 bones 的，有一部分是刚克隆进角色骨架的普通 GameObject
    /// （见 GetOrCreateBoneInCharacter），并不受本部位"所有权"保护：别处一次按名字清理同名残留、
    /// 或别的插槽换装时的销毁，都可能把它带走。而 Destroy 是帧末才执行的，
    /// 所以"绑的时候还在"绝不能当成"之后还在"。
    /// 一旦换装跨了帧，这些引用就变成已销毁对象（裙骨 + 上衣踩过：绑定全部成功，之后 15 根裙骨全是 null）。
    /// 规避方式见 LateUpdate 上方的说明。
    /// </summary>
    private void RemapBones(SkinnedMeshRenderer smr, GameObject instance, Dictionary<(Transform, string), Transform> createdThisWear)
    {
        // ── remap bones 数组（保留索引与 bindpose 的对应关系）──
        Transform[] bones = smr.bones;
        bool changed = false;
        for (int i = 0; i < bones.Length; i++)
        {
            Transform src = bones[i];
            if (src == null) continue;

            if (boneMap.TryGetValue(src.name, out Transform charBone))
            {
                bones[i] = charBone;
                changed = true;
            }
            else
            {
                Transform cloned = GetOrCreateBoneInCharacter(src, instance, createdThisWear);
                if (cloned != null)
                {
                    bones[i] = cloned;
                    changed = true;
                }
            }
        }
        if (changed) smr.bones = bones;

        // ── remap rootBone ──
        if (smr.rootBone != null)
        {
            if (boneMap.TryGetValue(smr.rootBone.name, out Transform charRoot))
            {
                smr.rootBone = charRoot;
            }
            else
            {
                Transform cloned = GetOrCreateBoneInCharacter(smr.rootBone, instance, createdThisWear);
                if (cloned != null) smr.rootBone = cloned;
            }
        }

        // SMR 的 GameObject 提为 equipRoot 的直接子级（与装备骨骼分离）
        smr.transform.SetParent(equipRoot, true);
    }

    /// <summary>
    /// 将装备中的自定义骨骼在角色的骨架下创建出对应的副本。
    /// 从 <paramref name="customBone"/> 沿 parent 向上走，找到第一个在
    /// <see cref="boneMap"/> 中存在的祖先，然后把中间的自定义骨骼链路
    /// 原样（localPosition / localRotation / localScale）克隆到角色对应祖先下面。
    ///
    /// 【名字撞车是这个函数的固有风险】克隆链顶端那个节点的名字，就是装备里最上面那层自定义骨骼的
    /// 名字（例如 Woman_SkirtRoot）。如果这个名字恰好等于别的部位挂点表里的挂点根（SlotMountBone），
    /// 那个部位上装时就会去挂点目标底下"清理同名旧节点"，而它找到的正是这里克隆出来的**活骨骼**，
    /// 于是整条链被摘掉销毁。历史事故：上衣 Woman_Girl0002_Upper 引用裙骨、但它的挂点根
    /// Woman_CoatRoot 在 prefab 里不存在，裙骨只能走克隆路径，克隆链顶端也叫 Woman_SkirtRoot；
    /// 紧接着穿下装（挂点根正是 Woman_SkirtRoot → Bip001）就把它当残留删了。
    /// 所以：往 SlotMountBone 里加名字前，先确认不会和别的部位克隆出来的链撞名；
    /// 挂点清理那边也必须先判断"是否还被在穿的 SMR 引用"（见 ApplySlot 挂点段）。
    ///
    /// 返回角色骨架下与 customBone 对应的 Transform。
    /// </summary>
    private Transform GetOrCreateBoneInCharacter(Transform customBone, GameObject instance, Dictionary<(Transform, string), Transform> createdThisWear)
    {
        if (customBone == null) return null;

        // 先收集从 customBone 到 instanceRoot 的链路（[0]=最底层=src）
        var chain = new List<(string name, Vector3 pos, Quaternion rot, Vector3 scale)>();
        Transform walk = customBone;
        while (walk != null && walk != instance.transform)
        {
            chain.Add((walk.name, walk.localPosition, walk.localRotation, walk.localScale));
            if (boneMap.ContainsKey(walk.name))
                break; // 走到角色骨骼就停，这一层就是锚点
            walk = walk.parent;
        }

        if (chain.Count == 0) return null;

        // chain[last] 是锚点（名字在角色表中存在）
        // 从锚点下一层开始，逐层在角色骨架下创建
        Transform anchor = null;
        int startIdx = chain.Count - 1;

        // 先确认锚点
        string anchorName = chain[startIdx].name;
        if (!boneMap.TryGetValue(anchorName, out anchor))
        {
            // 链路一直走到 instanceRoot 都没找到角色骨骼 → 挂到 equipRoot 下，
            // 从最顶层开始全部创建（startIdx = chain.Count）
            anchor = equipRoot;
            startIdx = chain.Count;
        }

        // 从锚点的下一层开始，逐层创建/找到子骨骼
        // 只复用本次穿戴创建过的骨骼，绝不复用历史装备留下的克隆
        Transform current = anchor;
        for (int i = startIdx - 1; i >= 0; i--)
        {
            var (name, pos, rot, scale) = chain[i];

            if (createdThisWear.TryGetValue((current, name), out Transform existing))
            {
                current = existing;
                continue;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(current, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            createdThisWear[(current, name)] = go.transform;
            current = go.transform;
        }

        return current; // 这就是 customBone 在角色骨架下的等价物
    }



    ////////////////////////////////////////插槽装配////////////////////////////////////////
    /// <summary>已生成的装备实例的根容器</summary>
    private Transform equipRoot;

    /// <summary>每件装备实例化后的 SMR 引用，用于随时替换/清除</summary>
    private Dictionary<EClothType, SkinnedMeshRenderer> activeSmrs
        = new Dictionary<EClothType, SkinnedMeshRenderer>();

    /// <summary>每件装备实例化后的根 GameObject（RemapBones 后与 SMR 分离的残留容器），清除装备时一并销毁</summary>
    private Dictionary<EClothType, GameObject> activeInstanceRoots
        = new Dictionary<EClothType, GameObject>();

    /// <summary>多 SMR 装备（如上装自带袜子）中除第一个外的额外 SMR 对象，清除时一并销毁</summary>
    private Dictionary<EClothType, List<GameObject>> activeSmrExtras
        = new Dictionary<EClothType, List<GameObject>>();

    /// <summary>每件装备克隆进角色骨架的自定义骨骼（Woman_Coat_* 等），换装时必须销毁，避免被下一件复用</summary>
    private Dictionary<EClothType, List<Transform>> activeCustomBones
        = new Dictionary<EClothType, List<Transform>>();

    /// <summary>真正销毁某部位当前的装备（不恢复默认）</summary>
    private void ClearSlot(EClothType slot)
    {
        if (activeSmrs.TryGetValue(slot, out SkinnedMeshRenderer oldSmr))
        {
            // 先摘表，再销毁；而且销毁之前必须判空。
            // 表里可能有已经被别处销毁过的渲染器（历史事故：装备被建成场景根对象、
            // 随场景卸载一起被销毁，这里就留着死引用）。不判空会直接抛
            // MissingReferenceException，把整条"卸下/换装"流程打断在半路 ——
            // wornCloth 已经清了、WearDefault 还没穿，数据侧和渲染侧从此错位，
            // 而且异常是从拖拽回调里抛出去的，拖拽残影也来不及销毁、会卡在屏幕上。
            activeSmrs.Remove(slot);
            if (oldSmr != null) Destroy(oldSmr.gameObject);
        }
        if (activeSmrExtras.TryGetValue(slot, out List<GameObject> oldExtras))
        {
            foreach (GameObject e in oldExtras)
                if (e != null) Destroy(e);
            activeSmrExtras.Remove(slot);
        }
        if (activeInstanceRoots.TryGetValue(slot, out GameObject oldRoot))
        {
            if (oldRoot != null) Destroy(oldRoot);
            activeInstanceRoots.Remove(slot);
        }
        if (activeCustomBones.TryGetValue(slot, out List<Transform> oldBones))
        {
            foreach (Transform b in oldBones)
            {
                if (b == null) continue;
                // 必须【立刻】脱离角色骨架再销毁：Destroy 是帧末延迟执行，
                // 若这批旧骨骼（HairRoot / Woman_SkirtRoot / Woman_Coat_* 等）还挂在
                // Bip001 Head / Bip001 等锚点下，紧接着的 BuildBoneMap 会按名字
                // first-wins 把它们抢先记进表里（旧节点在兄弟序里更靠前），
                // 新装备就被绑到这批"待销毁"骨骼上 → 帧末变 null → 蒙皮顶点被拉飞
                // （表现就是换装后头发/裙摆被拉伸变形）。SetParent(null) 让它们当帧
                // 就从骨架里消失，Destroy 只负责回收内存。
                //
                // 【危险：这批骨骼可能正被别的部位用着】骨骼挂在角色骨架上是公共资源，
                // 别的部位会按名字找到并绑上去（先穿下装时，下装把 Woman_SkirtRoot 搬到 Bip001 下，
                // 之后穿的上衣因为自己没有挂点表项，就直接按名字绑到这条链上）。这种情况下在这里
                // 一律销毁，会把那个部位一起弄死：它下次合体时这些骨骼已经是 null，整个部件被排除
                // （表现：整件衣服消失）。改这段之前，先加上"是否仍被其它在穿 SMR 的 bones/rootBone
                // 引用"的判断，只销毁确实没人用的。
                b.SetParent(null, false);
                Destroy(b.gameObject);
            }
            activeCustomBones.Remove(slot);
        }

        meshDirty = true; // 部位集合变化，则帧末重建合体网格
    }

    /// <summary>替换单个插槽（实例化 → 所有 SMR 骨骼重映射 → 清理冗余 → 材质参数）</summary>
    private void ApplySlot(EClothType slot, GameObject prefab, SlotColors colors)
    {
        // 清除同槽旧装备
        ClearSlot(slot);

        // 实例化
        GameObject instance = Instantiate(prefab, equipRoot);
        instance.name = prefab.name;
        SetLayerRecursively(instance.transform, 6);

        // 找所有 SkinnedMeshRenderer（可能在根或子对象上；部分装备如 Girl0297_Upper 自带袜子 SMR）
        SkinnedMeshRenderer[] smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0)
        {
            Destroy(instance);
            return;
        }

        // 本次穿戴内创建的自定义骨骼（parent+name → transform），多 SMR 共用同一批克隆
        var createdThisWear = new Dictionary<(Transform, string), Transform>();

        // ── 摘挂点根：把装备自己的挂点子树整条搬到角色骨骼上（原版 TryReplaceEquipAnimation）──
        //   头发  HairRoot        → Bip001 Head
        //   下装  Woman_SkirtRoot → Bip001
        //   眼镜  GlassesRoot     → Bip001 Head
        //   鞋子  ShoestRoot      → Bip001 R/L Calf 1
        // 只有装备真的有这个专用挂点时才搬；找不到就什么都不做（走后面的克隆分支）。
        // 不搬的话，HairRoot 这类挂点会走"逐层 new GameObject 抄 localPosition"的克隆路径，
        // 父空间不同 → 骨骼落到错误位置 → 头发整个飞起来。
        var mounted = new List<Transform>();
        if (SlotMountBone.TryGetValue(slot, out string mountBoneName)
            && SlotMountTarget.TryGetValue(slot, out string mountTargetName))
        {
            Transform mountRoot = FindChildRecursive(instance.transform, mountBoneName);
            if (mountRoot != null)
            {
                Transform mountTarget = FindBoneByName(mountTargetName);
                if (mountTarget != null)
                {
                    // 先清掉角色锚点上同名的旧节点（原版 Object.Destroy(gameObject2)）
                    // 同样必须立刻脱离：延迟销毁的同名节点会被后面的 BuildBoneMap 抢先记录
                    //
                    // 【危险：这个"同名旧节点"不一定是垃圾】它可能是别的部位克隆进骨架、
                    // 并且正被那件在穿的 SMR 当骨头用的链。历史事故：Woman_SkirtRoot 被两个部位共用 ——
                    // 上衣 Woman_Girl0002_Upper 引用裙骨，但它自己的挂点根 Woman_CoatRoot 在 prefab 里
                    // 不存在，整段挂点跳过，裙骨只能走 GetOrCreateBoneInCharacter 克隆进骨架，
                    // 克隆链顶端也叫 Woman_SkirtRoot；紧接着穿下装（挂点根正是 Woman_SkirtRoot → Bip001）
                    // 就把上衣那条活链当残留删了，上衣 15 根裙骨全灭，合体时按 null 被整体排除
                    // （表现：少掉上衣/躯干）。
                    // 所以改动这里或往 SlotMountBone 加名字之前，必须保证：
                    //   1) 只删"没有任何在穿 SMR 的 bones/rootBone 引用它或它子节点"的节点；
                    //   2) 同一个名字在同一副骨架下只允许存在一条链（要么挂点搬移，要么克隆，不能两者都有）。
                    Transform stale = FindChildRecursive(mountTarget, mountRoot.name);
                    if (stale != null && stale != mountRoot)
                    {
                        stale.SetParent(null, false);
                        Destroy(stale.gameObject);
                    }

                    // 挂点必须按【装备自带同名锚点】算出的相对变换挂上去，不能直接 worldPositionStays=true：
                    // 装备实例挂在 equipRoot（角色局部零位），而角色骨架被整体下移过
                    // SkeletonGroundOffset(0.126) 对齐地面 —— 两者不在同一空间。
                    // worldPositionStays=true 保留的是"骨架未下移"那一刻的世界位置，
                    // 结果整套头发比头高 0.126（表现就是头发位置不对）。
                    // 装备自带的同名锚点（如 Bip001 Head）与角色骨架的同名骨骼局部变换完全一致，
                    // 所以"相对装备锚点的变换"原样套到角色锚点上就是正确位置。
                    Transform partAnchor = mountRoot.parent;
                    while (partAnchor != null && partAnchor != instance.transform
                           && partAnchor.name != mountTargetName)
                        partAnchor = partAnchor.parent;
                    if (partAnchor == null || partAnchor == instance.transform)
                        partAnchor = FindChildRecursive(instance.transform, mountTargetName);

                    Matrix4x4 rel = mountRoot.localToWorldMatrix;
                    if (partAnchor != null)
                        rel = partAnchor.worldToLocalMatrix * rel; // 此时两者都还在装备实例子树内，同一空间

                    mountRoot.SetParent(mountTarget, false);
                    if (partAnchor != null)
                    {
                        mountRoot.localPosition = rel.GetColumn(3);
                        mountRoot.localRotation = rel.rotation;
                        mountRoot.localScale = rel.lossyScale;
                    }
                    mounted.Add(mountRoot);
                }
            }
        }

        // 骨骼表必须在旧装备销毁、新挂点就位之后重建，
        // 否则表里会残留上一件装备已销毁的骨骼引用。
        BuildBoneMap();

        // ── 所有 SMR 骨骼重映射（此时所有骨骼仍存活，逐个处理）──
        foreach (SkinnedMeshRenderer smr in smrs)
            RemapBones(smr, instance, createdThisWear);

        // ── 清理装备自带的冗余骨骼（保留实例根与所有 SMR 对象，销毁纯骨骼节点）──
        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t == instance.transform) continue;
            if (t.parent == null) continue;
            if (t.GetComponent<SkinnedMeshRenderer>() != null) continue; // 保留所有 SMR
            Destroy(t.gameObject);
        }

        // ── 销毁装备自带的 Animator ──
        // 部分衣服 prefab 自带 Animator（如 Girl0202 系列），若不销毁会残留在装备实例上，
        // 进入骨架子树后干扰骨架 Animator / GetComponentInChildren 查找 → 角色动画错乱（腿乱晃、头不动）。
        foreach (Animator a in instance.GetComponentsInChildren<Animator>(true))
        {
            if (a == null) continue;
            Destroy(a);
        }

        // ── 材质参数 ──
        foreach (SkinnedMeshRenderer s in smrs)
            SetMaterialParameters(s, colors.clothColor, colors.clothColor1);

        // ── 烘焙为 Toon 卡通材质──
        if (bakeToToon)
        {
            foreach (SkinnedMeshRenderer s in smrs)
                BakeMaterialsToToon(s);
        }

        // 换装时要一并销毁的骨骼：本次克隆出来的 + 搬到角色骨骼上的挂点子树
        var ownedBones = new List<Transform>(createdThisWear.Values);
        ownedBones.AddRange(mounted);
        activeCustomBones[slot] = ownedBones;
        activeSmrs[slot] = smrs[0];
        if (smrs.Length > 1)
        {
            List<GameObject> extras = new List<GameObject>();
            for (int i = 1; i < smrs.Length; i++)
                extras.Add(smrs[i].gameObject);
            activeSmrExtras[slot] = extras;
        }
        activeInstanceRoots[slot] = instance;

        meshDirty = true; // 部位集合变化 → 帧末重建合体网格
    }



    /// <summary>返回当前所有穿戴的 SMR（主 SMR + 多 SMR 装备的额外 SMR）</summary>
    private List<SkinnedMeshRenderer> GetAllActiveSmrs()
    {
        var list = new List<SkinnedMeshRenderer>();
        foreach (var kv in activeSmrs)
            if (kv.Value != null) list.Add(kv.Value);
        foreach (var kv in activeSmrExtras)
        {
            if (kv.Value == null) continue;
            foreach (GameObject go in kv.Value)
            {
                if (go == null) continue;
                SkinnedMeshRenderer s = go.GetComponent<SkinnedMeshRenderer>();
                if (s != null) list.Add(s);
            }
        }
        return list;
    }

    // 遍历Layer，修改为6player，使其能够被玩家相机拍到。
    private void SetLayerRecursively(Transform root, int layer)
    {
        if (isNPC) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }



    ////////////////////////////////////////材质与烘焙////////////////////////////////////////

    /// <summary>
    /// 材质参数由代码在运行时统一覆盖（不依赖预制体/材质资产里序列化的值）：
    ///   - 皮肤颜色 _SkinColor           → FFE6D2
    ///   - 贴图剪裁 _MainCutOut 等       → 0.5
    ///   - 颜色系数 _ColorCoef           → 1
    ///   - 颜色过滤系数 _ColorFilterCoef → 1
    ///   - 换色 _ClothColor / _ClothColor1 → 各部位染色
    /// 材质会实例化一份，避免污染原始材质。
    /// </summary>
    private void SetMaterialParameters(SkinnedMeshRenderer smr, Color clothColor0, Color clothColor1)
    {
        if (smr == null) return;

        Material[] srcMats = smr.sharedMaterials;
        Material[] newMats = new Material[srcMats.Length];
        for (int i = 0; i < srcMats.Length; i++)
        {
            Material mat = srcMats[i];
            if (mat == null) { newMats[i] = null; continue; }

            mat = Instantiate(mat);

            mat.SetColor("_SkinColor", new Color(1f, 0.902f, 0.824f));
            mat.SetFloat("_MainCutOut", 0.5f);
            mat.SetFloat("_ClothMaskCutOut", 0.5f);
            mat.SetFloat("_ClothMaskCutOut1", 0.5f);
            mat.SetFloat("_SkinMaskCutOut", 0.5f);
            mat.SetFloat("_ColorCoef", 1f);
            mat.SetFloat("_ColorFilterCoef", 1f);
            mat.SetColor("_ClothColor", clothColor0);
            mat.SetColor("_ClothColor1", clothColor1);

            newMats[i] = mat;
        }
        smr.sharedMaterials = newMats;
    }

    /// <summary>
    /// 把部位上的每个材质用 ShaderUtil 烘焙成 UTS 材质并替换。
    /// 烘焙失败的材质（_2uvSwitch=1 / 非换色 shader / 加载失败）保留原样，
    /// 由 ShaderUtil 内部打警告；成功后销毁 SetMaterialParameters 产生的中间实例。
    /// </summary>
    private void BakeMaterialsToToon(SkinnedMeshRenderer smr)
    {
        Material[] srcMats = smr.sharedMaterials;
        Material[] newMats = new Material[srcMats.Length];
        int bakedCount = 0;

        for (int i = 0; i < srcMats.Length; i++)
        {
            Material src = srcMats[i];
            if (src == null) { newMats[i] = null; continue; }

            Material baked = ShaderUtil.ProcessAndCreateMaterial(src);
            if (baked != null)
            {
                newMats[i] = baked;
                bakedCount++;
                Destroy(src); // 中间实例不再需要
            }
            else
            {
                newMats[i] = src; // 保留原材质
            }
        }

        smr.sharedMaterials = newMats;
    }



    ////////////////////////////////////////网格焊接////////////////////////////////////////

    /// <summary>部位集合变化的脏标记（LateUpdate 统一焊接，避免一帧多次）</summary>
    private bool meshDirty;

    // 合并Bottom和Stocking的顶点
    private void WeldBottomStocking(float threshold = 0.001f)
    {
        if (!activeSmrs.TryGetValue(EClothType.BOTTOM, out var bottomSmr) || bottomSmr == null) return;
        if (!activeSmrs.TryGetValue(EClothType.STOCKING, out var stockSmr) || stockSmr == null) return;

        var bMesh = bottomSmr.sharedMesh;
        var sMesh = stockSmr.sharedMesh;
        if (bMesh == null || sMesh == null) return;

        bMesh = Instantiate(bMesh); bottomSmr.sharedMesh = bMesh;
        sMesh = Instantiate(sMesh); stockSmr.sharedMesh = sMesh;

        var bVerts = bMesh.vertices;
        var sVerts = sMesh.vertices;
        var bWeights = bMesh.boneWeights;
        var sWeights = sMesh.boneWeights;

        var bBones = bottomSmr.bones;
        var sBones = stockSmr.bones;

        // Transform: 各自索引
        var bBoneToIdx = new Dictionary<Transform, int>();
        var sBoneToIdx = new Dictionary<Transform, int>();
        for (int i = 0; i < bBones.Length; i++) if (bBones[i] != null) bBoneToIdx[bBones[i]] = i;
        for (int i = 0; i < sBones.Length; i++) if (sBones[i] != null) sBoneToIdx[sBones[i]] = i;

        Matrix4x4 sToB = bottomSmr.transform.worldToLocalMatrix * stockSmr.transform.localToWorldMatrix;
        float sqr = threshold * threshold;

        // Stocking 顶点变换到 Bottom 本地空间（只算一次；原来写在最内层循环里，每个配对都重算一遍）
        var sVertsInB = new Vector3[sVerts.Length];
        for (int j = 0; j < sVerts.Length; j++) sVertsInB[j] = sToB.MultiplyPoint3x4(sVerts[j]);

        // 先收集所有"位置重合"的顶点对（原来边找边改权重，会因为权重被覆盖而互相干扰）
        var matches = new List<int>();   // 扁平存放 i, j 对
        for (int i = 0; i < bVerts.Length; i++)
        {
            Vector3 bp = bVerts[i];
            for (int j = 0; j < sVerts.Length; j++)
            {
                if ((bp - sVertsInB[j]).sqrMagnitude > sqr) continue;
                matches.Add(i);
                matches.Add(j);
            }
        }
        if (matches.Count == 0) return;

        // 每个顶点最多参与一次合并
        var bGroup = new int[bVerts.Length];
        var sGroup = new int[sVerts.Length];
        for (int i = 0; i < bGroup.Length; i++) bGroup[i] = -1;
        for (int j = 0; j < sGroup.Length; j++) sGroup[j] = -1;

        var gB = new List<int>();
        var gS = new List<int>();

        // 骨骼权重平均（保留原有的 AccumulateBW / MakeBW 逻辑）
        for (int k = 0; k + 1 < matches.Count; k += 2)
        {
            int i = matches[k], j = matches[k + 1];
            if (bGroup[i] >= 0 || sGroup[j] >= 0) continue;

            int gid = gB.Count;
            bGroup[i] = gid; gB.Add(i);
            sGroup[j] = gid; gS.Add(j);

            var avg = new Dictionary<Transform, float>();
            AccumulateBW(avg, bBones, bWeights[i]);
            AccumulateBW(avg, sBones, sWeights[j]);

            var list = new List<KeyValuePair<Transform, float>>(avg);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            // 各自映射回自己的索引空间。
            // fallback 传该顶点【原来】的权重：候选骨骼一根都映射不上时保留原权重，
            // 绝不写出"权重和 < 1"的顶点（那种顶点会被 Unity 朝网格原点收缩，腿看着就断了）。
            bWeights[i] = MakeBW(list, bBoneToIdx, bWeights[i]);
            sWeights[j] = MakeBW(list, sBoneToIdx, sWeights[j]);
        }

        bMesh.boneWeights = bWeights;
        sMesh.boneWeights = sWeights;

        // ---- 位置 + 法线 + 切线 也一并合并（等价于 Blender 的 Merge by Distance）----
        // 只平均属性、不改顶点数与三角形索引。
        var bNormals = bMesh.normals;
        var sNormals = sMesh.normals;
        var bTangents = bMesh.tangents;
        var sTangents = sMesh.tangents;
        bool doPos = bNormals != null && bNormals.Length == bVerts.Length
                  && sNormals != null && sNormals.Length == sVerts.Length;
        if (!doPos) return;

        var bN = (Vector3[])bNormals.Clone();
        var sN = (Vector3[])sNormals.Clone();
        var bT = bTangents != null && bTangents.Length == bVerts.Length ? (Vector4[])bTangents.Clone() : null;
        var sT = sTangents != null && sTangents.Length == sVerts.Length ? (Vector4[])sTangents.Clone() : null;

        for (int g = 0; g < gB.Count; g++)
        {
            int i = gB[g], j = gS[g];

            // 位置取平均
            Vector3 pm = (bVerts[i] + sVertsInB[j]) * 0.5f;
            bVerts[i] = pm;
            sVerts[j] = sToB.inverse.MultiplyPoint3x4(pm);

            // 法线取平均（归一化）
            Vector3 n = bN[i] + sN[j];
            if (n.sqrMagnitude > 1e-12f) n.Normalize();
            bN[i] = n;
            sN[j] = n;

            // 切线取平均（与法线保持正交；w 取和，即"两次镜像 = 不翻转"）
            if (bT != null && sT != null)
            {
                Vector3 t = new Vector3(bT[i].x + sT[j].x, bT[i].y + sT[j].y, bT[i].z + sT[j].z);
                t -= n * Vector3.Dot(n, t);
                if (t.sqrMagnitude > 1e-12f) t.Normalize();
                float tw = bT[i].w + sT[j].w;
                Vector4 tm = new Vector4(t.x, t.y, t.z, tw >= 0f ? 1f : -1f);
                bT[i] = tm;
                sT[j] = tm;
            }
        }

        bMesh.normals = bN;
        sMesh.normals = sN;
        bMesh.vertices = bVerts;
        sMesh.vertices = sVerts;
        if (bT != null && sT != null)
        {
            bMesh.tangents = bT;
            sMesh.tangents = sT;
        }
    }

    private static void AccumulateBW(Dictionary<Transform, float> dict, Transform[] bones, BoneWeight bw)
    {
        void Add(int idx, float w)
        {
            if (w <= 0f || idx < 0 || idx >= bones.Length) return;
            var t = bones[idx];
            if (t == null) return;
            dict.TryGetValue(t, out var c);
            dict[t] = c + w;
        }
        Add(bw.boneIndex0, bw.weight0);
        Add(bw.boneIndex1, bw.weight1);
        Add(bw.boneIndex2, bw.weight2);
        Add(bw.boneIndex3, bw.weight3);
    }

    /// <summary>
    /// 把"下装 + 袜子权重相加后的候选集"折算成【某一件网格自己的】BoneWeight。
    ///
    /// 【必须只在本网格骨骼表里挑骨骼，并按它们的和归一化】原来是把前 4 个候选（可能含
    /// 映射不上的骨骼）的和当分母，映射不上的跳过不写，结果写出去的权重和小于 1。
    /// Unity 蒙皮对权重和不足 1 的顶点会按缺失比例把它朝网格原点收缩 —— 一整片顶点被收缩，
    /// 看上去就是那条腿/裙摆"从中间断掉"。长筒袜踩过这个坑：它的骨骼表里只有 Bip001 *，
    /// 没有下装的 Woman_Skirt_*，而下装的权重会混进同一个排序里，被算进分母又被跳过。
    /// 一根都映射不上时退回该顶点原来的权重（<paramref name="fallback"/>），
    /// 保证任何情况下写出去的权重和都是 1。
    /// </summary>
    private static BoneWeight MakeBW(List<KeyValuePair<Transform, float>> sorted, Dictionary<Transform, int> boneToIdx, BoneWeight fallback)
    {
        var idx = new int[4];
        var val = new float[4];
        int n = 0;
        float total = 0f;
        for (int i = 0; i < sorted.Count && n < 4; i++)
        {
            if (!boneToIdx.TryGetValue(sorted[i].Key, out int boneIdx)) continue;
            idx[n] = boneIdx;
            val[n] = sorted[i].Value;
            total += sorted[i].Value;
            n++;
        }
        if (n == 0 || total <= 0f) return fallback;

        var r = new BoneWeight();
        for (int i = 0; i < n; i++)
        {
            float weight = val[i] / total;
            switch (i)
            {
                case 0: r.boneIndex0 = idx[i]; r.weight0 = weight; break;
                case 1: r.boneIndex1 = idx[i]; r.weight1 = weight; break;
                case 2: r.boneIndex2 = idx[i]; r.weight2 = weight; break;
                case 3: r.boneIndex3 = idx[i]; r.weight3 = weight; break;
            }
        }
        return r;
    }

    ////////////////////////////////////////工具////////////////////////////////////////

    /// <summary>当前挂载到手上的工具实例（斧头/镐子/剑等）</summary>
    private GameObject activeTool;

    /// <summary>当前挂载的工具数据，用于比较是否需要重新挂载</summary>
    private ToolBase wornTool;

    /// <summary>工具挂载后残留在角色身上的附属节点</summary>
    private readonly List<GameObject> activeToolExtras = new List<GameObject>();

    /// <summary>
    /// 把工具挂到WeaponRoot，挂到Bip001 R Finger11，支持传null卸下装备
    /// </summary>
    public void SetTool(ToolBase tool)
    {
        if (tool == null)
        {
            ClearTool();
            return;
        }

        // 比较是否一样，如果一样则不重新挂载
        if (wornTool != null && tool.IsSameAs(wornTool))
        {
            wornTool = tool;
            return;
        }

        ClearTool();

        GameObject prefab = Resources.Load<GameObject>(tool.prefabPath);
        if (prefab == null)
        {
            PK.Log.Error("[Equip] SetTool 加载工具预制体失败：" + tool.prefabPath);
            return;
        }

        GameObject instance = Instantiate(prefab, equipRoot);
        instance.name = prefab.name;

        Transform hand = GetHandBone();
        Transform weaponRoot = FindChildRecursive(instance.transform, "WeaponRoot");
        if (weaponRoot == null)
        {
            PK.Log.Error("[Equip] SetTool 工具预制体缺少 WeaponRoot：" + prefab.name);
            Destroy(instance);
            return;
        }

        weaponRoot.SetParent(hand, true);
        weaponRoot.localPosition = Vector3.zero;

        activeTool = instance;
        activeToolExtras.Add(weaponRoot.gameObject);

        foreach (var nodeName in new[] { "BalloonRoot", "IK Chain001", "Line001" })
        {
            Transform node = FindChildRecursive(instance.transform, nodeName);
            if (node != null)
            {
                node.SetParent(transform, true);
                activeToolExtras.Add(node.gameObject);
            }
        }

        SetLayerRecursively(instance.transform, 6);

        wornTool = tool;
    }

    /// <summary>销毁当前工具实例及附属节点</summary>
    public void ClearTool()
    {
        Destroy(activeTool);
        activeTool = null;
        wornTool = null;
        foreach (var extra in activeToolExtras)
        {
            Destroy(extra);
        }
        activeToolExtras.Clear();
    }

    /// <summary>优先取右手食指骨骼，其次右手手掌，再退左手手掌</summary>
    private Transform GetHandBone()
    {
        Transform root = GetBonesRoot();
        Transform finger = FindChildRecursive(root, "Bip001 R Finger11");
        if (finger != null) return finger;
        Transform right = FindChildRecursive(root, "Bip001 R Hand");
        if (right != null) return right;
        return FindChildRecursive(root, "Bip001 L Hand");
    }

    ////////////////////////////////////////眨眼////////////////////////////////////////

    /// <summary>当前表情用的材质实例（首次访问时从 sharedMaterial 克隆，避免修改到共享资产、也避免多个角色互相干扰）</summary>
    private Material eyeMaterial;

    /// <summary>
    /// 表情贴图的UV偏移量。
    /// 实测来源：头部网格 Girl0000_Head 的 38 个眼睛顶点，其 uv1-uv0 恒为 (0.471780, -0.000940)，
    /// 即美术做好的闭眼坐标与睁眼坐标之差。合体烘焙会重排UV，原始uv1的绝对坐标不再适用，
    /// 但这个相对偏移保持不变，所以用它来切左右两半。
    /// </summary>
    private static readonly Vector2 EyeBlinkUVOffset = new Vector2(0.471780f, -0.000940f);

    /// <summary>眨眼调试开关：打开后打印找到的材质与写入的属性，用于排查"不眨眼"。</summary>
    public bool debugEyeBlink = false;   // 排查完请改回 false

    /// <summary>眨眼。true = 闭眼，false = 睁眼。切换表情贴图的 UV 偏移。</summary>
    public void SetEyeBlink(bool shouldBlink)
    {
        Material mat = GetEyeMaterial();
        if (mat == null)
        {
            if (debugEyeBlink) Debug.LogWarning("[Blink] 没找到表情材质，跳过。" + EyeMaterialReport());
            return;
        }

        ApplyBlinkToMaterial(mat, shouldBlink);

        if (debugEyeBlink)
            Debug.Log("[Blink] " + (shouldBlink ? "闭眼" : "睁眼") + " -> 材质 '" + mat.name
                + "' (shader " + (mat.shader == null ? "null" : mat.shader.name) + ")"
                + "  _MaskUVOffset=" + (mat.HasProperty("_MaskUVOffset") ? mat.GetVector("_MaskUVOffset").ToString("F4") : "无此属性")
                + "  _2uvSwitch=" + (mat.HasProperty("_2uvSwitch") ? mat.GetFloat("_2uvSwitch").ToString("F2") : "无此属性"));
    }

    /// <summary>
    /// 取角色表情材质。眼睛由带表情遮罩的那件渲染，识别方式（按优先级）：
    ///   1) 原版 Apowo 双 UV 眼睛 Shader —— 开关叫 _2uvSwitch
    ///   2) 子网格材质名含 Head 的（组合渲染器里 Head 只是其中一个子网格，如 Girl0009_Head(Clone)_Toon）
    ///   3) 带 _SkinMask 的 UTS 表情件
    /// 先从 activeSmrs 找；找不到就退回扫描整个角色层级（LocalPlayer/NetPlayer 上 activeSmrs 可能已失效）。
    /// </summary>
    private Material GetEyeMaterial()
    {
        if (eyeMaterial != null) return eyeMaterial;

        // 第一轮：装备系统记录的渲染器
        Material m1 = FindEyeMaterialIn(GetAllActiveSmrs());
        if (m1 != null) return m1;

        // 第二轮：按层级扫不到的渲染器，改为用「骨骼是否属于本角色」来认领：
        // bones 里含有本 Equip 的 bonesRoot 下的骨骼，即认定属于本角色。
        if (bonesRoot != null)
        {
            var referenced = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer s in UnityEngine.Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
            {
                if (s == null || s.sharedMesh == null) continue;
                if (!s.gameObject.scene.IsValid()) continue;          // 排除预制体资产
                if (s.bones == null || s.bones.Length == 0) continue;

                for (int i = 0; i < s.bones.Length; i++)
                {
                    var b = s.bones[i];
                    if (b == null) continue;
                    if (b == bonesRoot || b.IsChildOf(bonesRoot))
                    {
                        referenced.Add(s);
                        break;
                    }
                }
            }
            Material m2a = FindEyeMaterialIn(referenced);
            if (m2a != null) return m2a;
        }

        // 第三轮：兜底扫本 Equip 及其父根层级（覆盖未合体的情况）
        var fallback = new List<SkinnedMeshRenderer>();
        Transform top = transform;
        while (top.parent != null) top = top.parent;
        fallback.AddRange(top.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        fallback.AddRange(transform.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        Material m2 = FindEyeMaterialIn(fallback);
        if (m2 != null) return m2;

        return null;
    }

    /// <summary>在给定渲染器集合里找表情材质（找到就克隆并写回）。</summary>
    private Material FindEyeMaterialIn(List<SkinnedMeshRenderer> smrs)
    {
        if (smrs == null) return null;

        foreach (SkinnedMeshRenderer smr in smrs)
        {
            if (smr == null) continue;

            Material[] arr = smr.sharedMaterials;
            if (arr == null || arr.Length == 0) continue;

            // 已经克隆过的直接复用（幂等，避免重复克隆）
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null && arr[i].name.EndsWith("_Blink", System.StringComparison.Ordinal))
                {
                    eyeMaterial = arr[i];
                    return eyeMaterial;
                }
            }

            // 找"眼睛材质"：
            //   1) 原版 Apowo 双 UV 眼睛 Shader（开关 _2uvSwitch）
            //   2) 子网格材质名含 Head 的（组合渲染器里 Head 只是其中一个子网格）
            //   3) 带 _SkinMask 的 UTS 表情件
            int idx = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                var m = arr[i];
                if (m != null && m.HasProperty("_2uvSwitch")) { idx = i; break; }
            }
            if (idx < 0)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var m = arr[i];
                    if (m != null && m.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    { idx = i; break; }
                }
            }
            if (idx < 0)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var m = arr[i];
                    if (m != null && m.HasProperty("_UseSecondUV") && m.HasProperty("_SkinMask"))
                    { idx = i; break; }
                }
            }
            // 必须有表情切换开关才认，否则跳过这个 SMR（不要退化成改第一个材质）
            if (idx < 0) continue;
            if (arr[idx] == null) continue;
            if (!arr[idx].HasProperty("_2uvSwitch") && !arr[idx].HasProperty("_MaskUVOffset")) continue;

            Material src = arr[idx];
            eyeMaterial = Instantiate(src);
            eyeMaterial.name = src.name + "_Blink";
            arr[idx] = eyeMaterial;
            smr.sharedMaterials = arr;
            return eyeMaterial;
        }
        return null;
    }

    /// <summary>
    /// 把眨眼状态写到材质上。
    /// 用「遮罩/主贴图 UV 偏移」切换左右两半：
    ///   睁眼 → 偏移 (0, 0)
    ///   闭眼 → 偏移 EyeBlinkUVOffset = (0.47178, -0.00094)（取自网格 uv1-uv0 的实测恒定差）
    /// 之所以不用第二套 UV 的绝对坐标：合体烘焙会重排 UV，原始 uv1（0.877）在图集里已不在闭眼位置，
    /// 但「睁眼到闭眼的相对偏移」不随重排改变，所以偏移方案对源网格与合体网格都成立。
    ///   Apowo 原版双 UV 眼睛 Shader → _2uvSwitch
    /// </summary>
    private static void ApplyBlinkToMaterial(Material mat, bool shouldBlink)
    {
        if (mat == null) return;
        float v = shouldBlink ? 1f : 0f;
        if (mat.HasProperty("_2uvSwitch")) mat.SetFloat("_2uvSwitch", v);
        if (mat.HasProperty("_UseSecondUV")) mat.SetFloat("_UseSecondUV", 0f);
        if (mat.HasProperty("_MaskUVOffset"))
            mat.SetVector("_MaskUVOffset", shouldBlink
                ? new Vector4(EyeBlinkUVOffset.x, EyeBlinkUVOffset.y, 0f, 0f)
                : Vector4.zero); 
    }

    /// <summary>列出当前所有装备材质，用于排查为什么找不到表情材质。</summary>
    public string EyeMaterialReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(" 当前装备材质: ");
        foreach (SkinnedMeshRenderer smr in GetAllActiveSmrs())
        {
            if (smr == null) continue;
            var arr = smr.sharedMaterials;
            for (int i = 0; i < (arr == null ? 0 : arr.Length); i++)
            {
                var m = arr[i];
                if (m == null) continue;
                sb.Append("[").Append(smr.name).Append(":").Append(i).Append("]").Append(m.name)
                  .Append("(").Append(m.shader == null ? "?" : m.shader.name).Append(")")
                  .Append(m.HasProperty("_MaskUVOffset") ? " 有Offset" : "")
                  .Append(m.HasProperty("_2uvSwitch") ? " 有2uvSwitch" : "")
                  .Append("  ");
            }
        }
        return sb.ToString();
    }

    ////////////////////////////////////////编辑器辅助与内部结构////////////////////////////////////////

    private Mesh _bakedCache;

    private void OnDrawGizmosSelected()
    {
        return;
        if (_bakedCache == null) _bakedCache = new Mesh();
        Gizmos.color = Color.green;

        foreach (EClothType slot in new[] { EClothType.BOTTOM, EClothType.STOCKING })
        {
            var smrs = new List<SkinnedMeshRenderer>();
            if (activeSmrs.TryGetValue(slot, out var main) && main != null) smrs.Add(main);
            if (activeSmrExtras.TryGetValue(slot, out var extras) && extras != null)
                foreach (var go in extras)
                    if (go != null) { var s = go.GetComponent<SkinnedMeshRenderer>(); if (s != null) smrs.Add(s); }

            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null) continue;

                smr.BakeMesh(_bakedCache, true);
                var verts = _bakedCache.vertices;
                var m = smr.transform.localToWorldMatrix;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = m.MultiplyPoint3x4(verts[i]);
                    Gizmos.DrawSphere(wp, 0.01f);
#if UNITY_EDITOR
                    if (verts.Length <= 300)
                        UnityEditor.Handles.Label(wp + Vector3.up * 0.02f, i.ToString());
#endif
                }
            }
        }
    }

    [System.Serializable]
    public struct SlotColors
    {
        public Color clothColor;
        public Color clothColor1;

        public SlotColors(Color color1, Color color2)
        {
            clothColor = Color.white;
            clothColor1 = Color.white;
        }
    }
}
