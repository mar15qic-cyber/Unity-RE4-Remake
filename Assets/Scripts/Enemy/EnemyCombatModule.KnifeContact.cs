using System;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 KnifeEnemyAttackSegment；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[Serializable]
public sealed class KnifeEnemyAttackSegment
{
    /// <summary>原始源动作按一百二十赫兹采样。本段使用的手骨；原始源动作按一百二十赫兹采样。</summary>
    [Tooltip("本段使用的手骨；原始源动作按一百二十赫兹采样。")]
    public HumanBodyBones hand;
    /// <summary>开始。</summary>
    public float begin;
    /// <summary>结束。</summary>
    public float end;
    /// <summary>半径。</summary>
    public float radius = .12f;
    /// <summary>局部点集合。</summary>
    public Vector3[] localPoints = Array.Empty<Vector3>();
    [HideInInspector] public Vector3[] localEndPoints = Array.Empty<Vector3>();
    [HideInInspector] public string contactBonePath;
    [HideInInspector] public Vector3 contactLocalStart;
    [HideInInspector] public Vector3 contactLocalEnd;
    /// <summary>Sample 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)、Mathf.FloorToInt(float)、Mathf.InverseLerp(float, float, float)、Mathf.Min(int, int)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.LateUpdate()；下游（含内部回调体） Mathf.Clamp(int, int, int)、Mathf.FloorToInt(float)、Mathf.InverseLerp(float, float, float)、Mathf.Min(int, int)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="seconds">秒。</param>
    public Vector3 Sample(float seconds)
    {
        // 帧。
float frame = Mathf.InverseLerp(begin, end, seconds) * (localPoints.Length - 1);
        // 首个。
int first = Mathf.Clamp(Mathf.FloorToInt(frame), 0, localPoints.Length - 1);
        // 第二。
int second = Mathf.Min(first + 1, localPoints.Length - 1);
        return Vector3.Lerp(localPoints[first], localPoints[second], frame - first);
    }
}

/// <summary>敌人行为、动作与通行组件 EnemyCombatModule，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>敌人攻击与接触结算宿主，结合角色当前动作、时序及命中信息提交战斗事实；小刀接触通过既有攻击上下文与招架入口通信。</remarks>
public sealed partial class EnemyCombatModule
{
    private readonly HitQueryBuffer knifeContactOcclusionHits = new();

    /// <summary>是否小刀时间轴采样到的。</summary>
    private const float KnifeSweepStep = 1f / 120f;

    private struct KnifeSweepFrame
    {
        public Vector3 origin;
        public Quaternion rotation;
        public Vector3 playerPosition;
        public Quaternion playerRotation;
        public Vector3 capsuleTop;
        public Vector3 capsuleBottom;
        public float capsuleRadius;

        public static KnifeSweepFrame Interpolate(KnifeSweepFrame first, KnifeSweepFrame second, float fraction)
        {
            return new KnifeSweepFrame
            {
                origin = Vector3.Lerp(first.origin, second.origin, fraction),
                rotation = Quaternion.Slerp(first.rotation, second.rotation, fraction),
                playerPosition = Vector3.Lerp(first.playerPosition, second.playerPosition, fraction),
                playerRotation = Quaternion.Slerp(first.playerRotation, second.playerRotation, fraction),
                capsuleTop = Vector3.Lerp(first.capsuleTop, second.capsuleTop, fraction),
                capsuleBottom = Vector3.Lerp(first.capsuleBottom, second.capsuleBottom, fraction),
                capsuleRadius = Mathf.Lerp(first.capsuleRadius, second.capsuleRadius, fraction)
            };
        }
    }

    private struct KnifeSweepContact
    {
        public KnifeSweepFrame frame;
        public Vector3 position;
        public float clipTime;
    }

    private bool knifeTimelineSampled;
    private float previousKnifeClipTime;
    private double previousKnifeWorldTime;
    private KnifeSweepFrame previousKnifeFrame;
    private int knifeContactMask;
    private int knifeFriendlyFireMask;

    private bool UsesKnifeContactTimeline
    {
        get
        {
            KnifeEnemyAttackProfile profile = CurrentKnifeConfiguration;
            return chainsawBridge == null && CurrentState == VillagerState.Attack && profile != null
                && profile.sourceClip != null && profile.contactSegments != null && profile.contactSegments.Length > 0;
        }
    }

