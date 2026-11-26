using UnityEngine;

/// <summary>
/// 挂在骨骼上的 Trigger Collider，负责受击检测。
/// 子弹 Raycast 命中后通过 OnHit 转发给 VillagerController。
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyHitbox : MonoBehaviour
{
    /// <summary>命中盒类型。</summary>
    [Header("部位设置")]
    public HitboxType hitboxType = HitboxType.Chest;
    /// <summary>拥有者。</summary>
    private VillagerController owner;
    /// <summary>拥有者。</summary>
    public VillagerController Owner
    {
        get
        {
            if (owner == null)
            {
                owner = GetComponentInParent<VillagerController>();
            }

            return owner;
        }
    }

    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Collider&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        owner = GetComponentInParent<VillagerController>();
        // 确保 Collider 是 Trigger
        // 列。
var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 被子弹命中时调用。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyHitbox.ApplyResolvedHit(VillagerController, HitboxType, float, Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseDamage">基础伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitNormal">命中表面法线。</param>
    public void OnHit(float baseDamage, Vector3 hitPoint, Vector3 hitNormal)
    {
        ApplyResolvedHit(Owner, hitboxType, baseDamage, hitPoint, hitNormal);
    }

    /// <summary>ApplyResolvedHit 的业务处理入口；本方法直接依赖 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyHitbox.OnHit(float, Vector3, Vector3)；下游（含内部回调体） DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标村民敌人控制器。</param>
    /// <param name="resolvedType">解算后的类型。</param>
    /// <param name="baseDamage">基础伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitNormal">命中表面法线。</param>
    public static void ApplyResolvedHit(VillagerController target, HitboxType resolvedType, float baseDamage, Vector3 hitPoint, Vector3 hitNormal)
    {
        DamageResolutionModule.CommitEnemyHit(target, resolvedType, baseDamage, hitPoint, hitNormal);
    }

    /// <summary>
    /// 根据部位返回伤害倍率。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyHitbox.GetDamageMultiplier(HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetDamageMultiplier()
    {
        return GetDamageMultiplier(hitboxType);
    }

    /// <summary>GetDamageMultiplier 的业务处理入口；本方法直接依赖 DamageResolutionModule.QueryBodyPartMultiplier(HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyHitbox.GetDamageMultiplier()；下游（含内部回调体） DamageResolutionModule.QueryBodyPartMultiplier(HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中部位类别。</param>
    public static float GetDamageMultiplier(HitboxType type)
    {
        return DamageResolutionModule.QueryBodyPartMultiplier(type);
    }

    /// <summary>
    /// 将 HitboxType 转换为 Animator HitDir 参数。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） EnemyHitbox.ResolveSide(Vector3, Transform, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="enemyRoot">敌人根节点。</param>
    public static int ToHitDir(HitboxType type, Vector3 hitPoint, Transform enemyRoot)
    {
        switch (type)
        {
            case HitboxType.Head:
                return 0;
            case HitboxType.LeftArm:
                return 1;
            case HitboxType.RightArm:
                return 2;
            case HitboxType.LeftLeg:
                return 3;
            case HitboxType.RightLeg:
                return 4;
            case HitboxType.Chest:
                return ResolveSide(hitPoint, enemyRoot, 1, 2);
            case HitboxType.Pelvis:
                return ResolveSide(hitPoint, enemyRoot, 3, 4);
            default:
                return 0;
        }
    }

    /// <summary>ResolveSide 的业务处理入口；本方法直接依赖 Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyHitbox.ToHitDir(HitboxType, Vector3, Transform)；下游（含内部回调体） Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="enemyRoot">敌人根节点。</param>
    /// <param name="leftResult">左侧结果。</param>
    /// <param name="rightResult">右侧结果。</param>
    private static int ResolveSide(Vector3 hitPoint, Transform enemyRoot, int leftResult, int rightResult)
    {
        if (enemyRoot == null || hitPoint == default)
        {
            if (Random.value < 0.5f)
            {
                return leftResult;
            }
            else
            {
                return rightResult;
            }
        }

        // 局部横向。
float localX = enemyRoot.InverseTransformPoint(hitPoint).x;
        if (localX <= 0f)
        {
            return leftResult;
        }
        else
        {
            return rightResult;
        }
    }
}
