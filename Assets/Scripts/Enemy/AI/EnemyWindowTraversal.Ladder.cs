using System.Collections.Generic;
using GameArchitecture;
using UnityEngine;
using UnityEngine.Playables;

public partial class EnemyWindowTraversal
{
    private enum LadderPhase { None, Raise, Mount, Climb, Hold, Exit, Fall, Land, SiegeApproach }

    private VillageLadderSite ladder;
    private LadderPhase ladderPhase;
    private ActionHandle ladderAction;
    private int ladderVersion;
    private float ladderElapsed;
    private float ladderDuration;
    private float ladderFallSpeed;
    private int ladderRung;
    private int ladderCycles;
    private int ladderFirstHandRung;
    private int pistolLadderImpact;
    private IReadOnlyList<Vector3> siegeApproachRoute;
    private int siegeWaypoint;
    private Vector3 ladderStart;
    private Vector3 ladderLastPosition;
    private Quaternion ladderStartRotation;
    private bool ladderExitSupported;
    private bool ladderWindowContact;
    private EnemyHeldWeapon ladderWeapon;
    private Transform ladderWeaponParent;
    private Vector3 ladderWeaponPosition;
    private Quaternion ladderWeaponRotation;
    private readonly Transform[,] ladderLimbs = new Transform[4, 3];
    private readonly Transform[] ladderContactBones = new Transform[4];
    private readonly RaycastHit[] ladderFallHits = new RaycastHit[12];

    internal bool HasActiveLadderTraversal => ladderPhase != LadderPhase.None;
    internal bool DeferLadderGroundReaction => HasActiveLadderTraversal && ladderPhase != LadderPhase.Raise;

    internal bool TryReactToLadderDamage()
    {
        if (!DeferLadderGroundReaction || ladder == null || !ladder.Owns(controller, ladderAction, ladderVersion))
        {
            return false;
        }

        if (ladderPhase != LadderPhase.Fall && ladderPhase != LadderPhase.Land)
        {
            StartLadderFall();
        }

        return true;
    }

    internal void ReactToLadderFirearmShot(bool pistol, bool hitHead)
    {
        if (ladder == null || ladderExitSupported || ladderPhase is LadderPhase.Fall or LadderPhase.Land
            || !ladder.Owns(controller, ladderAction, ladderVersion))
        {
            return;
        }

        var profile = controller.combatModule.NativeCombatProfile;
        if (pistol && profile != null)
        {
            // 每次攀爬单独记录抓握受击，身体命中按半次爆头计。
            pistolLadderImpact++;
            if (hitHead)
            {
                pistolLadderImpact++;
            }
            if (pistolLadderImpact < Mathf.Max(1, profile.pistolLadderHeadHitsToFall) * 2)
            {
                return;
            }
        }

        TryReactToLadderDamage();
    }

    internal void OnLadderApproachAssigned()
    {
        ClearPendingRoute();
        ClearPendingDropPlan();
        ClearRegionWindowRoute();
    }

    internal bool TryReadLadderApproach(out Vector3 point)
    {
        point = default;
        if (controller == null || HasActiveLadderTraversal)
        {
            return false;
        }

        var site = VillageLadderSite.GetAssignment(controller);
        return site != null && site.TryReadAssignment(controller, out _, out point);
    }

    internal bool CanBeginLadderTraversal()
    {
        if (!VillageLadderSite.CanAssign(controller) || IsMovementProtected)
        {
            return false;
        }

        var site = VillageLadderSite.GetAssignment(controller);
        return site != null && site.CanBegin(controller);
    }

