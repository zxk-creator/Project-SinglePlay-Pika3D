using System.Collections.Generic;
using UnityEngine;
using PK;

// 本类不强依赖CharacterBase，只需要一个接口ICharacterAnimationOwner
// 为的就是解耦，让任何人都能用，所以尽量别乱改
public class CharacterAnimation : MonoBehaviour, IDeathAndRevive
{
    ////////////////////////////////////////生命周期与初始化////////////////////////////////////////
    public RuntimeAnimatorController femaleAnimController;
    public RuntimeAnimatorController maleAnimController;
    // 服装
    public Equip equipSystem;

    [Tooltip("骨架根上的Animator。必须在Inspector里手动拖，不再自动查找。装备部件自带 Animator，自动找会找到部件上导致 A-pose")]
    [SerializeField] private Animator anim;

    public ICharacter characterBase;

    /// <summary>
    /// 是否用 AnimatorOverrideController 承载衣服层动画替换。
    /// 打开＝正常流程（衣服层播自身动画）；关闭＝直接把基础控制器交给 Animator，
    /// 衣服层动画替换失效，用于排查"角色完全不播动画"是不是这层包装导致的。
    /// </summary>
    [Tooltip("打开＝用 AnimatorOverrideController 做衣服动画替换（正常）。关闭＝直接用基础控制器，仅用于排查动画不播的问题。")]
    public bool useClothOverrideController = true;

    private AnimatorOverrideController overrideController;

    /// <summary>基础控制器，重建衣服动画覆盖时用它收集占位 clip</summary>
    private RuntimeAnimatorController baseController;

    public const float CROSSFADE_DURATION = 0.15f;

    private static readonly string[] layerPrefixes = { "Hair", "Coat", "Skirt", "Wing" };

    private void OnDestroy()
    {
        if (equipSystem != null)
            equipSystem.OnEquipChanged -= OnEquipChangedHandler;
        if (characterBase is LocalPlayer) {}
            LocalPlayer.UnregisterDeathAndReviveEvents(this);
        
        // 把角色身上的Animator也销毁了
        Destroy(anim);
    }

    /// <summary>
    /// 由使用者显式调用此方法来初始化动画系统，宿主与换装系统必须已就绪。
    /// </summary>
    public void Initialize(ICharacter owner, Equip equip)
    {
        if (owner == null)
            throw new UnityException("[CharacterAnimation] owner 为 null");
        if (equip == null)
            throw new UnityException("[CharacterAnimation] equip 为 null");
        if (anim == null)
            throw new UnityException("[CharacterAnimation] Animator 未赋值，请在 Inspector 里拖上骨架根上的 Animator");

        characterBase = owner;
        equipSystem = equip;

        RuntimeAnimatorController controller = owner.isGirl ? femaleAnimController : maleAnimController;
        if (controller == null)
            throw new UnityException("CharacterAnimation: 未赋值 femaleAnimController/maleAnimController，请检查 Inspector 配置");

        string[] requiredStates = { "Idle", "Run", "FlyIdle", "FlyRun", idleExtensionStateName };
        foreach (string stateName in requiredStates)
        {
            if (!anim.HasState(0, Animator.StringToHash(stateName)))
                throw new UnityException("Animator Controller 缺少状态 '" + stateName + "'，请检查 controller 配置");
        }
        for (int layer = 1; layer <= layerPrefixes.Length; layer++)
        {
            string layerIdle = layerPrefixes[layer - 1] + "Idle";
            if (!anim.HasState(layer, Animator.StringToHash(layerIdle)))
                throw new UnityException("Animator Controller 第 " + layer + " 层缺少状态 '" + layerIdle + "'，请检查 controller 配置");
        }

        if (useClothOverrideController)
        {
            overrideController = new AnimatorOverrideController
            {
                runtimeAnimatorController = controller
            };
            anim.runtimeAnimatorController = overrideController;
        }
        else
        {
            // 排除法开关：不套 AnimatorOverrideController，直接把基础控制器交给 Animator。
            // 代价是衣服层的动画替换失效，用来确认主状态（Idle/Run/...）本身是否正常。
            overrideController = null;
            anim.runtimeAnimatorController = controller;
        }
        baseController = controller;

        equipSystem.OnEquipChanged += OnEquipChangedHandler;
        if (owner is LocalPlayer) {}
            // LocalPlayer.RegisterDeathAndReviveEvents(this);

        animState = EAnimationState.IDLE;
        idleExtensionPlaying = false;
        idleExtensionTimer = 0f;
        idleExtensionDelay = 0f;
        lastAnimName = "";
    }

