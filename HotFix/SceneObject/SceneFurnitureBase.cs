using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;

// 具有被玩家操控的能力，自身本身具有一定的能力。
public class SceneFurnitureBase : SceneObjectBase, IControlable, IInputAcceptable, IDeathAndRevive
{
    public TFPSCamera thisCamera;
    // 正在交互的玩家，允许可以可以同时容纳多个，视FurntureBase内可容纳字段而定，本身数组元素个数就代表了可容纳玩家数
    public Dictionary<GameObject, CharacterBase> interactingChars {get; private set;} = new Dictionary<GameObject, CharacterBase>();

    void Start()
    {
        thisCamera = gameObject.AddComponent<TFPSCamera>();
        thisCamera.TPSCameraTarget = gameObject.transform;
        thisCamera.DisableAll();

        // 配置好座位和玩家的映射关系，运行时直接取得
        // 找到所有的可坐的GameObject
        List<GameObject> seats = new List<GameObject>();
        foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
        {
            // 我们的资源一定保证有sitPoint就没有ActionPoint，不会同时存在。
            if (t.name.StartsWith("sitPoint"))
            {
                seats.Add(t.gameObject);
            }
            else if (t.name.StartsWith("ActionPoint"))
            {
                seats.Add(t.gameObject);
            }
        }

        // 排序
        try
        {
            seats.Sort((a, b) =>
            {
                int numA = int.Parse(a.name.Substring(a.name.Length - 2));
                int numB = int.Parse(b.name.Substring(b.name.Length - 2));
                return numA.CompareTo(numB);
            });
        }
        catch (Exception e)
        {
            Log.Error(furniture.itemName + "的座位点命名不符合规范：" + e + "！");
            return;
        }

        if (seats.Count <= 0)
        {
            Log.Warn(furniture.itemName + "没有找到座位点，无法入座！");
            return;
        }

        // 开始建立映射关系(直接用元素顺序)，初始玩家全都为null，因为刚开始不可能有人坐
        foreach (var e in seats)
        {
            interactingChars.Add(e, null);
        }
        // 完成！

        // 注册玩家死亡事件，死亡后脱离
        LocalPlayer.RegisterDeathAndReviveEvents(this);
    }

    public void OnMouseMove(Vector2 direction)
    {
        thisCamera.RotateCamera(direction);
    }

    // 暂时不适配手机，电脑按shift脱离
    public void OnSpiritReleased()
    {
        ReleaseCharacter(Context.localPlayer);
    }

    public void OnMouseWheelMoved(float scrollDelta)
    {
        thisCamera.Zoom(scrollDelta);
    }

    // 为什么单独拆出，因为有很多个地方都可能调用
    public void ReleaseCharacter(CharacterBase who)
    {
        GameObject targetKey = null;
        foreach (var kvp in interactingChars)
        {
            if (kvp.Value == who)
            {
                targetKey = kvp.Key;
                break;
            }
        }
        if (targetKey == null)
        {
            Log.Warn("没在正在交互的对象字典中找到要释放的对象，这不该发生！");
            return;
        }
        interactingChars[targetKey] = null;
        who.transform.SetParent(null, true);
        who.rb.isKinematic = false;
        // 起身：常规Idle带层前缀，同步播5层恢复
        who.characterAnimation.PlayLocomotionAllLayers("Idle");
        who.characterAnimation.isDoingAction = false;
        who.characterAnimation.actionStateName = "";
        // 动作状态也要清掉，否则 moveState 停在 SIT/LIE/BATH，动画上报会一直报坐着
        who.moveState = EAnimationState.IDLE;

        // 如果是玩家，还要切换回去相机
        if (who is LocalPlayer pl)
        {
            Context.im.PossessNewChracter(pl);
        }

        // 如果是厕所，播放冲水声
        if (furniture.interactType == EInteractableType.Toilet)
        {
            Context.ss.Play("toiletFlush");
        }

        who.ApplyEquipedSuit();
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return (furniture.itemName, furniture.description, furniture.iconPath, furniture.interactType, this);
    }

