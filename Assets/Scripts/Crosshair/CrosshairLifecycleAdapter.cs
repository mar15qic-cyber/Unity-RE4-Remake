using UnityEngine;

/// <summary>
/// 准心生命周期适配器 — 桥接统一界面中枢的事件
/// 实现 ICrosshairLifecycleProvider 接口，挂载在场景中即可
/// </summary>
public class CrosshairLifecycleAdapter : MonoBehaviour, ICrosshairLifecycleProvider
{
    /// <summary>中文含义：是否已暂停。</summary>
    public bool IsPaused => gameUIManager != null && gameUIManager.IsPaused;
    /// <summary>中文含义：是否已进入对局结束状态。</summary>
    public bool IsGameOver => gameUIManager != null && gameUIManager.IsGameOver;
    /// <summary>中文含义：背包界面是否打开。</summary>
    public bool IsInventoryOpen => gameUIManager != null && gameUIManager.IsInventoryOpen;

    /// <summary>生命周期变化时的通知回调。事件通信成员 OnLifecycleChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<CrosshairLifecycleEvent> OnLifecycleChanged;
    /// <summary>未赋值时会在场景中查找。可在检视面板拖入；未赋值时会在场景中查找。</summary>
    [Header("统一界面中枢")]
    [Tooltip("可在检视面板拖入；未赋值时会在场景中查找")]
    public GameUIManager gameUIManager;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (gameUIManager == null)
        {
            gameUIManager = GameUIManager.Instance;
        }

        if (gameUIManager != null)
        {
            gameUIManager.OnLifecycleChanged += ForwardEvent;
        }
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        if (gameUIManager != null)
        {
            gameUIManager.OnLifecycleChanged -= ForwardEvent;
        }
    }

    /// <summary>ForwardEvent 的业务处理入口；本方法直接依赖 Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="evt">当前准星生命周期事件。</param>
    private void ForwardEvent(CrosshairLifecycleEvent evt)
    {
        // 判空前缓存的生命周期变化时的通知回调。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue1 = OnLifecycleChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(evt);
        }
    }
}
