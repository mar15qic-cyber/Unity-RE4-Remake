using UnityEngine;

/// <summary>
/// 敌人特殊通行动画的场景级引用库。
/// 动画仅负责姿态，翻窗轨迹和坠落重力均由代码驱动。
/// </summary>
[DisallowMultipleComponent]
public class EnemyTraversalAnimationLibrary : MonoBehaviour
{
    /// <summary>生效。</summary>
    public static EnemyTraversalAnimationLibrary Active { get; private set; }

    /// <summary>窗口攀爬穿过。</summary>
    [Header("翻窗")]
    public AnimationClip windowClimbThrough;
    /// <summary>掉落跳跃起始。</summary>
    [Header("坠楼")]
    public AnimationClip dropJumpStart;
    /// <summary>掉落下落循环。</summary>
    public AnimationClip dropFallLoop;
    /// <summary>掉落落地。</summary>
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
