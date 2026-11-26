using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public sealed partial class EnemyAnimationAdapter
{
    private PlayableGraph nativeCombatGraph;
    private AnimationClipPlayable nativeCombatPlayable;
    private AnimationPlayableOutput nativeCombatOutput;

    private AnimationClipPlayable grabOutgoing;
    private AnimationMixerPlayable grabMixer;
    private double grabOutgoingStart;
    private const float MissBlendTime = 12f / 60f;

    // 抓取判定结束后仍采样伸手动作的尾部，位移仍由当前战斗轨道独占。
    internal void BlendMissedGrab(AnimationClip recovery)
    {
        if (!nativeCombatGraph.IsValid() || !nativeCombatPlayable.IsValid() || grabMixer.IsValid()) return;
        grabOutgoing = nativeCombatPlayable;
        grabOutgoingStart = grabOutgoing.GetTime();
        nativeCombatPlayable = CreateCombatPlayable(recovery);
        grabMixer = AnimationMixerPlayable.Create(nativeCombatGraph, 2);
        nativeCombatGraph.Connect(grabOutgoing, 0, grabMixer, 0);
        nativeCombatGraph.Connect(nativeCombatPlayable, 0, grabMixer, 1);
        grabMixer.SetInputWeight(0, 1f);
        grabMixer.SetInputWeight(1, 0f);
        nativeCombatOutput.SetSourcePlayable(grabMixer);
    }

    private AnimationClipPlayable CreateCombatPlayable(AnimationClip clip)
    {
        var playable = AnimationClipPlayable.Create(nativeCombatGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(0d);
        return playable;
    }

    internal void PresentNativeCombat(AnimationClip clip, float time, bool releaseMiss = false, float entryBlendSeconds = 0f)
    {
        if (clip != null && !clip.name.StartsWith("Revived_", System.StringComparison.Ordinal))
        {
            ClearRevivedGait();
        }
        if (anim == null || clip == null)
        {
            return;
        }
        if (!nativeCombatGraph.IsValid() || nativeCombatPlayable.GetAnimationClip() != clip)
        {
            StopNativeCombat();
            nativeCombatGraph = PlayableGraph.Create("村民原版接触与倒地");
            nativeCombatGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            nativeCombatPlayable = CreateCombatPlayable(clip);
            nativeCombatOutput = AnimationPlayableOutput.Create(nativeCombatGraph, "原版村民动作", anim);
            nativeCombatOutput.SetSourcePlayable(nativeCombatPlayable);
            nativeCombatOutput.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            nativeCombatOutput.SetSortingOrder(60);
            nativeCombatGraph.Play();
        }
        nativeCombatPlayable.SetTime(time);
        if (grabMixer.IsValid())
        {
            float weight = Mathf.SmoothStep(0f, 1f, time / MissBlendTime);
            grabOutgoing.SetTime(grabOutgoingStart + time);
            grabMixer.SetInputWeight(0, 1f - weight);
            grabMixer.SetInputWeight(1, weight);
            if (time >= MissBlendTime)
            {
                nativeCombatOutput.SetSourcePlayable(nativeCombatPlayable);
                nativeCombatGraph.Disconnect(grabMixer, 1);
                nativeCombatGraph.DestroySubgraph(grabMixer);
                grabMixer = default;
                grabOutgoing = default;
            }
        }
        float releaseWeight = 1f;
        if (releaseMiss)
        {
            releaseWeight = Mathf.SmoothStep(0f, 1f, (clip.length - time) / MissBlendTime);
        }

        float entryWeight = 1f;
        if (entryBlendSeconds > 0f && time < entryBlendSeconds)
        {
            entryWeight = Mathf.SmoothStep(0f, 1f, time / entryBlendSeconds);
        }

        float outputWeight = releaseWeight * entryWeight;
        nativeCombatOutput.SetWeight(outputWeight);
    }

    internal void StopNativeCombat()
    {
        if (nativeCombatGraph.IsValid())
        {
            nativeCombatGraph.Destroy();
        }
    }
}
