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
    /// <summary>中文含义：候选目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数。</summary>
    public const string candidateDirectory = "Assets/Animations/里昂持刀候选";
    /// <summary>中文含义：模型路径。编译期常量，保持既有值与消费者映射。</summary>
    public const string modelPath = "Assets/Models/角色/里昂/LeonR4R.fbx";
    /// <summary>中文含义：来源材质目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数。</summary>
    public const string sourceMaterialDirectory = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/";
    /// <summary>中文含义：动作名称。只读字段，初始化后不由普通写入口替换。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数、LeonKnifeAnimationPreview.Sequence(int)。</summary>
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
    /// <summary>中文含义：视图名称。只读字段，初始化后不由普通写入口替换。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数。</summary>
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
    /// <summary>中文含义：移动名称。只读字段，初始化后不由普通写入口替换。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数。</summary>
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
    /// <summary>中文含义：新角色。使用位置：LeonKnifeAnimationPreview.CleanupValue()、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private PreviewCharacter newCharacter;
    /// <summary>中文含义：前一次角色。使用位置：LeonKnifeAnimationPreview.CleanupValue()、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private PreviewCharacter previousCharacter;
    /// <summary>中文含义：动作。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数、作为 LeonKnifeAnimationPreview.OriginalSourceMaterial(int) 的参数、作为 LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float) 的参数 等共 5 处。</summary>
    private int action;
    /// <summary>中文含义：视图。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int) 的参数、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private int view;
    /// <summary>中文含义：移动。使用位置：作为 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private int movement;
    /// <summary>中文含义：时间。使用位置：作为 EditorGUILayout.Slider(string, float, float, float, params GUILayoutOption[]) 的参数、作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float) 的参数 等共 8 处。</summary>
    private float time;
    /// <summary>中文含义：速度。使用位置：作为 EditorGUILayout.Slider(string, float, float, float, params GUILayoutOption[]) 的参数、LeonKnifeAnimationPreview.OnGUI()、LeonKnifeAnimationPreview.Update()。</summary>
    private float speed = 1f;
    /// <summary>中文含义：是否播放。使用位置：LeonKnifeAnimationPreview.OnGUI()、LeonKnifeAnimationPreview.Update()。</summary>
    private bool play = true;
    /// <summary>中文含义：是否循环。使用位置：作为 GUILayout.Toggle(bool, string, params GUILayoutOption[]) 的参数、LeonKnifeAnimationPreview.OnGUI()、LeonKnifeAnimationPreview.Update()。</summary>
    private bool loop = true;
    /// <summary>中文含义：是否引用。使用位置：作为 EditorGUILayout.ToggleLeft(string, bool, params GUILayoutOption[]) 的参数、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private bool reference;
    /// <summary>中文含义：上一次时间。使用位置：作为 Mathf.Min(float, float) 的参数、LeonKnifeAnimationPreview.OnEnable()、LeonKnifeAnimationPreview.Update()。</summary>
    private double lastTime;
    /// <summary>中文含义：错误。使用位置：作为 EditorGUILayout.HelpBox(string, MessageType) 的参数、作为 string.IsNullOrEmpty(string) 的参数、LeonKnifeAnimationPreview.OnGUI()。</summary>
    private string error;
    /// <summary>编辑器菜单调用入口 Open；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.GetWindow&lt;LeonKnifeAnimationPreview&gt;()、EditorWindow.Show()、GUIContent.GUIContent(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/打开动作预览")]
    public static void Open()
    {
        // window（窗口）：保存 EditorWindow.GetWindow<LeonKnifeAnimationPreview>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.Open()。
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
        // current（当前）：使用位置：作为 Mathf.Min(float, float) 的参数、LeonKnifeAnimationPreview.Update()。
double current = EditorApplication.timeSinceStartup;
        if (play && !EditorApplication.isCompiling)
        {
            time += Mathf.Min((float)(current - lastTime), 0.1f) * speed;
            // length（长度）：保存 LeonKnifeAnimationPreview.TotalLong(int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Repeat(float, float) 的参数、LeonKnifeAnimationPreview.Update()。
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
        // newAction（新动作）：保存 EditorGUILayout.Popup(string, int, string[], params GUILayoutOption[]) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.OnGUI()。
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
        // selectedValue1（分支选出的文字“暂停”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GUILayout.Button(string, params GUILayoutOption[]) 的参数、LeonKnifeAnimationPreview.OnGUI()。
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

        // region（区域）：保存 GUILayoutUtility.GetRect(float, float, float, float, params GUILayoutOption[]) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int) 的参数、作为 Rect.Rect(float, float, float, float) 的参数。
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

            // clip（动画片段）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float) 的参数、作为 Mathf.Clamp01(float) 的参数 等共 4 处。
AnimationClip clip;
            // localTime（局部时间）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float) 的参数、作为 Mathf.Clamp01(float) 的参数。
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

                // originalClip（原始动画片段）：保存 LeonKnifeAnimationPreview.OriginalSourceMaterial(int) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeAnimationPreview.OnGUI()。
var originalClip = OriginalSourceMaterial(action);
                // originalTime（原始时间）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeAnimationPreview.OnGUI()。
float originalTime = 0f;
                if (originalClip != null)
                {
                    originalTime = Mathf.Clamp01(localTime / clip.length) * originalClip.length;
                }

                previousCharacter.Sample(originalClip, originalTime, movement, time);
                // left（左侧）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int) 的参数、作为 Rect.Rect(float, float, float, float) 的参数。
Rect left = new Rect(region.x, region.y, region.width * 0.5f - 2f, region.height);
                // right（右侧）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int) 的参数、作为 Rect.Rect(float, float, float, float) 的参数。
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
    /// <param name="name">中文含义：名称。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数。</param>
    public static AnimationClip Read(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(candidateDirectory + "/" + name + ".anim");
    }

    /// <summary>ReadSourceMaterial 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、LeonKnifeAnimationPreview.OriginalSourceMaterial(int)、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)、LeonKnifeWebExporter.ExportTo(string, string)；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="relativePairPath">中文含义：相对配对路径。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数。</param>
    public static AnimationClip ReadSourceMaterial(string relativePairPath)
    {
        // asset（资源）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationPreview.ReadSourceMaterial(string)。
foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sourceMaterialDirectory + relativePairPath + ".fbx"))
        {
            // clip（动画片段）：使用位置：LeonKnifeAnimationPreview.ReadSourceMaterial(string)。
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
    /// <param name="id">中文含义：编号。使用位置：LeonKnifeAnimationPreview.OriginalSourceMaterial(int)。</param>
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
            // selectedValue2（分支选出的文字“Knife_MeleeSet/Knife_Light_Melee05”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonKnifeAnimationPreview.ReadSourceMaterial(string) 的参数、LeonKnifeAnimationPreview.OriginalSourceMaterial(int)。
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
    /// <param name="id">中文含义：编号。使用位置：LeonKnifeAnimationPreview.Sequence(int)。</param>
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
    /// <param name="id">中文含义：编号。使用位置：作为 LeonKnifeAnimationPreview.Sequence(int) 的参数。</param>
    private static float TotalLong(int id)
    {
        // lengthValue（长度值）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonKnifeAnimationPreview.TotalLong(int)。
float lengthValue = 0f;
        // name（名称）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数。
foreach (string name in Sequence(id))
        {
            // clip（动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.TotalLong(int)。
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
    /// <param name="id">中文含义：编号。使用位置：作为 LeonKnifeAnimationPreview.Sequence(int) 的参数。</param>
    /// <param name="timestamp">中文含义：时间戳。使用位置：作为 Mathf.Min(float, float) 的参数、LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float)。</param>
    /// <param name="clip">中文含义：动画片段。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Min(float, float) 的参数、LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float)。</param>
    /// <param name="localTime">中文含义：局部时间。输出参数，由本入口写入并交给调用方。使用位置：LeonKnifeAnimationPreview.SelectClip(int, float, out AnimationClip, out float)。</param>
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
        // directory（目录）：保存 Path.GetFullPath(string) 的返回结果，供当前步骤使用。使用位置：作为 Directory.CreateDirectory(string) 的参数、作为 Path.Combine(string, string) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
string directory = Path.GetFullPath("持刀动作审阅");
        Directory.CreateDirectory(directory);
        // nameGroup（名称分组）：使用位置：LeonKnifeAnimationPreview.ExportCheckImage()。
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
                // clip（动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
var clip = Read(name);
                if (clip == null)
                {
                    throw new InvalidOperationException("缺少候选动画：" + name);
                }

                // puzzle（拼图）：使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
var puzzle = new Texture2D(1200, 720, TextureFormat.RGB24, false);
                try
                {
                    // row（行）：使用位置：作为 Texture2D.SetPixels(int, int, int, int, Color[]) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
for (int row = 0; row < 2; row++)
                    {
                        // column（列）：使用位置：作为 Texture2D.SetPixels(int, int, int, int, Color[]) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
for (int column = 0; column < 4; column++)
                        {
                            // timestamp（时间戳）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
float timestamp = clip.length * column / 3f;
                            character.Sample(clip, timestamp, 0, timestamp);
                            // callReceiver6（调用前缓存的角色）：从角色读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeAnimationPreview.ExportCheckImage()。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver6 = character;
                            // argumentValue4（按原顺序缓存的实参300）：从300读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int) 的参数。
int argumentValue4 = 300;
                            // argumentValue5（按原顺序缓存的实参360）：从360读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int) 的参数。
int argumentValue5 = 360;
                            // selectedValue3（分支选出的0）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int) 的参数、LeonKnifeAnimationPreview.ExportCheckImage()。
int selectedValue3;
                            if (row == 0)
                            {
                                selectedValue3 = 0;
                            }
                            else
                            {
                                selectedValue3 = 3;
                            }

                            // singleImage（单一图像）：保存 LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 Texture2D.SetPixels(int, int, int, int, Color[]) 的参数。
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
                // clip（动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()。
var clip = Read(name);
                // callReceiver9（调用前缓存的角色）：从角色读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver9 = character;
                // argumentValue8（按原顺序缓存的实参动画片段）：从动画片段读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
global::UnityEngine.AnimationClip argumentValue8 = clip;
                // selectedValue7（分支选出的0f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()。
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
                // image（图像）：保存 LeonKnifeAnimationPreview.PreviewCharacter.Screenshot(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数。
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
        // directory（目录）：保存 Path.GetFullPath(string) 的返回结果，供当前步骤使用。使用位置：作为 Directory.CreateDirectory(string) 的参数、作为 Path.Combine(string, string) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
string directory = Path.GetFullPath("Temp/持刀审阅视频帧");
        Directory.CreateDirectory(directory);
        // name（名称）：使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
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
        // duration（持续时间）：使用位置：作为 Mathf.RoundToInt(float) 的参数。
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
        // sequence（序号）：使用位置：作为 Path.Combine(string, string) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
int sequence = 0;
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new PreviewCharacter())
        {
            // segment（线段）：使用位置：LeonKnifeAnimationPreview.ExportVideoFrame()。
for (int segment = 0; segment < name.Length; segment++)
            {
                // clip（动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 Mathf.Min(float, float) 的参数。
var clip = Read(name[segment]);
                // frameCount（帧数量）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.ExportVideoFrame()。
int frameCount = Mathf.RoundToInt(duration[segment] * 30);
                // frame（帧）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
for (int frame = 0; frame < frameCount; frame++)
                {
                    character.Sample(clip, Mathf.Min(frame / 30f, clip.length), 0, frame / 30f);
                    // view（视图）：使用位置：作为 File.WriteAllBytes(string, byte[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
var view = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    // front（前方）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 Texture2D.SetPixels(int, int, int, int, Color[]) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
Texture2D front = null;
                    // back（后方）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 Texture2D.SetPixels(int, int, int, int, Color[]) 的参数、LeonKnifeAnimationPreview.ExportVideoFrame()。
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
        /// <summary>中文含义：模型。使用位置：作为 AnimationClip.SampleAnimation(GameObject, float) 的参数、作为 AnimationUtility.CalculateTransformPath(Transform, Transform) 的参数、作为 KnifePreviewReferenceValidator.BindReference(GameObject, JObject) 的参数 等共 6 处。</summary>
        public GameObject Model { get; private set; }
        /// <summary>中文含义：动画控制器值。使用位置：作为 AnimationPlayableOutput.Create(PlayableGraph, string, Animator) 的参数、作为 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 的参数、作为 LeonKnifeGrip.ApplyFinger(Animator, AnimationClip) 的参数 等共 11 处。</summary>
        public Animator AnimatorValue { get; private set; }

        /// <summary>中文含义：渲染。使用位置：作为 GUI.DrawTexture(Rect, Texture, ScaleMode, bool) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Dispose()、LeonKnifeAnimationPreview.PreviewCharacter.Draw(Rect, int) 等共 6 处。</summary>
        private PreviewRenderUtility render;
        /// <summary>中文含义：动画图像。使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数、作为 AnimationLayerMixerPlayable.Create(PlayableGraph, int) 的参数、作为 AnimationMixerPlayable.Create(PlayableGraph, int) 的参数 等共 7 处。</summary>
        private PlayableGraph animationImage;
        /// <summary>中文含义：图层。使用位置：作为 AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationLayerMixerPlayable&gt;(AnimationLayerMixerPlayable) 的参数、作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationLayerMixerPlayable&gt;(AnimationClipPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.Connect&lt;AnimationMixerPlayable, AnimationLayerMixerPlayable&gt;(AnimationMixerPlayable, int, AnimationLayerMixerPlayable, int) 的参数 等共 6 处。</summary>
        private AnimationLayerMixerPlayable layer;
        /// <summary>中文含义：主要动作。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationLayerMixerPlayable&gt;(AnimationClipPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.DestroyPlayable&lt;AnimationClipPlayable&gt;(AnimationClipPlayable) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private AnimationClipPlayable mainAction;
        /// <summary>中文含义：移动动作。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private AnimationClipPlayable movementAction;
        /// <summary>中文含义：原地动作。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private AnimationClipPlayable inPlaceAction;
        /// <summary>中文含义：移动混合。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、作为 PlayableGraph.Connect&lt;AnimationMixerPlayable, AnimationLayerMixerPlayable&gt;(AnimationMixerPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.DestroySubgraph&lt;AnimationMixerPlayable&gt;(AnimationMixerPlayable) 的参数 等共 4 处。</summary>
        private AnimationMixerPlayable movementBlend;
        /// <summary>中文含义：主要动画片段。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private AnimationClip mainClip;
        /// <summary>中文含义：移动动画片段。使用位置：作为 Mathf.Repeat(float, float) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private AnimationClip movementClip;
        /// <summary>中文含义：上半身遮罩。使用位置：作为 AnimationLayerMixerPlayable.SetLayerMaskFromAvatarMask(uint, AvatarMask) 的参数、作为 Object.DestroyImmediate(Object) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Dispose() 等共 4 处。</summary>
        private AvatarMask upperBodyMask;
        /// <summary>中文含义：地面材质。使用位置：作为 Object.DestroyImmediate(Object) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Dispose()、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。</summary>
        private Material groundMaterial;
        /// <summary>中文含义：小刀视觉表现。使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.CurrentBladeTip.get、LeonKnifeAnimationPreview.PreviewCharacter.CurrentKnifeDirection.get 等共 5 处。</summary>
        private Transform knifeVisual;
        /// <summary>中文含义：小刀视觉表现原始位置。使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。</summary>
        private Vector3 knifeVisualOriginalPosition;
        /// <summary>中文含义：小刀视觉表现原始旋转。使用位置：作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数、作为 Quaternion.Inverse(Quaternion) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。</summary>
        private Quaternion knifeVisualOriginalRotation;
        /// <summary>中文含义：刀刃尖端局部。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。</summary>
        private Vector3 bladeTipLocal;
        /// <summary>中文含义：小刀轴局部。使用位置：作为 Transform.TransformDirection(Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。</summary>
        private Vector3 knifeAxisLocal;
        /// <summary>中文含义：当前刀刃尖端。</summary>
        public Vector3 CurrentBladeTip => knifeVisual.TransformPoint(bladeTipLocal);
        /// <summary>中文含义：当前小刀方向。</summary>
        public Vector3 CurrentKnifeDirection => knifeVisual.TransformDirection(knifeAxisLocal).normalized;

        /// <summary>中文含义：游戏状态机位置。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)。</summary>
        private CameraPose gameMachinePosition;
        /// <summary>中文含义：蒙皮变形来源。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Dispose()、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</summary>
        private readonly List<SkinnedMeshRenderer> skinningSource = new List<SkinnedMeshRenderer>();
        /// <summary>中文含义：变形网格。只读字段，初始化后不由普通写入口替换。使用位置：作为 SkinnedMeshRenderer.BakeMesh(Mesh) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Dispose()、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter() 等共 4 处。</summary>
        private readonly List<Mesh> deformationMesh = new List<Mesh>();
        /// <summary>构造并初始化 PreviewCharacter 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage() 等共 9 处；下游（含内部回调体） AnimationLayerMixerPlayable.Create(PlayableGraph, int)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationPlayableOutput.Create(PlayableGraph, string, Animator)、AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationLayerMixerPlayable&gt;(AnimationLayerMixerPlayable)、AnimationUtility.CalculateTransformPath(Transform, Transform)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.GetAssetPath(Object) 等共 54 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public PreviewCharacter()
        {
            try
            {
                // currentScene（当前场景）：保存 Object.FindFirstObjectByType<LeonController>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var currentScene = Object.FindFirstObjectByType<LeonController>();
                if (currentScene == null)
                {
                    throw new InvalidOperationException("请先打开包含里昂的主场景。");
                }

                // sourceAnimator（来源动画控制器）：保存 Component.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.GetAssetPath(Object) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var sourceAnimator = currentScene.GetComponent<Animator>();
                // cameraConfiguration（摄像机配置）：保存 AssetDatabase.LoadAssetAtPath<CameraProfile>(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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

                // path（路径）：保存 AssetDatabase.GetAssetPath(Object) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数。
string path = AssetDatabase.GetAssetPath(sourceAnimator.avatar);
                // sourceModel（来源模型）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 PreviewRenderUtility.InstantiatePrefabInScene(GameObject) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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
                    // relativePair（相对配对）：保存 AnimationUtility.CalculateTransformPath(Transform, Transform) 的返回结果，供当前步骤使用。使用位置：作为 Transform.Find(string) 的参数。
string relativePair = AnimationUtility.CalculateTransformPath(skinning.transform, Model.transform);
                    // sourceNode（来源节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
Transform sourceNode = currentScene.transform.Find(relativePair);
                    if (sourceNode != null)
                    {
                        // sourceSkinning（来源蒙皮变形）：保存 Component.GetComponent<SkinnedMeshRenderer>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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
                        // displayObject（显示对象）：使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var displayObject = new GameObject("逐帧变形预览");
                        displayObject.transform.SetParent(skinning.transform, false);
                        // mesh（网格）：使用位置：作为 List<Mesh>.Add(Mesh) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var mesh = new Mesh();
                        mesh.MarkDynamic();
                        displayObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        displayObject.AddComponent<MeshRenderer>().sharedMaterials = skinning.sharedMaterials;
                        skinningSource.Add(skinning);
                        deformationMesh.Add(mesh);
                        skinning.enabled = false;
                    }
                }

                // visual（视觉表现）：保存 Component.GetComponent<LeonWeaponVisualController>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var visual = currentScene.GetComponent<LeonWeaponVisualController>();
                // sourceKnife（来源小刀）：使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 4 处。
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

                // sourceHand（来源手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Inverse(Quaternion) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
Transform sourceHand = sourceAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                // targetHand（目标手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的参数。
Transform targetHand = AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand);
                // knifeObject（小刀对象）：保存 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var knifeObject = Object.Instantiate(sourceKnife.gameObject, targetHand, false);
                knifeVisual = knifeObject.transform;
                knifeVisual.localPosition = sourceHand.InverseTransformPoint(sourceKnife.position);
                knifeVisual.localRotation = Quaternion.Inverse(sourceHand.rotation) * sourceKnife.rotation;
                // ratio（比例）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 ratio = sourceHand.lossyScale;
                knifeVisual.localScale = new Vector3(sourceKnife.lossyScale.x / ratio.x, sourceKnife.lossyScale.y / ratio.y, sourceKnife.lossyScale.z / ratio.z);
                knifeVisualOriginalPosition = knifeVisual.localPosition;
                knifeVisualOriginalRotation = knifeVisual.localRotation;
                knifeAxisLocal = Quaternion.Inverse(knifeVisualOriginalRotation) * LeonKnifeGrip.originalKnifeAxis;
                // farthest（最远）：使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
float farthest = float.NegativeInfinity;
                // meshValue（网格值）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
foreach (var meshValue in knifeVisual.GetComponentsInChildren<MeshFilter>(true))
                {
                    // top（顶部）：每轮从网格值的共享网格的顶点集合取得一个元素，仅代表当前这一项。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。
foreach (var top in meshValue.sharedMesh.vertices)
                    {
                        // point（点）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
Vector3 point = knifeVisual.InverseTransformPoint(meshValue.transform.TransformPoint(top));
                        // distance（距离）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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

                // ground（地面）：保存 GameObject.CreatePrimitive(PrimitiveType) 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 PreviewRenderUtility.AddSingleGO(GameObject) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.transform.localScale = Vector3.one * 0.4f;
                ground.transform.position = new Vector3(0f, -0.015f, 0f);
                groundMaterial = new Material(Shader.Find("Standard"));
                groundMaterial.color = new Color(0.16f, 0.19f, 0.22f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                render.AddSingleGO(ground);
                upperBodyMask = new AvatarMask();
                // i（当前遍历索引）：使用位置：作为 AvatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart, bool) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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
                // output（输出）：保存 AnimationPlayableOutput.Create(PlayableGraph, string, Animator) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter()。
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

        /// <summary>中文含义：是否烘焙预览网格。使用位置：KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool) 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("烘焙预览网格")]
        [UnityEngine.InspectorName("烘焙预览网格")]
        public bool bakePreviewMesh = true;
        /// <summary>Sample 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double) 等共 41 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage() 等共 9 处；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double) 等共 41 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="clip">中文含义：动画片段。使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数、作为 LeonKnifeGrip.ApplyFinger(Animator, AnimationClip) 的参数、作为 LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip) 的参数 等共 5 处。</param>
        /// <param name="timestamp">中文含义：时间戳。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</param>
        /// <param name="movementType">中文含义：移动类型。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。</param>
        /// <param name="totalTime">中文含义：总计时间。使用位置：作为 Mathf.Repeat(float, float) 的参数。</param>
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

            // direction（方向）：使用位置：作为 LeonKnifeAnimationPreview.ReadSourceMaterial(string) 的参数。
string[] direction =
            {
                "F",
                "F",
                "B",
                "L",
                "R",
                "F"
            };
            // walkAnimation（行走动画）：使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
                    // callReceiver13（调用前缓存的可播放导航图）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
ref global::UnityEngine.Playables.PlayableGraph callReceiver13 = ref animationImage;
                    // argumentValue12（按原顺序缓存的实参图层）：从图层读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PlayableGraph.Disconnect<AnimationLayerMixerPlayable>(AnimationLayerMixerPlayable, int) 的参数。
global::UnityEngine.Animations.AnimationLayerMixerPlayable argumentValue12 = layer;
                    // selectedValue11（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PlayableGraph.Disconnect<AnimationLayerMixerPlayable>(AnimationLayerMixerPlayable, int) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
                    // stationaryClip（静止动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
                // weight（权重）：使用位置：作为 AnimationMixerPlayable.SetInputWeight<AnimationMixerPlayable>(int, float) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
float weight = 1f;
                if (movementType == 5)
                {
                    // phase（阶段）：保存 Mathf.Repeat(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数。
float phase = Mathf.Repeat(totalTime, 3f);
                    weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .35f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 1.8f) / .35f)));
                }

                movementBlend.SetInputWeight(0, 1f - weight);
                movementBlend.SetInputWeight(1, weight);
            }

            if (movementType > 0)
            {
                // 先取完整动作的上身朝向，再让步态独立决定骨盆与双腿。
                // completeClip（完成动画片段）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
                // spine（脊柱）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
var spine = AnimatorValue.GetBoneTransform(HumanBodyBones.Spine);
                // upperBodyBone（上半身骨骼）：保存 Component.GetComponentsInChildren<Transform>(bool) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
var upperBodyBone = spine.GetComponentsInChildren<Transform>(true);
                // rotationValue（旋转值）：保存 IEnumerable<Quaternion>.ToArray<Quaternion>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
var rotationValue = upperBodyBone.Select( /* 中文含义：骨骼。 */bone => bone.localRotation).ToArray();
                // positionValue（位置值）：保存 IEnumerable<Vector3>.ToArray<Vector3>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
var positionValue = upperBodyBone.Select( /* 中文含义：骨骼。 */bone => bone.localPosition).ToArray();
                // orientation（朝向）：使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
Quaternion orientation = spine.rotation;
                layer.SetInputWeight(1, 0f);
                animationImage.Evaluate(0f);
                animationImage.Evaluate(0f);
                Model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // index（当前元素索引）：使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
for (int index = 0; index < upperBodyBone.Length; index++)
                {
                    upperBodyBone[index].localRotation = rotationValue[index];
                    upperBodyBone[index].localPosition = positionValue[index];
                }

                spine.rotation = orientation;
                if (clip.name == "低位待机")
                {
                    // 摆臂跟随实际脚步，停步时自然衰减，刀手幅度小于空手。
                    // leftFootPosition（左脚位置）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
var leftFootPosition = AnimatorValue.GetBoneTransform(HumanBodyBones.LeftFoot);
                    // rightFootPosition（右脚位置）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
var rightFootPosition = AnimatorValue.GetBoneTransform(HumanBodyBones.RightFoot);
                    // strideDifference（步幅差值）：使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。
float strideDifference = Mathf.Clamp((leftFootPosition.position.z - rightFootPosition.position.z) / .65f, -1f, 1f) * movementBlend.GetInputWeight(1);
                    // leftArm（左侧手臂）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
var leftArm = AnimatorValue.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    // rightArm（右侧手臂）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
            // i（当前遍历索引）：使用位置：作为 List<Mesh>.this[int] 的参数、作为 List<SkinnedMeshRenderer>.this[int] 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
for (int i = 0; bakePreviewMesh && i < skinningSource.Count; i++)
            {
                skinningSource[i].BakeMesh(deformationMesh[i]);
                deformationMesh[i].RecalculateBounds();
            }

            // showKnife（是否显示小刀）：使用位置：作为 GameObject.SetActive(bool) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)。
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
        /// <param name="id">中文含义：编号。使用位置：LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)。</param>
        private void SetMachinePosition(int id)
        {
            render.cameraFieldOfView = 32f;
            // target（目标）：使用位置：作为 Transform.LookAt(Vector3) 的参数、LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)。
Vector3 target = new Vector3(0f, 0.95f, 0f);
            // positionValue（位置值）：使用位置：LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)。
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
        /// <param name="region">中文含义：区域。使用位置：作为 GUI.DrawTexture(Rect, Texture, ScaleMode, bool) 的参数、作为 PreviewRenderUtility.BeginPreview(Rect, GUIStyle) 的参数。</param>
        /// <param name="id">中文含义：编号。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int) 的参数。</param>
        public void Draw(Rect region, int id)
        {
            SetMachinePosition(id);
            render.BeginPreview(region, GUIStyle.none);
            render.Render(true, false);
            GUI.DrawTexture(region, render.EndPreview(), ScaleMode.StretchToFill, false);
        }

        /// <summary>Screenshot 的业务处理入口；本方法直接依赖 LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginStaticPreview(Rect)、PreviewRenderUtility.EndStaticPreview()、PreviewRenderUtility.Render(bool, bool)、Rect.Rect(float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.ExportCheckImage()、LeonKnifeAnimationPreview.ExportKnifeGripCheckImage()、LeonKnifeAnimationPreview.ExportVideoFrame()；下游（含内部回调体） LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int)、PreviewRenderUtility.BeginStaticPreview(Rect)、PreviewRenderUtility.EndStaticPreview()、PreviewRenderUtility.Render(bool, bool)、Rect.Rect(float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="width">中文含义：宽度。使用位置：作为 Rect.Rect(float, float, float, float) 的参数。</param>
        /// <param name="height">中文含义：高度。使用位置：作为 Rect.Rect(float, float, float, float) 的参数。</param>
        /// <param name="id">中文含义：编号。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.SetMachinePosition(int) 的参数。</param>
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