    private void ResetKnifeContactTimeline()
    {
        ClearStandingMelee();
        nativeSawContactTarget = null;
        nativeSawFriendlyHitMask = 0;
        knifeTimelineSampled = false;
        knifeContactMask = 0;
        knifeFriendlyFireMask = 0;
        previousKnifeClipTime = 0f;
        previousKnifeWorldTime = Time.timeAsDouble;
        previousKnifeFrame = default;
    }

    private float HorizontalSwingDistance(LeonController player)
    {
        return Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude;
    }

    private float CalibratedStandingAttackRange()
    {
        if (chainsawBridge != null)
        {
            return 0f;
        }

        KnifeEnemyAttackProfile[] profiles = ActiveKnifeAttackProfiles;
        int count = Mathf.Max(1, standingAttackAnimationCount);
        if (profiles == null || profiles.Length < count)
        {
            return 0f;
        }

        float range = 0f;
        for (int index = 0; index < count; index++)
        {
            KnifeEnemyAttackProfile profile = profiles[index];
            if (profile == null || profile.maximumStartDistance <= 0f)
            {
                return 0f;
            }

            range = Mathf.Max(range, profile.maximumStartDistance);
        }

        return range;
    }

    private int SelectStandingAttack()
    {
        int count = Mathf.Max(1, standingAttackAnimationCount);
        int choice = UnityEngine.Random.Range(0, count);
        KnifeEnemyAttackProfile[] profiles = ActiveKnifeAttackProfiles;
        LeonController player = Player;
        if (profiles == null || profiles.Length < count || player == null)
        {
            return choice;
        }

        float distance = HorizontalSwingDistance(player);
        if (CalibratedStandingAttackRange() > 0f)
        {
            // 随机起点保留招式变化，并且只从当前距离可达的招式中选取。
            for (int offset = 0; offset < count; offset++)
            {
                int index = (choice + offset) % count;
                if (distance <= profiles[index].maximumStartDistance && CanReachWithStandingPose(index, player))
                {
                    return index;
                }
            }

            return -1;
        }

        if (profiles[choice] == null || profiles[choice].swingStartDistance <= 0f)
        {
            return choice;
        }

        float bestError = Mathf.Abs(profiles[choice].swingStartDistance - distance);
        for (int index = 0; index < count; index++)
        {
            KnifeEnemyAttackProfile profile = profiles[index];
            if (profile == null)
            {
                continue;
            }

            float approach = profile.swingStartDistance;
            float error = Mathf.Abs(approach - distance);
            if (approach > 0f && error + .08f < bestError)
            {
                choice = index;
                bestError = error;
            }
        }

        return choice;
    }

