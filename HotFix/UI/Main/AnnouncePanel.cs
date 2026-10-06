using TMPro;
using UnityEngine.UI;

public class AnnouncePanel : UIBase
{
    private TMP_Text announceText;
    public AnnouncePanel() : base("RulePanel")
    {
        announceText = GetTargetComponent<TMP_Text>("AnnounceContent");
    }

    public void SetAnnounceText(string msg)
    {
        announceText.text = msg;
    }
}
