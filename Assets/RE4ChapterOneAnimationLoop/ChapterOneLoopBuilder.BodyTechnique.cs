#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string LeftKneelState = "Hit_NativeKneelLeft";
        private const string RightKneelState = "Hit_NativeKneelRight";

        public static int RefreshMainLeonMelee()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请退出播放后更新正式体术。");
            }

            ImportAdditionalArt("LeonFatalKicks");
            Clips.Remove("LeonFatalKicks");
            string bankPath = Generated + "/Clips/Leon_Clips.asset";
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            BakeMelee("LeonFatal_0900");
            BakeMelee("LeonFatal_0906");
            BakeMelee("LeonCrate_0500");
            ApplyMainLeonMelee(graph, clips);
            AssetDatabase.SaveAssetIfDirty(library);
            AssetDatabase.SaveAssetIfDirty(graph);
            return 6;

            void BakeMelee(string name)
            {
                var source = Find("LeonFatalKicks", name);
                if (source == null)
                {
                    throw new InvalidOperationException("缺少原版体术片段 " + name);
                }
                clips[name] = Bake(source, rig, "Leon", "Leon", false, Array.Empty<int>(), library, clips, out _, name);
            }
        }

        private static void ApplyMainLeonMelee(AnimatorController graph, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var machine = graph.layers[LeonAnimatorDriver.LayerFullBodyAction].stateMachine;
            Install(FullBodyActionType.QTESpinningHookKick, clips["LeonFatal_0900"], 108f / 137f,
                MeleeActionKinds.EnemyKickContactTime(FullBodyActionType.QTESpinningHookKick));
            Install(FullBodyActionType.QTERightCutKick, clips["LeonFatal_0906"], 108f / 135f,
                MeleeActionKinds.EnemyKickContactTime(FullBodyActionType.QTERightCutKick));
            Install(FullBodyActionType.WoodenBoxKick, clips["LeonCrate_0500"], 74f / 140f, 26f / 140f);
            Install(FullBodyActionType.WoodenBoxStomp, clips["LeonInteract_0530"], 0.76f, 27f / 135f);
            Install(FullBodyActionType.WoodenBoxElbow, clips["LeonInteract_0800"], 0.80f, 30f / 120f);
            Install(FullBodyActionType.WoodenBoxPistolElbow, clips["LeonInteract_0800_wp4000m"], 0.75f, 30f / 137f);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);

            void Install(FullBodyActionType action, AnimationClip clip, float completion, float contact)
            {
                string name = action.ToString();
                if (MeleeActionKinds.IsWoodenBox(action))
                {
                    name = "Native" + name;
                }
                var state = machine.states.Select(child => child.state).SingleOrDefault(candidate => candidate.name == name);
                if (state == null)
                {
                    state = machine.AddState(name);
                    state.writeDefaultValues = false;
                }
                var complete = state.behaviours.OfType<FullBodyActionSMB>().FirstOrDefault();
                if (complete == null)
                {
                    complete = state.AddStateMachineBehaviour<FullBodyActionSMB>();
                }
                complete.actionType = action;
                complete.completeNormalizedTime = completion;
                var hit = state.behaviours.OfType<MeleeDamageSMB>().FirstOrDefault();
                if (hit == null)
                {
                    hit = state.AddStateMachineBehaviour<MeleeDamageSMB>();
                }
                hit.actionType = action;
                hit.hitNormalizedTime = contact;
                var exit = state.transitions.FirstOrDefault(transition => transition.destinationState == machine.defaultState);
                if (exit == null)
                {
                    exit = state.AddTransition(machine.defaultState);
                    exit.AddCondition(AnimatorConditionMode.NotEqual, (int)action, "FullBodyAction");
                }
                exit.hasExitTime = false;
                exit.hasFixedDuration = true;
                exit.duration = 0f;
                state.motion = clip;
                EditorUtility.SetDirty(complete);
                EditorUtility.SetDirty(hit);
                EditorUtility.SetDirty(exit);
                EditorUtility.SetDirty(state);
            }
        }

        public static string RefreshMainGanadoBodyTechniques()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式绑定正式村民的体术硬直。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainGanadoController);
            var male = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(MainGanadoMaleOverride);
            var armed = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(MainArmedOverride);
            var root = graph.layers[0].stateMachine;
            var hits = root.stateMachines.Single(item => item.stateMachine.name == "HitReact").stateMachine;
            var head = (AnimationClip)FindArmedStateClipOwner(root, "Hit_Head").motion;
            var parry = (AnimationClip)FindArmedStateClipOwner(root, "Stagger_Heavy_Front").motion;
            string femaleBank = Generated + "/MainGanadoMotions.asset";
            string maleBank = Generated + "/MainGanadoMaleMotions.asset";
            var femaleLeft = BakeBodyHit("Ganado", "0420", femaleBank);
            var femaleRight = BakeBodyHit("Ganado", "0520", femaleBank);
            var maleLeft = BakeBodyHit("GanadoMale", "0420", maleBank);
            var maleRight = BakeBodyHit("GanadoMale", "0520", maleBank);
            var femaleLeftFlinch = BakeBodyHit("Ganado", "0400", femaleBank, 82);
            var femaleRightFlinch = BakeBodyHit("Ganado", "0500", femaleBank, 88);

            BindBodyHitState(hits, LeftKneelState, EnemyCombatModule.NativeLeftKneelHitDirection, femaleLeft);
            BindBodyHitState(hits, RightKneelState, EnemyCombatModule.NativeRightKneelHitDirection, femaleRight);
            BindBodyHitState(hits, "Hit_NativeLegFlinchLeft", EnemyCombatModule.NativeLeftLegFlinchDirection, femaleLeftFlinch);
            BindBodyHitState(hits, "Hit_NativeLegFlinchRight", EnemyCombatModule.NativeRightLegFlinchDirection, femaleRightFlinch);
            EditorUtility.SetDirty(hits);
            male[femaleLeft] = maleLeft;
            male[femaleRight] = maleRight;
            male[femaleLeftFlinch] = BakeBodyHit("GanadoMale", "0400", maleBank, 82);
            male[femaleRightFlinch] = BakeBodyHit("GanadoMale", "0500", maleBank, 88);
            EditorUtility.SetDirty(male);

            var femaleMotions = Motions(head, femaleLeft, femaleRight, parry);
            var maleMotions = Motions(EffectiveClip(male, head), maleLeft, maleRight, EffectiveClip(male, parry));
            ConfigurePrefab(Generated + "/MainGanadoFemale.prefab", femaleMotions);
            ConfigurePrefab(Generated + "/MainGanadoMale.prefab", maleMotions);

            if (armed != null && AssetDatabase.LoadAssetAtPath<GameObject>(MainGanadoArmedPrefab) != null)
            {
                var gait = (BlendTree)FindArmedStateClipOwner(root, "Locomotion").motion;
                var idle = EffectiveClip(armed, (AnimationClip)gait.children[0].motion);
                var armedLeft = BakeBodyHit("GanadoMale", "0420", MainArmedMotionBank, grip: idle);
                var armedRight = BakeBodyHit("GanadoMale", "0520", MainArmedMotionBank, grip: idle);
                armed[femaleLeft] = armedLeft;
                armed[femaleRight] = armedRight;
                EditorUtility.SetDirty(armed);
                ConfigurePrefab(MainGanadoArmedPrefab, maleMotions,
                    Motions(EffectiveClip(armed, head), armedLeft, armedRight, EffectiveClip(armed, parry)));
            }

            AssetDatabase.SaveAssets();
            return "已绑定正式村民头部失衡、左右跪地和完美弹反的原生体术窗口。";
        }

        private static EnemyAnimationAdapter.BodyTechniqueMotions Motions(AnimationClip head, AnimationClip left,
            AnimationClip right, AnimationClip parry)
        {
            return new EnemyAnimationAdapter.BodyTechniqueMotions
            {
                head = head, leftKneel = left, rightKneel = right, perfectParry = parry
            };
        }

        private static AnimationClip EffectiveClip(AnimatorOverrideController controller, AnimationClip source)
        {
            AnimationClip clip = controller[source];
            if (clip != null)
            {
                return clip;
            }

            return source;
        }

        // 完成事件保持原帧，尾部额外保留一个采样间隔，覆盖整段站立恢复混合。
        private static int StandingReactionEndFrame(AnimationClip source, int completeFrame)
        {
            int blendFrames = Mathf.CeilToInt(EnemyCombatModule.StandingHitRecoveryBlend * source.frameRate);
            return Mathf.Min(Mathf.RoundToInt(source.length * source.frameRate), completeFrame + blendFrames + 1);
        }

        private static AnimationClip BakeBodyHit(string role, string take, string bankPath, int frames = 165,
            AnimationClip grip = null)
        {
            string sourcePath = $"{Generated}/Clips/{role}_Clips.asset";
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Single(clip => clip.name == "Ganado_" + take);
            int completeFrame = frames - 10;
            AnimationClip result = SliceMainGanadoMotion(source, 0, StandingReactionEndFrame(source, completeFrame));
            result.name = $"Main{role}_BodyTechnique_{take}";
            if (grip != null)
            {
                result.name = $"MainGanadoArmed_BodyTechnique_{take}";
                // 在男村民原生跪地姿态上保留持斧的握点和手指弯曲。
                foreach (var binding in AnimationUtility.GetCurveBindings(grip))
                {
                    if (binding.type != typeof(Transform)
                        || (!binding.path.EndsWith("/R_Wep", StringComparison.Ordinal)
                            && !binding.path.Contains("/R_Hand/R_Thumb", StringComparison.Ordinal)
                            && !binding.path.Contains("/R_Hand/R_Index", StringComparison.Ordinal)
                            && !binding.path.Contains("/R_Hand/R_Middle", StringComparison.Ordinal)
                            && !binding.path.Contains("/R_Hand/R_Ring", StringComparison.Ordinal)
                            && !binding.path.Contains("/R_Hand/R_Pinky", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    float value = AnimationUtility.GetEditorCurve(grip, binding).Evaluate(0f);
                    AnimationUtility.SetEditorCurve(result, binding, AnimationCurve.Constant(0f, result.length, value));
                }
                result.EnsureQuaternionContinuity();
            }

            SetMarkers(result, ("OnHitComplete", completeFrame));
            var stored = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == result.name);
            if (stored == null)
            {
                AssetDatabase.AddObjectToAsset(result, bankPath);
                stored = result;
            }
            else
            {
                EditorUtility.CopySerialized(result, stored);
                Object.DestroyImmediate(result);
            }
            EditorUtility.SetDirty(stored);
            return stored;
        }

        private static void BindBodyHitState(AnimatorStateMachine machine, string name, int direction, AnimationClip clip)
        {
            var state = machine.states.FirstOrDefault(item => item.state.name == name).state;
            if (state == null)
            {
                state = machine.AddState(name);
                var exit = state.AddExitTransition(false);
                exit.hasFixedDuration = true;
                exit.duration = .16f;
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "HIT");
            }

            var entry = machine.entryTransitions.FirstOrDefault(item => item.destinationState == state);
            if (entry == null)
            {
                entry = machine.AddEntryTransition(state);
            }

            entry.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, threshold = direction, parameter = "HitDir" }
            };
            EditorUtility.SetDirty(entry);
            state.motion = clip;
            EditorUtility.SetDirty(state);
        }

        private static void ConfigurePrefab(string path, EnemyAnimationAdapter.BodyTechniqueMotions unarmed,
            EnemyAnimationAdapter.BodyTechniqueMotions armed = null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponent<EnemyAnimationAdapter>().ConfigureNativeBodyTechnique(unarmed, armed);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"无法保存体术硬直绑定 {path}。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
