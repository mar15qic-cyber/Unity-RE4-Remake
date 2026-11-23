using RE4.ChapterOne;
using UnityEngine;

public sealed partial class EnemyAnimationAdapter
{
    internal const string NativeGaitPaceParameter = "NativeGaitPace";
    internal const string NativeGaitTag = "NativeGanadoGait";
    private const float NativeGaitBlendSeconds = 0.12f;
    private const float SourceGaitFrameRate = 60f;
    private static readonly int HashSawWalkStart = Animator.StringToHash("Salvador_1000");
    private static readonly int HashGanadoWalkStart = Animator.StringToHash("Base Layer.Ganado_1000");
    private float hitWalkStartUntil = float.NegativeInfinity;
    private static readonly int HashNativeGaitPace = Animator.StringToHash(NativeGaitPaceParameter);
    private static readonly int HashNativeLocomotion = Animator.StringToHash("Base Layer.Locomotion");

    [SerializeField] private bool nativeArmedGait;
    [SerializeField] private bool nativeSawGait;
    private bool hasNativeGaitPace;
    private int nativeGaitPace;
    private string nativeGaitEdge;

    internal void SwitchToUnarmedGanadoGait()
    {
        ResetNativeFootContacts();
        nativeArmedGait = false;
        ConfigureNativeFootContacts(true);
        nativeGaitEdge = null;
        SetNativeGaitPace(0);
    }

    internal void UpdateNativeStandingGait()
    {
        if (UpdateRevivedStandingGait())
        {
            return;
        }
        if (!hasNativeGaitPace || !host.UsesNativeStandingStride)
        {
            return;
        }

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        bool gaitState = state.IsTag(NativeGaitTag);
        if (!gaitState && state.fullPathHash != HashNativeLocomotion)
        {
            if (!anim.IsInTransition(0))
            {
                nativeGaitEdge = null;
                SetNativeGaitPace(0);
            }
            return;
        }

        // 让受击和攻击从当前脚相淡出，等动作接管后再清理步态参数。
        if (anim.IsInTransition(0) || host.IsQTEControlled
            || CurrentState == VillagerState.Staggered || CurrentState == VillagerState.Dead
            || CurrentState == VillagerState.Attack || CurrentState == VillagerState.CrawlAttack
            || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion && CurrentMovementMode != EnemyMovementMode.FullyStopped)
        {
            return;
        }

        if (nativeSawGait)
        {
            ChainsawManAnimationBridge bridge = host.ArchitectureReadChainsawBridge;
            if (bridge != null && bridge.InPlaceIntimidationMiddle)
            {
                return;
            }
        }

        float remaining = host.NativeGaitDistanceToStop;
        int requested = ResolveNativeGaitPace(remaining);
        if (gaitState)
        {
            if (nativeSawGait && state.shortNameHash == HashSawWalkStart
                || Time.time < hitWalkStartUntil && state.fullPathHash == HashGanadoWalkStart)
            {
                // Animator 在受击通知中接收过渡，下一个更新才会报告起步状态。
                nativeGaitEdge = "Ganado_1000";
                if (requested == 0 && Time.time < hitWalkStartUntil && CurrentState == VillagerState.Chase
                    && host.Player != null && host.PlayerDistance > CombatModule.StandingApproachRange)
                {
                    // 寻路重新给出步速前，保留已进入的起步。碰撞与实际位移仍由运动执行器决定。
                    return;
                }
            }
            if (nativeGaitEdge == null || !TryGetStandingGait(nativeGaitEdge, out var edge))
            {
                ReturnToNativeCycle(0, 0);
                return;
            }
            if (edge.toPace == 0 && requested > 0 && remaining > edge.travel + 0.55f)
            {
                StartNativeGaitEdge("Ganado_1000");
                return;
            }
            if (edge.toPace > 0 && requested == 0)
            {
                if (edge.fromPace == 0 && state.normalizedTime * state.length < 0.25f)
                {
                    ReturnToNativeCycle(0, 0);
                }
                else
                {
                    StartNativeGaitEdge(SelectNativeGaitStop(edge.toPace, edge.entryPhase, remaining));
                }
                return;
            }
            if (state.normalizedTime * state.length >= edge.exitFrame / SourceGaitFrameRate)
            {
                ReturnToNativeCycle(edge.toPace, edge.cycleFrame);
            }
            return;
        }

        float phase = Mathf.Repeat(state.normalizedTime, 1f);
        if (nativeGaitPace > 0)
        {
            string stop = SelectNativeGaitStop(nativeGaitPace, phase, remaining);
            if (requested == 0 || TryGetStandingGait(stop, out var brake) && remaining <= brake.travel + 0.05f)
            {
                StartNativeGaitEdge(stop);
                return;
            }
        }
        if (requested == nativeGaitPace)
        {
            return;
        }
        if (nativeGaitPace == 0)
        {
            if (requested == 2)
            {
                StartNativeGaitEdge("Ganado_2000");
            }
            else if (remaining > 0.04f)
            {
                StartNativeGaitEdge("Ganado_1000");
            }
        }
        else if (nativeGaitPace < requested)
        {
            StartNativeGaitEdge(SelectNativeGaitEdge("Ganado_0350", "Ganado_0351", phase, remaining - 2.1f));
        }
        else
        {
            StartNativeGaitEdge(SelectNativeGaitEdge("Ganado_0352", "Ganado_0353", phase, remaining));
        }
    }

