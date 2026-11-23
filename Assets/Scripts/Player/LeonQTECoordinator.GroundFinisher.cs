using System;
using RE4.ChapterOne;
using UnityEngine;
using System.Collections.Generic;
using GameArchitecture;

public partial class LeonQTECoordinator
{
    private string groundFinisherMotion;

    internal GridItem ReadGroundPromptKnife()
    {
        if (combat == null || combat.knifeModule == null)
        {
            return null;
        }
        return combat.knifeModule.SelectGroundFinisherKnife();
    }
    private readonly Dictionary<VillagerQTEParticipant, GroundFinisherPlan> groundFinisherPlans = new();

    private sealed class GroundFinisherPlan
    {
        public NativePairPlayback pair;
        public ActionHandle action;
        public string motion;
        public Pose player;
        public Pose enemy;
        public float expiresAt;
    }

    private bool TryReadGroundFinisherPlan(VillagerQTEParticipant target, out GroundFinisherPlan plan,
        bool checkAction = true)
    {
        if (!groundFinisherPlans.TryGetValue(target, out plan))
        {
            return false;
        }
        bool currentAction = !checkAction || target.controller.actionCoordinator.CurrentHandle == plan.action;
        if (checkAction && (!target.TryReadGroundFinisher(out string currentMotion) || currentMotion != plan.motion))
        {
            return false;
        }
        return currentAction && Time.time <= plan.expiresAt
            && (transform.position - plan.player.position).sqrMagnitude < .0025f
            && (target.Root.position - plan.enemy.position).sqrMagnitude < .0025f
            && Quaternion.Angle(transform.rotation, plan.player.rotation) < 4f
            && Quaternion.Angle(target.Root.rotation, plan.enemy.rotation) < 4f;
    }

    private void RememberGroundFinisherPlan(NativePairPlayback pair, IEnemyQTEParticipant target)
    {
        if (target is VillagerQTEParticipant villager && villager.TryReadGroundFinisher(out string motion))
        {
            groundFinisherPlans[villager] = new GroundFinisherPlan
            {
                pair = pair,
                action = villager.controller.actionCoordinator.CurrentHandle,
                motion = motion,
                player = new Pose(transform.position, transform.rotation),
                enemy = new Pose(target.Root.position, target.Root.rotation),
                expiresAt = Time.time + .22f
            };
        }
    }

    private bool CanAlignGroundFinisher(VillagerQTEParticipant target)
    {
        if (TryReadGroundFinisherPlan(target, out var existing))
        {
            return NativePairStageHasClearance(existing.pair, target, true);
        }
        if (!TryLoadGroundFinisher(target, out var pair, out _))
        {
            return false;
        }
        pair.SetStage(pair.Guard, transform, target.Root, SawPairBlend);
        return TryFitGroundFinisherStage(pair, target);
    }

