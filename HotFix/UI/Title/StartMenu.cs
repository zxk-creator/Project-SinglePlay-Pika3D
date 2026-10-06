using UnityEngine.UIElements;

public class StartMenu : UIBase
{
    private Button btnNew;
    private Button btnLoad;
    private Button btnSettings;
    private Button btnQuit;

    public StartMenu() : base("StartMenu", isUTK: true)
    {
        var root = document.rootVisualElement;

        btnNew = root.Q<Button>("btn-new");
        btnLoad = root.Q<Button>("btn-load");
        btnSettings = root.Q<Button>("btn-settings");
        btnQuit = root.Q<Button>("btn-quit");

        btnNew.clicked += OnNewGame;
        btnLoad.clicked += OnLoad;
        btnSettings.clicked += OnSettings;
        btnQuit.clicked += OnQuit;
    }

    private void OnNewGame()
    {
        // 检测存档插槽是否满了
        if (PKSv.SvMgr.FindEmptySlot() == -1)
        {
            new PromptMessage("存档插槽已满！请删掉一些存档后再试").Show();
            return;
        }

        new NewSavePanel(null).Show();
    }
    private void OnLoad() { new SaveMenu().Show(); }
    private void OnSettings() { PK.Log.Info("游戏设置"); }
    private void OnQuit() {
        var dg = new MessageBoxOKCancel();
        dg.ShowOkCancel(ExitGame, null, "您确定要退出游戏吗?");
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
    }
}
