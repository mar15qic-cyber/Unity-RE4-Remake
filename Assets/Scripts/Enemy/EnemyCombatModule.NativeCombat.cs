using GameArchitecture;
using RE4.ChapterOne;
using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private enum NativeCombatPhase
    {
        None,
        Grab,
        GrabRecovery,
        Falling,
        Landing,
        Down,
        Rising,
        LegTrap,
        DyingEntry,
        DyingLoop,
        Reviving
    }

    [Header("原版抓取、倒地与歪脖复起")]
    [SerializeField] private NativeEnemyCombatProfile nativeCombatProfile;
    internal NativeEnemyCombatProfile NativeCombatProfile => nativeCombatProfile;
    private NativeCombatPhase nativeCombatPhase;
    private ChapterOnePairMotion.Track nativeCombatTrack;
    private ActionHandle nativeCombatAction;
    private float nativeCombatTime;
    private Pose nativeCombatPreviousSample;
    private Quaternion nativeCombatOrientation;
    private LeonController nativeGrabTarget;
    private QTEKind nativeGrabKind;
    private bool nativeGrabContactChecked;
    private bool nativeDownFaceUp;
    private string nativeLandingMotion;
    private long nativeLifeRevision;
    private bool pendingDyingAnimation;
    private bool nativePairSuspended;
    private Transform[] nativeGrabHands;
    private Transform nativeFinisherHead;
    private float nativeFallLandingTime;
    private float nativeDownElapsed;
    private float nativeKnockdownTravelScale = 1f;
    private bool nativeKickKnockdown;
    private Vector3 nativeKickTravelDirection;

    private VillageLegTrap enemyTrap;
    private Transform trapPelvis;
    private Transform trapLeftFoot;
    private Transform trapRightFoot;
    private EnvironmentPoseOffset trapPelvisOffset;
    private bool trapBonesSearched;

    internal bool HasCachedTrapFeet
    {
        get
        {
            EnsureTrapBonesCached();
            return trapLeftFoot != null || trapRightFoot != null;
        }
    }

    private void EnsureTrapBonesCached()
    {
        if (trapBonesSearched || anim == null)
        {
            return;
        }

        trapBonesSearched = true;
        foreach (Transform joint in anim.GetComponentsInChildren<Transform>(true))
        {
            if (joint.name == "Hip")
            {
                trapPelvis = joint;
            }
            else if (joint.name == "L_Foot")
            {
                trapLeftFoot = joint;
            }
            else if (joint.name == "R_Foot")
            {
                trapRightFoot = joint;
            }
        }

        if (trapPelvis != null)
        {
            trapPelvisOffset = new EnvironmentPoseOffset(trapPelvis);
        }
    }

    internal bool IsFootNearLegTrap(Vector3 anklePosition, float maxDistance)
    {
        EnsureTrapBonesCached();
        float maxSqr = maxDistance * maxDistance;
        if (trapLeftFoot != null && (trapLeftFoot.position - anklePosition).sqrMagnitude <= maxSqr)
        {
            return true;
        }

        if (trapRightFoot != null && (trapRightFoot.position - anklePosition).sqrMagnitude <= maxSqr)
        {
            return true;
        }

        return false;
    }

    internal bool TryCatchLegTrap(VillageLegTrap trap)
    {
        if (!host.IsRuntimeInitialized || missionEnemyType != MissionEnemyType.Ganado || IsDead || IsQTEControlled
            || NativeCombatActive || permanentCrawl || host.IsNavigationTraversalProtected
            || !TryReadNativeCombatTrack("Trap2000", out var track))
        {
            return false;
        }
        var handle = ActionCoordinator.BeginNativeCombat(false);
        if (!handle.Valid)
        {
            return false;
        }
        EnsureTrapBonesCached();
        enemyTrap = trap;
        nativeCombatAction = handle;
        nativeCombatOrientation = transform.rotation * Quaternion.Inverse(track.Sample(0f).rotation);
        SetNativeCombatTrack(track, NativeCombatPhase.LegTrap);

        // Trap2000 中由左脚向前踏进夹口（稳态偏移约 (-0.17, 0, +0.52)），入夹瞬间先微调根节点让左脚落点贴近夹口，
        // 避免大幅拉扯骨盆造成明显滑步或错位。
        if (trap != null)
        {
            Vector3 expectedPinnedFootOffset = transform.rotation * new Vector3(-0.17f, 0f, 0.52f);
            Vector3 desiredRoot = trap.Ankle - expectedPinnedFootOffset;
            Vector3 rootShift = Vector3.ProjectOnPlane(desiredRoot - transform.position, Vector3.up);
            if (rootShift.sqrMagnitude > 0.0004f)
            {
                Vector3 clampedShift = Vector3.ClampMagnitude(rootShift, 0.55f);
                host.CommitNativeCombatMotion(handle, clampedShift, transform.rotation, 0f, false, out _);
            }
        }

        return true;
    }

    internal void ApplyLegTrapFootPin()
    {
        if (nativeCombatPhase != NativeCombatPhase.LegTrap || enemyTrap == null || IsDead || IsQTEControlled)
        {
            if (trapPelvisOffset != null)
            {
                trapPelvisOffset.Restore();
            }
            return;
        }

        EnsureTrapBonesCached();
        if (trapPelvis == null || trapPelvisOffset == null || trapLeftFoot == null)
        {
            return;
        }

        float frame = nativeCombatTime * 60f;
        float entryWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 8f, frame));
        float releaseWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(366f, 419f, frame));
        float pinWeight = entryWeight * releaseWeight;
        if (pinWeight <= 0.001f)
        {
            trapPelvisOffset.Restore();
            return;
        }

        // 参照里昂踩夹对齐方式：平移骨盆使被夹左脚精准钉在夹口中心，完整保留原版挣扎动作。
        trapPelvisOffset.Capture();
        Vector3 delta = enemyTrap.Ankle - trapLeftFoot.position;
        trapPelvis.position += Vector3.ClampMagnitude(delta, 0.75f) * pinWeight;
        trapPelvisOffset.Commit();
    }

    internal bool TryReadTrappedOpportunity(out ActionHandle handle)
    {
        handle = nativeCombatAction;
        float frame = nativeCombatTime * 60f;
        return nativeCombatPhase == NativeCombatPhase.LegTrap && enemyTrap != null
            && handle.Valid && handle == ActionCoordinator.CurrentHandle && frame >= 64f && frame < 419f;
    }

    internal void ReleaseEnemyTrap()
    {
        if (trapPelvisOffset != null)
        {
            trapPelvisOffset.Restore();
        }
        if (enemyTrap != null)
        {
            enemyTrap.ReleaseEnemy(this);
            enemyTrap = null;
        }
    }

    private void Start()
    {
        if (anim != null)
        {
            var hands = new System.Collections.Generic.List<Transform>();
            foreach (Transform joint in anim.GetComponentsInChildren<Transform>())
            {
                if (joint.name == "Head")
                {
                    nativeFinisherHead = joint;
                }
                if (joint.name == "L_Hand" || joint.name == "R_Hand")
                {
                    hands.Add(joint);
                }
            }
            nativeGrabHands = hands.ToArray();
        }
    }

    // 补刀会话拥有受击表现。照常扣血和进入终局死亡，但不再由这次刺击排队一次正面重硬直。
    internal void CommitPairedKnifeDamage(float damage, Vector3 point)
    {
        ApplyIncomingDamage(damage, point, HitboxType.Chest, Vector3.zero, true, out _);
    }

    internal bool NativeCombatActive => nativeCombatPhase != NativeCombatPhase.None;
    internal bool HasGrabWindup => nativeCombatPhase == NativeCombatPhase.Grab && nativeCombatAction == ActionCoordinator.CurrentHandle;
    internal bool HasGroundFinisherPose => HealthModule != null &&
        // 仅在落地完成至起身前开放普通击倒；濒死与变异沿用各自窗口。
        (nativeCombatPhase == NativeCombatPhase.Down && (HealthModule.LifePhase is EnemyLifePhase.Alive or EnemyLifePhase.Revived)
        || nativeCombatPhase == NativeCombatPhase.DyingEntry || nativeCombatPhase == NativeCombatPhase.DyingLoop
        || nativeCombatPhase == NativeCombatPhase.Reviving && IsRevivalPoseNearGround());

    private bool IsRevivalPoseNearGround()
    {
        if (nativeCombatTrack != null && nativeCombatTrack.clip != null)
        {
            AnimationClip clip = nativeCombatTrack.clip;
            float duration = Mathf.Max(0.01f, clip.length);
            float normalizedTime = nativeCombatTime / duration;
            string clipName = clip.name;
            float maxNormalizedTime = 0.65f;
            if (!string.IsNullOrEmpty(clipName))
            {
                if (clipName.IndexOf("3592", System.StringComparison.Ordinal) >= 0)
                {
                    maxNormalizedTime = 0.72f;
                }
                else if (clipName.IndexOf("3597", System.StringComparison.Ordinal) >= 0)
                {
                    maxNormalizedTime = 0.55f;
                }
            }
            if (normalizedTime <= maxNormalizedTime)
            {
                return true;
            }
        }

        if (nativeFinisherHead == null)
        {
            return nativeCombatTime < 2.5f;
        }

        // 两套复起扭动动作的抬头时点不同；在完全站起前保留倒地与变异补刀窗口。
        return nativeFinisherHead.position.y - transform.position.y <= 1.15f;
    }
    internal string GroundFinisherMotion
    {
        get
        {
            if (!HasGroundFinisherPose)
            {
                return null;
            }
            if (nativeDownFaceUp)
            {
                return "Ground1010";
            }
            return "Ground1000";
        }
    }

    private bool TryReadNativeCombatTrack(string motion, out ChapterOnePairMotion.Track track)
    {
        track = null;
        if (nativeCombatProfile == null || anim == null || anim.runtimeAnimatorController == null || anim.isHuman)
        {
            return false;
        }
        string role = "GanadoMale";
        if (anim.runtimeAnimatorController.name == "MainGanado")
        {
            role = "Ganado";
        }
        var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
        return bank != null && bank.TryGetVillagerTrack(role, motion, out track);
    }

    internal bool TryBeginGrabAttack(LeonController player, QTEKind kind)
    {
        if (NativeCombatActive || player == null || IsDead || IsQTEControlled || host.IsNavigationTraversalProtected
            || HealthModule.LifePhase == EnemyLifePhase.Dying || HealthModule.LifePhase == EnemyLifePhase.Reviving)
        {
            return false;
        }
        string motion = "Native_2100";
        if (HealthModule.LifePhase == EnemyLifePhase.Revived)
        {
            motion = "Native_2190";
        }
        if (!TryReadNativeCombatTrack(motion, out var track))
        {
            return false;
        }
        ActionHandle handle = ActionCoordinator.BeginNativeCombat(true);
        if (!handle.Valid)
        {
            return false;
        }
        nativeCombatAction = handle;
        nativeGrabTarget = player;
        lastAttackTime = Time.time;
        nativeGrabKind = kind;
        nativeGrabContactChecked = false;
        Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
        nativeCombatOrientation = transform.rotation;
        if (direction.sqrMagnitude > .001f)
        {
            nativeCombatOrientation = Quaternion.LookRotation(direction);
        }
        SetNativeCombatTrack(track, NativeCombatPhase.Grab);
        return true;
    }

    internal bool TryBeginFirearmKnockdown(FirearmBodyTechniqueShot shot)
    {
        if (nativeCombatProfile == null || !shot.shotgun || NativeCombatActive || IsDead || IsQTEControlled
            || missionEnemyType != MissionEnemyType.Ganado || pendingCrawl || permanentCrawl
            || shot.pellets < nativeCombatProfile.shotgunKnockdownPellets
            || shot.totalDamage < nativeCombatProfile.shotgunKnockdownDamage)
        {
            return false;
        }
        return BeginNativeKnockdown(shot.sourcePosition, false);
    }

    internal bool TryBeginBodyTechniqueKnockdown(Vector3 source, float travelScale)
    {
        if (nativeCombatProfile == null || IsQTEControlled || missionEnemyType != MissionEnemyType.Ganado ||
            host.IsNavigationTraversalProtected || permanentCrawl)
        {
            return false;
        }

        int kneelSide = ConsumeCachedBodyTechniqueKneelState();
        if (IsDead)
        {
            return NativeDeathActive || BeginTerminalDirectionalKnockdown(source, travelScale, true, kneelSide);
        }

        if (nativeCombatPhase == NativeCombatPhase.LegTrap)
        {
            CancelNativeCombat(true);
        }
        if (NativeCombatActive && nativeCombatPhase != NativeCombatPhase.Grab
            && nativeCombatPhase != NativeCombatPhase.GrabRecovery && nativeCombatPhase != NativeCombatPhase.Rising)
        {
            return true;
        }
        return BeginNativeKnockdown(source, false, travelScale, true, kneelSide);
    }

    private bool BeginNativeKnockdown(Vector3 source, bool dying, float travelScale = 1f, bool fromKick = false, int kneelSide = 0)
    {
        Vector3 local = transform.InverseTransformDirection(source - transform.position);
        string motion = "Native_1015";
        nativeLandingMotion = "Native_1016";
        nativeDownFaceUp = true;
        if (fromKick && kneelSide == 1 && TryReadNativeCombatTrack("Ganado_1100", out _))
        {
            motion = "Ganado_1100";
            nativeLandingMotion = "Ganado_1101";
            nativeDownFaceUp = true;
        }
        else if (fromKick && kneelSide == 2 && TryReadNativeCombatTrack("Ganado_1105", out _))
        {
            motion = "Ganado_1105";
            nativeLandingMotion = "Ganado_1106";
            nativeDownFaceUp = true;
        }
        else if (local.z < -Mathf.Abs(local.x))
        {
            motion = "Native_1030";
            nativeLandingMotion = "Native_1031";
            nativeDownFaceUp = false;
        }
        else if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
        {
            nativeDownFaceUp = false;
            if (local.x < 0f)
            {
                motion = "Native_1040";
                nativeLandingMotion = "Native_1041";
            }
            else
            {
                motion = "Native_1060";
                nativeLandingMotion = "Native_1061";
            }
        }
        if (!TryReadNativeCombatTrack(motion, out var track))
        {
            return false;
        }
        CancelNativeCombat(false);
        ActionHandle handle = ActionCoordinator.BeginNativeCombat(false);
        if (!handle.Valid)
        {
            return false;
        }
        nativeCombatAction = handle;
        nativeCombatOrientation = transform.rotation * Quaternion.Inverse(track.Sample(0f).rotation);
        nativeKnockdownTravelScale = travelScale;
        nativeKickKnockdown = fromKick;
        nativeKickTravelDirection = Vector3.ProjectOnPlane(transform.position - source, Vector3.up).normalized;
        pendingDyingAnimation = dying;
        pendingHit = false;
        pendingCrawl = false;
        pendingStandUp = false;
        standUpFinished = false;
        pendingTripOver = false;
        ClearNativeBodyReaction();
        SetNativeCombatTrack(track, NativeCombatPhase.Falling);
        return true;
    }

    internal bool TryBeginPairedGroundRecovery()
    {
        if (IsDead || IsQTEControlled || !TryReadNativeCombatTrack("Native_3500", out var track))
        {
            return false;
        }
        CancelNativeCombat(false);
        ActionHandle handle = ActionCoordinator.BeginNativeCombat(false);
        if (!handle.Valid)
        {
            return false;
        }
        nativeCombatAction = handle;
        nativeCombatOrientation = transform.rotation * Quaternion.Inverse(track.Sample(0f).rotation);
        nativeKnockdownTravelScale = 1f;
        nativeKickKnockdown = false;
        pendingHit = false;
        pendingStandUp = false;
        ClearNativeBodyReaction();
        SetNativeCombatTrack(track, NativeCombatPhase.Rising);
        return true;
    }

    private void SetNativeCombatTrack(ChapterOnePairMotion.Track track, NativeCombatPhase phase)
    {
        if (phase == NativeCombatPhase.GrabRecovery && nativeCombatPhase == NativeCombatPhase.Grab)
        {
            AnimationPresenter.BlendMissedGrab(track.clip);
        }
        nativeCombatPhase = phase;
        nativeCombatTrack = track;
        nativeCombatTime = 0f;
        nativeDownElapsed = 0f;
        nativeCombatPreviousSample = track.Sample(0f);
        if (phase == NativeCombatPhase.Falling)
        {
            nativeFallLandingTime = ReadNativeFallLandingTime(track);
        }
        float entryBlendSeconds = 0f;
        if (phase == NativeCombatPhase.Falling)
        {
            entryBlendSeconds = 0.08f;
        }
        AnimationPresenter.PresentNativeCombat(track.clip, 0f, false, entryBlendSeconds);
    }

    private bool SwitchNativeCombatTrack(string motion, NativeCombatPhase phase)
    {
        if (!TryReadNativeCombatTrack(motion, out var track))
        {
            CancelNativeCombat(true);
            return false;
        }
        nativeCombatOrientation = transform.rotation * Quaternion.Inverse(track.Sample(0f).rotation);
        SetNativeCombatTrack(track, phase);
        return true;
    }

    private static float ReadNativeFallLandingTime(ChapterOnePairMotion.Track track)
    {
        // 原起手包含高处跌落尾段；平地按源根轨越过落地平面切换，再确认场景支撑。
        int peak = 0;
        for (int frame = 1; frame < track.positions.Length; frame++)
        {
            if (track.positions[frame].y > track.positions[peak].y)
            {
                peak = frame;
            }
        }
        for (int frame = peak + 1; frame < track.positions.Length; frame++)
        {
            if (track.positions[frame].y <= track.positions[0].y)
            {
                return frame / track.clip.frameRate;
            }
        }
        return track.clip.length;
    }

    internal void SuspendNativeCombatForGroundFinisher()
    {
        // 即使配对在同一帧取消，倒地动作也要在下一帧重新领取凭证。
        if (NativeCombatActive)
        {
            nativePairSuspended = true;
        }
    }

    internal bool TickNativeCombat()
    {
        if (!NativeCombatActive)
        {
            ResumePendingRevival();
        }
        if (!NativeCombatActive)
        {
            return false;
        }
        if (IsQTEControlled)
        {
            nativePairSuspended = true;
            AnimationPresenter.StopNativeCombat();
            return true;
        }
        if (nativePairSuspended && !IsDead)
        {
            nativePairSuspended = false;
            nativeCombatAction = ActionCoordinator.BeginNativeCombat(false);
        }
        if (IsDead || !nativeCombatAction.Valid || nativeCombatAction != ActionCoordinator.CurrentHandle)
        {
            CancelNativeCombat(false);
            return false;
        }
        if (Time.deltaTime <= 0f)
        {
            return true;
        }
        float speed = 1f;
        if (nativeCombatTrack.clip.name == "Native_2190")
        {
            speed = 1.42f;
        }
        float previousTime = nativeCombatTime;
        nativeCombatTime = Mathf.Min(nativeCombatTime + Time.deltaTime * speed, nativeCombatTrack.clip.length);
        if (nativeCombatPhase == NativeCombatPhase.DyingEntry || nativeCombatPhase == NativeCombatPhase.DyingLoop || nativeCombatPhase == NativeCombatPhase.Reviving)
        {
            PlayRevivalMarkers(previousTime, nativeCombatTime);
        }
        Pose sample = nativeCombatTrack.Sample(nativeCombatTime);
        Vector3 displacement = nativeCombatOrientation * (sample.position - nativeCombatPreviousSample.position);
        Vector3 planar = Vector3.ProjectOnPlane(displacement, Vector3.up);
        if (nativeKickKnockdown && (nativeCombatPhase == NativeCombatPhase.Falling || nativeCombatPhase == NativeCombatPhase.Landing)
            && nativeKickTravelDirection.sqrMagnitude > .001f)
        {
            planar = nativeKickTravelDirection * planar.magnitude;
        }
        float lift = 0f;
        if (nativeCombatPhase == NativeCombatPhase.Falling)
        {
            displacement = planar * nativeKnockdownTravelScale;
            if (nativeKickKnockdown && nativeCombatTime < nativeFallLandingTime)
            {
                lift = ReadKickLift(nativeCombatTrack, sample);
            }
        }
        if (nativeCombatPhase == NativeCombatPhase.Landing)
        {
            // 原 KneelDown 落地节点的 GroundAdsorberLimitSpeedXZ 为 1。
            displacement = Vector3.ClampMagnitude(planar * nativeKnockdownTravelScale, Time.deltaTime);
        }
        Quaternion facing = nativeCombatOrientation * sample.rotation;
        bool findingLanding = nativeCombatPhase == NativeCombatPhase.Falling || nativeCombatPhase == NativeCombatPhase.Landing;
        bool moved = host.CommitNativeCombatMotion(nativeCombatAction, displacement, facing, lift, findingLanding, out bool onSupport);
        nativeCombatPreviousSample = sample;
        float poseTime = nativeCombatTime;
        if (nativeCombatPhase == NativeCombatPhase.Falling && !onSupport)
        {
            poseTime = Mathf.Min(poseTime, Mathf.Max(0f, nativeFallLandingTime - 1f / nativeCombatTrack.clip.frameRate));
        }
        float entryBlendSeconds = 0f;
        if (nativeCombatPhase == NativeCombatPhase.Falling)
        {
            entryBlendSeconds = 0.08f;
        }
        AnimationPresenter.PresentNativeCombat(nativeCombatTrack.clip, poseTime, nativeCombatPhase == NativeCombatPhase.GrabRecovery, entryBlendSeconds);
        if (nativeCombatPhase == NativeCombatPhase.LegTrap && enemyTrap != null)
            enemyTrap.SampleEnemy(poseTime);
        if (nativeCombatPhase == NativeCombatPhase.LegTrap && nativeCombatTime >= 419f / 60f)
        {
            ReleaseEnemyTrap();
        }
        if (nativeCombatPhase == NativeCombatPhase.Falling)
        {
            if ((onSupport && nativeCombatTime >= nativeFallLandingTime)
                || nativeCombatTime >= nativeFallLandingTime + 0.75f)
            {
                AdvanceNativeCombatPhase();
            }
            return true;
        }
        if (findingLanding && !onSupport && nativeCombatTime < nativeFallLandingTime + 0.85f)
        {
            return true;
        }

        bool isGrabPhase = nativeCombatPhase == NativeCombatPhase.Grab || nativeCombatPhase == NativeCombatPhase.GrabRecovery;
        if (isGrabPhase && !host.HasPhysicalEnemySupport())
        {
            EnemyWindowTraversal traversal = host.ArchitectureReadSpecialTraversal;
            CancelNativeCombat(true);
            if (traversal != null)
            {
                traversal.TryBeginUnsupportedDrop();
            }
            return true;
        }

        if (!moved && nativeCombatPhase == NativeCombatPhase.Grab)
        {
            nativeGrabContactChecked = true;
        }
        if (nativeCombatPhase == NativeCombatPhase.DyingLoop)
        {
            BeginNativeRevivalWait();
            if (HealthModule.TryBeginReviving(nativeLifeRevision, Time.timeAsDouble))
            {
                string motion = "Native_3592";
                if (nativeDownFaceUp)
                {
                    motion = "Native_3597";
                }
                SwitchNativeCombatTrack(motion, NativeCombatPhase.Reviving);
            }
            else if (nativeCombatTime >= nativeCombatTrack.clip.length)
            {
                SetNativeCombatTrack(nativeCombatTrack, nativeCombatPhase);
            }
            return true;
        }
        // 倒地等待独立计时，让短动作也留出接近和补刀的时间。
        if (nativeCombatPhase == NativeCombatPhase.Down)
        {
            nativeDownElapsed += Time.deltaTime;
            if (nativeDownElapsed >= 2f)
            {
                AdvanceNativeCombatPhase();
            }
            return true;
        }
        if (nativeCombatPhase == NativeCombatPhase.Grab
            && nativeCombatTrack.clip.name == "Native_2100" && nativeCombatTime * nativeCombatTrack.clip.frameRate > 92f)
        {
            nativeGrabContactChecked = true;
            SwitchNativeCombatTrack("Native_2110", NativeCombatPhase.GrabRecovery);
        }
        else if (nativeCombatTime >= nativeCombatTrack.clip.length)
        {
            AdvanceNativeCombatPhase();
        }
        return true;
    }

    private static float ReadKickLift(ChapterOnePairMotion.Track track, Pose sample)
    {
        // 悬空高度取自倒地根轨，并限制到村庄地面探测能连续跟踪的范围。
        return Mathf.Clamp(sample.position.y - track.positions[0].y, 0f, .36f);
    }

    private void AdvanceNativeCombatPhase()
    {
        if (nativeCombatPhase == NativeCombatPhase.Falling)
        {
            SwitchNativeCombatTrack(nativeLandingMotion, NativeCombatPhase.Landing);
        }
        else if (nativeCombatPhase == NativeCombatPhase.Landing)
        {
            string motion = "Native_3001";
            NativeCombatPhase next = NativeCombatPhase.Down;
            if (nativeDownFaceUp)
            {
                motion = "Native_3011";
            }
            if (pendingDyingAnimation)
            {
                next = NativeCombatPhase.DyingEntry;
                motion = "Native_3590";
                if (nativeDownFaceUp)
                {
                    motion = "Native_3595";
                }
            }
            SwitchNativeCombatTrack(motion, next);
        }
        else if (nativeCombatPhase == NativeCombatPhase.DyingEntry)
        {
            string motion = "Native_3591";
            if (nativeDownFaceUp)
            {
                motion = "Native_3596";
            }
            SwitchNativeCombatTrack(motion, NativeCombatPhase.DyingLoop);
        }
        else if (nativeCombatPhase == NativeCombatPhase.Down)
        {
            string motion = "Native_3500";
            if (nativeDownFaceUp)
            {
                motion = "Native_3510";
            }
            SwitchNativeCombatTrack(motion, NativeCombatPhase.Rising);
        }
        else
        {
            if (nativeCombatPhase == NativeCombatPhase.Reviving)
            {
                float revivedHealth = Mathf.Max(nativeCombatProfile.revivalContinuationHealth, maxHealth * 1.65f);
                if (HealthModule.CompleteRevival(nativeLifeRevision, revivedHealth))
                {
                    bodyTechniquePoise = Vector3.zero;
                    accumulatedPoiseDamage = 0f;
                    UpdateHealthAnimationParameter();
                }
            }
            CancelNativeCombat(true);
        }
    }

    private void CheckNativeGrabContact()
    {
        if (!HasGrabWindup || nativeGrabContactChecked || nativeGrabTarget == null
            || nativeGrabTarget.currentState == LeonState.Dead || IsDead || IsQTEControlled)
        {
            return;
        }
        float frame = nativeCombatTime * nativeCombatTrack.clip.frameRate;
        bool inContactWindow = frame >= 26f && frame <= 92f;
        if (nativeCombatTrack.clip.name == "Native_2190")
        {
            inContactWindow = frame >= 40f && frame <= 57f || frame >= 89f && frame <= 101f
                || frame >= 146f && frame <= 161f;
        }
        if (!inContactWindow || nativeGrabHands == null || !HasClearLineToPlayer(false, 3f)
            || Mathf.Abs(nativeGrabTarget.transform.position.y - transform.position.y) > .28f)
        {
            return;
        }
        if (nativeGrabKind == QTEKind.RearBiteNeck)
        {
            if (!VillagerQTEParticipant.IsValidRearGrabGeometry(
                transform.position,
                transform.forward,
                nativeGrabTarget.transform.position,
                nativeGrabTarget.transform.forward,
                nativeGrabTarget.IsCrouching))
            {
                return;
            }
        }
        if (!nativeGrabTarget.TryGetCurrentWorldCapsule(out Vector3 top, out Vector3 bottom, out float radius))
        {
            return;
        }
        bool touching = false;
        foreach (Transform joint in nativeGrabHands)
        {
            if (joint == null)
            {
                continue;
            }
            Vector3 axis = top - bottom;
            float along = 0f;
            if (axis.sqrMagnitude > .0001f)
            {
                along = Mathf.Clamp01(Vector3.Dot(joint.position - bottom, axis) / axis.sqrMagnitude);
            }
            float reach = radius + .18f;
            if ((joint.position - bottom - axis * along).sqrMagnitude <= reach * reach)
            {
                touching = true;
                break;
            }
        }
        if (!touching)
        {
            return;
        }
        var participant = host.GetComponent<VillagerQTEParticipant>();
        var coordinator = nativeGrabTarget.GetComponent<LeonQTECoordinator>();
        if (participant == null || coordinator == null)
        {
            return;
        }
        nativeGrabContactChecked = true;
        participant.BeginConfirmedGrabContact(nativeCombatAction, nativeGrabTarget, nativeGrabKind);
        bool started = coordinator.TryStart(nativeGrabKind, participant, out _);
        participant.EndConfirmedGrabContact();
        if (EnemyAIManager.Instance != null)
        {
            EnemyAIManager.Instance.CompleteQTEAttempt(host, nativeGrabKind, started);
        }
        if (started)
        {
            CancelNativeCombat(false);
        }
    }

    internal void CancelNativeCombat(bool resume)
    {
        if (!NativeCombatActive)
        {
            return;
        }
        ActionHandle old = nativeCombatAction;
        bool grab = nativeCombatPhase == NativeCombatPhase.Grab || nativeCombatPhase == NativeCombatPhase.GrabRecovery;
        ReleaseEnemyTrap();
        nativeCombatPhase = NativeCombatPhase.None;
        nativeCombatAction = default;
        nativeCombatTrack = null;
        nativePairSuspended = false;
        nativeGrabTarget = null;
        AnimationPresenter.StopNativeCombat();
        if (grab && EnemyAIManager.Instance != null)
        {
            EnemyAIManager.Instance.CompleteQTEAttempt(host, nativeGrabKind, false);
        }
        if (resume)
        {
            ActionCoordinator.EndNativeCombat(old, !grab);
        }
    }
}
