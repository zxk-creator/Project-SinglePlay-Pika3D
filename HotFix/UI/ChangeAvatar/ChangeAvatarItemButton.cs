using UnityEngine;
using UnityEngine.UI;
using PK;

public class ChangeAvatarItemButton : UIBase
{
    private ClothBase ownedCloth;
    public ChangeAvatarItemButton(ClothBase cloth) : base(R.Path.ChangeAvatarScreenItemButton)
    {
        UIPrefab.GetComponentInChildren<GameAcceptButton>(true).onClick.AddListener(ChangeCloth);

        var ItemImage = GetTargetComponent<Image>("ItemImage");
        // 设置Item图标等
        ItemImage.sprite = Resources.Load<Sprite>(cloth.iconPath);
        ItemImage.color = Color.white;
        ownedCloth = cloth;
    }

    private void ChangeCloth()
    {
        Log.Info("按钮被点击了！");
        var Cloth = ownedCloth.GetACopy() as ClothBase;
        if (Cloth == null)
        {
            Log.NullPtr("AvatarButton: ChangeCloth");
        }
        Context.localPlayer.inventory.ChangeCloth(Cloth, true);
    }

    public override void Show()
    {
        UIPrefab.SetActive(true);
    }

    /// <summary>
    /// 仅仅隐藏，清空父级防止跟顶层父级一块被销毁
    /// </summary>
    public override void Hide()
    {
        UIPrefab.transform.SetParent(canvas.transform);
        UIPrefab.SetActive(false);
    }

}
