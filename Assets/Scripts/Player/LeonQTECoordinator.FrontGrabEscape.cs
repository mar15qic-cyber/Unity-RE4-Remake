using RE4.ChapterOne;
using UnityEngine;

public partial class LeonQTECoordinator
{
    private enum FrontGrabChoice { None, Manual, Knife }

    private const float FrontGrabManualCost = 5f;
    private const int FrontGrabManualContactFrame = 22;
    private const int FrontGrabKnifeCameraSwitchFrame = 120;
    private const int FrontGrabKnifeMoveCancelFrame = 140;
    private FrontGrabChoice frontGrabChoice;
    private int frontGrabChoiceFrame;
    private bool playingFrontGrabStab;
    private bool committingFrontGrabKnife;
    private bool frontGrabMotionsChecked;
    private LeonQTEConfig.FrontGrabKnifeMotion frontGrabMotion;
    private NativePairPlayback.Stage frontGrabStage;
    private Collider frontGrabChest;
    private Transform cachedLeftShoulder;
    private Transform cachedRightShoulder;
    private bool cachedShouldersResolved;

    internal bool IsFrontGrabActive => IsActive && activeKind == QTEKind.FrontGrab;

    internal bool ShouldReleaseFrontGrabStabCamera()
    {
        if (!playingFrontGrabStab || nativePair == null)
        {
            return false;
        }
        return nativePair.SourceFrame >= FrontGrabKnifeCameraSwitchFrame - 0.001f;
    }

    private bool FrontGrabInputOpen => IsFrontGrabActive && phase == QTEPhase.WaitingForInput
        && remainingTime > 0f && !cleanupRunning && (frontGrabChoice == FrontGrabChoice.None
            || frontGrabChoice == FrontGrabChoice.Manual && frontGrabChoiceFrame == Time.frameCount);

    internal bool TryReadFrontGrabPrompt(out float progress, out bool urgent, out GridItem knife, out Transform target)
    {
        progress = 0f;
        urgent = false;
        knife = null;
        target = null;
        if (!FrontGrabInputOpen || frontGrabChoice != FrontGrabChoice.None || participant == null || !participant.IsAlive)
        {
            return false;
        }
        progress = Mathf.Clamp01(pressCount / (float)Mathf.Max(1, config.requiredPresses));
        urgent = remainingTime <= 1f;
        target = participant.Root;
        if (inputBridge != null && inputBridge.HasKnifeEscapeBinding)
        {
            knife = ReadFrontGrabKnife();
        }
        return true;
    }

    internal bool TryReadEscapeRingPrompt(out float progress, out bool urgent, out string text)
    {
        progress = 0f;
        urgent = false;
        text = string.Empty;
        if (!IsActive || phase != QTEPhase.WaitingForInput || remainingTime <= 0f || cleanupRunning)
        {
            return false;
        }
        if (activeKind == QTEKind.FrontGrab)
        {
            if (frontGrabChoice != FrontGrabChoice.None)
            {
                return false;
            }
        }
        progress = Mathf.Clamp01(pressCount / (float)Mathf.Max(1, config.requiredPresses));
        urgent = remainingTime <= 1f;
        text = "挣脱";
        return true;
    }

    private GridItem ReadFrontGrabKnife()
    {
        if (nativePair == null || combat == null || combat.knifeModule == null
            || participant is not VillagerQTEParticipant victim || !victim.IsAlive
            || victim.controller == null || victim.controller.combatModule == null)
        {
            return null;
        }
        EnsureFrontGrabKnifeMotion(victim);
        if (frontGrabMotion == null || frontGrabChest == null || !frontGrabChest.enabled)
        {
            return null;
        }
        var knife = combat.knifeModule.SelectFrontGrabEscapeKnife();
        if (!combat.knifeModule.TryReadFrontGrabKnifeDamage(knife, out _))
        {
            return null;
        }
        return knife;
    }

