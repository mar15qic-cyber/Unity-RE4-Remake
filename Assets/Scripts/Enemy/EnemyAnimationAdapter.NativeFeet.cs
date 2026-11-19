using RE4.ChapterOne;
using UnityEngine;

public sealed partial class EnemyAnimationAdapter
{
    [SerializeField] private Vector4 nativeWalkFootFrames;
    [SerializeField] private Vector4 nativeRunFootFrames;
    private NativeFootContact nativeLeftFoot;
    private NativeFootContact nativeRightFoot;
    private bool nativeFeetInitialized;
    private bool hasNativeFootRootSample;
    private Vector3 lastNativeFootRoot;
    private Vector3 lastNativeFootForward;
    private float nativeStraightHeadingTime;

    internal void ConfigureNativeFootContacts(bool male)
    {
        // 每组依次是左右脚的源帧区间，结束帧不含在区间内。右脚支撑可能跨过循环边界。
        if (male)
        {
            nativeWalkFootFrames = new Vector4(19f, 72f, 64f, 118f);
            nativeRunFootFrames = new Vector4(20f, 36f, 0f, 15f);
        }
        else
        {
            nativeWalkFootFrames = new Vector4(20f, 74f, 65f, 121f);
            nativeRunFootFrames = new Vector4(19f, 35f, 40f, 56f);
        }
    }

#if UNITY_EDITOR
    internal void ConfigureNativeArmedGait()
    {
        nativeArmedGait = true;
        nativeWalkFootFrames = new Vector4(34f, 84f, 82f, 127f);
        nativeRunFootFrames = new Vector4(0f, 17f, 24f, 40f);
    }
    internal void ConfigureNativeSawGait()
    {
        nativeSawGait = true;
        nativeWalkFootFrames = new Vector4(34f, 86f, 82f, 136f);
        nativeRunFootFrames = new Vector4(29f, 52f, 0f, 26f);
    }
#endif

    internal void RestoreNativeFootPose()
    {
        if (nativeRightFoot != null)
        {
            nativeRightFoot.Restore();
        }
        if (nativeLeftFoot != null)
        {
            nativeLeftFoot.Restore();
        }
    }

    internal void ResetNativeFootContacts()
    {
        RestoreNativeFootPose();
        if (nativeLeftFoot != null)
        {
            nativeLeftFoot.Release();
        }
        if (nativeRightFoot != null)
        {
            nativeRightFoot.Release();
        }
        hasNativeFootRootSample = false;
        nativeStraightHeadingTime = 0f;
    }

    internal void UpdateNativeFootContacts(bool pathIntent, Vector3 requestedStep)
    {
        if (HasRevivedGait)
        {
            ResetNativeFootContacts();
            return;
        }
        if (!BipedFootPlanting.EnemyGroundIKEnabled)
        {
            return;
        }

        if (!hasNativeGaitPace || !host.UsesNativeStandingStride || Time.deltaTime <= 0f
            || nativeWalkFootFrames == Vector4.zero && nativeRunFootFrames == Vector4.zero)
        {
            return;
        }

        if (!host.IsActive || host.IsDead || host.IsQTEControlled || host.IsNavigationTraversalProtected || host.IsCombatFalling
            || host.knifeLocalHitStop != null && host.knifeLocalHitStop.IsHitStop
            || CurrentState == VillagerState.Staggered || CurrentState == VillagerState.Attack
            || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion && CurrentMovementMode != EnemyMovementMode.FullyStopped)
        {
            ResetNativeFootContacts();
            return;
        }

        if (!nativeFeetInitialized)
        {
            nativeFeetInitialized = true;
            Transform hips = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Hips);
            nativeLeftFoot = BindNativeFoot(hips, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftToes);
            nativeRightFoot = BindNativeFoot(hips, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightToes);
        }
        if (nativeLeftFoot == null || nativeRightFoot == null)
        {
            return;
        }

