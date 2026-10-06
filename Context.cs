using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace GS;

// 各种单例的存储容器
public static class Context
{
    public static UIManager um;
    public static Canvas uiCanvas;
    public static EventSystem uiEventSystem;
}

