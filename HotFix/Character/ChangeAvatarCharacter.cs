using UnityEngine;

public class ChangeAvatarCharacter : MonoBehaviour, ICharacter
{
    public Equip displayCloth;
    // 一个相机
    public Camera renderView;
    public RenderTexture renderTexture;

    [Tooltip("Inspector手动赋值（displayCloth.bonesRoot 同样要预先连到这具骨架）")]
    public GameObject bonesRoot;

    [Tooltip("骨架根上的 Animator")]
    public Animator previewAnimator;

    [Tooltip("女性待机控制器")]
    public RuntimeAnimatorController femaleAnimController;

    [Tooltip("男性待机控制器")]
    public RuntimeAnimatorController maleAnimController;

    private static readonly int IdleStateHash = Animator.StringToHash("Idle");

    private bool equipInitialized;

    /// <summary>性别</summary>
    public bool isGirl { get; set; } = true;

    /// <summary>纯展示角色，动作状态恒为待机</summary>
    public EAnimationState moveState { get; set; } = EAnimationState.IDLE;

    public bool IsGirl => isGirl;

    void Start()
    {
        displayCloth.Initialize();
        ApplyGender(isGirl);
    }

    /// <summary>切换性别，性别没变就跳过</summary>
    public void SetGender(bool newIsGirl)
    {
        if (isGirl == newIsGirl) return;
        ApplyGender(newIsGirl);
    }

    /// <summary>恢复默认装</summary>
    public void RestoreDefaultOutfit()
    {
        ApplyGender(isGirl);
    }

    /// <summary>按性别换控制器、换外观</summary>
    private void ApplyGender(bool newIsGirl)
    {
        if (bonesRoot == null)
        {
            PK.Log.Error("bonesRoot 未赋值，请在 Inspector 里把骨架拖上");
            return;
        }

        if (displayCloth == null)
        {
            PK.Log.Error("displayCloth 未赋值，无法换装");
            return;
        }

        isGirl = newIsGirl;

        displayCloth.isNPC = false;
        displayCloth.isGirl = newIsGirl;

        if (previewAnimator == null)
            previewAnimator = bonesRoot.GetComponentInChildren<Animator>(true);

        if (previewAnimator == null)
        {
            PK.Log.Error("骨架里找不到 Animator");
            return;
        }

        previewAnimator.enabled = true;

        if (!equipInitialized)
        {
            displayCloth.Initialize();
            equipInitialized = true;
        }

        PlayIdle();

        displayCloth.SetGender(newIsGirl);
    }

    /// <summary>换上对应性别的基础控制器并播待机</summary>
    private void PlayIdle()
    {
        RuntimeAnimatorController controller = isGirl ? femaleAnimController : maleAnimController;

        if (controller == null)
        {
            PK.Log.Error("ChangeAvatarCharacter 未赋值对应性别的 Animator Controller");
            return;
        }

        previewAnimator.runtimeAnimatorController = controller;
        previewAnimator.Rebind();
        previewAnimator.Play(IdleStateHash, 0, 0f);
        previewAnimator.Update(0f);
    }

    public void Wear(ClothBase newCloth)
    {
        displayCloth.Wear(newCloth);
    }

    public void Remove(EClothType slot)
    {
        displayCloth.Remove(slot);
    }

    public void SetCharacterRotation(float newRotation)
    {
        transform.rotation = Quaternion.Euler(0,newRotation,0);
    }
}
