using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneRootActor
    {
        internal const string LowPickup = "LeonInteract_0408";
        private const float PickupEntryRate = 0.68f;
        internal static readonly (string id, string label, int frame)[] PickupTakes =
        {
            ("LeonPickup_0400", "高位正面拾取", 12),
            ("LeonPickup_0402", "高位左侧拾取", 13),
            ("LeonPickup_0404", "高位右侧拾取", 17),
            ("LeonPickup_0410", "中位正面拾取", 16),
            ("LeonPickup_0412", "中位左侧拾取", 20),
            ("LeonPickup_0414", "中位右侧拾取", 24)
        };

        private Transform pickupFinger;
        private int pickupMotionAction;
        private float pickupContactTime;

        internal Vector3 PickupHandPosition => MeleeMotionPoint(pickupFinger.position);

        internal static int PickupContactFrame(string id)
        {
            if (id == LowPickup)
            {
                return 12;
            }
            foreach (var pickup in PickupTakes)
            {
                if (id == pickup.id)
                {
                    return pickup.frame;
                }
            }
            return -1;
        }

        private void RegisterPickupTakes()
        {
            foreach (var pickup in PickupTakes)
            {
                RegisterGeneratedTake(pickup.id, pickup.label, "");
                RegisterPickupContact(pickup.id, pickup.frame);
            }
            RegisterPickupContact(LowPickup, PickupContactFrame(LowPickup));
        }

        private void RegisterPickupContact(string id, int frame)
        {
            if (byId.TryGetValue(id, out var pickup))
            {
                pickup.contactFrames = new[] { frame };
            }
        }

        internal bool PlayPickup(Take pickup, ChapterOnePairMotion.Track track, Pose start, float blend)
        {
            if (pickupFinger == null || track == null || !track.Matches(pickup.body)
                || !animator.HasState(0, pickup.state) || !Play(pickup.id, true, actionFade: blend * 1.18f))
            {
                return false;
            }
            SetStageTrack(track, start, blend);
            pickupMotionAction = actionId;
            pickupContactTime = pickup.contactFrames[0] / pickup.body.frameRate;
            animator.SetFloat(DurationScale, PickupEntryRate);
            return true;
        }

        private void UpdatePickupEntryRate()
        {
            if (actionId == pickupMotionAction)
            {
                // 让步态先交给伸手姿势，再恢复源片速度。接触仍按源帧结算。
                animator.SetFloat(DurationScale, Mathf.SmoothStep(PickupEntryRate, 1f, actionTime / pickupContactTime));
            }
        }
    }
}
