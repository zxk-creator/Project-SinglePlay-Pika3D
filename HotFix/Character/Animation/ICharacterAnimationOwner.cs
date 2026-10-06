using UnityEngine;

/// <summary>
/// CharacterAnimation 真正依赖的那几个成员。
/// 提出接口是为了让"不是 CharacterBase 的角色"（比如纯显示用的 NetPlayer）
/// 也能带 CharacterAnimation，而不用去继承整套物理和移动逻辑。
///
/// 骨架（Animator）不在接口里：CharacterAnimation 自己在 Inspector 上赋值，
/// 不再通过宿主去找，所以谁带着它都行。
/// </summary>
public interface ICharacterAnimationOwner
{
    /// <summary>性别：决定用哪套动画控制器、待机小动作池、衣服动画目录</summary>
    bool isGirl { get; set; }

    /// <summary>当前移动/动作状态，决定播哪个动画。由宿主按自己的规则算好推过来（见 CharacterBase.UpdateMoveState）</summary>
    EAnimationState moveState { get; set; }
}
