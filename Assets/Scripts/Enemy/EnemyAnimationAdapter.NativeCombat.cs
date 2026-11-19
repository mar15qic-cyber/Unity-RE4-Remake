using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public sealed partial class EnemyAnimationAdapter
{
    private PlayableGraph nativeCombatGraph;
    private AnimationClipPlayable nativeCombatPlayable;
    private AnimationPlayableOutput nativeCombatOutput;

    internal void PresentNativeCombat(AnimationClip clip, float time)
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
            nativeCombatPlayable = AnimationClipPlayable.Create(nativeCombatGraph, clip);
            nativeCombatPlayable.SetApplyFootIK(false);
            nativeCombatPlayable.SetApplyPlayableIK(false);
            nativeCombatPlayable.SetSpeed(0d);
            nativeCombatOutput = AnimationPlayableOutput.Create(nativeCombatGraph, "原版村民动作", anim);
            nativeCombatOutput.SetSourcePlayable(nativeCombatPlayable);
            nativeCombatOutput.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            nativeCombatOutput.SetSortingOrder(60);
            nativeCombatGraph.Play();
        }
        nativeCombatPlayable.SetTime(time);
        nativeCombatOutput.SetWeight(1f);
    }

    internal void StopNativeCombat()
    {
        if (nativeCombatGraph.IsValid())
        {
            nativeCombatGraph.Destroy();
        }
    }
}
