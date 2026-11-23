using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        internal const string LegBiteEnter = "LeonPair_LegBiteEnter";
        internal const string LegBiteHold = "LeonPair_LegBiteHold";
        internal const string LegBiteEscape = "LeonPair_LegBiteEscape";
        internal const string LegBiteFailure = "LeonPair_LegBiteFailure";
        internal static readonly (string player, string enemy, string label)[] LegBiteTakes =
        {
            (LegBiteEnter, "GanadoPair_LegBiteEnter", "倒地咬腿入场"),
            (LegBiteHold, "GanadoPair_LegBiteHold", "倒地咬腿挣扎"),
            (LegBiteEscape, "GanadoPair_LegBiteEscape", "倒地咬腿挣脱"),
            (LegBiteFailure, "GanadoPair_LegBiteFailureFit", "倒地咬腿受伤")
        };

        private Transform[] biteMouth;

        internal static bool IsLegBiteClip(string id) =>
            id != null && id.StartsWith("LeonPair_LegBite", StringComparison.Ordinal);

        private void RegisterLegBite(bool player)
        {
            foreach (var stage in LegBiteTakes)
            {
                if (player)
                {
                    RegisterGeneratedTake(stage.player, stage.label, "");
                    if (byId.TryGetValue(stage.player, out var action))
                    {
                        action.partnerId = stage.enemy;
                    }
                }
                else
                {
                    RegisterGeneratedTake(stage.enemy, stage.label, "");
                }
            }
        }

        private void CacheLegBiteMouth(Transform[] joints)
        {
            Transform[] marks = new Transform[4];
            string[] names = { "L_U_MouthCorner", "R_U_MouthCorner", "L_L_MouthCorner", "R_L_MouthCorner" };
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i] = Array.Find(joints, joint => joint.name == names[i]);
                if (marks[i] == null)
                {
                    return;
                }
            }
            biteMouth = marks;
        }

        internal float LegBiteGap(ChapterOneRootActor player, out Vector3 jaw, out Vector3 shin)
        {
            jaw = Vector3.zero;
            shin = Vector3.zero;
            if (biteMouth == null || player.leftFoot == null || player.leftFoot.parent == null)
            {
                return float.PositiveInfinity;
            }
            foreach (Transform mark in biteMouth)
            {
                jaw += MeleeMotionPoint(mark.position) * 0.25f;
            }
            Vector3 ankle = player.MeleeMotionPoint(player.leftFoot.position);
            Vector3 leg = player.MeleeMotionPoint(player.leftFoot.parent.position) - ankle;
            shin = ankle + leg * Mathf.Clamp(Vector3.Dot(jaw - ankle, leg) / Mathf.Max(leg.sqrMagnitude, 0.0001f), 0.12f, 0.88f);
            return Vector3.Distance(jaw, shin);
        }
    }
}
