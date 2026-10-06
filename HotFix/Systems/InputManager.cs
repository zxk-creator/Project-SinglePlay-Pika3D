using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInputAcceptable
{
    public void OnMouseMove(Vector2 direction) { }
    public void OnMove(Vector2 direction) { }
    public void OnFixedMove(Vector2 direction) { }

    public void OnJumpPressed() { }

    public void OnSpiritPressed() { }

    public void OnSpiritReleased() { }

    public void OnMouseWheelMoved(float scrollDelta) { }

    public void OnExitPressed() { }

    public void OnBeginChatPressed() { }

    public void OnOpenBagPressed() { }

    public void OnAcceptPressed() { }

    public void OnLeftMouseKeyPressed() { }
    public void OnRightMouseKeyPressed() { }
    public void OnAltPressed() { }
    public void OnAltReleased() { }
    public void OnKeypadUpPressed() { }
    public void OnKeypadDownPressed() { }
}

// 实现了这个接口也
public interface IControlable
{
    public void SwitchToCamera(bool enable);
}

public class InputManager : MonoBehaviour
{
    /// <summary>Alt 是否处于按住状态（供 UIManager.RefreshCursorState 决策鼠标显隐，任何时刻可查）</summary>
    public static bool altHolding = false;

    public KeyCode forward = KeyCode.W;
    public KeyCode backward = KeyCode.S;
    public KeyCode leftward = KeyCode.A;
    public KeyCode rightward = KeyCode.D;
    public KeyCode spirit = KeyCode.LeftShift;
    public KeyCode jump = KeyCode.Space;
    public KeyCode mouseLeft = KeyCode.Mouse0;
    public KeyCode rotateCam = KeyCode.Mouse2;
    public KeyCode exit = KeyCode.Escape;
    public KeyCode openChat = KeyCode.Slash;
    public KeyCode openBag = KeyCode.Tab;
    public KeyCode changePerspective = KeyCode.C;
    public KeyCode accept = KeyCode.Return;
    public KeyCode alt = KeyCode.LeftAlt;
    public KeyCode keypadUp = KeyCode.UpArrow;
    public KeyCode keypadDown = KeyCode.DownArrow;

    // 当前正在接收输入者
    private IControlable currentController;

    void FixedUpdate()
    {
        IInputAcceptable controller = GetController();
        if (controller == null) return;
        controller.OnFixedMove(GetMovementInput());
    }

    void Update()
    {
        // 实时同步 Alt 按住状态：UI 打开时 controller 可能是 UI 或 null，
        // Alt 回调到不了 LocalPlayer，必须在这里独立维护全局状态供鼠标显隐决策。
        altHolding = Input.GetKey(alt);

        IInputAcceptable controller = GetController();
        // 不是静默失败，而是
        if (controller == null) return;

        controller.OnMove(GetMovementInput());
        controller.OnMouseMove(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));
        if (Input.GetKeyDown(spirit)) controller.OnSpiritPressed();
        if (Input.GetKeyUp(spirit)) controller.OnSpiritReleased();
        if (Input.GetKeyDown(jump)) controller.OnJumpPressed();
        if (Input.GetKeyDown(exit)) controller.OnExitPressed();
        if (Input.GetKeyDown(openChat)) controller.OnBeginChatPressed();
        if (Input.GetKeyDown(openBag)) controller.OnOpenBagPressed();
        if (Input.GetKeyDown(accept)) controller.OnAcceptPressed();
        if (Input.GetKeyDown(mouseLeft)) controller.OnLeftMouseKeyPressed();
        if (Input.GetMouseButtonDown(1)) controller.OnRightMouseKeyPressed();
        if (Input.GetKeyDown(keypadUp)) controller.OnKeypadUpPressed();
        if (Input.GetKeyDown(keypadDown)) controller.OnKeypadDownPressed();

        // Alt 按下/松开统一在这里刷新鼠标显隐
        if (Input.GetKeyDown(alt) || Input.GetKeyUp(alt))
            Context.um.RefreshCursorState();

        controller.OnMouseWheelMoved(Input.GetAxis("Mouse ScrollWheel"));
    }

    public void PossessNewChracter(IControlable newController)
    {
        if (Util.CheckNull(newController)) return;

        currentController?.SwitchToCamera(false);
        currentController = newController;
        newController.SwitchToCamera(true);
    }

    private Vector2 GetMovementInput()
    {
        bool left = Input.GetKey(leftward);
        bool right = Input.GetKey(rightward);
        float horizontal = 0f;
        if (left && !right) horizontal = -1f;
        else if (!left && right) horizontal = 1f;

        bool fwd = Input.GetKey(forward);
        bool bwd = Input.GetKey(backward);
        float vertical = 0f;
        if (fwd && !bwd) vertical = 1f;
        else if (!fwd && bwd) vertical = -1f;

        return new Vector2(horizontal, vertical);
    }

    private IInputAcceptable GetController()
    {
        if (Context.um.HasUIBlocking())
        {
            if (Context.um.Peek() is IInputAcceptable ic)
            {
                return ic;
            }
            else return null;
        }
        else
        {
            return currentController as IInputAcceptable;
        }
    }
}