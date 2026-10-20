
using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using PK;
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
        { ESceneType.CITY, new Vector3(-1460.05f, 30.12f, -827.49f) },
        { ESceneType.TEST, new Vector3(0, 0.5f, 0) }
    };

    public NetManager()
    {
        server = new GameObject().AddComponent<ServerConnect>();
        UnityEngine.Object.DontDestroyOnLoad(server);
        client = new GameObject().AddComponent<ClientConnect>();
        UnityEngine.Object.DontDestroyOnLoad(client);
    }

    private void Update()
    {
        if (_requestCallbacks.Count == 0) return;

        float now = Time.realtimeSinceStartup;
        List<uint> timedOut = null;

        foreach (var kv in _requestCallbacks)
        {
            if (now - kv.Value.SendTime > REQUEST_TIMEOUT)
            {
                if (timedOut == null) timedOut = new List<uint>();
                timedOut.Add(kv.Key);
            }
        }

        if (timedOut != null)
        {
            foreach (uint id in timedOut)
            {
                var entry = _requestCallbacks[id];
                _requestCallbacks.Remove(id);
                entry.OnFailed?.Invoke();
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////连接接口///////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////

    // 连接到服务器(直接连接)
    public void StartClient(int port, string IP, SaveData sv)
    {
        if (messageBox == null)
        {
            messageBox = new MessageBoxWithNoClose();
            messageBox.ShowMsg("正在连接，请稍后！");
        }

        client.Init(IP,port, sv);
        if (!client.StartClient(OnConnectionSuccess, OnConnectionLost, OnResponse))
        {
            messageBox.Hide();
            new MessageBoxOk().ShowMsg("连接到服务器失败，可能是IP或端口填写不正确，请检查后再试一次。");
            return;
        }
        this.sv = sv;

        // 开启一个Update轮询使其能够正确处理回调字典
        Context.updateProxy.RegisterNewTask(Update, int.MaxValue);
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

    /////////////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////连接接口///////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////Client连接成功/失败回调//////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////
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
        if (Context.localPlayer != null)
        {
            UnityEngine.Object.Destroy(Context.localPlayer.gameObject);
            Context.localPlayer = null;
        }
        // 直接返回到主界面（不用发送信号，因为我们的连接在服务器那边已经看不到了）
        Context.sc.EnterNewScene(ESceneType.MAIN_MENU, false);

        _requestCallbacks.Clear();
        _nextRequestId = 0;
        // 取消注册
        Context.updateProxy.DestoryTask(Update);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////Client连接成功/失败回调//////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////回调与信息发送/////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // 存待处理回调的字典，key = requestId
    private struct CallbackEntry
    {
        public Action OnSuccess;
        public Action OnFailed;
        public float SendTime;
    }


    // 回调的通用消息键值对{RequestId, {OnSuccess, OnFailed}}
    private Dictionary<uint, CallbackEntry> _requestCallbacks = new Dictionary<uint, CallbackEntry>();
    // id逐个自增防重复
    private uint _nextRequestId = 0;
    // 缓存Writer减少GC开销
    private NetDataWriter _writer = new NetDataWriter();
    private const float REQUEST_TIMEOUT = 5f;

    /// <summary>
    /// 客户端向服务器发送一条消息，调用方只需构造消息对象。
    /// </summary>
    public void Send<T>(T packet, byte channel = 0, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    where T : INetSerializable
    {
        if (!client.isRunning || client.netPeer == null) return;

        _writer.Reset();
        client.processor.WriteNetSerializable(_writer, ref packet);
        client.netPeer.Send(_writer, channel, method);
    }

    /// <summary>
    /// 客户端向服务器发送一条消息的重载版本，支持传入回调实现发信息后自动调用你的回调
    /// </summary>
    /// <typeparam name="T">消息类型，必须实现 INetSerializable</typeparam>
    /// <param name="packet">要发送的消息对象</param>
    /// <param name="channel">通道号。0=可靠有序（默认），1=状态（高频、可丢）</param>
    /// <param name="method">投递方式。默认 ReliableOrdered</param>
    /// 两个Action分别是成功请求和失败请求回调，若两个都为null则不发。
    public void Send<T>(T packet, byte channel = 0, DeliveryMethod method = DeliveryMethod.ReliableOrdered, Action successCallback = null, Action failCallback = null)
        where T : INetSerializable, INetResponseReceivable
    {
        if (!client.isRunning || client.netPeer == null)
        {
            PK.Log.Error("当前根本没有进行多人游戏，而你尝试发送信息，这不该发生！");
            return;
        }

        if (successCallback != null || failCallback != null)
        {
            _nextRequestId++;
            if (_nextRequestId == 0) _nextRequestId++;

            packet.requestId = _nextRequestId;

            _requestCallbacks[_nextRequestId] = new CallbackEntry
            {
                OnSuccess = successCallback,
                OnFailed = failCallback,
                SendTime = Time.realtimeSinceStartup
            };
        }
        else
        {
            packet.requestId = 0;
        }

        _writer.Reset();
        client.processor.WriteNetSerializable(_writer, ref packet);
        client.netPeer.Send(_writer, channel, method);
    }

    /// <summary>
    /// 服务端回应ACK后自动调用的回调，内部自动找requestCallback进行成功/失败回调
    /// </summary>
    private void OnResponse(ServerCommonResponse request)
    {
        if (_requestCallbacks.TryGetValue(request.requestId, out var callback))
        {
            _requestCallbacks.Remove(request.requestId);

            if (request.success)
                callback.OnSuccess?.Invoke();
            else
                callback.OnFailed?.Invoke();
        }
        else
        {
            PK.Log.Warn($"收到未知的 requestId 回执：{request.requestId}");
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////回调与信息发送/////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
}

// 实现此接口并使用重载版本Send发送信息，即可成功绑定回调
public interface INetResponseReceivable
{
    public uint requestId {get; set;}
}