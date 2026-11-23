using UnityEngine;
using static RE4.ChapterOne.ChapterOneRootActor;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const int LegBiteEntryFrame = 24;
        private const int LegBiteTimeoutFrame = 74;
        private const int LegBiteFailureEntryFrame = 136;
        private const int LegBiteContactFrame = 160;
        private bool pairLegBite;
        private bool pairBiteEscape;
        private bool pairBiteContactHandled;
        private Pose pairBiteStage;
        private float pairBiteContactGap;
        private float pairBiteContactFrame;

        private bool LegBiteCanPlay(Threat threat, string id)
        {
            if (id == LegBiteEnter)
            {
                return partner == null && threat.groundPhase == EnemyGroundPhase.Hold && threat.permanentGround
                    && !leon.BlocksLocomotion && Time.time >= threat.attackAfter;
            }
            return pairLegBite && partner == threat;
        }

        private bool FitLegBite(Threat threat, string id, ChapterOnePairMotion.Track playerTrack,
            ChapterOnePairMotion.Track enemyTrack, out Pose stage, out float sourceTime)
        {
            stage = pairBiteStage;
            sourceTime = 0f;
            if (id == LegBiteFailure)
            {
                sourceTime = LegBiteFailureEntryFrame / 60f;
            }
            if (id != LegBiteEnter)
            {
                return true;
            }
            sourceTime = LegBiteEntryFrame / 60f;
            Pose enemyPose = enemyTrack.Sample(sourceTime);
            Quaternion facing = threat.actor.MotionRotation * Quaternion.Inverse(enemyPose.rotation);
            Vector3 origin = threat.actor.MotionPosition - facing * (enemyTrack.sourceOrigin + enemyPose.position);
            stage = new Pose(origin, facing);
            Pose playerPose = playerTrack.Sample(sourceTime);
            Vector3 target = origin + facing * (playerTrack.sourceOrigin + playerPose.position);
            Vector3 toEnemy = Vector3.ProjectOnPlane(threat.actor.MotionPosition - leon.MotionPosition, Vector3.up);
            return toEnemy.sqrMagnitude <= 1.08f * 1.08f
                && Mathf.Abs(threat.actor.MotionPosition.y - leon.MotionPosition.y) < 0.20f
                && Quaternion.Angle(leon.MotionRotation, facing * playerPose.rotation) <= 105f
                && Vector3.ProjectOnPlane(target - leon.MotionPosition, Vector3.up).sqrMagnitude <= 0.63f * 0.63f;
        }

        private void UpdateLegBite(string id)
        {
            int frame = PairSourceFrame();
            if (id == LegBiteEnter)
            {
                if (leon.PairedFinished && partner.actor.PairedFinished && !BeginPair(partner, LegBiteHold))
                {
                    AbortPair();
                }
                return;
            }
            if (id == LegBiteHold)
            {
                if (pairBiteEscape && (frame >= 12 || leon.PairedFinished))
                {
                    if (!BeginPair(partner, LegBiteEscape))
                    {
                        AbortPair();
                    }
                }
                else if (frame >= LegBiteTimeoutFrame && !BeginPair(partner, LegBiteFailure))
                {
                    AbortPair();
                }
                return;
            }
            if (id == LegBiteFailure && !pairBiteContactHandled && frame >= LegBiteContactFrame)
            {
                pairBiteContactHandled = true;
                pairBiteContactFrame = Mathf.Min(leon.ActionTime, partner.actor.ActionTime) * leon.ActiveTake.body.frameRate;
                pairBiteContactGap = partner.actor.LegBiteGap(leon, out var jaw, out var shin);
                if (pairBiteContactGap <= 0.145f && !Physics.Linecast(jaw, shin, obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    health = Mathf.Max(0, health - 20);
                    notice = "咬腿命中";
                }
                else
                {
                    notice = "咬腿接触落空";
                }
            }
            if (leon.PairedFinished && partner.actor.PairedFinished)
            {
                CompletePair(id);
            }
        }

        private static void ResumeLegBiteCrawler(Threat threat)
        {
            threat.actor.Cancel();
            if (threat.actor.isActiveAndEnabled)
            {
                PlayEnemyGround(threat, EnemyGroundPhase.Hold, "GanadoGround_3001", 0.28f);
                threat.attackAfter = Time.time + 4f;
            }
        }

        private void ReleaseLegBitePlayer()
        {
            leon.Cancel();
            if (health == 0)
            {
                leon.Play("LeonDamage_0900", true);
            }
            else if (slot == 2)
            {
                SetCqbrAimPose(false);
            }
        }
    }
}
