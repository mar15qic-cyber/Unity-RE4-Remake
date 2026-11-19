using UnityEngine;

/// <summary>
/// 放在关键位置的教学触发器，用于首次进入时显示举枪射击提示。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialTrigger : MonoBehaviour
{
    /// <summary>中文含义：玩家标签。使用位置：作为 Component.CompareTag(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数。</summary>
    [Header("触发设置")]
    public string playerTag = "Player";
    /// <summary>中文含义：是否禁用之后已触发。使用位置：TutorialTrigger.OnTriggerEnter(Collider)。</summary>
    public bool disableAfterTriggered = true;
    /// <summary>中文含义：是否需要玩家退出之前触发器。使用位置：TutorialTrigger.ArmStartupProtection()。</summary>
    public bool requirePlayerExitBeforeTrigger;
    /// <summary>中文含义：激活延迟秒。使用位置：TutorialTrigger.ArmStartupProtection()。</summary>
    [Min(0f)]
    public float activationDelaySeconds;
    /// <summary>中文含义：是否已触发当前会话。使用位置：TutorialTrigger.OnTriggerEnter(Collider)。</summary>
    private bool triggeredThisSession;
    /// <summary>中文含义：是否等待玩家退出。使用位置：TutorialTrigger.ArmStartupProtection()、TutorialTrigger.OnTriggerEnter(Collider)、TutorialTrigger.OnTriggerExit(Collider)。</summary>
    private bool waitingForPlayerExit;
    /// <summary>中文含义：就绪时间。使用位置：TutorialTrigger.ArmStartupProtection()、TutorialTrigger.OnTriggerEnter(Collider)。</summary>
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
        // boxCollider（盒形碰撞体）：保存 Component.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：作为 TutorialTrigger.IsPlayerInside(BoxCollider) 的参数、TutorialTrigger.ArmStartupProtection()。
BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        readyTime = Time.time + activationDelaySeconds;
        waitingForPlayerExit = requirePlayerExitBeforeTrigger && IsPlayerInside(boxCollider);
    }

    /// <summary>Unity 触发区域进入回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.TryShowAimShootTutorial()、TutorialTrigger.IsPlayer(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">中文含义：另一个碰撞体。使用位置：作为 TutorialTrigger.IsPlayer(Collider) 的参数。</param>
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

        // manager（当前教学管理器）：使用位置：TutorialTrigger.OnTriggerEnter(Collider)。
TutorialManager manager = TutorialManager.Instance;
        if (manager == null)
        {
            return;
        }

        // shown（是否已显示）：保存 TutorialManager.TryShowAimShootTutorial() 的返回结果，供当前步骤使用。使用位置：TutorialTrigger.OnTriggerEnter(Collider)。
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
    /// <param name="other">中文含义：另一个碰撞体。使用位置：作为 TutorialTrigger.IsPlayer(Collider) 的参数。</param>
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
    /// <param name="other">中文含义：另一个碰撞体。使用位置：TutorialTrigger.IsPlayer(Collider)。</param>
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
    /// <param name="boxCollider">中文含义：盒形碰撞体。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、作为 Vector3.Scale(Vector3, Vector3) 的参数、TutorialTrigger.IsPlayerInside(BoxCollider)。</param>
    private bool IsPlayerInside(BoxCollider boxCollider)
    {
        if (boxCollider == null)
        {
            return false;
        }

        // center（中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction) 的参数。
Vector3 center = transform.TransformPoint(boxCollider.center);
        // halfExtents（一半半尺寸）：使用位置：作为 Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction) 的参数。
Vector3 halfExtents = Vector3.Scale(boxCollider.size, transform.lossyScale) * 0.5f;
        // hits（命中结果）：保存 Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：作为 TutorialTrigger.IsPlayer(Collider) 的参数、TutorialTrigger.IsPlayerInside(BoxCollider)。
Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        // i（当前遍历索引）：使用位置：TutorialTrigger.IsPlayerInside(BoxCollider)。
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