    ////////////////////////////////////////性别切换////////////////////////////////////////

    /// <summary>
    /// 换用对应性别的Animator控制器并重建衣服动画。(不会设置父类的isGirl字段。只会管自己的)
    /// </summary>
    public void SetGender(bool newIsGirl)
    {
        RuntimeAnimatorController controller = newIsGirl ? femaleAnimController : maleAnimController;
        if (controller == null)
            throw new UnityException("CharacterAnimation.SetGender: 对应性别的 Animator Controller 未赋值，请检查 Inspector 配置");

        if (useClothOverrideController)
        {
            // AnimatorOverrideController 的 runtimeAnimatorController 不能直接换控制器，
            // 必须整个重建一个，否则覆盖映射还挂在旧控制器上
            overrideController = new AnimatorOverrideController
            {
                runtimeAnimatorController = controller
            };
            anim.runtimeAnimatorController = overrideController;
        }
        else
        {
            overrideController = null;
            anim.runtimeAnimatorController = controller;
        }
        baseController = controller;

        RebuildAnimationOverrides();

        lastAnimName = "";
        idleExtensionPlaying = false;
        idleExtensionTimer = 0f;
        idleExtensionDelay = 0f;
    }

    ////////////////////////////////////////每帧驱动////////////////////////////////////////

    private void Update()
    {
        if (pendingOverrideRebuild)
        {
            pendingOverrideRebuild = false;
            RebuildAnimationOverrides(); // 换装后重建衣服动画

            // 重建会重设 Animator（状态机复位）。这里只清播放记忆，
            // 让紧接着的 ApplyInjectedState 按当前 animState 重播一遍 ——
            // 坐着/躺着换装（如浴巾）时就会补播回 Sit/Lie，不会被 Idle 顶掉。
            lastAnimName = "";
        }

        // 唯一驱动源：外部通过 SetAnimationState 注入的 animState。
        // 本组件不再读 characterBase.moveState，也不按速度/落地/死亡自己判断。
        // 顺序：待机小动作（只认 IDLE）→ 状态分发。
        UpdateIdleExtension();
        ApplyInjectedState();
    }

    ////////////////////////////////////////待机小动作////////////////////////////////////////

    [Tooltip("待机小动作状态名")]
    public string idleExtensionStateName = "IdleExtension";
    [Tooltip("站定多久后随机触发待机小动作（秒）")]
    public float idleExtensionMinDelay = 4f;
    public float idleExtensionMaxDelay = 8f;
    [Tooltip("各性别的待机小动作 clip 池")]
    public AnimationClip[] femaleIdleExtensions;
    public AnimationClip[] maleIdleExtensions;
    private float idleExtensionTimer;
    private float idleExtensionDelay;
    private float idleExtensionClipLength;
    private float idleExtensionStartTime;

    /// <summary>
    /// 待机小动作是否正在播放。这是小动作自己的独立状态标志 —— 不能用 animState 兼任：
    /// 宿主每帧都会把 moveState（IDLE）推过来，若用 animState 表示"小动作播放中"，
    /// 这个临时状态会被宿主的推送立刻盖掉，小动作一帧就被顶掉、再也播不出来。
    /// 播放期间状态机由小动作独占，宿主的推送不得抢占。
    /// </summary>
    private bool idleExtensionPlaying;

