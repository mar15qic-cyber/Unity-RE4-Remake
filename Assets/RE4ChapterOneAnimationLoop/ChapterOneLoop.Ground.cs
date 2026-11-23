using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        internal enum EnemyGroundPhase
        {
            None, Fall, Hold, Start, Crawl, Stop, Rise, Die, Back, Attack
        }

        private static bool IsEnemyGrounded(Threat threat) => threat.groundPhase != EnemyGroundPhase.None;

        private static bool StartEnemyGroundDamage(Threat threat, bool leg, bool left, bool staggered)
        {
            if (threat.prefix != "Ganado")
            {
                return false;
            }
            bool permanent = leg && threat.health > 0 && 5L * threat.health < 2L * threat.initialHealth;
            if (IsEnemyGrounded(threat))
            {
                threat.permanentGround |= permanent;
                if (permanent)
                {
                    threat.leftLeg = left;
                }
                if (threat.groundPhase != EnemyGroundPhase.Rise)
                {
                    return true;
                }
                if (threat.health == 0 && GroundFrame(threat.actor) >= 225f)
                {
                    ClearEnemyGround(threat);
                    return false;
                }
                if (!permanent || GroundFrame(threat.actor) < 225f)
                {
                    return true;
                }
            }
            if ((!permanent && !(leg && staggered)) || threat.health <= 0)
            {
                return false;
            }
            int entry = 0;
            float blend = 0.18f;
            if (staggered)
            {
                float staggerFrame = GroundFrame(threat.actor);
                if (staggerFrame >= 20f && staggerFrame <= 165f)
                {
                    // 低跪姿从重心开始下落的原帧接入，避免混到绊倒的站立首帧。
                    entry = 19;
                    blend = 0.26f;
                }
            }
            if (!PlayEnemyGround(threat, EnemyGroundPhase.Fall, GroundFallClip(left), blend, entry))
            {
                return false;
            }
            threat.permanentGround = permanent;
            return true;
        }

        private static string GroundFallClip(bool left)
        {
            if (left)
            {
                return "GanadoGround_0421";
            }
            return "GanadoGround_0521";
        }

        private static float GroundFrame(ChapterOneRootActor actor)
        {
            if (actor.ActiveTake != null && actor.ActiveTake.body != null)
            {
                return actor.ActionTime * actor.ActiveTake.body.frameRate;
            }
            return 0f;
        }

        private static bool PlayEnemyGround(Threat threat, EnemyGroundPhase phase, string id, float blend, int sourceFrame = 0)
        {
            if (!threat.actor.TryGetTake(id, out var take) || !take.body
                || !threat.actor.Play(id, true, sourceTime: sourceFrame / take.body.frameRate, actionFade: blend))
            {
                return false;
            }
            threat.groundPhase = phase;
            threat.groundAction = threat.actor.ActionId;
            threat.groundSince = Time.fixedTime;
            threat.groundStopAfter = 0f;
            return true;
        }

        private static void ClearEnemyGround(Threat threat)
        {
            threat.actor.RestoreEnemyGroundHeight();
            threat.groundPhase = EnemyGroundPhase.None;
            threat.groundAction = 0;
            threat.permanentGround = false;
        }

        private bool UpdateEnemyGround(Threat threat)
        {
            if (!IsEnemyGrounded(threat))
            {
                return false;
            }
            var actor = threat.actor;
            if (!actor || !actor.isActiveAndEnabled || actor.IsTerminal)
            {
                return true;
            }
            if (actor.ActionId != threat.groundAction || actor.ActiveTake == null || !actor.ActiveTake.body)
            {
                ClearEnemyGround(threat);
                return false;
            }

            float frame = GroundFrame(actor);
            if (threat.groundPhase == EnemyGroundPhase.Fall)
            {
                actor.SetEnemyGroundHeight(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 42f, frame)));
                int landed = 59;
                if (actor.ActiveTake.id == "GanadoGround_0421")
                {
                    landed = 54;
                }
                if (frame >= landed)
                {
                    AdvanceEnemyGround(threat, threat.groundAction);
                }
                return true;
            }
            if (threat.health == 0 && threat.groundPhase != EnemyGroundPhase.Die)
            {
                FinishEnemyOnGround(threat);
                return true;
            }
            if (threat.groundPhase == EnemyGroundPhase.Rise)
            {
                actor.SetEnemyGroundHeight(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(126f, 263f, frame)));
                if (frame >= 303f && !threat.permanentGround)
                {
                    AdvanceEnemyGround(threat, threat.groundAction);
                }
                else if (threat.permanentGround && frame >= 225f)
                {
                    PlayEnemyGround(threat, EnemyGroundPhase.Fall, GroundFallClip(threat.leftLeg), 0.18f);
                }
                return true;
            }
            actor.SetEnemyGroundHeight(1f);
            if (threat.groundPhase is EnemyGroundPhase.Back or EnemyGroundPhase.Attack)
            {
                UpdateCrawlCombat(threat, frame);
                return true;
            }
            if (threat.groundPhase == EnemyGroundPhase.Stop && frame >= StopGroundFrame(actor.ActiveTake.id))
            {
                AdvanceEnemyGround(threat, threat.groundAction);
                return true;
            }
            if (!threat.permanentGround)
            {
                if (threat.groundPhase == EnemyGroundPhase.Hold && Time.fixedTime - threat.groundSince >= 0.36f
                    && actor.CanRecoverFromGround())
                {
                    PlayEnemyGround(threat, EnemyGroundPhase.Rise, "GanadoGround_3500", 0.20f);
                }
                return true;
            }
            if (threat.groundPhase is EnemyGroundPhase.Hold or EnemyGroundPhase.Start or EnemyGroundPhase.Crawl)
            {
                DriveEnemyOnGround(threat, frame);
            }
            return true;
        }

        private void DriveEnemyOnGround(Threat threat, float frame)
        {
            var actor = threat.actor;
            Vector3 toward = Vector3.ProjectOnPlane(leon.MotionPosition - actor.MotionPosition, Vector3.up);
            float distance = toward.magnitude;
            bool holding = threat.groundPhase == EnemyGroundPhase.Hold;
            if (holding && TryStartCrawlCombat(threat, toward, distance))
            {
                return;
            }
            float stopDistance = 1.28f;
            if (holding)
            {
                stopDistance = 1.58f;
            }
            bool pursue = ai && health > 0 && distance > stopDistance && distance < 7.8f;
            Vector3 heading = toward;
            if (pursue)
            {
                Vector3 low = Vector3.up * 0.3f;
                if (Physics.Linecast(actor.MotionPosition + low, leon.MotionPosition + low,
                    obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    heading = PathHeading(threat, toward);
                }
                pursue = !actor.IsEnemyCrawlBlocked(heading);
            }
            if (pursue)
            {
                threat.groundStopAfter = 0f;
                actor.Move(heading, Vector2.up, 1f, 0);
                if (holding && Time.fixedTime - threat.groundSince >= 0.24f)
                {
                    PlayEnemyGround(threat, EnemyGroundPhase.Start, "GanadoGround_2120", 0.24f);
                }
                return;
            }
            actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            if (holding)
            {
                return;
            }
            bool starting = threat.groundPhase == EnemyGroundPhase.Start;
            if (starting && frame < 40f)
            {
                PlayEnemyGround(threat, EnemyGroundPhase.Hold, "GanadoGround_3001", 0.22f);
                return;
            }
            if (threat.groundStopAfter <= 0f)
            {
                threat.groundStopAfter = Time.fixedTime + 0.30f;
            }
            // 用原版手部支撑相位选左、右收势，并限制近身后等待支撑的时间。
            bool leftSupport = frame < 89f || (frame > 280f && frame < 425f) || frame > 609f;
            bool nearSupport = Mathf.Abs(frame - 52f) < 7f || Mathf.Abs(frame - 132f) < 7f
                || Mathf.Abs(frame - 336f) < 7f || Mathf.Abs(frame - 466f) < 7f;
            if (starting)
            {
                leftSupport = frame < 107f || frame > 275f;
                nearSupport = Mathf.Abs(frame - 130f) < 7f || Mathf.Abs(frame - 90f) < 7f;
            }
            if (nearSupport || Time.fixedTime >= threat.groundStopAfter)
            {
                string stop = "GanadoGround_2123";
                if (leftSupport)
                {
                    stop = "GanadoGround_2122";
                }
                PlayEnemyGround(threat, EnemyGroundPhase.Stop, stop, 0.28f);
            }
        }

        private static int StopGroundFrame(string id)
        {
            if (id == "GanadoGround_2122")
            {
                return 290;
            }
            return 190;
        }

        private static bool AdvanceEnemyGround(Threat threat, int actionId)
        {
            if (!IsEnemyGrounded(threat) || threat.groundAction != actionId)
            {
                return false;
            }
            if (threat.health == 0)
            {
                FinishEnemyOnGround(threat);
                return true;
            }
            switch (threat.groundPhase)
            {
                case EnemyGroundPhase.Fall:
                case EnemyGroundPhase.Stop:
                case EnemyGroundPhase.Back:
                case EnemyGroundPhase.Attack:
                    threat.actor.SetEnemyGroundHeight(1f);
                    if (PlayEnemyGround(threat, EnemyGroundPhase.Hold, "GanadoGround_3001", 0.22f))
                    {
                        return true;
                    }
                    break;
                case EnemyGroundPhase.Start:
                    if (PlayEnemyGround(threat, EnemyGroundPhase.Crawl, "GanadoGround_2121", 0.10f))
                    {
                        return true;
                    }
                    break;
            }
            ClearEnemyGround(threat);
            threat.actor.Cancel();
            return true;
        }

        private static void FinishEnemyOnGround(Threat threat)
        {
            threat.actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            threat.actor.SetEnemyGroundHeight(1f);
            if (threat.groundPhase != EnemyGroundPhase.Die
                && PlayEnemyGround(threat, EnemyGroundPhase.Die, "GanadoGround_1001", 0.24f))
            {
                threat.actor.DropEquipment();
                return;
            }
            threat.actor.HoldTerminal();
            threat.actor.DropEquipment();
        }
    }
}
