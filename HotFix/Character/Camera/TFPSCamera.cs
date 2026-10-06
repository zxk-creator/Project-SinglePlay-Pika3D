using UnityEngine;

/// <summary>
/// 挂上这个组件，物体就具有了第一（暂未实现）三人称相机功能。
/// </summary>
public class TFPSCamera : MonoBehaviour, IDeathAndRevive
{
    [Header("目标")]
    public Transform TPSCameraTarget;

    [Header("视角")]
    [Tooltip("相机在世界坐标系下的俯仰角")]
    public float xRotation = 0f;
    [Tooltip("相机在世界坐标系下的偏航角")]
    public float yRotation = 0f;
    [Tooltip("相机与目标之间的距离")]
    public float distance = 4f;

    [Header("碰撞")]
    [Tooltip("从玩家向相机方向发射球体检测的半径")]
    public float castRadius = 0.25f;
    [Tooltip("相机距离目标最近不能低于此值")]
    public float minDistance = 1.5f;
    [Tooltip("碰撞检测层级")]
    public LayerMask collisionMask = -1;

    [Header("滚轮缩放")]
    [Tooltip("滚动缩放灵敏度")]
    public float zoomSpeed = 3f;
    [Tooltip("最近拉近距离")]
    public float minZoomDistance = 1.5f;
    [Tooltip("最远拉远距离")]
    public float maxZoomDistance = 10f;

    public Camera TPCamera { get; private set; }

    void Start()
    {
        collisionMask = 1 << 0;
        EnsureCamera();
        LocalPlayer.RegisterDeathAndReviveEvents(this);
    }

    private void EnsureCamera()
    {
        if (TPCamera != null) return;
        GameObject camObj = new GameObject($"{name}_TPCamera");
        TPCamera = camObj.AddComponent<Camera>();
        TPCamera.nearClipPlane = 0.1f;
        camObj.AddComponent<AudioListener>();
        // 和自身一样设置为DontDestroyOnLoad
        DontDestroyOnLoad(camObj);
        TPCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        TPCamera.transform.position = TPSCameraTarget.position - TPCamera.transform.forward * distance;
    }

    void OnDestroy()
    {
        LocalPlayer.UnregisterDeathAndReviveEvents(this);
    }

    void LateUpdate()
    {
        UpdateCameraPosition();
    }

    public void RotateCamera(Vector2 direction)
    {
        yRotation += direction.x;
        xRotation += direction.y;
        xRotation = Mathf.Clamp(xRotation, -85, 85);
    }

    public void Zoom(float scrollDelta)
    {
        distance -= scrollDelta * zoomSpeed;
        distance = Mathf.Clamp(distance, minZoomDistance, maxZoomDistance);
    }

    public void DisableAll()
    {
        EnsureCamera();
        TPCamera.gameObject.SetActive(false);
    }

    public void EnableAll()
    {
        EnsureCamera();
        TPCamera.gameObject.SetActive(true);
    }

    void UpdateCameraPosition()
    {
        TPCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

        Vector3 dir = -TPCamera.transform.forward;
        Vector3 center = TPSCameraTarget.position + Vector3.up * 1.5f;
        Vector3 desiredPos = center + dir * distance;

        // 碰撞检测
        RaycastHit hit;
        if (Physics.SphereCast(
                center, castRadius,
                dir, out hit, distance,
                collisionMask))
        {
            desiredPos = hit.point - dir * castRadius;
        }

        // 防穿地 / 过分靠近玩家
        if ((desiredPos - center).magnitude < minDistance)
        {
            desiredPos = center + dir * minDistance;
        }

        TPCamera.transform.position = desiredPos;
    }

    public Camera GetActiveCamera()
    {
        return TPCamera;
    }

    void OnDrawGizmosSelected()
    {
        if (TPSCameraTarget == null || TPCamera == null) return;

        Vector3 center = TPSCameraTarget.position + Vector3.up * 1.5f;
        Vector3 dir = -TPCamera.transform.forward;
        Vector3 desired = center + dir * distance;
        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            center, castRadius,
            dir, out hit, distance,
            collisionMask
        );

        // 命中=红色，未命中=绿色
        Gizmos.color = hasHit ? Color.red : Color.green;
        Gizmos.DrawLine(center, desired);
        Gizmos.DrawWireSphere(desired, 0.3f);
    }

    // 供玩家获取水平前方向量
    public Vector3 GetHorizontalForward()
    {
        return Vector3.ProjectOnPlane(TPCamera.transform.forward, Vector3.up).normalized;
    }

    public void OnPlayerRevive()
    {
        xRotation = 0f;
        yRotation = 0f;
        distance = 4f;
    }

    public void OnPlayerDeath()
    {
        xRotation = 0f;
        yRotation = 0f;
        distance = 4f;
    }
}
