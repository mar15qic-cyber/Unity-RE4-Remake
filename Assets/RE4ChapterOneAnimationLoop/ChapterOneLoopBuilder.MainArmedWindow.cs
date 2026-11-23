#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private static AnimationClip BuildMainArmedWindowClip(AnimationClip source, GameObject actor, Transform wrist)
        {
            string[] rows = File.ReadAllLines(Source + "/RigData/WP5801WindowCalibration.tsv");
            int endFrame = rows.Length - 1;
            var clip = SliceMainGanadoMotion(source, 0, endFrame);
            clip.name = source.name + "_Calibrated";
            string wristPath = AnimationUtility.CalculateTransformPath(wrist, actor.transform);
            var bindings = new EditorCurveBinding[5];
            var curves = new AnimationCurve[bindings.Length];
            var values = new float[bindings.Length][];
            const string axes = "xyzw";
            for (int axis = 0; axis < axes.Length; axis++)
            {
                bindings[axis] = EditorCurveBinding.FloatCurve(wristPath, typeof(Transform), "m_LocalRotation." + axes[axis]);
            }
            bindings[4] = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            var authoredHeight = AnimationUtility.GetEditorCurve(clip, bindings[4]);
            for (int axis = 0; axis < values.Length; axis++)
            {
                values[axis] = new float[rows.Length];
            }
            for (int frame = 0; frame < rows.Length; frame++)
            {
                string[] row = rows[frame].Split('	');
                if (row.Length != 6 || int.Parse(row[0], CultureInfo.InvariantCulture) != frame)
                {
                    throw new InvalidDataException($"持械翻窗第 {frame} 帧校准数据无效。");
                }
                for (int axis = 0; axis < values.Length; axis++)
                {
                    values[axis][frame] = float.Parse(row[axis + 1], CultureInfo.InvariantCulture);
                }
                values[4][frame] += authoredHeight.Evaluate(frame / clip.frameRate);
            }
            for (int axis = 0; axis < curves.Length; axis++)
            {
                var keys = new Keyframe[rows.Length];
                var samples = values[axis];
                for (int frame = 0; frame < keys.Length; frame++)
                {
                    float left = (samples[frame] - samples[Mathf.Max(0, frame - 1)]) * clip.frameRate;
                    float right = (samples[Mathf.Min(frame + 1, endFrame)] - samples[frame]) * clip.frameRate;
                    keys[frame] = new Keyframe(frame / clip.frameRate, samples[frame], left, right);
                }
                curves[axis] = new AnimationCurve(keys);
            }
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            clip.EnsureQuaternionContinuity();
            return clip;
        }
    }
}
#endif
