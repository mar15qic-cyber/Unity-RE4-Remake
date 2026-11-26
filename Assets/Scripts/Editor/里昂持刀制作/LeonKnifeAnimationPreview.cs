using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>在隔离预览场景内播放真实里昂和刀具，不驱动正式角色或战斗事件。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀动作预览")]
public sealed class LeonKnifeAnimationPreview : EditorWindow
{
    /// <summary>候选目录。编译期常量，保持既有值与消费者映射。</summary>
    public const string candidateDirectory = "Assets/Animations/里昂持刀候选";
    /// <summary>中文含义：模型路径。编译期常量，保持既有值与消费者映射。</summary>
    public const string modelPath = "Assets/Models/角色/里昂/LeonR4R.fbx";
    /// <summary>来源材质目录。编译期常量，保持既有值与消费者映射。</summary>
    public const string sourceMaterialDirectory = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/";
    /// <summary>动作名称。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] actionName =
    {
        "低位待机",
        "高位防御",
        "拔刀",
        "收刀",
        "右向左主挥砍",
        "左向右回切",
        "主挥砍回收",
        "回切回收",
        "抬刀",
        "放下",
        "普通格挡反应",
        "弹反成功",
        "移动主挥砍",
        "移动回切",
        "单刀完整回收",
        "两刀连续回收",
        "防御完整衔接"
    };
    /// <summary>视图名称。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] viewName =
    {
        "正面",
        "右侧",
        "背面",
        "越肩全身检查",
        "左侧",
        "游戏越肩（水平）",
        "右手握刀近景"
    };
    /// <summary>移动名称。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] movementName =
    {
        "原地",
        "前行",
        "后退",
        "左移",
        "右移",
        "移动起停",
        "原地转向"
    };
    /// <summary>新角色。</summary>
    private PreviewCharacter newCharacter;
    /// <summary>前一次角色。</summary>
    private PreviewCharacter previousCharacter;
    /// <summary>动作。</summary>
    private int action;
    /// <summary>视图。</summary>
    private int view;
    /// <summary>移动。</summary>
    private int movement;
    /// <summary>时间。</summary>
    private float time;
    /// <summary>速度。</summary>
    private float speed = 1f;
    /// <summary>是否播放。</summary>
    private bool play = true;
    /// <summary>是否循环。</summary>
    private bool loop = true;
    /// <summary>是否引用。</summary>
    private bool reference;
    /// <summary>上一次时间。</summary>
    private double lastTime;
    /// <summary>错误。</summary>
    private string error;
    /// <summary>编辑器菜单调用入口 Open；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.GetWindow&lt;LeonKnifeAnimationPreview&gt;()、EditorWindow.Show()、GUIContent.GUIContent(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/打开动作预览")]
    public static void Open()
    {
        // 窗口。
var window = GetWindow<LeonKnifeAnimationPreview>();
        window.titleContent = new GUIContent("里昂持刀动作预览");
        window.minSize = new Vector2(760f, 620f);
        window.Show();
    }

    /// <summary>编辑器菜单调用入口 MenuExportKeyframe；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、LeonKnifeAnimationPreview.ExportCheckImage()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/导出多视角关键帧")]
    public static void MenuExportKeyframe()
    {
        Debug.Log(ExportCheckImage());
    }

    /// <summary>编辑器菜单调用入口 MenuExportKnifeGrip；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/导出握刀近景")]
    public static void MenuExportKnifeGrip()
    {
        Debug.Log(ExportKnifeGripCheckImage());
    }

    /// <summary>OnEnable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Update;
    }

    /// <summary>OnDisable 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.CleanupValue()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonKnifeAnimationPreview.CleanupValue()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        EditorApplication.update -= Update;
        CleanupValue();
    }

    /// <summary>CleanupValue 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.PreviewCharacter.Dispose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.OnDisable()、LeonKnifeAnimationPreview.OnGUI()；下游（含内部回调体） LeonKnifeAnimationPreview.PreviewCharacter.Dispose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupValue()
    {
        if (newCharacter != null)
        {
            newCharacter.Dispose();
        }

        if (previousCharacter != null)
        {
            previousCharacter.Dispose();
        }

        newCharacter = null;
        previousCharacter = null;
    }

    /// <summary>Update 的业务处理入口；本方法直接依赖 EditorWindow.Repaint()、LeonKnifeAnimationPreview.TotalLong(int)、Mathf.Min(float, float)、Mathf.Repeat(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.Repaint()、LeonKnifeAnimationPreview.TotalLong(int)、Mathf.Min(float, float)、Mathf.Repeat(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        // 当前。
double current = EditorApplication.timeSinceStartup;
        if (play && !EditorApplication.isCompiling)
        {
            time += Mathf.Min((float)(current - lastTime), 0.1f) * speed;
            // 长度。
float length = TotalLong(action);
            if (time > length)
            {
                if (loop)
                {
                    time = Mathf.Repeat(time, length);
                }
                else
                {
                    time = length;
                    play = false;
                }
            }

            Repaint();
        }

        lastTime = current;
    }

    /// <summary>OnGUI 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.HelpBox(string, MessageType)、EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[])、EditorGUILayout.Slider(string, float, float, float, params GUILayoutOption[])、EditorGUILayout.ToggleLeft(string, bool, params GUILayoutOption[])、GUI.Label(Rect, string) 等共 25 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.HelpBox(string, MessageType)、EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[])、EditorGUILayout.Slider(string, float, float, float, params GUILayoutOption[])、EditorGUILayout.ToggleLeft(string, bool, params GUILayoutOption[])、GUI.Label(Rect, string) 等共 25 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("候选动作审阅：使用真实角色材质和刀具，不执行伤害。游戏越肩读取小刀相机配置，以水平朝向检查构图，不包含镜头碰撞和战斗反馈。", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        // 新动作。
