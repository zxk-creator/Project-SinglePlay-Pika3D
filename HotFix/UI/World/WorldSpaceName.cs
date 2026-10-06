using UnityEngine;
using TMPro;

public class WorldSpaceName : MonoBehaviour
{
    public string characterName;

    void Start()
    {
        GetComponent<TextMeshPro>().text = characterName;
    }

    void LateUpdate()
    {
        transform.rotation = Context.localPlayer.GetCamera().gameObject.transform.rotation;
    }

    public void SetName(string newName)
    {
        characterName = newName;
        GetComponent<TextMeshPro>().text = characterName;
    }
}