#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static class ChapterOneGaitCycles
    {
        private const string Generated = "Assets/RE4ChapterOneAnimationLoop/Generated";
        private const string Suffix = "_GaitCycle";

        internal static AnimationClip RefreshLeonStandingIdle()
        {
            var library = new CycleLibrary("Leon", Generated + "/GaitCycles");
            var clip = library.Resolve("LeonMove_0160", true, rebuild: true);
            library.Save();
            return clip;
        }

        public static int Apply()
        {
            string folder = Generated + "/GaitCycles";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Generated, "GaitCycles");

            int linked = 0;
            foreach (string role in new[] { "Leon", "Ganado", "Salvador" })
            {
                var library = new CycleLibrary(role, folder);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/" + role + ".controller");
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var childState in controller.layers[0].stateMachine.states)
                    {
                        if (childState.state.motion is BlendTree tree)
                        {
                            linked += ResolveTree(tree, library);
                        }
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                library.Save();
                AssetDatabase.SaveAssetIfDirty(controller);
            }

            var male = new CycleLibrary("GanadoMale", folder);
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Generated + "/GanadoMale.overrideController");
            var bindings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(bindings);
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    var source = bindings[i].Key;
                    bindings[i] = new KeyValuePair<AnimationClip, AnimationClip>(source,
                        male.Resolve(source.name, source.name.EndsWith(Suffix, StringComparison.Ordinal)));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            overrides.ApplyOverrides(bindings);
            male.Save();
            EditorUtility.SetDirty(overrides);
            AssetDatabase.SaveAssetIfDirty(overrides);
            AssetDatabase.SaveAssets();
            return linked;
        }

        private static int ResolveTree(BlendTree tree, CycleLibrary library)
        {
            int linked = 0;
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion is BlendTree nested)
                {
                    linked += ResolveTree(nested, library);
                }
                else if (children[i].motion is AnimationClip clip)
                {
                    children[i].motion = library.Resolve(clip.name, true);
                    linked++;
                }
            }
            tree.children = children;
            EditorUtility.SetDirty(tree);
            return linked;
        }

        private sealed class CycleLibrary
        {
            private readonly string role;
            private readonly ChapterOneBakedClips asset;
            private readonly Dictionary<string, AnimationClip> sources;
            private readonly Dictionary<string, AnimationClip> cycles;

            public CycleLibrary(string role, string folder)
            {
                this.role = role;
                sources = AssetDatabase.LoadAllAssetsAtPath($"{Generated}/Clips/{role}_Clips.asset")
                    .OfType<AnimationClip>().ToDictionary(c => c.name);
                string path = $"{folder}/{role}.asset";
                asset = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                cycles = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToDictionary(c => c.name);
            }

            public AnimationClip Resolve(string name, bool gait, bool rebuild = false)
            {
                if (name.EndsWith(Suffix, StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - Suffix.Length);
                var source = sources[name];
                if (!gait || !TryRange(role, source, out var frames))
                    return source;
                string cycleName = name + Suffix;
                if (cycles.TryGetValue(cycleName, out var existing) && !rebuild)
                    return existing;

                var clip = BakeCycle(source, cycleName, frames.start, frames.end);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    UnityEngine.Object.DestroyImmediate(clip);
                    EditorUtility.SetDirty(existing);
                    EditorUtility.SetDirty(asset);
                    return existing;
                }
                AssetDatabase.AddObjectToAsset(clip, asset);
                EditorUtility.SetDirty(asset);
                cycles.Add(cycleName, clip);
                return clip;
            }

            public void Save() => AssetDatabase.SaveAssetIfDirty(asset);
        }

        // 帧段来自同侧脚接地相位和循环两端姿势的对比。
        private static bool TryRange(string role, AnimationClip source, out (int start, int end) range)
        {
            range = source.name switch
            {
                "LeonMove_0403" => (311, 374),
                "LeonMove_0520" => (177, 224),
                "LeonMove_0720" => (30, 64),
                "LeonMove_1403" => (51, 115),
                "LeonMove_1433" => (55, 116),
                "LeonMove_1443" => (52, 116),
                "LeonMove_1473" => (58, 122),
                "SG09R_0403" or "W870_0403" or "CQBR_0403" => (54, 123),
                "SG09R_0433" or "W870_0433" or "CQBR_0433" => (118, 188),
                "SG09R_0443" or "W870_0443" or "CQBR_0443" => (121, 191),
                "SG09R_0473" or "W870_0473" or "CQBR_0473" => (1, 71),
                "Knife_0403" => (59, 136),
                "Knife_0413" => (57, 132),
                "Knife_0443" => (71, 150),
                "Knife_0453" => (140, 221),
                "LeonHurt_0403" => (216, 275),
                "LeonHurt_0520" => (94, 141),
                "Ganado_1001" => role == "GanadoMale" ? (233, 316) : (231, 314),
                "Ganado_2001" => role == "GanadoMale" ? (172, 213) : (173, 214),
                "Salvador_1001" => (34, 126),
                "Salvador_2001" => (16, 69),
                "LeonMove_0160" => (0, Mathf.RoundToInt(source.length * source.frameRate)),
                "LeonMove_1160" or "LeonHurt_0160" or "SG09R_0160" or "W870_0160"
                    or "CQBR_0160" or "Knife_0160" or "Ganado_0000" or "Salvador_0000" => (0, 60),
                _ => default
            };
            return range.end > range.start;
        }

        internal static AnimationClip BakeCycle(AnimationClip source, string name, int start, int end)
        {
            float rate = source.frameRate;
            int count = end - start;
            float length = count / rate;
            var result = new AnimationClip { name = name, frameRate = rate };
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
            var output = new AnimationCurve[curves.Length];
            var rotations = new Dictionary<(string path, string property), int>();
            for (int i = 0; i < bindings.Length; i++)
                if (bindings[i].propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    rotations[(bindings[i].path, bindings[i].propertyName)] = i;
            float clipEnd = source.length;

            float Read(AnimationCurve curve, int frame, bool accumulate)
            {
                float time = (start + frame) / rate;
                if (time <= clipEnd + 0.0001f)
                    return curve.Evaluate(Mathf.Min(time, clipEnd));
                return curve.Evaluate(time - clipEnd) +
                    (accumulate ? curve.Evaluate(clipEnd) - curve.Evaluate(0f) : 0f);
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                if (output[i] != null)
                    continue;
                var binding = bindings[i];
                var curve = curves[i];
                string property = binding.propertyName;
                bool root = binding.type == typeof(Animator);
                if (root && (property.StartsWith("RootQ.", StringComparison.Ordinal) || property == "RootT.y"))
                {
                    output[i] = AnimationCurve.Constant(0f, length, property == "RootQ.w" ? 1f : 0f);
                    continue;
                }

                if (property.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                {
                    const string axes = "xyzw";
                    var indices = new int[4];
                    for (int axis = 0; axis < axes.Length; axis++)
                    {
                        if (!rotations.TryGetValue((binding.path, "m_LocalRotation." + axes[axis]), out indices[axis]))
                            indices[axis] = -1;
                    }
                    if (indices.All(k => k >= 0))
                    {
                        Quaternion At(int frame) => new Quaternion(Read(curves[indices[0]], frame, false),
                            Read(curves[indices[1]], frame, false), Read(curves[indices[2]], frame, false),
                            Read(curves[indices[3]], frame, false)).normalized;
                        Quaternion previous = At(0);
                        Quaternion correction = Quaternion.Inverse(At(count)) * previous;
                        var samples = new float[4][];
                        for (int axis = 0; axis < axes.Length; axis++)
                            samples[axis] = new float[count + 1];
                        for (int frame = 0; frame <= count; frame++)
                        {
                            Quaternion pose = At(frame) * Quaternion.Slerp(Quaternion.identity, correction, Seam(frame, count));
                            if (Quaternion.Dot(previous, pose) < 0f)
                                pose = new Quaternion(-pose.x, -pose.y, -pose.z, -pose.w);
                            previous = pose;
                            for (int axis = 0; axis < axes.Length; axis++)
                                samples[axis][frame] = pose[axis];
                        }
                        for (int axis = 0; axis < axes.Length; axis++)
                            output[indices[axis]] = MakeCurve(samples[axis], rate, root);
                        continue;
                    }
                }

                float initial = Read(curve, 0, root);
                float closure = root ? 0f : initial - Read(curve, count, false);
                var values = new float[count + 1];
                for (int frame = 0; frame <= count; frame++)
                    values[frame] = Read(curve, frame, root) - (root ? initial : 0f) + closure * Seam(frame, count);
                output[i] = MakeCurve(values, rate, root);
            }

            AnimationUtility.SetEditorCurves(result, bindings, output);
            result.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = true;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            return result;
        }

        private static float Seam(int frame, int count) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, frame / (float)count));

        private static AnimationCurve MakeCurve(float[] values, float rate, bool root)
        {
            int count = values.Length - 1;
            if (values.All(v => Mathf.Abs(v - values[0]) < 0.000001f))
                return AnimationCurve.Constant(0f, count / rate, values[0]);

            float offset = root ? values[count] - values[0] : 0f;
            float edgeSlope = (values[1] - (values[count - 1] - offset)) * rate * 0.5f;
            var keys = new Keyframe[values.Length];
            for (int i = 0; i <= count; i++)
            {
                float slope = i == 0 || i == count ? edgeSlope : (values[i + 1] - values[i - 1]) * rate * 0.5f;
                keys[i] = new Keyframe(i / rate, values[i], slope, slope);
            }
            return new AnimationCurve(keys);
        }
    }
}
#endif

