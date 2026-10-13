using UnityEngine;

public class ChangeAvatarCharacter : MonoBehaviour, ICharacterAnimationOwner
{
    public Equip displayCloth;
    // 一个相机
    public Camera renderView;
    public RenderTexture renderTexture;

    [Tooltip("Inspector手动赋值（displayCloth.bonesRoot 同样要预先连到这具骨架）")]
    public GameObject bonesRoot;

    private Animator previewAnimator;

    /// <summary>性别。公开读写：ICharacterAnimationOwner 要求 set，CharacterAnimation 靠它选控制器</summary>
    public bool isGirl { get; set; } = true;

    /// <summary>
    /// 纯展示角色，不做移动/动作状态机。给 CharacterAnimation 一个恒定值即可，
    /// 否则它在 Update 里读 moveState 会直接 NRE。
    /// </summary>
    public EAnimationState moveState { get; set; } = EAnimationState.IDLE;

    public bool IsGirl => isGirl;

    // 应用默认
    void Start()
    {
        ApplyGender(isGirl);
    }

    /// <summary>外部切性别。性别没变就不做（防止重复点同一个按钮时白白重建一次穿戴）</summary>
    public void SetGender(bool newIsGirl)
    {
        if (this.isGirl == newIsGirl) return;
        ApplyGender(newIsGirl);
    }

    /// <summary>恢复默认装。即使性别没变也要重来一遍</summary>
    public void RestoreDefaultOutfit()
    {
        ApplyGender(isGirl);
    }

    /// <summary>
    /// 切性别：骨架不换（男女骨架层级相同），只换外观和动画。
    /// 骨架归一化与 displayCloth.bonesRoot 都交给 Equip（Awake 里统一拉正，
    /// 且依赖 displayCloth.bonesRoot 在 Inspector 里预先连好）。
    /// </summary>
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

        // 穿戴侧：切性别 + 清骨骼缓存 + 按新性别默认装重穿
        displayCloth.SetGender(newIsGirl);

        // 动画侧：换对应性别的 Animator 控制器并重建衣服动画覆盖
        var charAnim = bonesRoot.GetComponentInChildren<CharacterAnimation>(true);
        if (charAnim != null)
            charAnim.SetGender(newIsGirl);

        previewAnimator = bonesRoot.GetComponentInChildren<Animator>(true);
        if (previewAnimator == null)
        {
            PK.Log.Error("骨架里找不到 Animator");
            return;
        }
        previewAnimator.enabled = true;
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
