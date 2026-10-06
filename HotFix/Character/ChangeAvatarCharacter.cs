using UnityEngine;

public class ChangeAvatarCharacter : MonoBehaviour, ICharacterAnimationOwner
{
    public Equip displayCloth;
    // 一个相机
    public Camera renderView;
    public RenderTexture renderTexture;

    /// <summary>
    /// 骨架相对角色根的向下偏移，对齐地面用（原来 CharacterBase 建骨架后手写的 0.126）
    /// </summary>
    private const float SkeletonGroundOffset = 0.126f;

    [Tooltip("Inspector手动赋值")]
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
    /// 先把骨骼根指好，Equip.SetGender 才能往上挂部件。
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

        NormalizeSkeleton();

        displayCloth.isNPC = false;
        displayCloth.bonesRoot = bonesRoot.transform;

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

    /// <summary>
    /// 骨架是预先放在 prefab 里的固定子节点，但它的局部坐标不受代码控制
    /// （拖进来时 Unity 可能保留世界坐标，编辑器里也可能被误动）。
    /// 这里每次切性别都拉回正确位置：局部零位，再往下挪 0.126 对齐地面。
    /// </summary>
    private void NormalizeSkeleton()
    {
        Transform t = bonesRoot.transform;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        t.localPosition = new Vector3(0f, -SkeletonGroundOffset, 0f);
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
