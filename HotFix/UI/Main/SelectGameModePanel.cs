using System.Collections;
using System.Collections.Generic;
using PKSv;
using UnityEngine;

public class SelectGameModePanel : UIBase
{
    private GameAcceptButton singlePlay;
    private GameAcceptButton multiPlay;
    private GameCancelButton close;
    public SelectGameModePanel(SaveData sv) : base("SelectGameModePanel")
    {
        singlePlay = GetTargetComponent<GameAcceptButton>("SinglePlayBtn");
        multiPlay = GetTargetComponent<GameAcceptButton>("MultiPlayBtn");
        close = GetTargetComponent<GameCancelButton>("CloseButton");

        singlePlay.onClick.AddListener(() =>
        {
            // 单机 = 本机回环：服务器和客户端各跑一个（地址端口取 Client 的配置）
            // Context.net.StartLocalGame();
            // 暂时测试，还是进入游戏
            Context.net.StartLocalPlay(sv);
            Hide();
        });

        
        multiPlay.onClick.AddListener(() =>
        {
            new MultiplayerConnectPanel(sv).Show();
        });

        close.onClick.AddListener(() =>
        {
            Hide();
        });
    }
}
