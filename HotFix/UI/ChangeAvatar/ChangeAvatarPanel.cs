using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeAvatarPanel : UIBase
{
    private static List<ChangeAvatarItemButton> maleButtons = new List<ChangeAvatarItemButton>();
    private static List<ChangeAvatarItemButton> femaleButtons = new List<ChangeAvatarItemButton>();
    private GameAcceptButton TurnLeftBtn;
    private GameAcceptButton TurnRightBtn;
    private GameCancelButton returnButton;
    private LocalPlayer playerRef;
    public ChangeAvatarPanel(LocalPlayer playerRef) : base(R.Path.ChangeAvatarPanel)
    {
        this.playerRef = playerRef;
        
        var parent = FindChildRecursive(UIPrefab.transform, "Content");
        bool isGirl = Context.localPlayer.isGirl;
        List<ChangeAvatarItemButton> targetButtons = isGirl ? femaleButtons : maleButtons;
        if (targetButtons.Count == 0)
        {
            GenderEquipCollection gender = isGirl ? Context.Item.clothes.female : Context.Item.clothes.male;
            BuildButtons(gender.hair, targetButtons);
            BuildButtons(gender.face, targetButtons);
            BuildButtons(gender.earring, targetButtons);
            BuildButtons(gender.glasses, targetButtons);
            BuildButtons(gender.upper, targetButtons);
            BuildButtons(gender.bottom, targetButtons);
            BuildButtons(gender.stocking, targetButtons);
            BuildButtons(gender.hand, targetButtons);
            BuildButtons(gender.shoe, targetButtons);
        }

        foreach (var e in targetButtons)
        {
            e.Show();
            e.SetParent(parent);
        }

        TurnLeftBtn = GetTargetComponent<GameAcceptButton>("TurnLeftBtn");
        TurnLeftBtn.onClick.AddListener(TurnPlayerLeft15);
        TurnRightBtn = GetTargetComponent<GameAcceptButton>("TurnRightBtn");
        TurnRightBtn.onClick.AddListener(TurnPlayerRight15);
        returnButton = GetTargetComponent<GameCancelButton>("goMainButton");
        returnButton.onClick.AddListener(() => { Hide(); });
    }

    private static void BuildButtons(List<ClothBase> cloths, List<ChangeAvatarItemButton> buttons)
    {
        foreach (var c in cloths)
        {
            buttons.Add(new ChangeAvatarItemButton(c));
        }
    }

    private void TurnPlayerLeft15()
    {
        playerRef.transform.Rotate(Vector3.up, -15f, Space.Self);        
    }

    private void TurnPlayerRight15()
    {
        playerRef.transform.Rotate(Vector3.up, 15f, Space.Self);
    }

    public override void Destroy()
    {
        foreach (var e in femaleButtons)
        {
            e.Hide();
        }

        foreach (var e in maleButtons)
        {
            e.Hide();
        }
        base.Destroy();
    }
}
