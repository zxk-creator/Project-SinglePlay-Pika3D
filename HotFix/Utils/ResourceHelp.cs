using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PK;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ResourceHelp
{
    public static AudioClip LoadAudio(string relativePath)
    {
        var res = Resources.Load<AudioClip>(relativePath);
        if (res == null)
        {
            Log.NoSuchFileOrPath(relativePath);
            return null;
        }

        return res;
    }

    public static AudioClip LoadAudioNew(string name)
    {
        var sH = Addressables.LoadAssetAsync<AudioClip>(name);
        sH.WaitForCompletion();

        return sH.Result;
    }

    /// <summary>
    /// 按 Addressables 地址加载 prefab 并实例化，返回实例。
    /// 调用方拿到直接就能改 Transform / GetComponent，不需要自己再 Instantiate。
    /// 加载失败返回 null（调用方必须判空）。
    /// </summary>
    public static GameObject LoadPrefab(string name)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(name);
        handle.WaitForCompletion();

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Log.NoSuchFileOrPath(name);
            Addressables.Release(handle);
            return null;
        }

        var go = Object.Instantiate(handle.Result);

        // 实例已经独立存在，这里释放句柄即可
        Addressables.Release(handle);

        return go;
    }

    public static string LoadTextAsset(string name)
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(name);
        handle.WaitForCompletion();

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Log.NoSuchFileOrPath(name);
            Addressables.Release(handle);
            return null;
        }

        string text = handle.Result.text;

        Addressables.Release(handle);

        return text;
    }
}

