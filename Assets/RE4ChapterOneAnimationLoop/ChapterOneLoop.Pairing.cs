using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const string VillagerHold = "LeonPair_0000";
        private const string SawHold = "LeonPair_1200";
        private const string SawExecution = "LeonPair_0030_jack_pl_2nd_wp5300";
        private const int VillagerTimeoutFrame = 258;
        internal const int SawTimeoutFrame = 269;
        private static readonly int[] VillagerEscapeFrames = { 40, 95, 99, 115, 175, 186, 215, 235 };
        internal static readonly int[] SawEscapeFrames = { 47, 87, 153, 173, 193, 215, 249 };

        private ChapterOnePairMotion pairMotion;
        private Threat partner;
        private Quaternion pairFacing;
        private int pairPlayerAction = -1;
        private int pairEnemyAction = -1;
        private int pairEscapeFrame;
        private float pairBlockedFor;
        private CapsuleCollider pairPlayerCollider;
        private CapsuleCollider pairEnemyCollider;
        private bool pairCollisionWasIgnored;

        private bool BeginPair(Threat threat, string id)
        {
            bool groundFinish = ChapterOneRootActor.IsGroundFinishClip(id);
            bool legBite = ChapterOneRootActor.IsLegBiteClip(id);
            if (threat == null || !threat.actor || threat.health <= 0 || threat.actor.IsTerminal
                || (!groundFinish && !legBite && (IsInMeleeReaction(threat) || IsEnemyGrounded(threat)))
                || (groundFinish && (partner != null || slot != 3 || GroundFinishId(threat) != id))
                || (legBite && !LegBiteCanPlay(threat, id))
                || !threat.actor.isActiveAndEnabled
                || threat.actor.IsTraversing || threat.actor.WindowNeedsClearance
                || !leon || !leon.isActiveAndEnabled || leon.IsTerminal || leon.IsTraversing || leon.TraversalOpeningOccupied
                || health <= 0 || partner != null && partner != threat
                || (leon.IsPaired || threat.actor.IsPaired) && partner != threat
                || partner != null && !PairCanContinue())
            {
                return false;
            }
            if (pairMotion == null)
            {
                pairMotion = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            }
            if (pairMotion == null || !leon.TryGetTake(id, out var action) || string.IsNullOrEmpty(action.partnerId)
                || !threat.actor.TryGetTake(action.partnerId, out var response)
                || !pairMotion.TryGetTrack(action.body, out var playerTrack)
                || !pairMotion.TryGetTrack(response.body, out var enemyTrack))
            {
                notice = "配对片段或根轨道缺失";
                return false;
            }

            Vector3 player = leon.MotionPosition;
            Vector3 enemy = threat.actor.MotionPosition;
            Vector3 nativePlayer = Vector3.ProjectOnPlane(action.pairOrigin, Vector3.up);
            Vector3 nativeEnemy = Vector3.ProjectOnPlane(response.pairOrigin, Vector3.up);
            Vector3 nativeOffset = nativeEnemy - nativePlayer;
            Vector3 separation = Vector3.ProjectOnPlane(enemy - player, Vector3.up);
            Quaternion facing = leon.MotionRotation;
            if (partner == threat)
            {
                facing = pairFacing;
            }
            if (nativeOffset.sqrMagnitude > 0.01f && separation.sqrMagnitude > 0.01f)
            {
                facing = Quaternion.AngleAxis(Vector3.SignedAngle(nativeOffset, separation, Vector3.up), Vector3.up);
            }
            Vector3 center = (player + enemy) * 0.5f;
            Vector3 playerStart = center - facing * (nativeOffset * 0.5f);
            Vector3 enemyStart = center + facing * (nativeOffset * 0.5f);
            Vector3 contactHeight = Vector3.up * 0.9f;
            float sourceTime = 0f;
            Pose biteStage = default;
            if (legBite)
            {
                if (!FitLegBite(threat, id, playerTrack, enemyTrack, out biteStage, out sourceTime))
                {
                    return false;
                }
                facing = biteStage.rotation;
                playerStart = biteStage.position + facing * playerTrack.sourceOrigin;
                enemyStart = biteStage.position + facing * enemyTrack.sourceOrigin;
                contactHeight = Vector3.up * 0.36f;
            }
            if (groundFinish)
            {
                if (!FitGroundFinish(threat, playerTrack, enemyTrack, out facing, out playerStart))
                {
                    notice = "需要靠近处决入场点";
                    return false;
                }
                enemyStart = enemy;
                contactHeight = Vector3.up * 0.45f;
            }
            Vector3 playerCheck = playerStart + facing * playerTrack.Sample(sourceTime).position;
            Vector3 enemyCheck = enemyStart + facing * enemyTrack.Sample(sourceTime).position;
            if ((playerCheck - player).sqrMagnitude > 1f || (enemyCheck - enemy).sqrMagnitude > 1f
                || !leon.CanAlignForPair(playerCheck) || !threat.actor.CanAlignForPair(enemyCheck)
                || Physics.Linecast(playerCheck + contactHeight, enemyCheck + contactHeight,
                    obstructionMask, QueryTriggerInteraction.Ignore))
            {
                notice = "配对位置或净空不足";
                return false;
            }

            EndReload();
            reloadAfterAction = -1;
            pendingSlot = -1;
            meleeTarget = null;
            queuedKnifeAction = 0;
            previousGait = 0f;
            previousMoveAxis = Vector2.zero;
            crouch = false;
            crouchTogglePending = false;
            aim = false;
            previousAim = false;
            cqbrAimApplied = false;
            rifleTriggerHeld = false;
            rifleShotAfterRaise = -1;
            leon.Move(Vector3.zero, Vector2.zero, 0f, 0);
            leon.SetWeaponPose(null, false);
            threat.actor.Move(Vector3.zero, Vector2.zero, 0f, 0);

            float blend = 0.16f;
            if (legBite)
            {
                blend = 0.26f;
            }
            else if (partner == threat && id == SawExecution)
            {
                blend = 0.24f;
            }
            if (partner == null)
            {
                pairPlayerCollider = leon.GetComponent<CapsuleCollider>();
                pairEnemyCollider = threat.actor.GetComponent<CapsuleCollider>();
                pairCollisionWasIgnored = Physics.GetIgnoreCollision(pairPlayerCollider, pairEnemyCollider);
                Physics.IgnoreCollision(pairPlayerCollider, pairEnemyCollider, true);
                partner = threat;
            }
            pairFacing = facing;
            pairEscapeFrame = 0;
            pairBlockedFor = 0f;
            if (!leon.PlayPaired(action, playerTrack, new Pose(playerStart, facing), blend, sourceTime))
            {
                AbortPair();
                return false;
            }
            pairPlayerAction = leon.ActionId;
            var previousEnemyAction = threat.actor.ActiveTake;
            float previousEnemyTime = threat.actor.ActionTime;
            if (!threat.actor.PlayPaired(response, enemyTrack, new Pose(enemyStart, facing), blend, sourceTime))
            {
                AbortPair();
                return false;
            }
            pairEnemyAction = threat.actor.ActionId;
            if (legBite && !pairLegBite)
            {
                pairLegBite = true;
                pairBiteStage = biteStage;
                pairBiteContactGap = float.PositiveInfinity;
                pairBiteContactFrame = 0f;
                notice = "倒地咬腿";
            }
            if (groundFinish)
            {
                var finish = Array.Find(ChapterOneRootActor.GroundFinishes, entry => entry.player == id);
                pairGroundContact = finish.contact;
                pairGroundReturnTake = null;
                if (previousEnemyAction != null)
                {
                    pairGroundReturnTake = previousEnemyAction.id;
                }
                pairGroundReturnTime = previousEnemyTime;
                pairGroundContactGap = float.PositiveInfinity;
                pairGroundSampledFrame = 0f;
                notice = finish.label;
            }
            return true;
        }

        private int PairSourceFrame()
        {
            return Mathf.FloorToInt(Mathf.Min(leon.ActionTime, partner.actor.ActionTime)
                * leon.ActiveTake.body.frameRate + 0.001f);
        }

        private void RequestPairEscape()
        {
            if (pairLegBite && PairCanContinue() && leon.ActiveTake.id is (ChapterOneRootActor.LegBiteEnter or ChapterOneRootActor.LegBiteHold))
            {
                pairBiteEscape = true;
                return;
            }
            if (pairEscapeFrame > 0 || partner == null)
            {
                return;
            }
            if (!PairCanContinue())
            {
                AbortPair();
                return;
            }
            string id = leon.ActiveTake.id;
            if (id != VillagerHold && id != SawHold)
            {
                return;
            }
            int[] seams = VillagerEscapeFrames;
            int timeout = VillagerTimeoutFrame;
            if (id == SawHold)
            {
                seams = SawEscapeFrames;
                timeout = SawTimeoutFrame;
            }
            int current = PairSourceFrame();
            if (current >= timeout)
            {
                return;
            }
            // 按键先登记，双方到达同一个接触相位后再接原版挣脱。
            foreach (int frame in seams)
            {
                if (frame >= current)
                {
                    pairEscapeFrame = frame;
                    return;
                }
            }
            pairEscapeFrame = current + 1;
        }

        private void UpdatePair()
        {
            if (partner == null)
            {
                return;
            }
            if (!PairCanContinue())
            {
                AbortPair();
                return;
            }
            if (Mathf.Max(leon.PairMotionError, partner.actor.PairMotionError) > 0.32f)
            {
                pairBlockedFor += Time.fixedDeltaTime;
                if (pairBlockedFor > 0.16f)
                {
                    notice = "配对动作受阻";
                    AbortPair();
                    return;
                }
            }
            else
            {
                pairBlockedFor = 0f;
            }
            string id = leon.ActiveTake.id;
            if (pairLegBite)
            {
                UpdateLegBite(id);
                return;
            }
            if (!UpdateGroundFinishContact())
            {
                return;
            }
            if (id is VillagerHold or SawHold)
            {
                int timeout = VillagerTimeoutFrame;
                if (id == SawHold)
                {
                    timeout = SawTimeoutFrame;
                }
                int frame = PairSourceFrame();
                if (pairEscapeFrame > 0 && frame >= pairEscapeFrame)
                {
                    ResolvePair(true);
                }
                else if (frame >= timeout)
                {
                    ResolvePair(false);
                }
                return;
            }
            if (leon.PairedFinished && (partner.actor.PairedFinished || pairGroundCommitted || id == "LeonPair_0020"))
            {
                CompletePair(id);
            }
        }

        private void ResolvePair(bool success)
        {
            string next = "LeonPair_0030";
            if (leon.ActiveTake.id == SawHold)
            {
                next = SawExecution;
                if (success)
                {
                    next = "LeonPair_1202";
                }
            }
            else if (success)
            {
                next = "LeonPair_0020";
            }
            if (!BeginPair(partner, next))
            {
                AbortPair();
            }
        }

        private void CompletePair(string id)
        {
            Threat other = partner;
            bool groundFinish = pairGroundContact > 0;
            bool legBite = pairLegBite;
            ReleasePairSession();
            if (legBite)
            {
                ReleaseLegBitePlayer();
                ResumeLegBiteCrawler(other);
                return;
            }
            if (id.StartsWith("LeonPair_0030", StringComparison.Ordinal) || id == "LeonPair_0035")
            {
                health = 0;
                leon.HoldTerminal();
            }
            else
            {
                leon.Cancel();
                if (slot == 2)
                {
                    SetCqbrAimPose(false);
                }
            }
            if (groundFinish)
            {
                FinishGroundFinishTarget(other);
            }
            else if (id == "LeonPair_1101")
            {
                DamageEnemy(other, other.health);
            }
            else if (id == "LeonPair_0020")
            {
                other.actor.ReleasePairAfterTail();
            }
            else
            {
                other.actor.Cancel();
            }
        }

        private void AbortPair()
        {
            Threat other = partner;
            bool cancelPlayer = OwnsPairAction(leon, pairPlayerAction);
            bool cancelEnemy = other != null && OwnsPairAction(other.actor, pairEnemyAction);
            bool groundFinish = pairGroundContact > 0;
            bool legBite = pairLegBite;
            bool committed = pairGroundCommitted;
            string returnTake = pairGroundReturnTake;
            float returnTime = pairGroundReturnTime;
            ReleasePairSession();
            if (cancelEnemy)
            {
                if (legBite)
                {
                    ResumeLegBiteCrawler(other);
                }
                else if (groundFinish && other.actor.isActiveAndEnabled)
                {
                    RestoreGroundFinishTarget(other, returnTake, returnTime, committed);
                }
                else
                {
                    other.actor.Cancel();
                }
            }
            if (cancelPlayer)
            {
                if (legBite)
                {
                    ReleaseLegBitePlayer();
                }
                else
                {
                    leon.Cancel();
                }
            }
            if (other != null && leon != null && slot == 2 && (cancelPlayer || !leon.IsBusy))
            {
                SetCqbrAimPose(false);
            }
        }

        private bool PairCanContinue()
        {
            return partner != null && (health > 0 || pairLegBite && pairBiteContactHandled)
                && (partner.health > 0 || pairGroundCommitted)
                && OwnsPairAction(leon, pairPlayerAction) && leon.isActiveAndEnabled && leon.HasValidStageTrack
                && OwnsPairAction(partner.actor, pairEnemyAction) && partner.actor.isActiveAndEnabled
                && partner.actor.HasValidStageTrack;
        }

        private static bool OwnsPairAction(ChapterOneRootActor actor, int id) =>
            actor != null && actor.IsPaired && actor.ActionId == id;

        private void ReleasePairSession()
        {
            if (pairPlayerCollider != null && pairEnemyCollider != null)
            {
                Physics.IgnoreCollision(pairPlayerCollider, pairEnemyCollider, pairCollisionWasIgnored);
            }
            pairPlayerCollider = null;
            pairEnemyCollider = null;
            partner = null;
            pairPlayerAction = -1;
            pairEnemyAction = -1;
            pairEscapeFrame = 0;
            pairBlockedFor = 0f;
            pairGroundContact = 0;
            pairGroundCommitted = false;
            pairGroundReturnTake = null;
            pairGroundReturnTime = 0f;
            pairLegBite = false;
            pairBiteEscape = false;
            pairBiteContactHandled = false;
        }

        private string PairPrompt()
        {
            if (pairLegBite && leon.ActiveTake != null
                && leon.ActiveTake.id is (ChapterOneRootActor.LegBiteEnter or ChapterOneRootActor.LegBiteHold))
            {
                if (pairBiteEscape)
                {
                    return "已登记挣脱";
                }
                return "倒地咬腿，按 F 挣脱";
            }
            if (leon.ActiveTake != null && leon.ActiveTake.id is VillagerHold or SawHold)
            {
                if (pairEscapeFrame > 0)
                {
                    return "已登记挣脱，等待双方接触帧";
                }
                return "配对进行中，按 F 挣脱";
            }
            return "配对动作进行中";
        }
    }
}
