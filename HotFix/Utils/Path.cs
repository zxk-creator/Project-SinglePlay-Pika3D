using System;
using System.IO;


namespace PKSv;

public static class Path
{
    /// <summary>
    /// Windows: %LocalAppData%\a3f8c1d94e7b2f6a8d0e5c3b1f9a7e4d
    /// Android: /data/data/&lt;包名&gt;/files
    /// 返回时目录保证存在。
    /// </summary>
    public static string GetSavePath()
    {
        string dir;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity"))
        using (var filesDir = activity.Call<UnityEngine.AndroidJavaObject>("getFilesDir"))
        {
            dir = filesDir.Call<string>("getAbsolutePath");
        }
#else
        dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "a3f8c1d94e7b2f6a8d0e5c3b1f9a7e4d");
#endif

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return dir;
    }

    public static string GetSavePath(string fileName)
        => System.IO.Path.Combine(GetSavePath(), fileName);

    /// <summary>写入存档（覆盖）。</summary>
    public static void WriteSave(string fileName, byte[] data)
    {
        System.IO.File.WriteAllBytes(GetSavePath(fileName), data);
    }

    /// <summary>读取存档；不存在返回 null。</summary>
    public static byte[] ReadSave(string fileName)
    {
        string full = GetSavePath(fileName);
        if (!System.IO.File.Exists(full)) return null;
        return System.IO.File.ReadAllBytes(full);
    }

    /// <summary>存档是否存在。</summary>
    public static bool SaveExists(string fileName)
        => System.IO.File.Exists(GetSavePath(fileName));

    /// <summary>删除存档。</summary>
    public static void DeleteSave(string fileName)
    {
        string full = GetSavePath(fileName);
        if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
    }

    public static bool FileExists(string fileName)
        => System.IO.File.Exists(GetSavePath(fileName));
}