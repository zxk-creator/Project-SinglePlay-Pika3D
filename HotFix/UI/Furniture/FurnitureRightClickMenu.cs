using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureRightClickMenu : UIBase
{
    private SceneObjectBase target;
    private GameAcceptButton recycle;
    private GameAcceptButton rotateLeft;
    private GameAcceptButton rotateRight;

    public FurnitureRightClickMenu(SceneObjectBase target) : base("FurnitureRightClickMenu")
    {
        this.target = target;
        recycle = GetTargetComponent<GameAcceptButton>("RecycleBtn");
        rotateLeft = GetTargetComponent<GameAcceptButton>("RotateLeftBtn");
        rotateRight = GetTargetComponent<GameAcceptButton>("RotateRightBtn");

        if (!target.canRecycle) recycle.interactable = false;

        recycle.onClick.AddListener( () =>
        {
            if (target.canRecycle == false)
            {
                new PromptMessage("此家具不能回收！").Show();
                return;
            }
            else
            {
                target.Destroy(Context.localPlayer);
            }

            Hide();
        });

        // 设置Transform到鼠标位置
        UIPrefab.GetComponent<RectTransform>().position = Input.mousePosition;
        
        rotateLeft.onClick.AddListener(() => { target.RotateLeft(); Hide(); });
        rotateRight.onClick.AddListener(() => { target.RotateRight(); Hide(); });
    }

}
