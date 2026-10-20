using PK;
using PKWeb;
using UnityEngine;
using UnityEngine.AddressableAssets;

// 网络玩家的真身：纯显示用，位置，朝向，动画全部由服务端下发。
// 每个玩家拥有一个WorldSpaceName，我们需要获取到他并使用
public class NetPlayer : Actor, ICharacter
{
    public Equip equipSystem;
    public CharacterAnimation characterAnimation;

    // 包含的网络玩家存储数据
    public NetPlayerData netPlayer;
    // 位置同步缓冲区
    public LocationSyncBuffer syncBuffer = new LocationSyncBuffer();

    public bool isGirl { get; set; } = true;
    public EAnimationState moveState { get; set; } = EAnimationState.IDLE;
    // 头上的悬浮式玩家名称
    private WorldSpaceName worldSpaceName;

    private void Awake()
    {
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();
        if (characterAnimation == null) characterAnimation = GetComponentInChildren<CharacterAnimation>();

        equipSystem.isGirl = isGirl;
        equipSystem.Initialize();
        characterAnimation.Initialize(this, equipSystem);

        // WorldSpaceName
        worldSpaceName = GetComponentInChildren<WorldSpaceName>();

        // 获得刚体
        var allRbs = GetComponentsInChildren<Rigidbody>(true);
        limbRbs = System.Array.FindAll(allRbs, r => r != GetComponent<Rigidbody>());
        var allColliders = GetComponentsInChildren<Collider>(true);
        limbColliders = System.Array.FindAll(allColliders, r => r != GetComponent<CapsuleCollider>());

        EnableOrDisableRagdoll(false);
    }

    /// <summary>
    /// 设置网络玩家穿戴的装备
    /// </summary>
    public void SetOutfit(CharacterEquipCollection characterEquip, bool newIsGirl)
    {
        Context.updateProxy.RegisterNextFrameTask(() =>
        {
            equipSystem.ApplyEquipped(characterEquip);
            if (isGirl != newIsGirl)
            {
                isGirl = newIsGirl;
                equipSystem.SetGender(isGirl);
                characterAnimation.SetGender(isGirl);
            }
        });
    }

    ///////////////////////////////////////// 插值 ////////////////////////////////////////////////
    void Update()
    {
        var s = syncBuffer.ReadSync();
        if (!s.isValid) return;

        transform.position = s.sync.location;
        transform.rotation = s.sync.rotation;
    }

    // 网络同步位置与朝向 
    public void SetTransform(Vector3 newPosition, Quaternion newRotation)
    {
        // 如果是大跳变直接瞬移，不再用缓冲区
        float dist = Vector3.Distance(transform.position, newPosition);
        if (dist > 16f)
        {
            syncBuffer.Clear();
            transform.position = newPosition;
            transform.rotation = newRotation;
        }

        var sync = new OtherPlayerLocationSync
        {
            playerName = netPlayer.playerName,
            location = newPosition,
            rotation = newRotation,           
            currentAnimation = moveState,    
        };
        syncBuffer.AddOneSync(new TransformSyncSnapshot(sync, Time.realtimeSinceStartup));
    }

    public void SetPlayerName(string newName)
    {
        worldSpaceName.SetName(newName);
    }

    // 设置玩家死亡
    public void SetPlayerDead(Vector3 deathVelocity,Vector3 deathAngularVelocity)
    {
        characterAnimation.OnPlayerDeath();
        // 开启布娃娃
        EnableOrDisableRagdoll(true);
        // 赋值速度
        foreach (Rigidbody r in limbRbs)
        {
            if (r == null) continue;
            r.linearVelocity = deathVelocity;
            r.angularVelocity = deathAngularVelocity;
        }

        // 关闭胶囊体
        GetComponent<CapsuleCollider>().enabled = false;
        // 闭眼
        equipSystem.SetEyeBlink(true);
        // 隐藏名字
        worldSpaceName.SetVisible(false);
    }

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

    /// <summary>
    /// 直接设置动画来播放动画
    /// </summary>
    /// <param name="newState"></param>
    public void SetAnimation(EAnimationState newState)
    {
        characterAnimation.SetAnimationState(newState);
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return (null, null, null, EInteractableType.None, this);
    }

    public override void StartInteract(Vector3 charPos, CharacterBase interactChar)
    {
    }
}
