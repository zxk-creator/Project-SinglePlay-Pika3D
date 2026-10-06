using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureBase : ItemBase
{
    public FurnitureBase()
    {
        itemType = EItemType.FurnitureBase;
        canStack = false;
    }
    public string prefabPath;
    public EInteractableType interactType;
    public bool canRecycle = true;
    public bool canInteract = false;

    public override ItemBase GetACopy()
    {
        var copy = new FurnitureBase
        {
            canStack = canStack,
            currentStackCount = currentStackCount,
            description = description,
            iconPath = iconPath,
            quality = quality,
            itemName = itemName,
            occupiedSpace = occupiedSpace,
            value = value,
            id = id,
            prefabPath = prefabPath,
            interactType = interactType,
            canRecycle = canRecycle,
            canInteract = canInteract,
        };
        return copy;
    }
}
