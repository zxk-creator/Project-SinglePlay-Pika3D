
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using PKSv;
using PKWeb;
using UnityEngine;

// 所有的已知发送的消息枚举
public enum EMessageType
{
    ClientEnterNewScene
}

/// <summary>
/// 负责new出客户端和服务端
/// </summary>
public class NetManager
{
    public ServerConnect server;
    public ClientConnect client;
    // 没有关闭按钮的提示框，关闭依赖UIManager切换场景全部销毁机制，或连接失败后隐藏机制
    private MessageBoxWithNoClose messageBox;
    public SaveData sv;

    // 出生点配置，替代文本配置。懒得配文本了
    public Dictionary<ESceneType, Vector3> spawnPoint = new Dictionary<ESceneType, Vector3>
    {
        { ESceneType.CITY, new Vector3(-1460.05f, 15.12f, -827.49f) }
    };

    public NetManager()
    {
        server = new GameObject().AddComponent<ServerConnect>();
        Object.DontDestroyOnLoad(server);
        client = new GameObject().AddComponent<ClientConnect>();
        Object.DontDestroyOnLoad(client);
    }

    // 开启一个服务器连接
    public void StartHost(int port, SaveData sv)
    {
        // 防止玩家不知道发生了什么，导致重复点击出bug
        if (messageBox == null)
        {
            messageBox = new MessageBoxWithNoClose();
            messageBox.ShowMsg("正在连接，请稍后！");
        }
        server.Init(port, sv);
        if (!server.StartServer(() => { new PromptMessage("服务器启动成功！").Show(); }))
        {
            messageBox.Hide();
            new MessageBoxOk().ShowMsg("服务器启动失败，请尝试填写别的端口后再试一次。");
            return;
        }
    }

    // 连接到服务器(直接连接)
    public void StartClient(int port, string IP, SaveData sv)
    {
        if (messageBox == null)
        {
            messageBox = new MessageBoxWithNoClose();
            messageBox.ShowMsg("正在连接，请稍后！");
        }

        client.Init(IP,port, sv);
        if (!client.StartClient( OnConnectionSuccess, OnConnectionLost))
        {
            messageBox.Hide();
            new MessageBoxOk().ShowMsg("连接到服务器失败，可能是IP或端口填写不正确，请检查后再试一次。");
            return;
        }
        this.sv = sv;
    }

    private void OnConnectionSuccess(Vector3 spawnLocation, ESceneType sceneType)
    {
        new PromptMessage("连接成功！").Show();
    }

    /// <summary>
    /// 连接被强制丢失了
    /// </summary>
    /// <param name="res">原因</param>
    private void OnConnectionLost(string res)
    {
        new MessageBoxOk().ShowMsg("连接已丢失，原因：" + res);
        // 先销毁玩家
        Object.Destroy(Context.localPlayer.gameObject);
        // 直接返回到主界面（不用发送信号，因为我们的连接在服务器那边已经看不到了）
        Context.sc.EnterNewScene(ESceneType.MAIN_MENU,false);
    }

    // 完全单机游玩
    public void StartLocalPlay(SaveData sv)
    {
        StartHost(9999, sv);
        StartClient(9999, "127.0.0.1", sv);
    }

    public void StopServer()
    {
        if (!server.isRunning)
        {
            PK.Log.Info("服务器没有在运行，无需关闭");
            return;
        }
        server.StopServer();
    }

    public void DisconnectFromServer()
    {
        if (!client.isRunning)
        {
            PK.Log.Info("客户端没有连接到任何服务器，无需关闭");
            return;
        }
        client.Disconnect();
    }

    /// <summary>
    /// 客户端向服务器发送一条消息。
    /// 自动处理类型哈希写入和连接检查，调用方只需构造消息对象。
    /// </summary>
    /// <typeparam name="T">消息类型，必须实现 INetSerializable</typeparam>
    /// <param name="packet">要发送的消息对象</param>
    /// <param name="channel">通道号。0=可靠有序（默认），1=状态（高频、可丢）</param>
    /// <param name="method">投递方式。默认 ReliableOrdered</param>
    /// <remarks>未连接时记录错误日志并静默返回，不会抛出异常。</remarks>
    public void Send<T>(T packet, byte channel = 0, DeliveryMethod method = DeliveryMethod.ReliableOrdered) where T : INetSerializable
    {
        if (!client.isRunning || client.netPeer == null)
        {
            PK.Log.Error("当前根本没有进行多人游戏，而你尝试发送信息，这不该发生！");
            return;
        }

        var w = new NetDataWriter();
        client.processor.WriteNetSerializable(w, ref packet);
        client.netPeer.Send(w, channel, method);
    }
}