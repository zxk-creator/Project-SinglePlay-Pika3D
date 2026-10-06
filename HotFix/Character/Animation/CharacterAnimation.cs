using System.Collections.Generic;
using UnityEngine;
using PK;


public class CharacterAnimation : MonoBehaviour, IDeathAndRevive
{
    public RuntimeAnimatorController femaleAnimController;
    public RuntimeAnimatorController maleAnimController;
    // 服装
    public Equip equipSystem;

    private const float CROSSFADE_DURATION = 0.15f;
    private string lastAnimName = "";
    private EAnimationState moveState;
    private bool initialized;
    private bool isDead;
    private bool isReviving;
    public System.Action onReviveFinished;
    // 正在做动作被占用，不播放IdleExtension动画
    public bool isDoingAction = false;
    /// <summary>当前动作状态名（坐Sit/躺Lie/洗澡Bath等）。动作期间锁定动画不被移动状态切换，换装重建后自动补播</summary>
   public string actionStateName = "";

    /// <summary>
    /// 强制动画模式。为 true 时不再按速度/落地/死亡等条件自己判断播哪个动画，
    /// 完全由外部设置 EMoveState 决定（见 SetAnimationState）
    /// </summary>
    public bool bForceSetAnimationState = false;

    /// <summary>强制模式下当前已套用的移动状态，用来判断外部是否改了值</summary>
    private EAnimationState forcedMoveState;
    private bool forcedApplied;

    [Tooltip("待机小动作状态名（控制器 Base Layer 里必须存在）")]
    public string idleExtensionStateName = "IdleExtension";
    [Tooltip("站定多久后随机触发待机小动作（秒）")]
    public float idleExtensionMinDelay = 4f;
    public float idleExtensionMaxDelay = 8f;
    [Tooltip("各性别的待机小动作 clip 池")]
    public AnimationClip[] femaleIdleExtensions;
    public AnimationClip[] maleIdleExtensions;
    private bool isPlayingIdleExtension;
    private float idleExtensionTimer;
    private float idleExtensionDelay;
    private float idleExtensionClipLength;
    private float idleExtensionStartTime;

    [Tooltip("骨架根上的 Animator。必须在 Inspector 里手动拖，不再自动查找。装备部件（如 Boy0280_Hand）自带 Animator，自动找会找到部件上导致 A-pose")]
    [SerializeField] private Animator anim;
    public ICharacterAnimationOwner characterBase;
    private AnimatorOverrideController overrideController;

    /// <summary>基础控制器，重建衣服动画覆盖时用它收集占位 clip</summary>
    private RuntimeAnimatorController baseController;

    /// <summary>换装后置位，下一帧重建衣服动画覆盖</summary>
    private bool pendingOverrideRebuild;

    /// <summary>衣服层前缀:（部位槽位, 动画目录名）。对应 hotfix/equips/animations/{gender}/clothing/{目录}/...</summary>
    private static readonly (string prefix, EClothType slot, string folder)[] PartPrefixMap =
    {
        ("Hair",  EClothType.HAIR,   "hair"),
        ("Coat",  EClothType.UPPER,  "coat"),
        ("Skirt", EClothType.BOTTOM, "skirt"),
    };

    void Awake()
    {
        ResolveOwner();
        if (characterBase == null)
        {
            Log.Error("未找到 ICharacterAnimationOwner，无法订阅换装事件");
            return;
        }
        if (equipSystem == null)
        {
            Log.Error("equipSystem 为 null，无法订阅换装事件（衣服动画加载将失败）");
        }
        else
        {
            equipSystem.OnEquipChanged += OnEquipChangedHandler;
        }
        if (characterBase is NPC) return;
        LocalPlayer.RegisterDeathAndReviveEvents(this);
    }

    /// <summary>
    /// 找宿主和换装系统：优先用 Inspector 已赋的值，没赋就自己找。
    /// 必须自己找一次 —— characterBase 一旦为空，Initialize 里的 characterBase.isGirl 就直接 NRE，
    /// 而且 Update 每帧都会重试 Initialize，会刷屏。
    /// </summary>
    private void ResolveOwner()
    {
        if (characterBase == null)
        {
            var owner = GetComponentInParent<ICharacterAnimationOwner>();
            if (owner == null) owner = GetComponent<ICharacterAnimationOwner>();
            characterBase = owner;
        }
        if (equipSystem == null)
        {
            equipSystem = GetComponentInParent<Equip>();
            if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();
        }
    }

