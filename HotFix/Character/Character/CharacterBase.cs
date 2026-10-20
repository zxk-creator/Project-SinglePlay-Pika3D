using UnityEngine;

/// <summary>
/// 所有角色的基类——移动、跳跃、奔跑、地面检测全部内置。
/// 子类只需调Move()告诉基类往哪走，其余自动处理。
/// 注意，关联的脚本的初始化由父脚本（本脚本，NetPlayer等）手动调用，严格保证初始化顺序防止出错！
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterAnimation))]
[RequireComponent(typeof(Equip))]
public abstract class CharacterBase : Actor, ICharacter
{
    ////////////////////////////////////////生命周期////////////////////////////////////////

    [Header("角色动画")]
    public CharacterAnimation characterAnimation;

    [Header("换装系统")]
    [Tooltip("运行时换装系统；留空则在子物体上自动查找")]
    public Equip equipSystem;

    public CharacterProperty property;

    public CharacterBase()
    {
        isNPC = GetType() != typeof(LocalPlayer);
    }

    /// <summary>
    /// 解析组件引用。子类可在自己的Awake里读写这些字段，但不得调用它们的 Initialize。
    /// </summary>
    protected virtual void Awake()
    {
        if (characterAnimation == null) characterAnimation = GetComponentInChildren<CharacterAnimation>();
        if (property == null) property = GetComponent<CharacterProperty>();

        if (movementRb == null) movementRb = GetComponent<Rigidbody>();
        if (characterCapsuleCol == null) characterCapsuleCol = GetComponent<CapsuleCollider>();
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();

        // 获得刚体
        var allRbs = GetComponentsInChildren<Rigidbody>(true);
        limbRbs = System.Array.FindAll(allRbs, r => r != movementRb);
        var allColliders = GetComponentsInChildren<Collider>(true);
        limbColliders = System.Array.FindAll(allColliders, r => r != characterCapsuleCol);

        // Equip 先建好骨架与装备根容器，
        // CharacterAnimation 再据此绑定控制器。
        equipSystem.Initialize();
    }

    protected virtual void Start()
    {
        // 注意：不能放到Awake里面，characterAnimation会读取playerSaveData导致时序问题
        characterAnimation.Initialize(this, equipSystem);

        movementRb.useGravity = true;
        movementRb.freezeRotation = true;
        movementRb.interpolation = RigidbodyInterpolation.Interpolate;
        movementRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        characterCapsuleCol.center = Vector3.up * 1f;

        EnableOrDisableRagdoll(false);

        // 创建高摩擦和低摩擦材质
        runtimeNoFriction = new PhysicsMaterial("CharacterNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f
        };

        runtimeHighFriction = new PhysicsMaterial("CharacterHighFriction")
        {
            dynamicFriction = 0.8f,
            staticFriction = 0.9f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounciness = 0f
        };
    }

    protected virtual void Update()
    {
        UpdateMoveState();

        // 把算好的 moveState 推给动画组件。CharacterAnimation 是纯被动组件
        // （它只负责"怎么播"，不读速度/落地/死亡，也不从宿主拉 moveState），
        // 不在这里推送的话，移动/跳跃/下落这些状态永远播不出来，只剩 IDLE。
        characterAnimation.SetAnimationState(moveState);

        UpdateBlink();
    }

    protected virtual void FixedUpdate()
    {
        // 着地高摩擦，防止站斜坡下滑。空中零摩擦，防止撞墙被吸附
        PhysicsMaterial target = isGrounded ? runtimeHighFriction : runtimeNoFriction;
        characterCapsuleCol.sharedMaterial = target;
    }

    ////////////////////////////////////////物理刚体与胶囊体////////////////////////////////////////

    [Header("物理")]
    public Rigidbody movementRb;
    public CapsuleCollider characterCapsuleCol;

    /// <summary>运行时自动创建的高摩擦材质</summary>
    private PhysicsMaterial runtimeHighFriction;
    /// <summary>运行时自动创建的零摩擦材质</summary>
    private PhysicsMaterial runtimeNoFriction;

    public void SetRigidbodyKinematic(bool kinematic)
    {
        movementRb.isKinematic = kinematic;
    }

    ////////////////////////////////////////地面检测////////////////////////////////////////

