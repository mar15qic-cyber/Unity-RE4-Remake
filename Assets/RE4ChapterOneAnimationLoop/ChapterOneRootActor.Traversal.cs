using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        private readonly Collider[] traversalObstacles = new Collider[24];
        private Collider traversalSill;
        private float traversalEndTime;
        private int sampledTraversalAction;
        private Vector3 vaultExitVelocity;
        private float vaultExitRemaining;
        private float enemyWindowStandingHeight;
        private Vector3 enemyWindowStandingCenter;

        internal bool IsTraversing => traversalEndTime > 0f;
        internal bool WindowNeedsClearance => traversalSill != null && (TraversalOpeningOccupied || IsDeformedNearWindow());

        internal bool TraversalOpeningOccupied
        {
            get
            {
                if (traversalSill == null || capsule == null || body == null)
                {
                    return false;
                }
                return Physics.ComputePenetration(capsule, body.position, body.rotation, traversalSill,
                    traversalSill.transform.position, traversalSill.transform.rotation, out _, out _);
            }
        }

        internal bool PlayTraversal(Take action, ChapterOnePairMotion.Track motion, Pose start,
            float endTime, float blend, Collider sill)
        {
            if (motion == null || !motion.Matches(action.body) || !animator.HasState(0, action.state)
                || !Play(action.id, true, actionFade: blend))
            {
                return false;
            }
            SetStageTrack(motion, start, blend);
            traversalEndTime = endTime;
            traversalSill = sill;
            if (traversalSill != null)
            {
                Physics.IgnoreCollision(capsule, traversalSill, true);
                if (IsEnemyWindowStart(action.id))
                {
                    enemyWindowStandingHeight = capsule.height;
                    enemyWindowStandingCenter = capsule.center;
                }
            }
            return true;
        }

        internal bool TraversalPoseClear(Vector3 at, Collider sill, float enemyWindowFrame = -1f)
        {
            float height = capsule.height;
            Vector3 capsuleCenter = capsule.center;
            if (enemyWindowFrame >= 0f)
            {
                height = EnemyWindowHeight(capsule.height, enemyWindowFrame);
                capsuleCenter.y -= (capsule.height - height) * 0.5f;
            }
            Vector3 center = at + Vector3.Scale(capsuleCenter, transform.lossyScale) + Vector3.up * 0.025f;
            float radius = capsule.radius * 0.94f;
            float axis = Mathf.Max(0f, height * 0.5f - capsule.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * axis, center + Vector3.up * axis,
                radius, traversalObstacles, worldMask | (1 << 9) | (1 << 10), QueryTriggerInteraction.Ignore);
            if (count == traversalObstacles.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                Collider hit = traversalObstacles[i];
                if (hit != capsule && hit != sill)
                {
                    return false;
                }
            }
            return true;
        }

        private float PrepareVaultExit()
        {
            if (take.id != "LeonTraverse_0371" || pairTrack == null || !finished || !CanKeepVaultMomentum(body.rotation))
            {
                return 0f;
            }
            const float sampleDuration = 0.05f;
            Vector3 tail = pairTrack.Sample(traversalEndTime).position;
            Vector3 previous = pairTrack.Sample(traversalEndTime - sampleDuration).position;
            vaultExitVelocity = pairStageStart.rotation * ((tail - previous) / sampleDuration);
            vaultExitRemaining = 0.17f;
            // 对照翻窗末帧的左脚支撑、腿部姿势与运动方向。
            if (speed >= 2.5f)
            {
                return 23f / 34f;
            }
            return 26f / 47f;
        }

        private bool CanKeepVaultMomentum(Quaternion facing)
        {
            return speed >= 2f && !obstructed && Vector3.Dot(desiredForward.normalized, facing * Vector3.forward) > 0.85f;
        }

        private Vector3 BlendVaultExitVelocity(Vector3 target, Quaternion facing, float dt)
        {
            if (vaultExitRemaining <= 0f)
            {
                return target;
            }
            if (!CanKeepVaultMomentum(facing))
            {
                vaultExitRemaining = 0f;
                return target;
            }
            Vector3 forward = facing * Vector3.forward;
            float missing = Mathf.Max(0f, Vector3.Dot(vaultExitVelocity - target, forward));
            float weight = Mathf.Sqrt(Mathf.Clamp01(vaultExitRemaining / 0.17f));
            vaultExitRemaining -= dt;
            return target + forward * (missing * weight);
        }

        private void ReleaseTraversalSill()
        {
            if (WindowNeedsClearance)
            {
                return;
            }
            RestoreWindowCollision();
        }

        private bool IsDeformedNearWindow()
        {
            if (enemyWindowStandingHeight <= 0f)
            {
                return false;
            }
            Bounds bounds = traversalSill.bounds;
            bounds.Expand(capsule.radius * 2.2f);
            Vector3 at = body.position;
            at.y = bounds.center.y;
            return bounds.Contains(at);
        }

        private void RestoreWindowCollision()
        {
            if (traversalSill != null && capsule != null)
            {
                Physics.IgnoreCollision(capsule, traversalSill, false);
            }
            traversalSill = null;
            if (enemyWindowStandingHeight > 0f && capsule != null)
            {
                capsule.height = enemyWindowStandingHeight;
                capsule.center = enemyWindowStandingCenter;
            }
            enemyWindowStandingHeight = 0f;
        }

        private void OnDisable()
        {
            RestoreWindowCollision();
            RestoreEnemyGroundHeight();
        }

        private static bool IsEnemyWindowStart(string id) => id is "GanadoWindow_1100" or "SalvadorWindow_1100";

        private void RegisterEnemyWindows()
        {
            foreach (string id in new[] { "GanadoWindow_1100", "GanadoWindow_1102", "SalvadorWindow_1100", "SalvadorWindow_1102" })
            {
                string label = "翻窗落地";
                if (IsEnemyWindowStart(id))
                {
                    label = "翻窗下落";
                }
                RegisterGeneratedTake(id, label, "");
                if (byId.TryGetValue(id, out var action))
                {
                    action.verticalTravel = true;
                }
            }
        }

        private static float EnemyWindowHeight(float standing, float frame)
        {
            // 原版从第 39 帧进入地形通行形态，跨过开口后恢复正常胶囊高度。
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((frame - 39f) / 15f));
            float leave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((frame - 155f) / 15f));
            return Mathf.Lerp(standing, 1.24f, enter * (1f - leave));
        }

        private void UpdateEnemyWindowCapsule(float frame)
        {
            if (enemyWindowStandingHeight <= 0f)
            {
                return;
            }
            float height = EnemyWindowHeight(enemyWindowStandingHeight, frame);
            Vector3 center = enemyWindowStandingCenter;
            center.y -= (enemyWindowStandingHeight - height) * 0.5f;
            capsule.height = height;
            capsule.center = center;
        }
    }
}
