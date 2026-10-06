using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathPanel : UIBase
{
    private GameAcceptButton _Revive;
    private GameAcceptButton _MedkitRevive;
    public DeathPanel() : base("DeadPanel")
    {
        _Revive = GetTargetComponent<GameAcceptButton>("ReviveButton");
        _Revive.onClick.AddListener(Revive);
        _MedkitRevive = GetTargetComponent<GameAcceptButton>("MedkitButton");
        _MedkitRevive.onClick.AddListener(MedkitRevive);

        Time.timeScale = 0.3f;
        var animator = UIPrefab.GetComponent<Animator>();
        animator.speed = 1f / 0.3f;
    }

    private void Revive()
    {
        Context.localPlayer.ReviveAtHome();
    }

    private void MedkitRevive()
    {
        Context.localPlayer.Revive();
    }
}