    [Header("地面检测")]
    [Tooltip("从胶囊体底部向下检测的距离")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayerMask;

    private bool isGrounded;

    public bool IsGrounded()
    {
        Vector3 realBottom = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        return Physics.CheckSphere(realBottom, groundCheckDistance, groundLayerMask);
    }

    private Vector3 GetGroundNormal()
    {
        Vector3 origin = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        RaycastHit hit;
        if (Physics.Raycast(origin + Vector3.up * 0.2f, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayerMask))
            return hit.normal;
        return Vector3.up;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 realBottom = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        Gizmos.color = IsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(realBottom, groundCheckDistance);
    }

    ////////////////////////////////////////速度与跳跃////////////////////////////////////////

    [Header("速度")]
    public float maxWalkSpeed = 5f;
    public float maxRunSpeed = 10f;

    [Header("跳跃")]
    public float jumpSpeed = 0.5f;

    [HideInInspector] public bool isRunPressed {get; private set;}
    private bool isJumping;

    public void Move(Vector3 velocity)
    {
        isGrounded = IsGrounded();

        if (isGrounded && movementRb.linearVelocity.y <= 0.01f)
            isJumping = false;
    
        Vector3 targetVel = new Vector3(velocity.x, movementRb.linearVelocity.y, velocity.z);

        if (isGrounded)
        {
            Vector3 groundNormal = GetGroundNormal();
            Vector3 horizontal = new Vector3(velocity.x, 0, velocity.z);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                Vector3 slopeMove = Vector3.ProjectOnPlane(horizontal, groundNormal).normalized * horizontal.magnitude;
                if (!isJumping)
                    targetVel.y = slopeMove.y;
                targetVel.x = slopeMove.x;
                targetVel.z = slopeMove.z;
            }
            else
            {
                if (!isJumping)
                    targetVel.y = 0;
                targetVel.x = 0;
                targetVel.z = 0;
            }
        }

        Vector3 diff = targetVel - movementRb.linearVelocity;
        movementRb.AddForce(diff, ForceMode.VelocityChange);
    }

    public void Jump()
    {
        if (!IsGrounded()) return;
        float yDiff = jumpSpeed - movementRb.linearVelocity.y;
        movementRb.AddForce(Vector3.up * yDiff, ForceMode.VelocityChange);
        isJumping = true;
    }

    public void StartRun() { isRunPressed = true; }
    public void StopRun()  { isRunPressed = false; }

    ////////////////////////////////////////移动状态////////////////////////////////////////

    public EAnimationState moveState = EAnimationState.IDLE;

    // maxWalkSpeed, maxRunSpeed 不在接口里：它们是本地移动逻辑的速度上限，
    // 动画侧只用固定的 anim.speed，不需要知道具体速度

    public virtual void UpdateMoveState()
    {
        if (isDead)
        {
            moveState = EAnimationState.DEAD;
            return;
        }

        // 优先判断空中状态
        if (!IsGrounded())
        {
            if (movementRb.linearVelocity.y > 0.1f)
                moveState = EAnimationState.FLY_IDLE;
            else if (movementRb.linearVelocity.y < -0.1f)
                moveState = EAnimationState.FLY_RUN;
            return;
        }

        // 地面状态
        float hSpeedSqr = movementRb.linearVelocity.x * movementRb.linearVelocity.x + movementRb.linearVelocity.z * movementRb.linearVelocity.z;
        if (hSpeedSqr < 0.01f)
            moveState = EAnimationState.IDLE;
        else if (isRunPressed)
            moveState = EAnimationState.RUN;
        else
            moveState = EAnimationState.MOVE;
    }

    ////////////////////////////////////////角色身份与接口////////////////////////////////////////

    public bool isDead {get; private set;} = false;
    public bool isNPC {get; private set;} = false;
    protected bool isGirl = true;

    public bool IsGirl
    {
        get => isGirl;
    }

    // ICharacterAnimationOwner的实现。这几个在CharacterBase上本来就是public字段，
    // 显式实现一份是为了让接口签名和字段解耦，将来改字段名不会破坏接口
    bool ICharacter.isGirl
    {
        get => isGirl;
        set => isGirl = value;
    }

    EAnimationState ICharacter.moveState
    {
        get => moveState;
        set => moveState = value;
    }

    ////////////////////////////////////////生命////////////////////////////////////////

    public void ModifyHealth(float modifyVal)
    {
        if (isDead) return;
        property.AddLifeLoss(modifyVal);
    }

