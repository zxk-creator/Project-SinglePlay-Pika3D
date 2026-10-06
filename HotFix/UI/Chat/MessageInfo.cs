using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MessageInfo : UIBase
{
    private TMP_Text msgText;
    public MessageInfo(string msg) : base("MessageInfo")
    {
        msgText = GetTargetComponent<TMP_Text>();

        if (msg == null) return;
        msgText.text = msg;
    }
}