    private void OnDestroy()
    {
        if (characterBase != null && equipSystem != null)
            equipSystem.OnEquipChanged -= OnEquipChangedHandler;
        LocalPlayer.UnregisterDeathAndReviveEvents(this);
    }

    /// <summary>换装完成 → 下一帧重建衣服动画覆盖</summary>
    private void OnEquipChangedHandler()
    {
        pendingOverrideRebuild = true;
    }

    private void Initialize()
    {
        // Awake 时可能还没把自己挂到宿主下面（骨架是运行时 Instantiate 的），这里再补一次
        ResolveOwner();
        if (characterBase == null)
        {
            Log.Error("[CharacterAnimation] 找不到 ICharacterAnimationOwner（宿主），无法初始化动画");
            return;
        }
        // 骨架烘在 prefab 里了，Animator 由 Inspector 直接指定。
        // 不再自动查找：装备部件（如 Boy0280_Hand）自带 Animator，
        // GetComponentInChildren 会先遍历到它们，控制器赋到部件上、骨架 Animator 为空 → A-pose。
        if (anim == null)
        {
            Log.Error("[CharacterAnimation] Animator 未赋值，请在 Inspector 里拖上骨架根上的 Animator");
            return;
        }
        RuntimeAnimatorController controller = characterBase.isGirl ? femaleAnimController : maleAnimController;
        if (controller == null)
            throw new UnityException("CharacterAnimation: 未赋值 femaleAnimController/maleAnimController，请检查 Inspector 配置");
        overrideController = new AnimatorOverrideController
        {
            runtimeAnimatorController = controller
        };
        anim.runtimeAnimatorController = overrideController;
        baseController = controller;
        RebuildAnimationOverrides(); // 加载当前穿着的衣服动画（平时衣服播自身动画）
        // 原版 AvatarBase.PlayAnima：主体 + 4 个衣服层（Hair/Coat/Skirt/Wing）同步播放
        string[] requiredStates = { "Idle", "Run", "FlyIdle", "FlyRun", idleExtensionStateName };
        foreach (string stateName in requiredStates)
        {
            if (!anim.HasState(0, Animator.StringToHash(stateName)))
                throw new UnityException("Animator Controller 缺少状态 '" + stateName + "'，请检查 controller 配置");
        }
        string[] layerPrefixes = { "Hair", "Coat", "Skirt", "Wing" };
        for (int layer = 1; layer <= layerPrefixes.Length; layer++)
        {
            string layerIdle = layerPrefixes[layer - 1] + "Idle";
            if (!anim.HasState(layer, Animator.StringToHash(layerIdle)))
                throw new UnityException("Animator Controller 第 " + layer + " 层缺少状态 '" + layerIdle + "'，请检查 controller 配置");
        }
    }

    /// <summary>
    /// 切换性别：换用对应性别的 Animator 控制器并重建衣服动画覆盖。
    /// 只负责动画侧；骨架重建、穿衣、骨骼缓存由 Equip.SetGender 负责，两者需一起调用。
    /// 不做 "性别没变就跳过" 的判断：重建骨架后即使性别不变，控制器和衣服动画覆盖也得重建，
    /// 是否真的需要切换由调用方判断。
    /// 若尚未 Initialize（骨架没生成），这里直接跳过 —— Initialize 会自己按 characterBase.isGirl 取控制器。
    /// </summary>
    public void SetGender(bool newIsGirl)
    {
        if (characterBase == null)
        {
            Log.Error("SetGender: characterBase 为 null（Awake 未找到 CharacterBase）");
            return;
        }

        characterBase.isGirl = newIsGirl;

        // 控制器还没建起来 → 无需换
        if (anim == null || overrideController == null) return;

        RuntimeAnimatorController controller = newIsGirl ? femaleAnimController : maleAnimController;
        if (controller == null)
        {
            Log.Error("[CharacterAnimation] SetGender: 对应性别的 Animator Controller 未赋值，动画不会切换");
            return;
        }

        // AnimatorOverrideController 的 runtimeAnimatorController 不能直接换控制器，
        // 必须整个重建一个，否则覆盖映射还挂在旧控制器上
        overrideController = new AnimatorOverrideController
        {
            runtimeAnimatorController = controller
        };
        anim.runtimeAnimatorController = overrideController;
        baseController = controller;

        // 按新性别重新加载衣服动画（哪些层要覆盖由 PartPrefixMap 决定）
        RebuildAnimationOverrides();

        // 重建控制器会复位状态机：清播放记忆、清待机小动作状态，
        // 让下一帧 Update 按当前移动状态重新 CrossFade
        lastAnimName = "";
        isPlayingIdleExtension = false;
        idleExtensionTimer = 0f;
        idleExtensionDelay = 0f;
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
            if (anim == null)
            {
                // 骨架未就绪 → 不置位，下一帧重试
                return;
            }
            initialized = true;
            // 刚给 Animator 换控制器的一帧不要急着 CrossFade：
            // 状态机需要一整帧完成新控制器初始化，同帧发的 CrossFade 可能被丢弃，
            // 而 lastAnimName 一旦记下 "Idle" 就会永久挡住重试 → 卡 A-pose。
            // 这一帧直接跳过播放，下一帧再正常发起。
            lastAnimName = "";
            return;
        }

