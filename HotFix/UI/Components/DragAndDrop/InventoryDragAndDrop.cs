using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PK;

// 把这个挂在想要的GameObject上，就能实现拖拽赋值！
public class InventoryDragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private float originalAlpha = 0.6f;

    private GameObject dragClone;
    private CanvasGroup canvasGroup;

    public ItemBase draggingItem;
    public BagPanel bagPanelRef;
    public EOperateLocation owner;
    // 用于销毁整个面板
    public InventoryItem inventoryItemRef;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = originalAlpha;
        canvasGroup.blocksRaycasts = false;

        Canvas rootCanvas = GetComponentInParent<Canvas>();

        dragClone = Instantiate(gameObject, rootCanvas.transform);

        CanvasGroup cloneGroup = dragClone.GetComponent<CanvasGroup>();
        if (cloneGroup == null)
        {
            cloneGroup = dragClone.AddComponent<CanvasGroup>();
        }
        cloneGroup.blocksRaycasts = false;
        cloneGroup.interactable = false;

        dragClone.transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragClone.transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        GameObject currentUI = eventData.pointerCurrentRaycast.gameObject;
        if (currentUI == null)
        {
            Log.Info("拖拽结束点没有检测到任何UI！已返回。");
            Destroy(dragClone);
            return;
        }

        // 从当前物体开始，向上遍历父级，直到根
        Transform current = currentUI.transform;
        bool matched = false;
        bool accepted = false;
        while (current != null)
        {
            var itf = current.GetComponent<IDropRecevier>();
            if (itf != null)
            {
                matched = true;
                accepted = itf.OnDropEnd(owner, draggingItem);
                break;
            }
            current = current.parent;
        }

        if (!matched)
        {
            Log.Info("未匹配到任何指定Tag或此物品不是衣服无法装备。");
        }

        Destroy(dragClone);
    }
}