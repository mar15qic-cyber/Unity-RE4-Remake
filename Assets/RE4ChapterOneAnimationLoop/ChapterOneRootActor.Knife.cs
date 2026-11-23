using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        private static readonly Vector3 KnifeBladeBase = new(0f, 0.004f, 0.074f);
        private static readonly Vector3 KnifeBladeTip = new(0f, 0.0041f, 0.2673f);

        internal event Action<Take, int, float, Vector3, Vector3> KnifeBladeSample;

        internal static (int first, int last) KnifeHitWindow(string id) => id switch
        {
            "Knife_0600" => (12, 20),
            "Knife_0630" => (13, 23),
            "Knife_0640" => (15, 26),
            "Knife_0650" => (21, 32),
            "Knife_0660" => (22, 33),
            _ => (-1, -1)
        };

        internal static int KnifeMoveFrame(string id) => id switch
        {
            "Knife_0600" => 31,
            "Knife_0601" => 21,
            "Knife_0630" or "Knife_0640" or "Knife_0650" => 46,
            "Knife_0660" => 66,
            "Knife_0146" or "Knife_0800" => 41,
            _ => -1
        };

        internal static string KnifeTailId(string id) => id + "_Tail";

        internal static bool IsKnifeTail(string id) => id != null
            && id.StartsWith("Knife_", StringComparison.Ordinal) && id.EndsWith("_Tail", StringComparison.Ordinal);

        private void RegisterKnifeTails()
        {
            foreach (string id in new[] { "Knife_0600", "Knife_0601", "Knife_0630", "Knife_0640", "Knife_0650",
                         "Knife_0660", "Knife_0146", "Knife_0800" })
            {
                string tail = KnifeTailId(id);
                if (!byId.TryGetValue(id, out var source)
                    || !animator.HasState(weaponLayer, Animator.StringToHash(WeaponLayerName + "." + tail)))
                {
                    continue;
                }
                RegisterTake(new Take
                {
                    id = tail,
                    label = source.label + "收势",
                    body = source.body,
                    weapon = source.weapon,
                    contactFrames = Array.Empty<int>()
                });
            }
        }

        private void SampleKnifeBlade(Take action)
        {
            if (KnifeHitWindow(action.id).first < 0 || !TryGetKnifeBlade(out var bladeBase, out var bladeTip))
            {
                return;
            }
            KnifeBladeSample?.Invoke(action, actionId, actionTime, bladeBase, bladeTip);
        }

        internal bool TryGetKnifeBlade(out Vector3 bladeBase, out Vector3 bladeTip)
        {
            bladeBase = Vector3.zero;
            bladeTip = Vector3.zero;
            if (!HasHeldModel || held.id != "wp5000")
            {
                return false;
            }
            bladeBase = MeleeMotionPoint(held.model.TransformPoint(KnifeBladeBase));
            bladeTip = MeleeMotionPoint(held.model.TransformPoint(KnifeBladeTip));
            return true;
        }

        private float PrepareKnifeLocomotion(string id)
        {
            planarVelocity = Vector3.ProjectOnPlane(ActualStep, Vector3.up) / Time.fixedDeltaTime;
            if (id != "Knife_0660_Tail" || mode != 0 || speed < 2f)
            {
                return 0f;
            }
            // 第 66 源帧的右脚支撑与向前摆腿对应这两个步态相位。
            if (speed >= 2.5f)
            {
                return 10f / 34f;
            }
            return 8f / 47f;
        }
    }
}
