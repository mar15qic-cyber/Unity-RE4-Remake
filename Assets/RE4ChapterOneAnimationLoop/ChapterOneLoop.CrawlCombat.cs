using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private bool TryStartCrawlCombat(Threat threat, Vector3 toward, float distance)
        {
            if (!ai || health <= 0 || partner != null || distance > 1.37f
                || Time.fixedTime - threat.groundSince < 0.20f)
            {
                return false;
            }
            var actor = threat.actor;
            Vector3 facing = actor.MotionRotation * Vector3.forward;
            bool ready = Time.time >= threat.attackAfter;
            if (ready && distance < 1.08f && BeginPair(threat, ChapterOneRootActor.LegBiteEnter))
            {
                return true;
            }
            if (distance < 0.85f && Vector3.Angle(facing, toward) <= 62f
                && !actor.IsEnemyCrawlBlocked(-facing)
                && PlayEnemyGround(threat, EnemyGroundPhase.Back, ChapterOneRootActor.CrawlBackClip, 0.30f))
            {
                actor.Move(toward, Vector2.up, 1f, 0);
                return true;
            }
            if (!ready || Vector3.Angle(facing, toward) > 53f || leon.TraversalOpeningOccupied
                || Physics.Linecast(actor.MotionPosition + Vector3.up * 0.42f, leon.MotionPosition + Vector3.up * 0.42f,
                    obstructionMask, QueryTriggerInteraction.Ignore)
                || !PlayEnemyGround(threat, EnemyGroundPhase.Attack, ChapterOneRootActor.CrawlAttackClip, 0.27f))
            {
                return false;
            }
            actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            actor.TryGetCrawlStrike(out threat.groundPreviousStrike);
            threat.attackAfter = Time.time + 2.85f;
            return true;
        }

        private void UpdateCrawlCombat(Threat threat, float frame)
        {
            var actor = threat.actor;
            if (threat.groundPhase == EnemyGroundPhase.Back)
            {
                Vector3 facing = actor.MotionRotation * Vector3.forward;
                if (actor.IsEnemyCrawlBlocked(-facing))
                {
                    actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
                    AdvanceEnemyGround(threat, threat.groundAction);
                    return;
                }
                Vector3 retreatFacing = Vector3.ProjectOnPlane(leon.MotionPosition - actor.MotionPosition, Vector3.up);
                if (Vector3.Angle(facing, retreatFacing) <= 62f)
                {
                    actor.Move(retreatFacing, Vector2.up, 1f, 0);
                }
                return;
            }

            if (!actor.TryGetCrawlStrike(out var point))
            {
                return;
            }
            Vector3 previous = threat.groundPreviousStrike;
            threat.groundPreviousStrike = point;
            // 旧资源没有伤害事件。左手从第 19 帧进入挥击，第 29 帧前开始回收。
            if (frame < 19f || frame > 29f || health <= 0 || partner != null || leon.TraversalOpeningOccupied
                || threat.groundHitAction == actor.ActionId)
            {
                return;
            }
            Vector3 axis = point - previous;
            Vector3 center = leon.MotionPosition + Vector3.up * 0.44f;
            Vector3 nearest = previous + axis * Mathf.Clamp01(Vector3.Dot(center - previous, axis) / Mathf.Max(axis.sqrMagnitude, 0.0001f));
            Vector3 target = leon.ClosestBodyPoint(nearest);
            if ((nearest - target).sqrMagnitude > 0.15f * 0.15f
                || Physics.Linecast(actor.MotionPosition + Vector3.up * 0.42f, target, obstructionMask, QueryTriggerInteraction.Ignore)
                || Physics.Linecast(nearest, target, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }
            threat.groundHitAction = actor.ActionId;
            Vector3 toward = leon.MotionPosition - actor.MotionPosition;
            if (!TryParryEnemy(threat, -toward))
            {
                ApplyEnemyStrike(toward, 10, false);
            }
        }
    }
}
