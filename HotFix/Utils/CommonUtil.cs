using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PK;

public static class Util
{
    public static class Cam {
        public static void SetViewTargetWithSmooth(Camera oldCam,Camera newCam,float duration, Action finishedCallback)
        {
            duration += 0.01f;
            var oldCamLoc = oldCam.transform.position;
            var oldCamRot = oldCam.transform.rotation;
            var newCamLoc = newCam.transform.position;
            var newCamRot = newCam.transform.rotation;

            float elapsedTime = 0;

            oldCam.enabled = false;
            newCam.enabled = true;
            newCam.transform.SetPositionAndRotation(oldCamLoc, oldCamRot);

            Context.updateProxy.RegisterNewTask(
                () =>
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / duration);
                    newCam.transform.position = Vector3.Lerp(oldCamLoc, newCamLoc,t);
                    newCam.transform.rotation = Quaternion.Lerp(oldCamRot,newCamRot,t);

                    if (elapsedTime >= duration)
                    {
                        finishedCallback?.Invoke();
                    }
                }, duration
            );
        }

    }

    public static SceneFurnitureBase SummonFuriture(FurnitureBase furniture, Vector3 position, Quaternion rotation)
    {
        if (CheckNull(furniture)) return null;

        var fileObj = Resources.Load<GameObject>(furniture.prefabPath);
        var instancedObj = UnityEngine.Object.Instantiate(fileObj);

        instancedObj.transform.SetPositionAndRotation(position, rotation);

        SceneFurnitureBase sceneFurniture = instancedObj.AddComponent<SceneFurnitureBase>();
        sceneFurniture.furniture = furniture;
        sceneFurniture.canRecycle = furniture.canRecycle;
        sceneFurniture.canInteract = furniture.canInteract;
        return sceneFurniture;
    }

    public static class PostProcessUtil
    {
        public static Volume CreateGrayScreen()
        {
            GameObject volumeGo = new GameObject("GrayDeathVolume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.saturation.Override(-100f);
            colorAdjustments.postExposure.Override(0.15f);
            volume.sharedProfile = profile;

            return volume;
        }
    }

    public static class ItemLevelColor
    {
        public static readonly Color Commom = new Color32(255, 255, 255, 255);
        public static readonly Color Uncommon = new Color32(7, 140, 0, 255);
        public static readonly Color Rare = new Color32(0, 175, 255, 255);
        public static readonly Color Epic = new Color32(208, 0, 255, 255);
        public static readonly Color Gold = new Color32(255, 189, 0, 255);
        public static readonly Color Legendary = new Color32(215, 0, 2, 255);

        public static Color GetColor(EItemLevel level)
        {
            switch (level)
            {
                case EItemLevel.COMMOM: return Commom;
                case EItemLevel.UNCOMMOM: return Uncommon;
                case EItemLevel.RARE: return Rare;
                case EItemLevel.EPIC: return Epic;
                case EItemLevel.GOLD: return Gold;
                case EItemLevel.LEGENDARY: return Legendary;
            }

            return Commom;
        }
    }

    /// <summary>
    /// 如果为null则会返回false，您只需要一个检查即可。
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>false：不为null，true：null</returns>
    public static bool CheckNull(System.Object obj)
    {
        if (obj == null)
        {
            Log.NullPtr("");
            return true;
        }

        if (obj is string s && string.IsNullOrEmpty(s))
        {
            Log.Error("传入了空字符串！");
            return true;
        }
        
        return false;
    }

    public static bool GenBoolByPercent(float truePercent)
    {
        float randomValue = UnityEngine.Random.Range(0f, 1f);
        return randomValue < truePercent;
    }
}

public static class RandomExtension
{
    /// <summary>
    /// 返回数组中的一个随机元素
    /// </summary>
    public static T RandomElement<T>(this T[] array)
    {
        if (array == null || array.Length == 0)
        {
            Log.Warn("数组为空，无法获取随机元素！");
            return default(T);
        }

        int index = UnityEngine.Random.Range(0, array.Length);
        return array[index];
    }

    public static T RandomElement<T>(this IList<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));
        if (list.Count == 0)
            throw new ArgumentException("集合元素为空。不能获得随机元素！.", nameof(list));

        int index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }
}

// 拓展Transform，使其能够深度遍历
public static class TransformSearchExtension
{
    /// <summary>
    /// 深度优先搜索，根据名称查找子物体（包括未激活的）
    /// </summary>
    /// <param name="root">起始根节点（Transform）</param>
    /// <param name="targetName">要匹配的游戏对象名称</param>
    /// <param name="maxDepth">最大遍历深度（根节点为0，传入0则只检查根节点自身）</param>
    /// <returns>找到的第一个GameObject，未找到则返回null</returns>
    public static GameObject FindChildByNameDFS(this Transform root, string targetName, int maxDepth = int.MaxValue)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        Stack<(Transform node, int depth)> stack = new Stack<(Transform, int)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();

            if (depth > maxDepth) continue;
            if (current.name == targetName)
            {
                return current.gameObject;
            }

            for (int i = current.childCount - 1; i >= 0; i--)
            {
                Transform child = current.GetChild(i);
                stack.Push((child, depth + 1));
            }
        }

        return null;
    }

    // 重载
    public static GameObject FindChildByNameDFS(this GameObject root, string targetName, int maxDepth = int.MaxValue)
    {
        if (root == null) return null;
        return root.transform.FindChildByNameDFS(targetName, maxDepth);
    }
}