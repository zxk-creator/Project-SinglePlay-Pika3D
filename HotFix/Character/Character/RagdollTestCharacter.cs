using UnityEngine;

// 这个角色存在的意义就是为了测试布娃娃系统。
public class RagdollTestCharacter : MonoBehaviour, ICharacter
{
    // 换装系统，需要Inspector赋值
    public Equip equipSystem;

    public bool isGirl { get => true;}
    public EAnimationState moveState { get => EAnimationState.IDLE; set {} }
    bool ICharacter.isGirl { get => isGirl; set {} }

    // 动画系统？暂时不需要
    void Start()
    {
        equipSystem.Initialize();
        // 先Apply默认装备
        equipSystem.ApplyDefaultOutfit();
        // 开局全身骨骼设置为非kemematic
        Transform root = equipSystem.bonesRoot;
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            c.enabled = true;
        foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true))
        {
            r.detectCollisions = true;
            r.isKinematic = false;
        }

        // 直接闭眼
        equipSystem.SetEyeBlink(true);
    }

    void Update()
    {
        
    }
}
