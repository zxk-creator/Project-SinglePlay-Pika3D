using UnityEngine;

public enum EInteractableType
{
    NPC,
    Player,
    OtherPlayer,
    Toilet,
    Bed,
    Sittable,
    Choppable,   // 可砍伐的（树木）
    Mineable,    // 可开采的（矿石）
    Equippable,   // 可换装的（装备/衣物）
    Stroageable,
    Shower,        // 站着淋浴
    Tub,            // 躺着洗澡
    None
}

/// <summary>
/// 场景中所有可控制对象（床，换装点等）
/// </summary>
public abstract class Actor : MonoBehaviour
{
    public abstract (string name,string description,string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit(); 
    
    // 开始交互，必须交互者
    public abstract void StartInteract(Vector3 charPos, CharacterBase interactChar);

    /// <summary>
    /// 从屏幕鼠标位置发射射线，检测场景中的 Actor（任意可交互对象）
    /// </summary>
    /// <param name="from">发射射线的相机</param>
    public static Actor LineTraceSceneObject(Camera from)
    {
        Vector3 screenPos = Input.mousePosition;
        Ray ray = from.ScreenPointToRay(screenPos);
        Actor res = null;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 50.0f))
        {
            Debug.Log("命中物体" + hit.collider.name);
            var obj = hit.collider.gameObject;
            res = obj.GetComponent<Actor>();
        }

        return res;
    }
}
