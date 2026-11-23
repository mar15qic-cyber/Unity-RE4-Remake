using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        internal static readonly (string player, string enemy, string label, int contact)[] GroundFinishes =
        {
            ("LeonPair_Ground1000", "GanadoPair_Ground1000", "俯卧小刀处决", 38),
            ("LeonPair_Ground1010", "GanadoPair_Ground1010", "仰卧小刀处决", 35),
            ("LeonPair_Ground1035", "GanadoPair_Ground1035", "跪姿小刀处决", 36)
        };

        private Transform groundFinishLeft;
        private Transform groundFinishRight;
        private Transform groundFinishNeck;
        private Transform groundFinishHead;

        internal static bool IsGroundFinishClip(string id) =>
            id != null && (id.StartsWith("LeonPair_Ground", StringComparison.Ordinal)
                || id.StartsWith("GanadoPair_Ground", StringComparison.Ordinal));

        private void RegisterGroundFinishes(bool player)
        {
            foreach (var finish in GroundFinishes)
            {
                if (player)
                {
                    RegisterGeneratedTake(finish.player, finish.label, "wp5000");
                    if (byId.TryGetValue(finish.player, out var action))
                    {
                        action.partnerId = finish.enemy;
                    }
                }
                else
                {
                    RegisterGeneratedTake(finish.enemy, finish.label, "");
                }
            }
        }

        private void CacheGroundFinishJoints(Transform[] joints)
        {
            groundFinishLeft = Array.Find(joints, joint => joint.name == "L_Thigh");
            groundFinishRight = Array.Find(joints, joint => joint.name == "R_Thigh");
            groundFinishNeck = Array.Find(joints, joint => joint.name == "Neck_0");
            groundFinishHead = Array.Find(joints, joint => joint.name == "Head");
        }

        internal static Vector3 ReadGroundBodyRight(Transform root)
        {
            var joints = root.GetComponentsInChildren<Transform>(true);
            var left = Array.Find(joints, joint => joint.name == "L_Thigh");
            var right = Array.Find(joints, joint => joint.name == "R_Thigh");
            if (left == null || right == null)
            {
                return Vector3.zero;
            }
            return Vector3.ProjectOnPlane(right.position - left.position, Vector3.up).normalized;
        }

        internal Vector3 GroundFinishBodyRight()
        {
            if (groundFinishLeft == null || groundFinishRight == null)
            {
                return Vector3.zero;
            }
            Vector3 world = MeleeMotionPoint(groundFinishRight.position) - MeleeMotionPoint(groundFinishLeft.position);
            return Vector3.ProjectOnPlane(world, Vector3.up).normalized;
        }

        internal float GroundFinishKnifeGap(Vector3 bladeBase, Vector3 bladeTip)
        {
            if (groundFinishNeck == null || groundFinishHead == null)
            {
                return float.PositiveInfinity;
            }
            Vector3 blade = bladeTip - bladeBase;
            float Distance(Vector3 point)
            {
                Vector3 offset = MeleeMotionPoint(point) - bladeBase;
                float ratio = Mathf.Clamp01(Vector3.Dot(offset, blade) / Mathf.Max(blade.sqrMagnitude, 0.0001f));
                return Vector3.Distance(offset, blade * ratio);
            }
            return Mathf.Min(Distance(groundFinishNeck.position), Distance(groundFinishHead.position));
        }
    }
}
