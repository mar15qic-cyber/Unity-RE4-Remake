using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private sealed class EnemyWindowAction
        {
            public Transform window;
            public int id;
            public ChapterOneRootActor.Take landing;
            public ChapterOnePairMotion.Track landingTrack;
            public Pose landingStart;
            public float landingEnd;
            public float blockedTime;
            public string damageReaction;
            public bool legHit;
            public bool leftHit;
        }

        private readonly Dictionary<Threat, EnemyWindowAction> enemyWindows = new();

        private bool WindowReserved(Transform window, ChapterOneRootActor requester)
        {
            if (playerTraversalWindow == window && leon != requester
                && (leon.IsTraversing && leon.ActionId == traversalAction || leon.TraversalOpeningOccupied))
            {
                return true;
            }
            foreach (var item in enemyWindows)
            {
                var actor = item.Key.actor;
                if (item.Value.window == window && actor != null && actor != requester
                    && (actor.IsTraversing && actor.ActionId == item.Value.id || actor.WindowNeedsClearance))
                {
                    return true;
                }
            }
            return false;
        }

        private bool DriveEnemyThroughWindow(Threat threat)
        {
            return DriveEnemyThroughWindow(threat, dropVault, true) || DriveEnemyThroughWindow(threat, vault, false);
        }

        private bool DriveEnemyThroughWindow(Threat threat, Transform window, bool elevated)
        {
            var actor = threat.actor;
            if (window == null || actor.WindowNeedsClearance || !window.TryGetComponent<Collider>(out var sill))
            {
                return false;
            }

            Vector3 at = actor.MotionPosition;
            Vector3 player = leon.MotionPosition;
            Quaternion toWindow = Quaternion.Inverse(window.rotation);
            Vector3 from = toWindow * (at - window.position);
            Vector3 to = toWindow * (player - window.position);
            if (Mathf.Abs(at.y - sill.bounds.min.y) > 0.24f || Mathf.Abs(from.z) < 0.39f || Mathf.Abs(from.z) > 5.2f
                || from.z * to.z >= 0f || Mathf.Abs(to.z) < 0.45f || Mathf.Abs(to.z) > 5.6f
                || elevated && (from.z > 0f || at.y - player.y < 0.65f)
                || !elevated && Mathf.Abs(at.y - player.y) > 0.50f)
            {
                return false;
            }
            float crossing = from.z / (from.z - to.z);
            if (Mathf.Abs(Mathf.Lerp(from.x, to.x, crossing)) > 0.86f)
            {
                return false;
            }

            bool saw = threat.prefix == "Salvador";
            Quaternion facing = window.rotation;
            if (from.z > 0f)
            {
                facing *= Quaternion.Euler(0f, 180f, 0f);
            }
            // 以踩窗台的原版脚位为基准，将角色对齐到开口中心。
            float approach = 1.35f;
            if (saw)
            {
                approach = 1.44f;
            }
            Vector3 entry = window.position + facing * new Vector3(-0.10f, 0f, -approach);
            entry.y = at.y;
            Vector3 towardEntry = entry - at;
            Vector3 probeHeight = Vector3.up * 0.60f;
            if (Physics.Linecast(at + probeHeight, entry + probeHeight, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            bool reserved = WindowReserved(window, actor);
            float distance = towardEntry.magnitude;
            Vector3 forward = facing * Vector3.forward;
            if (reserved && distance < 0.72f)
            {
                actor.DriveEnemy(threat.prefix, Vector3.zero, forward, false, true);
                return true;
            }
            if (distance > 0.24f)
            {
                actor.DriveEnemy(threat.prefix, towardEntry, towardEntry, true, false);
                actor.LimitEnemyWindowApproach(distance);
                return true;
            }
            if (Quaternion.Angle(actor.MotionRotation, facing) > 58f)
            {
                actor.DriveEnemy(threat.prefix, Vector3.zero, forward, false, true);
                return true;
            }
            return BeginEnemyWindow(threat, window, sill, new Pose(entry, facing), saw);
        }

        private bool BeginEnemyWindow(Threat threat, Transform window, Collider sill, Pose start, bool saw)
        {
            var actor = threat.actor;
            string prefix = threat.prefix + "Window_";
            if (!TryTraversalTrack(actor, prefix + "1100", out var entry, out var track)
                || !TryTraversalTrack(actor, prefix + "1102", out var landing, out var landingTrack)
                || !FindEnemyWindowLanding(window, track, start, out float entryEnd, out Pose landingStart))
            {
                return false;
            }
            float landingFrame = 70f;
            if (saw)
            {
                landingFrame = 88f;
            }
            float landingEnd = landingFrame / landing.body.frameRate;
            if (!TraversalTrackClear(actor, track, start, entryEnd, sill, true)
                || !TraversalTrackClear(actor, landingTrack, landingStart, landingEnd, null))
            {
                return false;
            }

            actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            if (!actor.PlayTraversal(entry, track, start, entryEnd, 0.24f, sill))
            {
                return false;
            }
            enemyWindows[threat] = new EnemyWindowAction
            {
                window = window,
                id = actor.ActionId,
                landing = landing,
                landingTrack = landingTrack,
                landingStart = landingStart,
                landingEnd = landingEnd
            };
            return true;
        }

        private bool FindEnemyWindowLanding(Transform window, ChapterOnePairMotion.Track track, Pose start,
            out float takeEnd, out Pose landing)
        {
            takeEnd = 0f;
            landing = default;
            int frames = Mathf.RoundToInt(track.clip.length * track.clip.frameRate);
            Vector3 heading = start.rotation * Vector3.forward;
            for (int frame = 162; frame <= frames; frame++)
            {
                float time = frame / track.clip.frameRate;
                Vector3 point = At(track, start, time);
                if (Vector3.Dot(point - window.position, heading) < 0.55f)
                {
                    continue;
                }
                Vector3 probe = new(point.x, start.position.y + 0.24f, point.z);
                if (!FindTraversalGround(probe, 3.7f, out var floor) || point.y > floor.y + 0.19f)
                {
                    continue;
                }
                // 地面前一帧或第一帧接落地，避免碰撞先截断整个下落轨道。
                if (point.y < floor.y - 0.045f)
                {
                    time -= 1f / track.clip.frameRate;
                    point = At(track, start, time);
                }
                takeEnd = time;
                landing = new Pose(new Vector3(point.x, floor.y + GroundOffset, point.z), start.rotation);
                return true;
            }
            return false;
        }

        private bool CompleteEnemyWindow(Threat threat, int id)
        {
            if (!enemyWindows.TryGetValue(threat, out var state) || state.id != id)
            {
                return false;
            }
            if (state.landing != null)
            {
                var landing = state.landing;
                var track = state.landingTrack;
                state.landing = null;
                state.landingTrack = null;
                if (track != null && track.Matches(landing.body)
                    && TraversalTrackClear(threat.actor, track, state.landingStart, state.landingEnd, null)
                    && threat.actor.PlayTraversal(landing, track, state.landingStart, state.landingEnd, 0.20f, null))
                {
                    state.id = threat.actor.ActionId;
                    state.blockedTime = 0f;
                }
                else
                {
                    FinishEnemyWindow(threat, state);
                }
                return true;
            }
            FinishEnemyWindow(threat, state);
            return true;
        }

        private void FinishEnemyWindow(Threat threat, EnemyWindowAction state)
        {
            enemyWindows.Remove(threat);
            threat.repathAfter = 0f;
            threat.corners = null;
            if (state.damageReaction != null)
            {
                PlayEnemyDamageReaction(threat, state.damageReaction, state.legHit, state.leftHit);
            }
        }

        private bool DeferEnemyWindowDamage(Threat threat, string reaction, bool leg, bool left)
        {
            if (!enemyWindows.TryGetValue(threat, out var state) || threat.actor.ActionId != state.id
                || !threat.actor.WindowNeedsClearance)
            {
                return false;
            }
            // 伤害立即结算，动作等身体离开窗框再接续。
            state.damageReaction = reaction;
            state.legHit = leg;
            state.leftHit = left;
            return true;
        }

        private void UpdateEnemyWindow(Threat threat)
        {
            if (!enemyWindows.TryGetValue(threat, out var state))
            {
                return;
            }
            var actor = threat.actor;
            if (actor == null || (actor.ActionId != state.id || !actor.IsTraversing) && !actor.WindowNeedsClearance)
            {
                enemyWindows.Remove(threat);
                return;
            }
            if (actor.ActionId == state.id && state.damageReaction != null && state.landing == null
                && actor.ActionTime >= 0.12f && !actor.WindowNeedsClearance)
            {
                FinishEnemyWindow(threat, state);
                return;
            }
            if (actor.ActionId == state.id && actor.ActionTime > 0.28f && actor.PairMotionError > 0.38f)
            {
                state.blockedTime += Time.fixedDeltaTime;
                if (state.blockedTime >= 0.18f)
                {
                    actor.Cancel();
                    FinishEnemyWindow(threat, state);
                }
            }
            else
            {
                state.blockedTime = 0f;
            }
        }
    }
}
