using System.Collections.Generic;
using PK;
using UnityEngine;

public static class ColliderUtil
{


    public static List<(MeshCollider collider, Bounds bounds)> CollectAll(int layerMask = ~0)
    {
        var list = new List<(MeshCollider, Bounds)>();
        var all = Object.FindObjectsOfType<MeshCollider>(true);

        foreach (var mc in all)
        {
            if (mc == null) continue;
            if ((layerMask & (1 << mc.gameObject.layer)) == 0) continue;

            Bounds b = GetWorldBounds(mc);
            if (b.size == Vector3.zero) continue;   // 拿不到包围盒的跳过，留着只会白算
            list.Add((mc, b));
        }

        PK.Log.Info($"收集到 {list.Count} 个 MeshCollider");
        return list;
    }

    /// <summary>
    /// 取 MeshCollider 的世界包围盒。
    /// 不能用 mc.bounds —— collider 被禁用时它返回零尺寸（实测 9401 个全是 0），
    /// 于是"到包围盒的距离"就变成"到那个零尺寸点的距离"，永远判定为太远、一个都开不起来。
    /// 改用 sharedMesh.bounds（模型本地空间，任何时候都有效）乘以 transform 自己算。
    /// </summary>
    private static Bounds GetWorldBounds(MeshCollider mc)
    {
        Mesh mesh = mc.sharedMesh;
        if (mesh == null) return new Bounds();

        Bounds local = mesh.bounds;
        Vector3 c = local.center;
        Vector3 e = local.extents;

        // 把本地包围盒的 8 个角变换到世界空间，再重新求 AABB（模型可能带旋转/缩放）
        Matrix4x4 m = mc.transform.localToWorldMatrix;
        Vector3 worldCenter = m.MultiplyPoint3x4(c);
        Bounds world = new Bounds(worldCenter, Vector3.zero);   // Bounds 没有单参数构造函数
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            world.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return world;
    }

    /// <summary>
    /// 遍历列表，按距离启用/禁用 MeshCollider。
    /// 调用方自己控制频率（比如每秒一次）。
    /// </summary>
    /// <param name="entries">CollectAll 返回的列表</param>
    /// <param name="center">检测中心，一般是玩家位置</param>
    /// <param name="enableRadius">距离小于此值碰撞开启</param>
    /// <param name="disableRadius">距离大于此值碰撞关闭，必须大于 enableRadius</param>
    public static void Tick(
        List<(MeshCollider collider, Bounds bounds)> entries,
        Vector3 center,
        float enableRadius,
        float disableRadius)
    {
        float enableSqr = enableRadius * enableRadius;
        float disableSqr = disableRadius * disableRadius;

        for (int i = 0; i < entries.Count; i++)
        {
            var (mc, bounds) = entries[i];
            if (mc == null)
            {
                // 原来这里是 Log.NullPtr("Tick" + mc.ToString()) —— mc 已经是 null，
                // 拼字符串本身就空引用；而且没有 continue，下面照样用 mc。
                continue;
            }

            float sqrDist = SqrDistanceToBounds(center, bounds);
            bool currentlyEnabled = mc.enabled;

            if (!currentlyEnabled && sqrDist <= enableSqr)
            {
                mc.enabled = true;
            }
            else if (currentlyEnabled && sqrDist >= disableSqr)
            {
                mc.enabled = false;
            }
        }
    }

    /// <summary>点到AABB的平方距离。点在盒内返回 0。</summary>
    private static float SqrDistanceToBounds(Vector3 p, Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        Vector3 closest = new Vector3(
            Mathf.Clamp(p.x, min.x, max.x),
            Mathf.Clamp(p.y, min.y, max.y),
            Mathf.Clamp(p.z, min.z, max.z)
        );

        Vector3 d = p - closest;
        return d.x * d.x + d.y * d.y + d.z * d.z;
    }
}
