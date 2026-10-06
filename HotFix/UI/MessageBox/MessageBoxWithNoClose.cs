using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class MessageBoxWithNoClose : UIBase
{
    private TMP_Text message;
    private GameAcceptButton okBtn;
    private TaskCompletionSource<bool> tcs;

    public MessageBoxWithNoClose() : base("MessageBoxWithNoClose")
    {
        message = GetTargetComponent<TMP_Text>("message");
        okBtn = GetTargetComponent<GameAcceptButton>("okButton");

        okBtn.onClick.AddListener(() =>
        {
            Hide(); 
        });
    }

    public void ShowMsg(string msg)
    {
        message.text = msg;
        Show();
    }

    public Task ShowMsgAsync(string msg)
    {
        // 防止重复调用
        if (tcs != null && !tcs.Task.IsCompleted)
            tcs.TrySetResult(true);

        tcs = new TaskCompletionSource<bool>();
        message.text = msg;
        Show();
        return tcs.Task;
    }
}
