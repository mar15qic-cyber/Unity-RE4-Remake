#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private static AnimationClip BindMainArmedReactions(GameObject actor, AnimatorStateMachine machine,
            AnimationClip equippedIdle, Dictionary<AnimationClip, AnimationClip> overrides, Func<AnimationClip, AnimationClip> store)
        {
            const string prefix = "GanadoArmedReaction_";
            var reactions = GetMainArmedSourceClips(actor, "GanadoArmedReactions", prefix);
            var legs = GetMainArmedSourceClips(actor, "GanadoArmedStandingLeg", prefix);
            var hits = machine.stateMachines.Single(item => item.stateMachine.name == "HitReact").stateMachine;

            Override("Hit_Head", Make(reactions["Head"], 162, ("OnHitComplete", 150)));
            Override("Hit_RightArm", Make(reactions["ArmR"], 115, ("OnHitComplete", 105)));

            var disarm = SliceMainGanadoMotion(reactions["Disarm"], 0, 122);
            disarm.name = reactions["Disarm"].name + "_Combat";
            foreach (var binding in AnimationUtility.GetCurveBindings(equippedIdle))
            {
                if (binding.type != typeof(Transform) || !binding.path.EndsWith("/R_Wep", StringComparison.Ordinal))
                {
                    continue;
                }
                float equipped = AnimationUtility.GetEditorCurve(equippedIdle, binding).Evaluate(0f);
                AnimationUtility.SetEditorCurve(disarm, binding, AnimationCurve.Constant(0f, disarm.length, equipped));
            }
            disarm.EnsureQuaternionContinuity();
            // 原片第 5 帧是武器音效标记，Unity 在同一帧交接握点与刚体。
            SetMarkers(disarm, ("OnDisarmRelease", 5), ("OnHitComplete", 112));
            disarm = store(disarm);
            AddHitState("WP5801_Disarm", EnemyCombatModule.ArmedDisarmHitDirection, disarm, 0.18f);
            AddHitState("WP5801_LeftLegStanding", EnemyCombatModule.ArmedStandingLeftLegHitDirection,
                StandingLeg("StandingLegL", 72), 0.16f);
            AddHitState("WP5801_RightLegStanding", EnemyCombatModule.ArmedStandingRightLegHitDirection,
                StandingLeg("StandingLegR", 78), 0.16f);
            AddHitState("WP5801_LeftLegFall", EnemyCombatModule.ArmedFallingLeftLegHitDirection,
                Make(reactions["LegL"], 114, ("OnColliderCrawl", 11), ("OnHitComplete", 103)), 0.15f);
            AddHitState("WP5801_RightLegFall", EnemyCombatModule.ArmedFallingRightLegHitDirection,
                Make(reactions["LegR"], 114, ("OnColliderCrawl", 7), ("OnHitComplete", 103)), 0.15f);
            EditorUtility.SetDirty(hits);
            return disarm;

            AnimationClip StandingLeg(string id, int completeFrame)
            {
                var source = legs[id];
                return Make(source, StandingReactionEndFrame(source, completeFrame), ("OnHitComplete", completeFrame));
            }

            AnimationClip Make(AnimationClip source, int end, (string name, int frame) first,
                (string name, int frame) second = default)
            {
                var result = SliceMainGanadoMotion(source, 0, end);
                result.name = source.name + "_Combat";
                if (second.name != null)
                {
                    SetMarkers(result, first, second);
                }
                else
                {
                    SetMarkers(result, first);
                }
                return store(result);
            }

            void Override(string state, AnimationClip motion)
            {
                var slot = hits.states.Single(item => item.state.name == state).state;
                overrides.Add((AnimationClip)slot.motion, motion);
            }

            void AddHitState(string stateName, int direction, AnimationClip motion, float blendSeconds)
            {
                var state = hits.states.FirstOrDefault(item => item.state.name == stateName).state;
                if (state == null)
                {
                    state = hits.AddState(stateName, new Vector3(420f, 100f + (direction - 6) * 75f));
                    var entry = hits.AddEntryTransition(state);
                    entry.AddCondition(AnimatorConditionMode.Equals, direction, "HitDir");
                    var exit = state.AddExitTransition(false);
                    exit.hasFixedDuration = true;
                    exit.duration = blendSeconds;
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "HIT");
                }
                state.motion = motion;
                EditorUtility.SetDirty(state);
            }
        }

        private static void SetMarkers(AnimationClip clip, params (string name, int frame)[] markers)
        {
            AnimationUtility.SetAnimationEvents(clip, markers.Select(marker => new AnimationEvent
            {
                functionName = marker.name,
                time = marker.frame / clip.frameRate
            }).ToArray());
        }
    }
}
#endif
