using UnityEngine;

/// <summary>
/// 放在关键位置的教学触发器，用于首次进入时显示举枪射击提示。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialTrigger : MonoBehaviour
{
    /// <summary>玩家标签。</summary>
    [Header("触发设置")]
    public string playerTag = "Player";
    /// <summary>是否禁用之后已触发。</summary>
    public bool disableAfterTriggered = true;
    /// <summary>是否需要玩家退出之前触发器。</summary>
    public bool requirePlayerExitBeforeTrigger;
    /// <summary>激活延迟秒。</summary>
    [Min(0f)]
    public float activationDelaySeconds;
    /// <summary>是否已触发当前会话。</summary>
    private bool triggeredThisSession;
    /// <summary>是否等待玩家退出。</summary>
    private bool waitingForPlayerExit;
    /// <summary>就绪时间。</summary>
    private float readyTime;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialTrigger.ArmStartupProtection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        ArmStartupProtection();
    }

    /// <summary>ArmStartupProtection 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;BoxCollider&gt;()、TutorialTrigger.IsPlayerInside(BoxCollider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialTrigger.Start()；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()、TutorialTrigger.IsPlayerInside(BoxCollider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ArmStartupProtection()
    {
        // 盒形碰撞体。
BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        readyTime = Time.time + activationDelaySeconds;
        waitingForPlayerExit = requirePlayerExitBeforeTrigger && IsPlayerInside(boxCollider);
    }

    /// <summary>Unity 触发区域进入回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.TryShowAimShootTutorial()、TutorialTrigger.IsPlayer(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">另一个碰撞体。</param>
    void OnTriggerEnter(Collider other)
    {
        if (triggeredThisSession)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        if (Time.time < readyTime)
        {
            waitingForPlayerExit = true;
            return;
        }

        if (waitingForPlayerExit)
        {
            return;
        }

        // 当前教学管理器。
TutorialManager manager = TutorialManager.Instance;
        if (manager == null)
        {
            return;
        }

        // 是否已显示。
bool shown = manager.TryShowAimShootTutorial();
        if (!shown)
        {
            return;
        }

        triggeredThisSession = true;
        if (disableAfterTriggered)
        {
            enabled = false;
        }
    }

    /// <summary>Unity 触发区域退出回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialTrigger.IsPlayer(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">另一个碰撞体。</param>
    void OnTriggerExit(Collider other)
    {
        if (!waitingForPlayerExit)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        waitingForPlayerExit = false;
    }

    /// <summary>IsPlayer 的业务处理入口；本方法直接依赖 Component.CompareTag(string)、Component.GetComponentInParent&lt;LeonController&gt;()、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialTrigger.IsPlayerInside(BoxCollider)、TutorialTrigger.OnTriggerEnter(Collider)、TutorialTrigger.OnTriggerExit(Collider)；下游（含内部回调体） Component.CompareTag(string)、Component.GetComponentInParent&lt;LeonController&gt;()、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">另一个碰撞体。</param>
    private bool IsPlayer(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<LeonController>() != null;
    }

    /// <summary>IsPlayerInside 的业务处理入口；本方法直接依赖 Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction)、Transform.TransformPoint(Vector3)、TutorialTrigger.IsPlayer(Collider)、Vector3.Scale(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialTrigger.ArmStartupProtection()；下游（含内部回调体） Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction)、Transform.TransformPoint(Vector3)、TutorialTrigger.IsPlayer(Collider)、Vector3.Scale(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="boxCollider">盒形碰撞体。</param>
    private bool IsPlayerInside(BoxCollider boxCollider)
    {
        if (boxCollider == null)
        {
            return false;
        }

        // 中心。
Vector3 center = transform.TransformPoint(boxCollider.center);
        // 一半半尺寸。
Vector3 halfExtents = Vector3.Scale(boxCollider.size, transform.lossyScale) * 0.5f;
        // 命中结果。
Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        // 当前遍历索引。
for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && IsPlayer(hits[i]))
            {
                return true;
            }
        }

        return false;
    }
}
