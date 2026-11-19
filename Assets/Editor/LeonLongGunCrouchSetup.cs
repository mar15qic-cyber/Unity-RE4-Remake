#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

// 保留原版蹲伏的根运动和上下蹲衔接，按武器覆盖对应的持握骨骼。
public static class LeonLongGunCrouchSetup
{
    [MenuItem("工具/玩家/更新长枪蹲姿")]
    public static void Refresh()
    {
        const string generated = "Assets/RE4ChapterOneAnimationLoop/Generated/";
        var graph = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(generated + "MainLeon.controller");
        var machine = graph.layers[0].stateMachine;
        var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
        var prefixes = new [] { "NativeCqbr", "NativeW870" };
        var suffixes = new [] { "CrouchIdle", "CrouchStart", "CrouchLoop", "CrouchStop", "CrouchDown", "CrouchUp", "CrouchMovingDown", "CrouchMovingUpLeft", "CrouchMovingUpRight" };
        int count = 0;
        foreach (string prefix in prefixes)
        {
            var bank = generated + "MainLeonLongGunCrouchMotions.asset";
            string carryName = "NativeW870Carry";
            if (prefix == "NativeCqbr")
            {
                carryName = "NativeCqbrIdle";
            }
            var carry = graph.animationClips.First(clip => clip.name == carryName);
            foreach (string suffix in suffixes)
            {
                var sourceState = states["NativePistol" + suffix];
                var motion = (AnimationClip)sourceState.motion;
                string name = prefix + suffix;
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(bank).OfType<AnimationClip>().FirstOrDefault(item => item.name == name);
                bool created = clip == null;
                if (created)
                {
                    clip = new AnimationClip();
                }
                EditorUtility.CopySerialized(motion, clip);
                clip.name = name;
                var bindings = AnimationUtility.GetCurveBindings(carry).Where(binding => binding.path.Contains("/arm left shoulder") || binding.path.Contains("/arm right shoulder")).ToArray();
                AnimationUtility.SetEditorCurves(clip, bindings, bindings.Select(binding => AnimationCurve.Constant(0f, motion.length, AnimationUtility.GetEditorCurve(carry, binding).Evaluate(0f))).ToArray());
                if (created)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(bank) == null)
                    {
                        AssetDatabase.CreateAsset(clip, bank);
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(clip, bank);
                    }
                }
                if (!states.TryGetValue(name, out var state))
                {
                    state = machine.AddState(name, new Vector3(2700 + count % 3 * 220, 100 + count / 3 * 80));
                }
                state.motion = clip;
                state.writeDefaultValues = sourceState.writeDefaultValues;
                state.speed = sourceState.speed;
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(state);
                count++;
            }
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(bank));
        }
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssetIfDirty(graph);

        Debug.Log("长枪蹲姿已更新：" + count + " 个状态。");
    }
}
#endif
