using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GS;

/// <summary>
/// 所有UI的基类，存储了他的功能逻辑和显示资产。
/// 我们强制要求每一个Prefab都有一个CanvasGroup，用于控制是否能够接收输入。
/// 切换场景的时候，所有在场景中的UI都将被销毁。
/// 资源通过 Addressables 按地址加载（地址即 Prefab 文件名），仅支持 UGUI。
/// </summary>
public abstract class UIBase
{
    // 只要正确构造了父类，那么使用的时候一定指的是场景中那个正确的对象，而不是磁盘上的
    public GameObject UIPrefab;
    public Canvas canvas;
    public CanvasGroup canvasGroup;

    // 资源句柄，Destroy 时释放
    public AsyncOperationHandle<GameObject> UIPrefabH;

    public UIBase(string addressName)
    {
        canvas = GS.Context.uiCanvas;
        if (canvas == null)
        {
            Debug.LogError("GS.Context.uiCanvas 为空！请确认 GameShell.Awake 已创建 UI Canvas。");
            return;
        }

        // 通过 Addressables 按地址加载
        UIPrefabH = Addressables.LoadAssetAsync<GameObject>(addressName);
        UIPrefabH.WaitForCompletion();

        if (UIPrefabH.Status != AsyncOperationStatus.Succeeded || UIPrefabH.Result == null)
        {
            Debug.LogError($"Addressable 加载失败，地址：{addressName}，原因：{UIPrefabH.OperationException?.Message}");
            return;
        }

        UIPrefab = UIPrefabH.Result;

        // 实例化出来的是场景中自己的那份，不能直接改 Addressables 返回的资源
        UIPrefab = UnityEngine.Object.Instantiate(UIPrefab);
        UIPrefab.transform.SetParent(canvas.transform, false);

        // 这个 Canvas 由 GameShell.InitUIFramework 创建，已经设好 ScreenSpaceOverlay 与缩放
        UIPrefab.SetActive(false);

        canvasGroup = UIPrefab.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            Debug.LogError($"此UIPrefab的根节点上没有挂载CanvasGroup组件！地址：{addressName}");
    }

    public virtual void Show()
    {
        GS.Context.um.Push(this);
    }
    public virtual void Hide()
    {
        GS.Context.um.Pop(this);
    }

    public virtual void Destroy()
    {
        UnityEngine.Object.Destroy(UIPrefab);
        UIPrefab = null;
        canvasGroup = null;

        // 释放 Addressables 句柄
        if (UIPrefabH.IsValid())
        {
            Addressables.Release(UIPrefabH);
        }
    }

    public T GetTargetComponent<T>(string name) where T : Component
    {
        T[] res = UIPrefab.GetComponentsInChildren<T>(true);
        foreach (T entry in res)
        {
            if (entry.name == name)
                return entry;
        }

        return null;
    }

    /// <summary>
    /// 重载版本：深度优先搜索获取Component
    /// </summary>
    public T GetTargetComponent<T>() where T : Component
    {
        return SearchInDepth<T>(UIPrefab.transform);
    }

    private T SearchInDepth<T>(Transform current) where T : Component
    {
        T component = current.GetComponent<T>();
        if (component != null)
            return component;

        // 遍历所有子节点
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            T result = SearchInDepth<T>(child);
            if (result != null)
                return result;
        }

        // 全部没找到
        return null;
    }

    // 深度优先搜索获得Transform
    protected Transform FindChildRecursive(Transform current, string name)
    {
        if (current.name == name) return current;
        foreach (Transform child in current.transform)
        {
            var res = FindChildRecursive(child, name);
            if (res != null) return res;
        }

        return null;
    }

    public void SetParent(GameObject newParent)
    {
        if (newParent == null) return;
        UIPrefab.transform.SetParent(newParent.transform);
    }

    public void SetParent(Transform newParentTransform)
    {
        if (newParentTransform == null) return;
        UIPrefab.transform.SetParent(newParentTransform);
    }
}