    private void LateUpdate()
    {
        if (NativeCombatActive)
        {
            CheckNativeGrabContact();
            return;
        }
        UpdateStandingRebound();
        if (UsesNativeSawContact)
        {
            UpdateNativeSawContact();
            return;
        }

        if (!UsesKnifeContactTimeline || IsDead || IsQTEControlled || attackFinished || Time.timeScale <= 0f)
        {
            return;
        }

        LeonController player = Player;
        if (player == null || anim == null)
        {
            return;
        }

        KnifeCombatModule knife = player.GetComponent<KnifeCombatModule>();
        if (knife != null)
        {
            knife.CloseIncomingAttackPrompt(host);
        }

        if (!TryReadKnifeAttackState(out AnimatorStateInfo state))
        {
            return;
        }

        KnifeEnemyAttackProfile profile = CurrentKnifeConfiguration;
        if (profile.usePoseContact)
        {
            UpdateStandingMelee(profile, state, player, knife);
            return;
        }

        float clipTime = Mathf.Clamp01(state.normalizedTime) * profile.sourceClip.length;
        double now = Time.timeAsDouble;
        float rate = Mathf.Abs(anim.speed * state.speed * state.speedMultiplier);
        KnifeSweepFrame current = new KnifeSweepFrame
        {
            origin = transform.position,
            rotation = transform.rotation,
            playerPosition = player.transform.position,
            playerRotation = player.transform.rotation
        };
        bool hasCapsule = player.TryGetCurrentWorldCapsule(out current.capsuleTop, out current.capsuleBottom, out current.capsuleRadius);
        bool canContact = hasCapsule && IsPlayerWithinCombatVerticalRange(player);

        if (!knifeTimelineSampled || clipTime < previousKnifeClipTime)
        {
            previousKnifeFrame = current;
            previousKnifeClipTime = 0f;
            if (rate > .0001f)
            {
                previousKnifeWorldTime = Math.Max(previousKnifeWorldTime, now - clipTime / rate);
            }

            knifeTimelineSampled = true;
        }
        else if (now > previousKnifeWorldTime && clipTime > previousKnifeClipTime)
        {
            rate = (clipTime - previousKnifeClipTime) / (float)(now - previousKnifeWorldTime);
        }

        if (previousKnifeFrame.capsuleRadius <= 0f)
        {
            previousKnifeFrame = current;
        }

        bool contacted = false;
        float lastEnd = 0f;
        for (int index = 0; index < profile.contactSegments.Length && index < 30; index++)
        {
            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            if (segment == null)
            {
                continue;
            }

            lastEnd = Mathf.Max(lastEnd, segment.end);
            int segmentBit = 1 << index;
            if (clipTime >= segment.end && (knifeFriendlyFireMask & segmentBit) == 0)
            {
                knifeFriendlyFireMask |= segmentBit;
                TryApplyFriendlyFire(transform.position + Vector3.up, transform.forward, attackDamage);
            }

            if (!canContact || (knifeContactMask & segmentBit) != 0 || clipTime <= previousKnifeClipTime)
            {
                continue;
            }

            if (TrySweepKnifeSegment(profile, segment, previousKnifeClipTime, clipTime, previousKnifeFrame, current,
                player, out KnifeSweepContact hit))
            {
                float fraction = Mathf.InverseLerp(previousKnifeClipTime, clipTime, hit.clipTime);
                double contactTime = previousKnifeWorldTime + (now - previousKnifeWorldTime) * fraction;
                KnifeIncomingAttackContext context = CreateSweepContext(index, segment, hit);
                context.contactTime = contactTime;
                context.contactEvidence = "轨迹扫掠";
                knifeContactMask |= segmentBit;
                attackHitMask |= segmentBit;
                contacted = true;
                if (knife != null && knife.Ready)
                {
                    knife.CommitIncomingAttack(context);
                }
                else
                {
                    player.TakeDamageFrom(context.damage, context.contactPosition, current.playerPosition + Vector3.up);
                }
            }
        }

        if (!contacted && canContact && knife != null && knife.Ready && profile.canParry && enableKnifeParry && rate > .0001f)
        {
            float lead = Mathf.Min(knife.configuration.tapWindow, profile.promptEarly);
            float predictionEnd = Mathf.Min(lastEnd, clipTime + lead * rate);
            if (predictionEnd > clipTime)
            {
                Vector3 rootDelta = SampleKnifeRoot(profile, predictionEnd) - SampleKnifeRoot(profile, clipTime);
                Vector3 displacement = current.rotation * rootDelta;
                Vector3 allowedDisplacement = host.PreviewStandingAttackDisplacement(displacement);
                RegisterPredictedKnifeContact(profile, knife, player, current, clipTime, predictionEnd, now, rate, allowedDisplacement);
            }
        }

        if (clipTime >= lastEnd)
        {
            normalSwingHitHasRecoverWindow = true;
        }

        previousKnifeClipTime = clipTime;
        previousKnifeWorldTime = now;
        previousKnifeFrame = current;
    }

    private bool TryReadKnifeAttackState(out AnimatorStateInfo state)
    {
        state = anim.GetCurrentAnimatorStateInfo(0);
        if (!IsStandingAttackAnimation(state) && anim.IsInTransition(0))
        {
            state = anim.GetNextAnimatorStateInfo(0);
        }

        return IsStandingAttackAnimation(state);
    }

    internal Vector3 AdjustStandingKnifeRootMotion(Vector3 displacement)
    {
        if (!UsesKnifeContactTimeline || anim == null || normalSwingHitHasRecoverWindow)
        {
            return displacement;
        }

        float reduction = CurrentKnifeConfiguration.windupLateralReduction;
        if (reduction <= 0f || !TryReadKnifeAttackState(out _))
        {
            return displacement;
        }

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();
        return displacement - right * (Vector3.Dot(displacement, right) * Mathf.Clamp01(reduction));
    }

    private KnifeIncomingAttackContext CreateSweepContext(int index, KnifeEnemyAttackSegment segment, KnifeSweepContact hit)
    {
        KnifeIncomingAttackContext context = CreateKnifeIncomingAttack(index, attackDamage, hit.position, hit.frame.rotation * Vector3.forward);
        context.attackOrigin = hit.frame.origin;
        context.hasAttackOrigin = true;
        context.playerPositionAtContact = hit.frame.playerPosition;
        context.hasPlayerPositionAtContact = true;
        context.playerForwardAtContact = hit.frame.playerRotation * Vector3.forward;
        context.hasPlayerFacingAtContact = true;
        context.segmentBegin = segment.begin;
        context.segmentEnd = segment.end;
        context.animationTime = hit.clipTime;
        return context;
    }