    private bool TryFitGroundFinisherStage(NativePairPlayback pair, IEnemyQTEParticipant target)
    {
        if (FitsGroundFinisherAt(pair, target, Vector3.zero))
        {
            return true;
        }

        // 靠墙或地形接缝处允许双方整体挪一小步，保留原版刀刃与敌人的相对站位。
        Vector3 direction = Vector3.ProjectOnPlane(pair.PlayerStart.position - pair.EnemyStart.position, Vector3.up).normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, direction);
        for (int ring = 1; ring <= 2; ring++)
        {
            float distance = .28f * ring;
            if (FitsGroundFinisherAt(pair, target, -direction * distance)
                || FitsGroundFinisherAt(pair, target, lateral * distance)
                || FitsGroundFinisherAt(pair, target, -lateral * distance))
            {
                return true;
            }
        }
        // 同一姿态的配对一起转向可达侧，刀刃与敌人的相对轨迹保持一致。
        for (int side = 1; side <= 4; side++)
        {
            float angle = 45f * side;
            if (FitsGroundFinisherAt(pair, target, Vector3.zero, angle)
                || FitsGroundFinisherAt(pair, target, Vector3.zero, -angle))
            {
                return true;
            }
        }
        return false;
    }

    private bool FitsGroundFinisherAt(NativePairPlayback pair, IEnemyQTEParticipant target, Vector3 offset, float yawOffset = 0f)
    {
        float approachDistance = config.groundFinisherDistance + config.clearanceRadius;
        bool fits = pair.FitGroundFinisher(transform, target.Root, approachDistance, config.maximumHeightDifference, offset, yawOffset)
            && NativePairStageHasClearance(pair, target, true);
        if (fits)
        {
            RememberGroundFinisherPlan(pair, target);
        }
        return fits;
    }

    public bool TryStartNearestGroundFinisher()
    {
        return TryStartGroundFinisherInput(ReadFinisherInput());
    }

    internal bool TryStartGroundFinisherInput(InteractionPromptSnapshot request)
    {
        if (!UsesNativeInteractionPrompts || combat == null || combat.IsAiming || PromptInputBlocked()
            || !request.IsValid || request.Kind != InteractionPromptKind.GroundFinisher || !request.CanExecute
            || request.Target is not VillagerQTEParticipant selected)
        {
            return false;
        }
        Camera camera = Camera.main;
        if (camera == null || !IsPromptVisible(camera, selected, selected.Root.position + Vector3.up * .8f))
        {
            return false;
        }
        return TryStart(QTEKind.GroundFinisher, selected, out _);
    }

    private bool IsGroundFinisherMotionClear(Vector3 from, Vector3 to, IEnemyQTEParticipant target)
    {
        from = ResolvePairingClearanceBase(from, target);
        to = ResolvePairingClearanceBase(to, target);
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= .001f)
        {
            return true;
        }

        Vector3 bottom = from + Vector3.up * (config.clearanceRadius + .08f);
        Vector3 top = from + Vector3.up * 1.6f;
        foreach (var hit in Physics.CapsuleCastAll(bottom, top, config.clearanceRadius, delta / distance,
            distance, config.clearanceMask, QueryTriggerInteraction.Ignore))
        {
            if (!ContainsCollider(playerColliders, hit.collider) && !ContainsCollider(target.Colliders, hit.collider))
            {
                return false;
            }
        }
        return true;
    }

    private bool TryLoadGroundFinisher(IEnemyQTEParticipant target, out NativePairPlayback playback, out string motion)
    {
        playback = null;
        motion = null;
        if (target is not VillagerQTEParticipant villager || !villager.TryReadGroundFinisher(out motion)
            || playerAnimator == null || playerAnimator.isHuman || target.Animator == null || target.Animator.isHuman)
        {
            return false;
        }
        if (TryReadGroundFinisherPlan(villager, out var cached) && cached.motion == motion)
        {
            playback = cached.pair;
            return true;
        }
        string role = "GanadoMale";
        if (target.Animator.runtimeAnimatorController != null && target.Animator.runtimeAnimatorController.name == "MainGanado")
        {
            role = "Ganado";
        }
        string resource = ChapterOnePairMotion.ResourceName;
        string enemyPrefix = "GanadoPair_";
        if (villager.controller.missionEnemyType == MissionEnemyType.Sawman)
        {
            resource = "SawKnifeFollowUp";
            role = "Salvador";
            enemyPrefix = "SalvadorPair_";
        }
        var bank = Resources.Load<ChapterOnePairMotion>(resource);
        if (bank == null || !bank.TryGetVillagerTrack("Leon", "LeonPair_" + motion, out var playerTrack)
            || !bank.TryGetVillagerTrack(role, enemyPrefix + motion, out var enemyTrack))
        {
            return false;
        }
        if (!NativeGroundFinisherTiming.TryGet(motion, out var timing))
        {
            return false;
        }
        int contact = timing.contactFrame;
        int end = Mathf.Min(timing.releaseFrame, Mathf.Min(playerTrack.positions.Length, enemyTrack.positions.Length) - 1);
        var stage = new NativePairPlayback.Stage(playerTrack, enemyTrack, end);
        if (!stage.IsValid)
        {
            return false;
        }
        playback = new NativePairPlayback(stage, default, default, 0, contact, Array.Empty<int>(), true);
        playback.HoldEnemyResultPose = true;
        return true;
    }

    private bool TryCommitGroundFinisher(int session)
    {
        if (session != activeSessionId || assassinationCommitted || participant is not VillagerQTEParticipant victim
            || victim.ActiveSessionId != session || !victim.IsAlive || combat == null || combat.knifeModule == null)
        {
            return false;
        }
        if (weaponVisualController == null
            || !weaponVisualController.TryReadNativePairBlade(out Vector3 bladeBase, out Vector3 bladeTip)
            || !HasGroundFinisherBladeContact(bladeBase, bladeTip))
        {
            return false;
        }
        if (!combat.knifeModule.CommitGroundFinisherCost(session, pairedKnife))
        {
            Cancel(session, QTECancelReason.PlayerUnavailable);
            return false;
        }
        if (session != activeSessionId || victim == null || victim.ActiveSessionId != session)
        {
            return false;
        }
        assassinationCommitted = true;
        assassinationCommittedTime = Time.time;
        enemyActionCompleted = true;
        if (!victim.CommitAssassinationResult(session, ResolveAssassinationDamage(victim.controller)))
        {
            Cancel(session, QTECancelReason.EnemyUnavailable);
            return false;
        }
        if (victim.IsAlive && victim.controller.missionEnemyType == MissionEnemyType.Sawman && nativePair != null)
        {
            // 1020 的敌方释放标记在 79 帧，存活时从此处退出，致死则继续倒地。
            nativePair.SurvivorReleaseFrame = 79;
            nativePair.HoldEnemyResultPose = false;
        }
        Vector3 contact = Vector3.Lerp(bladeBase, bladeTip, .7f);
        ChapterOneEffects.TryPlay(ChapterOneEffect.Assassination, contact, -transform.forward);
        PlayKnifePairContactAudio(contact);
        return true;
    }

    private void PlayKnifePairContactAudio(Vector3 contact)
    {
        if (config != null && config.knifeContactAudio != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3D(config.knifeContactAudio, contact);
        }
    }

    private bool HasGroundFinisherBladeContact(Vector3 bladeBase, Vector3 bladeTip)
    {
        Vector3 blade = bladeTip - bladeBase;
        foreach (Transform bone in participant.Root.GetComponentsInChildren<Transform>())
        {
            if (bone.name != "Head" && bone.name != "Neck_0")
            {
                continue;
            }
            float ratio = Mathf.Clamp01(Vector3.Dot(bone.position - bladeBase, blade) / Mathf.Max(blade.sqrMagnitude, .0001f));
            // 沿用现有原版补刀验收器的头颈接触容差，先核对刀刃再消耗耐久。
            if ((bone.position - bladeBase - blade * ratio).sqrMagnitude <= .14f * .14f)
            {
                return true;
            }
        }
        return false;
    }
}
