using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

// 各种单例的存储容器
public static class Context
{
    public static SceneController sc;
    public static InputManager im;
    public static SoundSystem ss;
    public static UIManager um;
    public static MessageManager mm;
    private static PanelSettings _globalPanelSettings;
    public static LuaEnv luaEnv;
    public static PanelSettings defaultPanelSettings;
    public static LocalPlayer localPlayer;
    public static NetManager net;

    // 全局逐帧任务调度器。原来挂在 SceneController 上，但它和场景无关，所以上提到 Context。
    public static UpdateProxy updateProxy;

    // UI事件系统
    public static EventSystem uiEventSystem;

    public static GameObject boneRef;

    public static string[] fanNames;

    public static class Item
    {
        public static Clothes clothes;
        // {分类tag: 类型}
        public static Dictionary<string, List<ItemBase>> allItems;
    }

    public static bool bIsInitialized = false;
}

public struct Clothes
{
    public GenderEquipCollection male;
    public GenderEquipCollection female;
}

public struct GenderEquipCollection
{
    public List<ClothBase> hair;
    public List<ClothBase> glasses;
    public List<ClothBase> earring;
    public List<ClothBase> upper;
    public List<ClothBase> hand;
    public List<ClothBase> bottom;
    public List<ClothBase> face;
    public List<ClothBase> stocking;
    public List<ClothBase> shoe;
}
