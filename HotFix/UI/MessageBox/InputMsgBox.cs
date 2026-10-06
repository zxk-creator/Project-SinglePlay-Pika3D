using System;
using UnityEngine;

public class InputMsgBox : UIBase
{
    public GameAcceptButton okButton;
    public GameCancelButton cancelButton;
    public InputMsgBox() : base("InputMsgBox", false)
    {
        okButton = GetTargetComponent<GameAcceptButton>("okButton");
        cancelButton = GetTargetComponent<GameCancelButton>("cancelButton");

        okButton.onClick.AddListener(() =>
        {
            Hide();
        });

        cancelButton.onClick.AddListener(() =>
        {
            Hide();
        });
    }

    public void ShowBox(Action okAction, Action cancelAction)
    {
        
    }
}
