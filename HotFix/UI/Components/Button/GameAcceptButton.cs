using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameAcceptButton : Button
{
    // 重写点击事件处理
    public override void OnPointerClick(PointerEventData eventData)
    {
        Context.ss.Play(SoundSystem.EUISoundType.PRESS);
        base.OnPointerClick(eventData);
    }
}
