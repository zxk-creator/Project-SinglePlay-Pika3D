using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChangeClothLocation : Actor,IInputAcceptable,IDeathAndRevive
{
    // 缓存玩家当前穿的衣服
    public CharacterEquipCollection playerClothCache;
    public GameObject HintItem;
    // 手动设置碰撞
    public BoxCollider trigger;
    public float selfRotationSpeed;
    // 预览这个用的相机
    public Camera viewCamera;
    private Camera playerCamera;
    public ChangeAvatarPanel changeAvatarScreen;
    public float cantEscapeLastTime = 0.5f;
    public bool isInteracting = false;
    void Start()
    {
        HintItem = Instantiate(HintItem,transform.position, Quaternion.Euler(-90, 0, 0));
        HintItem.layer = 2;
        LocalPlayer.RegisterDeathAndReviveEvents(this);
    }

    void Update()
    {
        cantEscapeLastTime -= Time.deltaTime;
        HintItem.transform.Rotate(0, 0, selfRotationSpeed * Time.deltaTime);
        if (!(cantEscapeLastTime <= 0)) cantEscapeLastTime -= Time.deltaTime;
    }

    // 停止交互
    private void Resume()
    {
        if (cantEscapeLastTime <= 0 && isInteracting)
        {
            Context.localPlayer.GetCamera().enabled = true;
            viewCamera.enabled = false;

            changeAvatarScreen.Hide();

            // Context.singlePlayer.inventory.SetCharacterEquips(playerClothCache);

            HintItem.SetActive(true);
        }
    }

    public void OnPlayerDeath()
    {
        Resume();
        isInteracting = false;
    }

    public void OnPlayerRevive() {}

    public override (string name, string description, string iconPath, EInteractableType interactableType,Actor selfRef) OnRaycastHit()
    {
        return ("换装点","走进即可换装！", "ui/icon/Hat", EInteractableType.Equippable,this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        if (interactChar is not LocalPlayer) return;
        float distance = Vector3.Distance(playerPos, transform.position);

        // 换装
        if (distance <= 1.0f)
        {
            playerClothCache = Context.localPlayer.inventory.equips;

            changeAvatarScreen = new ChangeAvatarPanel(Context.localPlayer);
            changeAvatarScreen.Show();
            var retButton = changeAvatarScreen.GetTargetComponent<Button>("goMainButton");
            retButton.onClick.AddListener(Resume);

            playerCamera = Context.localPlayer.GetCamera();

            cantEscapeLastTime = 0.5f;
            HintItem.SetActive(false);

            Util.Cam.SetViewTargetWithSmooth(playerCamera, viewCamera, cantEscapeLastTime, null);
            Context.localPlayer.gameObject.transform.SetPositionAndRotation(transform.position, transform.rotation);

            isInteracting = true;
        }
        else
        {
            new PromptMessage("距离过远！").Show();
        }
    }

}
