using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FurnitureDragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CanvasGroup canvasGroup;
    public FurnitureBase currentItem;
    private GameObject dragClone;
    [SerializeField] private float originalAlpha = 0.6f;
    // 场景中实例化好的GameObject，用于销毁
    private GameObject instancedSceneObject;
    private List<RaycastResult> detectedUI = new List<RaycastResult>();
    private bool canPlace = false;
    public LayerMask excludeLayer;

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

        // 清空检测到的物体
        detectedUI.Clear();
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragClone.transform.position = Input.mousePosition;

        // 检测是否还命中UI
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = eventData.position };
        EventSystem.current.RaycastAll(pointerData, detectedUI);

        // 执行场景射线检测，在检测到的位置实例化家具物体
        if (detectedUI.Count <= 0)
        {
            if (instancedSceneObject == null)
            {
                var filePrefab = Resources.Load<GameObject>(currentItem.prefabPath);
                instancedSceneObject = Instantiate(filePrefab);
                // 设置碰撞为关闭
                Collider[] allCollider = instancedSceneObject.GetComponents<Collider>();
                foreach (var c in allCollider)
                {
                    c.enabled = false;
                }
            }
            // 移动
            Vector3 screenPos = Input.mousePosition;
            Ray ray = Context.localPlayer.GetCamera().ScreenPointToRay(screenPos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 20.0f, ~excludeLayer))
            {
                instancedSceneObject.transform.position = hit.point;
                canPlace = true;
            }
            // 没有命中，则销毁，表示不可放置
            else
            {
                DestroyPreviewPrefab();
                canPlace = false;
            }
        }
        // 有UI，销毁，不显示
        else
        {
            DestroyPreviewPrefab();
            canPlace = false;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 可以放，不销毁
        if (canPlace)
        {
            var sfb = instancedSceneObject.AddComponent<SceneFurnitureBase>();
            // 需要进行深拷贝，否则就会修改模板对象。
            sfb.furniture = currentItem.GetACopy() as FurnitureBase;
            sfb.canRecycle = currentItem.canRecycle;
            sfb.canInteract = currentItem.canInteract;
            // 开碰撞
            Collider[] allCollider = instancedSceneObject.GetComponents<Collider>();
            foreach (var c in allCollider)
            {
                c.enabled = true;
            }
            // 但是要把自己的引用置空，否则以后移动的就是放置后的gameObj了
            instancedSceneObject = null;
        }
        // 不能放
        else
        {
            DestroyPreviewPrefab();
        }

        Destroy(dragClone);

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    private void DestroyPreviewPrefab()
    {
        detectedUI.Clear();
        Destroy(instancedSceneObject);
        instancedSceneObject = null;
    }
}
