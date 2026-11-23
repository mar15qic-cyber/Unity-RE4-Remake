using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const float KnifeBladeRadius = 0.043f;
        private const float PerfectKnifeWindow = 0.11f;

        private readonly Collider[] knifeContacts = new Collider[16];
        private readonly HashSet<Threat> knifeVictims = new();
        private int knifeSampleAction;
        private float previousKnifeTime;
        private Vector3 previousKnifeBase;
        private Vector3 previousKnifeTip;
        private int thrustHitAction;

        private bool TryKnifeAttack(bool thrust)
        {
            var active = leon.ActiveTake;
            if (active != null && TryKnifeCombo(active.id, out int frame, out _, out _)
                && leon.ActionTime * active.body.frameRate <= frame + 5f)
            {
                queuedKnifeAction = leon.ActionId;
                return true;
            }
            if (leon.IsBusy && !IsMovementTransition(active) && !IsAimTransition(active)
                && (active == null || !ChapterOneRootActor.IsKnifeTail(active.id)))
            {
                return false;
            }
            string id = "Knife_0630";
            if (thrust)
            {
                id = "Knife_0600";
            }
            else if (gait >= 2f)
            {
                id = "Knife_0660";
            }
            return leon.Play(id, true);
        }

        private static bool TryKnifeCombo(string id, out int frame, out string next, out int start)
        {
            frame = 0;
            next = null;
            start = 0;
            if (id == "Knife_0630")
            {
                frame = 35;
                next = "Knife_0640";
                start = 1;
            }
            else if (id == "Knife_0640")
            {
                frame = 45;
                next = "Knife_0650";
            }
            return next != null;
        }

        private void UpdateKnifeAction()
        {
            var action = leon.ActiveTake;
            if (slot != 3 || action == null || !leon.BlocksLocomotion)
            {
                return;
            }
            float frame = leon.ActionTime * action.body.frameRate;
            int id = leon.ActionId;
            if (thrustHitAction == id && action.id == "Knife_0600")
            {
                thrustHitAction = 0;
                // 前刺与命中收势在 0600 的第 13 帧有同姿势的起点。
                float sourceTime = Mathf.Max(0f, frame - 13f) / action.body.frameRate;
                leon.Play("Knife_0601", true, sourceTime: sourceTime, actionFade: 0.08f);
                return;
            }
            if (queuedKnifeAction == id && TryKnifeCombo(action.id, out int comboFrame, out string next, out int nextFrame)
                && frame >= comboFrame)
            {
                queuedKnifeAction = 0;
                leon.Play(next, true, sourceTime: nextFrame / action.body.frameRate, actionFade: 0.12f);
                return;
            }
            int moveFrame = ChapterOneRootActor.KnifeMoveFrame(action.id);
            if (gait > 0f && moveFrame >= 0 && frame >= moveFrame)
            {
                // 走跑解锁后，沿用当前上身源帧，让基础层接回步态。
                leon.Play(ChapterOneRootActor.KnifeTailId(action.id), true, sourceTime: leon.ActionTime, actionFade: 0.14f);
            }
        }

        private void OnKnifeBladeSample(ChapterOneRootActor.Take action, int id, float time, Vector3 bladeBase, Vector3 bladeTip)
        {
            if (slot != 3)
            {
                return;
            }
            if (knifeSampleAction != id)
            {
                knifeSampleAction = id;
                knifeVictims.Clear();
                previousKnifeTime = time;
                previousKnifeBase = bladeBase;
                previousKnifeTip = bladeTip;
                return;
            }
            if (time <= previousKnifeTime)
            {
                return;
            }
            var window = ChapterOneRootActor.KnifeHitWindow(action.id);
            float begin = Mathf.Max(previousKnifeTime, window.first / action.body.frameRate);
            float end = Mathf.Min(time, (window.last + 1f) / action.body.frameRate);
            if (end > begin)
            {
                float sample = time - previousKnifeTime;
                float from = (begin - previousKnifeTime) / sample;
                float to = (end - previousKnifeTime) / sample;
                bool hit = SweepKnife(
                    Vector3.Lerp(previousKnifeBase, bladeBase, from), Vector3.Lerp(previousKnifeTip, bladeTip, from),
                    Vector3.Lerp(previousKnifeBase, bladeBase, to), Vector3.Lerp(previousKnifeTip, bladeTip, to));
                if (hit && action.id == "Knife_0600")
                {
                    thrustHitAction = id;
                }
            }
            previousKnifeTime = time;
            previousKnifeBase = bladeBase;
            previousKnifeTip = bladeTip;
        }

        private bool SweepKnife(Vector3 fromBase, Vector3 fromTip, Vector3 toBase, Vector3 toTip)
        {
            float distance = Mathf.Max(Vector3.Distance(fromBase, toBase), Vector3.Distance(fromTip, toTip));
            int segments = Mathf.Max(1, Mathf.CeilToInt(distance / 0.025f));
            bool hit = false;
            for (int i = 0; i <= segments; i++)
            {
                float blend = (float)i / segments;
                Vector3 edgeBase = Vector3.Lerp(fromBase, toBase, blend);
                Vector3 edgeTip = Vector3.Lerp(fromTip, toTip, blend);
                int count = Physics.OverlapCapsuleNonAlloc(edgeBase, edgeTip, KnifeBladeRadius,
                    knifeContacts, 1 << 10, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++)
                {
                    Collider contact = knifeContacts[j];
                    var actor = contact.GetComponentInParent<ChapterOneRootActor>();
                    Threat victim = System.Array.Find(threats, item => item.actor == actor && item.health > 0);
                    if (victim == null || knifeVictims.Contains(victim))
                    {
                        continue;
                    }
                    Vector3 point = contact.ClosestPoint((edgeBase + edgeTip) * 0.5f);
                    Vector3 bodyOrigin = leon.MotionPosition;
                    bodyOrigin.y = Mathf.Clamp(point.y, bodyOrigin.y + 0.45f, bodyOrigin.y + 1.42f);
                    if (Physics.Linecast(bodyOrigin, point, obstructionMask, QueryTriggerInteraction.Ignore)
                        || Physics.Linecast(leon.RightHand.position, point, obstructionMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    knifeVictims.Add(victim);
                    DamageEnemy(victim, 38);
                    hit = true;
                }
            }
            return hit;
        }

        private void PlayKnifeGuardTransition(float actionFade = 0f)
        {
            if (leon.ContinueAimTransition(aim))
            {
                return;
            }
            string id = "Knife_0158";
            if (aim)
            {
                id = "Knife_0140";
            }
            leon.Play(id, true, actionFade: actionFade);
        }

        private static bool IsKnifeParry(string id) => id is "Knife_0146" or "Knife_0800";

        private void PlayKnifeParry(bool perfect)
        {
            string id = "Knife_0146";
            if (perfect)
            {
                id = "Knife_0800";
            }
            leon.Play(id, true, actionFade: 0.09f);
        }

        private bool TryParryEnemy(Threat source, Vector3 incoming)
        {
            if (slot != 3 || Vector3.Angle(leon.transform.forward, incoming) >= 85f)
            {
                return false;
            }
            var current = leon.ActiveTake;
            if (current != null && IsKnifeParry(current.id))
            {
                if (leon.ActionTime * current.body.frameRate > 9f)
                {
                    return false;
                }
                DamageEnemy(source, 14);
                return true;
            }
            if (!aim || leon.IsBusy && !IsAimTransition(current) && !IsMovementTransition(current))
            {
                return false;
            }
            if (source.prefix == "Salvador" && BeginPair(source, "LeonPair_1200"))
            {
                return true;
            }
            PlayKnifeParry(Time.time - knifeGuardSince <= PerfectKnifeWindow);
            DamageEnemy(source, 14);
            return true;
        }

        private bool FinishKnifeAction(string id)
        {
            if (slot != 3 || !aim || !(ChapterOneRootActor.KnifeMoveFrame(id) >= 0
                || ChapterOneRootActor.IsKnifeTail(id) || id is "Knife_Draw" or "Knife_CrouchDraw"))
            {
                return false;
            }
            PlayKnifeGuardTransition();
            return true;
        }
    }
}
