using PK;
using PKWeb;
using UnityEngine;

// 网络玩家的真身：纯显示用，位置/朝向/动画全部由服务端下发。
public class NetPlayer : Actor, ICharacterAnimationOwner
{
    public Equip equipSystem;
    public CharacterAnimation characterAnimation;

    // 包含的网络玩家存储数据
    public NetPlayerData netPlayer;

    public bool isGirl { get; set; } = true;
    public EAnimationState moveState { get; set; } = EAnimationState.IDLE;

    // 等待骨架建好之后再套用的着装
    private CharacterEquipCollection pendingOutfit;
    private bool pendingIsGirl = true;
    private bool pendingOutfitReady;

    private void Awake()
    {
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();
        if (characterAnimation == null) characterAnimation = GetComponentInChildren<CharacterAnimation>();
    }

    void Start()
    {
        if (equipSystem == null)
        {
            Log.Error("[NetPlayer] 没有 Equip 组件，无法显示其他玩家");
            return;
        }

        if (pendingOutfit != null)
        {
            isGirl = pendingIsGirl;
            equipSystem.SetGender(isGirl);
        }

        TryApplyOutfit();
    }

    // 网络那边发过来信号就调用这个。
    // 注意：着装必须在骨架就绪之后才能挂，否则 equipSystem 没骨骼可挂。
    // 生成玩家的人可能在 Start 跑之前就调，所以这里做一次等待，到 Start 之后自动补上。
    public void SetOutfit(CharacterEquipCollection characterEquip, bool newIsGirl)
    {
        pendingOutfit = characterEquip;
        pendingIsGirl = newIsGirl;
        pendingOutfitReady = false;

        TryApplyOutfit();
    }

    private void TryApplyOutfit()
    {
        if (pendingOutfitReady || pendingOutfit == null) return;
        // 骨架由 prefab 提供，equipSystem 拿不到骨骼根就说明 prefab 没配好
        if (equipSystem.GetBonesRoot() == null)
        {
            Log.Error("[NetPlayer] prefab 上没配好骨架（Equip.bonesRoot 没赋值），无法穿装备");
            return;
        }

        equipSystem.ApplyEquipped(pendingOutfit);
        pendingOutfitReady = true;
    }

    // 网络同步：位置 + 朝向 + 动画
    public void SetTransform(Vector3 newPosition, Quaternion newRotation)
    {
        transform.SetPositionAndRotation(newPosition, newRotation);
    }

    // 直接设置动画
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
