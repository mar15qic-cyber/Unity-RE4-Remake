using System;
using RE4.ChapterOne;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public partial class LeonQTECoordinator
{
    private const int SawInputFrame = 24;
    private const int SawGameOverFrame = 520;
    // 0030 的前段仍在抵挡，刀链在源第 374 帧首次压到头部。
    private const int SawDamageContactFrame = 374;
    private const float SawPairBlend = 0.16f;
    private NativePairPlayback nativePair;
    private NativePairPlayback fadingNativePair;
    private NativePairPlayback terminalNativePair;
    private bool nativeSawKnifeVisible;
    [Tooltip("启用已在本场景验证的原版村民配对。")]
    [SerializeField] private bool useNativeVillagerPairs;

    internal bool HoldsNativeDeathPose => terminalNativePair != null;

    internal bool OwnsNativeFailureDeath(int session)
    {
        return session != 0 && session == activeSessionId && nativePair != null && phase == QTEPhase.FailureResult;
    }

    internal bool IsNativePairPlayerRecovering => nativePair != null && nativePair.IsPlayerRecovering;

    internal bool HasNativePairCameraClock(int sessionId)
    {
        return sessionId != 0 && sessionId == activeSessionId && nativePair != null;
    }

    internal bool TryGetNativePairCameraFrame(int sessionId, out string clipName, out float frame,
        out int frameRate, out Pose stage)
    {
        clipName = null;
        frame = 0f;
        frameRate = 0;
        stage = default;
        if (!HasNativePairCameraClock(sessionId) || phase is not (QTEPhase.Entering or QTEPhase.WaitingForInput
            or QTEPhase.SuccessResult or QTEPhase.FailureResult))
        {
            return false;
        }

        clipName = nativePair.PlayerClipName;
        frame = nativePair.SourceFrame;
        frameRate = Mathf.RoundToInt(nativePair.FrameRate);
        stage = nativePair.CameraStage;
        if (nativePair.IsPlayerRecovering)
        {
            clipName = nativePair.Failure.player.clip.name;
            frame = nativePair.Failure.finalFrame;
        }
        return true;
    }

    private bool UsesNativeSawPair(QTEKind kind, IEnemyQTEParticipant target)
    {
        return kind == QTEKind.ChainsawExecution && playerAnimator != null && !playerAnimator.isHuman
            && target is VillagerQTEParticipant saw && saw.controller != null
            && saw.controller.missionEnemyType == MissionEnemyType.Sawman && saw.Animator != null && !saw.Animator.isHuman;
    }

    private static bool TryLoadNativeSawPair(out NativePairPlayback playback)
    {
        playback = null;
        var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
        if (bank == null
            || !bank.TryGetNamedTrack("LeonPair_1200", out var guardPlayer)
            || !bank.TryGetNamedTrack("SalvadorPair_1200", out var guardEnemy)
            || !bank.TryGetNamedTrack("LeonPair_1202", out var escapePlayer)
            || !bank.TryGetNamedTrack("SalvadorPair_1202", out var escapeEnemy)
            || !bank.TryGetNamedTrack("LeonPair_0030_jack_pl_2nd_wp5300", out var failurePlayer)
            || !bank.TryGetNamedTrack("SalvadorPair_0030", out var failureEnemy))
        {
            return false;
        }

        var candidate = new NativePairPlayback(
            new NativePairPlayback.Stage(guardPlayer, guardEnemy, ChapterOneLoop.SawTimeoutFrame),
            new NativePairPlayback.Stage(escapePlayer, escapeEnemy, 160),
            new NativePairPlayback.Stage(failurePlayer, failureEnemy, SawGameOverFrame),
            SawInputFrame, SawDamageContactFrame, ChapterOneLoop.SawEscapeFrames);
        if (!candidate.HasValidStages)
        {
            return false;
        }
        playback = candidate;
        return true;
    }

    private bool BeginNativePair(int session)
    {
        var pair = nativePair;
        if (session != activeSessionId || pair == null || participant == null)
        {
            return false;
        }

        ChangePhase(QTEPhase.Aligning);
        if (session != activeSessionId)
        {
            return false;
        }

        bool preparedFinisher = activeKind == QTEKind.GroundFinisher
            && participant is VillagerQTEParticipant finisherTarget
            && TryReadGroundFinisherPlan(finisherTarget, out var preparedPlan, false)
            && ReferenceEquals(pair, preparedPlan.pair);
        if (!preparedFinisher)
        {
            pair.SetStage(pair.Guard, transform, participant.Root, SawPairBlend);
        }
        if (activeKind == QTEKind.ChainsawExecution && chainsawContactFailure)
        {
            pair.SetStage(pair.Failure, transform, participant.Root, SawPairBlend);
        }
        bool groundFinisher = activeKind == QTEKind.GroundFinisher;
        bool fitsStage;
        if (groundFinisher)
        {
            if (preparedFinisher)
            {
                fitsStage = NativePairStageHasClearance(pair, participant, true);
            }
            else
            {
                fitsStage = TryFitGroundFinisherStage(pair, participant);
            }
        }
        else if (activeKind == QTEKind.ChainsawExecution)
        {
            var stage = pair.Guard;
            if (chainsawContactFailure)
            {
                stage = pair.Failure;
            }
            fitsStage = TryFitStandingPairStage(pair, participant, stage, SawPairBlend);
        }
        else
        {
            fitsStage = NativePairStageHasClearance(pair, participant);
        }
        if (!fitsStage)
        {
            Cancel(session, QTECancelReason.SpaceBlocked);
            return Reject(QTECancelReason.SpaceBlocked);
        }

        if (!animationCoordinator.RequestQTE(session, false))
        {
            Cancel(session, QTECancelReason.AnimationInterrupted);
            return Reject(QTECancelReason.AnimationInterrupted);
        }

        pair.BeginGraph(playerAnimator, participant.Animator);
        if (activeKind == QTEKind.ChainsawExecution && !chainsawContactFailure && weaponVisualController != null)
        {
            weaponVisualController.SetKnifeModeVisible(true);
            nativeSawKnifeVisible = true;
        }
        if (activeKind == QTEKind.ChainsawExecution && chainsawContactFailure)
        {
            pendingResult = QTEResult.Failure;
            ChangePhase(QTEPhase.FailureResult);
        }
        else if (pair.SingleShot)
        {
            pendingResult = QTEResult.Success;
            ChangePhase(QTEPhase.SuccessResult);
        }
        else
        {
            ChangePhase(QTEPhase.Entering);
        }
        return session == activeSessionId;
    }

    private void AdvanceRearHold(NativePairPlayback pair)
    {
        if (!IsRearHelper(rearHelper) || rearHelper.HasPendingHit || rearHelper.IsNavigationTraversalProtected)
        {
            rearHelper = null;
            if (EnemyAIManager.HasInstance)
            {
                float bestDistance = 4f;
                foreach (var actor in EnemyAIManager.Instance.StandingContactEnemies)
                {
                    if (actor == null || !actor.isActiveAndEnabled || !actor.IsCombatEngaged ||
                        actor.IsDead || actor.IsQTEControlled || actor.HasPendingHit || actor.IsNavigationTraversalProtected ||
                        actor.CurrentState != VillagerState.Chase && actor.CurrentState != VillagerState.Attack ||
                        actor.missionEnemyType != MissionEnemyType.Ganado || participant.Root == actor.transform)
                    {
                        continue;
                    }
                    Vector3 offset = actor.transform.position - transform.position;
                    if (Mathf.Abs(offset.y) > .45f || offset.magnitude >= bestDistance ||
                        !actor.perceptionModule.HasClearLineToPlayer(false, bestDistance + .5f))
                    {
                        continue;
                    }
                    bestDistance = offset.magnitude;
                    rearHelper = actor;
                    rearHelperIdentity = actor.actionCoordinator.Identity;
                }
            }
        }
        if (rearHelper != null)
        {
            Vector3 toHelper = Vector3.ProjectOnPlane(rearHelper.transform.position - transform.position, Vector3.up);
            if (toHelper.magnitude > 1.15f)
            {
                Vector3 advance = toHelper.normalized * Mathf.Min(.65f * Time.deltaTime, toHelper.magnitude - 1.15f);
                var first = transform.position;
                var second = participant.Root.position;
                if ((ResolveRearEscapeStep(first, first + advance) - first - advance).sqrMagnitude < .000001f &&
                    (ResolveRearEscapeStep(second, second + advance) - second - advance).sqrMagnitude < .000001f &&
                    PairStartHasSupport(first + advance) && PairStartHasSupport(second + advance))
                {
                    pair.TranslateStage(advance);
                }
            }
        }
        if (!pair.EscapeQueued && inputBridge.IsEscapeHeld(activeSessionId))
        {
            float target = Mathf.Max(.1f, config.rearHoldSeconds);
            float delta = Mathf.Min(Time.deltaTime, target - rearHoldElapsed, remainingTime);
            if (delta > 0f)
            {
                rearHoldElapsed += delta;
                pressCount = Mathf.FloorToInt(Mathf.Clamp01(rearHoldElapsed / target) * config.requiredPresses);
                controller.ApplyRearHoldDamage(controller.maxHealth * config.rearDrainPerSecond * delta);
                if (IsActive && rearHoldElapsed >= target - .0001f)
                {
                    pair.QueueEscape();
                }
            }
        }
    }

    internal bool CommitRearHelperHit(VillagerController enemy, int session, float damage, Vector3 point, Vector3 normal)
    {
        if (session != activeSessionId || !IsRearHelper(enemy) || nativePair == null || nativePair.EscapeQueued)
        {
            return false;
        }
        rearHelperHit = true;
        damageCommitted = true;
        controller.ApplyRearHoldDamage(damage);
        if (enemy.TryGetComponent<EnemyHeldWeapon>(out var weapon) && weapon.IsHeld)
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.SlashBlood, point, normal);
            weapon.PlayHitAudio(point);
        }
        else
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.KickImpact, point, normal);
        }
        if (session == activeSessionId)
        {
            BeginNativePairResult(nativePair, false);
        }
        return true;
    }

    private void UpdateNativePair()
    {
        var pair = nativePair;
        int session = activeSessionId;
        if (activeKind == QTEKind.RearGrab && phase == QTEPhase.WaitingForInput)
        {
            AdvanceRearHold(pair);
            if (session != activeSessionId)
            {
                return;
            }
        }
        pair.Step(Time.deltaTime);
        pair.ReadPoses(out var playerPose, out var enemyPose);
        if (activeKind == QTEKind.GroundFinisher)
        {
            bool playerBlocked = IsPairingSpaceBlocked(playerPose.position, participant)
                || !IsGroundFinisherMotionClear(transform.position, playerPose.position, participant);
            bool enemyBlocked = IsPairingSpaceBlocked(enemyPose.position, participant)
                || !IsGroundFinisherMotionClear(participant.Root.position, enemyPose.position, participant);
            if ((playerBlocked || enemyBlocked) && !assassinationCommitted)
            {
                Cancel(session, QTECancelReason.SpaceBlocked);
                return;
            }

            // 命中后的收步可以停在障碍前，保持已提交的补刀和收刀动画。
            if (playerBlocked)
            {
                playerPose.position = transform.position;
            }
            if (enemyBlocked)
            {
                enemyPose.position = participant.Root.position;
            }
        }
        else if (activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearGrab)
        {
            playerPose.position = ResolveRearEscapeStep(transform.position, playerPose.position);
            enemyPose.position = ResolveRearEscapeStep(participant.Root.position, enemyPose.position);
        }
        else if (!NativePairHasClearance(playerPose, enemyPose, participant))
        {
            Cancel(session, QTECancelReason.SpaceBlocked);
            return;
        }
        if (!playerMotion.CommitPairedActionPose(session, playerPose.position, playerPose.rotation, true)
            || !participant.CommitPairedActionPose(session, enemyPose.position, enemyPose.rotation))
        {
            Cancel(session, QTECancelReason.AnimationInterrupted);
            return;
        }
        pair.Present();
        if (TryGetComponent<NativeActionAudio>(out var audio))
        {
            var source = participant.Root;
            var saw = source.GetComponentInChildren<ChainsawManAnimationBridge>();
            if (saw != null && saw.chainsawDamageOrigin != null)
            {
                source = saw.chainsawDamageOrigin;
            }
            audio.SamplePair(pair.EnemyClipName, session, pair.EnemyAudioTime, source);
            audio.SamplePlayerPair(pair.PlayerClipName, session, pair.PlayerAudioTime);
        }

        if (phase == QTEPhase.Entering && pair.SourceFrame >= pair.InputFrame)
        {
            ChangePhase(QTEPhase.WaitingForInput);
        }
        if (session != activeSessionId)
        {
            return;
        }
        if (phase == QTEPhase.WaitingForInput)
        {
            bool grab = activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearGrab;
            bool resultReady = pair.Finished;
            if (grab)
            {
                remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
                resultReady = remainingTime <= 0f || pair.EscapeQueued && pair.Finished;
            }
            else
            {
                remainingTime = Mathf.Max(0f, (pair.Guard.finalFrame - pair.SourceFrame) / pair.FrameRate);
            }
            PublishProgress();
            if (resultReady)
            {
                BeginNativePairResult(pair, pair.EscapeQueued);
            }
        }
        else if (phase == QTEPhase.SuccessResult && pair.Finished)
        {
            if (activeKind == QTEKind.GroundFinisher && !pair.ContactSent)
            {
                Cancel(session, QTECancelReason.SpaceBlocked);
                return;
            }
            CompleteNativePair(true);
        }
        else if (phase == QTEPhase.FailureResult && pair.Finished)
        {
            if (activeKind == QTEKind.FrontGrab && !pair.IsPlayerRecovering
                && !controller.Health.IsDead)
            {
                var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
                if (bank != null && bank.TryGetNamedTrack("LeonRecovery_0403", out var recovery))
                {
                    pair.BeginPlayerRecovery(recovery, transform, participant.Root);
                    return;
                }
            }
            // 处决接触帧已结算伤害。结尾只完成配对，并保留受害者最后的姿势。
            CompleteNativePair(false);
        }
    }

    private void DispatchNativePairContact()
    {
        var pair = nativePair;
        if (pair == null)
        {
            return;
        }
        PresentNativeFrontGrabLanding(pair);
        if (pair.ContactSent || pair.SourceFrame < pair.ContactFrame - 0.001f)
        {
            return;
        }

        int session = activeSessionId;
        if (pair.SingleShot && phase == QTEPhase.SuccessResult)
        {
            if (activeKind == QTEKind.GroundFinisher)
            {
                if (!TryCommitGroundFinisher(session))
                {
                    // 接触标记之后给刀刃短暂的实际接触窗口。耗损及伤害只在有效接触时提交。
                    if (session == activeSessionId && pair.SourceFrame >= pair.ContactFrame + 6f)
                    {
                        Cancel(session, QTECancelReason.SpaceBlocked);
                    }
                    return;
                }
            }
            else
            {
                CommitAssassination(session);
            }
            pair.ContactSent = true;
            if (session == activeSessionId && OnQTEAssassinationContact != null)
            {
                OnQTEAssassinationContact.Invoke(session);
            }
        }
        else if (phase == QTEPhase.FailureResult)
        {
            pair.ContactSent = true;
            if (activeKind == QTEKind.ChainsawExecution)
            {
                Transform head = GetPlayerBone(HumanBodyBones.Head);
                Vector3 hitPoint = ResolveNeckBiteHitPoint();
                if (head != null)
                {
                    hitPoint = head.position + transform.forward * .04f;
                }
                pair.ContactVfxPlayed = ChapterOneEffects.TryPlay(ChapterOneEffect.ChainsawBlood,
                    hitPoint, transform.forward);
            }
            if (activeKind != QTEKind.RearGrab && OnQTEBiteContact != null)
            {
                OnQTEBiteContact.Invoke(session);
            }
            if (session == activeSessionId && activeKind is QTEKind.FrontGrab or QTEKind.ChainsawExecution)
            {
                CommitBiteDamage(session);
                if (session == activeSessionId && activeKind == QTEKind.FrontGrab && controller.Health.IsDead)
                {
                    pair.HoldDownedPlayerAtFrame(FrontGrabDownedFrame);
                }
            }
        }
    }

    private void PresentNativeFrontGrabLanding(NativePairPlayback pair)
    {
        if (activeKind != QTEKind.FrontGrab || pair.GroundVfxPlayed)
        {
            return;
        }
        Transform actor;
        if (phase == QTEPhase.SuccessResult && pair.SourceFrame >= 64f && pair.SourceFrame <= 79f)
        {
            actor = participant.Root;
        }
        else if (phase == QTEPhase.FailureResult && pair.SourceFrame >= 72f && pair.SourceFrame <= 81f)
        {
            actor = transform;
        }
        else
        {
            return;
        }

        if (FootstepSurface.TryFindGround(actor, 0.48f, Physics.DefaultRaycastLayers, out var ground))
        {
            pair.GroundVfxPlayed = ChapterOneEffects.TryPlay(ChapterOneEffect.PairLandingDust, ground.point, ground.normal);
        }
    }

    private void BeginNativePairResult(NativePairPlayback pair, bool escaped)
    {
        var stage = pair.Failure;
        float blend = 0.24f;
        pendingResult = QTEResult.Failure;
        var resultPhase = QTEPhase.FailureResult;
        if (escaped)
        {
            stage = pair.Escape;
            blend = SawPairBlend;
            pendingResult = QTEResult.Success;
            resultPhase = QTEPhase.SuccessResult;
        }

        if (!escaped && activeKind == QTEKind.RearBiteNeck)
        {
            pair.BeginRearPush(config.rearPushStagger, transform, participant.Root);
            if (TryGetComponent<NativeActionAudio>(out var audio))
            {
                audio.SeekPair(pair.EnemyClipName, activeSessionId, pair.EnemyAudioTime);
            }
            StopQTEStruggleLoop();
            ChangePhase(resultPhase);
            return;
        }

        bool stageClear;
        if (activeKind == QTEKind.ChainsawExecution)
        {
            stageClear = TryFitStandingPairStage(pair, participant, stage, blend);
        }
        else if (activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearGrab)
        {
            if (!TryFitStandingPairStage(pair, participant, stage, blend))
            {
                // 挣脱输入已经生效。狭窄处限制双方的落步，继续播放脱手与退让。
                pair.SetStage(stage, transform, participant.Root, blend);
            }
            stageClear = true;
        }
        else
        {
            pair.SetStage(stage, transform, participant.Root, blend);
            stageClear = NativePairStageHasClearance(pair, participant);
        }
        if (!stageClear)
        {
            Cancel(activeSessionId, QTECancelReason.SpaceBlocked);
            return;
        }
        pair.BeginResult();
        if (!escaped)
        {
            HideNativeSawKnife();
        }
        StopQTEStruggleLoop();
        ChangePhase(resultPhase);
    }

    private Vector3 ResolveRearEscapeStep(Vector3 from, Vector3 to)
    {
        if (RearEscapeStepIsClear(to))
        {
            return to;
        }

        float low = 0f;
        float high = 1f;
        for (int check = 0; check < 4; check++)
        {
            float weight = (low + high) * .5f;
            if (RearEscapeStepIsClear(Vector3.Lerp(from, to, weight)))
            {
                low = weight;
            }
            else
            {
                high = weight;
            }
        }
        return Vector3.Lerp(from, to, low);

        bool RearEscapeStepIsClear(Vector3 destination)
        {
            return !IsPairingSpaceBlocked(destination, participant)
                && IsGroundFinisherMotionClear(from, destination, participant);
        }
    }

    private bool TryFitStandingPairStage(NativePairPlayback pair, IEnemyQTEParticipant target,
        NativePairPlayback.Stage stage, float blend)
    {
        pair.SetStage(stage, transform, target.Root, blend);
        if (IsStandingPairStageClear(pair, target))
        {
            return true;
        }

        // 整体移到附近的净空位置，保持双方接触姿态及两条根轨的相对位置。
        Vector3 forward = Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        for (int ring = 1; ring <= 2; ring++)
        {
            for (int direction = 0; direction < 8; direction++)
            {
                Vector3 offset = Quaternion.AngleAxis(direction * 45f, Vector3.up) * side * (ring * .27f);
                pair.SetStage(stage, transform, target.Root, blend);
                pair.TranslateStage(offset);
                if (IsStandingPairStageClear(pair, target))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsStandingPairStageClear(NativePairPlayback pair, IEnemyQTEParticipant target)
    {
        return PairStartHasSupport(pair.PlayerStart.position) && PairStartHasSupport(pair.EnemyStart.position)
            && NativePairStageHasClearance(pair, target, sweepMotion: true);
    }

    private bool PairStartHasSupport(Vector3 foot)
    {
        foreach (var hit in Physics.RaycastAll(foot + Vector3.up * .25f, Vector3.down,
            .75f, config.clearanceMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y >= .65f && hit.collider.attachedRigidbody == null)
            {
                return true;
            }
        }
        return false;
    }

    private bool NativePairStageHasClearance(NativePairPlayback pair, IEnemyQTEParticipant target,
        bool groundFinisher = false, bool sweepMotion = false)
    {
        if (!NativePairHasClearance(pair.PlayerStart, pair.EnemyStart, target))
        {
            return false;
        }

        float duration = pair.StageDuration;
        if (groundFinisher)
        {
            // 补刀只要求能走到刀刃接触帧，收步的障碍在播放时处理。
            duration = Mathf.Min(duration, pair.ContactFrame / pair.FrameRate);
            if (!IsGroundFinisherMotionClear(transform.position, pair.PlayerStart.position, target)
                || !IsGroundFinisherMotionClear(target.Root.position, pair.EnemyStart.position, target))
            {
                return false;
            }
        }

        Vector3 previousPlayerPosition = transform.position;
        Vector3 previousEnemyPosition = target.Root.position;
        int checks = Mathf.Max(1, Mathf.CeilToInt(duration * 12f));
        for (int index = 1; index <= checks; index++)
        {
            pair.ReadPosesAt(duration * index / checks, out var playerPose, out var enemyPose);
            if (!NativePairHasClearance(playerPose, enemyPose, target)
                || (groundFinisher || sweepMotion) && (!IsGroundFinisherMotionClear(previousPlayerPosition, playerPose.position, target)
                    || !IsGroundFinisherMotionClear(previousEnemyPosition, enemyPose.position, target)))
            {
                return false;
            }
            previousPlayerPosition = playerPose.position;
            previousEnemyPosition = enemyPose.position;
        }
        return true;
    }

    private bool NativePairHasClearance(Pose playerPose, Pose enemyPose, IEnemyQTEParticipant target)
    {
        return !IsPairingSpaceBlocked(playerPose.position, target)
            && !IsPairingSpaceBlocked(enemyPose.position, target);
    }

    private void CompleteNativePair(bool escaped)
    {
        int session = activeSessionId;
        if (escaped && activeKind == QTEKind.ChainsawExecution)
        {
            VillageLeonDialogue.Report(controller, VillageSpeechCue.SawParry);
        }
        if (participant is VillagerQTEParticipant saw)
        {
            saw.RecordNativePairResult(session, escaped);
        }
        var result = pendingResult;
        ChangePhase(QTEPhase.Exiting);
        if (session == activeSessionId)
        {
            ChangePhase(QTEPhase.Completed);
            FinishCleanup(result, QTECancelReason.None);
        }
    }

    private void StopNativePair(bool fadeOut = false, bool keepPlayerDeathPose = false)
    {
        var pair = nativePair;
        if (pair == null)
        {
            return;
        }

        // 补刀已提交死亡时，把最后的原版敌人姿势交给表现组件持有，避免回到站立控制器。
        if (activeKind == QTEKind.GroundFinisher && assassinationCommitted
            && participant is VillagerQTEParticipant corpse && !corpse.IsAlive && corpse.Root != null)
        {
            var presenter = corpse.Root.GetComponent<EnemyAnimationAdapter>();
            if (presenter != null)
            {
                presenter.PresentNativeCombat(pair.EnemyClip, pair.SourceFrame / pair.FrameRate);
            }
        }
        nativePair = null;
        if (keepPlayerDeathPose)
        {
            DisposeNativeDeathPose();
            terminalNativePair = pair;
            pair.HoldPlayerDeathPose();
        }
        else if (fadeOut && pair.BeginFade())
        {
            DisposeNativePairFade();
            fadingNativePair = pair;
        }
        else
        {
            pair.Dispose();
        }
        HideNativeSawKnife();
    }

    private void UpdateNativePairFade()
    {
        if (fadingNativePair != null && fadingNativePair.AdvanceFade(Time.deltaTime))
        {
            DisposeNativePairFade();
        }
    }

    private void DisposeNativePairFade()
    {
        if (fadingNativePair != null)
        {
            fadingNativePair.Dispose();
            fadingNativePair = null;
        }
    }

    private void DisposeNativeDeathPose()
    {
        if (terminalNativePair != null)
        {
            terminalNativePair.Dispose();
            terminalNativePair = null;
        }
    }

    private void HideNativeSawKnife()
    {
        if (nativeSawKnifeVisible && weaponVisualController != null)
        {
            weaponVisualController.SetKnifeModeVisible(false);
        }
        nativeSawKnifeVisible = false;
    }

    private sealed class NativePairPlayback : IDisposable
    {
        internal readonly struct Stage
        {
            public readonly ChapterOnePairMotion.Track player;
            public readonly ChapterOnePairMotion.Track enemy;
            public readonly int finalFrame;

            public Stage(ChapterOnePairMotion.Track player, ChapterOnePairMotion.Track enemy, int finalFrame)
            {
                this.player = player;
                this.enemy = enemy;
                this.finalFrame = finalFrame;
            }

            public bool IsValid => player != null && enemy != null && player.clip != null && enemy.clip != null
                && player.clip.frameRate == enemy.clip.frameRate
                && player.positions.Length > finalFrame && enemy.positions.Length > finalFrame
                && (player.sourceOrigin - enemy.sourceOrigin).sqrMagnitude > 0.05f;
        }

        private readonly PairOutput playerOutput = new();
        private readonly PairOutput enemyOutput = new();
        private PlayableGraph graph;
        private Stage current;
        private Pose previousPlayer;
        private Pose previousEnemy;
        private float time;
        private float carryStep;
        private float blend;
        private float presentationWeight;
        private float fadeElapsed;
        private bool rearPush;
        private float rearPushDuration;
        private float enemyReleaseStart;
        private int escapeFrame;
        private bool isResult;
        private bool continuedGuard;
        private float recoveryEnemyTime;
        private float playerDeathTime = -1f;
        public bool IsPlayerRecovering { get; private set; }

        public readonly Stage Guard;
        public readonly Stage Escape;
        public readonly Stage Failure;
        public readonly int InputFrame;
        public readonly int ContactFrame;
        public readonly bool SingleShot;
        private readonly int[] escapeFrames;
        public Pose PlayerStart { get; private set; }
        public Pose EnemyStart { get; private set; }
        public float FrameRate => current.player.clip.frameRate;
        public string EnemyClipName => current.enemy.clip.name;
        public float EnemyAudioTime
        {
            get
            {
                if (rearPush)
                {
                    return enemyReleaseStart + time;
                }
                if (IsPlayerRecovering && HoldEnemyResultPose)
                {
                    return recoveryEnemyTime;
                }
                return time;
            }
        }
        public AnimationClip EnemyClip => current.enemy.clip;
        public string PlayerClipName => current.player.clip.name;
        public float PlayerAudioTime => ReadPlayerPoseTime(time);
        public Pose CameraStage => new(PlayerStart.position - PlayerStart.rotation * current.player.sourceOrigin,
            PlayerStart.rotation);
        public float SourceFrame => time * FrameRate;
        public float StageDuration => current.finalFrame / FrameRate;
        public bool EscapeQueued => escapeFrame > 0;
        public bool Finished => SourceFrame >= EndFrame - 0.001f;
        public bool GroundVfxPlayed { get; set; }
        public bool ContactVfxPlayed { get; set; }
        public bool ContactSent { get; set; }
        public bool HoldEnemyResultPose { get; set; }
        public bool HasValidStages => Guard.IsValid && (SingleShot || Escape.IsValid && Failure.IsValid);

        private int EndFrame
        {
            get
            {
                if (rearPush)
                {
                    return Mathf.RoundToInt(rearPushDuration * FrameRate);
                }
                if (!isResult && EscapeQueued)
                {
                    return escapeFrame;
                }
                return current.finalFrame;
            }
        }

        public NativePairPlayback(Stage guard, Stage escape, Stage failure, int inputFrame,
            int contactFrame, int[] resultSeams, bool singleShot = false)
        {
            Guard = guard;
            Escape = escape;
            Failure = failure;
            InputFrame = inputFrame;
            ContactFrame = contactFrame;
            escapeFrames = resultSeams;
            SingleShot = singleShot;
        }

        public void SetStage(Stage stage, Transform player, Transform enemy, float blendSeconds)
        {
            current = stage;
            time = 0f;
            carryStep = 0f;
            blend = blendSeconds;
            previousPlayer = new Pose(player.position, player.rotation);
            previousEnemy = new Pose(enemy.position, enemy.rotation);
            Vector3 nativeOffset = Vector3.ProjectOnPlane(stage.enemy.sourceOrigin - stage.player.sourceOrigin, Vector3.up);
            Vector3 currentOffset = Vector3.ProjectOnPlane(enemy.position - player.position, Vector3.up);
            Quaternion facing = player.rotation;
            if (graph.IsValid())
            {
                facing = PlayerStart.rotation;
            }
            if (currentOffset.sqrMagnitude > 0.01f)
            {
                facing = Quaternion.AngleAxis(Vector3.SignedAngle(nativeOffset, currentOffset, Vector3.up), Vector3.up);
            }
            Vector3 center = (player.position + enemy.position) * 0.5f;
            Vector3 halfOffset = facing * (nativeOffset * 0.5f);
            PlayerStart = new Pose(center - halfOffset, facing);
            EnemyStart = new Pose(center + halfOffset, facing);
        }

        public void TranslateStage(Vector3 offset)
        {
            PlayerStart = new Pose(PlayerStart.position + offset, PlayerStart.rotation);
            EnemyStart = new Pose(EnemyStart.position + offset, EnemyStart.rotation);
        }

        public bool FitGroundFinisher(Transform player, Transform enemy, float maximumDistance, float maximumHeightDifference, Vector3 stageOffset, float yawOffset = 0f)
        {
            Quaternion facing = enemy.rotation;
            Vector3 observed = ChapterOneRootActor.ReadGroundBodyRight(enemy);
            Vector3 source = Vector3.ProjectOnPlane(current.enemy.sourceRight, Vector3.up);
            if (observed.sqrMagnitude > .001f && source.sqrMagnitude > .001f)
            {
                facing = Quaternion.AngleAxis(Vector3.SignedAngle(source, observed, Vector3.up), Vector3.up);
            }
            facing = Quaternion.AngleAxis(yawOffset, Vector3.up) * facing;
            Vector3 offset = facing * (current.player.sourceOrigin - current.enemy.sourceOrigin);
            Vector3 enemyTarget = enemy.position + stageOffset;
            Vector3 playerTarget = enemyTarget + offset;
            EnemyStart = new Pose(enemyTarget, facing);
            PlayerStart = new Pose(playerTarget, facing);

            float distance = Vector3.ProjectOnPlane(playerTarget - player.position, Vector3.up).magnitude;
            if (Mathf.Abs(playerTarget.y - player.position.y) > maximumHeightDifference || distance > maximumDistance)
            {
                return false;
            }

            blend = Mathf.Max(SawPairBlend, Mathf.Min(distance / 5f, ContactFrame / FrameRate * .75f));
            return true;
        }

        public void BeginGraph(Animator player, Animator enemy)
        {
            graph = PlayableGraph.Create("原版配对动画");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playerOutput.Bind(graph, player, current.player.clip);
            enemyOutput.Bind(graph, enemy, current.enemy.clip);
            graph.Play();
        }

        public void BeginRearPush(AnimationClip stagger, Transform player, Transform enemy)
        {
            rearPush = true;
            isResult = true;
            previousPlayer = new Pose(player.position, player.rotation);
            previousEnemy = new Pose(enemy.position, enemy.rotation);
            rearPushDuration = .85f;
            if (stagger != null)
            {
                rearPushDuration = Mathf.Min(stagger.length, rearPushDuration);
                playerOutput.BindResult(graph, stagger);
            }
            else
            {
                playerOutput.BindResult(graph, Escape.player.clip);
            }
            enemyReleaseStart = Mathf.Max(0f, Guard.enemy.clip.length - rearPushDuration);
            enemyOutput.BindResult(graph, Guard.enemy.clip);
            HoldEnemyResultPose = false;
            time = 0f;
            carryStep = 0f;
            blend = .16f;
        }

        public void ContinueStage(Stage stage)
        {
            current = stage;
            isResult = true;
            continuedGuard = true;
        }

        public void BeginResult()
        {
            isResult = true;
            playerOutput.BindResult(graph, current.player.clip);
            enemyOutput.BindResult(graph, current.enemy.clip);
        }

        public void BeginPlayerRecovery(ChapterOnePairMotion.Track track, Transform player, Transform enemy)
        {
            recoveryEnemyTime = time;
            previousPlayer = new Pose(player.position, player.rotation);
            previousEnemy = new Pose(enemy.position, enemy.rotation);
            Pose first = track.Sample(0f);
            Quaternion facing = player.rotation * Quaternion.Inverse(first.rotation);
            PlayerStart = new Pose(player.position - facing * first.position, facing);
            current = new Stage(track, current.enemy, Mathf.RoundToInt(track.clip.length * track.clip.frameRate));
            playerOutput.ContinueWithResult(graph, track.clip);
            IsPlayerRecovering = true;
            time = 0f;
            carryStep = 0f;
            blend = SawPairBlend;
        }

        public void QueueEscape()
        {
            if (EscapeQueued)
            {
                return;
            }
            foreach (int frame in escapeFrames)
            {
                if (frame >= SourceFrame)
                {
                    escapeFrame = frame;
                    return;
                }
            }
            escapeFrame = Mathf.Min(Guard.finalFrame, Mathf.CeilToInt(SourceFrame + 1f));
        }

        public void Step(float delta)
        {
            float available = Mathf.Max(0f, delta) + carryStep;
            float advance = available;
            float next = Mathf.Min(time + advance, EndFrame / FrameRate);
            float contact = ContactFrame / FrameRate;
            // 双方在同一帧停到接触标记，未消费的时间留给下一次步进。
            if ((SingleShot || current.player == Failure.player) && !ContactSent && time < contact && next >= contact)
            {
                advance = contact - time;
                next = contact;
            }
            carryStep = Mathf.Max(0f, available - advance);
            time = next;
        }

        private float BlendWeight => Mathf.SmoothStep(0f, 1f, time / blend);

        public void ReadPoses(out Pose player, out Pose enemy) => ReadPosesAt(time, out player, out enemy);

        public void HoldDownedPlayerAtFrame(int frame)
        {
            playerDeathTime = frame / FrameRate;
        }

        private float ReadPlayerPoseTime(float seconds)
        {
            if (playerDeathTime >= 0f)
            {
                return Mathf.Min(seconds, playerDeathTime);
            }
            return seconds;
        }

        public void ReadPosesAt(float seconds, out Pose player, out Pose enemy)
        {
            if (rearPush)
            {
                float weightPush = Mathf.SmoothStep(0f, 1f, seconds / .58f);
                player = previousPlayer;
                enemy = previousEnemy;
                player.position += previousPlayer.rotation * Vector3.forward * (.55f * weightPush);
                enemy.position -= previousEnemy.rotation * Vector3.forward * (.12f * weightPush);
                return;
            }
            float weight = Mathf.SmoothStep(0f, 1f, seconds / blend);
            player = BlendRoot(PlayerStart, previousPlayer, current.player.Sample(ReadPlayerPoseTime(seconds)), weight);
            if (IsPlayerRecovering)
            {
                enemy = previousEnemy;
                return;
            }
            enemy = BlendRoot(EnemyStart, previousEnemy, current.enemy.Sample(seconds), weight);
        }

        private static Pose BlendRoot(Pose start, Pose previous, Pose sample, float weight)
        {
            return new Pose(Vector3.Lerp(previous.position, start.position + start.rotation * sample.position, weight),
                Quaternion.Slerp(previous.rotation, start.rotation * sample.rotation, weight));
        }

        public void Present()
        {
            float outputWeight = BlendWeight;
            if (SingleShot && ContactSent)
            {
                outputWeight = Mathf.SmoothStep(0f, 1f, (EndFrame / FrameRate - time) / SawPairBlend);
            }
            if (isResult)
            {
                outputWeight = 1f;
                if (rearPush || current.player == Escape.player || IsPlayerRecovering)
                {
                    outputWeight = Mathf.SmoothStep(0f, 1f, (EndFrame / FrameRate - time) / SawPairBlend);
                }
            }
            presentationWeight = outputWeight;
            playerOutput.Present(ReadPlayerPoseTime(time), BlendWeight, outputWeight, isResult && !continuedGuard);
            float enemyWeight = outputWeight;
            if (HoldEnemyResultPose && (isResult || SingleShot && ContactSent))
            {
                enemyWeight = 1f;
            }
            if (IsPlayerRecovering)
            {
                enemyOutput.Present(recoveryEnemyTime, 1f, 1f, true);
            }
            else
            {
                float enemyTime = time;
                if (rearPush)
                {
                    enemyTime += enemyReleaseStart;
                }
                enemyOutput.Present(enemyTime, BlendWeight, enemyWeight, isResult && !continuedGuard);
            }
        }

        public void HoldPlayerDeathPose()
        {
            if (graph.IsValid())
            {
                playerOutput.Present(ReadPlayerPoseTime(time), 1f, 1f, isResult && !continuedGuard);
                enemyOutput.SetWeight(0f);
                enemyOutput.RestoreAnimator();
            }
        }

        public bool BeginFade()
        {
            if (!graph.IsValid() || presentationWeight <= 0f)
            {
                return false;
            }

            // 根位移、物理步与输入立即交还，图仅保留最后的可见姿势做淡出。
            playerOutput.RestoreAnimator();
            enemyOutput.RestoreAnimator();
            return true;
        }

        public bool AdvanceFade(float delta)
        {
            if (!graph.IsValid())
            {
                return true;
            }

            fadeElapsed += Mathf.Max(0f, delta);
            float weight = presentationWeight * (1f - Mathf.SmoothStep(0f, 1f, fadeElapsed / SawPairBlend));
            playerOutput.SetWeight(weight);
            enemyOutput.SetWeight(weight);
            return fadeElapsed >= SawPairBlend;
        }

        public void Dispose()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
                playerOutput.RestoreAnimator();
                enemyOutput.RestoreAnimator();
            }
        }

        private sealed class PairOutput
        {
            private AnimationClipPlayable guard;
            private AnimationClipPlayable result;
            private AnimationMixerPlayable mixer;
            private AnimationPlayableOutput output;
            private Animator animator;
            private AnimatorUpdateMode previousUpdateMode;
            private bool updateModeOverridden;

            public void Bind(PlayableGraph graph, Animator target, AnimationClip clip)
            {
                animator = target;
                previousUpdateMode = animator.updateMode;
                animator.updateMode = AnimatorUpdateMode.Normal;
                updateModeOverridden = true;
                guard = Create(graph, clip);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(guard, 0, mixer, 0);
                mixer.SetInputWeight(0, 1f);
                output = AnimationPlayableOutput.Create(graph, "配对姿势", animator);
                output.SetSourcePlayable(mixer);
                output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
                output.SetSortingOrder(100);
                output.SetWeight(0f);
            }

            public void RestoreAnimator()
            {
                if (updateModeOverridden && animator != null)
                {
                    animator.updateMode = previousUpdateMode;
                }
                updateModeOverridden = false;
            }

            public void SetWeight(float weight)
            {
                if (animator != null)
                {
                    output.SetWeight(weight);
                }
            }

            public void BindResult(PlayableGraph graph, AnimationClip clip)
            {
                result = Create(graph, clip);
                graph.Connect(result, 0, mixer, 1);
            }

            public void ContinueWithResult(PlayableGraph graph, AnimationClip clip)
            {
                graph.Disconnect(mixer, 0);
                graph.Disconnect(mixer, 1);
                graph.DestroyPlayable(guard);
                guard = result;
                graph.Connect(guard, 0, mixer, 0);
                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 0f);
                BindResult(graph, clip);
            }

            private static AnimationClipPlayable Create(PlayableGraph graph, AnimationClip clip)
            {
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);
                return playable;
            }

            public void Present(float time, float blend, float weight, bool playingResult)
            {
                output.SetWeight(weight);
                if (playingResult)
                {
                    result.SetTime(time);
                    mixer.SetInputWeight(0, 1f - blend);
                    mixer.SetInputWeight(1, blend);
                }
                else
                {
                    guard.SetTime(time);
                }
            }
        }
    }
}
