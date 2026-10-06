using UnityEngine;
using UnityEngine.UI;

public class PhonePanel : ComputerPanel, IInputAcceptable
{
    // 与 PhoneUp 片段等长；片段首帧（-746）就是完全滑出屏幕的位置
    private const float CloseSeconds = 0.25f;
    private const float HiddenY = -746f;

    private Animator slideAnim;
    private bool closing;

    public PhonePanel() : base("PhonePanel")
    {
        showArea = GetTargetComponent<Image>("PhoneScreen");
        slideAnim = GetTargetComponent<Animator>();
        webUrl = "file:///" + Application.streamingAssetsPath.Replace('\\', '/') + "/IOSLikeDesktop/index.html";
    }

    public override void Show()
    {
        closing = false;
        if (slideAnim != null)
        {
            // 打开仍旧交给 Animator：enable 时自动正放 PhoneUp
            slideAnim.enabled = true;
        }

        base.Show();
    }

    public override void Hide()
    {
        if (closing) return;
        closing = true;

        // 关闭不再依赖 Animator 倒放（对一个已播完的非循环状态倒放不可靠，会原地不动）
        // 这里直接插值根节点的 anchoredPosition，并关掉 Animator 免得它继续写同一个属性。
        if (slideAnim != null) slideAnim.enabled = false;

        RectTransform root = UIPrefab.transform as RectTransform;
        if (root == null)
        {
            base.Hide();
            return;
        }

        float fromY = root.anchoredPosition.y;
        float elapsed = 0f;

        // 网页窗口是独立 Win32 窗口，靠 SyncWebView 每帧跟随 RectTransform，
        // 所以滑动根节点时网页会一起滑出屏幕（窗口允许移到客户区之外，已实测）。
        Context.updateProxy.RegisterNewTask(() =>
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / CloseSeconds);

            Vector2 pos = root.anchoredPosition;
            pos.y = Mathf.Lerp(fromY, HiddenY, k);
            root.anchoredPosition = pos;

            if (k >= 1f)
            {
                base.Hide();
            }
        }, CloseSeconds, 0f);
    }

    // 按下之后，隐藏自身（先播下滑动画，动画结束再真正隐藏）
    public void OnKeypadDownPressed()
    {
        Hide();
    }
}
