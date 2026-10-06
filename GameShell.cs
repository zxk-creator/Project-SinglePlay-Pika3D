// REMOTE_BUILD: Addressable远程下载构建
// LOCAL_BUILD: Addressable直接取本地资源
#define REMOTE_BUILD

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UI;
using System.Reflection;
using GS;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 挂载到场景中 GameObject 身上即可，它自身不包含代码，只是一个启动的壳。
public class GameShell : MonoBehaviour
{
    private GameShellTitle title;
    private BootNotice notice;

    private const string HotfixAssemblyName = "GS.HotFix";

    // 出包后热更入口在 Addressables 里的地址
    private const string HotfixEntryAddress = "GS.HotFix";

    void Awake()
    {
        InitUIFramework();
        // 然后创建出开始游戏的UI
        title = new GameShellTitle();
        title.Show();

        notice = new BootNotice();

        StartCoroutine(BootCoroutine());
    }

    /// <summary>
    /// 启动总流程。
    /// 编辑器永远直接读工程资源；出包后按构建宏区分：
    /// REMOTE_BUILD 检查网络、更新远程目录、下载资源，任何一环失败都弹窗后退出；
    /// LOCAL_BUILD 资源已内置，直接进入游戏，不做任何远程操作。
    /// </summary>
    private IEnumerator BootCoroutine()
    {
        Debug.Log("启动流程开始！");

#if UNITY_EDITOR
        Debug.Log("编辑器模式：直接读工程资源，不检查网络、不更新目录");
        BootShow("正在初始化，请稍后…");
#elif REMOTE_BUILD
        BootShow("正在为您检查更新和资源完整性，请稍后！");
        yield return RemoteBootCoroutine();
#else
        Debug.Log("LOCAL_BUILD：资源已内置，直接进入游戏");
        BootShow("正在加载资源，请稍后…");
#endif

        string error = StartGame();

        if (error != null)
        {
            yield return FailAndQuit(error);
        }
    }

#if !UNITY_EDITOR && REMOTE_BUILD
    // 只在远程构建里用来判断「本机完整下载过远程资源吗」，LOCAL_BUILD 用不到
    private static string CacheReadyMarkerPath
    {
        get { return Path.Combine(Application.persistentDataPath, "remote_ready"); }
    }

