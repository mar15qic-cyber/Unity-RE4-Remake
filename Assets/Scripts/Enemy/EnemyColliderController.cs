using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyColliderController，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(BoxCollider))]
public class EnemyColliderController : MonoBehaviour
{
    /// <summary>中文含义：中心。只读字段，初始化后不由普通写入口替换。使用位置：EnemyColliderController.SetStanding()。</summary>
    private static readonly Vector3 STAND_CENTER = new Vector3(0f, 0.9f, 0f);
    /// <summary>中文含义：高度。编译期常量，保持既有值与消费者映射。使用位置：EnemyColliderController.SetStanding()。</summary>
    private const float STAND_HEIGHT = 1.8f;
    /// <summary>中文含义：半径。编译期常量，保持既有值与消费者映射。使用位置：EnemyColliderController.GetHorizontalReach(Vector3)、EnemyColliderController.GetMaximumHorizontalReach()、EnemyColliderController.SetStanding()。</summary>
    private const float STAND_RADIUS = 0.35f;
    /// <summary>中文含义：方向。编译期常量，保持既有值与消费者映射。使用位置：EnemyColliderController.SetStanding()。</summary>
    private const int STAND_DIR = 1;
    /// <summary>中文含义：中心。只读字段，初始化后不由普通写入口替换。使用位置：EnemyColliderController.SetCrawl()。</summary>
    private static readonly Vector3 CRAWL_CENTER = new Vector3(0f, 0.2f, 0f);
    /// <summary>中文含义：尺寸。只读字段，初始化后不由普通写入口替换。使用位置：EnemyColliderController.SetCrawl()。</summary>
    private static readonly Vector3 CRAWL_SIZE = new Vector3(0.7f, 0.4f, 1.6f);
    /// <summary>中文含义：盖顶列。使用位置：EnemyColliderController.EnsureColliderCache()、EnemyColliderController.GetHorizontalReach(Vector3)、EnemyColliderController.GetMaximumHorizontalReach() 等共 6 处。</summary>
    private CapsuleCollider capCol;
    /// <summary>中文含义：箱体列。使用位置：EnemyColliderController.EnsureColliderCache()、EnemyColliderController.GetHorizontalReach(Vector3)、EnemyColliderController.GetMaximumHorizontalReach() 等共 6 处。</summary>
    private BoxCollider boxCol;
    /// <summary>敌人行为、动作与通行使用的 ColliderState 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    public enum ColliderState
    {
        /// <summary>中文含义：站立。</summary>
        Standing,
        /// <summary>中文含义：爬行。</summary>
        Crawl,
        /// <summary>中文含义：停用。</summary>
        Disabled
    }