    public virtual void Dead()
    {
        if (isDead) return;
        isDead = true;
        moveState = EAnimationState.DEAD;
        // 通知角色死了
        characterAnimation.OnPlayerDeath();
        // 启用布娃娃
        EnableOrDisableRagdoll(true);

        var beforeDeathLinearV = movementRb.linearVelocity;
        var beforeDeathAngularV = movementRb.angularVelocity;
        // 禁用MovementRb
        SetRigidbodyKinematic(true);
        // 禁用Capsule
        characterCapsuleCol.enabled = false;
        // 为肢体刚体添加速度，防止死亡瞬间戛然而止
        foreach (var limbRb in limbRbs)
        {
            if (limbRb == null) continue;

            limbRb.isKinematic = false; // 启用物理
            limbRb.linearVelocity = beforeDeathLinearV;
            limbRb.angularVelocity = beforeDeathAngularV;
        }

        // 闭眼
        Blink(true);
    }

    /// <summary>
    /// 已废弃，角色目前不能复活。
    /// </summary>
    public virtual void Revive()
    {
        if (!isDead) return;
        isDead = false;
        // 复活动画播完之前都算REVIVE，播完由CharacterAnimation.OnReviveFinished切回 IDLE
        moveState = EAnimationState.REVIVE;
    }

    ////////////////////////////////////////眨眼////////////////////////////////////////

    [Header("眨眼")]
    [Tooltip("两次眨眼之间的间隔")]
    public float blinkInterval = 8.0f;
    [Tooltip("闭眼持续时间")]
    public float blinkDuration = 0.1f;
    [Tooltip("眨眼间隔随机波动范围")]
    public float blinkRandomRange = 1.5f;
    private float blinkTimer;
    private float blinkedTimer;
    private bool isBlinking;

    private void UpdateBlink()
    {
        if (isBlinking)
        {
            blinkedTimer += Time.deltaTime;
            if (blinkedTimer >= blinkDuration)
            {
                Blink(false);
                isBlinking = false;
                blinkedTimer = 0f;
                // 下次眨眼间隔随机化，避免所有角色同步
                blinkTimer = Random.Range(-blinkRandomRange, blinkRandomRange);
            }
        }
        else
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkInterval)
            {
                Blink(true);
                isBlinking = true;
                blinkTimer = 0f;
            }
        }
    }

    /// <param name="shouldBlink">true 闭眼，false 睁眼</param>
    public void Blink(bool shouldBlink)
    {
        equipSystem.SetEyeBlink(shouldBlink);
    }

    ////////////////////////////////////////换装表现////////////////////////////////////////

    /// <summary>切换到浴巾造型（只作为渲染）：头发换浴巾、其余部位恢复默认，头脸不变。见 Equip.ApplyBathSuit</summary>
    public void ApplyBathSuit()
    {
        equipSystem.ApplyBathSuit();
    }

    /// <summary>恢复浴巾造型之前的穿戴显示。见 Equip.ApplyEquipedSuit</summary>
    public void ApplyEquipedSuit()
    {
        equipSystem.ApplyBeforeBathEquipedSuit();
    }

    ////////////////////////////////////////布娃娃////////////////////////////////////////

    // 角色的碰撞刚体
    private Rigidbody[] limbRbs;
    private Collider[] limbColliders;

    /// <summary>
    /// 开启角色的布娃娃系统，用于死亡等
    /// 注意，他仅负责开启布娃娃，至于死了还要干什么，不负责。
    /// </summary>
    /// <param name="enable"></param>
    public void EnableOrDisableRagdoll(bool enable)
    {
        if (!enable)
        {
            foreach (Collider c in limbColliders)
            {
                if (c == null) continue;
                c.enabled = false;
            }

            foreach (Rigidbody r in limbRbs)
            {
                if (r == null) continue;
                r.detectCollisions = false;
                r.isKinematic = true;
            }
        }
        else
        {
            foreach (Collider c in limbColliders)
            {
                if (c == null) continue;
                c.enabled = true;
            }

            foreach (Rigidbody r in limbRbs)
            {
                if (r == null) continue;
                r.detectCollisions = true;
                r.isKinematic = false;
            }
        }
    }

    public void SetGender(bool isGirl)
    {
        if (isGirl == this.isGirl) return;

        equipSystem.SetGender(isGirl);
        characterAnimation.SetGender(isGirl);
    }
}