    private void RegisterPredictedKnifeContact(KnifeEnemyAttackProfile profile, KnifeCombatModule knife, LeonController player,
        KnifeSweepFrame current, float clipTime, float predictionEnd, double now, float rate, Vector3 allowedDisplacement)
    {
        int bestIndex = -1;
        KnifeSweepContact best = default;
        for (int index = 0; index < profile.contactSegments.Length && index < 30; index++)
        {
            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            if ((knifeContactMask & (1 << index)) != 0 || segment == null || clipTime >= segment.end)
            {
                continue;
            }

            if (TrySweepKnifeSegment(profile, segment, clipTime, predictionEnd, current, current, player,
                out KnifeSweepContact hit, forecast: true, allowedDisplacement: allowedDisplacement)
                && hit.clipTime > clipTime && (bestIndex < 0 || hit.clipTime < best.clipTime))
            {
                best = hit;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
        {
            KnifeIncomingAttackContext context = CreateSweepContext(bestIndex, profile.contactSegments[bestIndex], best);
            context.expectedContactTime = now + (best.clipTime - clipTime) / rate;
            context.contactEvidence = "未来轨迹扫掠";
            knife.RegisterIncomingAttackPrompt(context);
        }
    }

    private static Vector3 SampleKnifeRoot(KnifeEnemyAttackProfile profile, float seconds)
    {
        Vector3[] trajectory = profile.planarRootTrajectory;
        if (trajectory == null || trajectory.Length < 2)
        {
            return Vector3.zero;
        }

        float frame = Mathf.Clamp(seconds / KnifeEnemyAttackProfile.RootSampleInterval, 0f, trajectory.Length - 1);
        int first = Mathf.Min(Mathf.FloorToInt(frame), trajectory.Length - 2);
        return Vector3.Lerp(trajectory[first], trajectory[first + 1], frame - first);
    }

    private bool TrySweepKnifeSegment(KnifeEnemyAttackProfile profile, KnifeEnemyAttackSegment segment, float fromTime, float toTime,
        KnifeSweepFrame from, KnifeSweepFrame to, LeonController player, out KnifeSweepContact contact,
        bool forecast = false, Vector3 allowedDisplacement = default)
    {
        contact = default;
        if (segment.localPoints == null || segment.localPoints.Length < 2 || toTime < segment.begin || fromTime > segment.end)
        {
            return false;
        }

        float begin = Mathf.Max(fromTime, segment.begin);
        float end = Mathf.Min(toTime, segment.end);
        float radius = segment.radius + Mathf.Clamp(profile.contactPadding, 0f, .05f);
        Vector3 scale = transform.lossyScale;
        Vector3 rootAtStart = Vector3.zero;
        float movementLimit = 0f;
        if (forecast)
        {
            rootAtStart = SampleKnifeRoot(profile, fromTime);
            movementLimit = allowedDisplacement.magnitude;
        }

        float cursor = begin;
        do
        {
            float next = Mathf.Min(end, cursor + KnifeSweepStep);
            KnifeSweepFrame first = KnifeSweepFrame.Interpolate(from, to, Mathf.InverseLerp(fromTime, toTime, cursor));
            KnifeSweepFrame second = KnifeSweepFrame.Interpolate(from, to, Mathf.InverseLerp(fromTime, toTime, next));
            if (forecast)
            {
                Vector3 startDelta = SampleKnifeRoot(profile, cursor) - rootAtStart;
                Vector3 endDelta = SampleKnifeRoot(profile, next) - rootAtStart;
                first.origin += first.rotation * Vector3.ClampMagnitude(startDelta, movementLimit);
                second.origin += second.rotation * Vector3.ClampMagnitude(endDelta, movementLimit);
            }

            Vector3 start = first.origin + first.rotation * Vector3.Scale(segment.Sample(cursor), scale);
            Vector3 finish = second.origin + second.rotation * Vector3.Scale(segment.Sample(next), scale);

            // 把玩家在这一小段内的位移转换为相对扫掠，避免低帧率时漏掉交叉接触。
            Vector3 shift = second.playerPosition - first.playerPosition;
            if (TrySweepKnifeCapsule(start + shift, finish, radius, second.capsuleBottom, second.capsuleTop,
                second.capsuleRadius, out float fraction))
            {
                KnifeSweepFrame impact = KnifeSweepFrame.Interpolate(first, second, fraction);
                Vector3 hand = Vector3.Lerp(start, finish, fraction);
                Vector3 axisPoint = ClosestPointOnKnifeAxis(hand, impact.capsuleBottom, impact.capsuleTop);
                Vector3 outward = hand - axisPoint;
                if (outward.sqrMagnitude < .000001f)
                {
                    outward = impact.origin - impact.playerPosition;
                }

                Vector3 point = axisPoint + outward.normalized * impact.capsuleRadius;
                if (KnifePathAllowed(impact, point, profile, player))
                {
                    contact.frame = impact;
                    contact.position = point;
                    contact.clipTime = Mathf.Lerp(cursor, next, fraction);
                    return true;
                }
            }

            cursor = next;
        }
        while (cursor < end);

        return false;
    }

    internal static bool TrySweepKnifeCapsule(Vector3 start, Vector3 finish, float radius, Vector3 bottom, Vector3 top,
        float capsuleRadius, out float fraction)
    {
        fraction = 0f;
        float combinedRadius = radius + capsuleRadius;
        float radiusSquared = combinedRadius * combinedRadius;
        if (KnifeAxisDistanceSquared(start, bottom, top) <= radiusSquared)
        {
            return true;
        }

        Vector3 direction = finish - start;
        float lengthSquared = direction.sqrMagnitude;
        if (lengthSquared < .00000001f)
        {
            return false;
        }

        // 先求两条线段最近的点，再在进入胶囊的一侧求首次接触。
        Vector3 axis = top - bottom;
        Vector3 offset = start - bottom;
        float axisLengthSquared = axis.sqrMagnitude;
        float alongSweep = Vector3.Dot(direction, offset);
        float closest = Mathf.Clamp01(-alongSweep / lengthSquared);
        if (axisLengthSquared > .00000001f)
        {
            float alongAxis = Vector3.Dot(axis, offset);
            float alignment = Vector3.Dot(direction, axis);
            float denominator = lengthSquared * axisLengthSquared - alignment * alignment;
            closest = 0f;
            if (denominator > .00000001f)
            {
                closest = Mathf.Clamp01((alignment * alongAxis - alongSweep * axisLengthSquared) / denominator);
            }

            float axisFraction = (alignment * closest + alongAxis) / axisLengthSquared;
            if (axisFraction < 0f)
            {
                closest = Mathf.Clamp01(-alongSweep / lengthSquared);
            }
            else if (axisFraction > 1f)
            {
                closest = Mathf.Clamp01((alignment - alongSweep) / lengthSquared);
            }
        }

        if (KnifeAxisDistanceSquared(start + direction * closest, bottom, top) > radiusSquared)
        {
            return false;
        }

        float outside = 0f;
        float inside = closest;
        for (int step = 0; step < 10; step++)
        {
            float middle = (outside + inside) * .5f;
            if (KnifeAxisDistanceSquared(start + direction * middle, bottom, top) > radiusSquared)
            {
                outside = middle;
            }
            else
            {
                inside = middle;
            }
        }

        fraction = inside;
        return true;
    }

    private static Vector3 ClosestPointOnKnifeAxis(Vector3 point, Vector3 bottom, Vector3 top)
    {
        Vector3 axis = top - bottom;
        float lengthSquared = axis.sqrMagnitude;
        if (lengthSquared < .00000001f)
        {
            return bottom;
        }

        return bottom + axis * Mathf.Clamp01(Vector3.Dot(point - bottom, axis) / lengthSquared);
    }

    private static float KnifeAxisDistanceSquared(Vector3 point, Vector3 bottom, Vector3 top)
    {
        return (point - ClosestPointOnKnifeAxis(point, bottom, top)).sqrMagnitude;
    }

    private bool KnifePathAllowed(KnifeSweepFrame frame, Vector3 contact, KnifeEnemyAttackProfile profile, LeonController player)
    {
        Vector3 direction = frame.playerPosition - frame.origin;
        direction.y = 0f;
        if (direction.magnitude > profile.contactDistance + attackRangeEpsilon
            || Vector3.Angle(frame.rotation * Vector3.forward, direction) > profile.contactAngle * .5f)
        {
            return false;
        }

        Vector3 start = frame.origin + Vector3.up;
        Vector3 line = contact - start;
        RaycastHit[] hits = knifeContactOcclusionHits.Raycast(start, line.normalized, line.magnitude, ~0, QueryTriggerInteraction.Ignore, out int hitCount);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = hits[hitIndex];
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
