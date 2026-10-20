using System.Collections.Generic;
using UnityEngine;
using PK;

/// <summary>
/// 布娃娃自碰撞抑制器。挂在角色骨架根（Equip.bonesRoot 指向的那个节点）上，
/// 由生成/修复工具烘入"同一具骨架的全部碰撞体"清单。
///
/// 为什么需要它：Unity 的 <see cref="CharacterJoint.enableCollision"/> 只抑制
/// **关节直接相连的那一对**刚体。而人形布娃娃的碰撞体是按骨段自动生成的，
/// 非相邻的两节（骨盆↔大腿、左右大腿、躯干↔上臂…）在静止姿态下本来就互相重叠 ——
/// 这些配对不受 enableCollision 控制，会持续产生穿透接触，解算器把两者往外顶、
/// 关节又把它们拉回来，表现为肢体持续抽搐。
///
/// 这里在启用物理时一次性把这具骨架内部所有碰撞体两两互相忽略，
/// 只保留"与外部世界（地面/建筑/其他角色）"的碰撞。
/// </summary>
public class RagdollSelfCollision : MonoBehaviour
{
    [Tooltip("这具骨架内部的全部碰撞体。启用布娃娃时两两互相忽略。")]
    public Collider[] selfColliders;

    [Tooltip("勾上则本组件在 Awake 时自动应用一次。一般不需要：由布娃娃开关在启用物理时调用 ApplyToPhysics()。")]
    public bool applyOnAwake = false;

    private bool applied;

    private void Awake()
    {
        if (applyOnAwake) ApplyToPhysics();
    }

    /// <summary>
    /// 让这具骨架内部所有碰撞体两两互相忽略。可重复调用（幂等）。
    /// 必须在把布娃娃刚体置为非运动学**之前或同时**调用，避免第一帧就产生自穿透冲量。
    /// </summary>
    public void ApplyToPhysics()
    {
        if (applied) return;
        if (selfColliders == null || selfColliders.Length < 2)
        {
            PK.Log.Error("[RagdollSelfCollision] selfColliders 为空或不足 2 个，无法抑制自碰撞（骨架根：" + name + "）");
            return;
        }

        int pairs = 0;
        for (int i = 0; i < selfColliders.Length; i++)
        {
            var a = selfColliders[i];
            if (a == null) continue;
            for (int j = i + 1; j < selfColliders.Length; j++)
            {
                var b = selfColliders[j];
                if (b == null) continue;
                Physics.IgnoreCollision(a, b, true);
                pairs++;
            }
        }

        applied = true;
        PK.Log.Info("[RagdollSelfCollision] 已忽略 " + pairs + " 对骨架内部碰撞（" + selfColliders.Length + " 个碰撞体）");
    }

    /// <summary>撤销抑制，恢复骨架内部自碰撞（一般只用于调试对比）。</summary>
    public void RevertPhysics()
    {
        if (selfColliders == null) return;
        for (int i = 0; i < selfColliders.Length; i++)
        {
            var a = selfColliders[i];
            if (a == null) continue;
            for (int j = i + 1; j < selfColliders.Length; j++)
            {
                var b = selfColliders[j];
                if (b == null) continue;
                Physics.IgnoreCollision(a, b, false);
            }
        }
        applied = false;
    }

#if UNITY_EDITOR
    /// <summary>编辑器里由修复工具调用：校验清单是否覆盖了骨架下全部碰撞体。</summary>
    public void CollectFromChildren()
    {
        selfColliders = GetComponentsInChildren<Collider>(true);
    }
#endif
}