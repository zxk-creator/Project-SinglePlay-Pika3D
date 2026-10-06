using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;

/// <summary>
/// 场景中只能交互NPC的基类，我们设置他为不能移动，只能交互
/// </summary>
public class FunctionalCharacter : Actor
{
    public Rigidbody rb;
    public CapsuleCollider cc;
    public string characterName;
    public string characterDescription;
    public string characterIconPath;
    private WorldSpaceName worldSpaceName;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CapsuleCollider>();

        var nameObj = ResourceHelp.LoadPrefab("WorldSpaceName");
        worldSpaceName = nameObj.GetComponent<WorldSpaceName>();
        worldSpaceName.SetName(characterName);

        nameObj.transform.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);
        nameObj.transform.Translate(Vector3.up * 2.5f, Space.World);
        nameObj.transform.SetParent(gameObject.transform);
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return (characterName, characterDescription, characterIconPath, EInteractableType.NPC, this);
    }

    public override void StartInteract(Vector3 charPos, CharacterBase interactChar)
    {
        Log.Info("尚未实现功能性角色交互功能！");
        return;
    }

}