int newAction = EditorGUILayout.Popup("动作", action, actionName);
        if (newAction != action)
        {
            action = newAction;
            time = 0f;
            if (action == 12 || action == 13)
            {
                movement = 1;
            }
        }

        view = EditorGUILayout.Popup("视角", view, viewName);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        movement = EditorGUILayout.Popup("下身叠加", movement, movementName);
        reference = EditorGUILayout.ToggleLeft("显示原素材对照", reference, GUILayout.Width(160f));
        if (GUILayout.Button("重新读取", GUILayout.Width(90f)))
        {
            CleanupValue();
            error = null;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        // 分支选出的文字“暂停”。
string selectedValue1;
        if (play)
        {
            selectedValue1 = "暂停";
        }
        else
        {
            selectedValue1 = "播放";
        }

        if (GUILayout.Button(selectedValue1, GUILayout.Width(65f)))
        {
            play = !play;
        }

        if (GUILayout.Button("上一帧", GUILayout.Width(70f)))
        {
            play = false;
            time = Mathf.Max(0f, time - 1f / 60f);
        }

        if (GUILayout.Button("下一帧", GUILayout.Width(70f)))
        {
            play = false;
            time = Mathf.Min(TotalLong(action), time + 1f / 60f);
        }

        loop = GUILayout.Toggle(loop, "循环", GUILayout.Width(55f));
        speed = EditorGUILayout.Slider("播放速度", speed, 0.1f, 1.5f);
        EditorGUILayout.EndHorizontal();
        time = EditorGUILayout.Slider("时间（秒）", time, 0f, TotalLong(action));
        if (!string.IsNullOrEmpty(error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        // 区域。
Rect region = GUILayoutUtility.GetRect(100f, 10000f, 200f, 10000f, GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint || EditorApplication.isCompiling)
        {
            return;
        }

        try
        {
            if (newCharacter == null)
            {
                newCharacter = new PreviewCharacter();
            }

            // 动画片段。
AnimationClip clip;
            // 局部时间。
float localTime;
            SelectClip(action, time, out clip, out localTime);
            if (clip == null)
            {
                throw new InvalidOperationException("候选动画尚未生成，请先使用生成候选动作菜单。");
            }

            newCharacter.Sample(clip, localTime, movement, time);
            if (reference)
            {
                if (previousCharacter == null)
                {
                    previousCharacter = new PreviewCharacter();
                }

                // 原始动画片段。
var originalClip = OriginalSourceMaterial(action);
                // 原始时间。
float originalTime = 0f;
                if (originalClip != null)
                {
                    originalTime = Mathf.Clamp01(localTime / clip.length) * originalClip.length;
                }

                previousCharacter.Sample(originalClip, originalTime, movement, time);
                // 左侧。
Rect left = new Rect(region.x, region.y, region.width * 0.5f - 2f, region.height);
                // 右侧。
Rect right = new Rect(region.x + region.width * 0.5f + 2f, region.y, region.width * 0.5f - 2f, region.height);
                newCharacter.Draw(left, view);
                previousCharacter.Draw(right, view);
                GUI.Label(new Rect(left.x + 10, left.y + 10, 240, 25), "候选动作");
                GUI.Label(new Rect(right.x + 10, right.y + 10, 240, 25), "原素材（归一化进度对照）");
            }
            else
            {
                newCharacter.Draw(region, view);
            }

            error = null;
        }
        catch (Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
        {
            error = exception.Message;
            play = false;
        }
    }

    /// <summary>Read 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()、LeonKnifeAnimationPreview.ExportVideoFrame()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 等共 9 处；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    public static AnimationClip Read(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(candidateDirectory + "/" + name + ".anim");
    }

    /// <summary>ReadSourceMaterial 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、LeonKnifeAnimationPreview.OriginalSourceMaterial(int)、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeWebExporter.ExportTo(string, string)；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="relativePairPath">相对配对路径。</param>
    public static AnimationClip ReadSourceMaterial(string relativePairPath)
    {
        // asset（资源）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationPreview.ReadSourceMaterial(string)。
foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sourceMaterialDirectory + relativePairPath + ".fbx"))
        {
            // 动画片段。
var clip = asset as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                return clip;
            }
        }

        return null;
    }

    /// <summary>OriginalSourceMaterial 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.ReadSourceMaterial(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.OnGUI()；下游（含内部回调体） LeonKnifeAnimationPreview.ReadSourceMaterial(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    private static AnimationClip OriginalSourceMaterial(int id)
    {
        if (id == 2)
        {
            return ReadSourceMaterial("Knife_St_OtherAnims/Knife_St_Take_R");
        }

        if (id == 3)
        {
            return ReadSourceMaterial("Knife_St_OtherAnims/Knife_St_Hide_R");
        }

        if (id >= 4 && id != 8 && id != 9)
        {
            // 分支选出的文字“Knife_MeleeSet/Knife_Light_Melee05”。
string selectedValue2;
            if (id == 10 || id == 11)
            {
                selectedValue2 = "Knife_MeleeSet/Knife_Light_Melee05";
            }
            else
            {
                selectedValue2 = "Knife_MeleeSet/Knife_Light_Melee06";
            }

            return ReadSourceMaterial(selectedValue2);
        }

        return ReadSourceMaterial("Knife_St_IdleSet/Knife_St_Idle_00");
    }

    /// <summary>Sequence 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float)、LeonKnifeAnimationPreview.TotalLong(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    private static string[] Sequence(int id)
    {
        if (id == 14)
        {
            return new[]
            {
                "右向左主挥砍",
                "主挥砍回收",
                "低位待机"
            };
        }

        if (id == 15)
        {
            return new[]
            {
                "右向左主挥砍",
                "左向右回切",
                "回切回收",
                "低位待机"
            };
        }

        if (id == 16)
        {
            return new[]
            {
                "拔刀",
                "高位防御",
                "普通格挡反应",
                "高位防御",
                "弹反成功",
                "高位防御",
                "放下",
                "低位待机"
            };
        }

        return new[]
        {
            actionName[id]
        };
    }

    /// <summary>TotalLong 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.Read(string)、LeonKnifeAnimationPreview.Sequence(int)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.OnGUI()、LeonKnifeAnimationPreview.Update()；下游（含内部回调体） LeonKnifeAnimationPreview.Read(string)、LeonKnifeAnimationPreview.Sequence(int)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    private static float TotalLong(int id)
    {
        // 长度值。
float lengthValue = 0f;
        // name（名称）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数。
foreach (string name in Sequence(id))
        {
            // 动画片段。
var clip = Read(name);
            if (clip != null)
            {
                lengthValue += clip.length;
            }
        }

        return Mathf.Max(lengthValue, 0.01f);
    }

    /// <summary>SelectClip 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.Read(string)、LeonKnifeAnimationPreview.Sequence(int)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.OnGUI()；下游（含内部回调体） LeonKnifeAnimationPreview.Read(string)、LeonKnifeAnimationPreview.Sequence(int)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    /// <param name="timestamp">时间戳。</param>
    /// <param name="clip">动画片段。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="localTime">局部时间。输出参数，由本入口写入并交给调用方。</param>
    private static void SelectClip(int id, float timestamp, out AnimationClip clip, out float localTime)
    {
        clip = null;
        localTime = 0f;
        // name（名称）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数。
foreach (string name in Sequence(id))
        {
            clip = Read(name);
            if (clip == null)
            {
                return;
            }

            localTime = Mathf.Min(timestamp, clip.length);
            if (timestamp <= clip.length)
            {
                return;
            }

            timestamp -= clip.length;
        }
    }

    /// <summary>导出真实模型的多视角关键帧，文件位于项目外层审阅目录。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.MenuExportKeyframe()；下游（含内部回调体） Directory.CreateDirectory(string)、File.WriteAllBytes(string, byte[])、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int)、LeonKnifeAnimationPreview.Read(string) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ExportCheckImage()
    {
        // 目录。
string directory = Path.GetFullPath("持刀动作审阅");
        Directory.CreateDirectory(directory);
        // 名称分组。
string[] nameGroup =
        {
            "低位待机",
            "高位防御",
            "右向左主挥砍",
            "左向右回切",
            "普通格挡反应",
            "弹反成功"
        };
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new PreviewCharacter())
        {
            // name（名称）：每轮从名称分组取得一个元素，仅代表当前这一项。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 LeonKnifeAnimationPreview.Read(string) 的参数、作为 Path.Combine(string, string) 的参数。
foreach (string name in nameGroup)
            {
                // 动画片段。
var clip = Read(name);
                if (clip == null)
                {
                    throw new InvalidOperationException("缺少候选动画：" + name);
                }

                // 拼图。
var puzzle = new Texture2D(1200, 720, TextureFormat.RGB24, false);
                try
                {
                    // 行。
for (int row = 0; row < 2; row++)
                    {
                        // 列。
for (int column = 0; column < 4; column++)
                        {
                            // 时间戳。
float timestamp = clip.length * column / 3f;
                            character.Sample(clip, timestamp, 0, timestamp);
                            // 调用前缓存的角色。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver6 = character;
                            // 按原顺序缓存的实参300。
int argumentValue4 = 300;
                            // 按原顺序缓存的实参360。
int argumentValue5 = 360;
                            // 分支选出的0。
int selectedValue3;
                            if (row == 0)
                            {
                                selectedValue3 = 0;
                            }
                            else
                            {
                                selectedValue3 = 3;
                            }

                            // 单一图像。
Texture2D singleImage = callReceiver6.Screenshot(argumentValue4, argumentValue5, selectedValue3);
                            try
                            {
                                puzzle.SetPixels(column * 300, (1 - row) * 360, 300, 360, singleImage.GetPixels());
                            }
                            finally
                            {
                                Object.DestroyImmediate(singleImage);
                            }
                        }
                    }

                    puzzle.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), puzzle.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(puzzle);
                }
            }
        }

        return directory;
    }

    /// <summary>ExportKnifeGripCheckImage 的业务处理入口；本方法直接依赖 Directory.CreateDirectory(string)、File.WriteAllBytes(string, byte[])、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int)、LeonKnifeAnimationPreview.Read(string)、Object.DestroyImmediate(Object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.MenuExportKnifeGrip()；下游（含内部回调体） Directory.CreateDirectory(string)、File.WriteAllBytes(string, byte[])、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int)、LeonKnifeAnimationPreview.Read(string)、Object.DestroyImmediate(Object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ExportKnifeGripCheckImage()
    {
        Directory.CreateDirectory("持刀动作审阅");
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new PreviewCharacter())
        {
            // name（名称）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、作为 LeonKnifeAnimationPreview.Read(string) 的参数、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()。
foreach (string name in new[]
            {
                "低位待机",
                "高位防御",
                "右向左主挥砍"
            }

            )
            {
                // 动画片段。
var clip = Read(name);
                // 调用前缓存的角色。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver9 = character;
                // 按原顺序缓存的实参动画片段。
global::UnityEngine.AnimationClip argumentValue8 = clip;
                // 分支选出的0f。
float selectedValue7;
                if (name == "右向左主挥砍")
                {
                    selectedValue7 = .3f;
                }
                else
                {
                    selectedValue7 = 0f;
                }

                callReceiver9.Sample(argumentValue8, selectedValue7, 0, 0);
                // 图像。
var image = character.Screenshot(800, 600, 6);
                try
                {
                    File.WriteAllBytes("持刀动作审阅/" + name + "握刀近景.png", image.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(image);
                }
            }
        }

        return "已导出三张握刀近景";
    }

    /// <summary>导出正常速度的双机位审阅帧，供视频编码；每秒三十帧。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Directory.CreateDirectory(string)、File.WriteAllBytes(string, byte[])、int.ToString(string)、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int)、LeonKnifeAnimationPreview.Read(string) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ExportVideoFrame()
    {
        // 目录。
string directory = Path.GetFullPath("Temp/持刀审阅视频帧");
        Directory.CreateDirectory(directory);
        // 名称。
string[] name =
        {
            "低位待机",
            "右向左主挥砍",
            "左向右回切",
            "回切回收",
            "低位待机",
            "抬刀",
            "高位防御",
            "普通格挡反应",
            "弹反成功",
            "放下",
            "低位待机"
        };
        // 持续时间。
float[] duration =
        {
            .8f,
            .6f,
            .6f,
            .2f,
            .5f,
            .2f,
            .8f,
            .35f,
            .45f,
            .25f,
            .5f
        };
        // 序号。
int sequence = 0;
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new PreviewCharacter())
        {
            // 线段。
for (int segment = 0; segment < name.Length; segment++)
            {
                // 动画片段。
var clip = Read(name[segment]);
                // 帧数量。
int frameCount = Mathf.RoundToInt(duration[segment] * 30);
                // 帧。
for (int frame = 0; frame < frameCount; frame++)
                {
                    character.Sample(clip, Mathf.Min(frame / 30f, clip.length), 0, frame / 30f);
                    // 视图。
var view = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    // 前方。
Texture2D front = null;
                    // 后方。
Texture2D back = null;
                    try
                    {
                        front = character.Screenshot(480, 540, 0);
                        back = character.Screenshot(480, 540, 3);
                        view.SetPixels(0, 0, 480, 540, front.GetPixels());
                        view.SetPixels(480, 0, 480, 540, back.GetPixels());
                        view.Apply();
                        File.WriteAllBytes(Path.Combine(directory, "画面" + sequence.ToString("D4") + ".png"), view.EncodeToPNG());
                    }
                    finally
                    {
                        if (front != null)
                        {
                            Object.DestroyImmediate(front);
                        }

                        if (back != null)
                        {
                            Object.DestroyImmediate(back);
                        }

                        Object.DestroyImmediate(view);
                    }

                    sequence++;
                }
            }
        }

        return "已导出" + sequence + "帧：" + directory;
    }

    /// <summary>每个实例拥有独立动画图和预览场景；所有资源随窗口关闭释放。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀动作预览+预览角色")]
    public sealed class PreviewCharacter : IDisposable
    {
        /// <summary>模型。</summary>
        public GameObject Model { get; private set; }
        /// <summary>动画控制器值。</summary>
        public Animator AnimatorValue { get; private set; }

        /// <summary>渲染。</summary>
        private PreviewRenderUtility render;
        /// <summary>动画图像。</summary>
        private PlayableGraph animationImage;
        /// <summary>图层。</summary>
        private AnimationLayerMixerPlayable layer;
        /// <summary>主要动作。</summary>
        private AnimationClipPlayable mainAction;
        /// <summary>移动动作。</summary>
        private AnimationClipPlayable movementAction;
        /// <summary>原地动作。</summary>
        private AnimationClipPlayable inPlaceAction;
        /// <summary>移动混合。</summary>
        private AnimationMixerPlayable movementBlend;
        /// <summary>主要动画片段。</summary>
        private AnimationClip mainClip;
        /// <summary>移动动画片段。</summary>
        private AnimationClip movementClip;
        /// <summary>上半身遮罩。</summary>
        private AvatarMask upperBodyMask;
        /// <summary>地面材质。</summary>
        private Material groundMaterial;
        /// <summary>小刀视觉表现。</summary>
        private Transform knifeVisual;
        /// <summary>小刀视觉表现原始位置。</summary>
        private Vector3 knifeVisualOriginalPosition;
        /// <summary>小刀视觉表现原始旋转。</summary>
        private Quaternion knifeVisualOriginalRotation;
        /// <summary>刀刃尖端局部。</summary>
        private Vector3 bladeTipLocal;
        /// <summary>小刀轴局部。</summary>
        private Vector3 knifeAxisLocal;
        /// <summary>中文含义：当前刀刃尖端。</summary>
        public Vector3 CurrentBladeTip => knifeVisual.TransformPoint(bladeTipLocal);
        /// <summary>中文含义：当前小刀方向。</summary>
        public Vector3 CurrentKnifeDirection => knifeVisual.TransformDirection(knifeAxisLocal).normalized;

        /// <summary>游戏状态机位置。</summary>
        private CameraPose gameMachinePosition;
        /// <summary>蒙皮变形来源。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly List<SkinnedMeshRenderer> skinningSource = new List<SkinnedMeshRenderer>();
        /// <summary>变形网格。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly List<Mesh> deformationMesh = new List<Mesh>();
        /// <summary>构造并初始化 PreviewCharacter 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage() 等共 9 处；下游（含内部回调体） AnimationLayerMixerPlayable.Create(PlayableGraph, int)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationPlayableOutput.Create(PlayableGraph, string, Animator)、AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationLayerMixerPlayable&gt;(AnimationLayerMixerPlayable)、AnimationUtility.CalculateTransformPath(Transform, Transform)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.GetAssetPath(Object) 等共 54 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public PreviewCharacter()
        {
            try
            {
                // 当前场景。
var currentScene = Object.FindFirstObjectByType<LeonController>();
                if (currentScene == null)
                {
                    throw new InvalidOperationException("请先打开包含里昂的主场景。");
                }

                // 来源动画控制器。
var sourceAnimator = currentScene.GetComponent<Animator>();
                // 摄像机配置。
var cameraConfiguration = AssetDatabase.LoadAssetAtPath<CameraProfile>("Assets/Settings/Camera/CameraProfile_Knife.asset");
                gameMachinePosition.offset = new Vector3(.5f, 1.6f, -1.8f);
                gameMachinePosition.fieldOfView = 48f;
                if (cameraConfiguration != null)
                {
                    gameMachinePosition = cameraConfiguration.explorePose;
                }

                if (sourceAnimator == null || !sourceAnimator.isHuman)
                {
                    throw new InvalidOperationException("当前里昂缺少有效人形骨骼。");
                }

                // 路径。
string path = AssetDatabase.GetAssetPath(sourceAnimator.avatar);
                // 来源模型。
var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (sourceModel == null)
                {
                    throw new InvalidOperationException("无法根据当前角色的人形映射找到模型。");
                }

                render = new PreviewRenderUtility();
                render.cameraFieldOfView = 32f;
                render.ambientColor = new Color(0.55f, 0.55f, 0.58f);
                render.lights[0].intensity = 1.1f;
                render.lights[0].transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                render.lights[1].intensity = 0.9f;
                render.lights[1].transform.rotation = Quaternion.Euler(30f, 145f, 0f);
                Model = render.InstantiatePrefabInScene(sourceModel);
                Model.name = "里昂持刀隔离预览";
                Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Model.transform.localScale = Vector3.one;
                AnimatorValue = Model.GetComponent<Animator>();
                AnimatorValue.runtimeAnimatorController = null;
                AnimatorValue.applyRootMotion = false;
                AnimatorValue.fireEvents = false;
                AnimatorValue.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // skinning（蒙皮变形）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimationUtility.CalculateTransformPath(Transform, Transform) 的参数、作为 List<SkinnedMeshRenderer>.Add(SkinnedMeshRenderer) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 4 处。
foreach (var skinning in Model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    // 相对配对。
string relativePair = AnimationUtility.CalculateTransformPath(skinning.transform, Model.transform);
                    // 来源节点。
Transform sourceNode = currentScene.transform.Find(relativePair);
                    if (sourceNode != null)
                    {
                        // 来源蒙皮变形。
var sourceSkinning = sourceNode.GetComponent<SkinnedMeshRenderer>();
                        if (sourceSkinning != null)
                        {
                            skinning.sharedMaterials = sourceSkinning.sharedMaterials;
                            skinning.enabled = sourceSkinning.enabled;
                        }
                    }

                    skinning.updateWhenOffscreen = true;
                    if (skinning.enabled)
                    {
                        // 编辑模式同帧多次采样不会自动刷新蒙皮缓存，因此显式烘焙真实骨骼变形。
                        // 显示对象。
var displayObject = new GameObject("逐帧变形预览");
                        displayObject.transform.SetParent(skinning.transform, false);
                        // 网格。
var mesh = new Mesh();
                        mesh.MarkDynamic();
                        displayObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        displayObject.AddComponent<MeshRenderer>().sharedMaterials = skinning.sharedMaterials;
                        skinningSource.Add(skinning);
                        deformationMesh.Add(mesh);
                        skinning.enabled = false;
                    }
                }

                // 视觉表现。
var visual = currentScene.GetComponent<LeonWeaponVisualController>();
                // 来源小刀。
Transform sourceKnife;
                if (visual == null)
                {
                    sourceKnife = null;
                }
                else
                {
                    sourceKnife = visual.GetWeaponVisualRoot(WeaponType.Knife);
                }

                if (sourceKnife == null)
                {
                    throw new InvalidOperationException("当前角色未绑定实际小刀模型。");
                }

                // 来源手部。
Transform sourceHand = sourceAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                // 目标手部。
Transform targetHand = AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand);
                // 小刀对象。
var knifeObject = Object.Instantiate(sourceKnife.gameObject, targetHand, false);
                knifeVisual = knifeObject.transform;
                knifeVisual.localPosition = sourceHand.InverseTransformPoint(sourceKnife.position);
                knifeVisual.localRotation = Quaternion.Inverse(sourceHand.rotation) * sourceKnife.rotation;
                // 比例。
Vector3 ratio = sourceHand.lossyScale;
                knifeVisual.localScale = new Vector3(sourceKnife.lossyScale.x / ratio.x, sourceKnife.lossyScale.y / ratio.y, sourceKnife.lossyScale.z / ratio.z);
                knifeVisualOriginalPosition = knifeVisual.localPosition;
                knifeVisualOriginalRotation = knifeVisual.localRotation;
                knifeAxisLocal = Quaternion.Inverse(knifeVisualOriginalRotation) * LeonKnifeGrip.originalKnifeAxis;
                // 最远。
float farthest = float.NegativeInfinity;
                // meshValue（网格值）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
foreach (var meshValue in knifeVisual.GetComponentsInChildren<MeshFilter>(true))
                {
                    // top（顶部）：每轮从网格值的共享网格的顶点集合取得一个元素，仅代表当前这一项。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。
foreach (var top in meshValue.sharedMesh.vertices)
                    {
                        // 点。
Vector3 point = knifeVisual.InverseTransformPoint(meshValue.transform.TransformPoint(top));
                        // 距离。
float distance = Vector3.Dot(point, knifeAxisLocal);
                        if (distance > farthest)
                        {
                            farthest = distance;
                            bladeTipLocal = point;
                        }
                    }
                }

                knifeObject.SetActive(true);
                // rowAs（行作为）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
foreach (var rowAs in knifeObject.GetComponentsInChildren<Behaviour>(true))
                {
                    rowAs.enabled = false;
                }

                // mesh（网格）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
foreach (var mesh in knifeObject.GetComponentsInChildren<Renderer>(true))
                {
                    mesh.gameObject.SetActive(true);
                    mesh.enabled = true;
                }

                // 地面。
var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.transform.localScale = Vector3.one * 0.4f;
                ground.transform.position = new Vector3(0f, -0.015f, 0f);
                groundMaterial = new Material(Shader.Find("Standard"));
                groundMaterial.color = new Color(0.16f, 0.19f, 0.22f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                render.AddSingleGO(ground);
                upperBodyMask = new AvatarMask();
                // 当前遍历索引。
for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    upperBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                }

                // bodyPart（身体部位）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AvatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart, bool) 的参数。
foreach (var bodyPart in new[]
                {
                    AvatarMaskBodyPart.Body,
                    AvatarMaskBodyPart.Head,
                    AvatarMaskBodyPart.LeftArm,
                    AvatarMaskBodyPart.RightArm,
                    AvatarMaskBodyPart.LeftFingers,
                    AvatarMaskBodyPart.RightFingers
                }

                )
                {
                    upperBodyMask.SetHumanoidBodyPartActive(bodyPart, true);
                }

                animationImage = PlayableGraph.Create("里昂持刀隔离采样");
                animationImage.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                layer = AnimationLayerMixerPlayable.Create(animationImage, 2);
                layer.SetInputWeight(0, 1f);
                layer.SetInputWeight(1, 0f);
                // 输出。
var output = AnimationPlayableOutput.Create(animationImage, "角色动作", AnimatorValue);
                output.SetSourcePlayable(layer);
                animationImage.Play();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>是否烘焙预览网格。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("烘焙预览网格")]
        [UnityEngine.InspectorName("烘焙预览网格")]
        public bool bakePreviewMesh = true;
        /// <summary>Sample 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double) 等共 41 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage() 等共 9 处；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double) 等共 41 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="clip">动画片段。</param>
        /// <param name="timestamp">时间戳。</param>
        /// <param name="movementType">移动类型。</param>
        /// <param name="totalTime">总计时间。</param>
        public void Sample(AnimationClip clip, float timestamp, int movementType, float totalTime)
        {
            if (clip == null)
            {
                throw new InvalidOperationException("找不到预览动画片段。");
            }

            if (movementType == 0 && clip.name == "移动主挥砍")
            {
                clip = Read("右向左主挥砍");
            }

            if (movementType == 0 && clip.name == "移动回切")
            {
                clip = Read("左向右回切");
            }

            // 方向。
string[] direction =
            {
                "F",
                "F",
                "B",
                "L",
                "R",
                "F"
            };
            // 行走动画。
AnimationClip walkAnimation = null;
            if (movementType > 0 && movementType < 6)
            {
                walkAnimation = ReadSourceMaterial("Knife_St_LocomotionSet_IPC/Walk/Act_Walk_" + direction[movementType] + "_IPC");
            }

            if (movementType == 6)
            {
                walkAnimation = ReadSourceMaterial("Knife_St_LocomotionSet/IdleTurns/Act_Idle_Turn90R_IPC");
            }

            if (movementType == 0 && clip.name.StartsWith("移动", StringComparison.Ordinal))
            {
                walkAnimation = Read("低位待机");
            }

            if (mainClip != clip || movementClip != walkAnimation)
            {
                if (mainAction.IsValid())
                {
                    // 调用前缓存的可播放导航图。
ref global::UnityEngine.Playables.PlayableGraph callReceiver13 = ref animationImage;
                    // 按原顺序缓存的实参图层。
global::UnityEngine.Animations.AnimationLayerMixerPlayable argumentValue12 = layer;
                    // 分支选出的1。
int selectedValue11;
                    if (movementClip != null)
                    {
                        selectedValue11 = 1;
                    }
                    else
                    {
                        selectedValue11 = 0;
                    }

                    callReceiver13.Disconnect(argumentValue12, selectedValue11);
                    animationImage.DestroyPlayable(mainAction);
                }

                if (movementBlend.IsValid())
                {
                    animationImage.Disconnect(layer, 0);
                    animationImage.DestroySubgraph(movementBlend);
                    movementAction = default;
                    inPlaceAction = default;
                    movementBlend = default;
                }

                mainClip = clip;
                movementClip = walkAnimation;
                mainAction = AnimationClipPlayable.Create(animationImage, clip);
                mainAction.SetApplyFootIK(false);
                mainAction.SetApplyPlayableIK(false);
                mainAction.SetSpeed(0);
                if (walkAnimation != null)
                {
                    movementAction = AnimationClipPlayable.Create(animationImage, walkAnimation);
                    movementAction.SetApplyFootIK(false);
                    movementAction.SetApplyPlayableIK(false);
                    movementAction.SetSpeed(0);
                    // 静止动画片段。
var stationaryClip = Read("低位待机");
                    if (stationaryClip == null)
                    {
                        stationaryClip = ReadSourceMaterial("Knife_St_IdleSet/Knife_St_Idle_00");
                    }

                    inPlaceAction = AnimationClipPlayable.Create(animationImage, stationaryClip);
                    inPlaceAction.SetSpeed(0);
                    inPlaceAction.SetApplyFootIK(false);
                    inPlaceAction.SetApplyPlayableIK(false);
                    movementBlend = AnimationMixerPlayable.Create(animationImage, 2);
                    animationImage.Connect(inPlaceAction, 0, movementBlend, 0);
                    animationImage.Connect(movementAction, 0, movementBlend, 1);
                    movementBlend.SetInputWeight(0, 0f);
                    movementBlend.SetInputWeight(1, 1f);
                    animationImage.Connect(movementBlend, 0, layer, 0);
                    animationImage.Connect(mainAction, 0, layer, 1);
                    layer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
                    layer.SetInputWeight(1, 1f);
                }
                else
                {
                    animationImage.Connect(mainAction, 0, layer, 0);
                    layer.SetInputWeight(1, 0f);
                }
            }

            mainAction.SetTime(Mathf.Clamp(timestamp, 0f, clip.length));
            if (movementAction.IsValid())
            {
                movementAction.SetTime(Mathf.Repeat(totalTime, movementClip.length));
                inPlaceAction.SetTime(0f);
                // 权重。
float weight = 1f;
                if (movementType == 5)
                {
                    // 阶段。
float phase = Mathf.Repeat(totalTime, 3f);
                    weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .35f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 1.8f) / .35f)));
                }

                movementBlend.SetInputWeight(0, 1f - weight);
                movementBlend.SetInputWeight(1, weight);
            }

            if (movementType > 0)
            {
                // 先取完整动作的上身朝向，再让步态独立决定骨盆与双腿。
                // 完成动画片段。
global::UnityEngine.AnimationClip completeClip;
                if (clip.name == "移动主挥砍")
                {
                    completeClip = Read("右向左主挥砍");
                }
                else
                {
                    if (clip.name == "移动回切")
                    {
                        completeClip = Read("左向右回切");
                    }
                    else
                    {
                        completeClip = clip;
                    }
                }

                completeClip.SampleAnimation(Model, Mathf.Clamp(timestamp, 0f, completeClip.length));
                Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // 脊柱。
var spine = AnimatorValue.GetBoneTransform(HumanBodyBones.Spine);
                // 上半身骨骼。
var upperBodyBone = spine.GetComponentsInChildren<Transform>(true);
                // 旋转值。
var rotationValue = upperBodyBone.Select( /* 中文含义：骨骼。 */bone => bone.localRotation).ToArray();
                // 位置值。
var positionValue = upperBodyBone.Select( /* 中文含义：骨骼。 */bone => bone.localPosition).ToArray();
                // 朝向。
Quaternion orientation = spine.rotation;
                layer.SetInputWeight(1, 0f);
                animationImage.Evaluate(0f);
                animationImage.Evaluate(0f);
                Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // 当前元素索引。
for (int index = 0; index < upperBodyBone.Length; index++)
                {
                    upperBodyBone[index].localRotation = rotationValue[index];
                    upperBodyBone[index].localPosition = positionValue[index];
                }

                spine.rotation = orientation;
                if (clip.name == "低位待机")
                {
                    // 摆臂跟随实际脚步，停步时自然衰减，刀手幅度小于空手。
                    // 左脚位置。
var leftFootPosition = AnimatorValue.GetBoneTransform(HumanBodyBones.LeftFoot);
                    // 右脚位置。
var rightFootPosition = AnimatorValue.GetBoneTransform(HumanBodyBones.RightFoot);
                    // 步幅差值。
float strideDifference = Mathf.Clamp((leftFootPosition.position.z - rightFootPosition.position.z) / .65f, -1f, 1f) * movementBlend.GetInputWeight(1);
                    // 左侧手臂。
var leftArm = AnimatorValue.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    // 右侧手臂。
var rightArm = AnimatorValue.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    leftArm.rotation = Quaternion.AngleAxis(strideDifference * 7f, Vector3.right) * leftArm.rotation;
                    rightArm.rotation = Quaternion.AngleAxis(-strideDifference * 4f, Vector3.right) * rightArm.rotation;
                }

                layer.SetInputWeight(1, 1f);
            }
            else
            {
                animationImage.Evaluate(0f);
                animationImage.Evaluate(0f);
            }

            Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            LeonKnifeGrip.ApplyKnifeVisual(knifeVisual, knifeVisualOriginalPosition, knifeVisualOriginalRotation, clip);
            LeonKnifeGrip.ApplyFinger(AnimatorValue, clip);
            // 当前遍历索引。
