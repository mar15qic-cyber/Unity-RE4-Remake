using RootMotion.FinalIK;
using UnityEngine;

public sealed partial class EnemyAnimationAdapter
{
    private Transform meleeUpperArm;
    private Transform meleeForearm;
    private Transform meleeWrist;
    private Quaternion meleeUpperRotation;
    private Quaternion meleeForearmRotation;
    private Quaternion meleeWristRotation;
    private bool meleeArmAdjusted;

    internal bool TryPrepareMeleeReboundArm(HumanBodyBones hand, Vector3 offset, out Vector3 target)
    {
        target = default;
        Animator animator = anim;
        if (animator == null)
        {
            return false;
        }

        string side = "R_";
        if (hand == HumanBodyBones.LeftHand)
        {
            side = "L_";
        }

        meleeUpperArm = StandingMeleeBody.FindBone(animator.transform, side + "UpperArm");
        meleeForearm = StandingMeleeBody.FindBone(animator.transform, side + "Forearm");
        meleeWrist = StandingMeleeBody.FindBone(animator.transform, side + "Hand");
        if (meleeUpperArm == null || meleeForearm == null || meleeWrist == null)
        {
            ClearMeleeReboundArm();
            return false;
        }

        target = meleeWrist.position + offset;
        return true;
    }

    internal void ApplyMeleeReboundArm(Vector3 target, float weight)
    {
        if (meleeUpperArm == null || meleeForearm == null || meleeWrist == null)
        {
            return;
        }

        meleeUpperRotation = meleeUpperArm.localRotation;
        meleeForearmRotation = meleeForearm.localRotation;
        meleeWristRotation = meleeWrist.localRotation;
        meleeArmAdjusted = true;
        Quaternion wristRotation = meleeWrist.rotation;
        Vector3 upperDirection = meleeForearm.position - meleeUpperArm.position;
        Vector3 forearmDirection = meleeWrist.position - meleeForearm.position;
        Vector3 bend = Vector3.Cross(upperDirection, forearmDirection).normalized;
        if (bend.sqrMagnitude < .5f)
        {
            bend = transform.right;
        }

        IKSolverTrigonometric.Solve(meleeUpperArm, meleeForearm, meleeWrist, target, bend, weight);
        meleeWrist.rotation = wristRotation;
    }

    internal void ClearMeleeReboundArm()
    {
        RestoreMeleeArm();
        meleeUpperArm = null;
        meleeForearm = null;
        meleeWrist = null;
    }

    private void Update()
    {
        RestoreMeleeArm();
    }

    private void OnDisable()
    {
        ClearRevivedGait();
        StopNativeCombat();
        ClearMeleeReboundArm();
    }

    private void RestoreMeleeArm()
    {
        if (!meleeArmAdjusted)
        {
            return;
        }

        meleeArmAdjusted = false;
        if (meleeUpperArm != null && meleeForearm != null && meleeWrist != null)
        {
            meleeUpperArm.localRotation = meleeUpperRotation;
            meleeForearm.localRotation = meleeForearmRotation;
            meleeWrist.localRotation = meleeWristRotation;
        }
    }
}
