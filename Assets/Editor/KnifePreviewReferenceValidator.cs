using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>从已经验收的网页读取独立参考矩阵，不重新生成或覆盖网页。</summary>
public static class KnifePreviewReferenceValidator
{
    /// <summary>引用目录。编译期常量，保持既有值与消费者映射。</summary>
    public const string ReferenceDirectory = "Library/KnifeApprovedReference";
    /// <summary>是否运行时捕获运行中。</summary>
    public static bool RuntimeCaptureRunning { get; private set; }

    /// <summary>BeginRuntimeCapture 的业务处理入口；本方法直接依赖 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.FindFirstObjectByType&lt;KnifeCombatModule&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.FindFirstObjectByType&lt;KnifeCombatModule&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string BeginRuntimeCapture()
    {
        if (!EditorApplication.isPlaying || RuntimeCaptureRunning)
        {
            return "当前不能开始播放核验。";
        }

        // 模块。
KnifeCombatModule module = UnityEngine.Object.FindFirstObjectByType<KnifeCombatModule>();
        module.StartCoroutine(CaptureRuntime(module));
        return "已开始正式角色姿态与网页逐帧核验。";
    }

    /// <summary>CaptureRuntime 的业务处理入口；本方法直接依赖 Buffer.BlockCopy(Array, int, Array, int, int)、Component.GetComponent&lt;LeonInputBridge&gt;()、Dictionary&lt;string, float[]&gt;.Dictionary()、Dictionary&lt;string, JObject&gt;.Dictionary()、File.ReadAllBytes(string)、File.ReadAllText(string)、File.WriteAllText(string, string) 等共 34 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.BeginRuntimeCapture()；下游（含内部回调体） Buffer.BlockCopy(Array, int, Array, int, int)、Component.GetComponent&lt;LeonInputBridge&gt;()、Dictionary&lt;string, float[]&gt;.Dictionary()、Dictionary&lt;string, JObject&gt;.Dictionary()、File.ReadAllBytes(string)、File.ReadAllText(string)、File.WriteAllText(string, string) 等共 34 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="module">模块。</param>
    private static IEnumerator CaptureRuntime(KnifeCombatModule module)
    {
        RuntimeCaptureRunning = true;
        // 输入数据。
LeonInputBridge input = module.GetComponent<LeonInputBridge>();
        // 是否前一次输入。
bool previousInput = input.enabled;
        // 前一次帧速率。
int previousFrameRate = Application.targetFrameRate;
        // 记录集合。
List<object> records = new List<object>();
        // 来源预览。
LeonKnifeAnimationPreview.PreviewCharacter sourcePreview = null;
        try
        {
            input.enabled = false;
            module.player.SetMoveInput(Vector3.zero);
            module.Interrupt(false);
            Application.targetFrameRate = 60;
            // 元数据。
JObject metadata = JObject.Parse(File.ReadAllText(ReferenceDirectory + "/reference.json"));
            // 绑定集合。
BoneBinding[] bindings = BindReference(module.gameObject, metadata);
            sourcePreview = new LeonKnifeAnimationPreview.PreviewCharacter();
            sourcePreview.bakePreviewMesh = false;
            // 来源绑定集合。
BoneBinding[] sourceBindings = BindReference(sourcePreview.Model, metadata);
            // 动作集合。
Dictionary<string, JObject> actions = new Dictionary<string, JObject>();
            // 当前键值映射表。
Dictionary<string, float[]> data = new Dictionary<string, float[]>();
            // action（动作）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 File.ReadAllBytes(string) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
foreach (JObject action in metadata["actions"])
            {
                if ((int)action["mode"] != 0)
                {
                    continue;
                }

                // 名称。
string name = (string)action["name"];
                actions[name] = action;
                // 字节集合。
byte[] bytes = File.ReadAllBytes(ReferenceDirectory + "/" + (string)action["file"]);
                // 数值集合。
float[] values = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                data[name] = values;
            }

            yield return new WaitForSeconds(.3f);
            // 场景用例。
for (int scenario = 0; scenario < 3; scenario++)
            {
                module.Interrupt(false);
                yield return new WaitForSeconds(.3f);
                if (scenario == 2)
                {
                    module.CommitGuard(true, Time.timeAsDouble);
                    yield return new WaitForSeconds(.5f);
                }

                // 起始。
double start = Time.timeAsDouble;
                module.CommitAttack(start);
                module.CommitAttackReleased(start + .0001);
                // 是否缓存了待执行的。
bool queued = false;
                // 是否已捕获。
bool captured = false;
                while (Time.timeAsDouble - start < 2.1)
                {
                    yield return new WaitForEndOfFrame();
                    if (scenario > 0 && !queued && Time.timeAsDouble - start > .15)
                    {
                        module.CommitAttack(Time.timeAsDouble);
                        module.CommitAttackReleased(Time.timeAsDouble + .0001);
                        queued = true;
                    }

                    // 动画片段。
KnifeActionConfiguration clip = module.configuration.Find(module.CurrentAction);
                    if (clip == null || clip.sourceClip == null || module.CurrentActionTime < .1)
                    {
                        continue;
                    }

                    // 名称。
string name = clip.sourceClip.name;
                    // 动作。
JObject action = actions[name];
                    // 时间。
float time = clip.sourceStart + (float)module.CurrentActionTime;
                    // 数量。
int count = (int)action["frames"];
                    // 帧。
float frame = Mathf.Clamp(time / (float)action["duration"] * (count - 1), 0, count - 1);
                    // 首个。
int first = Mathf.FloorToInt(frame);
                    // 第二。
int second = Mathf.Min(first + 1, count - 1);
                    // 比例。
float fraction = frame - first;
                    // 错误。
float error = 0;
                    // 来源错误。
float sourceError = 0;
                    // 最差骨骼。
string worstBone = "";
                    sourcePreview.Sample(clip.sourceClip, time, 0, time);
                    // 当前元素索引。
for (int index = 0; index < bindings.Length; index++)
                    {
                        // 首个矩阵。
Matrix4x4 firstMatrix = ReadMatrix(data[name], (first * bindings.Length + index) * 16);
                        // 第二矩阵。
Matrix4x4 secondMatrix = ReadMatrix(data[name], (second * bindings.Length + index) * 16);
                        // 混合后的。
Matrix4x4 blended = new Matrix4x4();
                        // 元素。
for (int element = 0; element < 16; element++)
                        {
                            blended[element] = Mathf.Lerp(firstMatrix[element], secondMatrix[element], fraction);
                        }

                        // 预期。
Matrix4x4 expected = blended * bindings[index].bindPose.inverse;
                        // 实际。
Vector3 actual = module.transform.InverseTransformPoint(bindings[index].bone.position);
                        sourceError = Mathf.Max(sourceError, Vector3.Distance(sourceBindings[index].bone.position, actual));
                        // 距离。
float distance = Vector3.Distance(expected.GetColumn(3), actual);
                        if (distance > error)
                        {
                            error = distance;
                            worstBone = bindings[index].bone.name;
                        }
                    }

                    records.Add(new { scenario, action = name, time, maximumPositionError = error, maximumSourcePoseError = sourceError, worstBone });
                    if (!captured && module.CurrentActionTime > .28 && module.IsAttack)
                    {
                        CaptureViews(module.transform, "实机姿态" + scenario);
                        captured = true;
                    }
                }
            }
        }
        finally
        {
            if (sourcePreview != null)
            {
                sourcePreview.Dispose();
            }

            File.WriteAllText("战斗日志/小刀重构/正式角色与网页逐帧核对.json", Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
            module.Interrupt(false);
            input.enabled = previousInput;
            Application.targetFrameRate = previousFrameRate;
            RuntimeCaptureRunning = false;
        }
    }

    /// <summary>CaptureViews 的业务处理入口；本方法直接依赖 Camera.Render()、File.WriteAllBytes(string, byte[])、GameObject.AddComponent&lt;Camera&gt;()、GameObject.GameObject(string)、Object.DestroyImmediate(Object)、Rect.Rect(float, float, float, float)、RenderTexture.GetTemporary(int, int, int) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)；下游（含内部回调体） Camera.Render()、File.WriteAllBytes(string, byte[])、GameObject.AddComponent&lt;Camera&gt;()、GameObject.GameObject(string)、Object.DestroyImmediate(Object)、Rect.Rect(float, float, float, float)、RenderTexture.GetTemporary(int, int, int) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">玩家。</param>
    /// <param name="prefix">前缀。</param>
    private static void CaptureViews(Transform player, string prefix)
    {
        // 偏移量集合。
Vector3[] offsets =
        {
            new Vector3(.1f, 1.35f, 3.2f),
            new Vector3(3.2f, 1.35f, .1f),
            new Vector3(.4f, 1.55f, -2.4f)
        };
        // 摄像机对象。
GameObject cameraObject = new GameObject("姿态核验临时相机");
        // 摄像机。
Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 40;
        // 目标渲染纹理。
RenderTexture target = RenderTexture.GetTemporary(700, 850, 24);
        // 前一次。
RenderTexture previous = RenderTexture.active;
        // 图像。
Texture2D image = new Texture2D(700, 850, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            // 视图。
for (int view = 0; view < offsets.Length; view++)
            {
                camera.transform.position = player.TransformPoint(offsets[view]);
                camera.transform.LookAt(player.TransformPoint(new Vector3(0, 1.05f, 0)));
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 700, 850), 0, 0);
                image.Apply();
                File.WriteAllBytes("战斗日志/小刀重构/" + prefix + "视角" + view + ".png", image.EncodeToPNG());
            }
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    /// <summary>编辑器配置与校验模块 BoneBinding；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    public sealed class BoneBinding
    {
        /// <summary>骨骼。</summary>
        public Transform bone;
        /// <summary>绑定姿态。</summary>
        public Matrix4x4 bindPose;
    }

    /// <summary>DecodeFloats 的业务处理入口；本方法直接依赖 Buffer.BlockCopy(Array, int, Array, int, int)、Convert.FromBase64String(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.BindReference(GameObject, JObject)；下游（含内部回调体） Buffer.BlockCopy(Array, int, Array, int, int)、Convert.FromBase64String(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="encoded">编码后的。</param>
    public static float[] DecodeFloats(string encoded)
    {
        // 字节集合。
byte[] bytes = Convert.FromBase64String(encoded);
        // 数值集合。
float[] values = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    /// <summary>BindReference 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;KnifePreviewReferenceValidator.BoneBinding&gt;.Any&lt;KnifePreviewReferenceValidator.BoneBinding&gt;(Func&lt;KnifePreviewReferenceValidator.BoneBinding, bool&gt;)、IEnumerable&lt;SkinnedMeshRenderer&gt;.First&lt;SkinnedMeshRenderer&gt;(Func&lt;SkinnedMeshRenderer, bool&gt;)、IEnumerable&lt;Transform&gt;.First&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、KnifePreviewReferenceValidator.BoneBinding.BoneBinding() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;KnifePreviewReferenceValidator.BoneBinding&gt;.Any&lt;KnifePreviewReferenceValidator.BoneBinding&gt;(Func&lt;KnifePreviewReferenceValidator.BoneBinding, bool&gt;)、IEnumerable&lt;SkinnedMeshRenderer&gt;.First&lt;SkinnedMeshRenderer&gt;(Func&lt;SkinnedMeshRenderer, bool&gt;)、IEnumerable&lt;Transform&gt;.First&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、KnifePreviewReferenceValidator.BoneBinding.BoneBinding() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">模型。</param>
    /// <param name="metadata">元数据。</param>
    public static BoneBinding[] BindReference(GameObject model, JObject metadata)
    {
        // 绑定集合。
BoneBinding[] bindings = new BoneBinding[(int)metadata["boneCount"]];
        // meshData（网格数据）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 KnifePreviewReferenceValidator.DecodeFloats(string) 的参数、KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
foreach (JObject meshData in metadata["meshes"])
        {
            // 网格名称。
string meshName = (string)meshData["name"];
            // 关节集合。
float[] joints = DecodeFloats((string)meshData["joints"]);
            // 权重集合。
float[] weights = DecodeFloats((string)meshData["weights"]);
            if ((bool)meshData["knife"])
            {
                // 找到的。
Transform found = model.GetComponentsInChildren<Transform>(true).First( /* 中文含义：当前变换节点。 */item => item.name == meshName);
                bindings[(int)joints[0]] = new BoneBinding
                {
                    bone = found,
                    bindPose = Matrix4x4.identity
                };
                continue;
            }

            // 渲染器。
SkinnedMeshRenderer renderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First( /* 中文含义：当前蒙皮网格渲染器。 */item => item.name == meshName);
            // 骨骼权重集合。
BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            // 网格骨骼集合。
Transform[] meshBones = renderer.bones;
            // 绑定姿态集合。
Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;
            if (boneWeights.Length * 4 != joints.Length)
            {
                throw new InvalidOperationException("网页与角色网格顶点数不一致：" + meshName);
            }

            // 顶点。
for (int vertex = 0; vertex < boneWeights.Length; vertex++)
            {
                // 权重。
BoneWeight weight = boneWeights[vertex];
                // 索引集合。
int[] indices =
                {
                    weight.boneIndex0,
                    weight.boneIndex1,
                    weight.boneIndex2,
                    weight.boneIndex3
                };
                // 槽位。
for (int slot = 0; slot < 4; slot++)
                {
                    if (weights[vertex * 4 + slot] <= 0)
                    {
                        continue;
                    }

                    // 全局索引。
int globalIndex = (int)joints[vertex * 4 + slot];
                    // 局部索引。
int localIndex = indices[slot];
                    if (bindings[globalIndex] != null && bindings[globalIndex].bone != meshBones[localIndex])
                    {
                        throw new InvalidOperationException("网页骨骼编号映射冲突：" + meshName);
                    }

                    if (bindings[globalIndex] == null)
                    {
                        bindings[globalIndex] = new BoneBinding
                        {
                            bone = meshBones[localIndex],
                            bindPose = bindPoses[localIndex]
                        };
                    }
                }
            }
        }

        if (bindings.Any( /* 中文含义：当前骨骼绑定。 */item => item == null))
        {
            throw new InvalidOperationException("网页存在未映射骨骼。");
        }

        return bindings;
    }

    /// <summary>ReadMatrix 的业务处理入口；本方法直接依赖 Matrix4x4.Matrix4x4()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)；下游（含内部回调体） Matrix4x4.Matrix4x4()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="values">数值集合。</param>
    /// <param name="offset">偏移量。</param>
    public static Matrix4x4 ReadMatrix(float[] values, int offset)
    {
        // 矩阵。
Matrix4x4 matrix = new Matrix4x4();
        // 元素。
for (int element = 0; element < 16; element++)
        {
            matrix[element] = values[offset + element];
        }

        return matrix;
    }

    /// <summary>CompareSourcePreview 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、Buffer.BlockCopy(Array, int, Array, int, int)、Directory.CreateDirectory(string)、File.ReadAllBytes(string)、File.ReadAllText(string) 等共 27 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、Buffer.BlockCopy(Array, int, Array, int, int)、Directory.CreateDirectory(string)、File.ReadAllBytes(string)、File.ReadAllText(string) 等共 27 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="useRuntimeSampler">是否使用运行时采样器。</param>
    public static string CompareSourcePreview(bool useRuntimeSampler = false)
    {
        // 元数据。
JObject metadata = JObject.Parse(File.ReadAllText(ReferenceDirectory + "/reference.json"));
        // 结果集合。
List<object> results = new List<object>();
        // preview（预览）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var preview = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            preview.bakePreviewMesh = false;
            // 绑定集合。
BoneBinding[] bindings = BindReference(preview.Model, metadata);
            // 采样器。
KnifePoseSampler sampler = null;
            // 手部。
Transform hand = preview.AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand);
            // 小刀。
Transform knife = hand.GetChild(hand.childCount - 1);
            // 小刀位置。
Vector3 knifePosition = knife.localPosition;
            // 小刀旋转。
Quaternion knifeRotation = knife.localRotation;
            if (useRuntimeSampler)
            {
                // 配置。
KnifeCombatConfiguration configuration = AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(KnifeCombatBuilder.configurationPath);
                sampler = new KnifePoseSampler(configuration, preview.AnimatorValue);
            }

            try
            {
                // action（动作）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 File.ReadAllBytes(string) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
foreach (JObject action in metadata["actions"])
                {
                    // 名称。
string name = (string)action["name"];
                    // 动画片段。
AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/左臂整体重构/" + name + ".anim");
                    if (clip == null)
                    {
                        continue;
                    }

                    // 模式。
int mode = (int)action["mode"];
                    // 帧集合。
int frames = (int)action["frames"];
                    // 持续时间。
float duration = (float)action["duration"];
                    // 字节集合。
byte[] bytes = File.ReadAllBytes(ReferenceDirectory + "/" + (string)action["file"]);
                    // 矩阵集合。
float[] matrices = new float[bytes.Length / 4];
                    Buffer.BlockCopy(bytes, 0, matrices, 0, bytes.Length);
                    // 最大位置错误。
float maximumPositionError = 0;
                    // 最大旋转错误。
float maximumRotationError = 0;
                    // 最差骨骼。
string worstBone = "";
                    // 帧。
for (int frame = 0; frame < frames; frame++)
                    {
                        // 时间。
float time = duration * frame / (frames - 1);
                        // 局部时间。
float localTime = time;
                        if (name == "低位待机" || name == "防守保持")
                        {
                            localTime = Mathf.Repeat(time, clip.length);
                        }

                        if (sampler == null)
                        {
                            preview.Sample(clip, localTime, mode, time);
                        }
                        else
                        {
                            sampler.Sample(clip, localTime, mode, time, 1f, name == "斜上外拨弹反");
                            sampler.Apply(0f);
                            LeonKnifeGrip.ApplyKnifeVisual(knife, knifePosition, knifeRotation, clip);
                        }

                        // 当前元素索引。
for (int index = 0; index < bindings.Length; index++)
                        {
                            // 绑定。
BoneBinding binding = bindings[index];
                            // 预期。
Matrix4x4 expected = ReadMatrix(matrices, (frame * bindings.Length + index) * 16) * binding.bindPose.inverse;
                            // 错误。
float error = Vector3.Distance(expected.GetColumn(3), binding.bone.position);
                            if (error > maximumPositionError)
                            {
                                maximumPositionError = error;
                                worstBone = binding.bone.name;
                            }

                            maximumRotationError = Mathf.Max(maximumRotationError, Quaternion.Angle(expected.rotation, binding.bone.rotation));
                        }
                    }

                    results.Add(new { action = name, mode, maximumPositionError, maximumRotationError, worstBone });
                }
            }
            finally
            {
                if (sampler != null)
                {
                    sampler.Dispose();
                }
            }
        }

        // 报告。
string report = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("战斗日志/小刀重构");
        // 文件。
string file = "网页与制作源核对.json";
        if (useRuntimeSampler)
        {
            file = "网页与实机求值器核对.json";
        }

        File.WriteAllText("战斗日志/小刀重构/" + file, report);
        return "已核对网页与当前制作源：" + results.Count + "组，结果已保存。";
    }
}