    /// <summary>中文含义：当前状态。使用位置：EnemyColliderController.GetHorizontalReach(Vector3)、EnemyColliderController.GetMaximumHorizontalReach()、EnemyColliderController.SetCrawl() 等共 5 处。</summary>
    public ColliderState CurrentState { get; private set; } = ColliderState.Standing;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()、EnemyColliderController.SetStanding()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        EnsureColliderCache();
        SetStanding();
    }

    /// <summary>SetStanding 的业务处理入口；本方法直接依赖 EnemyColliderController.EnsureColliderCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyColliderController.Start()、EnemyCombatModule.BeginCrawl()、EnemyCombatModule.CompleteStandUp()、EnemyCombatModule.HandleOnColliderCrawl()、EnemyCombatModule.HandleOnColliderDisable() 等共 6 处；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SetStanding()
    {
        EnsureColliderCache();
        if (capCol != null)
        {
            capCol.enabled = true;
            capCol.center = STAND_CENTER;
            capCol.height = STAND_HEIGHT;
            capCol.radius = STAND_RADIUS;
            capCol.direction = STAND_DIR;
        }

        if (boxCol != null)
        {
            boxCol.enabled = false;
        }

        CurrentState = ColliderState.Standing;
    }

    /// <summary>SetCrawl 的业务处理入口；本方法直接依赖 EnemyColliderController.EnsureColliderCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginCrawl()、EnemyCombatModule.BeginCrawlAttack()、EnemyCombatModule.CancelStandUpAndResumeCrawl()、EnemyCombatModule.ForceCrawlPoseAfterQTE()、EnemyCombatModule.HandleOnColliderCrawl() 等共 7 处；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SetCrawl()
    {
        EnsureColliderCache();
        if (capCol != null)
        {
            capCol.enabled = false;
        }

        if (boxCol != null)
        {
            boxCol.enabled = true;
            boxCol.center = CRAWL_CENTER;
            boxCol.size = CRAWL_SIZE;
        }

        CurrentState = ColliderState.Crawl;
    }

    /// <summary>SetDisabled 的业务处理入口；本方法直接依赖 EnemyColliderController.EnsureColliderCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()、EnemyCombatModule.BeginCrawl()、EnemyCombatModule.HandleOnColliderCrawl()、EnemyCombatModule.HandleOnColliderDisable()、EnemyCombatModule.HandleOnColliderStand()；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SetDisabled()
    {
        EnsureColliderCache();
        if (capCol != null)
        {
            capCol.enabled = false;
        }

        if (boxCol != null)
        {
            boxCol.enabled = false;
        }

        CurrentState = ColliderState.Disabled;
    }

    /// <summary>GetHorizontalReach 的业务处理入口；本方法直接依赖 EnemyColliderController.EnsureColliderCache()、EnemyColliderController.GetMaximumHorizontalReach()、Mathf.Abs(float)、Transform.InverseTransformDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsPlayerWithinBodyContactRange()；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()、EnemyColliderController.GetMaximumHorizontalReach()、Mathf.Abs(float)、Transform.InverseTransformDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="worldDirection">中文含义：世界空间方向。使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、EnemyColliderController.GetHorizontalReach(Vector3)。</param>
    public float GetHorizontalReach(Vector3 worldDirection)
    {
        EnsureColliderCache();
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return GetMaximumHorizontalReach();
        }

        if (CurrentState == ColliderState.Crawl && boxCol != null)
        {
            // localDirection（局部方向）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
            // halfSize（一半尺寸）：使用位置：EnemyColliderController.GetHorizontalReach(Vector3)。
Vector3 halfSize = boxCol.size * 0.5f;
            return Mathf.Abs(localDirection.x) * halfSize.x + Mathf.Abs(localDirection.z) * halfSize.z;
        }

        if (capCol != null)
        {
            return capCol.radius;
        }
        else
        {
            return STAND_RADIUS;
        }
    }

    /// <summary>GetMaximumHorizontalReach 的业务处理入口；本方法直接依赖 EnemyColliderController.EnsureColliderCache()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyColliderController.GetHorizontalReach(Vector3)、VillagerController.OnDrawGizmosSelected()；下游（含内部回调体） EnemyColliderController.EnsureColliderCache()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetMaximumHorizontalReach()
    {
        EnsureColliderCache();
        if (CurrentState == ColliderState.Crawl && boxCol != null)
        {
            // halfSize（一半尺寸）：使用位置：作为 Mathf.Max(float, float) 的参数。
Vector3 halfSize = boxCol.size * 0.5f;
            return Mathf.Max(halfSize.x, halfSize.z);
        }

        if (capCol != null)
        {
            return capCol.radius;
        }
        else
        {
            return STAND_RADIUS;
        }
    }

    /// <summary>EnsureColliderCache 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;BoxCollider&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyColliderController.GetHorizontalReach(Vector3)、EnemyColliderController.GetMaximumHorizontalReach()、EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding() 等共 6 处；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureColliderCache()
    {
        if (capCol == null)
        {
            capCol = GetComponent<CapsuleCollider>();
        }

        if (boxCol == null)
        {
            boxCol = GetComponent<BoxCollider>();
        }
    }
}