    internal bool BeginLadderTraversal(ActionHandle action)
    {
        var site = VillageLadderSite.GetAssignment(controller);
        if (site == null || !site.TryBegin(controller, action, out ladderVersion, out var role))
        {
            return false;
        }

        ClearPendingRoute();
        ClearPendingDropPlan();
        ClearRegionWindowRoute();
        ladder = site;
        ladderAction = action;
        ladderExitSupported = false;
        ladderWindowContact = false;
        ladderStartRotation = transform.rotation;
        ladderLastPosition = transform.position;
        ladderRung = 0;
        pistolLadderImpact = 0;
        bool stagingApproach = site.IsSiegeActor(controller) && role == VillageLadderSite.Assignment.Raise;
        BeginTraversalState(stagingApproach);
        CacheLadderLimbs();
        if (role == VillageLadderSite.Assignment.Raise)
        {
            if (stagingApproach)
            {
                siegeApproachRoute = site.ReadSiegeRoute(controller);
                siegeWaypoint = 0;
                var gait = ReadSiegeApproachGait();
                StartLadderPhase(LadderPhase.SiegeApproach, gait, 1f);
            }
            else
            {
                StartLadderPhase(LadderPhase.Raise, site.Clips.raise, site.Clips.raise.length);
            }
        }
        else
        {
            StowLadderWeapon();
            ladderFirstHandRung = site.ReadRungAtHeight(site.Foot.y + 1.55f);
            var mount = site.Clips.mount;
            if (site.IsFixed && site.Clips.fixedEnemyMount != null)
            {
                mount = site.Clips.fixedEnemyMount;
            }
            StartLadderPhase(LadderPhase.Mount, mount, Mathf.Min(mount.length, 1.2f));
        }

        return true;
    }

    private void StartLadderPhase(LadderPhase phase, AnimationClip clip, float duration)
    {
        ladderPhase = phase;
        ladderElapsed = 0f;
        ladderDuration = duration;
        ladderStart = ladderLastPosition;
        if (clip != null)
        {
            PlayTraversalClip(ladder.Clips.Resolve(animator, clip), duration);
            if (clipPlayable.IsValid() && phase == LadderPhase.Climb)
            {
                clipPlayable.SetSpeed(0d);
            }
        }

        ladder.Report(controller, DescribeLadderPhase(phase));
    }

    private void TickLadderTraversal()
    {
        if (ladder == null || !ladder.Owns(controller, ladderAction, ladderVersion) ||
            controller.IsQTEControlled || !controller.IsActive)
        {
            FinishLadderTraversal(true, "动作凭证失效");
            return;
        }

        if (controller.IsDead && traversalMotionToken.Source != MotionSource.Death)
        {
            if (!TryBeginTerminalLadderFall())
            {
                FinishLadderTraversal(false, "交还地面死亡动作");
                return;
            }
        }

        if (!controller.motionExecutor.Valid(traversalMotionToken))
        {
            FinishLadderTraversal(true, "运动凭证失效");
            return;
        }

        if (controller.knifeLocalHitStop != null && controller.knifeLocalHitStop.IsHitStop)
        {
            return;
        }

        bool inSiege = ladder.IsSiegeActor(controller);
        float delta = Time.deltaTime;
        if (inSiege)
        {
            delta = Mathf.Min(Time.unscaledDeltaTime, .075f);
            // 第一阶之后由正式游戏时间接续，玩家回来时仍有机会打断。
            if (ladderPhase == LadderPhase.Climb && ladderRung > 0)
            {
                return;
            }
        }
        if (ladderPhase == LadderPhase.SiegeApproach)
        {
            TickSiegeLadderApproach(delta);
            return;
        }

        if (ladderPhase == LadderPhase.Raise && (controller.IsDead || controller.HasPendingHit ||
            controller.HasPendingCrawl || controller.HasPendingStandUp))
        {
            FinishLadderTraversal(true, "扶梯受击中断");
            return;
        }

        bool losesSupport = (ladder.State != VillageLadderSite.LadderState.Ready || !ladder.IsRouteOpen) && !ladderExitSupported;
        int hitDirection = controller.combatModule.ReadPendingHitDirection;
        bool knockedDown = controller.HasPendingCrawl || (controller.HasPendingHit &&
            (controller.combatModule.ReadPendingTripOver || hitDirection == EnemyCombatModule.ArmedFallingLeftLegHitDirection ||
            hitDirection == EnemyCombatModule.ArmedFallingRightLegHitDirection));
        if (ladderExitSupported && (controller.IsDead || knockedDown))
        {
            FinishLadderTraversal(true, "出口受击");
            return;
        }

        if (ladderPhase != LadderPhase.Raise && ladderPhase != LadderPhase.Fall && ladderPhase != LadderPhase.Land &&
            (losesSupport || controller.IsDead || knockedDown))
        {
            StartLadderFall();
        }

        if (ladderPhase == LadderPhase.Climb && !ladder.CanAdvanceClimber(controller))
        {
            return;
        }
        if (delta <= 0f)
        {
            return;
        }
        ladderElapsed += delta;
        float progress = Mathf.Clamp01(ladderElapsed / ladderDuration);
        switch (ladderPhase)
        {
            case LadderPhase.Raise:
                TickRaise(progress);
                break;
            case LadderPhase.Mount:
                TickMount(progress);
                break;
            case LadderPhase.Climb:
                TickClimb(progress);
                break;
            case LadderPhase.Hold:
                TickLadderHold();
                break;
            case LadderPhase.Exit:
                TickLadderExit(progress);
                break;
            case LadderPhase.Fall:
                TickLadderFall();
                break;
            case LadderPhase.Land:
                if (progress >= 1f)
                {
                    FinishLadderTraversal(true, "坠落结束");
                }

                break;
        }

        if (inSiege && animationGraph.IsValid() && clipPlayable.IsValid())
        {
            if (ladderPhase == LadderPhase.Mount || ladderPhase == LadderPhase.Raise)
            {
                double fraction = Mathf.Clamp01(ladderElapsed / ladderDuration);
                clipPlayable.SetTime(fraction * clipPlayable.GetAnimationClip().length);
            }
            animationGraph.Evaluate(0f);
        }
    }

