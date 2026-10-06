using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using PK;

/// <summary>
/// 我们规定：最上面的UI拥有一切接受输入的权利，若有UI正在输入，下面的及玩家都不会接收
/// </summary>
public class UIManager : IDeathAndRevive, IEnterNewScene
{
    public List<UIBase> uiStack {get; private set;} = new List<UIBase>(5);
    private List<UIBase> pendingDestroy = new List<UIBase>(5);

    public UIManager()
    {
        LocalPlayer.RegisterDeathAndReviveEvents(this);
        SceneController.RegisterEnterNewSceneCallback(this);
    }

    // 推入一个新的UI
    public void Push(UIBase ui)
    {
        if (ui == null) { Log.NullPtr("Push"); return; }

        uiStack.Add(ui);
        ui.SetStackOrder(uiStack.Count - 1);
        ui.SetVisible(true);
        ui.SetInteractable(true);

        foreach (var u in uiStack)
        {
            if (u == ui) continue;
            u.SetInteractable(false);
        }

        RefreshCursorState();
    }

    // 弹出栈顶
    public void Pop()
    {
        if (uiStack.Count <= 0) return;

        uiStack[^1].Destroy();
        uiStack.RemoveAt(uiStack.Count - 1);

        while (pendingDestroy.Count > 0)
        {
            var d = pendingDestroy[0];
            pendingDestroy.RemoveAt(0);
            if (d == Peek()) { Pop(); return; }
            if (uiStack.Contains(d))
            {
                uiStack.Remove(d);
                d.Destroy();
            }
        }

        if (uiStack.Count == 0) { RefreshCursorState(); return; }

        var top = uiStack[^1];
        top.SetVisible(true);
        top.SetInteractable(true);
        RefreshCursorState();
    }

    /// <summary>
    /// 集中管理鼠标状态：UI 栈非空（有 UI 需要点击）或按住 Alt 时显示并解锁鼠标，
    /// 否则隐藏并锁定（TPS 视角旋转用）。所有会改变鼠标状态的路径都必须走这里，
    /// 避免 Push/Pop/Alt 各自设置导致状态漂移（如关闭 UI 后鼠标残留、有 UI 时松开 Alt 被锁）。
    /// </summary>
    public void RefreshCursorState()
    {
        bool showCursor = uiStack.Count > 0 || InputManager.altHolding;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = showCursor;
    }

    public bool HasUIBlocking()
    {
        return uiStack.Count > 0;
    }

    /// <summary>
    /// 尝试销毁自身
    /// </summary>
    /// <param name="uiNeedToDestroy"></param>
    public void Pop(UIBase uiNeedToDestroy)
    {
        if (uiNeedToDestroy == null)
        {
            Log.NullPtr("Pop");
            return;
        }

        var topUI = Peek();
        if (uiNeedToDestroy == topUI)
        {
            Pop();
        }
        else
        {
            pendingDestroy.Add(uiNeedToDestroy);
        }
    }

    // 查看栈顶UI
    public UIBase Peek()
    {
        if (uiStack.Count <= 0)
        {
            Log.Warn("栈为空！无法查看");
            return null;
        }
        return uiStack[uiStack.Count - 1];
    }

    // 切换关卡时调用，销毁所有UI
    private void DestroyAll()
    {
        var snapshot = new List<UIBase>(uiStack);
        foreach (var e in snapshot) e.Destroy();
        uiStack.Clear();
    }

    // 玩家死亡
    public void OnPlayerDeath()
    {
        var snapshot = new List<UIBase>(uiStack);
        // 销毁全部UI
        DestroyAll();
        new DeathPanel().Show();
    }

    // 玩家复活，主要是移除死亡界面
    public void OnPlayerRevive()
    {
        Context.um.Pop();
    }

    public void OnBeginEnterNewScene(ESceneType newScene)
    {
        DestroyAll();
        new LoadingScreen().Show();
    }

    // 根据进入的场景选择，GameMainPanel怎么显示
    public void OnNewSceneEntered(ESceneType newScene)
    {
        DestroyAll();
        switch (newScene)
        {
            case ESceneType.MAIN_MENU:
                {
                    new StartMenu().Show();
                    break;
                }
        }
    }

}
