using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        private Transform meleeHip;
        private Transform meleeChest;
        private Transform meleeLeftArm;
        private Transform meleeLeftHand;
        private Transform meleeRightToe;
        private Transform meleeVisualRoot;
        private Quaternion meleeGroundTilt = Quaternion.identity;
        private Quaternion meleeGroundRestore;
        private bool meleeGroundApplied;
        private int meleeApproachAction;
        private ChapterOneMeleeMotion.Track meleeApproachTrack;
        private Vector3 meleeApproachStart;
        private Vector3 meleeApproach;
        private Quaternion meleeApproachInitialRotation;
        private Quaternion meleeApproachFacing;
        private float meleeApproachDuration;
        private float meleeImpactTime;

        internal void CacheMeleeJoints(bool player)
        {
            Transform[] joints = GetComponentsInChildren<Transform>(true);
            if (player)
            {
                pickupFinger = Array.Find(joints, joint =>
                    joint.name.Equals("arm left finger middle b", StringComparison.OrdinalIgnoreCase));
                meleeLeftArm = Array.Find(joints, joint => joint.name == "arm left forearm");
                meleeLeftHand = Array.Find(joints, joint => joint.name == "arm left wrist");
                if (rightFoot != null)
                {
                    meleeRightToe = Array.Find(rightFoot.GetComponentsInChildren<Transform>(true), joint =>
                        joint.name.IndexOf("toe", StringComparison.OrdinalIgnoreCase) >= 0
                        && joint.name.IndexOf("end", StringComparison.OrdinalIgnoreCase) < 0);
                }
            }
            else
            {
                meleeHip = Array.Find(joints, joint => joint.name == "Hip");
                meleeChest = Array.Find(joints, joint => joint.name == "Spine_2");
                meleeVisualRoot = Array.Find(joints, joint => joint.name == "root");
                CacheGroundFinishJoints(joints);
                CacheLegBiteMouth(joints);
            }
        }

        internal Vector3 MeleeTargetPoint(bool low)
        {
            if (meleeHip == null || meleeChest == null)
            {
                float height = 1.20f;
                if (low)
                {
                    height = 0.61f;
                }
                return transform.position + Vector3.up * height;
            }
            if (low)
            {
                return MeleeMotionPoint(Vector3.Lerp(meleeHip.position, meleeChest.position, 0.52f));
            }
            return MeleeMotionPoint(meleeChest.position) + Vector3.up * 0.10f;
        }

        internal Vector3 MeleeStrikePoint(string id)
        {
            if (id == "LeonInteract_0800_wp4000m" && rightHand != null)
            {
                return MeleeMotionPoint(rightHand.position);
            }
            if (id == "LeonInteract_0800" && meleeLeftArm != null && meleeLeftHand != null)
            {
                return MeleeMotionPoint(Vector3.Lerp(meleeLeftArm.position, meleeLeftHand.position, 0.55f));
            }
            if (rightFoot != null && meleeRightToe != null)
            {
                return MeleeMotionPoint(Vector3.Lerp(rightFoot.position, meleeRightToe.position, 0.72f));
            }
            return transform.position;
        }

        private Vector3 MeleeMotionPoint(Vector3 point)
        {
            if (body == null)
            {
                return point;
            }
            Vector3 relative = Quaternion.Inverse(transform.rotation) * (point - transform.position);
            return body.position + body.rotation * relative;
        }

        internal void SetMeleeApproach(ChapterOneMeleeMotion.Track track, Vector3 correction, Quaternion facing,
            int endFrame, float impactTime)
        {
            meleeApproachAction = actionId;
            meleeApproachTrack = track;
            meleeApproachStart = body.position;
            meleeApproach = correction;
            meleeApproachInitialRotation = body.rotation;
            meleeApproachFacing = facing;
            meleeApproachDuration = endFrame / track.root.clip.frameRate;
            meleeImpactTime = impactTime;
        }

        private Vector3 ApplyMeleeApproach(Vector3 delta, ref Quaternion facing)
        {
            if (actionId != meleeApproachAction || meleeApproachTrack == null || actionTime >= meleeImpactTime)
            {
                return delta;
            }
            // 命中回调先于下一次刚体推进，提前一物理步提交即将采样的原版位置。
            float time = Mathf.Min(actionTime + Time.fixedDeltaTime, meleeImpactTime);
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / meleeApproachDuration));
            facing = Quaternion.Slerp(meleeApproachInitialRotation, meleeApproachFacing, weight);
            Vector3 path = meleeApproachFacing * meleeApproachTrack.root.Sample(time).position;
            Vector3 expected = meleeApproachStart + meleeApproach * weight + path;
            Vector3 error = Vector3.ProjectOnPlane(expected - (body.position + delta), Vector3.up);
            return delta + Vector3.ClampMagnitude(error, Time.fixedDeltaTime * 2.5f);
        }

        private void RestoreMeleeGroundPose()
        {
            if (meleeGroundApplied && meleeVisualRoot != null)
            {
                meleeVisualRoot.localRotation = meleeGroundRestore;
                meleeGroundApplied = false;
            }
        }

        private void ApplyMeleeGroundPose()
        {
            if (meleeVisualRoot == null)
            {
                return;
            }
            Quaternion target = Quaternion.identity;
            if (take != null && (take.id is "Ganado_1016" or "Ganado_1101" or "Ganado_1106" or "Ganado_3011" or "Ganado_3510")
                && Physics.Raycast(body.position + Vector3.up * 0.30f, Vector3.down,
                    out var support, 0.60f, worldMask, QueryTriggerInteraction.Ignore)
                && support.normal.y > 0.70f)
            {
                float weight = 1f;
                if (take.id == "Ganado_3510")
                {
                    weight -= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((actionTime - 0.75f) / 0.65f));
                }
                target = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, support.normal), weight);
            }
            meleeGroundTilt = Quaternion.Slerp(meleeGroundTilt, target, 1f - Mathf.Exp(-Time.deltaTime / 0.085f));
            if (Quaternion.Angle(meleeGroundTilt, Quaternion.identity) > 0.05f)
            {
                meleeGroundRestore = meleeVisualRoot.localRotation;
                meleeVisualRoot.rotation = meleeGroundTilt * meleeVisualRoot.rotation;
                meleeGroundApplied = true;
            }
        }
    }
}
