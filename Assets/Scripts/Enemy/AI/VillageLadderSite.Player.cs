using System.Collections.Generic;
using GameArchitecture;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public sealed partial class VillageLadderSite
{
    private enum PlayerPhase { Mount, Wait, Step, Exit, Fall, Land }

    private bool candidateClimb;
    private LeonController climbingPlayer;
    private LeonAnimationActionCoordinator climbActions;
    private LeonActionMotionDriver climbMotion;
    private LeonInputBridge climbInput;
    private LeonWeaponVisualController climbWeapon;
    private Rigidbody climbBody;
    private CapsuleCollider climbCapsule;
    private RE4RThirdPersonCamera climbCamera;
    private MotionControlToken climbToken;
    private int climbTransaction;
    private bool restoreGravity;
    private bool climbOwnsWindow;
    private bool dropCameraActive;
    private bool usedWindow;
    private bool playerExitAtFoot;
    private bool towerJumpDeparture;
    private Vector3 towerJumpTarget;
    private bool ignoredFixedLadder;
    private PlayerPhase playerPhase;
    private PlayableGraph climbGraph;
    private AnimationPlayableOutput climbOutput;
    private AnimationClipPlayable climbPlayable;
    private AnimationMixerPlayable climbWindowBlend;
    private Transform climbHead;
    private Animator climbAnimator;
    private AnimatorUpdateMode originalClimbUpdateMode;
    private Vector3 previousWindowHead;
    private bool playerWindowExit;
    private bool climbControlsBody;
    private bool bodyWasKinematic;
    private RigidbodyInterpolation climbOriginalInterpolation;
    private readonly int[] playerSupportRungs = { -1, -1, -1, -1 };
    private readonly float[] playerSupportAcross = new float[4];
    private readonly Transform[] playerMiddleKnuckles = new Transform[2];
    private readonly Transform[] playerToeBones = new Transform[2];
    private Vector3 windowEntryPalm;
    private const int WindowLeadHand = 1;
    private VillageLadderConfiguration.PlayerClips climbClips;
    private Vector3 playerPhaseStart;
    private Vector3 playerPhaseEnd;
    private Quaternion playerStartFacing;
    private float playerPhaseElapsed;
    private float playerPhaseDuration;
    private float lowestClimbHeight;
    private float playerClimbHeight;
    private bool leftStep;
    private bool playerSteppingDown;
    private readonly List<Collider> ignoredWindowColliders = new();
    private readonly Transform[,] playerLimbBones = new Transform[4, 3];

    private bool CanBeginPlayerClimb(LeonController player)
    {
        var clips = ReadPlayerClips();
        if (!IsRouteOpen || clips == null || !clips.IsComplete || IsWindow && configuration.playerWindowExit == null ||
            activeEnemy != null || fixedClimbers.Count != 0 || !player.CanAct() ||
            player.IsJumping || player.IsCrouching ||
            GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        Vector3 offset = player.transform.position - Approach;
        if (Mathf.Abs(offset.y) > 0.55f || !HasSupport(player.transform.position))
        {
            return false;
        }

        offset.y = 0f;
        if (offset.sqrMagnitude > 1.15f * 1.15f ||
            Vector3.Dot(player.transform.position - Foot, Outward) < 0.05f)
        {
            return false;
        }

        return player.TryGetComponent<LeonAnimationActionCoordinator>(out var actions) &&
            actions.CanRequestLadderPush();
    }

    private VillageLadderConfiguration.PlayerClips ReadPlayerClips()
    {
        if (IsFixed)
        {
            return configuration.playerFixed;
        }

        return configuration.playerLeaning;
    }

    private void BeginPlayerClimb(LeonController player)
    {
        if (!BeginPlayerTraversal(player, false))
        {
            return;
        }

        climbClips = ReadPlayerClips();
        if (window != null)
        {
            climbOwnsWindow = window.TryReservePassage(this);
            if (!climbOwnsWindow)
            {
                EndPlayerTraversal(true);
                return;
            }
        }

        var animator = player.GetComponent<Animator>();
        CachePlayerLimb(animator, 0, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        CachePlayerLimb(animator, 1, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        CachePlayerLimb(animator, 2, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        CachePlayerLimb(animator, 3, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        climbHead = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.Head);
        playerMiddleKnuckles[0] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftMiddleProximal);
        playerMiddleKnuckles[1] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightMiddleProximal);
        playerToeBones[0] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftToes);
        playerToeBones[1] = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightToes);
        for (int limb = 0; limb < playerSupportRungs.Length; limb++)
        {
            playerSupportRungs[limb] = -1;
        }

        IgnoreRails(climbCapsule);
        BeginClimbBodyControl();
        climbBody.useGravity = false;
        if (IsFixed)
        {
            IgnoreFixedLadderForPlayer();
        }
        lowestClimbHeight = Foot.y + 0.22f;
        playerClimbHeight = lowestClimbHeight;
        leftStep = true;
        StartPlayerPhase(PlayerPhase.Mount, climbClips.mount, ReadClimbPosition(playerClimbHeight));
    }

    internal bool BeginTowerDrop(LeonController player)
    {
        if (configuration.playerTowerFall == null || configuration.playerTowerLand == null ||
            !BeginPlayerTraversal(player, true, true))
        {
            return false;
        }

        StartPlayerFall(configuration.playerTowerFall);
        return true;
    }

    private bool BeginPlayerTraversal(LeonController player, bool falling, bool interruptWeapon = false)
    {
        var actions = player.GetComponent<LeonAnimationActionCoordinator>();
        var motion = player.GetComponent<LeonActionMotionDriver>();
        var animator = player.GetComponent<Animator>();
        var body = player.GetComponent<Rigidbody>();
        var capsule = player.GetComponent<CapsuleCollider>();
        if (climbingPlayer != null || actions == null || motion == null || animator == null ||
            body == null || capsule == null || !actions.RequestLadderTraversal(interruptWeapon))
        {
            return false;
        }

        climbingPlayer = player;
        climbActions = actions;
        climbMotion = motion;
        climbBody = body;
        climbCapsule = capsule;
        climbInput = player.GetComponent<LeonInputBridge>();
        climbWeapon = player.GetComponent<LeonWeaponVisualController>();
        climbTransaction = actions.CurrentFullBodyTransactionId;
        climbToken = motion.AuthorizeMotion(MotionSource.Traversal, climbTransaction);
        restoreGravity = body.useGravity;
        usedWindow = false;
        if (!motion.TokenValid(climbToken))
        {
            EndPlayerTraversal(true);
            return false;
        }

        actions.OnTraversalActionInterrupted += HandlePlayerTraversalInterrupted;
        player.ResetMovementInputForWindow();
        player.SetFreeLooking(false);
        player.ChangeState(LeonState.Traversing);
        if (!falling)
        {
            motion.CommitSpeed(climbToken, Vector3.zero);
        }
        if (climbWeapon != null)
        {
            climbWeapon.SetTraversalWeaponVisible(false);
        }

        // 攀梯图与帧末接触求解采用同一次渲染帧的动画姿势。
        climbAnimator = animator;
        originalClimbUpdateMode = animator.updateMode;
        animator.updateMode = AnimatorUpdateMode.Normal;
        climbGraph = PlayableGraph.Create("里昂梯子通行");
        climbGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        climbOutput = AnimationPlayableOutput.Create(climbGraph, "攀梯全身动作", animator);
        climbOutput.SetSortingOrder(100);
        climbGraph.Play();
        return true;
    }

    private void StartPlayerPhase(PlayerPhase phase, AnimationClip clip, Vector3 end, bool windowExit = false)
    {
        var oldPlayable = climbPlayable;
        if (climbWindowBlend.IsValid())
        {
            var previous = climbWindowBlend.GetInput(0);
            climbGraph.DestroyPlayable(climbWindowBlend);
            climbGraph.DestroyPlayable(previous);
            climbWindowBlend = default;
        }

        climbPlayable = AnimationClipPlayable.Create(climbGraph, clip);
        climbPlayable.SetApplyFootIK(false);
        climbPlayable.SetApplyPlayableIK(false);
        if (phase == PlayerPhase.Wait)
        {
            climbPlayable.SetSpeed(0f);
        }
        playerWindowExit = windowExit;
        if (windowExit && oldPlayable.IsValid())
        {
            oldPlayable.SetSpeed(0f);
            climbWindowBlend = AnimationMixerPlayable.Create(climbGraph, 2);
            climbGraph.Connect(oldPlayable, 0, climbWindowBlend, 0);
            climbGraph.Connect(climbPlayable, 0, climbWindowBlend, 1);
            climbWindowBlend.SetInputWeight(0, 1f);
            climbOutput.SetSourcePlayable(climbWindowBlend);
        }
        else
        {
            climbOutput.SetSourcePlayable(climbPlayable);
            if (oldPlayable.IsValid())
            {
                climbGraph.DestroyPlayable(oldPlayable);
            }
        }

        playerPhase = phase;
        playerPhaseElapsed = 0f;
        playerPhaseDuration = Mathf.Max(clip.length, 0.02f);
        playerPhaseStart = climbBody.position;
        playerStartFacing = climbBody.rotation;
        playerPhaseEnd = end;
    }

    private float ReadPlayerClimbTime()
    {
        return Mathf.Clamp((float)climbPlayable.GetTime(), 0f, playerPhaseDuration);
    }

    private void IgnoreFixedLadderForPlayer()
    {
        if (tower.ladderCollider != null && !Physics.GetIgnoreCollision(climbCapsule, tower.ladderCollider))
        {
            Physics.IgnoreCollision(climbCapsule, tower.ladderCollider, true);
            ignoredFixedLadder = true;
        }
        foreach (var rail in tower.ladderFrameColliders)
        {
            if (rail != null && !Physics.GetIgnoreCollision(climbCapsule, rail))
            {
                Physics.IgnoreCollision(climbCapsule, rail, true);
                ignoredWindowColliders.Add(rail);
            }
        }
    }

    private void StartPlayerFall(AnimationClip clip)
    {
        ReleaseClimbBodyControl();
        if (clip == null)
        {
            EndPlayerTraversal(true);
            return;
        }

        climbBody.useGravity = true;
        climbCamera = FindFirstObjectByType<RE4RThirdPersonCamera>();
        if (climbCamera != null)
        {
            dropCameraActive = true;
            climbCamera.BeginDropCamera();
        }
        StartPlayerPhase(PlayerPhase.Fall, clip, climbBody.position);
    }

    private void CachePlayerLimb(Animator animator, int slot, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones tip)
    {
        playerLimbBones[slot, 0] = AnimatorBoneBindings.Resolve(animator, upper);
        playerLimbBones[slot, 1] = AnimatorBoneBindings.Resolve(animator, lower);
        playerLimbBones[slot, 2] = AnimatorBoneBindings.Resolve(animator, tip);
    }

    private void LateUpdate()
    {
        if (climbTransaction == 0 || !climbMotion.TokenValid(climbToken))
        {
            return;
        }
        if (playerWindowExit)
        {
            BreakWindowAtHeadContact();
            ApplyWindowSillSupport();
            return;
        }
        if (playerPhase != PlayerPhase.Wait && playerPhase != PlayerPhase.Step)
        {
            return;
        }

        float progress = Mathf.Clamp01(ReadPlayerClimbTime() / playerPhaseDuration);
        for (int i = 0; i < 4; i++)
        {
            var upper = playerLimbBones[i, 0];
            var lower = playerLimbBones[i, 1];
            var tip = playerLimbBones[i, 2];
            if (upper == null || lower == null || tip == null)
            {
                continue;
            }

            bool moving = playerPhase == PlayerPhase.Step && IsPlayerLimbChangingRungs(i);
            float grip = 1f;
            if (moving)
            {
                float contactPhase = .87f;
                if (i >= 2 && playerSteppingDown)
                {
                    // 下梯的摆动脚在片段尾部才到下一档，过早取点会仍选中上一档。
                    contactPhase = .92f;
                }
                grip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(contactPhase, 1f, progress));
                if (progress >= contactPhase && playerSupportRungs[i] < 0)
                {
                    RecordPlayerRung(i);
                }
            }
            if (playerSupportRungs[i] < 0)
            {
                if (moving)
                {
                    continue;
                }

                RecordPlayerRung(i);
            }

            Vector3 contact = ReadPlayerRungContact(i);
            if (i < 2)
            {
                ApplyLadderHandIK(i, contact, grip);
                continue;
            }

            ApplyLadderFootIK(i, contact, grip);
        }
    }

    private void FixedUpdate()
    {
        if (climbTransaction == 0)
        {
            return;
        }

        if (climbingPlayer == null || !climbMotion.TokenValid(climbToken) ||
            !climbActions.OwnsLadderAction(climbTransaction, FullBodyActionType.LadderTraversal))
        {
            EndPlayerTraversal(true);
            return;
        }

        playerPhaseElapsed = ReadPlayerClimbTime();
        if (climbWindowBlend.IsValid())
        {
            float blendWeight = Mathf.SmoothStep(0f, 1f, playerPhaseElapsed / .14f);
            climbWindowBlend.SetInputWeight(0, 1f - blendWeight);
            climbWindowBlend.SetInputWeight(1, blendWeight);
        }
        if (playerPhase == PlayerPhase.Fall)
        {
            TickPlayerFall();
            return;
        }

        if (playerPhase == PlayerPhase.Land)
        {
            if (playerPhaseElapsed >= playerPhaseDuration)
            {
                EndPlayerTraversal(false);
            }
            return;
        }

        if (State != LadderState.Ready)
        {
            StartPlayerFall(configuration.playerJumpDown);
            return;
        }

        if (playerPhase == PlayerPhase.Wait)
        {
            ReadPlayerNextStep();
            return;
        }

        float progress = Mathf.Clamp01(playerPhaseElapsed / playerPhaseDuration);
        float blend = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 next = Vector3.Lerp(playerPhaseStart, playerPhaseEnd, blend);
        if (playerPhase == PlayerPhase.Exit && !playerExitAtFoot)
        {
            if (playerWindowExit)
            {
                next = ReadPlayerWindowPosition(playerPhaseStart, progress);
            }
            else
            {
                Vector3 crest = Head + Outward * 0.24f + Vector3.up * 0.10f;
                next = Vector3.Lerp(playerPhaseStart, crest, Mathf.SmoothStep(0f, 1f, progress * 2f));
                if (progress >= 0.5f)
                {
                    next = Vector3.Lerp(crest, playerPhaseEnd, Mathf.SmoothStep(0f, 1f, progress * 2f - 1f));
                }
            }
        }

        Quaternion facing = Quaternion.Slerp(playerStartFacing, Facing, blend);
        climbMotion.CommitTraversalPose(climbToken, next, facing, climbControlsBody && progress < 1f);
        if (playerPhase == PlayerPhase.Step && progress >= .55f && TryBeginPlayerWindowExit(ReadPlayerLadderDirection()))
        {
            return;
        }
        if (progress < 1f)
        {
            return;
        }

        if (playerPhase == PlayerPhase.Exit)
        {
            EndPlayerTraversal(false);
            return;
        }

        if (playerPhase == PlayerPhase.Step)
        {
            bool movedLeftFoot = IsPlayerLimbChangingRungs(2);
            leftStep = !leftStep;
            AudioEventData tread = configuration.rightRungSound;
            if (movedLeftFoot)
            {
                tread = configuration.leftRungSound;
            }
            PlaySound(tread, next);
        }
        ReadPlayerNextStep();
    }

    private void RecordPlayerRung(int limb)
    {
        var tip = playerLimbBones[limb, 2];
        if (tip == null)
        {
            return;
        }

        Vector3 point = tip.position;
        if (limb < 2)
        {
            point += ReadPlayerPalmOffset(limb);
        }
        else
        {
            point += ReadPlayerFootOffset(limb);
        }

        if (ladderVisual != null)
        {
            Vector3 local = ladderVisual.InverseTransformPoint(point);
            playerSupportRungs[limb] = Mathf.Clamp(Mathf.RoundToInt((local.y - FirstRung) / RungSpacing), 0, LastRung);
            float sideLimit = .23f;
            if (limb < 2)
            {
                sideLimit = .19f;
            }
            playerSupportAcross[limb] = Mathf.Clamp(local.x, -sideLimit, sideLimit);
        }
        else
        {
            playerSupportRungs[limb] = ReadRungAtHeight(point.y);
            playerSupportAcross[limb] = Mathf.Clamp(Vector3.Dot(point - Foot, Across), -.23f, .23f);
        }
    }

    private bool IsPlayerLimbChangingRungs(int limb)
    {
        bool matchesClipSide = (limb % 2 == 0) == leftStep;
        // 原有斜梯和直梯片段里，上梯换同侧脚、对侧手，下梯交换这两个角色。
        if (limb < 2)
        {
            matchesClipSide = !matchesClipSide;
        }
        if (playerSteppingDown)
        {
            matchesClipSide = !matchesClipSide;
        }
        return matchesClipSide;
    }

    private Vector3 ReadPlayerRungContact(int limb)
    {
        int rung = playerSupportRungs[limb];
        int colliderIndex = rung + 2;
        if (rung >= 0 && colliderIndex < solidRails.Count && solidRails[colliderIndex] is BoxCollider rail)
        {
            Vector3 contact = rail.center;
            contact.x = playerSupportAcross[limb];
            float face = Mathf.Sign(Vector3.Dot(rail.transform.forward, Outward));
            contact.z += face * rail.size.z * .5f;
            if (limb < 2)
            {
                // 握点在横档外侧上沿，为手套厚度留出距离，让手指从上方扣过横档。
                contact.y += rail.size.y * .32f;
                contact.z += face * .014f;
            }
            else
            {
                contact.y += rail.size.y * .3f;
                contact.z -= face * .014f;
            }
            return rail.transform.TransformPoint(contact);
        }
        Vector3 expected = ReadRungPosition(rung, playerSupportAcross[limb], .04f);
        if (IsFixed && tower.ladderCollider != null)
        {
            var probe = new Ray(expected + Outward * .25f, -Outward);
            if (tower.ladderCollider.Raycast(probe, out var contact, .5f))
            {
                expected = contact.point + Outward * .014f;
                expected += Vector3.up * .018f;
            }
        }
        return expected;
    }

    private Vector3 ReadPlayerPalmOffset(int hand)
    {
        Transform wrist = playerLimbBones[hand, 2];
        Transform knuckle = playerMiddleKnuckles[hand];
        if (wrist == null || knuckle == null)
        {
            return Vector3.zero;
        }
        return (knuckle.position - wrist.position) * .65f;
    }

    private Vector3 ReadPlayerFootOffset(int limb)
    {
        Transform ankle = playerLimbBones[limb, 2];
        Transform toe = playerToeBones[limb - 2];
        if (ankle == null || toe == null)
        {
            return Vector3.zero;
        }
        return (toe.position - ankle.position) * .75f - Vector3.up * .025f;
    }

    private void ApplyLadderFootIK(int limb, Vector3 contact, float weight)
    {
        if (weight <= .001f)
        {
            return;
        }

        Transform thigh = playerLimbBones[limb, 0];
        Transform shin = playerLimbBones[limb, 1];
        Transform ankle = playerLimbBones[limb, 2];
        if (thigh == null || shin == null || ankle == null)
        {
            return;
        }

        // 用前脚掌承重，保留片段中脚踝的角度。
        Vector3 shift = Vector3.ClampMagnitude(contact - (ankle.position + ReadPlayerFootOffset(limb)), .23f) * weight;
        EnemyAnimationAdapter.FitLadderBoneContact(thigh, shin, ankle, ankle.position + shift);
    }

    private void ApplyLadderHandIK(int hand, Vector3 contact, float weight, bool onSill = false, float alignment = 1f)
    {
        if (weight <= .001f)
        {
            return;
        }

        Transform shoulder = playerLimbBones[hand, 0];
        Transform elbow = playerLimbBones[hand, 1];
        Transform wrist = playerLimbBones[hand, 2];
        if (shoulder == null || elbow == null || wrist == null)
        {
            return;
        }

        Vector3 palmOffset = ReadPlayerPalmOffset(hand);
        Quaternion wristCorrection = Quaternion.identity;
        if ((ladderVisual != null || IsFixed) && palmOffset.sqrMagnitude > .0001f)
        {
            // 保留片段里的手指动作。握住横档时，让手掌朝向横档的截面，避免擦进旁边的梯柱。
            Vector3 gripDirection = Vector3.ProjectOnPlane(palmOffset, Across);
            if (onSill)
            {
                gripDirection = Vector3.ProjectOnPlane(gripDirection, Vector3.up);
            }
            if (gripDirection.sqrMagnitude > .0001f)
            {
                wristCorrection = Quaternion.FromToRotation(palmOffset, gripDirection);
                wristCorrection = Quaternion.RotateTowards(Quaternion.identity, wristCorrection, 35f * weight * alignment);
            }
        }

        Vector3 wristTarget = contact - wristCorrection * palmOffset;
        Vector3 easedTarget = Vector3.Lerp(wrist.position, wristTarget, weight);
        EnemyAnimationAdapter.FitLadderBoneContact(shoulder, elbow, wrist, easedTarget,
            Vector3.zero, wristCorrection * wrist.rotation);
    }

    private float ReadPlayerLadderDirection()
    {
        if (climbInput == null)
        {
            return 0f;
        }
        return climbInput.ReadLadderVerticalInput();
    }

    private Vector3 ReadPlayerWindowPosition(Vector3 start, float phase)
    {
        // 按出梯片段的撑台、收膝和落脚阶段安排越窗。收膝前留在墙外，身体落下前越过窗台内沿。
        float cross = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.29f, .71f, phase));
        Vector3 position = Vector3.Lerp(start, Exit, cross);
        float sillHeight = window.OpeningCenterWorld.y - window.openingHeight * .5f;
        float liftHeight = Mathf.Min(Exit.y + .28f, sillHeight + .12f);
        if (phase < .5f)
        {
            position.y = Mathf.Lerp(start.y, liftHeight, Mathf.SmoothStep(0f, 1f, phase * 2f));
        }
        else
        {
            position.y = Mathf.Lerp(liftHeight, Exit.y, Mathf.SmoothStep(0f, 1f, phase * 2f - 1f));
        }

        float kneeClearance = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.37f, .46f, phase));
        kneeClearance *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.56f, .69f, phase));
        position.y += .09f * kneeClearance;
        return position;
    }

    private Vector3 ReadPlayerWindowHandContact()
    {
        Vector3 sill = window.OpeningCenterWorld - Vector3.up * (window.openingHeight * .5f - .035f);
        return sill + Across * .17f + Outward * .17f;
    }

    private bool TryBeginPlayerWindowExit(float direction)
    {
        if (direction <= .25f || !IsWindow || !climbOwnsWindow || climbHead == null
            || window.IsPassageReservedByOther(this) || climbBody.position.y < TopClimbHeight - .42f)
        {
            return false;
        }

        // 这一段出梯片段由右手撑台。先确认右手能触及窗台，再播放原有的收膝和翻入动作。
        var shoulder = playerLimbBones[WindowLeadHand, 0];
        var elbow = playerLimbBones[WindowLeadHand, 1];
        var wrist = playerLimbBones[WindowLeadHand, 2];
        if (shoulder == null || elbow == null || wrist == null)
        {
            return false;
        }

        float reach = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
        Vector3 palmOffset = ReadPlayerPalmOffset(WindowLeadHand);
        Vector3 wristContact = ReadPlayerWindowHandContact() - palmOffset;
        bool atLastRung = climbBody.position.y >= TopClimbHeight - .015f;
        if ((!atLastRung && Vector3.Distance(shoulder.position, wristContact) > reach - .015f)
            || !HasExitSupport(Exit) || !IsExitClear(climbCapsule, climbingPlayer.transform))
        {
            return false;
        }

        Vector3 start = climbBody.position;
        Vector3 previous = start;
        for (int segment = 1; segment <= 4; segment++)
        {
            Vector3 next = ReadPlayerWindowPosition(start, segment / 4f);
            if (!IsPlayerWindowSegmentClear(previous, next))
            {
                return false;
            }
            previous = next;
        }

        windowEntryPalm = wrist.position + palmOffset;
        if (playerSupportRungs[0] < 0)
        {
            RecordPlayerRung(0);
        }

        previousWindowHead = climbHead.position - Outward * .09f;
        StartPlayerPhase(PlayerPhase.Exit, configuration.playerWindowExit, Exit, true);
        return true;
    }

    private bool IsPlayerWindowSegmentClear(Vector3 start, Vector3 end)
    {
        Vector3 displacement = end - start;
        if (displacement.sqrMagnitude < .0001f)
        {
            return true;
        }

        foreach (var hit in Physics.CapsuleCastAll(start + Vector3.up * .22f, start + Vector3.up * .52f,
            .18f, displacement.normalized, displacement.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(climbingPlayer.transform) || solidRails.Contains(hit.collider)
                || window.ShouldIgnorePathCollider(hit.collider)
                || Physics.GetIgnoreLayerCollision(climbCapsule.gameObject.layer, hit.collider.gameObject.layer))
            {
                continue;
            }

            // 固定窗框使用该梯位已配好的通行轨迹。这里复核新增到轨迹上的活动物体。
            if (hit.collider.attachedRigidbody != null || hit.collider.GetComponentInParent<DestructibleWoodenBox>() != null
                || hit.collider.GetComponentInParent<VillagerController>() != null)
            {
                return false;
            }
        }
        return true;
    }

    private void ApplyWindowSillSupport()
    {
        float progress = Mathf.Clamp01(ReadPlayerClimbTime() / playerPhaseDuration);
        for (int limb = 0; limb < playerSupportRungs.Length; limb++)
        {
            var tip = playerLimbBones[limb, 2];
            if (tip == null)
            {
                continue;
            }

            Vector3 contact;
            float weight;
            if (limb == WindowLeadHand)
            {
                float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .14f, progress));
                contact = Vector3.Lerp(windowEntryPalm, ReadPlayerWindowHandContact(), reach);
                weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.38f, .55f, progress));
            }
            else if (playerSupportRungs[limb] >= 0)
            {
                contact = ReadPlayerRungContact(limb);
                weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.14f, .35f, progress));
            }
            else
            {
                continue;
            }

            if (limb < 2)
            {
                if (limb == WindowLeadHand)
                {
                    ApplyLadderHandIK(limb, contact, weight, true);
                }
                else if (progress < .45f)
                {
                    Vector3 palm = tip.position + ReadPlayerPalmOffset(limb);
                    contact = Vector3.Lerp(palm, contact, weight);
                    float outside = Vector3.Dot(contact - window.OpeningCenterWorld, Outward);
                    float clearanceWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.34f, .45f, progress));
                    contact += Outward * (Mathf.Max(0f, .19f - outside) * clearanceWeight);
                    ApplyLadderHandIK(limb, contact, 1f, false, weight);
                }
                continue;
            }

            ApplyLadderFootIK(limb, contact, weight);
        }
    }

    private void BreakWindowAtHeadContact()
    {
        if (usedWindow || !climbOwnsWindow || window == null || window.fracture == null || window.fracture.IsBroken)
        {
            return;
        }

        Vector3 nextHead = climbHead.position - Outward * .09f;
        Vector3 displacement = nextHead - previousWindowHead;
        if (Vector3.Dot(displacement, -Outward) > 0f)
        {
            usedWindow = window.fracture.TryFractureTraversalContact(previousWindowHead, nextHead);
        }
        previousWindowHead = nextHead;
    }

    private void ReadPlayerNextStep()
    {
        float direction = ReadPlayerLadderDirection();
        if (TryBeginPlayerWindowExit(direction))
        {
            return;
        }

        if (direction > 0.25f && playerClimbHeight >= TopClimbHeight - 0.01f)
        {
            if (!IsWindow && HasExitSupport(Exit) && IsExitClear(climbCapsule, climbingPlayer.transform))
            {
                AnimationClip clip = climbClips.exitRight;
                if (leftStep)
                {
                    clip = climbClips.exitLeft;
                }
                StartPlayerPhase(PlayerPhase.Exit, clip, Exit);
                return;
            }
        }
        else if (direction > 0.25f || direction < -0.25f && playerClimbHeight > lowestClimbHeight + 0.02f)
        {
            playerSteppingDown = direction < 0f;
            float playerStepHeight = RungSpacing * ClimbAxis.y;
            if (playerSteppingDown)
            {
                playerStepHeight = -playerStepHeight;
            }
            playerClimbHeight = Mathf.Clamp(playerClimbHeight + playerStepHeight, lowestClimbHeight, TopClimbHeight);
            AnimationClip clip = climbClips.climbRight;
            if (leftStep)
            {
                clip = climbClips.climbLeft;
            }
            if (playerSteppingDown)
            {
                clip = climbClips.descendRight;
                if (leftStep)
                {
                    clip = climbClips.descendLeft;
                }
            }
            for (int limb = 0; limb < playerSupportRungs.Length; limb++)
            {
                if (IsPlayerLimbChangingRungs(limb))
                {
                    playerSupportRungs[limb] = -1;
                }
            }
            StartPlayerPhase(PlayerPhase.Step, clip, ReadClimbPosition(playerClimbHeight));
            return;
        }
        else if (direction < -0.25f && playerClimbHeight <= lowestClimbHeight + 0.02f)
        {
            StartPlayerPhase(PlayerPhase.Exit, climbClips.dismountBottom, Approach);
            playerExitAtFoot = true;
            return;
        }

        if (playerPhase != PlayerPhase.Wait)
        {
            AnimationClip idle = climbClips.waitRight;
            if (leftStep)
            {
                idle = climbClips.waitLeft;
            }
            StartPlayerPhase(PlayerPhase.Wait, idle, climbBody.position);
        }
    }

    private void TickPlayerFall()
    {
        if (playerPhaseElapsed > 2.6f)
        {
            EndPlayerTraversal(true);
            return;
        }

        if (towerJumpDeparture)
        {
            Vector3 offset = Vector3.ProjectOnPlane(towerJumpTarget - climbBody.position, Vector3.up);
            Vector3 velocity = Vector3.ClampMagnitude(offset / Time.fixedDeltaTime, 2.8f);
            velocity.y = climbBody.linearVelocity.y;
            climbMotion.CommitSpeed(climbToken, velocity);
            if (climbBody.position.y > Head.y - .32f)
            {
                return;
            }
            towerJumpDeparture = false;
        }
        if (playerPhaseElapsed < 0.15f ||
            !LeonDropContact.TryReadSupport(climbCapsule, climbingPlayer.transform, null,
                Physics.DefaultRaycastLayers, 0.32f, out float distance) ||
            !LeonDropContact.IsContact(climbBody, distance, 0.11f))
        {
            return;
        }

        if (climbCamera != null)
        {
            climbCamera.NotifyDropImpact(Mathf.Abs(climbBody.linearVelocity.y));
        }
        climbMotion.CommitSpeed(climbToken, Vector3.zero);
        AnimationClip land = configuration.playerJumpLand;
        if (tower != null && tower.HasCollapsed)
        {
            land = configuration.playerTowerLand;
        }
        if (land == null)
        {
            EndPlayerTraversal(false);
            return;
        }
        StartPlayerPhase(PlayerPhase.Land, land, climbBody.position);
    }

    private void HandlePlayerTraversalInterrupted(AnimationActionTransaction action)
    {
        if (action.transactionId == climbTransaction)
        {
            EndPlayerTraversal(true);
        }
    }

    private void BeginClimbBodyControl()
    {
        bodyWasKinematic = climbBody.isKinematic;
        climbOriginalInterpolation = climbBody.interpolation;
        climbControlsBody = true;
        if (!bodyWasKinematic)
        {
            climbMotion.CommitSpeed(climbToken, Vector3.zero);
            climbMotion.CommitAngleSpeed(climbToken, Vector3.zero);
            climbBody.isKinematic = true;
        }
        // 攀梯和出梯由同一个通行凭证插值，避免固定步位移把当前支撑姿势推向梯背。
        climbBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void ReleaseClimbBodyControl()
    {
        if (!climbControlsBody)
        {
            return;
        }
        if (climbBody != null)
        {
            climbBody.interpolation = climbOriginalInterpolation;
            climbBody.isKinematic = bodyWasKinematic;
        }
        climbControlsBody = false;
    }

    private void EndPlayerTraversal(bool interrupted)
    {
        if (climbTransaction == 0)
        {
            return;
        }

        int transaction = climbTransaction;
        RestoreRails(climbCapsule);
        ReleaseClimbBodyControl();
        towerJumpDeparture = false;
        climbTransaction = 0;
        playerExitAtFoot = false;
        if (climbOwnsWindow && window != null)
        {
            window.ReleasePassage(this);
        }
        climbOwnsWindow = false;
        if (climbGraph.IsValid())
        {
            climbGraph.Destroy();
        }
        if (climbAnimator != null)
        {
            climbAnimator.updateMode = originalClimbUpdateMode;
            climbAnimator = null;
        }
        if (climbBody != null)
        {
            climbBody.useGravity = restoreGravity;
        }
        if (ignoredFixedLadder && climbCapsule != null && tower != null && tower.ladderCollider != null)
        {
            Physics.IgnoreCollision(climbCapsule, tower.ladderCollider, false);
        }
        ignoredFixedLadder = false;
        if (climbCapsule != null)
        {
            foreach (var collider in ignoredWindowColliders)
            {
                if (collider != null)
                {
                    Physics.IgnoreCollision(climbCapsule, collider, false);
                }
            }
        }
        ignoredWindowColliders.Clear();
        if (climbWeapon != null)
        {
            climbWeapon.SetTraversalWeaponVisible(true);
        }
        if (dropCameraActive && climbCamera != null)
        {
            climbCamera.EndDropCamera();
        }
        dropCameraActive = false;
        if (climbMotion != null)
        {
            climbMotion.ReleaseMotion(climbToken);
        }
        if (climbActions != null)
        {
            climbActions.OnTraversalActionInterrupted -= HandlePlayerTraversalInterrupted;
            climbActions.EndLadderAction(transaction, FullBodyActionType.LadderTraversal, interrupted);
            if (climbingPlayer != null && climbingPlayer.currentState == LeonState.Traversing && !climbActions.HasActiveFullBodyAction)
            {
                climbingPlayer.ChangeState(LeonState.Idle);
            }
            climbActions.SynchronizeMotionControlAuthority();
        }
        climbingPlayer = null;
        climbCamera = null;
        climbPlayable = default;
    }
}