    private int ResolveNativeGaitPace(float remaining)
    {
        if (CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion || !standingLocomotionVisible
            || requestedStandingMoveParameter <= 0.02f)
        {
            return 0;
        }

        float runRange = 6.6f;
        if (nativeGaitPace == 2)
        {
            runRange = 4.3f;
        }
        if (nativeArmedGait)
        {
            runRange += 0.9f;
        }
        if (CurrentState == VillagerState.Chase && requestedStandingMoveParameter >= EnemyContext.StandingWalkMoveParameter
            && remaining >= runRange)
        {
            return 2;
        }
        return 1;
    }

    private string SelectNativeGaitStop(int pace, float phase, float remaining)
    {
        if (pace == 2)
        {
            if (nativeSawGait)
            {
                return "Ganado_2002";
            }
            return SelectNativeGaitEdge("Ganado_2002", "Ganado_2003", phase, remaining + 0.18f);
        }
        return SelectNativeGaitEdge("Ganado_1002", "Ganado_1003", phase, remaining + 0.18f);
    }

    private string SelectNativeGaitEdge(string first, string second, float phase, float available)
    {
        TryGetStandingGait(first, out var firstEdge);
        TryGetStandingGait(second, out var secondEdge);
        if (Cost(firstEdge) <= Cost(secondEdge))
        {
            return first;
        }
        return second;

        float Cost(ChapterOneRootActor.EnemyGait edge)
        {
            float phaseError = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, edge.entryPhase * 360f)) / 360f;
            return phaseError + Mathf.Max(0f, edge.travel - available) * 0.7f;
        }
    }

    private bool TryGetStandingGait(string id, out ChapterOneRootActor.EnemyGait edge)
    {
        if (nativeSawGait)
        {
            return ChapterOneRootActor.TryGetEnemyGait(SawGaitStateName(id), out edge);
        }
        if (nativeArmedGait)
        {
            string suffix = id.Substring("Ganado".Length);
            return ChapterOneRootActor.TryGetEnemyGait("GanadoArmed" + suffix, out edge);
        }
        return ChapterOneRootActor.TryGetEnemyGait(id, out edge);
    }

    internal bool BlendSawHitIntoPursuit()
    {
        if (!nativeSawGait || !hasNativeGaitPace || anim == null || !host.UsesNativeStandingStride)
        {
            return false;
        }

        // 起步与受击末尾都是低位持锯。直接交接，避免经过高位待机。
        var state = anim.GetCurrentAnimatorStateInfo(0);
        float remaining = Mathf.Max(0f, (1f - state.normalizedTime) * state.length);
        StartHitWalk(Mathf.Min(.22f, remaining), 0f);
        return true;
    }

    private void StartHitWalk(float visualBlend, float recoverySeconds)
    {
        hitWalkStartUntil = Time.time + recoverySeconds + host.staggerRecoveryMovementDelay
            + host.staggerRecoveryAccelerationDuration;
        StartNativeGaitEdge("Ganado_1000", visualBlend);
    }

    private void StartNativeGaitEdge(string edge, float blendSeconds = NativeGaitBlendSeconds)
    {
        nativeGaitEdge = edge;
        string state = edge;
        if (nativeSawGait)
        {
            state = SawGaitStateName(edge);
        }
        anim.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + state), blendSeconds, 0);
    }

    private static string SawGaitStateName(string id) => "Salvador" + id.Substring("Ganado".Length);

    private void ReturnToNativeCycle(int pace, int sourceFrame)
    {
        nativeGaitEdge = null;
        SetNativeGaitPace(pace);
        anim.CrossFadeInFixedTime(HashNativeLocomotion, NativeGaitBlendSeconds, 0, sourceFrame / SourceGaitFrameRate);
    }

    private void SetNativeGaitPace(int pace)
    {
        if (nativeGaitPace != pace)
        {
            nativeGaitPace = pace;
            anim.SetFloat(HashNativeGaitPace, pace);
        }
    }
}
