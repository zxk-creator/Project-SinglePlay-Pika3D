using System;
using System.Runtime.InteropServices;
using UnityEngine;
using PK;

public static class UnityWebView2
{
    private const string DllName = "UnityWebView2";
    private static readonly Vector3[] corners = new Vector3[4];

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr WebView_Create(float x, float y, float w, float h,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string url);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_Destroy(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_SetUrl(IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string url);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_Reload(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_SetRect(IntPtr handle, float x, float y, float w, float h);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_GoBack(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_GoForward(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_SetUserAgent(IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userAgent);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void WebViewMessageCallback(IntPtr messageUtf8, IntPtr user);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void WebView_SetMessageCallback(IntPtr handle,
        WebViewMessageCallback callback, IntPtr user);

    public static string PtrToUtf8(IntPtr utf8)
    {
        if (utf8 == IntPtr.Zero) return string.Empty;

        int len = 0;
        while (Marshal.ReadByte(utf8, len) != 0) len++;
        if (len == 0) return string.Empty;

        byte[] buffer = new byte[len];
        Marshal.Copy(utf8, buffer, 0, len);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    public static bool TryGetClientRect(RectTransform rectTransform,
        out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;

        if (rectTransform == null)
        {
            Log.NullPtr("UnityWebView2.TryGetClientRect的rectTransform");
            return false;
        }

        rectTransform.GetWorldCorners(corners);

        // 转换到Unity屏幕坐标（但不是Win32窗口坐标）
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

        // 取真正的上下左右边界
        float xMin = Mathf.Min(min.x, max.x);
        float xMax = Mathf.Max(min.x, max.x);
        float yMin = Mathf.Min(min.y, max.y);
        float yMax = Mathf.Max(min.y, max.y);

        float rawWidth = xMax - xMin;
        float rawHeight = yMax - yMin;

        if (rawWidth < 1f || rawHeight < 1f)
        {
            Log.Error("显示区域无效：" + rectTransform.name +
                      " 换算后 width=" + rawWidth.ToString("F2") +
                      " height=" + rawHeight.ToString("F2") +
                      "，本次不更新网页窗口");
            return false;
        }

        // 转换成Win32窗口坐标
        width = Mathf.RoundToInt(rawWidth);
        height = Mathf.RoundToInt(rawHeight);
        x = Mathf.RoundToInt(xMin);
        // 反转y
        y = Screen.height - Mathf.RoundToInt(yMax);
        return true;
    }

    public static bool ApplyRect(IntPtr handle, RectTransform rectTransform)
    {
        if (handle == IntPtr.Zero)
        {
            Log.NullPtr(handle.ToString());
            return false;
        }

        if (!TryGetClientRect(rectTransform, out int x, out int y, out int w, out int h))
        {
            Log.Warn("UnityWebView2.ApplyRect：显示区域不可用，本次未更新网页窗口的位置与大小");
            return false;
        }

        WebView_SetRect(handle, x, y, w, h);
        return true;
    }

    // 把窗口移动到屏幕外，又不销毁他，假隐藏
    public static void Hide(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            Log.NullPtr(handle.ToString());
            return;
        }

        WebView_SetRect(handle, -32000f, -32000f, 1f, 1f);
    }

    // 真销毁
    public static void Destroy(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            Log.NullPtr(handle.ToString());
            return;
        }

        WebView_Destroy(handle);
    }
}