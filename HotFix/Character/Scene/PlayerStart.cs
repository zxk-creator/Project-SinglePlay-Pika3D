using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 安上这个组件，GameObject秒变角色出生点
public class PlayerStart : MonoBehaviour
{
    /*
    public GameObject playerObjRef;
    void Start()
    {
        // 说明没有出生，直接出生
        if (Context.singlePlayer == null)
        {
            var obj = Instantiate(playerObjRef, transform.position, transform.rotation);
            DontDestroyOnLoad(obj);
            Context.singlePlayer = obj.GetComponent<LocalPlayer>();
        }
        // 有，则传送
        else
        {
            Context.singlePlayer.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }
    */
}