    private void TickSiegeLadderApproach(float delta)
    {
        Vector3 distanceToFoot = ladder.Approach - ladderLastPosition;
        distanceToFoot.y = 0f;
        if (distanceToFoot.sqrMagnitude <= .16f * .16f)
        {
            if (!ladder.IsSiegeActor(controller) || ladder.CanPerformSiegeRaise(controller))
            {
                if (movementCollider != null)
                {
                    movementCollider.enabled = false;
                }
                ladder.BeginRaise();
                StartLadderPhase(LadderPhase.Raise, ladder.Clips.raise, .75f);
            }
            return;
        }
        if (delta <= 0f)
        {
            return;
        }

        Vector3 toward = distanceToFoot;
        while (siegeApproachRoute != null && siegeWaypoint < siegeApproachRoute.Count)
        {
            toward = siegeApproachRoute[siegeWaypoint] - ladderLastPosition;
            toward.y = 0f;
            if (toward.sqrMagnitude > .12f * .12f)
            {
                break;
            }
            siegeWaypoint++;
            toward = distanceToFoot;
        }
        Vector3 step = ladderLastPosition + Vector3.ClampMagnitude(toward, 2.05f * delta);
        if (!controller.TryResolveSupportedNavigationPosition(step, ladderLastPosition.y, out var support) ||
            !controller.CanMoveDirectlyToNavigationPoint(support))
        {
            ladder.RejectSiegeApproach(controller);
            FinishLadderTraversal(true, "围攻梯脚通路受阻");
            return;
        }
        Quaternion facing = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toward), 500f * delta);
        WriteLadderPose(support, facing);
        if (animationGraph.IsValid() && clipPlayable.IsValid())
        {
            var gait = clipPlayable.GetAnimationClip();
            float cycle = Mathf.Repeat((float)clipPlayable.GetTime() + delta, gait.length);
            clipPlayable.SetTime(cycle);
            animationGraph.Evaluate(0f);
        }
    }

    private AnimationClip ReadSiegeApproachGait()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "Ganado_2001_GaitCycle" || clip.name == "GanadoArmed1st_2001_GaitCycle")
                {
                    return clip;
                }
            }
        }
        return null;
    }

    internal void CancelSiegeLadder(VillageLadderSite site)
    {
        if (ladder == site)
        {
            FinishLadderTraversal(true, "围攻演出结束并释放梯口");
        }
    }

    internal bool TryBeginTerminalLadderFall()
    {
        if (ladder == null || controller == null || !controller.IsActive || controller.IsQTEControlled ||
            ladderPhase == LadderPhase.Raise ||
            !ladder.Owns(controller, ladderAction, ladderVersion))
        {
            return false;
        }

        if (controller.IsDead && traversalMotionToken.Source == MotionSource.Death &&
            controller.motionExecutor.Valid(traversalMotionToken))
        {
            return true;
        }

        MotionControlToken death = controller.actionCoordinator.AuthorizeLadderDeath(ladderAction);
        if (!controller.motionExecutor.Valid(death) || death.Source != MotionSource.Death ||
            !ladder.TransferToDeath(controller, ladderAction, ladderVersion, death.Action))
        {
            return false;
        }

        ladderAction = death.Action;
        traversalMotionToken = death;
        if (ladderPhase != LadderPhase.Fall && ladderPhase != LadderPhase.Land)
        {
            StartLadderFall();
        }

        return true;
    }

    private void TickRaise(float progress)
    {
        float alignment = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.26f));
        WriteLadderPose(Vector3.Lerp(ladderStart, ladder.Approach, alignment),
            Quaternion.Slerp(ladderStartRotation, ladder.Facing, alignment));
        float raiseProgress = Mathf.InverseLerp(0.26f, 0.87f, progress);
        ladder.RaiseProgress(controller, ladderAction, ladderVersion, raiseProgress);
        if (progress >= 1f)
        {
            FinishLadderTraversal(true, "扶梯完成");
        }
    }

    private void TickMount(float progress)
    {
        Vector3 startRung = ladder.ReadClimbPosition(ladder.Foot.y + 0.31f);
        float alignment = Mathf.SmoothStep(0f, 1f, progress);
        WriteLadderPose(Vector3.Lerp(ladderStart, startRung, alignment),
            Quaternion.Slerp(ladderStartRotation, ladder.Facing, alignment));
        if (progress >= 1f)
        {
            float rise = ladder.TopClimbHeight - startRung.y;
            float rungHeight = ladder.RungSpacing * ladder.ClimbAxis.y;
            ladderCycles = Mathf.Max(1, Mathf.RoundToInt(rise / rungHeight));
            var step = ReadEnemyLadderStep();
            StartLadderPhase(LadderPhase.Climb, step, Mathf.Max(0.48f, rungHeight / 0.75f));
        }
    }

    private void TickClimb(float progress)
    {
        float remaining = ladderCycles - ladderRung;
        float nextHeight = Mathf.Lerp(ladderStart.y, ladder.TopClimbHeight, 1f / remaining);
        Vector3 next = ladder.ReadClimbPosition(Mathf.Lerp(ladderStart.y, nextHeight, progress));
        WriteLadderPose(next, ladder.Facing);
        if (clipPlayable.IsValid())
        {
            float length = clipPlayable.GetAnimationClip().length;
            clipPlayable.SetTime(Mathf.Min(length - 0.001f, progress * length));
        }
        if (progress >= 1f)
        {
            ladderRung++;
            var sound = ladder.Clips.leftRungSound;
            if (ladderRung % 2 == 0)
            {
                sound = ladder.Clips.rightRungSound;
            }
            VillageLadderSite.PlaySound(sound, next);
            if (ladderRung >= ladderCycles)
            {
                StartLadderPhase(LadderPhase.Hold, null, 2.2f);
            }
            else
            {
                StartLadderPhase(LadderPhase.Climb, ReadEnemyLadderStep(), ladderDuration);
            }
        }
    }

    private void TickLadderHold()
    {
        if (!ladder.IsPushPending && ladder.TryReserveExit(controller))
        {
            if (ladder.IsWindow)
            {
                StartLadderPhase(LadderPhase.Exit, ladder.Clips.windowExit, 1.18f);
            }
            else
            {
                var exit = ladder.Clips.platformExit;
                if (ladder.IsFixed && ladder.Clips.fixedEnemyExit != null)
                {
                    exit = ladder.Clips.fixedEnemyExit;
                }
                StartLadderPhase(LadderPhase.Exit, exit, 0.96f);
            }
        }
        // 出口未能预留时继续抓住横档。坠落由梯子失稳、受击或失去实际支撑触发。
    }

    private void TickLadderExit(float progress)
    {
        Vector3 overEdge = ladder.Head + Vector3.up * 0.08f + ladder.Outward * 0.06f;
        Vector3 position;
        if (progress <= 0.47f)
        {
            position = Vector3.Lerp(ladderStart, overEdge, Mathf.SmoothStep(0f, 1f, progress / 0.47f));
        }
        else
        {
            position = Vector3.Lerp(overEdge, ladder.Exit, Mathf.SmoothStep(0f, 1f, (progress - 0.47f) / 0.53f));
        }

        WriteLadderPose(position, ladder.Facing);
        if (!ladderWindowContact && ladder.IsWindow && progress >= 0.47f)
        {
            ladderWindowContact = true;
            ladder.NotifyWindowCrossing();
        }

        if (progress >= 0.92f && !ladderExitSupported && ladder.HasExitSupport(position))
        {
            ladderExitSupported = true;
            ladder.Report(controller, "出口承重");
        }

        if (progress >= 1f)
        {
            if (ladderExitSupported)
            {
                FinishLadderTraversal(true, "出梯完成");
            }
            else
            {
                StartLadderFall();
            }
        }
    }

    private void StartLadderFall()
    {
        ladderExitSupported = false;
        ladder.MarkClimberFalling(controller);
        ladderFallSpeed = 0f;
        var fall = ladder.Clips.fall;
        if (ladder.IsFixed && ladder.Clips.fixedEnemyFall != null)
        {
            fall = ladder.Clips.fixedEnemyFall;
        }
        StartLadderPhase(LadderPhase.Fall, fall, fall.length);
    }

    internal void TryFallFromLadderImpact(VillageLadderSite site)
    {
        if (ladder == site && HasActiveLadderTraversal && !ladderExitSupported &&
            ladderPhase != LadderPhase.Fall && ladderPhase != LadderPhase.Land)
        {
            StartLadderFall();
        }
    }

    private void TickLadderFall()
    {
        float delta = Time.deltaTime;
        ladderFallSpeed += 13f * delta;
        Vector3 outwardLanding = ladder.Approach + ladder.Outward * 0.15f;
        outwardLanding.y = ladderLastPosition.y;
        Vector3 position = Vector3.MoveTowards(ladderLastPosition, outwardLanding, 2.8f * delta);
        position.y -= ladderFallSpeed * delta;
        ladder.CheckFallingClimberContact(controller, ladderLastPosition, position);
        if (TryReadLadderFallGround(position, out float ground))
        {
            position.y = ground;
            WriteLadderPose(position, ladder.Facing);
            var land = ladder.Clips.land;
            if (ladder.IsFixed && ladder.Clips.fixedEnemyLand != null)
            {
                land = ladder.Clips.fixedEnemyLand;
            }

            StartLadderPhase(LadderPhase.Land, land, Mathf.Min(land.length, 0.9f));
            return;
        }

        WriteLadderPose(position, ladder.Facing);
        LoopCurrentClipIfNeeded();
        if (ladderElapsed >= 2.8f)
        {
            FinishLadderTraversal(true, "未找到坠落支撑");
        }
    }

    private bool TryReadLadderFallGround(Vector3 next, out float ground)
    {
        Vector3 probe = next;
        probe.y = ladderLastPosition.y + 0.15f;
        float reach = probe.y - next.y + 0.04f;
        int count = Physics.RaycastNonAlloc(probe, Vector3.down, ladderFallHits, reach,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = ladderFallHits[i];
            // 出梯阶段经过的横档、梁和平台边缘都高于梯脚落地区域。
            if (hit.point.y <= ladder.Foot.y + 0.55f && hit.normal.y >= 0.55f && !hit.collider.transform.IsChildOf(transform) &&
                hit.collider.GetComponentInParent<VillagerController>() == null &&
                hit.collider.GetComponentInParent<LeonController>() == null)
            {
                ground = Mathf.Max(ground, hit.point.y);
            }
        }

        return !float.IsNegativeInfinity(ground);
    }

    private AnimationClip ReadEnemyLadderStep()
    {
        bool right = ladderRung % 2 != 0;
        var clips = ladder.Clips;
        if (ladder.IsFixed && clips.fixedEnemyClimb != null)
        {
            if (right && clips.fixedEnemyClimbRight != null)
            {
                return clips.fixedEnemyClimbRight;
            }
            return clips.fixedEnemyClimb;
        }
        if (right && clips.leaningEnemyClimbRight != null)
        {
            return clips.leaningEnemyClimbRight;
        }
        return clips.climb;
    }

    private void WriteLadderPose(Vector3 position, Quaternion facing)
    {
        if (CommitTraversalPose(position, facing))
        {
            ladderLastPosition = position;
        }
    }

    private void FinishLadderTraversal(bool resume, string reason)
    {
        if (!HasActiveLadderTraversal)
        {
            return;
        }

        bool completedExit = ladderPhase == LadderPhase.Exit && ladderExitSupported;
        bool completeDeath = traversalMotionToken.Source == MotionSource.Death && controller != null &&
            controller.IsDead && controller.IsActive && controller.isActiveAndEnabled;
        AnimationClip corpsePose = null;
        if (completeDeath && ladderPhase == LadderPhase.Land && clipPlayable.IsValid())
        {
            corpsePose = clipPlayable.GetAnimationClip();
        }
        if (ladder != null)
        {
            ladder.Release(controller, ladderAction, ladderVersion, reason);
        }

        ladderPhase = LadderPhase.None;
        ladder = null;
        ladderAction = default;
        siegeApproachRoute = null;
        StopTraversalClip();
        RestoreLadderWeapon();
        if (movementCollider != null && controller != null && !controller.IsDead)
        {
            movementCollider.enabled = previousMovementColliderEnabled;
        }

        if (completedExit)
        {
            InitializePlatformRegionState();
        }

        if (agent != null)
        {
            agent.InvalidatePursuitResolutionCache();
        }

        if (completeDeath)
        {
            controller.actionCoordinator.BeginDeath();
            if (corpsePose != null)
            {
                controller.animationPresenter.PresentNativeCombat(corpsePose, corpsePose.length);
            }
        }

        if (resume && controller != null && !controller.IsDead && controller.IsActive && controller.isActiveAndEnabled)
        {
            controller.CompleteTraversalAndResumeChase();
        }
    }

    private void StowLadderWeapon()
    {
        ladderWeapon = GetComponent<EnemyHeldWeapon>();
        if (ladderWeapon == null || !ladderWeapon.IsHeld || animator == null)
        {
            return;
        }

        var back = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.Spine);
        if (back == null)
        {
            return;
        }

        Transform weapon = ladderWeapon.WeaponTransform;
        ladderWeaponParent = weapon.parent;
        ladderWeaponPosition = weapon.localPosition;
        ladderWeaponRotation = weapon.localRotation;
        weapon.SetParent(back, false);
        weapon.localPosition = new Vector3(0.16f, 0.08f, -0.23f);
        weapon.localRotation = Quaternion.Euler(21f, 0f, -38f);
    }

    private void RestoreLadderWeapon()
    {
        if (ladderWeapon != null && ladderWeapon.IsHeld && ladderWeaponParent != null)
        {
            Transform weapon = ladderWeapon.WeaponTransform;
            weapon.SetParent(ladderWeaponParent, false);
            weapon.localPosition = ladderWeaponPosition;
            weapon.localRotation = ladderWeaponRotation;
        }

        ladderWeapon = null;
        ladderWeaponParent = null;
    }

    private void CacheLadderLimbs()
    {
        CacheLadderLimb(0, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        CacheLadderLimb(1, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        CacheLadderLimb(2, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        CacheLadderLimb(3, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        ladderContactBones[0] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftMiddleProximal);
        ladderContactBones[1] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightMiddleProximal);
        ladderContactBones[2] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftToes);
        ladderContactBones[3] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightToes);
    }

    private void CacheLadderLimb(int index, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones tip)
    {
        ladderLimbs[index, 0] = AnimatorBoneBindings.Resolve(animator, upper);
        ladderLimbs[index, 1] = AnimatorBoneBindings.Resolve(animator, lower);
        ladderLimbs[index, 2] = AnimatorBoneBindings.Resolve(animator, tip);
    }

    private void ApplyLadderContacts()
    {
        if (animator == null || ladder == null)
        {
            return;
        }

        if (ladderPhase == LadderPhase.Raise)
        {
            AlignRaisingHand(0, -1f);
            AlignRaisingHand(1, 1f);
            return;
        }

        if (ladderPhase != LadderPhase.Mount && ladderPhase != LadderPhase.Climb &&
            ladderPhase != LadderPhase.Hold && ladderPhase != LadderPhase.Exit)
        {
            return;
        }

        float phase = Mathf.Clamp01(ladderElapsed / ladderDuration);
        float cycle = 0f;
        float hands = 1f;
        float leftFoot = 1f;
        float rightFoot = 1f;
        if (ladderPhase == LadderPhase.Mount)
        {
            hands = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.73f, phase));
            leftFoot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.95f, phase));
            rightFoot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.37f, 0.86f, phase));
        }
        else if (ladderPhase == LadderPhase.Climb)
        {
            cycle = phase;
        }
        else if (ladderPhase == LadderPhase.Exit)
        {
            hands = Mathf.InverseLerp(0.36f, 0.06f, phase);
            leftFoot = Mathf.InverseLerp(0.26f, 0.02f, phase);
            rightFoot = leftFoot;
        }

        // 抓握与踩踏期间保留固定梯档，只有换手、换脚的时间段沿梯面移动。
        AlignLadderLimb(0, ladderFirstHandRung + ladderRung, -0.24f, cycle, 0.04f, 0.37f, hands);
        AlignLadderLimb(1, ladderFirstHandRung + ladderRung + 1, 0.24f, cycle, 0.54f, 0.87f, hands);
        AlignLadderLimb(2, ladderRung + 1, -0.16f, cycle, 0.59f, 0.97f, leftFoot);
        AlignLadderLimb(3, ladderRung + 1, 0.16f, cycle, 0.09f, 0.47f, rightFoot);
    }

    private void AlignRaisingHand(int index, float side)
    {
        var upper = ladderLimbs[index, 0];
        var lower = ladderLimbs[index, 1];
        var tip = ladderLimbs[index, 2];
        if (upper == null || lower == null || tip == null)
        {
            return;
        }

        float progress = ladderElapsed / ladderDuration;
        float grip = Mathf.InverseLerp(0.20f, 0.33f, progress) * Mathf.InverseLerp(0.93f, 0.79f, progress);
        Vector3 contact = ladder.RaiseHandContact(side);
        float reach = Mathf.InverseLerp(0.65f, 0.16f, Vector3.Distance(tip.position, contact));
        if (grip * reach > 0f)
        {
            EnemyAnimationAdapter.FitLadderBoneContact(upper, lower, tip, Vector3.Lerp(tip.position, contact, grip * reach));
        }
    }

    private void AlignLadderLimb(int index, int rung, float across, float cycle, float release, float plant, float weight)
    {
        var upper = ladderLimbs[index, 0];
        var lower = ladderLimbs[index, 1];
        var tip = ladderLimbs[index, 2];
        if (upper == null || lower == null || tip == null || weight <= 0f)
        {
            return;
        }

        bool hand = index < 2;
        float transfer = Mathf.InverseLerp(release, plant, cycle);
        float swing = Mathf.Sin(Mathf.PI * transfer);
        float lift = 0.10f;
        float offset = 0.045f;
        Vector3 bend = -ladder.Outward + ladder.Across * Mathf.Sign(across) * 0.35f;
        Vector3 facing = -ladder.Outward;
        if (hand)
        {
            lift = 0.07f;
            offset = 0.065f;
            bend = Vector3.down + ladder.Across * Mathf.Sign(across) * 0.7f;
            facing -= Vector3.up * 0.23f;
        }

        Vector3 lowerRung = ladder.ReadRungPosition(rung, across, offset);
        Vector3 upperRung = ladder.ReadRungPosition(rung + 1, across, offset);
        Vector3 contact = Vector3.Lerp(lowerRung, upperRung, Mathf.SmoothStep(0f, 1f, transfer)) +
            (ladder.Outward * lift + Vector3.up * (0.04f * (1f - transfer))) * swing;
        if (!hand)
        {
            contact += Vector3.up * 0.034f;
        }

        Quaternion rotation = tip.rotation;
        var contactBone = ladderContactBones[index];
        if (contactBone != null)
        {
            Vector3 authoredOffset = contactBone.position - tip.position;
            Quaternion alignment = Quaternion.FromToRotation(authoredOffset, facing);
            rotation = Quaternion.Slerp(rotation, alignment * rotation, weight);
            contact -= rotation * (Quaternion.Inverse(tip.rotation) * authoredOffset);
        }

        EnemyAnimationAdapter.FitLadderBoneContact(upper, lower, tip,
            Vector3.Lerp(tip.position, contact, weight), bend, rotation);
    }

    private static string DescribeLadderPhase(LadderPhase phase)
    {
        switch (phase)
        {
            case LadderPhase.Raise:
                return "扶梯";
            case LadderPhase.Mount:
                return "起攀";
            case LadderPhase.Climb:
                return "攀爬";
            case LadderPhase.Hold:
                return "等候出口";
            case LadderPhase.Exit:
                return "出梯";
            case LadderPhase.Fall:
                return "失手坠落";
            case LadderPhase.Land:
                return "坠落接地";
            case LadderPhase.SiegeApproach:
                return "围攻接近梯脚";
            default:
                return "结束";
        }
    }
}
