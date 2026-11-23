using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemyAnimationAdapter
{
    [Serializable]
    internal sealed class BodyTechniqueMotions
    {
        public AnimationClip head;
        public AnimationClip leftKneel;
        public AnimationClip rightKneel;
        public AnimationClip perfectParry;
        public Vector2 standingWindow = new Vector2(.12f, .90f);
        public Vector2 kneelFrames = new Vector2(32f, 135f);
    }

    [SerializeField] private bool nativeBodyTechnique;
    [SerializeField] private BodyTechniqueMotions unarmedBodyTechnique;
    [SerializeField] private BodyTechniqueMotions armedBodyTechnique;
    private readonly List<AnimatorClipInfo> bodyTechniqueClips = new List<AnimatorClipInfo>(2);
    private static readonly int BodyHeadState = Animator.StringToHash("Hit_Head");
    private static readonly int BodyLeftKneelState = Animator.StringToHash("Hit_NativeKneelLeft");
    private static readonly int BodyRightKneelState = Animator.StringToHash("Hit_NativeKneelRight");
    private static readonly int BodyPerfectParryState = Animator.StringToHash("Stagger_Heavy_Front");

    internal bool HasNativeBodyTechnique => nativeBodyTechnique;
    internal bool StandingHitBlendCompleted => anim != null && !anim.IsInTransition(0)
        && (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == HashNativeLocomotion
            || anim.GetCurrentAnimatorStateInfo(0).fullPathHash == HashGanadoWalkStart);

    private static readonly int DisarmHitState = Animator.StringToHash("Base Layer.HitReact.WP5801_Disarm");

    internal static float StandingHitVisualBlend(AnimatorStateInfo state, float requested)
    {
        if (state.fullPathHash != DisarmHitState)
        {
            return requested;
        }

        // 脱手完成通知到原片尾只余十帧，按实际进入混合时的剩余时间收尾。
        float timeLeft = (1f - state.normalizedTime) * state.length;
        return Mathf.Clamp(timeLeft, 0f, requested);
    }

    internal bool TryBlendStandingHitToLocomotion(float blendSeconds, bool resumePursuit)
    {
        if (!nativeBodyTechnique || anim == null || !anim.HasState(0, HashNativeLocomotion)
            || anim.GetCurrentAnimatorStateInfo(0).fullPathHash == HashNativeLocomotion)
        {
            return false;
        }

        // 从受击动作当前姿势直接混合回站立层，避免子状态机先切走再进入步态。
        nativeGaitEdge = null;
        SetNativeGaitPace(0);
        float visualBlend = StandingHitVisualBlend(anim.GetCurrentAnimatorStateInfo(0), blendSeconds);
        if (resumePursuit && hasNativeGaitPace && host.UsesNativeStandingStride
            && anim.HasState(0, HashGanadoWalkStart))
        {
            // 追击直接接当前控制器的起步，缴械后由徒手覆盖器选片。
            StartHitWalk(visualBlend, blendSeconds);
        }
        else
        {
            anim.CrossFadeInFixedTime(HashNativeLocomotion, visualBlend, 0, 0f);
        }
        return true;
    }

    internal bool TryPresentNativeBodyReaction(NativeBodyReaction reaction)
    {
        if (!nativeBodyTechnique || anim == null || reaction == NativeBodyReaction.None)
        {
            return false;
        }
        string state;
        switch (reaction)
        {
            case NativeBodyReaction.HeadStagger:
                state = "Base Layer.HitReact.Hit_Head";
                break;
            case NativeBodyReaction.LeftKneel:
                state = "Base Layer.HitReact.Hit_NativeKneelLeft";
                break;
            case NativeBodyReaction.RightKneel:
                state = "Base Layer.HitReact.Hit_NativeKneelRight";
                break;
            case NativeBodyReaction.PerfectParry:
                state = "Base Layer.Stagger.Stagger_Heavy_Front";
                break;
            default:
                return false;
        }
        int hash = Animator.StringToHash(state);
        if (!anim.HasState(0, hash))
        {
            return false;
        }
        // 明确选择已绑定的原生受击状态，避免子状态机入口触发器未消费时保持移动姿态。
        anim.ResetTrigger(Animator.StringToHash("HitTrigger"));
        anim.CrossFadeInFixedTime(hash, .05f, 0, 0f);
        return true;
    }

#if UNITY_EDITOR
    internal void ConfigureNativeBodyTechnique(BodyTechniqueMotions unarmed, BodyTechniqueMotions armed)
    {
        nativeBodyTechnique = true;
        unarmedBodyTechnique = unarmed;
        armedBodyTechnique = armed;
    }
#endif

    internal bool IsNativeBodyTechniquePoseReady(NativeBodyReaction reaction)
    {
        Animator animator = anim;
        if (animator == null)
        {
            return false;
        }

        BodyTechniqueMotions motions = unarmedBodyTechnique;
        if (CombatModule.UsesArmedNativeBodyTechnique)
        {
            motions = armedBodyTechnique;
        }

        if (motions == null)
        {
            return false;
        }

        AnimationClip expected;
        int expectedState;
        switch (reaction)
        {
            case NativeBodyReaction.HeadStagger:
                expected = motions.head;
                expectedState = BodyHeadState;
                break;
            case NativeBodyReaction.LeftKneel:
                expected = motions.leftKneel;
                expectedState = BodyLeftKneelState;
                break;
            case NativeBodyReaction.RightKneel:
                expected = motions.rightKneel;
                expectedState = BodyRightKneelState;
                break;
            case NativeBodyReaction.PerfectParry:
                expected = motions.perfectParry;
                expectedState = BodyPerfectParryState;
                break;
            default:
                return false;
        }

        if (expected == null)
        {
            return false;
        }
        Vector2 window = motions.standingWindow;
        bool stepInFrames = reaction is NativeBodyReaction.LeftKneel or NativeBodyReaction.RightKneel;
        if (stepInFrames)
        {
            window = motions.kneelFrames;
        }
        else if (reaction == NativeBodyReaction.PerfectParry || reaction == NativeBodyReaction.HeadStagger)
        {
            // 站立大硬直在片段开始承接后的第二个源帧开放，不能把长片段的百分比当作输入延迟。
            float sourceFrames = Mathf.Max(1f, expected.length * expected.frameRate);
            window.x = Mathf.Min(window.x, 2f / sourceFrames);
        }

        bool transitioning = animator.IsInTransition(0);
        if (transitioning && HasReadyBodyTechniqueClip(animator, expected, expectedState, window, stepInFrames, true))
        {
            return true;
        }
        return HasReadyBodyTechniqueClip(animator, expected, expectedState, window, stepInFrames, false);
    }

    private bool HasReadyBodyTechniqueClip(Animator animator, AnimationClip expected, int expectedState,
        Vector2 window, bool stepInFrames, bool readNext)
    {
        AnimatorStateInfo state;
        if (readNext)
        {
            state = animator.GetNextAnimatorStateInfo(0);
        }
        else
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
        }
        if (state.shortNameHash != expectedState)
        {
            return false;
        }

        float position = state.normalizedTime;
        if (stepInFrames)
        {
            position *= expected.length * expected.frameRate;
        }
        if (position < window.x || position > window.y)
        {
            return false;
        }

        bodyTechniqueClips.Clear();
        if (readNext)
        {
            animator.GetNextAnimatorClipInfo(0, bodyTechniqueClips);
        }
        else
        {
            animator.GetCurrentAnimatorClipInfo(0, bodyTechniqueClips);
        }
        foreach (AnimatorClipInfo clip in bodyTechniqueClips)
        {
            if (clip.clip == expected && clip.weight > .05f)
            {
                return true;
            }
        }
        return false;
    }
}
