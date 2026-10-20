using System.Collections;
using System.Collections.Generic;
using PKSv;
using UnityEngine;

public class NPC : CharacterBase
{
    private string characterName = "NPC";
    private WorldSpaceName spaceName;
    public SaveData saveData { get; private set; } = new SaveData();

    /// <summary>
    /// 组件引用与初始化都在 CharacterBase 里，这里只做 NPC 专属标记。
    /// </summary>
    protected override void Awake()
    {
        equipSystem.isNPC = true;
        base.Awake();
    }

    protected override void Start()
    {
        equipSystem.isGirl = isGirl;

        // 里面按顺序初始化 equipSystem 与 characterAnimation，必须先于穿衣服
        base.Start();

        equipSystem.ApplyDefaultOutfit();

        var nameObj = ResourceHelp.LoadPrefab("WorldSpaceName");
        spaceName = nameObj.GetComponent<WorldSpaceName>();
        spaceName.characterName = characterName;

        nameObj.transform.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);
        nameObj.transform.Translate(Vector3.up * 2f, Space.World);
        nameObj.transform.SetParent(gameObject.transform);
    }

    public void SetName(string newNames)
    {
        characterName = newNames;
    }

    public override void Dead()
    {
        base.Dead();
        movementRb.linearVelocity = new Vector3(0,movementRb.linearVelocity.y,0);
    }

    public void ApplyCloth(CharacterEquipCollection cloths)
    {
        // TODO: 由于API发生重大变更，NPC现在没有衣服可以穿
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
