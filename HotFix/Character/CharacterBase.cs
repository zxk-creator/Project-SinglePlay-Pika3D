using UnityEngine;

/// <summary>
/// 所有角色的基类——移动、跳跃、奔跑、地面检测全部内置。
/// 物理基于 Rigidbody，LocalPlayer 和 AI 都能用。
/// 子类只需调 Move() 告诉基类往哪走，其余自动处理。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Equip))]
public abstract class CharacterBase : Actor, ICharacterAnimationOwner
{
    [Header("基本")]
    public CharacterAnimation characterAnimation;

    [Header("换装系统")]
    [Tooltip("运行时换装系统；留空则在子物体上自动查找")]
    public Equip equipSystem;

    [Header("物理")]
    public Rigidbody movementRb;
    public CapsuleCollider characterCapsuleCol;
    [Tooltip("true = 不可被推动/推挤（碰撞仍生效，不会穿模；用于NPC等不可推动角色）")]
    public bool immovable;

    /// <summary>运行时自动创建的高摩擦材质（Inspector 未赋值时用）</summary>
    private PhysicsMaterial runtimeHighFriction;
    /// <summary>运行时自动创建的零摩擦材质（Inspector 未赋值时用）</summary>
    private PhysicsMaterial runtimeNoFriction;

    [Header("地面检测")]
    [Tooltip("从胶囊体底部向下检测的距离")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayerMask;

    [Header("速度")]
    public float maxWalkSpeed = 5f;
    public float maxRunSpeed = 10f;

    [Header("跳跃")]
    public float jumpSpeed = 0.5f;

    public CharacterProperty property;
    public EAnimationState moveState = EAnimationState.IDLE;

    // ICharacterAnimationOwner 的实现。这几个在 CharacterBase 上本来就是 public 字段/property，
    // 显式实现一份是为了让接口签名和字段解耦，将来改字段名不会破坏接口
    bool ICharacterAnimationOwner.isGirl
    {
        get => isGirl;
        set => isGirl = value;
    }

    EAnimationState ICharacterAnimationOwner.moveState
    {
        get => moveState;
        set => moveState = value;
    }

    // maxWalkSpeed / maxRunSpeed 不在接口里：它们是本地移动逻辑的速度上限，
    // 动画侧只用固定的 anim.speed，不需要知道具体速度

    public bool isDead {get; private set;} = false;
    public bool isNPC {get; private set;} = false;
    public bool isGirl = true;

    [HideInInspector] public bool isRunPressed {get; private set;}
    private bool isJumping;
    private bool isGrounded;

    public CharacterBase()
    {
        isNPC = this.GetType() != typeof(LocalPlayer);
    }

    protected virtual void Start()
    {
        characterAnimation = GetComponentInChildren<CharacterAnimation>();
        property = GetComponent<CharacterProperty>();

        if (movementRb == null) movementRb = GetComponent<Rigidbody>();
        if (characterCapsuleCol == null) characterCapsuleCol = GetComponent<CapsuleCollider>();
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();

        movementRb.useGravity = true;
        movementRb.freezeRotation = true;
        movementRb.interpolation = RigidbodyInterpolation.Interpolate;
        movementRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (immovable)
            movementRb.constraints = RigidbodyConstraints.FreezeAll;

        characterCapsuleCol.center = Vector3.up * 1f;

        EnableOrDisableRagdoll(false);
    }

    /// <summary>切换到浴巾造型（只作为渲染）：头发换浴巾、其余部位恢复默认，头脸不变。见 Equip.ApplyBathSuit</summary>
    public void ApplyBathSuit()
    {
        equipSystem.ApplyBathSuit();
    }

    /// <summary>恢复浴巾造型之前的穿戴显示。见 Equip.ApplyEquipedSuit</summary>
    public void ApplyEquipedSuit()
    {
        equipSystem.ApplyEquipedSuit();
    }

    protected virtual void Update()
    {
        UpdateMoveState();
    }

    protected virtual void FixedUpdate()
    {
        // 按着地状态切换胶囊体物理材质：
        // 着地 → 高摩擦（站斜坡/地面不下滑）；空中 → 零摩擦（撞墙不被吸附）
        PhysicsMaterial target = isGrounded ? GetHighFrictionMaterial() : GetNoFrictionMaterial();
        characterCapsuleCol.sharedMaterial = target;
    }

    /// <summary>高摩擦材质：Inspector 赋值优先，否则运行时创建（摩擦 1 + Maximum 组合，保证与任何表面接触都取高摩擦）</summary>
    private PhysicsMaterial GetHighFrictionMaterial()
    {
        if (runtimeHighFriction == null)
        {
            runtimeHighFriction = new PhysicsMaterial("CharacterHighFriction");
            runtimeHighFriction.dynamicFriction = 0.8f;
            runtimeHighFriction.staticFriction = 0.9f;
            runtimeHighFriction.frictionCombine = PhysicsMaterialCombine.Average;
            runtimeHighFriction.bounciness = 0f;
        }
        return runtimeHighFriction;
    }

    /// <summary>零摩擦材质：Inspector 赋值优先，否则运行时创建</summary>
    private PhysicsMaterial GetNoFrictionMaterial()
    {
        if (runtimeNoFriction == null)
        {
            runtimeNoFriction = new PhysicsMaterial("CharacterNoFriction");
            runtimeNoFriction.dynamicFriction = 0f;
            runtimeNoFriction.staticFriction = 0f;
            runtimeNoFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
            runtimeNoFriction.bounciness = 0f;
        }
        return runtimeNoFriction;
    }

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

        movementRb.linearVelocity = new Vector3(0,movementRb.linearVelocity.y,0);
    }

    public virtual void Revive()
    {
        if (!isDead) return;
        isDead = false;
        // 复活动画播完之前都算 REVIVE，播完由 CharacterAnimation.OnReviveFinished 切回 IDLE
        moveState = EAnimationState.REVIVE;
    }

    public bool IsGrounded()
    {
        Vector3 realBottom = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        return Physics.CheckSphere(realBottom, groundCheckDistance, groundLayerMask);
    }

    /// <summary>
    /// 开启角色的布娃娃系统，用于死亡等
    /// 注意，他仅负责开启布娃娃，至于死了还要干什么，不负责。
    /// </summary>
    /// <param name="enable"></param>
    public void EnableOrDisableRagdoll(bool enable)
    {
        if (!enable)
        {
            // 游戏开始时，取消骨骼的碰撞，原本预留用于布娃娃的，但是实际上没必要
            Transform root = equipSystem.bonesRoot;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true))
            {
                r.detectCollisions = false;
                r.isKinematic = true;
            }
        }
        else
        {
            // 死亡的时候开启布娃娃
            Transform root = equipSystem.bonesRoot;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                c.enabled = true;
            foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true))
            {
                r.detectCollisions = true;
                r.isKinematic = false;
            }
        }
    }

    private Vector3 GetGroundNormal()
    {
        Vector3 origin = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        RaycastHit hit;
        if (Physics.Raycast(origin + Vector3.up * 0.2f, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayerMask))
            return hit.normal;
        return Vector3.up;
    }

    public void SetRigidbodyKinematic(bool kinematic)
    {
        movementRb.isKinematic = kinematic;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 realBottom = transform.position + characterCapsuleCol.center - Vector3.up * (characterCapsuleCol.height * 0.45f);
        Gizmos.color = IsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(realBottom, groundCheckDistance);
    }
}