    private void EnsureFrontGrabKnifeMotion(VillagerQTEParticipant victim)
    {
        if (frontGrabMotionsChecked)
        {
            return;
        }
        frontGrabMotionsChecked = true;
        if (config.frontGrabKnifeMotions == null || config.frontGrabKnifeMotions.Length == 0)
        {
            return;
        }
        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }
        if (weaponVisualController == null || weaponVisualController.nativePairKnife == null)
        {
            return;
        }
        var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
        if (bank == null)
        {
            return;
        }
        var enemyController = victim.Animator.runtimeAnimatorController;
        foreach (var motion in config.frontGrabKnifeMotions)
        {
            if (motion == null || motion.enemyController == null || motion.enemyController != enemyController
                || motion.knifeShowFrame < 0 || motion.contactFrame <= motion.knifeShowFrame
                || motion.knifeHideFrame <= motion.contactFrame || motion.endFrame < motion.knifeHideFrame
                || !bank.TryGetTrack(motion.playerClip, out var playerTrack)
                || !bank.TryGetTrack(motion.enemyClip, out var enemyTrack))
            {
                continue;
            }
            var stage = new NativePairPlayback.Stage(playerTrack, enemyTrack, motion.endFrame);
            if (!stage.IsValid)
            {
                continue;
            }
            foreach (var hitbox in victim.controller.GetComponentsInChildren<EnemyHitbox>())
            {
                if (hitbox.hitboxType == HitboxType.Chest && hitbox.Owner == victim.controller
                    && hitbox.TryGetComponent(out Collider collider) && collider.enabled)
                {
                    frontGrabStage = stage;
                    frontGrabMotion = motion;
                    frontGrabChest = collider;
                    return;
                }
            }
        }
    }

    internal bool SubmitFrontGrabKnife(int session)
    {
        if (!FrontGrabInputOpen || session != activeSessionId)
        {
            return false;
        }
        GridItem knife = ReadFrontGrabKnife();
        if (knife == null)
        {
            return false;
        }
        pairedKnife = knife;
        frontGrabChoice = FrontGrabChoice.Knife;
        frontGrabChoiceFrame = Time.frameCount;
        return true;
    }

    private void QueueFrontGrabManualEscape()
    {
        if (frontGrabChoice == FrontGrabChoice.None)
        {
            frontGrabChoice = FrontGrabChoice.Manual;
            frontGrabChoiceFrame = Time.frameCount;
        }
    }

    // 等本帧全部输入抵达后定结果。同帧左键可替换第八次 F。
    private bool UpdateFrontGrabChoice()
    {
        if (inputBridge != null)
        {
            inputBridge.ObserveKnifeEscapeRelease();
        }
        if (phase != QTEPhase.WaitingForInput)
        {
            return false;
        }
        if (frontGrabChoice != FrontGrabChoice.None && frontGrabChoiceFrame == Time.frameCount)
        {
            return true;
        }
        if (frontGrabChoice == FrontGrabChoice.Manual)
        {
            if (nativePair == null)
            {
                DecideResult(true);
                return true;
            }
            nativePair.QueueEscape();
        }
        else if (frontGrabChoice == FrontGrabChoice.Knife)
        {
            BeginFrontGrabKnifeResult();
            return true;
        }
        return false;
    }

    private void BeginFrontGrabKnifeResult()
    {
        int session = activeSessionId;
        if (nativePair == null || frontGrabMotion == null || !participant.IsAlive
            || !combat.knifeModule.TryReadFrontGrabKnifeDamage(pairedKnife, out _))
        {
            Cancel(session, QTECancelReason.PlayerUnavailable);
            return;
        }

        if (!TryFitStandingPairStage(nativePair, participant, frontGrabStage, SawPairBlend))
        {
            // 狭窄空间或后方有其他敌人时沿用手动挣脱的同款回退，由播放期碰撞限步承接，避免按左键反击直接被空间校验取消。
            nativePair.SetStage(frontGrabStage, transform, participant.Root, SawPairBlend);
        }

        weaponVisualController.SetKnifeAppearance(pairedKnife);
        if (!weaponVisualController.BeginNativePairKnife())
        {
            Cancel(session, QTECancelReason.AnimationInterrupted);
            return;
        }
        assassinationKnifeVisualActive = true;
        weaponVisualController.nativePairKnife.gameObject.SetActive(false);
        playingFrontGrabStab = true;
        nativePair.ContactSent = false;
        nativePair.SuccessContactFrame = frontGrabMotion.contactFrame;
        nativePair.BeginResult();
        pendingResult = QTEResult.Success;
        StopQTEStruggleLoop();
        ChangePhase(QTEPhase.SuccessResult);
    }

    private void PresentFrontGrabEscape()
    {
        if (!IsFrontGrabActive || phase != QTEPhase.SuccessResult || Time.deltaTime <= 0f)
        {
            return;
        }
        if (frontGrabChoice == FrontGrabChoice.Manual && nativePair != null && !nativePair.ContactSent
            && nativePair.SourceFrame >= FrontGrabManualContactFrame - .001f)
        {
            nativePair.ContactSent = true;
            if (controller != null && controller.Health != null && !controller.Health.IsDead)
            {
                float cost = Mathf.Min(FrontGrabManualCost, controller.Health.CurrentHealth - 1f);
                if (cost > 0f)
                {
                    controller.Health.TakeDamage(cost);
                }
            }
            return;
        }
        if (!playingFrontGrabStab || nativePair == null)
        {
            return;
        }
        var knife = weaponVisualController.nativePairKnife;
        float frame = nativePair.SourceFrame;
        knife.gameObject.SetActive(frame >= frontGrabMotion.knifeShowFrame - .001f && frame < frontGrabMotion.knifeHideFrame);
        if (!nativePair.ContactSent && frame >= frontGrabMotion.contactFrame - .001f)
        {
            bool allowFallbackContact = frame >= frontGrabMotion.contactFrame + 6f - .001f;
            CommitFrontGrabKnifeContact(allowFallbackContact);
        }
        if (nativePair != null && nativePair.ContactSent && frame >= FrontGrabKnifeMoveCancelFrame - .001f && inputBridge != null)
        {
            bool hasMoveIntent = inputBridge.ReadRawMoveInput().sqrMagnitude > 0.04f;
            bool hasAimIntent = inputBridge.IsRawAimPressed();
            if (hasMoveIntent || hasAimIntent)
            {
                CompleteNativePair(true);
            }
        }
    }

    private bool TryReadPlayerVisualForward(out Vector3 visualForward)
    {
        visualForward = Vector3.zero;
        if (!cachedShouldersResolved)
        {
            cachedShouldersResolved = true;
            Transform searchRoot = transform;
            if (playerAnimator != null)
            {
                searchRoot = playerAnimator.transform;
            }
            foreach (Transform bone in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (cachedLeftShoulder == null && bone.name == "arm left shoulder")
                {
                    cachedLeftShoulder = bone;
                }
                else if (cachedRightShoulder == null && bone.name == "arm right shoulder")
                {
                    cachedRightShoulder = bone;
                }
            }
        }
        if (cachedLeftShoulder != null && cachedRightShoulder != null)
        {
            Vector3 shoulderRight = Vector3.ProjectOnPlane(cachedRightShoulder.position - cachedLeftShoulder.position, Vector3.up);
            if (shoulderRight.sqrMagnitude > 0.0004f)
            {
                visualForward = Vector3.Cross(shoulderRight.normalized, Vector3.up).normalized;
                return true;
            }
        }
        if (participant != null && participant.Root != null)
        {
            Vector3 toEnemy = Vector3.ProjectOnPlane(participant.Root.position - transform.position, Vector3.up);
            if (toEnemy.sqrMagnitude > 0.0004f)
            {
                visualForward = toEnemy.normalized;
                return true;
            }
        }
        return false;
    }

    private void CommitFrontGrabKnifeContact(bool allowFallbackContact)
    {
        int session = activeSessionId;
        if (participant is not VillagerQTEParticipant victim || victim.ActiveSessionId != session || !victim.IsAlive
            || !combat.knifeModule.TryReadFrontGrabKnifeDamage(pairedKnife, out float damage))
        {
            Cancel(session, QTECancelReason.AnimationInterrupted);
            return;
        }

        Vector3 point = default;
        bool hasBlade = weaponVisualController.TryReadNativePairBlade(out var bladeBase, out var bladeTip);
        bool hasExactContact = hasBlade && TryReadFrontGrabChestContact(bladeBase, bladeTip, out point);
        if (!hasExactContact)
        {
            if (!allowFallbackContact)
            {
                return;
            }

            if (frontGrabChest != null && frontGrabChest.enabled)
            {
                if (hasBlade)
                {
                    point = frontGrabChest.ClosestPoint(bladeTip);
                }
                else
                {
                    point = frontGrabChest.bounds.center;
                }
            }
            else if (hasBlade)
            {
                point = bladeTip;
            }
            else
            {
                point = victim.Root.position + Vector3.up * 1.25f;
            }
        }

        nativePair.ContactSent = true;
        if (!combat.knifeModule.TryCommitFinisherCost(pairedKnife))
        {
            Cancel(session, QTECancelReason.PlayerUnavailable);
            return;
        }
        if (session != activeSessionId || !victim.IsAlive)
        {
            return;
        }

        // 先预留配对死亡姿态，避免致命一击在接触帧立刻切入普通死亡状态机并截断后半段退出动画。
        bool preparedDeathPose = victim.PrepareAssassinationDeathPose(session);
        committingFrontGrabKnife = true;
        try
        {
            victim.controller.combatModule.CommitPairedKnifeDamage(damage, point);
        }
        finally
        {
            committingFrontGrabKnife = false;
        }
        if (preparedDeathPose && victim.IsAlive)
        {
            victim.CancelAssassinationDeathPose(session);
        }
        ChapterOneEffects.TryPlay(ChapterOneEffect.StabBlood, point, -transform.forward);
        PlayKnifePairContactAudio(point);
    }

    private bool TryReadFrontGrabChestContact(Vector3 bladeBase, Vector3 bladeTip, out Vector3 point)
    {
        point = default;
        Vector3 line = bladeTip - bladeBase;
        if (frontGrabChest == null || !frontGrabChest.enabled || line.sqrMagnitude < .0001f)
        {
            return false;
        }
        if (frontGrabChest.Raycast(new Ray(bladeBase, line.normalized), out var hit, line.magnitude + .015f))
        {
            point = hit.point;
            return true;
        }
        point = frontGrabChest.ClosestPoint(bladeTip);
        if ((point - bladeTip).sqrMagnitude <= .025f * .025f)
        {
            return true;
        }
        // 0021 的刀刃接触头颈，复用既有配对动作的头颈接触验证。
        if (HasGroundFinisherBladeContact(bladeBase, bladeTip))
        {
            point = bladeTip;
            return true;
        }
        return false;
    }

    private void ResetFrontGrabEscape()
    {
        frontGrabMotion = null;
        frontGrabStage = default;
        frontGrabChest = null;
        frontGrabMotionsChecked = false;
        frontGrabChoice = FrontGrabChoice.None;
        frontGrabChoiceFrame = 0;
        playingFrontGrabStab = false;
        committingFrontGrabKnife = false;
    }

    internal bool TryReadFrontGrabStabCamera(out float progress)
    {
        progress = 0f;
        if (!playingFrontGrabStab || nativePair == null)
        {
            return false;
        }
        progress = Mathf.Clamp01(nativePair.SourceFrame / Mathf.Max(1, frontGrabMotion.endFrame));
        return true;
    }
}
