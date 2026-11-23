using GameArchitecture;
using RE4.ChapterOne;
using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private ChapterOnePairMotion.Track nativeDeathTrack;
    private string nativeDeathLanding;
    private float nativeDeathTime;
    private bool nativeDeathFrozen;
    private float assassinationEndTime;
    private Pose nativeDeathPreviousSample;
    private Quaternion nativeDeathOrientation;
    private ActionHandle nativeDeathAction;
    private float nativeDeathTravelScale = 1f;
    private bool nativeKickDeath;
    private Vector3 nativeDeathTravelDirection;
    internal bool NativeDeathActive => nativeDeathTrack != null;
    internal bool NativeDeathPoseReady => NativeDeathActive && (nativeDeathFrozen ||
        (string.IsNullOrEmpty(nativeDeathLanding) && nativeDeathTime >= nativeDeathTrack.clip.length));

    internal void ContinueAssassinationDeath(ChapterOnePairMotion.Track track, float time, float endTime)
    {
        if (!host.IsDead || track == null || track.clip == null)
        {
            return;
        }
        nativeDeathTrack = track;
        assassinationEndTime = Mathf.Min(endTime, track.clip.length);
        nativeDeathTime = Mathf.Clamp(time, 0f, assassinationEndTime);
        nativeDeathFrozen = nativeDeathTime >= assassinationEndTime;
        nativeDeathLanding = null;
        nativeKickDeath = false;
        nativeDeathPreviousSample = track.Sample(nativeDeathTime);
        nativeDeathOrientation = transform.rotation * Quaternion.Inverse(nativeDeathPreviousSample.rotation);
        nativeDeathTravelScale = 1f;
        // 配对释放后，遗体的位移和地面支撑使用死亡动作的凭证。
        ActionCoordinator.ApproveMotionMode(EnemyMovementMode.FullRootMotion);
        BindNativeDeathAction();
        AnimationPresenter.SetPlaySpeed(1f);
        AnimationPresenter.PresentNativeCombat(track.clip, nativeDeathTime);
    }

    internal void PrepareNativeDeathPresentation()
    {
        if (nativeCombatTrack == null || IsQTEControlled
            || nativeCombatPhase == NativeCombatPhase.Grab || nativeCombatPhase == NativeCombatPhase.GrabRecovery
            || (nativeCombatPhase == NativeCombatPhase.Rising || nativeCombatPhase == NativeCombatPhase.Reviving)
                && !IsRevivalPoseNearGround())
        {
            return;
        }
        assassinationEndTime = 0f;
        nativeDeathTrack = nativeCombatTrack;
        nativeDeathTime = nativeCombatTime;
        nativeDeathPreviousSample = nativeCombatPreviousSample;
        nativeDeathOrientation = nativeCombatOrientation;
        nativeDeathLanding = null;
        nativeDeathFrozen = false;
        nativeDeathTravelScale = nativeKnockdownTravelScale;
        nativeKickDeath = nativeKickKnockdown;
        nativeDeathTravelDirection = nativeKickTravelDirection;
        if (nativeCombatPhase == NativeCombatPhase.Falling)
        {
            nativeDeathLanding = nativeLandingMotion;
        }
        else if (nativeCombatPhase != NativeCombatPhase.Landing)
        {
            // 已经在地上的终结保留当前姿势，不再执行普通起身或复起。
            nativeDeathFrozen = true;
        }
    }

    internal void BindNativeDeathAction()
    {
        if (nativeDeathTrack != null)
        {
            nativeDeathAction = ActionCoordinator.CurrentHandle;
        }
    }

    private void TryBeginTerminalFirearmKnockdown(FirearmBodyTechniqueShot shot)
    {
        if (nativeCombatProfile == null || !shot.shotgun || IsQTEControlled
            || missionEnemyType != MissionEnemyType.Ganado || permanentCrawl || NativeDeathActive
            || shot.pellets < nativeCombatProfile.shotgunKnockdownPellets
            || shot.totalDamage < nativeCombatProfile.shotgunKnockdownDamage)
        {
            return;
        }
        BeginTerminalDirectionalKnockdown(shot.sourcePosition);
    }

    private bool BeginTerminalDirectionalKnockdown(Vector3 source, float travelScale = 1f, bool fromKick = false)
    {
        var traversal = host.ArchitectureReadSpecialTraversal;
        if (traversal != null && traversal.DeferLadderGroundReaction)
        {
            return false;
        }

        Vector3 local = transform.InverseTransformDirection(source - transform.position);
        string motion = "Native_1015";
        string landing = "Native_1016";
        if (local.z < -Mathf.Abs(local.x))
        {
            motion = "Native_1030";
            landing = "Native_1031";
        }
        else if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
        {
            motion = "Native_1040";
            landing = "Native_1041";
            if (local.x >= 0f)
            {
                motion = "Native_1060";
                landing = "Native_1061";
            }
        }
        if (!TryReadNativeCombatTrack(motion, out var track))
        {
            return false;
        }
        // 终局死亡照常提交一次；后续只有表现与有效死亡运动凭证，没有恢复生命。
        ActionCoordinator.BeginDeath();
        nativeDeathTrack = track;
        nativeDeathLanding = landing;
        nativeDeathTime = 0f;
        nativeDeathFrozen = false;
        nativeDeathTravelScale = travelScale;
        nativeKickDeath = fromKick;
        nativeDeathTravelDirection = Vector3.ProjectOnPlane(transform.position - source, Vector3.up).normalized;
        nativeDeathPreviousSample = track.Sample(0f);
        nativeDeathOrientation = transform.rotation * Quaternion.Inverse(nativeDeathPreviousSample.rotation);
        BindNativeDeathAction();
        return true;
    }

    internal void TickNativeDeathPresentation()
    {
        if (nativeDeathTrack == null || !IsDead)
        {
            return;
        }
        if (!nativeDeathAction.Valid || nativeDeathAction != ActionCoordinator.CurrentHandle)
        {
            nativeDeathTrack = null;
            AnimationPresenter.StopNativeCombat();
            return;
        }
        if (NativeDeathPoseReady)
        {
            AnimationPresenter.PresentNativeCombat(nativeDeathTrack.clip, nativeDeathTime);
            return;
        }
        nativeDeathTime = Mathf.Min(nativeDeathTime + Time.deltaTime, nativeDeathTrack.clip.length);
        if (assassinationEndTime > 0f && nativeDeathTime >= assassinationEndTime)
        {
            nativeDeathTime = assassinationEndTime;
            nativeDeathFrozen = true;
        }
        Pose sample = nativeDeathTrack.Sample(nativeDeathTime);
        Vector3 movement = Vector3.ProjectOnPlane(
            nativeDeathOrientation * (sample.position - nativeDeathPreviousSample.position), Vector3.up) * nativeDeathTravelScale;
        if (nativeKickDeath && nativeDeathTravelDirection.sqrMagnitude > .001f)
        {
            movement = nativeDeathTravelDirection * movement.magnitude;
        }
        float landingTime = ReadNativeFallLandingTime(nativeDeathTrack);
        float lift = 0f;
        if (string.IsNullOrEmpty(nativeDeathLanding))
        {
            movement = Vector3.ClampMagnitude(movement, Time.deltaTime);
        }
        else if (nativeKickDeath && nativeDeathTime < landingTime)
        {
            lift = ReadKickLift(nativeDeathTrack, sample);
        }
        bool findingLanding = !string.IsNullOrEmpty(nativeDeathLanding);
        host.CommitNativeCombatMotion(nativeDeathAction, movement, nativeDeathOrientation * sample.rotation, lift, true, out bool onSupport);
        nativeDeathPreviousSample = sample;
        float poseTime = nativeDeathTime;
        if (findingLanding && !onSupport)
        {
            poseTime = Mathf.Min(poseTime, Mathf.Max(0f, landingTime - 1f / nativeDeathTrack.clip.frameRate));
        }
        AnimationPresenter.PresentNativeCombat(nativeDeathTrack.clip, poseTime);
        if (findingLanding && onSupport && nativeDeathTime >= landingTime)
        {
            if (TryReadNativeCombatTrack(nativeDeathLanding, out var landing))
            {
                nativeDeathTrack = landing;
                nativeDeathTime = 0f;
                nativeDeathPreviousSample = landing.Sample(0f);
                nativeDeathOrientation = transform.rotation * Quaternion.Inverse(nativeDeathPreviousSample.rotation);
            }
            nativeDeathLanding = null;
        }
    }
}
