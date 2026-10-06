using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EAnimationState
{
    IDLE,         // 站立
    MOVE,         // 行走
    RUN,          // 跑步
    FLY_IDLE,     // 上升阶段
    FLY_RUN,      // 下落阶段
    SIT,          // 坐
    LIE,          // 躺
    BATH,         // 泡澡
    REVIVE,       // 复活中
    DEAD
}
