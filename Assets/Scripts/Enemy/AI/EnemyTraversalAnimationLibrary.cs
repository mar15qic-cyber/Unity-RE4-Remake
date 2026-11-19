using UnityEngine;

/// <summary>
/// 敌人特殊通行动画的场景级引用库。
/// 动画仅负责姿态，翻窗轨迹和坠落重力均由代码驱动。
/// </summary>
[DisallowMultipleComponent]
public class EnemyTraversalAnimationLibrary : MonoBehaviour
{
    /// <summary>中文含义：生效。使用位置：EnemyTraversalAnimationLibrary.OnDisable()、EnemyTraversalAnimationLibrary.OnEnable()、EnemyWindowTraversal.BeginDropFallAnimation() 等共 6 处。</summary>
    public static EnemyTraversalAnimationLibrary Active { get; private set; }

    /// <summary>中文含义：窗口攀爬穿过。使用位置：EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)。</summary>
    [Header("翻窗")]
    public AnimationClip windowClimbThrough;
    /// <summary>中文含义：掉落跳跃起始。使用位置：EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)。</summary>
    [Header("坠楼")]
    public AnimationClip dropJumpStart;
    /// <summary>中文含义：掉落下落循环。使用位置：EnemyWindowTraversal.BeginDropFallAnimation()。</summary>
    public AnimationClip dropFallLoop;
    /// <summary>中文含义：掉落落地。使用位置：EnemyWindowTraversal.BeginDropLanding()。</summary>
    public AnimationClip dropLanding;
    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        Active = this;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
    }
}
