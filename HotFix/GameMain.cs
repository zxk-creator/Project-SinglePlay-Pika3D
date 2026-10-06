using UnityEngine;
using System.Collections.Generic;
using FunnyChineseName;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.IO;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

// 真正的游戏启动类
public static class GameMain
{
    public static float globalGravity = 20.0f;

    public static Dictionary<ESceneType, string> scenes;
    // 游戏开始的时候调用这个来初始化一切，否则报错。挂到某个GameObject身上即可
    public static void StartGame()
    {
        
        // 防止重复初始化
        if (Context.bIsInitialized) return;
        Context.bIsInitialized = true;

        var uiEventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (uiEventSystem == null)
        {
            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            uiEventSystem = eventSystemGO.GetComponent<EventSystem>();
            Object.DontDestroyOnLoad(eventSystemGO);
        }
        Context.uiEventSystem = uiEventSystem;
        Context.um = new UIManager();

        // 加载物品
        Context.Item.allItems = DBUtil.ParseItem();
        // 加载衣服
        DBUtil.ParseClothing();
        // 加载粉丝列表（使用搞笑中文昵称生成器随机生成100个，风格随意）
        Context.fanNames = FunnyName.GenerateMany(100);
        // 加载lua解释环境
        Context.luaEnv = new LuaEnv();
        var panelH = Addressables.LoadAssetAsync<PanelSettings>("DefaultPanelSettings");
        panelH.WaitForCompletion();
        Context.defaultPanelSettings = panelH.Result;

        scenes = new Dictionary<ESceneType, string>
        {
            { ESceneType.MAIN_MENU, "Main" },
            { ESceneType.DEFAULT, "Hotel" },
            { ESceneType.CITY, "GTAIII" }
        };

        Context.sc = new SceneController(scenes);

        // 全局逐帧任务调度器（原来由 SceneController 创建，已上提到 Context）
        var updateProxyGO = new GameObject("UpdateProxy");
        // 必须跨场景常驻：否则切场景时 UpdateProxy 被销毁，所有任务（TimePass/RefreshProperties）不再执行
        Object.DontDestroyOnLoad(updateProxyGO);
        Context.updateProxy = updateProxyGO.AddComponent<UpdateProxy>();

        var inputProxy = new GameObject("inputProxy");
        Object.DontDestroyOnLoad(inputProxy);
        Context.im = inputProxy.AddComponent<InputManager>();
        Context.mm = new MessageManager();
        // 加载audio AssetBundle
        AssetBundle audio = AssetBundle.LoadFromFile(Path.Combine(Application.persistentDataPath, "hotfix", "audio"));
        Context.ss = new SoundSystem
        {
            press = audio.LoadAsset<AudioClip>("Assets/Audio/Click01.wav"),
            close = audio.LoadAsset<AudioClip>("Assets/Audio/Close.wav"),
            cityBGM = audio.LoadAsset<AudioClip>("Assets/Audio/scenebackgroundmusic/CommonBGM.ogg"),
            titleBGM = audio.LoadAsset<AudioClip>("Assets/Audio/Title.ogg")
        };

        // 设置netManager
        Context.net = new NetManager();

        Context.ss.PlayBGM(SoundSystem.EBGMSoundType.TITLE);

        // 换装和装置面板
        PlaceFurniturePanel.GenerateAllFurnitureButtons();
    }
}
