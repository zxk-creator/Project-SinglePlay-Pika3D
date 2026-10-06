using System;
using AOT;
using UnityEngine;
using UnityEngine.UI;
using PK;

public class ComputerPanel : UIBase
{
    // 自己引用自己放置被回收
    protected static ComputerPanel s_current;
    protected static UnityWebView2.WebViewMessageCallback s_pageMessage;

    protected Image showArea;
    protected IntPtr webView = IntPtr.Zero;
    protected string webUrl = "file:///" + Application.streamingAssetsPath.Replace('\\', '/') + "/MacLikeDesktop/index.html";

    public ComputerPanel(string prefabPath = "ComputerPanel") : base(prefabPath)
    {
        showArea = GetTargetComponent<Image>();

        if (canvasGroup == null)
        {
            canvasGroup = UIPrefab.AddComponent<CanvasGroup>();
        }
    }

    public override void Show()
    {
        base.Show();
        // 注册Update事件，当用户改变窗口大小时，确保Webview界面也会跟着改变。
        Context.updateProxy.RegisterNewTask(SyncWebView, int.MaxValue, 0.01f);
        Canvas.ForceUpdateCanvases();

        if (webView == IntPtr.Zero)
        {
            if (UnityWebView2.TryGetClientRect(showArea.transform as RectTransform, out int x, out int y, out int w, out int h))
            {
                webView = UnityWebView2.WebView_Create(x, y, w, h, webUrl);
                if (webView == IntPtr.Zero)
                {
                    Log.Error("ComputerPanel.Show：WebView_Create返回空句柄，网页窗口未创建。"
                              + "运行库缺失等内部原因见 %TEMP%\\UnityWebView2.log");
                    Context.updateProxy.DestoryTask(SyncWebView);
                    return;
                }
                else
                {
                    Log.Info("ComputerPanel.Show：网页窗口已创建 rect=(" + x + "," + y + "," + w + "," + h +
                             ") url=" + webUrl);
                }
            }
            else
            {
                Log.Error("ComputerPanel.Show：显示区域计算失败，网页窗口未创建");
                Context.updateProxy.DestoryTask(SyncWebView);
                return;
            }
        }

        if (webView != IntPtr.Zero)
        {
            s_current = this;
            s_pageMessage = OnPageMessage;
            UnityWebView2.WebView_SetMessageCallback(webView, s_pageMessage, IntPtr.Zero);
        }

        SyncWebView();
    }

    [MonoPInvokeCallback(typeof(UnityWebView2.WebViewMessageCallback))]
    protected static void OnPageMessage(IntPtr messageUtf8, IntPtr user)
    {
        string message = UnityWebView2.PtrToUtf8(messageUtf8);
        Log.Info("ComputerPanel：收到网页消息 " + message);

        if (message.Contains("close"))
        {
            s_current.Hide();
        }
    }

    public void SyncWebView()
    {
        if (webView == IntPtr.Zero)
        {
            Log.Warn("ComputerPanel.SyncWebView：当前没有网页窗口，本次同步跳过");
            return;
        }

        UnityWebView2.ApplyRect(webView, showArea.transform as RectTransform);
    }

    public override void Hide()
    {
        Context.updateProxy.DestoryTask(SyncWebView);
        base.Hide();
        Context.updateProxy.RegisterDelayTask(ReleaseWebView, 0.5f);
    }

    public override void Destroy()
    {
        Context.updateProxy.DestoryTask(SyncWebView);
        Context.updateProxy.RegisterDelayTask(ReleaseWebView, 0.5f);

        base.Destroy();
    }

    // 立刻销毁。
    protected void ReleaseWebView()
    {
        Context.updateProxy.DestoryTask(SyncWebView);

        if (s_current == this)
        {
            s_current = null;
        }

        if (webView == IntPtr.Zero)
        {
            Log.Info("webView本身为空，无需销毁");
            return;
        }

        UnityWebView2.Destroy(webView);
        webView = IntPtr.Zero;
        Log.Info("ComputerPanel：网页窗口已释放");
    }
}