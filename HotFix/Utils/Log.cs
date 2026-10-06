using System;
using UnityEngine;

namespace PK
{
    public static class Log
{
    public static void NoSuchComponent(String componentName)
    {
        Debug.LogError("尚未添加" + componentName + "!");
    }

    public static void NotAssignedYet(String name)
    {
        Debug.LogError("尚未赋值" + name + "!");
    }

    public static void NoSuchFileOrPath(String nameOrPath)
    {
        Debug.LogError("文件或路径" + nameOrPath + "不存在！");
    }

    public static void NullPtr(String where)
    {
        Debug.LogError("发生空指针异常！你是否忘记了初始化传入参数？在" + where);
    }

    public static void Info(String msg,bool printToStreen = false)
    {
        Debug.Log(msg);
    }

    public static void Warn(String msg, bool printToStreen = false)
    {
        Debug.LogWarning(msg);
    }

    public static void Error(String msg, bool printToStreen = false)
    {
        Debug.LogError(msg);
    }
}
}
