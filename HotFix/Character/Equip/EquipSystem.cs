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
/// ─── 使用方式 ———
/// 把本脚本挂到角色 GameObject 上，在 Inspector 拖入 bonesRoot（男女共用同一骨架）。
/// 性别和 NPC 标记由外部所有者直接注入；本类不再查找 CharacterBase。
/// 本类仅负责装备的渲染显示，初始装备、穿戴/脱下决策全部由 InventorySystem
/// 控制，本类不自行做任何初始化。单独调用显示接口：
/// 运行时换装：
///     equip.Wear(cloth);          // 穿上一件布料（clothType 决定部位）
///     equip.Remove(EClothType.UPPER);   // 恢复该部位默认着装
///
/// 本系统强制要求每个部位始终有衣服：任何部位不允许为空，也不提供
/// 清空/销毁接口。Wear 传空路径或 null、Remove 脱衣，都会自动恢复默认着装。
/// 注意：本类负责的是渲染相关逻辑，不负责存储，存储在InventorySystem中
/// </summary>
public class Equip : MonoBehaviour
{
    [Tooltip("骨架根（男女共用同一骨架，Inspector 手动赋值）")]
    public Transform bonesRoot;

    [Header("外部注入")]
    [Tooltip("由 LocalPlayer / NPC 在初始化时直接赋值。Equip 不再自动查找 CharacterBase。")]
    public bool isGirl = true;
    [Tooltip("由角色所有者注入。NPC 不参与玩家图层重设。")]
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

    [Header("男性女性头上的浴巾")]
    public GameObject maleHeadTowel;
    public GameObject femaleHeadTowel;

    [Header("每件装备的染色（_ClothColor / _ClothColor1，默认白色=不改色）")]
    public SlotColors hairColors = new SlotColors { clothColor = Color.white, clothColor1 = Color.white };

    [Header("换装后烘焙为 Toon 卡通材质（走 ShaderUtil 烘焙）")]
    [Tooltip("关闭则保持 Apowo 换色材质原样")]
    public bool bakeToToon = true;

    [Header("网格合体（复刻原版 CombineMesh.SkinnedMeshCombiner）")]
    [Tooltip("把已穿戴的部位合并成一个 SkinnedMeshRenderer：只合并网格与骨骼，每个部件保留自己的材质（不做贴图图集）。解决模块之间接缝、动画裂缝、光照不一致的问题")]
    public bool combineMeshes = true;

    // ═══════════════════════════════════════════════════════
    //  原版挂点表（AvatarExtension.BoneRoot + AvatarLoadStatus.AddEquipBoneAndAnimation）
    // ═══════════════════════════════════════════════════════

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

    /// <summary>各部位挂点对应的角色真实骨骼名（原版 AvatarExtension.BoneRoot 的值）</summary>
    private static readonly Dictionary<EClothType, string> SlotMountTarget = new Dictionary<EClothType, string>
    {
        { EClothType.HAIR,     "Bip001 Head" },
        { EClothType.BOTTOM,   "Bip001" },
        { EClothType.GLASSES,  "Bip001 Head" },
        { EClothType.EARRING,  "Bip001 Head" },
        { EClothType.FACE,     "Bip001 Head" },
        { EClothType.SHOE,     "Bip001 R Calf 1" },
        { EClothType.STOCKING, "Bip001 R Calf 1" },
        { EClothType.HAND,     "Bip001 R Hand" },
        { EClothType.UPPER,    "Bip001" },
    };

    /// <summary>按名字取角色骨架上的骨骼（含未激活）</summary>
    private Transform FindBoneByName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        if (boneMap != null && boneMap.TryGetValue(boneName, out Transform cached)) return cached;

