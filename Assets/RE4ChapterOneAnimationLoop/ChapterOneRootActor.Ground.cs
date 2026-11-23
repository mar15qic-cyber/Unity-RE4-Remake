using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        internal const string CrawlBackClip = "GanadoGround_Back";
        internal const string CrawlAttackClip = "GanadoGround_Attack";
        internal static readonly (string id, string label, string source)[] LegacyCrawlClips =
        {
            (CrawlBackClip, "匍匐后退", "Zombie_Crawl_Back"),
            (CrawlAttackClip, "匍匐挥击", "Zombie_Crawl_Attack01")
        };
        internal static readonly (string id, string label)[] EnemyGroundClips =
        {
            ("GanadoGround_0421", "左腿绊倒"),
            ("GanadoGround_0521", "右腿绊倒"),
            ("GanadoGround_3001", "俯卧保持"),
            ("GanadoGround_3500", "俯卧起身"),
            ("GanadoGround_1001", "俯卧死亡"),
            ("GanadoGround_2120", "匍匐起步"),
            ("GanadoGround_2121", "匍匐循环"),
            ("GanadoGround_2122", "匍匐左侧收势"),
            ("GanadoGround_2123", "匍匐右侧收势")
        };

        private float standingGroundHeight;
        private Vector3 standingGroundCenter;
        private Transform crawlStrikeWrist;
        private Transform crawlStrikeFinger;
        private ChapterOnePairMotion.Track crawlBackTrack;
        private int crawlBackAction;
        private float crawlBackTime;

        internal static bool IsEnemyGroundLoop(string id) =>
            id is "GanadoGround_3001" or "GanadoGround_2121";

        private static bool IsEnemyCrawlTravel(string id) =>
            id is "GanadoGround_2120" or "GanadoGround_2121" or "GanadoGround_2122" or "GanadoGround_2123" or CrawlBackClip;

        private void RegisterCrawlCombat()
        {
            foreach (var item in LegacyCrawlClips)
            {
                RegisterGeneratedTake(item.id, item.label, "");
            }
            Transform[] joints = GetComponentsInChildren<Transform>(true);
            crawlStrikeWrist = Array.Find(joints, joint => joint.name == "L_Hand");
            crawlStrikeFinger = Array.Find(joints, joint => joint.name == "L_MiddleF2");
        }

        internal bool TryGetCrawlStrike(out Vector3 point)
        {
            point = Vector3.zero;
            if (crawlStrikeWrist == null || crawlStrikeFinger == null)
            {
                return false;
            }
            point = MeleeMotionPoint(Vector3.Lerp(crawlStrikeWrist.position, crawlStrikeFinger.position, 0.85f));
            return true;
        }

        internal Vector3 ClosestBodyPoint(Vector3 point) => capsule.ClosestPoint(point);

        private bool TryReadCrawlBackStep(Take current, bool sampled, out Vector3 step)
        {
            step = Vector3.zero;
            if (!sampled || current == null || current.id != CrawlBackClip)
            {
                return false;
            }
            if (crawlBackAction != actionId)
            {
                crawlBackAction = actionId;
                crawlBackTime = 0f;
                crawlBackTrack = null;
                var motion = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
                if (motion != null)
                {
                    motion.TryGetTrack(current.body, out crawlBackTrack);
                }
            }
            if (crawlBackTrack == null)
            {
                return false;
            }
            // 逐步取根轨差值。刚体撞住时，由当前物理步处理这次位移。
            step = crawlBackTrack.Sample(actionTime).position - crawlBackTrack.Sample(crawlBackTime).position;
            crawlBackTime = actionTime;
            return true;
        }

        internal bool IsEnemyCrawlBlocked(Vector3 heading)
        {
            if (heading.sqrMagnitude < 0.0001f)
            {
                return true;
            }
            Vector3 direction = heading.normalized;
            return body.SweepTest(direction, out var hit, 0.12f, QueryTriggerInteraction.Ignore)
                && hit.normal.y < 0.55f && Vector3.Dot(hit.normal, direction) < -0.25f;
        }

        internal void SetEnemyGroundHeight(float down)
        {
            if (standingGroundHeight <= 0f)
            {
                standingGroundHeight = capsule.height;
                standingGroundCenter = capsule.center;
            }
            float lowHeight = Mathf.Max(capsule.radius * 2f, 0.62f);
            capsule.height = Mathf.Lerp(standingGroundHeight, lowHeight, down);
            capsule.center = standingGroundCenter - Vector3.up * ((standingGroundHeight - capsule.height) * 0.5f);
        }

        internal bool CanRecoverFromGround()
        {
            Vector3 center = body.position + body.rotation * Vector3.Scale(standingGroundCenter, transform.lossyScale);
            float spine = Mathf.Max(0f, standingGroundHeight * 0.5f - capsule.radius);
            return !Physics.CheckCapsule(center - Vector3.up * spine, center + Vector3.up * spine,
                capsule.radius * 0.94f, worldMask, QueryTriggerInteraction.Ignore);
        }

        internal void RestoreEnemyGroundHeight()
        {
            if (standingGroundHeight <= 0f)
            {
                return;
            }
            capsule.height = standingGroundHeight;
            capsule.center = standingGroundCenter;
            standingGroundHeight = 0f;
        }
    }
}
