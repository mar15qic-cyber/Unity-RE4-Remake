using RE4.ChapterOne;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public partial class EnemyWindowTraversal
{
    private const float NativeWindowSillDepth = 1.35f;
    private const float NativeWindowSideOffset = -0.12f;
    private const float NativeWindowEntryBlend = 0.24f;
    private const float NativeWindowLandingBlend = 0.10f;
    private const float NativeWindowExitBlend = 0.18f;
    private const float NativeWindowMaxFrameStep = 0.05f;
    private const int NativeWindowLandingEndFrame = 70;

    [SerializeField] private EnemyHeldWeapon nativeWindowWeapon;
    [SerializeField] private ChapterOnePairMotion.Track nativeWindowCross;
    [SerializeField] private ChapterOnePairMotion.Track nativeWindowLand;
    [SerializeField, Tooltip("持械村民脱手后使用的同骨架原版翻窗和落地动作。")]
    private AnimationClip unarmedWindowCross;
    [SerializeField] private AnimationClip unarmedWindowLand;
    private ChapterOnePairMotion.Track crossingTrack;
    private ChapterOnePairMotion.Track landingTrack;

    private AnimationMixerPlayable nativeWindowMixer;
    private AnimationClipPlayable nativeLandingPlayable;
    private AnimationPlayableOutput nativeWindowOutput;
    private Vector3 nativeWindowFloor;
    private Vector3 nativeWindowStartBias;
    private Vector3 nativeWindowContact;
    private Vector3 nativeWindowLandingDelta;
    private float nativeWindowRise;
    private float nativeWindowEntryTime;
    private float nativeWindowContactTime;
    private float nativeWindowLandingDuration;
    private float nativeWindowBrakeProgress;
    private float nativeWindowTime;
    private float nativeWindowExitDepth;
    private float nativeWindowExitCorrection;

#if UNITY_EDITOR
    internal void ConfigureNativeWindow(EnemyHeldWeapon weapon, ChapterOnePairMotion.Track crossing,
        ChapterOnePairMotion.Track landing)
    {
        if (weapon != null && nativeWindowWeapon == null && nativeWindowCross != null && nativeWindowLand != null)
        {
            unarmedWindowCross = nativeWindowCross.clip;
            unarmedWindowLand = nativeWindowLand.clip;
        }
        nativeWindowWeapon = weapon;
        nativeWindowCross = crossing;
        nativeWindowLand = landing;
    }
#endif

    private bool CanUseNativeWindow(EnemyWindowTraversalPlan plan)
    {
        crossingTrack = nativeWindowCross;
        landingTrack = nativeWindowLand;
        if (nativeWindowWeapon != null && !nativeWindowWeapon.IsHeld && unarmedWindowCross != null && unarmedWindowLand != null)
        {
            var library = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            if (library != null && library.TryGetTrack(unarmedWindowCross, out var crossing)
                && library.TryGetTrack(unarmedWindowLand, out var landing))
            {
                crossingTrack = crossing;
                landingTrack = landing;
            }
        }
        if (animator == null || crossingTrack == null || !crossingTrack.Matches(crossingTrack.clip)
            || crossingTrack.positions.Length <= 155 || landingTrack == null || !landingTrack.Matches(landingTrack.clip)
            || landingTrack.positions.Length <= NativeWindowLandingEndFrame)
        {
            return false;
        }

        var window = plan.route.windowPoint;
        float sill = window.OpeningCenterWorld.y - window.openingHeight * 0.5f;
        float rise = sill - plan.sourceGround.y;
        float drop = plan.sourceGround.y - plan.destinationGround.y;
        float availableFall = -crossingTrack.positions[crossingTrack.positions.Length - 1].y;
        return window.openingWidth >= 1f && window.openingHeight >= 1.25f
            && rise >= 0.75f && rise <= 1.2f && drop >= -.30f
            && drop <= Mathf.Max(.30f, availableFall + .035f);
    }

    private readonly Collider[] nativeWindowLandingHits = new Collider[24];
    private static readonly float[] NativeLandingOffsets = { 0.32f, -0.32f, 0.56f, -0.56f };

    internal bool IsWindowLandingClear(EnemyWindowTraversalPlan plan)
    {
        if (plan.route == null || plan.route.windowPoint == null)
        {
            return false;
        }

        EnemyWindowTraversalPlan probePlan = plan;
        return TryResolveNativeWindowLanding(ref probePlan);
    }

    private bool TryResolveNativeWindowLanding(ref EnemyWindowTraversalPlan plan)
    {
        Vector3 landing = plan.destinationGround;
        if (NativeWindowLandingClear(plan.route.windowPoint, landing))
        {
            return true;
        }

        Vector3 side = Vector3.Cross(Vector3.up, plan.travelDirection);
        foreach (float offset in NativeLandingOffsets)
        {
            if (plan.route.TryResolveAlternateLanding(landing + side * offset, landing, out Vector3 point)
                && NativeWindowLandingClear(plan.route.windowPoint, point))
            {
                plan.destinationGround = point;
                return true;
            }
        }
        return false;
    }

    private bool NativeWindowLandingClear(WindowTraversalPoint window, Vector3 point)
    {
        float radius = 0.26f;
        if (movementCollider != null)
        {
            radius = movementCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        }

        Vector3 bottom = point + Vector3.up * (radius + 0.06f);
        Vector3 top = bottom + Vector3.up * 0.88f;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, nativeWindowLandingHits,
            window.obstacleMask, QueryTriggerInteraction.Ignore);
        if (count >= nativeWindowLandingHits.Length)
        {
            return false;
        }
        for (int index = 0; index < count; index++)
        {
            var collider = nativeWindowLandingHits[index];
            if (!collider.transform.IsChildOf(transform) && !window.ShouldIgnoreCollider(collider))
            {
                // 倒地后仍保留的命中盒不参与角色通行。
                var enemy = collider.GetComponentInParent<VillagerController>();
                if (enemy != null && enemy.IsDead && !enemy.IsNavigationTraversalProtected)
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    private bool BeginNativeWindow(EnemyWindowTraversalPlan plan)
    {
        if (!CanUseNativeWindow(plan))
        {
            return false;
        }

        var window = plan.route.windowPoint;
        Vector3 center = window.OpeningCenterWorld;
        nativeWindowFloor = new Vector3(center.x, plan.sourceGround.y, center.z);
        nativeWindowRise = center.y - window.openingHeight * 0.5f - plan.sourceGround.y;
        Vector3 forward = motionRotation * Vector3.forward;
        float startDepth = Vector3.Dot(plan.sourceGround - nativeWindowFloor, forward);
        float targetEntry = NativeWindowSillDepth + startDepth;
        float fps = crossingTrack.clip.frameRate;
        int entryFrame = 0;
        while (entryFrame < 65 && crossingTrack.positions[entryFrame].z < targetEntry)
        {
            entryFrame++;
        }
        nativeWindowEntryTime = entryFrame / fps;

        float contactHeight = motionDestination.y + 0.035f;
        int contactFrame = 155;
        while (contactFrame < crossingTrack.positions.Length - 1 && NativeWindowHeight(crossingTrack.positions[contactFrame].y) > contactHeight)
        {
            contactFrame++;
        }
        float before = NativeWindowHeight(crossingTrack.positions[contactFrame - 1].y);
        float after = NativeWindowHeight(crossingTrack.positions[contactFrame].y);
        nativeWindowContactTime = (contactFrame - 1 + Mathf.InverseLerp(before, after, contactHeight)) / fps;

        var contactRoot = crossingTrack.Sample(nativeWindowContactTime).position;
        nativeWindowExitDepth = Mathf.Max(0.01f, contactRoot.z - NativeWindowSillDepth);
        float landingDepth = Vector3.Dot(motionDestination - nativeWindowFloor, forward);
        nativeWindowExitCorrection = Mathf.Min(0f, landingDepth - 0.32f - nativeWindowExitDepth);
        nativeWindowContact = NativeWindowCrossPosition(contactRoot);
        nativeWindowStartBias = motionStart - NativeWindowCrossPosition(crossingTrack.Sample(nativeWindowEntryTime).position);
        nativeWindowLandingDuration = NativeWindowLandingEndFrame / landingTrack.clip.frameRate;
        nativeWindowLandingDelta = landingTrack.Sample(nativeWindowLandingDuration).position;
        nativeWindowBrakeProgress = landingTrack.Sample(nativeWindowLandingDuration - NativeWindowExitBlend).position.z
            / nativeWindowLandingDelta.z;
        nativeWindowTime = 0f;

        StopTraversalClip();
        animationGraph = PlayableGraph.Create("敌人原版翻窗");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        clipPlayable = CreateNativeWindowClip(crossingTrack.clip, nativeWindowEntryTime);
        nativeLandingPlayable = CreateNativeWindowClip(landingTrack.clip, 0f);
        nativeWindowMixer = AnimationMixerPlayable.Create(animationGraph, 2);
        animationGraph.Connect(clipPlayable, 0, nativeWindowMixer, 0);
        animationGraph.Connect(nativeLandingPlayable, 0, nativeWindowMixer, 1);
        nativeWindowMixer.SetInputWeight(0, 1f);
        nativeWindowOutput = AnimationPlayableOutput.Create(animationGraph, "敌人原版翻窗输出", animator);
        nativeWindowOutput.SetSourcePlayable(nativeWindowMixer);
        nativeWindowOutput.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        nativeWindowOutput.SetSortingOrder(100);
        nativeWindowOutput.SetWeight(0f);
        animationGraph.Play();
        return true;
    }

    private AnimationClipPlayable CreateNativeWindowClip(AnimationClip clip, float time)
    {
        var playable = AnimationClipPlayable.Create(animationGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(0);
        playable.SetTime(time);
        return playable;
    }

    private void UpdateNativeWindow()
    {
        // 卡帧时根轨和姿势共用限步时钟，保留进场、落地和退出的混合区间。
        nativeWindowTime += Mathf.Min(Time.deltaTime, NativeWindowMaxFrameStep);
        float time = nativeWindowTime;
        float crossDuration = nativeWindowContactTime - nativeWindowEntryTime;
        float sourceTime = Mathf.Min(nativeWindowEntryTime + time, nativeWindowContactTime);
        float landTime = Mathf.Clamp(time - crossDuration, 0f, nativeWindowLandingDuration);
        float entryWeight = Mathf.SmoothStep(0f, 1f, time / NativeWindowEntryBlend);
        float landingWeight = Mathf.SmoothStep(0f, 1f, landTime / NativeWindowLandingBlend);
        float remaining = crossDuration + nativeWindowLandingDuration - time;
        float exitWeight = Mathf.SmoothStep(0f, 1f, remaining / NativeWindowExitBlend);

        clipPlayable.SetTime(sourceTime);
        nativeLandingPlayable.SetTime(landTime);
        nativeWindowMixer.SetInputWeight(0, 1f - landingWeight);
        nativeWindowMixer.SetInputWeight(1, landingWeight);
        nativeWindowOutput.SetWeight(entryWeight * exitWeight);

        Vector3 position;
        if (landTime <= 0f)
        {
            var root = crossingTrack.Sample(sourceTime).position;
            position = NativeWindowCrossPosition(root) + nativeWindowStartBias * (1f - entryWeight);
        }
        else
        {
            var root = landingTrack.Sample(landTime).position;
            float progress = Mathf.Clamp01(root.z / nativeWindowLandingDelta.z);
            if (progress > nativeWindowBrakeProgress)
            {
                float brake = Mathf.InverseLerp(nativeWindowBrakeProgress, 1f, progress);
                float eased = brake + brake * brake * (1f - brake);
                progress = Mathf.Lerp(nativeWindowBrakeProgress, 1f, eased);
            }
            position = Vector3.Lerp(nativeWindowContact, motionDestination, progress);
            position += motionRotation * new Vector3(root.x - nativeWindowLandingDelta.x * progress, 0f, 0f);
            position.y = Mathf.Lerp(nativeWindowContact.y, motionDestination.y, landingWeight);
        }

        if (CommitTraversalPose(position, Quaternion.Slerp(motionStartRotation, motionRotation, entryWeight)))
        {
            FractureWindowOnCrossing(position);
        }
        if (remaining <= 0f)
        {
            CompleteTraversal();
        }
    }

    private float NativeWindowHeight(float rootHeight)
    {
        if (rootHeight >= 0f)
        {
            return nativeWindowFloor.y + rootHeight * nativeWindowRise;
        }
        return nativeWindowFloor.y + rootHeight;
    }

    private Vector3 NativeWindowCrossPosition(Vector3 root)
    {
        float depth = root.z - NativeWindowSillDepth;
        float warped = depth + nativeWindowExitCorrection * Mathf.SmoothStep(0f, 1f, depth / nativeWindowExitDepth);
        if (depth > 0f && nativeWindowFloor.y - motionDestination.y > .30f)
        {
            // 保留越过窗台前的原轨迹；高窗落点较近时只压缩窗外段的水平距离。
            warped = depth * (nativeWindowExitDepth + nativeWindowExitCorrection) / nativeWindowExitDepth;
        }
        Vector3 position = nativeWindowFloor + motionRotation * new Vector3(root.x + NativeWindowSideOffset, 0f, warped);
        position.y = NativeWindowHeight(root.y);
        return position;
    }
}
