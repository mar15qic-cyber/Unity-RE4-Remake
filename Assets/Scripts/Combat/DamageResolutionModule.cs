using UnityEngine;
using GameArchitecture;

/// <summary>部位倍率规则和伤害提交入口。不拥有生命、动画或特效状态。</summary>
/// <remarks>部位倍率和伤害提交边界，不拥有生命、动画或特效；基础伤害、倍率结算值和实际扣血分别保留各自语义。</remarks>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "伤害结算模块")]
public static class DamageResolutionModule
{
    /// <summary>CommitNormalHit 的业务处理入口；本方法直接依赖 IDamageable.TakeDamage(float, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.ProcessHit(RaycastHit, float)；下游（含内部回调体） IDamageable.TakeDamage(float, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标索引可受伤对象。使用位置：DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3)。</param>
    /// <param name="damage">中文含义：伤害。使用位置：作为 IDamageable.TakeDamage(float, Vector3) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 IDamageable.TakeDamage(float, Vector3) 的参数。</param>
    public static void CommitNormalHit(IDamageable target, float damage, Vector3 position)
    {
        // receiverValue1（判空前缓存的目标索引可受伤对象）：从目标读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3)。
global::IDamageable receiverValue1 = target;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.TakeDamage(damage, position);
        }
    }

    /// <summary>CommitPairedActionHeavyStrike 的业务处理入口；本方法直接依赖 VillagerController.TakeHeavyFrontDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CommitAssassinationResult(int, float)；下游（含内部回调体） VillagerController.TakeHeavyFrontDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标村民敌人控制器。使用位置：DamageResolutionModule.CommitPairedActionHeavyStrike(VillagerController, float, Vector3)。</param>
    /// <param name="damage">中文含义：伤害。使用位置：作为 VillagerController.TakeHeavyFrontDamage(float, Vector3) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 VillagerController.TakeHeavyFrontDamage(float, Vector3) 的参数。</param>
    public static void CommitPairedActionHeavyStrike(VillagerController target, float damage, Vector3 position)
    {
        if (target != null)
        {
            target.TakeHeavyFrontDamage(damage, position);
        }
    }

    /// <summary>Commit 的业务处理入口；本方法直接依赖 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)、DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3)；下游（含内部回调体） DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)、DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="request">中文含义：请求。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool) 的参数、DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3)。</param>
    /// <param name="target">中文含义：目标村民敌人控制器。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool) 的参数、作为 Mathf.Max(float, float) 的参数 等共 4 处。</param>
    /// <param name="bodyPart">中文含义：身体部位。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数。</param>
    /// <param name="normal">中文含义：表面法线。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数。</param>
    public static DamageResult Commit(DamageRequest request, VillagerController target, HitboxType bodyPart, Vector3 normal, bool impactSoundHandled = false,
        ChapterOneEffect bloodEffect = ChapterOneEffect.None)
    {
        FirearmBodyTechniqueShot shot = default;
        return Commit(request, target, bodyPart, normal, impactSoundHandled, false, ref shot, bloodEffect);
    }

    internal static DamageResult CommitFirearm(DamageRequest request, VillagerController target, HitboxType bodyPart,
        Vector3 normal, bool impactSoundHandled, ref FirearmBodyTechniqueShot shot, ChapterOneEffect bloodEffect, bool deferBloodPresentation = false, Vector3 bloodDirection = default)
    {
        return Commit(request, target, bodyPart, normal, impactSoundHandled, true, ref shot, bloodEffect, deferBloodPresentation, bloodDirection);
    }

    private static DamageResult Commit(DamageRequest request, VillagerController target, HitboxType bodyPart,
        Vector3 normal, bool impactSoundHandled, bool firearm, ref FirearmBodyTechniqueShot shot, ChapterOneEffect bloodEffect, bool deferBloodPresentation = false, Vector3 bloodDirection = default)
    {
        if (!request.Context.Valid || !request.AttackAction.Valid || request.HitSequence < 0)
        {
            return new DamageResult(request, false, RejectionReason.ParameterInvalid, 0, false);
        }

        if (target == null || target.actionCoordinator == null || request.Context.Target != target.actionCoordinator.Identity)
        {
            return new DamageResult(request, false, RejectionReason.ObjectHasInvalidate, 0, false);
        }

        var enemyGroup = EnemyAIManager.Instance;
        if (enemyGroup != null && enemyGroup.IsBellWithdrawal)
        {
            return new DamageResult(request, false, RejectionReason.PermissionBeingOccupancy, 0, false);
        }

        // originalHealth（原始生命）：使用位置：作为 DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool) 的参数、作为 Mathf.Max(float, float) 的参数。
        float originalHealth = target.currentHealth;
        bool deferReaction = firearm && target.combatModule != null && target.combatModule.UsesNativeBodyTechnique;
        if (deferReaction)
        {
            if (shot.action.Valid && (shot.action != request.AttackAction || shot.target != request.Context.Target))
            {
                return new DamageResult(request, false, RejectionReason.ParameterInvalid, 0, false);
            }

            shot.action = request.AttackAction;
            shot.target = request.Context.Target;
        }

        CommitEnemyHit(target, bodyPart, request.BaseDamage, request.HitPosition, normal, impactSoundHandled,
            deferReaction, ref shot, bloodEffect, deferBloodPresentation, bloodDirection);
        if (firearm && originalHealth > 0f && target.IsDead)
        {
            VillageLeonDialogue.ReportGunKill(target);
        }
        return new DamageResult(request, true, RejectionReason.None, Mathf.Max(0, originalHealth - target.currentHealth), originalHealth > 0 && target.currentHealth <= 0);
    }

    /// <summary>QueryBodyPartMultiplier 的业务处理入口；本方法直接依赖 BodyPartDamageRules.QueryBodyPartMultiplier(HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)、EnemyHitbox.GetDamageMultiplier(HitboxType)；下游（含内部回调体） BodyPartDamageRules.QueryBodyPartMultiplier(HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bodyPart">中文含义：身体部位。使用位置：作为 BodyPartDamageRules.QueryBodyPartMultiplier(HitboxType) 的参数。</param>
    public static float QueryBodyPartMultiplier(HitboxType bodyPart) => BodyPartDamageRules.QueryBodyPartMultiplier(bodyPart);
    /// <summary>CommitEnemyHit 的业务处理入口；本方法直接依赖 CombatEventDispatcher.PublishHit(HitResult)、DamageResolutionModule.QueryBodyPartMultiplier(HitboxType)、Debug.Log(object, Object)、HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)、VillagerController.TakeDamage(float, Vector3, HitboxType, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3)、EnemyHitbox.ApplyResolvedHit(VillagerController, HitboxType, float, Vector3, Vector3)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；下游（含内部回调体） CombatEventDispatcher.PublishHit(HitResult)、DamageResolutionModule.QueryBodyPartMultiplier(HitboxType)、Debug.Log(object, Object)、HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)、VillagerController.TakeDamage(float, Vector3, HitboxType, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标村民敌人控制器。使用位置：作为 Debug.Log(object, Object) 的参数、作为 HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool) 的参数、DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)。</param>
    /// <param name="bodyPart">中文含义：身体部位。使用位置：作为 DamageResolutionModule.QueryBodyPartMultiplier(HitboxType) 的参数、作为 Debug.Log(object, Object) 的参数、作为 HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool) 的参数 等共 4 处。</param>
    /// <param name="baseDamage">中文含义：基础伤害。使用位置：作为 Debug.Log(object, Object) 的参数、DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool) 的参数、作为 VillagerController.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    /// <param name="normal">中文含义：表面法线。使用位置：作为 HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool) 的参数、作为 VillagerController.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    public static void CommitEnemyHit(VillagerController target, HitboxType bodyPart, float baseDamage, Vector3 position, Vector3 normal, bool impactSoundHandled = false,
        ChapterOneEffect bloodEffect = ChapterOneEffect.None)
    {
        FirearmBodyTechniqueShot shot = default;
        CommitEnemyHit(target, bodyPart, baseDamage, position, normal, impactSoundHandled, false, ref shot, bloodEffect);
    }

    private static void CommitEnemyHit(VillagerController target, HitboxType bodyPart, float baseDamage, Vector3 position,
        Vector3 normal, bool impactSoundHandled, bool deferReaction, ref FirearmBodyTechniqueShot shot, ChapterOneEffect bloodEffect, bool deferBloodPresentation = false, Vector3 bloodDirection = default)
    {
        if (target == null || target.IsDead || baseDamage <= 0f)
        {
            return;
        }

        float originalHealth = target.currentHealth;
        float multiplier = QueryBodyPartMultiplier(bodyPart);
        bool collapse = false;
        if (deferReaction)
        {
            target.combatModule.TakeNativeFirearmDamage(baseDamage * multiplier, position, bodyPart, normal, out collapse);
        }
        else
        {
            target.TakeDamage(baseDamage * multiplier, position, bodyPart, normal);
        }
        float appliedDamage = Mathf.Max(0f, originalHealth - target.currentHealth);
        if (appliedDamage <= 0f)
        {
            return;
        }

        if (deferReaction)
        {
            shot.Add(appliedDamage, position, normal, bodyPart, collapse);
        }

        Debug.Log($"敌人命中：部位={bodyPart}，基础伤害={baseDamage:F1}，倍率={multiplier:F1}，最终伤害={appliedDamage:F1}", target);
        var hit = new HitResult(target.gameObject, position, normal, bodyPart, appliedDamage, target.currentHealth <= 0, bodyPart == HitboxType.Head, impactSoundHandled, bloodEffect, deferBloodPresentation);
        if (bloodDirection.sqrMagnitude > .0001f)
        {
            hit.bloodDirection = bloodDirection;
        }
        CombatEventDispatcher.PublishHit(hit);
        VillageLeonDialogue.ReportEnemyHealth(target, originalHealth, target.currentHealth);
    }
}