        Transform root = GetBonesRoot();
        if (root == null) root = transform;
        return FindChildRecursive(root, boneName);
    }

    // ═══════════════════════════════════════════════════════
    //  Public 事件
    // ═══════════════════════════════════════════════════════

    /// <summary>换装完成通知（CharacterAnimation 用它重建衣服动画覆盖，帧末统一触发）</summary>
    public event System.Action OnEquipChanged;

    // ═══════════════════════════════════════════════════════
    //  Public 方法
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 设定性别并按该性别恢复默认着装。
    /// 即使性别没变也必须重穿一次：调用方（ChangeAvatarCharacter / LocalPlayer / NetPlayer）
    /// 在 Start 里就是靠这一句把默认装穿上，提前 return 会让角色一件衣服都没有。
    /// </summary>
    public void SetGender(bool newIsGirl)
    {
        isGirl = newIsGirl;
        ClearBoneCache();
        ApplyDefaultOutfit();
    }

    /// <summary>
    /// 穿上布料。由 clothType 决定部位、prefabPath 决定模型。
    /// 若布料带 conflictPart，则对应部位的布料不渲染。
    /// </summary>
    public void Wear(ClothBase newCloth)
    {
        if (newCloth == null)
        {
            Log.NullPtr("Wear");
            return;
        }

        EClothType slot = newCloth.clothType;
        if (slot == EClothType.NONE)
        {
            return;
        }

        if (string.IsNullOrEmpty(newCloth.prefabPath))
        {
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
    /// 用一整套装备记录替换当前着装（读档 / 切换角色时用）。
    /// 先恢复默认着装清掉现有记录与显示，再依次穿上 newEquips 里非空的部位；
    /// 记录里为 null 的部位保持默认，不会残留上一次的装备。
    /// </summary>
    public void ApplyEquipped(CharacterEquipCollection newEquips)
    {
        if (newEquips == null)
        {
            Log.NullPtr("EquipSystem.ApplyEquipped");
            return;
        }

        ApplyDefaultOutfit();

        Wear(newEquips.Hair);
        Wear(newEquips.Upper);
        Wear(newEquips.Bottom);
        Wear(newEquips.Stock);
        Wear(newEquips.Shoes);
        Wear(newEquips.Hands);
        Wear(newEquips.Earring);
        Wear(newEquips.Face);
        Wear(newEquips.Glasses);

        ReapplyConflicts();
        animNotifyDirty = true;
    }

    /// <summary>清除缓存的角色骨骼表。重建骨架或切换性别后必须调用。</summary>
    public void ClearBoneCache()
    {
        boneMap = null;
    }

    /// <summary>应用默认着装</summary>
    public void ApplyDefaultOutfit()
    {
        wornCloth.Clear();
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

    /// <summary>
    /// 浴巾造型（纯渲染层）：把头发换成浴巾（按 isGirl 取 femaleHeadTowel / maleHeadTowel），
    /// 上衣/下衣/手/袜子/脚恢复为默认着装（脚无默认鞋则清空显示），已装备的头脸（FACE）保持不变。
    /// 只改显示：不修改 wornCloth 穿戴记录、不触发冲突重算、不影响背包/存档。
    /// 调用 ApplyEquipedSuit() 恢复浴巾造型之前的穿戴显示。
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
    /// 恢复浴巾造型之前的穿戴显示（纯渲染层）：有快照记录的部位按原记录重新穿上，
    /// 原本就是默认着装的部位恢复默认；头脸全程未动。
    /// </summary>
    public void ApplyEquipedSuit()
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

    /// <summary>
    /// 把工具挂到角色骨骼上：WeaponRoot 挂到 Bip001 R Finger11。
    /// SetParent(hand, true) 保留世界旋转（握持朝向），随后忽略 prefab 位置
    /// （localPosition 归零，武器枢轴就在手指骨骼原点），位置完全由骨骼决定。
    /// </summary>
    public void SetTool(ToolBase tool)
    {
        ClearTool();

        GameObject prefab = Resources.Load<GameObject>(tool.prefabPath);
        GameObject instance = Instantiate(prefab, equipRoot);
        instance.name = prefab.name;

        Transform hand = GetHandBone();
        Transform weaponRoot = FindChildRecursive(instance.transform, "WeaponRoot");
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
    }

    /// <summary>卸下当前工具</summary>
    public void RemoveTool()
    {
        ClearTool();
    }

    // ═══════════════════════════════════════════════════════
    //  Private 字段
    // ═══════════════════════════════════════════════════════

    /// <summary>名称 → Transform 的角色骨骼表</summary>
    private Dictionary<string, Transform> boneMap;

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

    /// <summary>合体后的渲染器（所有普通部位合并为一个 SMR）；null = 未合体</summary>
    private SkinnedMeshRenderer combinedSmr;

    /// <summary>合体网格需要重建的脏标记（LateUpdate 统一重建，避免一帧多次重建）</summary>
    private bool meshDirty;

    /// <summary>上一次的 combineMeshes 开关值（运行时开关立即生效，且不会每帧重复重建）</summary>
    private bool lastCombineToggle = true;

    /// <summary>当前挂载到手上的工具实例（斧头/镐子/剑等）</summary>
    private GameObject activeTool;

    /// <summary>换装后置位，LateUpdate 统一通知，避免同帧多次换装重复通知</summary>
    private bool animNotifyDirty;

    /// <summary>工具挂载后残留在角色身上的附属节点（Line001/IK Chain001/BalloonRoot 等）</summary>
    private readonly List<GameObject> activeToolExtras = new List<GameObject>();

    /// <summary>当前每个部位穿着的布料（用于冲突解除后重新渲染）</summary>
    private readonly Dictionary<EClothType, ClothBase> wornCloth
        = new Dictionary<EClothType, ClothBase>();

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

    protected virtual void Start()
    {
        equipRoot = new GameObject("_EquipRoot").transform;
        equipRoot.SetParent(transform, false);
        SetLayerRecursively(transform, 6);
        Context.boneRef = bonesRoot != null ? bonesRoot.gameObject : null;
    }

    /// <summary>帧末统一重建合体网格（换装路径可能一帧内多次改动，避免重复合并；无改动时每帧仅一次布尔判断）</summary>
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
            RebuildCombined();
        }
        else if (combineMeshes != lastCombineToggle)
        {
            // 运行时在 Inspector 开关 combineMeshes → 立即补建/拆除（只在值变化时执行一次，避免每帧空转）
            lastCombineToggle = combineMeshes;
            RebuildCombined();
        }
    }

    /// <summary>
    /// 按当前所有穿着布料的 conflictPart 重新计算并应用隐藏：
    /// 冲突部位被清除不渲染；被解除冲突的部位按记录的布料或默认着装重新渲染。
    ///
    /// 特殊规则：若当前穿着的上装（UPPER）的 conflictPart 包含"下装"
    /// （配置编号 5 → EClothType.BOTTOM，见 ClothBase.ConvertConflictPart），
    /// 则下装（BOTTOM）自身的 conflictPart 不生效（例如长裙类上装盖住下装时，
    /// 被盖住的下装不应再去隐藏其他部位）。
    /// </summary>
    private void ReapplyConflicts()
    {
        // 上装是否为"压制下装冲突"的特殊类型：conflictPart 含 BOTTOM（配置编号 5）
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
            ReapplySlot(slot);
        }
    }

    /// <summary>重新渲染某部位：有记录的布料用布料，否则用默认着装</summary>
    private void ReapplySlot(EClothType slot)
    {
        if (wornCloth.TryGetValue(slot, out ClothBase cloth) && !string.IsNullOrEmpty(cloth.prefabPath))
        {
            GameObject prefab = Resources.Load<GameObject>(cloth.prefabPath);
            if (prefab != null)
            {
                ApplySlot(slot, prefab, GetSlotColors(slot));
                return;
            }
        }
        WearDefault(slot);
    }

    /// <summary>穿上某部位的默认着装（不记录布料）。按 isGirl 取男性/女性默认装备</summary>
    private void WearDefault(EClothType slot)
    {
        GameObject prefab = GetDefaultPrefab(slot);
        if (prefab == null) return;
        ApplySlot(slot, prefab, GetSlotColors(slot));
    }

    /// <summary>某部位的默认装备；没有默认装的部位或未赋值返回 null</summary>
    private GameObject GetDefaultPrefab(EClothType slot)
    {
        switch (slot)
        {
            case EClothType.UPPER: return isGirl ? femaleDefaultUpper : maleDefaultUpper;
            case EClothType.BOTTOM: return isGirl ? femaleDefaultBottom : maleDefaultBottom;
            case EClothType.FACE: return isGirl ? femaleDefaultFace : maleDefaultFace;
            case EClothType.HAIR: return isGirl ? femaleDefaultHair : maleDefaultHair;
            case EClothType.HAND: return isGirl ? femaleDefaultHand : maleDefaultHand;
            case EClothType.STOCKING: return isGirl ? femaleDefaultStocking : maleDefaultStocking;
            default: return null; // GLASSES / EARRING / SHOE 等无默认装
        }
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

    // ═══════════════════════════════════════════════════════
    //  Private 方法 —— 工具
    // ═══════════════════════════════════════════════════════

    /// <summary>销毁当前工具实例及附属节点</summary>
    private void ClearTool()
    {
        Destroy(activeTool);
        activeTool = null;
        foreach (var extra in activeToolExtras)
        {
            Destroy(extra);
        }
        activeToolExtras.Clear();
    }

    /// <summary>骨架根（男女共用同一骨架；NetPlayer / LocalPlayer 也读它来关掉骨骼物理）</summary>
    public Transform GetBonesRoot()
    {
        return bonesRoot;
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

    // ═══════════════════════════════════════════════════════
    //  Private 方法 —— 插槽装配
    // ═══════════════════════════════════════════════════════

    /// <summary>真正销毁某部位当前的装备（不恢复默认）</summary>
    private void ClearSlot(EClothType slot)
    {
        if (activeSmrs.TryGetValue(slot, out SkinnedMeshRenderer oldSmr))
        {
            Destroy(oldSmr.gameObject);
            activeSmrs.Remove(slot);
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
                b.SetParent(null, false);
                Destroy(b.gameObject);
            }
            activeCustomBones.Remove(slot);
        }

        meshDirty = true; // 部位集合变化 → 帧末重建合体网格
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

    /// <summary>
    /// 把单个 SMR 引用的骨骼全部注册到角色的骨骼层级中：
    ///   - 标准 Bip001 骨骼 → 直接引用角色的同名骨骼
    ///   - 自定义骨骼（Woman_Skirt_* / HatRoot 等）→ 沿装备层级向上找到最近的
    ///     角色骨骼，把中间链路原样克隆到角色骨架下
    /// 只做重映射，不做清理（多 SMR 装备需全部重映射后再统一清理，避免误删其他 SMR）。
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
                // 标准骨骼 → 指向角色骨骼
                bones[i] = charBone;
                changed = true;
            }
            else
            {
                // 自定义骨骼 → 在角色层级下创建/定位
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

    // ═══════════════════════════════════════════════════════
    //  Private 方法 —— 材质
    // ═══════════════════════════════════════════════════════

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
        if (smr == null) return;

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

    // ═══════════════════════════════════════════════════════
    //  Private 方法 —— 网格合体（复刻原版 CombineMesh.SkinnedMeshCombiner）
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 把所有已穿戴部位的 SMR 合并成一个 SkinnedMeshRenderer：
    ///   - 顶点/法线/切线/UV 按原样拼接（所有部件同骨架、同 bind pose，可直接拼接）
    ///   - bones 数组按顺序拼接，boneWeights 索引逐个偏移
    ///   - bindposes 按 bones 同序拼接（Mesh.CombineMeshes 不处理 bindpose，必须手动补）
    ///   - 每个部件的材质按子网格顺序保留（不合并材质、不做图集）
    ///   - 带 blendshape 的部件（表情脸等）不参与合体，保持独立渲染
    /// 合体后各部件 SMR 关闭渲染（GameObject 保留，下次换装时按当前部位集合重建）。
    /// 数学依据：SkinnedMeshRenderer 的渲染只依赖 mesh + bones + bindposes，
    /// 不依赖 SMR 自身 Transform，因此各部件 mesh 原样拼接 + bindpose 原样拼接即可，
    /// 与各部件单独渲染时逐顶点的蒙皮结果完全一致。
    /// </summary>
    private void RebuildCombined()
    {
        try
        {
            if (equipRoot == null)
            {
                equipRoot = new GameObject("_EquipRoot").transform;
                equipRoot.SetParent(transform, false);
            }

            if (combinedSmr != null)
            {
                combinedSmr.gameObject.SetActive(false);
                Destroy(combinedSmr.gameObject);
                combinedSmr = null;
            }

            // 收集所有穿戴的 SMR（主 + 多 SMR 装备的额外 SMR）
            List<SkinnedMeshRenderer> allSmrs = GetAllActiveSmrs();

            // 合体关闭或部位不足时：保持各部件独立渲染
            if (!combineMeshes || allSmrs.Count < 2)
            {
                foreach (SkinnedMeshRenderer smr in allSmrs)
                    if (smr != null) smr.enabled = true;
                return;
            }

            // ── 第一遍：筛选可合体的部件（blendshape / 无网格 / 无权重 / 骨骼未绑全的部件保持独立）──
            var sources = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in allSmrs)
            {
                Mesh m = smr.sharedMesh;
                if (m == null || m.vertexCount == 0 || m.subMeshCount == 0) { smr.enabled = true; continue; }
                if (m.blendShapeCount > 0) { smr.enabled = true; continue; }
                if (smr.bones.Length == 0) { smr.enabled = true; continue; }
                // 缺 bindpose 的部件不参与合体：合并时缺的 bindpose 用单位矩阵兜底会把
                // 对应骨骼蒙皮的顶点拉到骨骼位置（裙摆被拉伸到头部）。保持独立渲染。
                if (m.bindposes == null || m.bindposes.Length != smr.bones.Length) { smr.enabled = true; continue; }
                if (m.boneWeights == null || m.boneWeights.Length != m.vertexCount) { smr.enabled = true; continue; }
                bool hasNullBone = false;
                foreach (Transform b in smr.bones)
                {
                    if (b == null) { hasNullBone = true; break; }
                }
                if (hasNullBone) { smr.enabled = true; continue; }
                sources.Add(smr);
            }

            if (sources.Count < 2)
            {
                foreach (SkinnedMeshRenderer smr in allSmrs)
                    if (smr != null) smr.enabled = true;
                return;
            }

            // ── 第二遍：拼接网格数据 ──
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var uvs2 = new List<Vector2>();
            var uvs3 = new List<Vector2>();
            var colors = new List<Color32>();
            var submeshes = new List<List<int>>();
            var materials = new List<Material>();
            var bones = new List<Transform>();
            var bindposes = new List<Matrix4x4>();
            var weights = new List<BoneWeight>();

            bool hasAnyUv = false, hasAnyUv2 = false, hasAnyUv3 = false, hasAnyColor = false;

            foreach (SkinnedMeshRenderer smr in sources)
            {
                Mesh m = smr.sharedMesh;
                int vBase = vertices.Count;
                int boneOffset = bones.Count;
                int vc = m.vertexCount;

                vertices.AddRange(m.vertices);

                if (m.HasVertexAttribute(VertexAttribute.Normal))
                    normals.AddRange(m.normals);
                else
                    for (int i = 0; i < vc; i++) normals.Add(Vector3.up);

                if (m.HasVertexAttribute(VertexAttribute.Tangent))
                    tangents.AddRange(m.tangents);
                else
                    for (int i = 0; i < vc; i++) tangents.Add(new Vector4(1f, 0f, 0f, 1f));

                if (m.HasVertexAttribute(VertexAttribute.TexCoord0)) { uvs.AddRange(m.uv); hasAnyUv = true; }
                else for (int i = 0; i < vc; i++) uvs.Add(Vector2.zero);

                if (m.HasVertexAttribute(VertexAttribute.TexCoord1)) { uvs2.AddRange(m.uv2); hasAnyUv2 = true; }
                else for (int i = 0; i < vc; i++) uvs2.Add(Vector2.zero);

                if (m.HasVertexAttribute(VertexAttribute.TexCoord2)) { uvs3.AddRange(m.uv3); hasAnyUv3 = true; }
                else for (int i = 0; i < vc; i++) uvs3.Add(Vector2.zero);

                if (m.HasVertexAttribute(VertexAttribute.Color)) { colors.AddRange(m.colors32); hasAnyColor = true; }
                else for (int i = 0; i < vc; i++) colors.Add(new Color32(255, 255, 255, 255));

                // bones + bindposes 同序拼接（bindpose 缺失的骨骼用单位矩阵兜底）
                Transform[] partBones = smr.bones;
                Matrix4x4[] partBindposes = m.bindposes;
                for (int i = 0; i < partBones.Length; i++)
                {
                    bones.Add(partBones[i]);
                    bindposes.Add((partBindposes != null && i < partBindposes.Length)
                        ? partBindposes[i] : Matrix4x4.identity);
                }

                // boneWeights：骨骼索引偏移到拼接后的骨骼数组
                BoneWeight[] partWeights = m.boneWeights;
                for (int v = 0; v < partWeights.Length; v++)
                {
                    BoneWeight bw = partWeights[v];
                    bw.boneIndex0 += boneOffset;
                    bw.boneIndex1 += boneOffset;
                    bw.boneIndex2 += boneOffset;
                    bw.boneIndex3 += boneOffset;
                    weights.Add(bw);
                }

                // 子网格按顺序保留，每个子网格对应一个材质
                Material[] mats = smr.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    int[] tris = m.GetTriangles(s);
                    int[] offsetTris = new int[tris.Length];
                    for (int t = 0; t < tris.Length; t++) offsetTris[t] = tris[t] + vBase;
                    submeshes.Add(new List<int>(offsetTris));
                    materials.Add((mats != null && s < mats.Length) ? mats[s] : null);
                }
            }

            // ── 创建合体渲染器 ──
            GameObject go = new GameObject("_CombinedAvatar");
            go.transform.SetParent(equipRoot, false);
            go.layer = 6; // player

            Mesh mesh = new Mesh();
            mesh.name = "CombinedAvatarMesh";
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.tangents = tangents.ToArray();
            if (hasAnyUv) mesh.uv = uvs.ToArray();
            if (hasAnyUv2) mesh.uv2 = uvs2.ToArray();
            if (hasAnyUv3) mesh.uv3 = uvs3.ToArray();
            if (hasAnyColor) mesh.colors32 = colors.ToArray();
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes.ToArray();
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s].ToArray(), s);
            mesh.RecalculateBounds();

            SkinnedMeshRenderer combined = go.AddComponent<SkinnedMeshRenderer>();
            combined.sharedMesh = mesh;
            combined.bones = bones.ToArray();
            combined.sharedMaterials = materials.ToArray(); // 保留各部件材质实例，不做合并
            combined.updateWhenOffscreen = true; // 动画中持续更新包围盒，避免被裁剪

            // ── 关键：合体渲染器必须处在【世界单位变换】下 ──
            // 蒙皮结果是  R⁻¹ · rootBone.worldToLocal · Σ(w_i · bone_i.localToWorld · bindpose_i · v)
            // 逐部件渲染时，R 就是该 SMR 自己的 localToWorld、rootBone 是同一根骨骼，
            // bindpose 也是按此烘焙的 —— 两项抵消，顶点落在正确位置。
            // 合体时顶点与 bindposes 都是【原样拷贝】，这个抵消关系必须自己保证。
            // 原版 CombineMesh 的 cs.target 就是角色根（target.localToWorld == 单位矩阵），
            // 所以这里也必须把合体物体的世界变换清成单位矩阵，否则整个网格被
            // R⁻¹ · rootBone.localToWorld 拧成一坨（表现就是装备/头发拉成一条长条）。
            //
            // 同时 rootBone 取 null：合并后的骨骼数组混了多个部件，各自的 bindpose 基准不同，
            // 单取某一根作 rootBone 只会对其中一个部件正确。
            // 与父级解绑，彻底保证 R == I、lossyScale == (1,1,1)。
            go.transform.SetParent(null, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            combined.rootBone = null;

            // 关闭被合并部件的渲染（GameObject 保留，供下次重建）
            foreach (SkinnedMeshRenderer smr in sources) smr.enabled = false;

            combinedSmr = combined;
        }
        catch (System.Exception e)
        {
            // 合体异常时恢复各部件独立渲染，避免角色丢失（仅异常时执行，正常路径零开销）
            Log.Error("[Equip] 合体异常，已恢复各部件独立渲染：" + e);
            if (combinedSmr != null)
            {
                combinedSmr.gameObject.SetActive(false);
                Destroy(combinedSmr.gameObject);
                combinedSmr = null;
            }
            foreach (SkinnedMeshRenderer smr in GetAllActiveSmrs())
                if (smr != null) smr.enabled = true;
        }
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

    // ═══════════════════════════════════════════════════════
    //  内部数据结构
    // ═══════════════════════════════════════════════════════

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