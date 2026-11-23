using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const int MeleeDamage = 45;
        private const int LegFatalEndFrame = 165;
        private const float LowerMeleeReach = 2.05f;
        private int meleeStaggerAction;
        private ChapterOneMeleeMotion meleeMotion;

        private void TryMelee()
        {
            if (!CanStartMelee())
            {
                return;
            }
            if (slot == 3 && TryStartGroundFinish())
            {
                return;
            }
            Threat staggered = ClosestThreat(LowerMeleeReach,
                item => LegMeleeReady(item) && InMeleeReach(item, LowerMeleeReach));
            if (staggered != null)
            {
                StartMelee(staggered, true);
                return;
            }
            Threat target = ClosestThreat(1.8f, item => !IsEnemyGrounded(item) && !IsInMeleeReaction(item) && !HasLegStagger(item));
            if (target == null)
            {
                return;
            }
            Vector3 back = leon.transform.position - target.actor.transform.position;
            if (slot == 3 && Vector3.Dot(target.actor.transform.forward, back.normalized) < -0.55f
                && BeginPair(target, "LeonPair_1101"))
            {
                return;
            }
            if (InMeleeReach(target, 1.8f))
            {
                StartMelee(target, false);
            }
        }

        private bool CanStartMelee()
        {
            if (!leon.IsBusy || IsMovementTransition(leon.ActiveTake) || IsAimTransition(leon.ActiveTake))
            {
                return true;
            }
            return slot != 1 && firedAction == leon.ActionId && leon.ActiveTake != null
                && ChapterOneRootActor.IsFirearmShot(leon.ActiveTake.id);
        }

        private static bool HasLegStagger(Threat threat) =>
            threat.actor && threat.actor.isActiveAndEnabled && threat.legStaggerAction != 0
                && threat.actor.ActionId == threat.legStaggerAction;

        private static bool LegMeleeReady(Threat threat)
        {
            if (!HasLegStagger(threat) || threat.actor.ActiveTake == null)
            {
                return false;
            }
            float frame = threat.actor.ActionTime * threat.actor.ActiveTake.body.frameRate;
            // 左右腿原版受击的终结技窗口止于 165 帧，留出踢击准备时间。
            return frame >= 32 && frame <= LegFatalEndFrame - 30;
        }

        private void StartMelee(Threat target, bool low)
        {
            if (!HasEnemyApproachLine(target.actor))
            {
                return;
            }

            if (meleeMotion == null)
            {
                meleeMotion = Resources.Load<ChapterOneMeleeMotion>(ChapterOneMeleeMotion.ResourceName);
            }
            if (meleeMotion == null)
            {
                notice = "体术接触轨道缺失";
                return;
            }
            string id = "LeonInteract_0800";
            int turnEnd = 18;
            if (low)
            {
                id = "LeonInteract_0530";
                turnEnd = 21;
            }
            else if (slot == 0)
            {
                id = "LeonInteract_0800_wp4000m";
            }

            if (!leon.TryGetTake(id, out var take) || !meleeMotion.TryGetTrack(take.body, out var track))
            {
                return;
            }
            float marker = ChapterOneRootActor.MeleeContactFrame(id) / take.body.frameRate;
            float impact = Mathf.Ceil(marker / Time.fixedDeltaTime - 0.0001f) * Time.fixedDeltaTime;
            Vector3 targetPoint = PredictMeleeTarget(target.actor, low, impact);
            Vector3 toward = Vector3.ProjectOnPlane(targetPoint - leon.MotionPosition, Vector3.up);
            if (low && toward.sqrMagnitude > 1.38f * 1.38f
                && leon.TryGetTake("LeonInteract_0531", out var longKick)
                && meleeMotion.TryGetTrack(longKick.body, out var longTrack))
            {
                id = longKick.id;
                track = longTrack;
            }
            if (toward.sqrMagnitude < 0.09f)
            {
                return;
            }

            Vector3 source = Vector3.ProjectOnPlane(track.SamplePoint(impact), Vector3.up);
            Quaternion facing = Quaternion.AngleAxis(Vector3.SignedAngle(source, toward, Vector3.up), Vector3.up);
            Vector3 correction = toward - facing * source;
            float turn = Vector3.SignedAngle(leon.MotionRotation * Vector3.forward, facing * Vector3.forward, Vector3.up);
            Vector3 end = leon.MotionPosition + correction + facing * track.root.Sample(impact).position;
            if (correction.sqrMagnitude > 0.48f * 0.48f || Mathf.Abs(turn) > 75f || !CanApproachForMelee(end)
                || !leon.Play(id, true))
            {
                return;
            }
            meleeTarget = target;
            meleeAction = leon.ActionId;
            meleeStaggerAction = 0;
            if (low)
            {
                meleeStaggerAction = target.legStaggerAction;
            }
            reloadAfterAction = -1;
            leon.SetMeleeApproach(track, correction, facing, turnEnd, impact);
        }

        private Vector3 PredictMeleeTarget(ChapterOneRootActor actor, bool low, float duration)
        {
            Vector3 target = actor.MeleeTargetPoint(low);
            if (low && actor.ActiveTake != null && meleeMotion.TryGetTrack(actor.ActiveTake.body, out var track))
            {
                float at = actor.ActionTime;
                Vector3 offset = track.SamplePoint(at + duration) - track.SamplePoint(at);
                target += actor.MotionRotation * offset;
            }
            return target;
        }

        private bool CanApproachForMelee(Vector3 end)
        {
            const float groundClearance = 0.075f;
            if (Physics.Raycast(end + Vector3.up * 0.6f, Vector3.down, out var floor, 0.95f,
                obstructionMask, QueryTriggerInteraction.Ignore) && floor.normal.y > 0.55f)
            {
                end.y = Mathf.Max(end.y, floor.point.y + 0.03f);
            }
            return leon.CanAlignForPair(end + Vector3.up * groundClearance, groundClearance);
        }

        private void ResolveMeleeContact(string id, int actionId)
        {
            if (meleeAction != actionId)
            {
                return;
            }
            Threat target = meleeTarget;
            meleeTarget = null;
            if (target == null || !target.actor || !target.actor.isActiveAndEnabled
                || target.health <= 0 || IsEnemyGrounded(target) || IsInMeleeReaction(target)
                || !HasEnemyApproachLine(target.actor))
            {
                return;
            }
            bool low = id is "LeonInteract_0530" or "LeonInteract_0531";
            if (low && (!HasLegStagger(target) || target.legStaggerAction != meleeStaggerAction
                || target.actor.ActionTime * target.actor.ActiveTake.body.frameRate > LegFatalEndFrame))
            {
                return;
            }
            Vector3 strike = leon.MeleeStrikePoint(id);
            Vector3 hit = target.actor.MeleeTargetPoint(low);
            if ((hit - strike).sqrMagnitude > 0.34f * 0.34f
                || Physics.Linecast(strike, hit, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Vector3 towardPlayer = leon.MotionPosition - target.actor.MotionPosition;
            if (target.prefix != "Ganado" || Vector3.Angle(target.actor.MotionRotation * Vector3.forward, towardPlayer) > 58f)
            {
                ApplyEnemyDamage(target, MeleeDamage, "_0110");
                return;
            }
            string reaction = "Ganado_1015";
            if (low)
            {
                reaction = "Ganado_1100";
                if (!target.leftLeg)
                {
                    reaction = "Ganado_1105";
                }
            }
            int reactionStart = 0;
            float reactionBlend = 0f;
            if (HasMeleeWallBehind(target.actor))
            {
                reaction = "Ganado_1410";
                reactionBlend = 0.20f;
                if (low)
                {
                    reactionStart = 78;
                }
            }
            if (PlayMeleeReaction(target, reaction, reactionStart, reactionBlend))
            {
                target.health = Mathf.Max(0, target.health - MeleeDamage);
                target.legStaggerAction = 0;
                target.alerted = true;
                target.actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            }
            else
            {
                ApplyEnemyDamage(target, MeleeDamage, "_0110");
            }
        }

        private static bool IsInMeleeReaction(Threat threat) =>
            threat.actor && threat.meleeReactionAction != 0 && threat.actor.ActionId == threat.meleeReactionAction;

        private bool HasMeleeWallBehind(ChapterOneRootActor actor)
        {
            Vector3 chest = actor.MotionPosition + Vector3.up * 0.64f;
            Vector3 forward = actor.MotionRotation * Vector3.forward;
            return Physics.SphereCast(chest, 0.12f, -forward, out var wall, 0.53f,
                obstructionMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(wall.normal.y) < 0.35f && Vector3.Dot(wall.normal, forward) > 0.6f;
        }

        private static bool PlayMeleeReaction(Threat threat, string id, int startFrame = 0, float blend = 0f)
        {
            if (!threat.actor.TryGetTake(id, out var take)
                || !threat.actor.Play(id, true, sourceTime: startFrame / take.body.frameRate, actionFade: blend))
            {
                return false;
            }
            threat.meleeReactionAction = threat.actor.ActionId;
            return true;
        }

        private void UpdateMeleeReaction(Threat threat)
        {
            if (!IsInMeleeReaction(threat))
            {
                threat.meleeReactionAction = 0;
                return;
            }
            var actor = threat.actor;
            var action = actor.ActiveTake;
            float frame = actor.ActionTime * action.body.frameRate;
            if (action.id is "Ganado_1100" or "Ganado_1105" or "Ganado_1015")
            {
                int landingFrame = 24;
                if (action.id != "Ganado_1105")
                {
                    landingFrame = 25;
                }
                if (frame >= landingFrame && actor.RequestedStep.y <= 0f
                    && Physics.Raycast(actor.MotionPosition + Vector3.up * 0.09f, Vector3.down,
                        out var ground, 0.28f, obstructionMask, QueryTriggerInteraction.Ignore)
                    && ground.normal.y > 0.55f)
                {
                    AdvanceMeleeReaction(threat, action.id, actor.ActionId);
                }
            }
            else if ((action.id == "Ganado_3011" && (frame >= 16 || threat.health == 0))
                || (action.id == "Ganado_3510" && frame >= 263)
                || (action.id == "Ganado_1410" && frame >= 106)
                || (action.id == "Ganado_3550" && frame >= 265))
            {
                AdvanceMeleeReaction(threat, action.id, actor.ActionId);
            }
        }

        private static bool AdvanceMeleeReaction(Threat threat, string id, int actionId)
        {
            if (threat.meleeReactionAction != actionId || !IsInMeleeReaction(threat))
            {
                return false;
            }
            string next = id switch
            {
                "Ganado_1015" => "Ganado_1016",
                "Ganado_1100" => "Ganado_1101",
                "Ganado_1105" => "Ganado_1106",
                "Ganado_1016" or "Ganado_1101" or "Ganado_1106" => "Ganado_3011",
                "Ganado_3011" => "Ganado_3510",
                "Ganado_1410" => "Ganado_3550",
                _ => null
            };
            if (threat.health == 0 && (id is "Ganado_1016" or "Ganado_1101" or "Ganado_1106" or "Ganado_3011" or "Ganado_1410"))
            {
                threat.meleeReactionAction = 0;
                threat.actor.HoldTerminal();
                threat.actor.DropEquipment();
                return true;
            }
            float blend = 0f;
            if (id == "Ganado_1410")
            {
                blend = 0.22f;
            }
            if (next != null && PlayMeleeReaction(threat, next, blend: blend))
            {
                return true;
            }
            threat.meleeReactionAction = 0;
            if (threat.health == 0)
            {
                threat.actor.Play("Ganado_0000_dead_0th_com", true);
            }
            else
            {
                threat.actor.Cancel();
            }
            return true;
        }
    }
}
