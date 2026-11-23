using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const float GroundOffset = 0.018f;
        private const string WindowThrough = "LeonTraverse_0370";
        private const string WindowDash = "LeonTraverse_0371";
        private const string WindowDrop = "LeonTraverse_0375";
        private const string WindowLand = "LeonTraverse_0374";

        private ChapterOneRootActor.Take traversalLanding;
        private ChapterOnePairMotion.Track traversalLandingTrack;
        private Pose traversalLandingStart;
        private Transform playerTraversalWindow;
        private int traversalAction;
        private float traversalBlocked;

#if UNITY_EDITOR
        internal void ConfigureTraversal(Transform levelWindow, Transform elevatedWindow)
        {
            vault = levelWindow;
            dropVault = elevatedWindow;
        }
#endif

        private bool TryTraverse()
        {
            if (crouch || reload || pendingSlot >= 0 || partner != null || leon.BlocksLocomotion
                || leon.IsTerminal || leon.TraversalOpeningOccupied)
            {
                return false;
            }

            Vector3 at = leon.MotionPosition;
            if (dropVault != null && (at - dropVault.position).sqrMagnitude < 9f)
            {
                return TryWindow(dropVault, true);
            }
            if (vault != null && (at - vault.position).sqrMagnitude < 9f)
            {
                return TryWindow(vault, false);
            }
            return false;
        }

        private bool TryWindow(Transform window, bool drop)
        {
            if (!window.TryGetComponent<Collider>(out var sill))
            {
                return false;
            }
            float floor = sill.bounds.min.y;
            Vector3 at = leon.MotionPosition;
            Vector3 local = Quaternion.Inverse(window.rotation) * (at - window.position);
            if (Mathf.Abs(at.y - floor) > 0.23f || Mathf.Abs(local.x) > 0.65f
                || Mathf.Abs(local.z) < 0.34f || (drop && local.z > 0f))
            {
                return false;
            }

            Quaternion facing = window.rotation;
            if (local.z > 0f)
            {
                facing *= Quaternion.Euler(0f, 180f, 0f);
            }
            if (Quaternion.Angle(leon.MotionRotation, facing) > 54f)
            {
                notice = "面向窗口再翻越";
                return false;
            }
            if (drop)
            {
                return TryWindowTake(window, sill, facing, WindowDrop, true);
            }
            if (gait >= 2f && TryWindowTake(window, sill, facing, WindowDash, false))
            {
                return true;
            }
            return TryWindowTake(window, sill, facing, WindowThrough, false);
        }

        private bool TryWindowTake(Transform window, Collider sill, Quaternion facing, string id, bool drop)
        {
            if (WindowReserved(window, leon) || !TryTraversalTrack(leon, id, out var take, out var track))
            {
                return false;
            }
            Vector3 at = leon.MotionPosition;
            Vector3 startPosition = window.position + facing * Vector3.ProjectOnPlane(take.pairOrigin, Vector3.up);
            startPosition.y = at.y;
            Vector3 offset = startPosition - at;
            if (offset.sqrMagnitude > 0.50f * 0.50f)
            {
                return false;
            }
            var start = new Pose(startPosition, facing);
            float endTime = take.body.length;
            ChapterOneRootActor.Take landing = null;
            ChapterOnePairMotion.Track landingTrack = null;
            Pose landingStart = default;
            if (drop)
            {
                if (!TryTraversalTrack(leon, WindowLand, out landing, out landingTrack)
                    || !FindWindowLanding(window, track, start, out endTime, out landingStart)
                    || !TraversalTrackClear(leon, landingTrack, landingStart, landing.body.length, null))
                {
                    notice = "窗外落地区域被挡住";
                    return false;
                }
            }
            else
            {
                Vector3 end = At(track, start, endTime);
                if (!FindTraversalGround(end + Vector3.up * 0.32f, 0.48f, out _))
                {
                    notice = "窗外没有平层落脚点";
                    return false;
                }
            }

            if (!leon.CanAlignForPair(startPosition + Vector3.up * 0.04f, 0.04f)
                || !TraversalTrackClear(leon, track, start, endTime, sill)
                || !leon.PlayTraversal(take, track, start, endTime, 0.16f, sill))
            {
                notice = "翻越路径被挡住";
                return false;
            }

            traversalLanding = landing;
            traversalLandingTrack = landingTrack;
            traversalLandingStart = landingStart;
            playerTraversalWindow = window;
            traversalAction = leon.ActionId;
            traversalBlocked = 0f;
            notice = "";
            return true;
        }

        private bool TryTraversalTrack(ChapterOneRootActor actor, string id,
            out ChapterOneRootActor.Take take, out ChapterOnePairMotion.Track track)
        {
            take = null;
            track = null;
            if (pairMotion == null)
            {
                pairMotion = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            }
            return pairMotion != null && actor.TryGetTake(id, out take) && pairMotion.TryGetTrack(take.body, out track);
        }

        private bool FindWindowLanding(Transform window, ChapterOnePairMotion.Track track, Pose start,
            out float takeEnd, out Pose landing)
        {
            takeEnd = 0f;
            landing = default;
            int frames = Mathf.RoundToInt(track.clip.length * track.clip.frameRate);
            for (int frame = 43; frame <= frames; frame++)
            {
                float time = frame / track.clip.frameRate;
                Vector3 point = At(track, start, time);
                if (Vector3.Dot(point - window.position, start.rotation * Vector3.forward) < 0.52f)
                {
                    continue;
                }
                Vector3 probe = new(point.x, start.position.y + 0.12f, point.z);
                if (!FindTraversalGround(probe, 3.6f, out var floor) || floor.y > start.position.y - 0.65f)
                {
                    continue;
                }
                if (point.y > floor.y + 0.20f)
                {
                    continue;
                }
                // 落地片段的起始脚位略高于地面，保留最后一小段下降给入场混合。
                if (point.y < floor.y - 0.05f)
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

        private bool FindTraversalGround(Vector3 probe, float distance, out Vector3 ground)
        {
            ground = default;
            if (!Physics.Raycast(probe, Vector3.down, out var hit, distance, obstructionMask, QueryTriggerInteraction.Ignore)
                || hit.normal.y < 0.70f)
            {
                return false;
            }
            ground = hit.point;
            return true;
        }

        private static bool TraversalTrackClear(ChapterOneRootActor actor, ChapterOnePairMotion.Track track,
            Pose start, float end, Collider sill, bool enemyWindow = false)
        {
            float sample = 4f / track.clip.frameRate;
            for (float time = 0f; time < end; time += sample)
            {
                if (!ClearAt(time))
                {
                    return false;
                }
            }
            return ClearAt(end);

            bool ClearAt(float time)
            {
                float frame = -1f;
                if (enemyWindow)
                {
                    frame = time * track.clip.frameRate;
                }
                return actor.TraversalPoseClear(At(track, start, time), sill, frame);
            }
        }

        private static Vector3 At(ChapterOnePairMotion.Track track, Pose start, float time)
        {
            return start.position + start.rotation * track.Sample(time).position;
        }

        private bool CompleteTraversal(ChapterOneRootActor.Take action, int id)
        {
            if (id != traversalAction)
            {
                return false;
            }
            traversalAction = 0;
            if (action.id != WindowDrop || traversalLanding == null)
            {
                return false;
            }
            var landing = traversalLanding;
            var track = traversalLandingTrack;
            traversalLanding = null;
            traversalLandingTrack = null;
            if (track == null || !track.Matches(landing.body))
            {
                notice = "落地片段或根轨道缺失";
                return true;
            }
            if (TraversalTrackClear(leon, track, traversalLandingStart, landing.body.length, null)
                && leon.PlayTraversal(landing, track, traversalLandingStart, landing.body.length, 0.16f, null))
            {
                traversalAction = leon.ActionId;
                traversalBlocked = 0f;
            }
            else
            {
                notice = "落地路线被挡住";
            }
            return true;
        }

        private void UpdateTraversal()
        {
            if (traversalAction == 0)
            {
                return;
            }
            if (traversalAction != leon.ActionId)
            {
                traversalAction = 0;
                traversalLanding = null;
                traversalLandingTrack = null;
                return;
            }
            if (leon.ActionTime > 0.20f && leon.PairMotionError > 0.36f)
            {
                traversalBlocked += Time.fixedDeltaTime;
                if (traversalBlocked >= 0.16f)
                {
                    leon.Cancel();
                    notice = "通行中断";
                }
            }
            else
            {
                traversalBlocked = 0f;
            }
        }
    }
}
