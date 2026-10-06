using UnityEngine.UIElements;

[UxmlElement("CancelButton")]
public partial class UTKCancelButton : Button
{

    public UTKCancelButton()
    {
        clicked += OnClicked;
    }
    private void OnClicked()
    {
        Context.ss.Play(SoundSystem.EUISoundType.CLOSE);
    }
}
