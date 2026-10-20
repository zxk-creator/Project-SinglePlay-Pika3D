using UnityEngine;
using TMPro;

public class WorldSpaceName : MonoBehaviour
{
    public string characterName;
    public TextMeshPro textComp;
    void Start()
    {
        textComp = GetComponent<TextMeshPro>();
    }

    void LateUpdate()
    {
        var ply = Context.localPlayer;
        if (ply == null) return;
        transform.rotation = ply.GetCamera().gameObject.transform.rotation;
    }

    public void SetName(string newName)
    {
        characterName = newName;
        GetComponent<TextMeshPro>().text = characterName;
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            textComp.text = "";
        }
        else
        {
            textComp.text = characterName;
        }
    }
}