        Vector3 displacement = transform.position - lastNativeFootRoot;
        Vector3 planar = Vector3.ProjectOnPlane(displacement, Vector3.up);
        float requested = requestedStep.magnitude;
        float moved = planar.magnitude;
        float turnRate = Vector3.Angle(lastNativeFootForward, transform.forward) / Time.deltaTime;
        bool followsStride = hasNativeFootRootSample && pathIntent && Time.deltaTime < 0.15f
            && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion
            && requested > 0.0005f && moved >= requested * 0.65f && moved <= requested * 1.35f + 0.005f
            && Mathf.Abs(displacement.y) <= 0.02f + moved * 0.22f
            && Vector3.Angle(planar, transform.forward) < 18f
            && turnRate <= 50f;

        lastNativeFootRoot = transform.position;
        lastNativeFootForward = transform.forward;
        hasNativeFootRootSample = true;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        bool steady = followsStride && nativeGaitPace > 0 && state.fullPathHash == HashNativeLocomotion && !anim.IsInTransition(0);
        if (steady)
        {
            nativeStraightHeadingTime += Time.deltaTime;
        }
        else
        {
            nativeStraightHeadingTime = 0f;
        }
        if (nativeStraightHeadingTime < 0.045f)
        {
            nativeLeftFoot.Apply(false);
            nativeRightFoot.Apply(false);
            return;
        }

        Vector4 contacts = nativeWalkFootFrames;
        float cycleFrames = 83f;
        if (nativeGaitPace == 2)
        {
            contacts = nativeRunFootFrames;
            cycleFrames = 41f;
        }
        if (nativeArmedGait)
        {
            cycleFrames = 84f;
            if (nativeGaitPace == 2)
            {
                cycleFrames = 45f;
            }
        }
        if (nativeSawGait)
        {
            cycleFrames = 92f;
            if (nativeGaitPace == 2)
            {
                cycleFrames = 53f;
            }
        }
        float frame = Mathf.Repeat(state.normalizedTime, 1f) * cycleFrames;
        nativeLeftFoot.Apply(InNativeContact(frame, cycleFrames, contacts.x, contacts.y));
        nativeRightFoot.Apply(InNativeContact(frame, cycleFrames, contacts.z, contacts.w));
    }

    private NativeFootContact BindNativeFoot(Transform hips, HumanBodyBones foot, HumanBodyBones lower,
        HumanBodyBones upper, HumanBodyBones toes)
    {
        Transform ankle = AnimatorBoneBindings.Resolve(anim, foot);
        Transform knee = AnimatorBoneBindings.Resolve(anim, lower);
        Transform thigh = AnimatorBoneBindings.Resolve(anim, upper);
        Transform toe = AnimatorBoneBindings.Resolve(anim, toes);
        if (hips == null || ankle == null || knee == null || thigh == null || toe == null
            || !thigh.IsChildOf(hips) || !knee.IsChildOf(thigh) || !ankle.IsChildOf(knee) || !toe.IsChildOf(ankle))
        {
            return null;
        }
        return new NativeFootContact(new ChapterOneFootPlant(ankle, knee, thigh, hips, toe));
    }

    private static bool InNativeContact(float frame, float cycle, float first, float end)
        => Mathf.Repeat(frame - first, cycle) < end - first;

    private sealed class NativeFootContact
    {
        private readonly ChapterOneFootPlant solver;
        private Vector3 anchor;
        private bool planted;
        private float weight;

        public NativeFootContact(ChapterOneFootPlant solver)
        {
            this.solver = solver;
        }

        public void Restore() => solver.Restore();

        public void Release()
        {
            planted = false;
            weight = 0f;
        }

        public void Apply(bool contact)
        {
            if (contact)
            {
                if (!planted)
                {
                    anchor = solver.Position;
                    solver.BeginContact();
                }
                weight = 1f;
            }
            else
            {
                weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / 0.075f);
            }
            planted = contact;

            if (weight > 0f)
            {
                solver.Capture();
                solver.Apply(anchor, Mathf.SmoothStep(0f, 1f, weight), false, maximumCorrection: 0.12f, allowHipLowering: false);
            }
        }
    }
}