for (int i = 0; bakePreviewMesh && i < skinningSource.Count; i++)
            {
                skinningSource[i].BakeMesh(deformationMesh[i]);
                deformationMesh[i].RecalculateBounds();
            }

            // 是否显示小刀。
bool showKnife = true;
            if (clip.name == "拔刀")
            {
                showKnife = timestamp >= clip.length * 0.075f;
            }

            if (clip.name == "收刀")
            {
                showKnife = timestamp < clip.length * 0.92f;
            }

            if (knifeVisual != null)
            {
                knifeVisual.gameObject.SetActive(showKnife);
            }
        }

        /// <summary>SetMachinePosition 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Color.Color(float, float, float)、Transform.LookAt(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int)、LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Color.Color(float, float, float)、Transform.LookAt(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="id">编号。</param>
        private void SetMachinePosition(int id)
        {
            render.cameraFieldOfView = 32f;
            // 目标。
Vector3 target = new Vector3(0f, 0.95f, 0f);
            // 位置值。
Vector3 positionValue = new Vector3(0f, 1.1f, 3.9f);
            if (id == 1)
            {
                positionValue = new Vector3(3.9f, 1.1f, 0f);
            }

            if (id == 2)
            {
                positionValue = new Vector3(0f, 1.1f, -3.9f);
            }

            if (id == 3)
            {
                positionValue = new Vector3(1.55f, 1.65f, -2.9f);
                target = new Vector3(0f, 1.05f, 0.15f);
            }

            if (id == 4)
            {
                positionValue = new Vector3(-3.9f, 1.1f, 0f);
            }

            if (id == 6)
            {
                target = AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand).position;
                positionValue = target + new Vector3(.38f, .13f, .65f);
            }

            render.camera.transform.position = positionValue;
            render.camera.transform.LookAt(target);
            if (id == 5)
            {
                render.camera.transform.position = gameMachinePosition.offset;
                render.camera.transform.rotation = Quaternion.identity;
                render.cameraFieldOfView = gameMachinePosition.fieldOfView;
            }

            render.camera.nearClipPlane = 0.05f;
            render.camera.farClipPlane = 30f;
            render.camera.clearFlags = CameraClearFlags.Color;
            render.camera.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
        }

        /// <summary>Draw 的业务处理入口；本方法直接依赖 GUI.DrawTexture(Rect, Texture, ScaleMode, bool)、LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginPreview(Rect, GUIStyle)、PreviewRenderUtility.EndPreview()、PreviewRenderUtility.Render(bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.OnGUI()；下游（含内部回调体） GUI.DrawTexture(Rect, Texture, ScaleMode, bool)、LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginPreview(Rect, GUIStyle)、PreviewRenderUtility.EndPreview()、PreviewRenderUtility.Render(bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="region">区域。</param>
        /// <param name="id">编号。</param>
        public void Draw(Rect region, int id)
        {
            SetMachinePosition(id);
            render.BeginPreview(region, GUIStyle.none);
            render.Render(true, false);
            GUI.DrawTexture(region, render.EndPreview(), ScaleMode.StretchToFill, false);
        }

        /// <summary>Screenshot 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginStaticPreview(Rect)、PreviewRenderUtility.EndStaticPreview()、PreviewRenderUtility.Render(bool, bool)、Rect.Rect(float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()、LeonKnifeAnimationPreview.ExportVideoFrame()；下游（含内部回调体） LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginStaticPreview(Rect)、PreviewRenderUtility.EndStaticPreview()、PreviewRenderUtility.Render(bool, bool)、Rect.Rect(float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="id">编号。</param>
        public Texture2D Screenshot(int width, int height, int id)
        {
            SetMachinePosition(id);
            render.BeginStaticPreview(new Rect(0, 0, width, height));
            render.Render(true, false);
            return render.EndStaticPreview();
        }

        /// <summary>Dispose 的业务处理入口；本方法直接依赖 List&lt;Mesh&gt;.Clear()、List&lt;SkinnedMeshRenderer&gt;.Clear()、Object.DestroyImmediate(Object)、PlayableGraph.Destroy()、PlayableGraph.IsValid()、PreviewRenderUtility.Cleanup()，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonKnifeAnimationPreview.CleanupValue()、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()；下游（含内部回调体） List&lt;Mesh&gt;.Clear()、List&lt;SkinnedMeshRenderer&gt;.Clear()、Object.DestroyImmediate(Object)、PlayableGraph.Destroy()、PlayableGraph.IsValid()、PreviewRenderUtility.Cleanup()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public void Dispose()
        {
            if (animationImage.IsValid())
            {
                animationImage.Destroy();
            }

            if (upperBodyMask != null)
            {
                Object.DestroyImmediate(upperBodyMask);
            }

            if (render != null)
            {
                render.Cleanup();
            }

            // mesh（网格）：每轮从变形网格取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Dispose()。
foreach (var mesh in deformationMesh)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }

            deformationMesh.Clear();
            skinningSource.Clear();
            if (groundMaterial != null)
            {
                Object.DestroyImmediate(groundMaterial);
            }

            render = null;
        }
    }
}