    /// <summary>
    /// 远程构建的启动流程：网络 -> 目录 -> 下载 -> 缓存完整性检查。
    /// 只要本机已经完整下载过一次（有 remote_ready 标记且缓存完整），
    /// 服务器关了或网络异常时也允许带着提示进入游戏；从没下载过的仍然弹窗退出。
    /// </summary>
    private IEnumerator RemoteBootCoroutine()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("当前无网络，尝试使用已缓存的资源");
            yield return FallbackToCacheOrQuit();
            yield break;
        }

        yield return UpdateAddressablesCatalogCoroutine();

        yield return DownloadRemoteAssetsCoroutine();
    }

    /// <summary>
    /// 检查并拉取远程 Addressables 目录，保证后续 LoadAssetAsync 取到的是最新版本。
    /// </summary>
    private IEnumerator UpdateAddressablesCatalogCoroutine()
    {
        Debug.Log("开始检查 Addressables 远程目录");

        AsyncOperationHandle<IResourceLocator> initHandle = Addressables.InitializeAsync();
        yield return initHandle;

        if (initHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Exception e = initHandle.OperationException ?? new Exception("Addressables 初始化失败");
            Debug.LogError("Addressables 初始化失败: " + e.Message);
            Addressables.Release(initHandle);
            yield return FailAndQuit(e);
            yield break;
        }

        Addressables.Release(initHandle);

        AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs();
        yield return updateHandle;

        if (updateHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Exception e = updateHandle.OperationException ?? new Exception("Addressables 目录更新失败");
            Debug.LogError("Addressables 目录更新失败: " + e.Message);
            Addressables.Release(updateHandle);
            yield return FallbackToCacheOrQuit();
            yield break;
        }

        List<IResourceLocator> updated = updateHandle.Result;
        Debug.Log($"Addressables 目录更新完成，更新条目 {updated?.Count ?? 0}");
        Addressables.Release(updateHandle);
    }

    /// <summary>
    /// 把所有远程资源的依赖下载到本地缓存，这样即使之后再断网也能正常加载。
    /// </summary>
    private IEnumerator DownloadRemoteAssetsCoroutine()
    {
        List<object> remoteKeys = CollectRemoteKeys();
        Debug.Log($"远程资源地址数: {remoteKeys.Count}");

        if (remoteKeys.Count == 0)
        {
            yield break;
        }

        long size = 0;
        AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(remoteKeys);
        yield return sizeHandle;

        if (sizeHandle.Status == AsyncOperationStatus.Succeeded)
        {
            size = sizeHandle.Result;
        }
        else
        {
            Debug.LogWarning("获取下载大小失败，跳过空间检查: " + sizeHandle.OperationException?.Message);
        }

        Addressables.Release(sizeHandle);

        if (size <= 0)
        {
            Debug.Log("远程资源均已在缓存中，无需下载");
            MarkCacheReady();
            yield break;
        }

        long free = GetFreeDiskSpace(Application.persistentDataPath);

        if (free >= 0 && free < size)
        {
            yield return FailAndQuit($"设备存储空间不足，需要 {ToMB(size)} MB，可用 {ToMB(free)} MB。请清理空间后重新打开游戏。");
            yield break;
        }

        BootShow($"正在下载游戏资源 0%（共 {ToMB(size)} MB）");

        AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(remoteKeys);

        while (!downloadHandle.IsDone)
        {
            BootShow($"正在下载游戏资源 {(int)(downloadHandle.PercentComplete * 100f)}%（共 {ToMB(size)} MB）");
            yield return null;
        }

        if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Exception e = downloadHandle.OperationException ?? new Exception("资源下载失败");
            Debug.LogError("资源下载失败: " + e.Message);
            Addressables.Release(downloadHandle);
            yield return FallbackToCacheOrQuit();
            yield break;
        }

        Addressables.Release(downloadHandle);
        MarkCacheReady();
        Debug.Log("远程资源下载完成");
    }

    /// <summary>
    /// 远程目录或下载出问题时调用：本机以前完整下载过就带提示进游戏，否则弹窗退出。
    /// </summary>
    private IEnumerator FallbackToCacheOrQuit()
    {
        if (CanPlayFromCache())
        {
            Debug.LogWarning("服务器不可用，改用本地缓存的资源进入游戏");
            new GS.MessageBoxOk().ShowMsg("当前无法连接服务器，将使用已缓存的资源进入游戏。部分内容可能不是最新版本。");
            yield break;
        }

        yield return FailAndQuit("无法连接服务器下载游戏资源，本机也没有可用的资源缓存，无法进入游戏。请检查网络后重新打开游戏。");
    }

    /// <summary>
    /// 是否能用本地缓存进入游戏：以前完整下载过（有标记），且现在远程依赖已经没有缺的。
    /// </summary>
    private static bool CanPlayFromCache()
    {
        if (!File.Exists(CacheReadyMarkerPath))
        {
            Debug.Log("本机从未完整下载过远程资源，无法降级");
            return false;
        }

        long size = GetDownloadSizeRemote();

        if (size != 0)
        {
            Debug.LogWarning($"缓存不完整（还缺 {size} 字节），无法降级");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 取当前还缺多少远程资源，0 表示全在本地缓存里；查询失败返回 -1。
    /// </summary>
    private static long GetDownloadSizeRemote()
    {
        List<object> remoteKeys = CollectRemoteKeys();

        if (remoteKeys.Count == 0)
        {
            return 0;
        }

        AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(remoteKeys);
        handle.WaitForCompletion();

        long size = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : -1;
        Addressables.Release(handle);

        return size;
    }

    /// <summary>
    /// 记录「本机已完整下载过远程资源」，服务器永久关闭后靠它判断能否离线进入游戏。
    /// </summary>
    private static void MarkCacheReady()
    {
        try
        {
            File.WriteAllText(CacheReadyMarkerPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Debug.Log("已记录远程资源缓存标记");
        }
        catch (Exception e)
        {
            Debug.LogWarning("写入缓存标记失败: " + e.GetType().Name + " " + e.Message);
        }
    }

    /// <summary>
    /// 收集所有主资源位于远程的地址，Local 组的内容不走下载。
    /// </summary>
    private static List<object> CollectRemoteKeys()
    {
        List<object> keys = new List<object>();
        HashSet<object> seen = new HashSet<object>();

        foreach (IResourceLocator locator in Addressables.ResourceLocators)
        {
            foreach (object key in locator.Keys)
            {
                if (key == null || seen.Contains(key)) continue;

                IList<IResourceLocation> locations;

                if (!locator.Locate(key, typeof(object), out locations) || locations == null) continue;

                foreach (IResourceLocation location in locations)
                {
                    if (location.InternalId == null) continue;

                    if (location.InternalId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        seen.Add(key);
                        keys.Add(key);
                        break;
                    }
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// 取指定目录所在磁盘的剩余空间，取不到返回 -1（表示放弃检查）。
    /// </summary>
    private static long GetFreeDiskSpace(string dir)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(dir));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (string.Equals(drive.Name, root, StringComparison.OrdinalIgnoreCase) && drive.IsReady)
                {
                    return drive.AvailableFreeSpace;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("读取磁盘空间失败: " + e.GetType().Name);
        }

        return -1;
    }
#endif

    /// <summary>
    /// 进入游戏：取热更入口程序集，加载并执行入口。
    /// 编辑器读 Unity 自己编译的 Library/ScriptAssemblies/GS.HotFix.dll（改完脚本存盘即生效）；
    /// 出包后读 Addressables 地址 GS.HotFix（内容由 Addressables 决定是远程还是本地）。
    /// 同步阻塞主线程，失败时返回失败原因，由调用方弹窗退出。
    /// </summary>
    private string StartGame()
    {
        Debug.Log($"开始加载热更入口: {HotfixAssemblyName}");

        byte[] hotfixBytes = LoadHotfixBytes(out string loadErrorMsg);

        if (hotfixBytes == null)
        {
            return loadErrorMsg;
        }

        if (hotfixBytes.Length == 0)
        {
            Debug.LogError("热更入口内容为空");
            return "热更内容为空，无法进入游戏。请退出游戏后重新打开以重新下载。";
        }

        Debug.Log($"热更入口加载成功，{hotfixBytes.Length} 字节");

        Type type = null;
        MethodInfo method = null;

        try
        {
            Assembly asm = UnityEngine.Assemblies.CurrentAssemblies.LoadFromBytes(hotfixBytes);
            type = asm.GetType("GameMain");
            method = type.GetMethod("StartGame", BindingFlags.Public | BindingFlags.Static);
        }
        catch (Exception e)
        {
            Debug.LogError($"热更程序集加载失败: {e.GetType().Name} {e.Message}");
            return "热更程序集加载失败，无法进入游戏。请退出游戏后重新打开以重新下载。";
        }

        if (type == null || method == null)
        {
            Debug.LogError("热更入口 GameMain.StartGame 未找到");
            return "热更程序集无效，无法进入游戏。请退出游戏后重新打开以重新下载。";
        }

        BootHide();
        title.Hide();

        try
        {
            method.Invoke(null, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"热更入口执行失败: {e.GetType().Name} {e.Message}");
            return "热更内容执行失败，无法进入游戏。请退出游戏后重新打开以重新下载。";
        }

        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器内直接读 Unity 的编译输出，不走 Addressables。
    /// 兼容 Multiplayer Play Mode：它给每个 LocalPlayer 起的"虚拟工程"里 Assets/ProjectSettings 是指向
    /// 真实工程的 Junction，但它自己的 Library 下没有 ScriptAssemblies，编译产物仍在真实工程里。
    /// 所以这里不能只按 Application.dataPath 的相对路径找，要按候选位置探测、必要时往上层目录找。
    /// </summary>
    private static byte[] LoadHotfixBytes(out string errorMsg)
    {
        errorMsg = null;

        string dllPath = ResolveEditorHotfixDllPath();

        if (dllPath == null)
        {
            Debug.LogError($"编辑器热更 dll 不存在: {HotfixAssemblyName}.dll（Application.dataPath={Application.dataPath}），请先让 Unity 编译通过");
            errorMsg = $"编辑器中找不到 {HotfixAssemblyName}.dll，请先让 Unity 编译通过。";
            return null;
        }

        Debug.Log($"编辑器热更 dll: {dllPath}");
        return File.ReadAllBytes(dllPath);
    }

    /// <summary>
    /// 找到真实工程里的 Library/ScriptAssemblies/{HotfixAssemblyName}.dll。
    /// 找不到返回 null。
    /// </summary>
    private static string ResolveEditorHotfixDllPath()
    {
        string fileName = HotfixAssemblyName + ".dll";

        // 1) 常规工程：Application.dataPath 的上一级就是工程根目录
        string projectDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string direct = Path.Combine(projectDir, "Library", "ScriptAssemblies", fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        // 2) Multiplayer Play Mode 虚拟工程：沿着父目录往上找带 Library/ScriptAssemblies 的那一层
        //    （克隆工程位于 {真实工程}/Library/VP/{clone}，所以要上溯多级）
        DirectoryInfo dir = new DirectoryInfo(projectDir);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            string candidate = Path.Combine(dir.FullName, "Library", "ScriptAssemblies", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
#else
    /// <summary>
    /// 出包后通过 Addressables 取热更入口数据，直接通过Addressable加载
    /// </summary>
    private static byte[] LoadHotfixBytes(out string errorMsg)
    {
        errorMsg = null;

        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(HotfixEntryAddress);
        handle.WaitForCompletion();

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Exception e = handle.OperationException;
            Debug.LogError($"热更入口加载失败: {e?.GetType().Name} {e?.Message}");
            Addressables.Release(handle);
            errorMsg = "热更内容加载失败，无法进入游戏。请退出游戏后重新打开以重新下载。";
            return null;
        }

        byte[] bytes = handle.Result.bytes;
        Addressables.Release(handle);

        return bytes;
    }
#endif

    /// <summary>
    /// 无法进入游戏：弹窗提示具体原因，等待几秒后退出 App（编辑器内停止播放）。
    /// </summary>
    private IEnumerator FailAndQuit(Exception e)
    {
        yield return FailAndQuit("游戏启动失败，无法进入游戏。\n错误：" + e.GetType().Name + "\n" + e.Message);
    }

    private IEnumerator FailAndQuit(string msg)
    {
        BootHide();
        title.Hide();

        Debug.LogError(msg);
        new GS.MessageBoxOk().ShowMsg(msg);

        yield return new WaitForSecondsRealtime(5f);

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static string ToMB(long bytes)
    {
        return (bytes / 1024f / 1024f).ToString("0.0");
    }

    private void BootShow(string msg)
    {
        if (notice == null) return;
        notice.Show(msg);
    }

    private void BootHide()
    {
        if (notice == null) return;
        notice.Hide();
    }

    /// <summary>
    /// 初始化 UI 框架，后续弹窗依赖它，因此必须最先执行。
    /// </summary>
    private void InitUIFramework()
    {
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        GS.Context.uiCanvas = canvas;

        var eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem = eventSystemGO.GetComponent<EventSystem>();
        }

        DontDestroyOnLoad(eventSystem.gameObject);
        GS.Context.uiEventSystem = eventSystem;

        GS.Context.um = new GS.UIManager(GS.Context.uiCanvas);
    }

    /// <summary>
    /// 启动阶段的纯提示层，不挂在 UIManager 栈上，只显示文字。
    /// </summary>
    private class BootNotice
    {
        private GameObject root;
        private Text label;

        public BootNotice()
        {
            root = new GameObject("BootNotice", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(GS.Context.uiCanvas.transform, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var textGO = new GameObject("label", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(root.transform, false);

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(80f, 80f);
            textRect.offsetMax = new Vector2(-80f, -80f);

            label = textGO.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 32;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root.SetActive(false);
        }

        public void Show(string msg)
        {
            label.text = msg;
            root.SetActive(true);
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }
}
