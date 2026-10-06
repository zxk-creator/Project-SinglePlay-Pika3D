using UnityEngine;
using UnityEngine.UI;
using System;
using PK;
using UnityEngine.UIElements;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;

/// <summary>
///  所有UI的基类，存储了他的功能逻辑和显示资产。我们强制要求每一个Prefab都有一个CanvasGroup，用于控制是否能够接收输入
///  切换场景的时候，所有在场景中的UI都将被销毁
///  支持UTK
/// </summary>
public abstract class UIBase
{
    // 只要正确构造了父类，那么使用的时候一定指的是场景中那个正确的对象，而不是磁盘上的
    // 也可以是一个UTK对象，是的，UTK也被做成Prefab了，他是一个自带UIdocument引用了uxml的GameObject
    public GameObject UIPrefab;
    public GameObject canvasGO;
    // 现在改了，一个UI一个Canvas，为了兼容UTK
    public Canvas canvas;
    public CanvasGroup canvasGroup;
    // 我们要支持的visualElement
    public UIDocument document;
    public bool isUTK = false;
    // UTK 界面专用的 PanelSettings 副本（不共享资产，见构造函数说明）
    private PanelSettings _panelSettings;
    // 资源句柄，不用了会释放
    public AsyncOperationHandle<GameObject> UIPrefabH;
    // 防止句柄被重复释放（UIManager.Pop 与调用方可能都会走到 Destroy）
    private bool _released;
    // 用于UTK的遮挡输入的层
    private VisualElement _blocker;

    // 默认值主要是为了兼容老旧的，不用改了
    public UIBase(string addressName, bool isUTK = false)
    {
        this.isUTK = isUTK;
        // 加载UIPrefan
        UIPrefabH = Addressables.LoadAssetAsync<GameObject>(addressName);
        UIPrefabH.WaitForCompletion();
        UIPrefab = UIPrefabH.Result;
        UIPrefab = UnityEngine.Object.Instantiate(UIPrefab);

        if (!isUTK) {
            canvasGroup = UIPrefab.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                Log.Error("此UIPrefab的根节点上没有挂载CanvasGroup组件！");

            // 实例化canvas，一个UIBase一个Canvas
            canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            // 同样的Canvas设置
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.SetActive(false);
            UIPrefab.transform.SetParent(canvas.transform, false);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        // 是UTK
        else
        {
            // PanelSettings 不再走全局引用，直接用 prefab 上 UIDocument 自己配好的那份
            document = GetTargetComponent<UIDocument>();
            if (document == null)
            {
                Log.Error($"UTK prefab 上没有 UIDocument 组件，地址：{addressName}");
                return;
            }

            // 每份 UTK 界面复制一份自己的 PanelSettings，不再共享资产。
            // 因为 SetStackOrder 会往 PanelSettings 上写 sortingOrder，而共享资产上的值
            // 不随界面销毁复位：上一个 UTK 界面 Pop 后残留的 sortingOrder 会把
            // 之后弹出的 uGUI 弹窗（MessageBoxOKCancel 那类）压在下面，看起来像半透明。
            if (document.panelSettings != null)
            {
                _panelSettings = UnityEngine.Object.Instantiate(document.panelSettings);
                _panelSettings.name = document.panelSettings.name + "_Runtime";
                document.panelSettings = _panelSettings;
            }
            else
            {
                Log.Warn($"UTK prefab 上的 UIDocument 没有指定 PanelSettings，地址：{addressName}");
            }

            // 设置一个_blocker层用于被别人盖住时阻拦输入和点击
            _blocker = new VisualElement { name = "__blocker__" };
            _blocker.style.position = Position.Absolute;
            _blocker.style.left = 0;
            _blocker.style.right = 0;
            _blocker.style.top = 0;
            _blocker.style.bottom = 0;
            _blocker.pickingMode = PickingMode.Position;
            _blocker.style.display = DisplayStyle.None;

            document.rootVisualElement.Add(_blocker);
            // 设置隐藏
            document.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    public virtual void Show()
    {
        Context.um.Push(this);
    }
    public virtual void Hide()
    {
        Context.um.Pop(this);
    }

    public virtual void Destroy()
    {
        UnityEngine.Object.Destroy(UIPrefab);

        if (!isUTK)
        {
            // UGUI 那份 canvas 是自己 new 出来的，需要销毁
            UnityEngine.Object.Destroy(canvasGO);
        }
        else if (_panelSettings != null)
        {
            // 本体是共享资产不能销毁；这份是运行时复制出来的副本，必须自己销毁
            UnityEngine.Object.Destroy(_panelSettings);
            _panelSettings = null;
        }

        // 句柄只能释放一次
        if (!_released)
        {
            _released = true;
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

    /// <summary>
    /// 仅支持UGUI
    /// </summary>
    public void SetParent(GameObject newParent)
    {
        if (newParent == null || isUTK) return;

        UIPrefab.transform.SetParent(newParent.transform, false);

        // 销毁原来的Canvas，他已经不需要了
        if (canvasGO != null)
        {
            UnityEngine.Object.Destroy(canvasGO);
            canvasGO = null;
            canvas = null;
        }
    }

    /// <summary>
    /// 仅支持UGUI
    /// </summary>
    public void SetParent(Transform newParentTransform)
    {
        if (newParentTransform == null || isUTK) return;

        UIPrefab.transform.SetParent(newParentTransform, false);

        // 销毁原来的Canvas，他已经不需要了
        if (canvasGO != null)
        {
            UnityEngine.Object.Destroy(canvasGO);
            canvasGO = null;
            canvas = null;
        }
    }

    // 设置排序顺序
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            if (isUTK)
                document.rootVisualElement.style.display = DisplayStyle.Flex;
            else
                canvasGO.SetActive(true);
        }
        else
        {
            if (isUTK)
                document.rootVisualElement.style.display = DisplayStyle.None;
            else
                canvasGO.SetActive(false);
        }
    }

    public void SetInteractable(bool v)
    {
        if (!isUTK)
        {
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
        }
        else
        {
            _blocker.style.display = v? DisplayStyle.None : DisplayStyle.Flex;
            // 如果聚焦比如输入框了，则取消
            if (!v && document.rootVisualElement.focusController?.focusedElement
                 is VisualElement f)
                f.Blur();
        }
    }

    public void SetStackOrder(int index)
    {
        if (!isUTK)
        {
            canvas.sortingOrder = index;
        }
        else
        {
            document.panelSettings.sortingOrder = index;
        }
    }
}
