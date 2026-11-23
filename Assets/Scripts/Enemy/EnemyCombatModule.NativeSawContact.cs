using UnityEngine;

public sealed partial class EnemyCombatModule
{
    // 原版 WP5300 刀链网格的半长与横向半径，使用刀链中心挂点的武器局部坐标。
    private const float NativeSawBladeHalfLength = 0.45425f;
    private const float NativeSawBladeRadius = 0.074f;
    private static readonly NativeSawStrike[] NativeSawStrikes =
    {
        // 右挥从源第 72 帧裁入，Slash 窗口为源第 169 至 179 帧。
        new("Attack_RightSwing", 196, 97, 107),
        new("Attack_LeftSwing", 148, 44, 57),
        // 0365 与 0356 使用源 Slash 区间和源 ActionEnd。
        new("Attack_DoubleSwing", 236, 64, 80),
        new("Attack_FullSpinSwing", 400, 52, 64, 100, 112, 151, 162, 201, 214, 266, 277)
    };

    private LeonController nativeSawContactTarget;
    private float nativeSawPreviousFrame;
    private ContactCapsule nativeSawPreviousBlade;
    private ContactCapsule nativeSawPreviousBody;
    private int nativeSawFriendlyHitMask;

    private bool UsesNativeSawContact => CurrentState == VillagerState.Attack && anim != null && !anim.isHuman
        && chainsawBridge != null && chainsawBridge.chainsawDamageOrigin != null
        && chainsawBridge.chainsawDamageOrigin.name == "RE4_ChainsawBlade"
        && currentKnifeAttackMove >= 0 && currentKnifeAttackMove < NativeSawStrikes.Length;

    private void UpdateNativeSawContact()
    {
        if (IsDead || IsQTEControlled || attackFinished || Time.timeScale <= 0f)
        {
            return;
        }

        var player = Player;
        if (player == null || player.currentState == LeonState.Dead
            || !player.TryGetCurrentWorldCapsule(out var top, out var bottom, out var radius))
        {
            nativeSawContactTarget = null;
            return;
        }

        var strike = NativeSawStrikes[currentKnifeAttackMove];
        var state = anim.GetCurrentAnimatorStateInfo(0);
        if (anim.IsInTransition(0))
        {
            var next = anim.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash == strike.stateHash)
            {
                state = next;
            }
        }
        if (state.shortNameHash != strike.stateHash)
        {
            return;
        }

        float frame = Mathf.Clamp01(state.normalizedTime) * strike.frameCount;
        UpdateChainsawWarning(frame, strike.frames[0]);
        var marker = chainsawBridge.chainsawDamageOrigin;
        var scale = marker.lossyScale;
        float bladeRadius = NativeSawBladeRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Vector3 bladeAxis = marker.TransformVector(Vector3.forward * NativeSawBladeHalfLength);
        bladeAxis = Vector3.ClampMagnitude(bladeAxis, Mathf.Max(0f, bladeAxis.magnitude - bladeRadius));
        var blade = new ContactCapsule(marker.position, bladeAxis, bladeRadius);
        var body = new ContactCapsule((top + bottom) * 0.5f, (top - bottom) * 0.5f, radius);
        if (nativeSawContactTarget != player || frame < nativeSawPreviousFrame)
        {
            nativeSawContactTarget = player;
            nativeSawPreviousFrame = frame;
            nativeSawPreviousBlade = blade;
            nativeSawPreviousBody = body;
        }