    /// <summary>
    /// 站定一段时间后随机触发一次 IdleExtension（待机小动作），播完自动回到 Idle。
    /// 通过 AnimatorOverrideController 覆盖状态 "IdleExtension" 的 clip，
    /// 让同一条控制器在不同性别/不同角色身上都能播各自的扩展待机动画。
    /// </summary>
    private void UpdateIdleExtension()
    {
        if (isDead) return;

        // 只有 IDLE（准备触发小动作）和"小动作播放中"这两种情况允许往下走。
        // SIT/LIE/BATH/SHOWER 等"被动作占用"的状态一律不放行 —— 尤其 SHOWER 的动作动画名也是 Idle，
        // 跟普通站立在控制器上无法区分，必须靠枚举把它挡在外面。计时同时清零，起身后重新计。
        if (animState != EAnimationState.IDLE && !idleExtensionPlaying)
        {
            idleExtensionTimer = 0f;
            idleExtensionDelay = 0f;
            return;
        }

        // 待机小动作播完回到IDLE，交回正常分发
        if (idleExtensionPlaying)
        {
            if (Time.time - idleExtensionStartTime >= idleExtensionClipLength)
            {
                idleExtensionPlaying = false;
                animState = EAnimationState.IDLE;
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
            if (overrideController == null) return;   // 未启用覆盖控制器时不播小动作
            overrideController[idleExtensionStateName] = clip;
            // AnimatorOverrideController修改后必须重新赋值，Animator才会重载新的 override 映射
            anim.runtimeAnimatorController = overrideController;
            idleExtensionClipLength = clip.length;
            idleExtensionStartTime = Time.time;
            idleExtensionTimer = 0f;
            idleExtensionDelay = 0f;
            // 把有效状态切到IDLE_EXTENSION。否则下一帧ApplyInjectedState会按 IDLE
            // 算出"Idle"并 CrossFade，小动作一帧就被自己顶掉；同时 switch 的
            // case IDLE_EXTENSION 也才走得到。
            animState = EAnimationState.IDLE_EXTENSION;
            // 标记小动作播放中：从这一刻起状态机由小动作独占，宿主的 IDLE 推送不能抢占
            idleExtensionPlaying = true;
            anim.CrossFade(idleExtensionStateName, CROSSFADE_DURATION, 0, 0f);
            lastAnimName = idleExtensionStateName;
        }
    }

    ////////////////////////////////////////衣服动画覆盖////////////////////////////////////////
    /// <summary>衣服层前缀:（部位槽位, 动画目录名）。对应 hotfix/equips/animations/{gender}/clothing/{目录}/...</summary>
    private static readonly (string prefix, EClothType slot, string folder)[] PartPrefixMap =
    {
        ("Hair",  EClothType.HAIR,   "hair"),
        ("Coat",  EClothType.UPPER,  "coat"),
        ("Skirt", EClothType.BOTTOM, "skirt"),
    };

    /// <summary>换装后置位，下一帧重建衣服动画覆盖</summary>
    private bool pendingOverrideRebuild;

    /// <summary>换装完成 → 下一帧重建衣服动画覆盖</summary>
    private void OnEquipChangedHandler()
    {
        pendingOverrideRebuild = true;
    }

    /// <summary>
    /// 按当前穿着衣服的系列，把控制器Hair/Coat/Skirt层里的占位clip替换为对应衣服的动画
    /// 存储路径必须按照（hotfix/equips/animations/{gender}/clothing/{部位}/{系列}/{clip}，clip 名 = 系列+部位+状态）。
    /// 平时衣服层播真实衣服动画。
    /// 逐部位统计并输出：系列命中数缺失数，完全没有动画时Warn。
    /// </summary>
    private void RebuildAnimationOverrides()
    {
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
                // Initialize已校验过Hair/Coat/SkirtIdle状态存在，正常不会走到
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

        if (overrideController == null)
        {
            // useClothOverrideController 关闭时不套 override controller，无需重建衣服覆盖
            lastAnimName = "";
            return;
        }

        // ── 重建完整覆盖表 ──
        // AnimatorOverrideController.ApplyOverrides 是「累加」的：它把条目并进内部覆盖表，
        // 既不会清除上一次留下的条目，也不会清除历史遗留的 null 条目。
        // 一旦某个主状态（Idle/Run/FlyIdle/FlyRun 等）在覆盖表里被映射成 null，
        // 该状态就播不到任何 clip —— 表现为 Animator 状态机的 normalizedTime 正常推进，
        // 但骨骼一根都不动（实测 layer0 当前 clip 数 = 0）。
        //
        // 所以这里不只追加本帧的 pairs，而是取全部覆盖条目重算一遍：
        //   - 衣服占位（Hair/Coat/Skirt/Wing/Tail/Shoes 开头）→ 用本帧加载到的真实衣服动画
        //   - 其它条目（Idle/Run/Dead/... 等主状态）→ 一律复位回基础控制器的同名 clip
        //   - 两边都为 null 的条目丢弃
        // 这样无论之前谁往覆盖表里写过 null，每次重建都会把它修回来。
        var full = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(full);

        var baseByName = new Dictionary<string, AnimationClip>();
        if (baseController != null)
        {
            foreach (AnimationClip c in baseController.animationClips)
            {
                if (c != null && !baseByName.ContainsKey(c.name))
                    baseByName[c.name] = c;
            }
        }

        var clothPairs = new Dictionary<string, AnimationClip>();
        foreach (var kv in pairs)
        {
            if (kv.Key != null)
                clothPairs[kv.Key.name] = kv.Value;
        }

        int resetCount = 0;
        for (int i = 0; i < full.Count; i++)
        {
            AnimationClip key = full[i].Key;
            if (key == null) continue;

            AnimationClip target;
            if (clothPairs.TryGetValue(key.name, out AnimationClip cloth))
            {
                target = cloth;                       // 衣服占位 → 真实衣服动画
            }
            else
            {
                baseByName.TryGetValue(key.name, out target);   // 主状态 → 基础 clip
                if (target != null && full[i].Value != target) resetCount++;
            }
            full[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, target);
        }

        overrideController.ApplyOverrides(full);
        // AnimatorOverrideController 修改后必须重新赋值，Animator 才会重载新的 override 映射
        anim.runtimeAnimatorController = overrideController;

        if (resetCount > 0)
            Log.Info("[CharacterAnimation] 覆盖表里有 " + resetCount + " 条无效条目已复位回基础 clip（原本会播不出动画）");

        // 重新赋值控制器会重置状态机：清空播放记忆，下一帧按当前 animState 重新 CrossFade
        lastAnimName = "";
    }

    ////////////////////////////////////////状态注入与分发////////////////////////////////////////
    private string lastAnimName = "";

    /// <summary>
    /// 当前动画状态。这是本组件唯一的驱动源：外部通过 <see cref="SetAnimationState"/>
    /// 你需要对其进行注入，本组件只负责怎么播，不检测速度，落地，死亡，也不从宿主的moveState拉取。
    /// 默认IDLE，Controller就绪后由Update自动应用一次。
    /// </summary>
    [SerializeField] private EAnimationState animState = EAnimationState.IDLE;

    /// <summary>
    /// 这是外部注入入口，指定要播的动画状态。是本组件唯一的驱动方式，你只需要调用这个，状态机由外部自己实现
    /// 注意IDLE_EXTENSION无意义，因为他是由组件内部逻辑管理，会被当作IDLE。
    /// </summary>
    public void SetAnimationState(EAnimationState state)
    {
        if (state == EAnimationState.IDLE_EXTENSION)
        {
            Log.Info("传入了IDLE_EXTENSION，已按IDLE处理");
            state = EAnimationState.IDLE;
        }

        // 待机小动作播放期间：
        //   宿主推来的 IDLE 直接丢弃 —— 小动作就是站立表现，不能被 IDLE 抢回主体层。
        //   否则会出现：小动作刚 CrossFade 到 IdleExtension，下一帧宿主推 IDLE 就把状态抢回 IDLE、
        //   立刻 CrossFade("Idle") 覆盖，然后小动作分支又把状态改回 IDLE_EXTENSION……
        //   表现为「一播就回到 IDLE + 全身每帧抽搐」，直到小动作时长耗尽才停。
        //   推来的是别的状态（移动/跳跃/下落/死亡等）则立即打断小动作，交回正常分发 ——
        //   否则小动作会在角色已经走起来之后还站着播。
        if (idleExtensionPlaying)
        {
            if (state == EAnimationState.IDLE) return;

            idleExtensionPlaying = false;
            idleExtensionTimer = 0f;
            idleExtensionDelay = 0f;
        }

        // 幂等：状态没变化且播放记忆还在，就直接返回。
        // 宿主会在 Update 里每帧推送当前状态，若无条件往下走，
        // 每次都会清掉 lastAnimName，使 ApplyInjectedState 的
        // 「名字没变就不重播」守卫失效 → 每帧一次 CrossFade → 状态机被反复重启
        // （实测 normalizedTime 从 0.0353 回绕到 0.0065，动画永远播不完，表现就是定格在一个姿势）。
        //
        // lastAnimName 为空表示"播放记忆已被清掉、需要重播"（例如待机小动作播完后
        // UpdateIdleExtension 自己把状态改回 IDLE 并清了记忆），这种情况必须放行，
        // 否则会永远回不到 Idle。
        if (animState == state && !string.IsNullOrEmpty(lastAnimName)) return;

        animState = state;

        lastAnimName = "";
        ApplyInjectedState();
    }

    /// <summary>
    /// 按注入的 <see cref="animState"/> 分发播放。
    /// 唯一的状态动画映射点：状态名、是否同步播5层，anim.speed都在这里决定。
    /// </summary>
    private void ApplyInjectedState()
    {
        // 复活期间不切别的状态；Revive播完自动回 Idle（见 Update 里的自愈判断，
        // 外部 LocalPlayer 的 RegisterDelayTask 仍会兜底再调一次 OnReviveFinished）
        if (isReviving && animState == EAnimationState.REVIVE)
        {
            AnimatorStateInfo reviveInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (reviveInfo.IsName("Revive") && reviveInfo.normalizedTime >= 1f)
            {
                OnReviveFinished();
                return;
            }
        }

        string animName;
        bool allLayers = true;

        switch (animState)
        {
            case EAnimationState.IDLE:
                animName = "Idle";
                break;
            case EAnimationState.MOVE:              // 走：与 RUN 共用 "Run" 状态，1 倍速
            case EAnimationState.RUN:               // 跑：2 倍速
                animName = "Run";
                break;
            case EAnimationState.FLY_IDLE:
                animName = "FlyIdle";
                break;
            case EAnimationState.FLY_RUN:
                animName = "FlyRun";
                break;
            case EAnimationState.DEAD:
                animName = "Dead";
                break;
            case EAnimationState.REVIVE:
                animName = "Revive";
                break;
            case EAnimationState.SIT:
                animName = "Sit";
                break;
            case EAnimationState.LIE:
                animName = "Lie";
                allLayers = false;                  // 只有主体层有该状态
                break;
            case EAnimationState.BATH:
                animName = "Bath";
                allLayers = false;                  // 只有主体层有该状态
                break;
            case EAnimationState.SHOWER:
                // 淋浴：动作动画就是普通站立 Idle，只有主体层播。
                // 它跟 IDLE 的区别只在"不允许触发待机小动作"（见 UpdateIdleExtension）。
                animName = "Idle";
                allLayers = false;
                break;
            case EAnimationState.IDLE_EXTENSION:
                // 待机小动作：只在 IDLE 下由 UpdateIdleExtension 负责计时与播放，
                // 这里不参与分发
                return;
            default:
                throw new UnityException(
                    $"CharacterAnimation: EAnimationState.{animState} 还没有对应的动画状态名，" +
                    "请在 ApplyInjectedState 里补上");
        }

        // 走路和跑步共用控制器的 "Run" 状态，靠播放速度区分：走 1 倍速、跑 2 倍速
        anim.speed = (animState == EAnimationState.RUN) ? 2f : 1f;

        // 注：这里不再需要「待机小动作期间别用 Idle 顶掉」的守卫 ——
        // 小动作播放时 animState 就是 IDLE_EXTENSION，上面的 switch 已经提前 return。

        if (animName == lastAnimName)
            return;

        if (allLayers)
        {
            PlayAnimationLayer(animName, 0);
            PlayAnimationLayer("Hair" + animName, 1);
            PlayAnimationLayer("Coat" + animName, 2);
            PlayAnimationLayer("Skirt" + animName, 3);
            PlayAnimationLayer("Wing" + animName, 4);
        }
        else
            PlayAnimationLayer(animName, 0);

        lastAnimName = animName;
    }

    /// <summary>
    /// 当前实际在播的动画类型，可用于上报给服务端转发给别人
    /// </summary>
    public EAnimationState GetPlayingAnimationType()
    {
        return animState == EAnimationState.IDLE_EXTENSION ? EAnimationState.IDLE : animState;
    }

    ////////////////////////////////////////播放原语////////////////////////////////////////

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

    ////////////////////////////////////////死亡与复活////////////////////////////////////////

    private bool isDead;
    private bool isReviving;
    public System.Action onReviveFinished;

    public void OnPlayerDeath()
    {
        isDead = true;
        anim.enabled = false;
    }

    // 暂时废弃，角色不能复活
    public void OnPlayerRevive()
    {
        isDead = false;
        isReviving = true;
        // 走统一注入：Revive（主体层 "Revive"，衣服层带前缀 HairRevive/CoatRevive/SkirtRevive/WingRevive）
        SetAnimationState(EAnimationState.REVIVE);
    }

    // 暂时废弃，角色不能复活，也就没有复活结束
    public void OnReviveFinished()
    {
        isReviving = false;
        // 复活播完，交回 IDLE
        SetAnimationState(EAnimationState.IDLE);
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
