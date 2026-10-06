
using System.Collections.Generic;
using UnityEngine;
using PK;
using PKWeb;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using PKSv;

public interface IEnterNewScene
{
    public void OnBeginEnterNewScene(ESceneType newScene);
    public void OnNewSceneEntered(ESceneType newScene);
}

public enum ESceneType
{
    // 若传送点在本场境内，则设置为NONE 
    NONE,
    DEFAULT,
    CITY,
    MAIN_MENU
}

// 管理当前活跃的关卡，时间，天气等，全局唯一，服务器权威，谁来都只能获得数据。
// 目前暂未开放自定义场景数据。
// 多人也是用这个，但仅存在于客户端，玩家自己控制
public class SceneController
{
    // 所有的网络玩家
    // 只需要记住当前打开的场景的人数即可
    // 注意，我们记住的是他的真身，场景中那个人。要销毁一起销毁掉
    public List<NetPlayer> scenePlayers = new List<NetPlayer>();

    // 所有的场景键值对
    public Dictionary<ESceneType, string> scenes;

    // 存储一个NetPlayer资源的Handle，使其运行时能够迅速加载，不会释放，永久存在
    public AsyncOperationHandle<GameObject> netPlayerResourceHandle;
    // 本地玩家的
    public AsyncOperationHandle<GameObject> localPlayerResourceHandle;

    public ESceneType currentScene { get; private set; }
    private static List<IEnterNewScene> enterNewScenesCallbacks;


    public SceneController(Dictionary<ESceneType, string> scenes)
    {
        this.scenes = scenes;

        currentScene = ESceneType.MAIN_MENU;

        // 订阅进入新场景事件
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => {
            Log.Info("[SceneController] sceneLoaded 触发 scene=" + scene.name + " 当前currentScene=" + currentScene);
            CallNewSceneEntered();
        };

        // 加载资源
        netPlayerResourceHandle = Addressables.LoadAssetAsync<GameObject>("NetPlayer");
        netPlayerResourceHandle.WaitForCompletion();
        localPlayerResourceHandle = Addressables.LoadAssetAsync<GameObject>("LocalPlayer");
        localPlayerResourceHandle.WaitForCompletion();
    }

    /// <summary>
    /// 注意：进入场景全部通过此方法进入，没有别的
    /// 传入false，适用于第一次进入场景，仅本地视觉化操作，不发送到服务端防止出问题
    /// 只有在连接到服务器的时候可用，会向服务器发送数据
    /// </summary>
    /// <param name="newScene">强类型代表的方法类型</param>
    /// <param name="sendToServer">是否纯本地操作，不告知服务器</para,>
    public void EnterNewScene(ESceneType newScene, bool sendToServer)
    {
        if (scenes.TryGetValue(newScene, out string sceneName))
        {
            Log.Info("进入新的场景" + sceneName);
            CallBeginEnterNewScene(newScene);

            // 注意！这里不会阻塞C#执行线程！所以由sceneLoaded注册调用钩子事件。
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            currentScene = newScene;

            // 如果不需要发送到服务端，那就不发了下面的直接跳过
            if (!sendToServer) return;
            // 如果需要，那么借助NetManager进行发送
            var req = new ClientEnterNewScene();
            req.scene = newScene;

            Context.net.Send(req);

            switch (newScene)
            {
                case ESceneType.MAIN_MENU:
                    {
                        Cursor.lockState = CursorLockMode.None;

                        break;
                    }
                case ESceneType.CITY:
                    {
                        
                        break;
                    }
                default:
                    {
                        // 不负责玩家出生，由PlayerStart负责。因为LoadScene不会阻塞到这里。                        Context.updateProxy.RegisterNewTask(TimePass,int.MaxValue,1);
                        break;
                    }
            }

            return;
        }
        
        Log.Error("未找到场景" + newScene + "!");
    }

    public void RemovePlayerById(long id)
    {
        var p = scenePlayers.Find(p => p.netPlayer.playerId == id);
        if (p == null)
        {
            Log.Error("没有这个玩家，这不该发生！");
            return;
        }

        scenePlayers.Remove(p);
        Object.Destroy(p.gameObject);
    }

    /// <summary>
    /// 生成一个玩家(仅限网络玩家！)，内置回填进场景List的，外部无需继续回填
    /// </summary>
    public void SpawnANetPlayer(NetPlayerData playerData)
    {
        var playerObj = netPlayerResourceHandle.Result;
        // 需要实例化后再用！
        var instancedObj = Object.Instantiate(playerObj);
        // 位置和旋转都按服务端发来的数据摆
        instancedObj.transform.SetPositionAndRotation(playerData.position, playerData.rotation);

        var netPlayerCom = instancedObj.GetComponent<NetPlayer>();
        if (netPlayerCom == null)
        {
            Log.Error("[SceneController] NetPlayer 预制体的根节点上没有挂 NetPlayer 脚本，无法生成其他玩家");
            Object.Destroy(instancedObj);
            return;
        }

        netPlayerCom.netPlayer = playerData;
        // 套用他的着装和性别（骨架没建好时 SetOutfit 会先攒着，等 NetPlayer.Start 再补）
        netPlayerCom.SetOutfit(playerData.playerData.equipedCloth, playerData.playerData.isGirl);

        // 加进列表
        scenePlayers.Add(netPlayerCom);
        // 我们手动管理，不让他因为切换场景被销毁
        Object.DontDestroyOnLoad(instancedObj);
    }

    // 生成本地玩家（全游戏仅此一个！）
    public void SpawnALocalPlayer(SaveData playerData, Vector3 spawnLocation)
    {
        var playerObj = localPlayerResourceHandle.Result;
        var instancedObj = Object.Instantiate(playerObj);

        instancedObj.transform.SetPositionAndRotation(spawnLocation, Quaternion.identity);

        var localPlayer = instancedObj.GetComponent<LocalPlayer>();
        if (localPlayer == null)
        {
            Log.Error("[SceneController] LocalPlayer 预制体的根节点上没有挂 LocalPlayer 脚本，无法生成本地玩家");
            Object.Destroy(instancedObj);
            return;
        }

        Context.localPlayer = localPlayer;
        Context.localPlayer.playerSaveData = playerData;

        // 不会加入到sc的NetPlayer列表，我不属于控制范围之内！
        // 不让他销毁
        Object.DontDestroyOnLoad(instancedObj);
    }

    public void CallNewSceneEntered()
    {
        // 快照遍历：回调中可能增删注册者
        foreach (var callback in new List<IEnterNewScene>(enterNewScenesCallbacks))
        {
            callback.OnNewSceneEntered(currentScene);
        }

        // 设置本地玩家为非运动学的（加个？是因为进入TITLE的时候localPlayer根本没有，一跑到这就抛异常）
        Context.localPlayer?.SetRigidbodyKinematic(false);
    }

    public void CallBeginEnterNewScene(ESceneType newScene)
    {
        if (enterNewScenesCallbacks != null)
        {
            // 快照遍历：回调中可能增删注册者
            foreach (var callback in new List<IEnterNewScene>(enterNewScenesCallbacks))
            {
                callback.OnBeginEnterNewScene(newScene);
            }
        }
    }

    public static void RegisterEnterNewSceneCallback(IEnterNewScene who)
    {
        if (enterNewScenesCallbacks == null) enterNewScenesCallbacks = new List<IEnterNewScene>(5);
        if (!enterNewScenesCallbacks.Contains(who))
            enterNewScenesCallbacks.Add(who);
    }

    public static void UnregisterEnterNewSceneCallback(IEnterNewScene who)
    {
        enterNewScenesCallbacks.Remove(who);
    }
}