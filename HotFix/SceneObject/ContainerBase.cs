using UnityEngine;

// 场景中的容器
public class ContainerBase : SceneObjectBase
{
    // true：玩家自己的容器，存放物品用
    // false：野外的垃圾桶等容器，按天整体刷新
    public bool bShouldRefresh = false;
    // 存储用的容量空间
    public BagpackBase ContainerSpace = new BagpackBase(20);
    
    public ItemBase TryPutIntoContainer(ItemBase newItem)
    {
        return ContainerSpace.TryPutIntoBag(newItem);
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType,Actor selfRef) OnRaycastHit()
    {
        return (furniture.itemName, furniture.description,furniture.iconPath, EInteractableType.Stroageable,this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        throw new System.NotImplementedException();
    }

}
