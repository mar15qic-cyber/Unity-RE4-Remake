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
        public static string RefreshDoorSlotController()
        {
            const string path = Generated + "/NativeDoor.overrideController";
            const string legacyPath = "Assets/Animations/Door/DoorAnimatorController.controller";
            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(legacyPath);
            var states = source.layers[0].stateMachine.states.ToDictionary(child => child.state.name, child => child.state.motion);
            var opening = (AnimationClip)states["Opening"];
            var closing = (AnimationClip)states["Closing"];
            var openMotion = BakeDoorProgress(opening, "sm80_501_0000_wood_door_open_F", false);
            var closeMotion = BakeDoorProgress(closing, "sm80_501_0002_wood_door_close_F", true);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(source);
                AssetDatabase.CreateAsset(controller, path);
            }
            controller[opening] = Store(openMotion);
            controller[closing] = Store(closeMotion);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return path;

            AnimationClip Store(AnimationClip clip)
            {
                var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .SingleOrDefault(item => item.name == clip.name);
                if (saved == null)
                {
                    AssetDatabase.AddObjectToAsset(clip, controller);
                    return clip;
                }
                EditorUtility.CopySerialized(clip, saved);
                EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(clip);
                return saved;
            }
        }

        private static AnimationClip BakeDoorProgress(AnimationClip legacy, string id, bool closing)
        {
            var motion = Find("sm80_501_DoorMotion", id);
            const string hinge = "sm80_501_Skeleton/root/_00/_01";
            var yaw = AnimationUtility.GetEditorCurve(motion,
                EditorCurveBinding.FloatCurve(hinge, typeof(Transform), "m_LocalRotation.y"));
            var scalar = AnimationUtility.GetEditorCurve(motion,
                EditorCurveBinding.FloatCurve(hinge, typeof(Transform), "m_LocalRotation.w"));
            int length = Mathf.RoundToInt(motion.length * motion.frameRate);
            var angles = new float[length + 1];
            for (int frame = 0; frame <= length; frame++)
            {
                float time = frame / motion.frameRate;
                angles[frame] = 2f * Mathf.Atan2(yaw.Evaluate(time), scalar.Evaluate(time));
            }
            int begin = Array.FindIndex(angles, value => Mathf.Abs(value - angles[0]) > 0.0005f) - 1;
            int end = Array.FindLastIndex(angles, value => Mathf.Abs(value - angles[^1]) > 0.0005f) + 1;
            float closedAngle = angles[0];
            float openAngle = angles[^1];
            if (closing)
            {
                closedAngle = angles[^1];
                openAngle = angles[0];
            }
            float travel = openAngle - closedAngle;
            if (begin < 0 || end <= begin || travel < 0.1f)
            {
                throw new InvalidOperationException(id + " 门叶缺少可采样的开启行程。");
            }

            var keys = new Keyframe[end - begin + 1];
            for (int index = 0; index < keys.Length; index++)
            {
                float time = index * legacy.length / (keys.Length - 1);
                float progress = Mathf.Clamp01((angles[begin + index] - closedAngle) / travel);
                keys[index] = new Keyframe(time, progress);
            }
            keys[0].value = 0f;
            keys[^1].value = 1f;
            if (closing)
            {
                keys[0].value = 1f;
                keys[^1].value = 0f;
            }
            var curve = new AnimationCurve(keys);
            for (int index = 0; index < keys.Length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            }
            var result = Object.Instantiate(legacy);
            result.name = "Native_" + legacy.name;
            var binding = EditorCurveBinding.FloatCurve("", typeof(DoorController), "doorOpenProgress");
            AnimationUtility.SetEditorCurve(result, binding, curve);
            return result;
        }
    }
}
#endif