        if (pendingOverrideRebuild)
        {
            pendingOverrideRebuild = false;
            RebuildAnimationOverrides(); // 换装后重建衣服动画

            // 重建会重设 Animator（状态机复位、lastAnimName 清空）：
            // 若正处于动作中（如浴巾换装后的 Bath），立即补播动作动画，避免被默认/Idle 顶掉
            if (isDoingAction && !string.IsNullOrEmpty(actionStateName))
                PlayAnimationLayer(actionStateName, 0);
        }

        // 强制模式：不检测速度/落地/死亡，只认外部设定的 moveState。
        // 重建衣服动画会复位播放记忆，所以这里每帧都比对一次名字，不一致就重播。
        if (bForceSetAnimationState)
        {
            EAnimationState wanted = moveState;
            (string name, bool allLayers) = MoveStateToAnimation(wanted);

            if (!forcedApplied || forcedMoveState != wanted || lastAnimName != name)
                ApplyForcedAnimation(wanted, name, allLayers);

            return;
        }

        if (isDead)
            return;

        // Revive 动画播放期间不切换状态动画；播完自动切回 Idle（不依赖外部回调时序）
        if (isReviving)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Revive") && stateInfo.normalizedTime >= 1f)
            {
                isReviving = false;
                lastAnimName = "";
                PlayLocomotionAllLayers("Idle");
                onReviveFinished?.Invoke();
                onReviveFinished = null;
            }
            return;
        }

        // 动作（坐/躺/洗澡等）期间锁定动画：不按移动状态切换，避免 Bath/Lie/Sit 被 Idle 顶掉
        if (isDoingAction)
            return;

        // 状态由宿主算好（CharacterBase 按速度/落地/跑键算 UpdateMoveState），这里只用
        moveState = characterBase.moveState;
        UpdateIdleExtension();
        PlayAnimationForCurrentState();
    }

    /// <summary>
    /// 站定一段时间后随机触发一次 IdleExtension（待机小动作），播完自动回到 Idle。
    /// 通过 AnimatorOverrideController 覆盖状态 "IdleExtension" 的 clip，
    /// 让同一条控制器在不同性别/不同角色身上都能播各自的扩展待机动画。
    /// </summary>
    private void UpdateIdleExtension()
    {
        if (isDoingAction) return;
        
        if (moveState != EAnimationState.IDLE)
        {
            if (isPlayingIdleExtension)
            {
                isPlayingIdleExtension = false;
                lastAnimName = "";
            }
            idleExtensionTimer = 0f;
            idleExtensionDelay = 0f;
            return;
        }

        if (isPlayingIdleExtension)
        {
            if (Time.time - idleExtensionStartTime >= idleExtensionClipLength)
            {
                isPlayingIdleExtension = false;
                lastAnimName = "";
                idleExtensionTimer = 0f;
                idleExtensionDelay = 0f;
            }
            return;
        }

        if (idleExtensionDelay <= 0f)
            idleExtensionDelay = Random.Range(idleExtensionMinDelay, idleExtensionMaxDelay);

        idleExtensionTimer += Time.deltaTime;
        if (idleExtensionTimer >= idleExtensionDelay)
        {
            AnimationClip[] pool = characterBase.isGirl ? femaleIdleExtensions : maleIdleExtensions;
            if (pool == null || pool.Length == 0)
                return; // 未配置待机小动作池，静默跳过
            AnimationClip clip = pool[Random.Range(0, pool.Length)];
            if (clip == null)
                return;
            overrideController[idleExtensionStateName] = clip;
            // AnimatorOverrideController 修改后必须重新赋值，Animator 才会重载新的 override 映射
            anim.runtimeAnimatorController = overrideController;
            idleExtensionClipLength = clip.length;
            idleExtensionStartTime = Time.time;
            isPlayingIdleExtension = true;
            idleExtensionTimer = 0f;
            idleExtensionDelay = 0f;
            anim.CrossFade(idleExtensionStateName, CROSSFADE_DURATION, 0, 0f);
            lastAnimName = idleExtensionStateName;
        }
    }

    /// <summary>
    /// 按当前穿着衣服的系列，把控制器 Hair/Coat/Skirt 层里的占位 clip 替换为对应衣服的动画
    /// （hotfix/equips/animations/{gender}/clothing/{部位}/{系列}/{clip}，clip 名 = 系列+部位+状态）。
    /// 平时衣服层播真实衣服动画。
    /// 逐部位统计并输出：系列、命中数、缺失数；完全没有动画时明确 Warn，不静默。
    /// </summary>
    private void RebuildAnimationOverrides()
    {
        if (anim == null || overrideController == null || baseController == null)
        {
            return;
        }
        if (characterBase == null || equipSystem == null)
        {
            Log.Error("重建衣服动画覆盖失败：CharacterBase 或 Equip 组件缺失");
            return;
        }

        string genderDir = characterBase.isGirl ? "female" : "male";
        string animRoot = "hotfix/equips/animations/" + genderDir + "/clothing/";

        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        var stats = new List<string>(); // 每部位汇总："Hair(Girl0001): 12/14"

        foreach (var m in PartPrefixMap)
        {
            string prefix = m.prefix;
            EClothType slot = m.slot;
            string folder = m.folder;

            // 收集该前缀下的占位 clip（控制器层状态引用的空占位，如 HairIdle / CoatRun）
            var placeholders = new List<AnimationClip>();
            foreach (AnimationClip clip in baseController.animationClips)
            {
                if (clip != null && !string.IsNullOrEmpty(clip.name)
                    && clip.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    placeholders.Add(clip);
            }
            if (placeholders.Count == 0)
            {
                // Initialize 已校验过 Hair/Coat/SkirtIdle 状态存在，正常不会走到
                continue;
            }

            string series = equipSystem.GetWornClothSeries(slot);
            if (string.IsNullOrEmpty(series))
            {
                stats.Add(prefix + ": 未穿着（无系列）");
                continue;
            }

            string seriesDir = series.ToLowerInvariant();
            string basePath = animRoot + folder + "/" + seriesDir;
            int found = 0, missing = 0;
            foreach (AnimationClip placeholder in placeholders)
            {
                string clipName = series + placeholder.name; // 系列+部位+状态 → Girl0001CoatIdle
                AnimationClip real = Resources.Load<AnimationClip>(basePath + "/" + folder + "/" + clipName);
                if (real == null)
                    real = Resources.Load<AnimationClip>(basePath + "/" + clipName); // 男裙等无子目录的情况
                if (real == null)
                {
                    missing++;
                    continue;
                }
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(placeholder, real));
                found++;
            }
            stats.Add(prefix + "(" + series + "): " + found + "/" + (found + missing));
        }

        if (pairs.Count > 0)
        {
            overrideController.ApplyOverrides(pairs);
            // AnimatorOverrideController 修改后必须重新赋值，Animator 才会重载新的 override 映射
            anim.runtimeAnimatorController = overrideController;
        }
        else
        {
            // 当前着装完全没有可用动画 → override 保持基础控制器映射（空覆盖），并明确提示（不静默）
            anim.runtimeAnimatorController = overrideController;
        }

        // 重新赋值控制器会重置状态机：清空播放记忆，下一帧按当前移动状态重新 CrossFade
        lastAnimName = "";
        isPlayingIdleExtension = false;
    }

    /// <summary>
    /// 强制模式入口：外部直接指定要播的动画状态，不再依赖速度/落地/死亡等检测。
    /// 会把 bForceSetAnimationState 打开，之后每帧按 moveState 播对应动画。
    /// </summary>
    public void SetAnimationState(EAnimationState state)
    {
        if (characterBase == null)
        {
            Log.Error("characterBase为null！");
            return;
        }

        bForceSetAnimationState = true;
        moveState = state;

        // 骨架/控制器还没就绪时不播，等 Initialize 完成后的第一帧 Update 补上
        if (anim == null || overrideController == null) return;

        (string name, bool allLayers) = MoveStateToAnimation(state);
        ApplyForcedAnimation(state, name, allLayers);
    }

    private void ApplyForcedAnimation(EAnimationState state, string animName, bool allLayers)
    {
        // 走路和跑步共用 "Run"，靠播放速度区分
        anim.speed = (state == EAnimationState.RUN) ? 2f : 1f;

        if (allLayers)
            PlayLocomotionAllLayers(animName);
        else
            PlayAnimationLayer(animName, 0);

        lastAnimName = animName;
        forcedMoveState = state;
        forcedApplied = true;
    }

    /// <summary>EMoveState → 控制器状态名 + 是否要同步播 5 个层（衣服层有没有该状态由资产决定）</summary>
    private static (string name, bool allLayers) MoveStateToAnimation(EAnimationState state)
    {
        switch (state)
        {
            case EAnimationState.IDLE:     return ("Idle", true);
            case EAnimationState.MOVE:     return ("Run", true);      // 走：1 倍速
            case EAnimationState.RUN:      return ("Run", true);      // 跑：2 倍速
            case EAnimationState.FLY_IDLE: return ("FlyIdle", true);
            case EAnimationState.FLY_RUN:  return ("FlyRun", true);
            case EAnimationState.DEAD:     return ("Dead", true);
            case EAnimationState.REVIVE:   return ("Revive", true);
            case EAnimationState.SIT:      return ("Sit", true);      // 衣服层有 HairSit/CoatSit/SkirtSit/WingSit
            case EAnimationState.LIE:      return ("Lie", false);     // 只有主体层有该状态
            case EAnimationState.BATH:     return ("Bath", false);    // 只有主体层有该状态
            default:
                throw new UnityException(
                    $"CharacterAnimation: EMoveState.{state} 还没有对应的动画状态名，" +
                    "请在 MoveStateToAnimation 里补上");
        }
    }

    private void PlayAnimationForCurrentState()
    {
        string animName;

        switch (moveState)
        {
            case EAnimationState.FLY_IDLE:
                animName = "FlyIdle";
                break;
            case EAnimationState.FLY_RUN:
                animName = "FlyRun";
                break;
            case EAnimationState.MOVE:
            case EAnimationState.RUN:
                animName = "Run";
                break;
            case EAnimationState.IDLE:
            default:
                animName = "Idle";
                break;
        }

        // 走路和跑步共用控制器的 "Run" 状态，靠播放速度区分：走 1 倍速、跑 2 倍速
        anim.speed = (moveState == EAnimationState.RUN) ? 2f : 1f;

        // IdleExtension 播放期间停留在该状态，不切回 Idle（由 UpdateIdleExtension 播完后再放行）
        if (isPlayingIdleExtension && animName == "Idle")
            return;

        if (string.IsNullOrEmpty(animName) || animName == lastAnimName)
            return;

        PlayLocomotionAllLayers(animName);

        lastAnimName = animName;
    }

    public void PlayAnimationLayer(string stateName, int layer)
    {
        bool hasState = anim.layerCount > layer && anim.HasState(layer, Animator.StringToHash(stateName));
        if (hasState)
        {
            anim.CrossFade(stateName, CROSSFADE_DURATION, layer, 0f);
            return;
        }
        Log.Error("CharacterAnimation: Animator Controller 第 " + layer + " 层缺少状态 '" + stateName + "'，请检查 controller 配置");
    }

    /// <summary>
    /// 常规移动状态（Idle/Run/FlyIdle/FlyRun）同步播到 5 个播放层：
    /// 主体层用原名，衣服层用 "层前缀 + 名字"（HairIdle/CoatRun/SkirtFlyIdle/WingFlyRun）。
    /// </summary>
    public void PlayLocomotionAllLayers(string stateName)
    {
        PlayAnimationLayer(stateName, 0);
        PlayAnimationLayer("Hair" + stateName, 1);
        PlayAnimationLayer("Coat" + stateName, 2);
        PlayAnimationLayer("Skirt" + stateName, 3);
        PlayAnimationLayer("Wing" + stateName, 4);
    }

    /// <summary>
    /// 当前实际在播的动画类型（供上报给服务端转发给别人），直接用 EMoveState 表示。
    /// 判定顺序与 Update 里的优先级严格一致：死亡 → 复活 → 动作 → 待机小动作 → 移动状态。
    /// </summary>
    public EAnimationState GetPlayingAnimationType()
    {
        // 还没初始化完（LocalPlayer.Update 可能比本脚本的 Update 先跑）：
        // 这一刻确实没在播任何动作，报 IDLE 是准确答案，不该抛异常。
        // 抛的话每帧一条，只是在刷屏。
        if (!initialized || anim == null)
            return EAnimationState.IDLE;

        if (isDead) return EAnimationState.DEAD;
        if (isReviving) return EAnimationState.REVIVE;

        // 动作占用期间锁的是动作动画，不是移动动画
        if (isDoingAction)
        {
            switch (actionStateName)
            {
                case "Idle": return EAnimationState.IDLE;   // 淋浴：普通站立
                case "Sit":  return EAnimationState.SIT;
                case "Lie":  return EAnimationState.LIE;
                case "Bath": return EAnimationState.BATH;
                default:
                    throw new UnityException(
                        $"CharacterAnimation: 动作动画 '{actionStateName}' 还没有对应的 EMoveState，" +
                        "新增动作时请在 EMoveState 里补一个值并在 GetPlayingAnimationType 里映射");
            }
        }

        // 待机小动作属于站立表现的一类
        if (isPlayingIdleExtension) return EAnimationState.IDLE;

        switch (moveState)
        {
            // 移动状态本身就是最好的动画类型，直接返回。
            // MOVE / RUN 共用控制器的 "Run" 状态，靠 anim.speed 区分（1 倍速走 / 2 倍速跑）。
            case EAnimationState.IDLE:
            case EAnimationState.MOVE:
            case EAnimationState.RUN:
            case EAnimationState.FLY_IDLE:
            case EAnimationState.FLY_RUN:
                return moveState;

            default:
                throw new UnityException(
                    $"CharacterAnimation: 移动状态 {moveState} 还没有对应处理，" +
                    "请在 GetPlayingAnimationType 里补上");
        }
    }

    public void OnReviveFinished()
    {
        isReviving = false;
        lastAnimName = "";
        isPlayingIdleExtension = false;
        // 复活播完，移动状态从 REVIVE 切回 IDLE
        if (characterBase != null)
            moveState = EAnimationState.IDLE;
        // 强制切回 Idle（主体 + 各衣服层），避免停留在 Revive 最后一帧
        PlayLocomotionAllLayers("Idle");
    }

    public void OnPlayerDeath()
    {
        if (!initialized)
        {
            Initialize();
            if (anim == null)
                return; // 骨架未就绪，无法播放死亡动画，下一帧 Update 重试初始化
            initialized = true;
        }
        isDead = true;
        lastAnimName = "";
        isPlayingIdleExtension = false;
        // 强制所有层播放 Dead（Base 原名 Dead，衣服层带前缀 HairDead/CoatDead/SkirtDead/WingDead）
        PlayLocomotionAllLayers("Dead");
    }

    public void OnPlayerRevive()
    {
        if (!initialized)
        {
            Initialize();
            if (anim == null)
                return; // 骨架未就绪，无法播放复活动画，下一帧 Update 重试初始化
            initialized = true;
        }
        isDead = false;
        isReviving = true;
        lastAnimName = "";
        isPlayingIdleExtension = false;
        // 强制所有层播放 Revive（Base 原名 Revive，衣服层带前缀 HairRevive/CoatRevive/SkirtRevive/WingRevive）
        PlayLocomotionAllLayers("Revive");
    }

    /// <summary>
    /// 获取Revive动画时长（用于延迟复活流程）
    /// </summary>
    /// <returns></returns>
    public float GetReviveAnimTime()
    {
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Revive")
                return clip.length;
        }
        return 0f;
    }
}
