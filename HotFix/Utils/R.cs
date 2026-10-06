using UnityEngine;


/// <summary>
/// 集中管理所有硬编码字符串（资源路径、场景名、动画名、UI节点名等）。
/// 改路径时只需改这里，其余代码引用常量。
/// </summary>
public static class R
{
    /// <summary>Resources 下的预制体/资源路径</summary>
    public static class Path
    {
        // UI 面板
        public const string LoginPanel = "LoginPanel";
        public const string LoadingScenePanel = "LoadingScenePanel";
        public const string MessageBoxOKCancel = "MessageBoxOKCancel";
        public const string ChangeAvatarPanel = "ChangeAvatarPanelModified";
        public const string InventoryItem = "InventoryItem";
        public const string ChangeAvatarScreenItemButton = "ChangeAvatarItemButton";
        public const string BagPanelPath = "BagPanel";
    }
    
    public static class JsonName
    {
        public static string jsonStuffNode = "stuff";
        public static string jsonBagNode = "bag";
        public static string jsonClothNode = "cloth";
    }
}
