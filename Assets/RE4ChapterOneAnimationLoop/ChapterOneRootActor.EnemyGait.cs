using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        internal const float EnemyAttackRange = 1.6f;
        private const float EnemyStopRange = 1.40f;
        private const float EnemyMinimumClearance = 0.95f;

        internal readonly struct EnemyGait
        {
            public readonly int exitFrame;
            public readonly int cycleFrame;
            public readonly int cycleFrames;
            public readonly float entryPhase;
            public readonly float travel;
            public readonly int fromPace;
            public readonly int toPace;

            public EnemyGait(int exitFrame, int cycleFrame, int cycleFrames, float entryPhase,
                float travel, int fromPace, int toPace)
            {
                this.exitFrame = exitFrame;
                this.cycleFrame = cycleFrame;
                this.cycleFrames = cycleFrames;
                this.entryPhase = entryPhase;
                this.travel = travel;
                this.fromPace = fromPace;
                this.toPace = toPace;
            }
        }

        // 在目标骨架上采样原版关节姿势和根位移，记录进入相位、交接帧及制动距离。
        private static readonly Dictionary<string, EnemyGait> EnemyGaits = new()
        {
            { "Ganado_1000", new(86, 64, 83, 0f, 0.68f, 0, 1) },
            { "Ganado_2000", new(61, 28, 41, 0f, 2.90f, 0, 2) },
            { "Ganado_0350", new(78, 19, 41, 3f / 83f, 2.74f, 1, 2) },
            { "Ganado_0351", new(84, 0, 41, 51f / 83f, 2.24f, 1, 2) },
            { "Ganado_0352", new(113, 62, 83, 6f / 41f, 2.56f, 2, 1) },
            { "Ganado_0353", new(87, 74, 83, 33f / 41f, 2.01f, 2, 1) },
            { "Ganado_1002", new(79, 0, 60, 1f / 83f, 0.41f, 1, 0) },
            { "Ganado_1003", new(119, 0, 60, 51f / 83f, 0.94f, 1, 0) },
            { "Ganado_2002", new(82, 0, 60, 6f / 41f, 3.37f, 2, 0) },
            { "Ganado_2003", new(59, 0, 60, 28f / 41f, 2.04f, 2, 0) },
            { "GanadoArmed_1000", new(103, 68, 84, 0f, 0.45f, 0, 1) },
            { "GanadoArmed_2000", new(62, 2, 45, 0f, 2.59f, 0, 2) },
            { "GanadoArmed_0350", new(123, 28, 45, 16f / 84f, 4.23f, 1, 2) },
            { "GanadoArmed_0351", new(92, 25, 45, 63f / 84f, 2.59f, 1, 2) },
            { "GanadoArmed_0352", new(69, 19, 84, 32f / 45f, 2.01f, 2, 1) },
            { "GanadoArmed_0353", new(152, 50, 84, 9f / 45f, 3.15f, 2, 1) },
            { "GanadoArmed_1002", new(97, 0, 60, 17f / 84f, 0.51f, 1, 0) },
            { "GanadoArmed_1003", new(138, 0, 60, 63f / 84f, 1.00f, 1, 0) },
            { "GanadoArmed_2002", new(125, 0, 60, 31f / 45f, 2.20f, 2, 0) },
            { "GanadoArmed_2003", new(147, 0, 60, 9f / 45f, 3.60f, 2, 0) },
            { "Salvador_1000", new(72, 75, 92, 0f, 0.94f, 0, 1) },
            { "Salvador_2000", new(102, 27, 53, 0f, 2.79f, 0, 2) },
            { "Salvador_0350", new(41, 52, 53, 10f / 92f, 1.72f, 1, 2) },
            { "Salvador_0351", new(106, 39, 53, 13f / 92f, 4.88f, 1, 2) },
            { "Salvador_0352", new(125, 9, 92, 15f / 53f, 3.15f, 2, 1) },
            { "Salvador_0353", new(63, 8, 92, 38f / 53f, 2.32f, 2, 1) },
            { "Salvador_1002", new(124, 11, 60, 14f / 92f, 0.38f, 1, 0) },
            { "Salvador_1003", new(170, 11, 60, 63f / 92f, 1.06f, 1, 0) },
            { "Salvador_2002", new(74, 18, 60, 39f / 53f, 1.57f, 2, 0) }
        };

        private float enemyTravelLimit = float.PositiveInfinity;

        internal static bool TryGetEnemyGait(string id, out EnemyGait edge)
        {
            if (id != null)
            {
                return EnemyGaits.TryGetValue(id, out edge);
            }
            edge = default;
            return false;
        }

        private static bool IsEnemyGaitEdge(string id) => id != null && EnemyGaits.ContainsKey(id);

        private static bool IsEnemyGaitStop(string id) => TryGetEnemyGait(id, out var edge) && edge.toPace == 0;

        private void RegisterEnemyMovement()
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (IsEnemyGaitEdge(clip.name) && !byId.ContainsKey(clip.name))
                {
                    RegisterTake(new Take
                    {
                        id = clip.name,
                        label = "敌人步态过渡",
                        body = clip,
                        contactFrames = System.Array.Empty<int>()
                    });
                }
            }
        }

        internal bool DriveEnemy(string prefix, Vector3 steer, Vector3 target, bool pursue, bool direct)
        {
            float distance = target.magnitude;
            enemyTravelLimit = float.PositiveInfinity;
            if (pursue && direct && Vector3.Dot(steer, target) > 0f)
            {
                enemyTravelLimit = Mathf.Max(0f, distance - EnemyMinimumClearance);
            }

            int previous = Mathf.RoundToInt(speed);
            int pace = 0;
            if (pursue && (!direct || distance > EnemyAttackRange) && steer.sqrMagnitude > 0.01f && !obstructed)
            {
                pace = 1;
                if (direct && (distance > 7f || previous == 0 && distance > 6.6f || previous == 2 && distance > 4.3f))
                {
                    pace = 2;
                }
            }

            float phase = GetMovementCyclePhase();
            if (movementTake != null && TryGetEnemyGait(movementTake.id, out var current))
            {
                if (current.toPace == 0)
                {
                    if (pace == 0 || direct && distance < EnemyAttackRange + current.travel + 0.70f)
                    {
                        if (direct)
                        {
                            desiredForward = target;
                        }
                        return false;
                    }
                    Move(steer, Vector2.up, current.fromPace, 0);
                    CancelMovement();
                    previous = current.fromPace;
                }
                else if (pace != 0)
                {
                    Move(steer, Vector2.up, current.toPace, 0);
                    return false;
                }
                else
                {
                    CancelMovement();
                }
            }

            float remaining = float.PositiveInfinity;
            if (pursue && direct)
            {
                remaining = Mathf.Max(0f, distance - EnemyStopRange);
            }
            string stop = null;
            if (previous > 0)
            {
                string left = "_1002";
                string right = "_1003";
                float brakingMargin = 0.14f;
                if (previous == 2)
                {
                    left = "_2002";
                    right = "_2003";
                    // 跑停入场的混合会消耗一部分制动轨道，推迟触发以免停在攻击圈外。
                    brakingMargin = -0.16f;
                }
                stop = SelectEnemyEdge(prefix, left, right, phase, remaining + 0.18f);
                if (TryGetEnemyGait(stop, out var braking) && remaining <= braking.travel + brakingMargin)
                {
                    pace = 0;
                }
            }

            string next = null;
            Vector3 heading = steer;
            if (pace == 0)
            {
                next = stop;
                heading = desiredForward;
                if (direct)
                {
                    heading = target;
                }
            }
            else if (previous == 0)
            {
                next = prefix + "_1000";
                if (pace == 2)
                {
                    next = prefix + "_2000";
                }
            }
            else if (previous < pace)
            {
                next = SelectEnemyEdge(prefix, "_0350", "_0351", phase, Mathf.Max(0f, remaining - 2.1f));
            }
            else if (previous > pace)
            {
                next = SelectEnemyEdge(prefix, "_0352", "_0353", phase, remaining);
            }

            Move(heading, Vector2.up, pace, 0);
            if (next != null && TryGetTake(next, out var action))
            {
                PlayMovement(action);
                return false;
            }
            return pursue && direct && pace == 0 && distance <= EnemyAttackRange;
        }

        private string SelectEnemyEdge(string prefix, string left, string right, float phase, float available)
        {
            string selected = null;
            float lowestCost = float.PositiveInfinity;
            Consider(prefix + left);
            Consider(prefix + right);
            return selected;

            void Consider(string id)
            {
                if (!byId.ContainsKey(id) || !TryGetEnemyGait(id, out var edge))
                {
                    return;
                }
                float phaseGap = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, edge.entryPhase * 360f)) / 360f;
                float excessTravel = Mathf.Max(0f, edge.travel - available);
                float cost = phaseGap + excessTravel * 0.7f;
                if (cost < lowestCost)
                {
                    selected = id;
                    lowestCost = cost;
                }
            }
        }

        private Vector3 LimitEnemyApproach(Vector3 step)
        {
            if (float.IsPositiveInfinity(enemyTravelLimit))
            {
                return step;
            }
            return Vector3.ClampMagnitude(step, enemyTravelLimit);
        }

        internal void LimitEnemyWindowApproach(float distance)
        {
            enemyTravelLimit = Mathf.Max(0f, distance - 0.08f);
        }
    }
}
