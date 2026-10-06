using UnityEngine;
using PK;

public class PickupableItems : MonoBehaviour
{
    private Transform playerCamera;
    private ItemBase item;
    public SpriteRenderer spriteRenderer;
    public BoxCollider trigger;

    void Start()
    {
        playerCamera = Context.localPlayer.GetCamera().transform;
    }

    void Update()
    {
        if (playerCamera == null) playerCamera = Context.localPlayer.GetCamera().transform;
        transform.rotation = playerCamera.rotation;
    }

    public void SetPickupItem(ItemBase item)
    {
        if (item == null)
        {
            Log.NullPtr("SetPickupItem");
            return;
        }

        this.item = item;
        UpdateSprite();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<LocalPlayer>() == null) return;
        if (item == null) return;

        LocalPlayer player = Context.localPlayer;
        if (player == null || player.inventory == null) return;

        ItemBase remain = player.inventory.TryPutIntoBag(item);
        if (remain == null)
        {
            Log.Info("成功装入背包，掉落物已销毁：" + item.itemName);
            Destroy(gameObject);
        }
        else if (ReferenceEquals(remain, item))
        {
            Log.Info("背包已满，无法装入掉落物：" + item.itemName);
        }
        else
        {
            Log.Info("背包空间不足，只装入了一部分：" + item.itemName + "，剩余" + remain.currentStackCount + "个");
            item = remain;
            UpdateSprite();
        }
    }

    private void UpdateSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(item.iconPath);
        if (sprite == null)
        {
            Log.Error("加载掉落物精灵失败：" + item.iconPath);
            return;
        }

        spriteRenderer.sprite = sprite;
    }
}
