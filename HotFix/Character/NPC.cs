using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC : CharacterBase
{
    private string characterName = "NPC";
    private WorldSpaceName spaceName;
    public InventorySystem inventory {get; private set;}

    private void Awake()
    {
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();
        equipSystem.isGirl = isGirl;
        equipSystem.isNPC = true;
    }

    protected override void Start()
    {
        base.Start();
        equipSystem.isGirl = isGirl;
        equipSystem.ApplyDefaultOutfit();
        var nameObj = ResourceHelp.LoadPrefab("WorldSpaceName");
        spaceName = nameObj.GetComponent<WorldSpaceName>();
        spaceName.characterName = characterName;

        nameObj.transform.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);
        nameObj.transform.Translate(Vector3.up * 2f, Space.World);
        nameObj.transform.SetParent(gameObject.transform);

        inventory = new InventorySystem(equipSystem, this);
    }

    public void SetName(string newNames)
    {
        characterName = newNames;
    }

    public override void Dead()
    {
        base.Dead();
        rb.linearVelocity = new Vector3(0,rb.linearVelocity.y,0);
    }

    public void ApplyCloth(CharacterEquipCollection cloths)
    {
        if (inventory == null) inventory = new InventorySystem(GetComponent<Equip>(), this);
        inventory.SetCharacterEquips(cloths);
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return (characterName, "一个NPC", "ui/icon/sceneobjecticon/npc/Furniture_Npc_0001" , EInteractableType.NPC,this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        throw new System.NotImplementedException();
    }
}