    public override void StartInteract(Vector3 charPos, CharacterBase interactChar)
    {
        if (Util.CheckNull(interactChar)) return;

        float distance = Vector3.Distance(charPos, transform.position);
        if (distance >= 3.5f)
        {
            if (interactChar is LocalPlayer)
                new PromptMessage("距离过远！").Show();
            
            Log.Info("距离" + distance);
            return;
        }
        
        // 不用检测是不是满，因为交互点已经决定了这个家具有多少个座位。
        // 于是按顺序取出为null元素，作为可交互对象
        GameObject targetSeat = null;
        foreach (var kvp in interactingChars)
        {
            if (kvp.Value == null)
            {
                targetSeat = kvp.Key;
                break;
            }
        }
        // 找不到，返回
        if (targetSeat == null)
        {
            if (interactChar is LocalPlayer)
            {
                new PromptMessage("座位已满！").Show();
            }
            return;
        }
        // 更新字典，使其换成正确引用
        interactingChars[targetSeat] = interactChar;
        interactChar.characterAnimation.isDoingAction = true;
        
        MakeCharSeat(interactChar,targetSeat,furniture.interactType);
    }

    public void SwitchToCamera(bool enable)
    {
        if (enable)
        {
            thisCamera.EnableAll();
        }
        else
        {
            thisCamera.DisableAll();
        }
    }

    private void MakeCharSeat(CharacterBase target, GameObject targetSeat, EInteractableType interactType)
    {
        Transform player = target.transform;
        Rigidbody playerRb = target.GetComponent<Rigidbody>();

        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        playerRb.isKinematic = true;

        player.position = targetSeat.transform.position;
        player.rotation = targetSeat.transform.rotation;

        player.SetParent(targetSeat.transform, true);
        
        switch (interactType)
        {
            case EInteractableType.Toilet:
            case EInteractableType.Sittable:
            {
                // Sit：Base 原名 Sit，衣服层带前缀 HairSit/CoatSit/SkirtSit/WingSit
                target.characterAnimation.actionStateName = "Sit";
                target.moveState = EAnimationState.SIT;
                target.characterAnimation.PlayLocomotionAllLayers("Sit");
                break;
            }
            case EInteractableType.Bed:
                {
                    // Lie：状态名是 "Lie"（挂的动画资产叫 LieLeft），且只有主体层有该状态 → 只播层0
                    target.characterAnimation.actionStateName = "Lie";
                    target.moveState = EAnimationState.LIE;
                    target.characterAnimation.PlayAnimationLayer("Lie", 0);
                    break;
                }
            case EInteractableType.Shower:
                {
                    target.ApplyBathSuit();
                    // 洗澡用的是普通站立
                    target.characterAnimation.actionStateName = "Idle";
                    target.moveState = EAnimationState.IDLE;
                    target.characterAnimation.PlayAnimationLayer("Idle", 0);
                    break;
                }
                case EInteractableType.Tub:
                {
                    target.ApplyBathSuit();
                    target.characterAnimation.actionStateName = "Bath";
                    target.moveState = EAnimationState.BATH;
                    target.characterAnimation.PlayAnimationLayer("Bath", 0);
                    break;
                }
        }

        if (target is LocalPlayer)
        {
            Context.im.PossessNewChracter(this);
            // 更新相机旋转
            thisCamera.xRotation = Context.localPlayer.tpsCamera.xRotation;
            thisCamera.yRotation = Context.localPlayer.tpsCamera.yRotation;
            thisCamera.distance = Context.localPlayer.tpsCamera.distance;
        }
    }

    public void OnPlayerDeath()
    {
        ReleaseCharacter(Context.localPlayer);
    }

    public void OnPlayerRevive() {}

    void OnDestroy()
    {
        LocalPlayer.UnregisterDeathAndReviveEvents(this);
    }

}
