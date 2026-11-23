using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private int pairGroundContact;
        private bool pairGroundCommitted;
        private string pairGroundReturnTake;
        private float pairGroundReturnTime;
        private float pairGroundContactGap;
        private float pairGroundSampledFrame;

        private bool TryStartGroundFinish()
        {
            Threat target = ClosestThreat(1.85f, threat => GroundFinishId(threat) != null);
            if (target == null)
            {
                return false;
            }
            return BeginPair(target, GroundFinishId(target));
        }

        private static string GroundFinishId(Threat threat)
        {
            if (threat.prefix != "Ganado" || !threat.actor || !threat.actor.isActiveAndEnabled)
            {
                return null;
            }
            if (threat.groundPhase == EnemyGroundPhase.Hold)
            {
                return "LeonPair_Ground1000";
            }
            if (IsInMeleeReaction(threat) && threat.actor.ActiveTake != null
                && (threat.actor.ActiveTake.id == "Ganado_3011"
                    || threat.actor.ActiveTake.id == "Ganado_3510" && GroundFrame(threat.actor) < 65f))
            {
                return "LeonPair_Ground1010";
            }
            if (LegMeleeReady(threat))
            {
                return "LeonPair_Ground1035";
            }
            return null;
        }

        private bool FitGroundFinish(Threat threat, ChapterOnePairMotion.Track playerTrack,
            ChapterOnePairMotion.Track enemyTrack, out Quaternion facing, out Vector3 playerStart)
        {
            facing = Quaternion.identity;
            playerStart = leon.MotionPosition;
            Vector3 right = threat.actor.GroundFinishBodyRight();
            if (right.sqrMagnitude < 0.5f || enemyTrack.sourceRight.sqrMagnitude < 0.5f)
            {
                return false;
            }
            facing = Quaternion.AngleAxis(Vector3.SignedAngle(enemyTrack.sourceRight, right, Vector3.up), Vector3.up);
            Vector3 offset = Vector3.ProjectOnPlane(enemyTrack.sourceOrigin - playerTrack.sourceOrigin, Vector3.up);
            playerStart = threat.actor.MotionPosition - facing * offset;
            playerStart.y = leon.MotionPosition.y;
            return (playerStart - leon.MotionPosition).sqrMagnitude <= 0.62f * 0.62f
                && Mathf.Abs(threat.actor.MotionPosition.y - leon.MotionPosition.y) < 0.20f
                && Vector3.Angle(leon.MotionRotation * Vector3.forward, facing * Vector3.forward) <= 75f;
        }

        private bool UpdateGroundFinishContact()
        {
            if (pairGroundContact == 0 || pairGroundCommitted || PairSourceFrame() < pairGroundContact)
            {
                return true;
            }
            if (!leon.TryGetKnifeBlade(out var bladeBase, out var bladeTip))
            {
                AbortPair();
                return false;
            }
            pairGroundContactGap = partner.actor.GroundFinishKnifeGap(bladeBase, bladeTip);
            if (pairGroundContactGap > 0.14f)
            {
                notice = "刀刃与目标接触超出范围";
                AbortPair();
                return false;
            }
            pairGroundSampledFrame = Mathf.Min(leon.ActionTime, partner.actor.ActionTime) * leon.ActiveTake.body.frameRate;
            pairGroundCommitted = true;
            partner.health = 0;
            partner.actor.DropEquipment();
            return true;
        }

        private static void RestoreGroundFinishTarget(Threat threat, string id, float time, bool committed)
        {
            var actor = threat.actor;
            if (committed)
            {
                FinishGroundFinishTarget(threat);
                return;
            }
            actor.Cancel();
            if (IsEnemyGrounded(threat) && PlayEnemyGround(threat, EnemyGroundPhase.Hold, "GanadoGround_3001", 0.20f))
            {
                return;
            }
            if (id != null && actor.Play(id, true, sourceTime: time, actionFade: 0.20f))
            {
                if (threat.meleeReactionAction != 0)
                {
                    threat.meleeReactionAction = actor.ActionId;
                }
                if (threat.legStaggerAction != 0)
                {
                    threat.legStaggerAction = actor.ActionId;
                }
                return;
            }
        }

        private static void FinishGroundFinishTarget(Threat threat)
        {
            threat.meleeReactionAction = 0;
            threat.legStaggerAction = 0;
            threat.groundPhase = EnemyGroundPhase.Die;
            var actor = threat.actor;
            threat.groundAction = actor.ActionId;
            actor.SetEnemyGroundHeight(1f);
            if (actor.PairedFinished || !actor.HasValidStageTrack)
            {
                actor.HoldTerminal();
            }
            else
            {
                actor.ReleasePairAfterTail();
            }
        }
    }
}
