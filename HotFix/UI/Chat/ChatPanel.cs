using UnityEngine.UI;
using TMPro;
using System;

public class ChatPanel : UIBase,IInputAcceptable
{
    public TMP_InputField messageInput {get; private set;}
    private GameAcceptButton sendBtn;

    /// <summary>
    /// new出来就是聊天用的。
    /// </summary>
    public ChatPanel() : base("Chat")
    {
        messageInput = GetTargetComponent<TMP_InputField>("InputField");
        messageInput.onSubmit.AddListener(OnSubmitHandler);

        sendBtn = GetTargetComponent<GameAcceptButton>();
        sendBtn.onClick.AddListener(Send);

    }

    public override void Show()
    {
        base.Show();
        messageInput.Select();
        messageInput.ActivateInputField();
    }

    // 点一下就销毁自身
    public void OnBeginChatPressed()
    {
        if (messageInput.isFocused)
            return;
        Hide();
    }

    public void OnOpenBagPressed()
    {
        Hide();
    }

    private void OnSubmitHandler(string msg)
    {
        Context.mm.Command(msg);
        messageInput.text = "";
    }

    // 发送
    private void Send()
    {
        Context.mm.Command(messageInput.text);
        messageInput.text = "";
    }
}
