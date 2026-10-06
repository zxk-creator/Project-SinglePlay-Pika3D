using UnityEngine;
using PK;

/// <summary>
/// 继承本类的东西都可以被放在场景中，包括运行时，仅支持放置普通的，可显示的家具
/// </summary>
public class SceneObjectBase : Actor
{
    // 点了之后，右上角是否会出现交互提示（手机版专属，电脑也有）
    public bool canInteract = false;
    // 每一个Object身上都有这一个对象，方便回收时使用
    public FurnitureBase furniture;
    // 是否能够回收（对于资源，需设置为false，因为必须砍伐或开采才能收集）
    public bool canRecycle = true;

    /// <summary>
    /// 销毁且具有放入玩家背包
    /// </summary>
    /// <param name="player"></param>
    public void Destroy(LocalPlayer player)
    {
        if (Util.CheckNull(player)) return;
        if (player.inventory.TryPutIntoBag(furniture) != null)
        {
            Log.Info("背包已满，无法继续放入家具" + furniture.itemName);
            return;
        }
        Destroy(gameObject);
    }

    public void RotateLeft()
    {
        transform.Rotate(new Vector3(0, -15, 0));
    }

    public void RotateRight()
    {
        transform.Rotate(new Vector3(0, 15, 0));
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return (furniture.itemName, furniture.description, furniture.iconPath, EInteractableType.None, this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        return;
    }

}
