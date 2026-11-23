#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static int RefreshLegacySlotReactions()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式烘焙旧状态片段。");
            }
            var source = EditorSceneManager.NewPreviewScene();
            try
            {
                var saw = FindLegacyStateClip("Assets/Animations/Enemy/ChainsawMan/ChainsawMan.controller", "Taunt_Stomp");
                var stunned = FindLegacyStateClip("Assets/Animations/Enemy/Enemy.controller", "Electricuted");
                Retarget("Salvador", saw, "SalvadorLegacy_Taunt");
                Retarget("Ganado", stunned, "GanadoLegacy_Electricuted");
                Retarget("GanadoMale", stunned, "GanadoLegacy_Electricuted");
                return 3;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(source);
            }

            void Retarget(string role, AnimationClip clip, string id)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, source);
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                Avatar temporary = null;
                try
                {
                    temporary = BuildLegBiteAvatar(instance);
                    if (temporary == null || !temporary.isHuman || !temporary.isValid)
                    {
                        throw new InvalidOperationException(role + " 无法采样旧片段 " + clip.name);
                    }
                    animator.avatar = temporary;
                    var rig = instance.transform.Find(role + "_Skeleton");
                    var baked = BakeLegacyHumanoid(clip, animator, rig, id, false, sharedStage: false, inPlace: true);
                    var root = ChapterOnePairMotion.ReadTrack(baked);
                    Vector3 delta = root.positions[^1] - root.positions[0];
                    string bankPath = Generated + "/Clips/" + role + "_Clips.asset";
                    var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
                    var previous = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>()
                        .SingleOrDefault(item => item.name == id);
                    Audit.Add(FormattableString.Invariant(
                        $"{role}\t{id}\t{AssetDatabase.GetAssetPath(clip)}\t{baked.length:F4}\t{baked.frameRate}\t{AnimationUtility.GetCurveBindings(baked).Length}\t0\t{delta.x:F3},{delta.z:F3}\t\t旧动作过渡，已重绑"));
                    if (previous == null)
                    {
                        AssetDatabase.AddObjectToAsset(baked, bank);
                    }
                    else
                    {
                        EditorUtility.CopySerialized(baked, previous);
                        EditorUtility.SetDirty(previous);
                        Object.DestroyImmediate(baked);
                    }
                    EditorUtility.SetDirty(bank);
                    AssetDatabase.SaveAssetIfDirty(bank);
                    WriteLegacyStates(role, new[] { id });
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                    if (temporary != null)
                    {
                        Object.DestroyImmediate(temporary);
                    }
                }
            }
        }

        private static AnimationClip FindLegacyStateClip(string path, string state)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            var clip = controller.layers[0].stateMachine.states.Single(child => child.state.name == state).state.motion as AnimationClip;
            if (clip == null || !clip.humanMotion)
            {
                throw new InvalidOperationException(state + " 缺少有效的人形源片段。");
            }
            return clip;
        }
    }
}
#endif