        for (int index = 0; index < strike.frames.Length; index += 2)
        {
            CheckNativeSawWindow(player, blade, body, frame, index / 2, strike.frames[index], strike.frames[index + 1]);
        }
        nativeSawPreviousFrame = frame;
        nativeSawPreviousBlade = blade;
        nativeSawPreviousBody = body;
    }

    private void CheckNativeSawWindow(LeonController player, ContactCapsule blade, ContactCapsule body,
        float frame, int hitIndex, int windowStart, int windowEnd)
    {
        int mask = 1 << hitIndex;
        float first = Mathf.Max(nativeSawPreviousFrame, windowStart);
        float last = Mathf.Min(frame, windowEnd);
        if ((attackHitMask & mask) != 0 || last < first)
        {
            return;
        }

        // 在两个实际呈现姿势之间用同一比例采样刀链与玩家胶囊，覆盖窗口内的快速挥击。
        int steps = Mathf.Max(1, Mathf.CeilToInt((last - first) * 2f));
        for (int index = 0; index <= steps; index++)
        {
            float sample = Mathf.Lerp(first, last, index / (float)steps);
            float weight = 1f;
            if (frame > nativeSawPreviousFrame)
            {
                weight = Mathf.InverseLerp(nativeSawPreviousFrame, frame, sample);
            }
            var sampledBlade = ContactCapsule.Interpolate(nativeSawPreviousBlade, blade, weight);
            if (siegeAttackDoor != null)
            {
                if (siegeAttackLanded || siegeAttackDoor.TrySawSiegeDoor(sampledBlade.center, sampledBlade.OuterRadius))
                {
                    siegeAttackLanded = true;
                    return;
                }
                continue;
            }

            var sampledBody = ContactCapsule.Interpolate(nativeSawPreviousBody, body, weight);
            if (!sampledBlade.Touches(sampledBody, out var point))
            {
                continue;
            }

            if (!HasClearLineToPlayer(false, Mathf.Max(attackHitRange + 0.5f, PlayerDistance + 0.1f)))
            {
                return;
            }
            attackHitMask |= mask;
            if (TryCommitChainsawThreatContact(player, sampledBlade.center, point))
            {
                return;
            }
            player.TakeDamageFrom(chainsawBridge.ResolveAttackDamage(attackDamage), sampledBlade.center, point);
            chainsawBridge.PlayChainsawHit();
            return;
        }
    }

    private readonly struct NativeSawStrike
    {
        public readonly int stateHash;
        public readonly int frameCount;
        public readonly int[] frames;

        public NativeSawStrike(string state, int frameCount, params int[] frames)
        {
            stateHash = Animator.StringToHash(state);
            this.frameCount = frameCount;
            this.frames = frames;
        }
    }

    private readonly struct ContactCapsule
    {
        public readonly Vector3 center;
        private readonly Vector3 halfAxis;
        private readonly float radius;
        public float OuterRadius => halfAxis.magnitude + radius;

        public ContactCapsule(Vector3 center, Vector3 halfAxis, float radius)
        {
            this.center = center;
            this.halfAxis = halfAxis;
            this.radius = radius;
        }

        public static ContactCapsule Interpolate(ContactCapsule previous, ContactCapsule current, float weight)
        {
            return new ContactCapsule(Vector3.Lerp(previous.center, current.center, weight),
                Vector3.Slerp(previous.halfAxis, current.halfAxis, weight), Mathf.Lerp(previous.radius, current.radius, weight));
        }

        public bool Touches(ContactCapsule other, out Vector3 point)
        {
            const float epsilon = 0.000001f;
            Vector3 start = center - halfAxis;
            Vector3 otherStart = other.center - other.halfAxis;
            Vector3 axis = 2f * halfAxis;
            Vector3 otherAxis = 2f * other.halfAxis;
            Vector3 between = start - otherStart;
            float length = axis.sqrMagnitude;
            float otherLength = otherAxis.sqrMagnitude;
            float alignment = Vector3.Dot(axis, otherAxis);
            float projected = Vector3.Dot(axis, between);
            float otherProjected = Vector3.Dot(otherAxis, between);
            float first = 0f;
            float second = 0f;

            if (otherLength > epsilon)
            {
                float denominator = length * otherLength - alignment * alignment;
                if (denominator > epsilon)
                {
                    first = Mathf.Clamp01((alignment * otherProjected - projected * otherLength) / denominator);
                }
                second = Mathf.Clamp01((alignment * first + otherProjected) / otherLength);
            }
            if (length > epsilon)
            {
                first = Mathf.Clamp01((alignment * second - projected) / length);
                if (otherLength > epsilon)
                {
                    second = Mathf.Clamp01((alignment * first + otherProjected) / otherLength);
                }
            }

            point = start + axis * first;
            Vector3 otherPoint = otherStart + otherAxis * second;
            float reach = radius + other.radius;
            return (point - otherPoint).sqrMagnitude <= reach * reach;
        }
    }
}
