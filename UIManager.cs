using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace GS;

/// <summary>
/// 我们规定：最上面的UI拥有一切接受输入的权利，若有UI正在输入，下面的及玩家都不会接收
/// </summary>
public class UIManager
{
    public List<UIBase> uiStack {get; private set;} = new List<UIBase>(5);
    public Canvas gameCanvas;
    private List<UIBase> pendingDestroy = new List<UIBase>(5);

    public UIManager(Canvas gameCanvas)
    {
        this.gameCanvas = gameCanvas;
    }

    // 推入一个新的UI
    public void Push(UIBase ui)
    {
        if (ui == null)
        {
            Debug.LogError("Push传入的UI为 null");
            return;
        }
        uiStack.Add(ui);
        ui.UIPrefab.SetActive(true);
        ui.canvasGroup.interactable = true;
        ui.canvasGroup.blocksRaycasts = true;

        // 任何UI打开时，其他UI一律不可交互
        foreach (var u in uiStack)
        {
            if (u == ui) continue;
            u.canvasGroup.interactable = false;
            u.canvasGroup.blocksRaycasts = false;
        }
    }

    // 弹出栈顶
    public void Pop()
    {
        if (uiStack.Count <= 0) return;
        uiStack[uiStack.Count - 1].Destroy();
        uiStack.RemoveAt(uiStack.Count - 1);
        foreach (var d in pendingDestroy)
        {
            if (d == Peek()) Pop();
        }

        // 先判断是否为空，为空则不再设置
        if (uiStack.Count == 0)
        {
            return;
        }
        // 新栈顶恢复显示与交互
        var top = uiStack[uiStack.Count - 1];
        top.UIPrefab.SetActive(true);
        top.canvasGroup.interactable = true;
        top.canvasGroup.blocksRaycasts = true;
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
            Debug.LogError("Pop 传入的 UI 为 null");
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
            Debug.LogWarning("栈为空！无法查看");
            return null;
        }
        return uiStack[uiStack.Count - 1];
    }

    private void DestroyAll()
    {
        var snapshot = new List<UIBase>(uiStack);
        foreach (var e in snapshot)
        {
            e.Destroy();
        }
        uiStack.Clear();
    }
}
