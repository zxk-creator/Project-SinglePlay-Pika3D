using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;
using System.Collections;
using PK;

public class TitleScreen : UIBase
{
    private GameAcceptButton[] menuButtons;

    public TitleScreen() : base(R.Path.LoginPanel)
    {
        menuButtons = UIPrefab.GetComponentsInChildren<GameAcceptButton>();
        foreach(var btn in menuButtons)
        {
            switch (btn.name)
            {
                case "StartButton":
                    {
                        btn.onClick.AddListener(OnStart);
                        break;
                    }
                    case "ExitButton":
                    {
                        btn.onClick.AddListener(OnExit);
                        break;
                    }
            }
        }
    }
    
    private void OnStart()
    {
        
    }

    private void OnContinue()
    {
        Debug.Log("点击：继续游戏");
        // TODO: 加载存档
    }

    private void OnSettings()
    {
        Debug.Log("点击：设置");
        // TODO: 打开设置面板
    }

    private void OnExit()
    {
        var dg = new MessageBoxOKCancel();
        dg.ShowOkCancel(ExitGame,null,"您确定要退出游戏吗?");
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