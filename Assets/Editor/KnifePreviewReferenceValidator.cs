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
    /// <summary>中文含义：引用目录。编译期常量，保持既有值与消费者映射。使用位置：作为 File.ReadAllBytes(string) 的参数、作为 File.ReadAllText(string) 的参数。</summary>
    public const string ReferenceDirectory = "Library/KnifeApprovedReference";
    /// <summary>中文含义：是否运行时捕获运行中。使用位置：KnifePreviewReferenceValidator.BeginRuntimeCapture()、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。</summary>
    public static bool RuntimeCaptureRunning { get; private set; }

    /// <summary>BeginRuntimeCapture 的业务处理入口；本方法直接依赖 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.FindFirstObjectByType&lt;KnifeCombatModule&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.FindFirstObjectByType&lt;KnifeCombatModule&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string BeginRuntimeCapture()
    {
        if (!EditorApplication.isPlaying || RuntimeCaptureRunning)
        {
            return "当前不能开始播放核验。";
        }

        // module（模块）：保存 Object.FindFirstObjectByType<KnifeCombatModule>() 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule) 的参数、KnifePreviewReferenceValidator.BeginRuntimeCapture()。
KnifeCombatModule module = UnityEngine.Object.FindFirstObjectByType<KnifeCombatModule>();
        module.StartCoroutine(CaptureRuntime(module));
        return "已开始正式角色姿态与网页逐帧核验。";
    }

    /// <summary>CaptureRuntime 的业务处理入口；本方法直接依赖 Buffer.BlockCopy(Array, int, Array, int, int)、Component.GetComponent&lt;LeonInputBridge&gt;()、Dictionary&lt;string, float[]&gt;.Dictionary()、Dictionary&lt;string, JObject&gt;.Dictionary()、File.ReadAllBytes(string)、File.ReadAllText(string)、File.WriteAllText(string, string) 等共 34 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.BeginRuntimeCapture()；下游（含内部回调体） Buffer.BlockCopy(Array, int, Array, int, int)、Component.GetComponent&lt;LeonInputBridge&gt;()、Dictionary&lt;string, float[]&gt;.Dictionary()、Dictionary&lt;string, JObject&gt;.Dictionary()、File.ReadAllBytes(string)、File.ReadAllText(string)、File.WriteAllText(string, string) 等共 34 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="module">中文含义：模块。使用位置：作为 KnifeCombatConfiguration.Find(KnifeAction) 的参数、作为 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的参数、作为 KnifePreviewReferenceValidator.CaptureViews(Transform, string) 的参数 等共 4 处。</param>
    private static IEnumerator CaptureRuntime(KnifeCombatModule module)
    {
        RuntimeCaptureRunning = true;
        // input（输入数据）：保存 Component.GetComponent<LeonInputBridge>() 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
LeonInputBridge input = module.GetComponent<LeonInputBridge>();
        // previousInput（是否前一次输入）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
bool previousInput = input.enabled;
        // previousFrameRate（前一次帧速率）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
int previousFrameRate = Application.targetFrameRate;
        // records（记录集合）：使用位置：作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
List<object> records = new List<object>();
        // sourcePreview（来源预览）：使用位置：作为 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
LeonKnifeAnimationPreview.PreviewCharacter sourcePreview = null;
        try
        {
            input.enabled = false;
            module.player.SetMoveInput(Vector3.zero);
            module.Interrupt(false);
            Application.targetFrameRate = 60;
            // metadata（元数据）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
JObject metadata = JObject.Parse(File.ReadAllText(ReferenceDirectory + "/reference.json"));
            // bindings（绑定集合）：保存 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
BoneBinding[] bindings = BindReference(module.gameObject, metadata);
            sourcePreview = new LeonKnifeAnimationPreview.PreviewCharacter();
            sourcePreview.bakePreviewMesh = false;
            // sourceBindings（来源绑定集合）：保存 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
BoneBinding[] sourceBindings = BindReference(sourcePreview.Model, metadata);
            // actions（动作集合）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
Dictionary<string, JObject> actions = new Dictionary<string, JObject>();
            // data（当前键值映射表）：使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
Dictionary<string, float[]> data = new Dictionary<string, float[]>();
            // action（动作）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 File.ReadAllBytes(string) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
foreach (JObject action in metadata["actions"])
            {
                if ((int)action["mode"] != 0)
                {
                    continue;
                }

                // name（名称）：使用位置：作为 Dictionary<string, float[]>.this[string] 的参数、作为 Dictionary<string, JObject>.this[string] 的参数。
string name = (string)action["name"];
                actions[name] = action;
                // bytes（字节集合）：保存 File.ReadAllBytes(string) 的返回结果，供当前步骤使用。使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
byte[] bytes = File.ReadAllBytes(ReferenceDirectory + "/" + (string)action["file"]);
                // values（数值集合）：使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
float[] values = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                data[name] = values;
            }

            yield return new WaitForSeconds(.3f);
            // scenario（场景用例）：使用位置：作为 KnifePreviewReferenceValidator.CaptureViews(Transform, string) 的参数、作为 List<object>.Add(object) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
for (int scenario = 0; scenario < 3; scenario++)
            {
                module.Interrupt(false);
                yield return new WaitForSeconds(.3f);
                if (scenario == 2)
                {
                    module.CommitGuard(true, Time.timeAsDouble);
                    yield return new WaitForSeconds(.5f);
                }

                // start（起始）：使用位置：作为 KnifeCombatModule.CommitAttack(double) 的参数、作为 KnifeCombatModule.CommitAttackReleased(double) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
double start = Time.timeAsDouble;
                module.CommitAttack(start);
                module.CommitAttackReleased(start + .0001);
                // queued（是否缓存了待执行的）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
bool queued = false;
                // captured（是否已捕获）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
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

                    // clip（动画片段）：保存 KnifeCombatConfiguration.Find(KnifeAction) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
KnifeActionConfiguration clip = module.configuration.Find(module.CurrentAction);
                    if (clip == null || clip.sourceClip == null || module.CurrentActionTime < .1)
                    {
                        continue;
                    }

                    // name（名称）：使用位置：作为 Dictionary<string, float[]>.this[string] 的参数、作为 Dictionary<string, JObject>.this[string] 的参数、作为 List<object>.Add(object) 的参数。
string name = clip.sourceClip.name;
                    // action（动作）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
JObject action = actions[name];
                    // time（时间）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 List<object>.Add(object) 的参数、作为 Mathf.Clamp(float, float, float) 的参数。
float time = clip.sourceStart + (float)module.CurrentActionTime;
                    // count（数量）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Min(int, int) 的参数。
int count = (int)action["frames"];
                    // frame（帧）：保存 Mathf.Clamp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.FloorToInt(float) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
float frame = Mathf.Clamp(time / (float)action["duration"] * (count - 1), 0, count - 1);
                    // first（首个）：保存 Mathf.FloorToInt(float) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、作为 Mathf.Min(int, int) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
int first = Mathf.FloorToInt(frame);
                    // second（第二）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数。
int second = Mathf.Min(first + 1, count - 1);
                    // fraction（比例）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float fraction = frame - first;
                    // error（错误）：使用位置：作为 List<object>.Add(object) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
float error = 0;
                    // sourceError（来源错误）：使用位置：作为 List<object>.Add(object) 的参数、作为 Mathf.Max(float, float) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
float sourceError = 0;
                    // worstBone（最差骨骼）：使用位置：作为 List<object>.Add(object) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
string worstBone = "";
                    sourcePreview.Sample(clip.sourceClip, time, 0, time);
                    // index（当前元素索引）：使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
for (int index = 0; index < bindings.Length; index++)
                    {
                        // firstMatrix（首个矩阵）：保存 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
Matrix4x4 firstMatrix = ReadMatrix(data[name], (first * bindings.Length + index) * 16);
                        // secondMatrix（第二矩阵）：保存 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
Matrix4x4 secondMatrix = ReadMatrix(data[name], (second * bindings.Length + index) * 16);
                        // blended（混合后的）：使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
Matrix4x4 blended = new Matrix4x4();
                        // element（元素）：使用位置：作为 Matrix4x4.this[int] 的参数、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
for (int element = 0; element < 16; element++)
                        {
                            blended[element] = Mathf.Lerp(firstMatrix[element], secondMatrix[element], fraction);
                        }

                        // expected（预期）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
Matrix4x4 expected = blended * bindings[index].bindPose.inverse;
                        // actual（实际）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
Vector3 actual = module.transform.InverseTransformPoint(bindings[index].bone.position);
                        sourceError = Mathf.Max(sourceError, Vector3.Distance(sourceBindings[index].bone.position, actual));
                        // distance（距离）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)。
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
    /// <param name="player">中文含义：玩家。使用位置：作为 Transform.LookAt(Vector3) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。</param>
    /// <param name="prefix">中文含义：前缀。使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数。</param>
    private static void CaptureViews(Transform player, string prefix)
    {
        // offsets（偏移量集合）：使用位置：作为 Transform.TransformPoint(Vector3) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
Vector3[] offsets =
        {
            new Vector3(.1f, 1.35f, 3.2f),
            new Vector3(3.2f, 1.35f, .1f),
            new Vector3(.4f, 1.55f, -2.4f)
        };
        // cameraObject（摄像机对象）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
GameObject cameraObject = new GameObject("姿态核验临时相机");
        // camera（摄像机）：保存 GameObject.AddComponent<Camera>() 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 40;
        // target（目标渲染纹理）：保存 RenderTexture.GetTemporary(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
RenderTexture target = RenderTexture.GetTemporary(700, 850, 24);
        // previous（前一次）：使用位置：KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
RenderTexture previous = RenderTexture.active;
        // image（图像）：使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
Texture2D image = new Texture2D(700, 850, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            // view（视图）：使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、KnifePreviewReferenceValidator.CaptureViews(Transform, string)。
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
        /// <summary>中文含义：骨骼。使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数 等共 6 处。</summary>
        public Transform bone;
        /// <summary>中文含义：绑定姿态。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。</summary>
        public Matrix4x4 bindPose;
    }

    /// <summary>DecodeFloats 的业务处理入口；本方法直接依赖 Buffer.BlockCopy(Array, int, Array, int, int)、Convert.FromBase64String(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.BindReference(GameObject, JObject)；下游（含内部回调体） Buffer.BlockCopy(Array, int, Array, int, int)、Convert.FromBase64String(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="encoded">中文含义：编码后的。使用位置：作为 Convert.FromBase64String(string) 的参数。</param>
    public static float[] DecodeFloats(string encoded)
    {
        // bytes（字节集合）：保存 Convert.FromBase64String(string) 的返回结果，供当前步骤使用。使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、KnifePreviewReferenceValidator.DecodeFloats(string)。
byte[] bytes = Convert.FromBase64String(encoded);
        // values（数值集合）：使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、KnifePreviewReferenceValidator.DecodeFloats(string)。
float[] values = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    /// <summary>BindReference 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;KnifePreviewReferenceValidator.BoneBinding&gt;.Any&lt;KnifePreviewReferenceValidator.BoneBinding&gt;(Func&lt;KnifePreviewReferenceValidator.BoneBinding, bool&gt;)、IEnumerable&lt;SkinnedMeshRenderer&gt;.First&lt;SkinnedMeshRenderer&gt;(Func&lt;SkinnedMeshRenderer, bool&gt;)、IEnumerable&lt;Transform&gt;.First&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、KnifePreviewReferenceValidator.BoneBinding.BoneBinding() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;KnifePreviewReferenceValidator.BoneBinding&gt;.Any&lt;KnifePreviewReferenceValidator.BoneBinding&gt;(Func&lt;KnifePreviewReferenceValidator.BoneBinding, bool&gt;)、IEnumerable&lt;SkinnedMeshRenderer&gt;.First&lt;SkinnedMeshRenderer&gt;(Func&lt;SkinnedMeshRenderer, bool&gt;)、IEnumerable&lt;Transform&gt;.First&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、KnifePreviewReferenceValidator.BoneBinding.BoneBinding() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。</param>
    /// <param name="metadata">中文含义：元数据。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。</param>
    public static BoneBinding[] BindReference(GameObject model, JObject metadata)
    {
        // bindings（绑定集合）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
BoneBinding[] bindings = new BoneBinding[(int)metadata["boneCount"]];
        // meshData（网格数据）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 KnifePreviewReferenceValidator.DecodeFloats(string) 的参数、KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
foreach (JObject meshData in metadata["meshes"])
        {
            // meshName（网格名称）：使用位置：作为 IEnumerable<SkinnedMeshRenderer>.First<SkinnedMeshRenderer>(Func<SkinnedMeshRenderer, bool>) 的参数、作为 IEnumerable<Transform>.First<Transform>(Func<Transform, bool>) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。
string meshName = (string)meshData["name"];
            // joints（关节集合）：保存 KnifePreviewReferenceValidator.DecodeFloats(string) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
float[] joints = DecodeFloats((string)meshData["joints"]);
            // weights（权重集合）：保存 KnifePreviewReferenceValidator.DecodeFloats(string) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
float[] weights = DecodeFloats((string)meshData["weights"]);
            if ((bool)meshData["knife"])
            {
                // found（找到的）：保存 IEnumerable<Transform>.First<Transform>(Func<Transform, bool>) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
Transform found = model.GetComponentsInChildren<Transform>(true).First( /* 中文含义：当前变换节点。 */item => item.name == meshName);
                bindings[(int)joints[0]] = new BoneBinding
                {
                    bone = found,
                    bindPose = Matrix4x4.identity
                };
                continue;
            }

            // renderer（渲染器）：保存 IEnumerable<SkinnedMeshRenderer>.First<SkinnedMeshRenderer>(Func<SkinnedMeshRenderer, bool>) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
SkinnedMeshRenderer renderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First( /* 中文含义：当前蒙皮网格渲染器。 */item => item.name == meshName);
            // boneWeights（骨骼权重集合）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            // meshBones（网格骨骼集合）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
Transform[] meshBones = renderer.bones;
            // bindPoses（绑定姿态集合）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;
            if (boneWeights.Length * 4 != joints.Length)
            {
                throw new InvalidOperationException("网页与角色网格顶点数不一致：" + meshName);
            }

            // vertex（顶点）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
for (int vertex = 0; vertex < boneWeights.Length; vertex++)
            {
                // weight（权重）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
BoneWeight weight = boneWeights[vertex];
                // indices（索引集合）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
int[] indices =
                {
                    weight.boneIndex0,
                    weight.boneIndex1,
                    weight.boneIndex2,
                    weight.boneIndex3
                };
                // slot（槽位）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
for (int slot = 0; slot < 4; slot++)
                {
                    if (weights[vertex * 4 + slot] <= 0)
                    {
                        continue;
                    }

                    // globalIndex（全局索引）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
int globalIndex = (int)joints[vertex * 4 + slot];
                    // localIndex（局部索引）：使用位置：KnifePreviewReferenceValidator.BindReference(GameObject, JObject)。
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
    /// <param name="values">中文含义：数值集合。使用位置：KnifePreviewReferenceValidator.ReadMatrix(float[], int)。</param>
    /// <param name="offset">中文含义：偏移量。使用位置：KnifePreviewReferenceValidator.ReadMatrix(float[], int)。</param>
    public static Matrix4x4 ReadMatrix(float[] values, int offset)
    {
        // matrix（矩阵）：使用位置：KnifePreviewReferenceValidator.ReadMatrix(float[], int)。
Matrix4x4 matrix = new Matrix4x4();
        // element（元素）：使用位置：作为 Matrix4x4.this[int] 的参数、KnifePreviewReferenceValidator.ReadMatrix(float[], int)。
for (int element = 0; element < 16; element++)
        {
            matrix[element] = values[offset + element];
        }

        return matrix;
    }

    /// <summary>CompareSourcePreview 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、Buffer.BlockCopy(Array, int, Array, int, int)、Directory.CreateDirectory(string)、File.ReadAllBytes(string)、File.ReadAllText(string) 等共 27 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、Buffer.BlockCopy(Array, int, Array, int, int)、Directory.CreateDirectory(string)、File.ReadAllBytes(string)、File.ReadAllText(string) 等共 27 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="useRuntimeSampler">中文含义：是否使用运行时采样器。使用位置：KnifePreviewReferenceValidator.CompareSourcePreview(bool)。</param>
    public static string CompareSourcePreview(bool useRuntimeSampler = false)
    {
        // metadata（元数据）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
JObject metadata = JObject.Parse(File.ReadAllText(ReferenceDirectory + "/reference.json"));
        // results（结果集合）：使用位置：作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
List<object> results = new List<object>();
        // preview（预览）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var preview = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            preview.bakePreviewMesh = false;
            // bindings（绑定集合）：保存 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的返回结果，供当前步骤使用。使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
BoneBinding[] bindings = BindReference(preview.Model, metadata);
            // sampler（采样器）：使用位置：KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
KnifePoseSampler sampler = null;
            // hand（手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.GetChild(int) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
Transform hand = preview.AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand);
            // knife（小刀）：保存 Transform.GetChild(int) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
Transform knife = hand.GetChild(hand.childCount - 1);
            // knifePosition（小刀位置）：使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数。
Vector3 knifePosition = knife.localPosition;
            // knifeRotation（小刀旋转）：使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数。
Quaternion knifeRotation = knife.localRotation;
            if (useRuntimeSampler)
            {
                // configuration（配置）：保存 AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 的参数。
KnifeCombatConfiguration configuration = AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(KnifeCombatBuilder.configurationPath);
                sampler = new KnifePoseSampler(configuration, preview.AnimatorValue);
            }

            try
            {
                // action（动作）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 File.ReadAllBytes(string) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
foreach (JObject action in metadata["actions"])
                {
                    // name（名称）：使用位置：作为 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的参数、作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 List<object>.Add(object) 的参数 等共 4 处。
string name = (string)action["name"];
                    // clip（动画片段）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数 等共 5 处。
AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/左臂整体重构/" + name + ".anim");
                    if (clip == null)
                    {
                        continue;
                    }

                    // mode（模式）：使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 List<object>.Add(object) 的参数。
int mode = (int)action["mode"];
                    // frames（帧集合）：使用位置：KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
int frames = (int)action["frames"];
                    // duration（持续时间）：使用位置：KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
float duration = (float)action["duration"];
                    // bytes（字节集合）：保存 File.ReadAllBytes(string) 的返回结果，供当前步骤使用。使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
byte[] bytes = File.ReadAllBytes(ReferenceDirectory + "/" + (string)action["file"]);
                    // matrices（矩阵集合）：使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数。
float[] matrices = new float[bytes.Length / 4];
                    Buffer.BlockCopy(bytes, 0, matrices, 0, bytes.Length);
                    // maximumPositionError（最大位置错误）：使用位置：作为 List<object>.Add(object) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
float maximumPositionError = 0;
                    // maximumRotationError（最大旋转错误）：使用位置：作为 List<object>.Add(object) 的参数、作为 Mathf.Max(float, float) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
float maximumRotationError = 0;
                    // worstBone（最差骨骼）：使用位置：作为 List<object>.Add(object) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
string worstBone = "";
                    // frame（帧）：使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
for (int frame = 0; frame < frames; frame++)
                    {
                        // time（时间）：使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 Mathf.Repeat(float, float) 的参数 等共 4 处。
float time = duration * frame / (frames - 1);
                        // localTime（局部时间）：使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
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

                        // index（当前元素索引）：使用位置：作为 KnifePreviewReferenceValidator.ReadMatrix(float[], int) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
for (int index = 0; index < bindings.Length; index++)
                        {
                            // binding（绑定）：使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
BoneBinding binding = bindings[index];
                            // expected（预期）：使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数。
Matrix4x4 expected = ReadMatrix(matrices, (frame * bindings.Length + index) * 16) * binding.bindPose.inverse;
                            // error（错误）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
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

        // report（报告）：保存 JsonConvert.SerializeObject(object?, Formatting) 的返回结果，供当前步骤使用。使用位置：作为 File.WriteAllText(string, string) 的参数。
string report = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("战斗日志/小刀重构");
        // file（文件）：使用位置：作为 File.WriteAllText(string, string) 的参数、KnifePreviewReferenceValidator.CompareSourcePreview(bool)。
string file = "网页与制作源核对.json";
        if (useRuntimeSampler)
        {
            file = "网页与实机求值器核对.json";
        }

        File.WriteAllText("战斗日志/小刀重构/" + file, report);
        return "已核对网页与当前制作源：" + results.Count + "组，结果已保存。";
    }
}
