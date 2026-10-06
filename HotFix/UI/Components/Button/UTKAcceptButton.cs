using UnityEngine.UIElements;

[UxmlElement("AcceptButton")]
public partial class UTKAcceptButton : Button
{
    
    public UTKAcceptButton()
    {
        clicked += OnClicked;
    }
    private void OnClicked()
    {
        Context.ss.Play(SoundSystem.EUISoundType.PRESS);
    }
}
