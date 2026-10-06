using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一次性确认框：每次弹出都 new 一个新实例，点击按钮后销毁（用完就扔）。
/// 作为 Canvas 的最后一个子节点渲染，强制显示在所有 UI 之上；不参与 UI 栈。
/// </summary>
public class MessageBoxOKCancel : UIBase
{
    public MessageBoxOKCancel() : base(R.Path.MessageBoxOKCancel)
    {
    }

    public void ShowOkCancel(UnityAction ok, UnityAction cancel, string msg)
    {
        // UIBase 构造函数会 SetActive(false)，这里重新激活
        UIPrefab.SetActive(true);

        // 显示在所有UI之上
        UIPrefab.transform.SetAsLastSibling();

        // 设置提示文字
        TextMeshProUGUI msgText = UIPrefab.transform.Find("root/messageRoot/Scroll View/Viewport/Content/message")?.GetComponent<TextMeshProUGUI>();
        if (msgText != null) msgText.text = msg;

        // 绑定按钮，点击后销毁
        Button[] buttons = UIPrefab.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            switch (btn.name)
            {
                case "okButton":
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => { ok?.Invoke(); Hide(); });
                    break;
                case "cancelButton":
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => { cancel?.Invoke(); Hide(); });
                    break;
            }
        }

        Show();
    }
}
