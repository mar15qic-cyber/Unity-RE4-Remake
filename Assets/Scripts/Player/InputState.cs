using UnityEngine;

/// <summary>
/// LeonController 持有的玩家输入与运动查询值。LeonInputBridge 通过控制器入口提交移动、观察和按键意图，
/// 控制器同时写入蹲伏与地面检测结果；运动、镜头等消费者经控制器或只读属性查询，不另建输入来源。
/// </summary>
[System.Serializable]
public struct InputState
{
    /// <summary>中文含义：移动输入。输入桥提交的局部移动向量，玩家控制器结合镜头或自由环顾参考解算世界方向。</summary>
    public Vector3 moveInput;
    /// <summary>中文含义：观察输入。输入桥提交的观察增量，由摄像机链使用。</summary>
    public Vector2 lookInput;
    /// <summary>中文含义：是否正在冲刺。经控制器约束后的冲刺意图，进入蹲伏时会被清除。</summary>
    public bool isSprinting;
    /// <summary>中文含义：是否自由观察中。有效自由环顾绑定的持有意图，控制器将它与玩法许可传给 FreeLookState。</summary>
    public bool isFreeLooking;
    /// <summary>中文含义：是否跳跃中。保留的跳跃查询兼容标志，不在此数据结构中判定或启动跳跃动作。</summary>
    public bool isJumping;
    /// <summary>中文含义：是否正在蹲伏。控制器已接受的蹲伏状态，站起资格与碰撞尺寸调整由控制器负责。</summary>
    public bool isCrouching;
    /// <summary>中文含义：是否着地。控制器 CheckGrounded 最近射线检测的着地结果，不由输入系统直接决定。</summary>
    public bool isGrounded;
    /// <summary>中文含义：移动输入。消费者只读访问当前局部移动向量。</summary>
    public readonly Vector3 MoveInput => moveInput;
    /// <summary>中文含义：观察输入。消费者只读访问当前观察增量。</summary>
    public readonly Vector2 LookInput => lookInput;
    /// <summary>中文含义：是否正在冲刺。消费者只读访问控制器约束后的冲刺意图。</summary>
    public readonly bool IsSprinting => isSprinting;
    /// <summary>中文含义：是否自由观察中。消费者只读访问自由环顾输入持有意图，实际观察阶段还需读取 FreeLookState。</summary>
    public readonly bool IsFreeLooking => isFreeLooking;
    /// <summary>中文含义：是否跳跃中。消费者只读访问既有跳跃兼容标志。</summary>
    public readonly bool IsJumping => isJumping;
    /// <summary>中文含义：是否正在蹲伏。消费者只读访问控制器已提交的蹲伏状态。</summary>
    public readonly bool IsCrouching => isCrouching;
    /// <summary>中文含义：是否着地。消费者只读访问最近着地检测结果。</summary>
    public readonly bool IsGrounded => isGrounded;
}
