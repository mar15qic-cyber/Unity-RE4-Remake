using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 修复 PistolTPPController 重复层问题，删除多余层并重建
/// 菜单路径：Tools / Leon / 清理重复层
/// </summary>
public class FixDuplicateLayers : EditorWindow
{
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 Debug.LogError(object) 的参数。</summary>
    const string CTRL_PATH = "Assets/素材库/动画/PistolAnimsetPro/Mecanim/PistolTPPController.controller";
    /// <summary>编辑器菜单调用入口 ShowWindow；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.GetWindow&lt;FixDuplicateLayers&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Leon/清理重复层")]
    static void ShowWindow()
    {
        GetWindow<FixDuplicateLayers>("清理重复层");
    }

    /// <summary>OnGUI 的业务处理入口；本方法直接依赖 FixDuplicateLayers.RunFix()、GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） FixDuplicateLayers.RunFix()、GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnGUI()
    {
        GUILayout.Label("清理 PistolTPPController 重复层", EditorStyles.boldLabel);
        GUILayout.Label("会删除所有多余层，保留正确的 4 层结构");
        if (GUILayout.Button("执行清理", GUILayout.Height(40)))
        {
            RunFix();
        }
    }

    /// <summary>RunFix 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.SaveAssets()、Debug.Log(object)、Debug.LogError(object)、Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FixDuplicateLayers.OnGUI()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.SaveAssets()、Debug.Log(object)、Debug.LogError(object)、Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static void RunFix()
    {
        // ctrl（角色控制器）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：FixDuplicateLayers.RunFix()。
var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null)
        {
            Debug.LogError("找不到 Controller: " + CTRL_PATH);
            return;
        }

        // layers（图层集合）：保存 IEnumerable<AnimatorControllerLayer>.ToList<AnimatorControllerLayer>() 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object) 的参数、FixDuplicateLayers.RunFix()。
var layers = ctrl.layers.ToList();
        Debug.Log($"当前层数: {layers.Count}");
        // 保留的层名
        // keepOrder（保持顺序）：使用位置：FixDuplicateLayers.RunFix()。
string[] keepOrder = new[]
        {
            "Base Layer",
            "AimOffsets",
            "Firing",
            "ReloadOverride"
        };
        // 重建层列表：每个名只保留第一个出现的
        // newLayers（新图层集合）：使用位置：FixDuplicateLayers.RunFix()。
var newLayers = new List<AnimatorControllerLayer>();
        // seen（已见）：使用位置：FixDuplicateLayers.RunFix()。
var seen = new HashSet<string>();
        // layer（图层）：每轮从图层集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、作为 HashSet<string>.Add(string) 的参数、作为 HashSet<string>.Contains(string) 的参数 等共 5 处。
foreach (var layer in layers)
        {
            if (keepOrder.Contains(layer.name) && !seen.Contains(layer.name))
            {
                newLayers.Add(layer);
                seen.Add(layer.name);
                Debug.Log($"保留层: {layer.name}");
            }
            else
            {
                Debug.Log($"将删除层: {layer.name}");
            }
        }

        // 检查缺失的层
        // name（名称）：每轮从保持顺序取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogWarning(object) 的参数、作为 HashSet<string>.Contains(string) 的参数。
foreach (var name in keepOrder)
        {
            if (!seen.Contains(name))
            {
                Debug.LogWarning($"缺少层: {name}，需要重建");
            }
        }

        // 重新排序为正确顺序
        // orderedLayers（已排序图层集合）：使用位置：作为 Debug.Log(object) 的参数、FixDuplicateLayers.RunFix()。
var orderedLayers = new List<AnimatorControllerLayer>();
        // name（名称）：每轮从保持顺序取得一个元素，仅代表当前这一项。使用位置：作为 IEnumerable<AnimatorControllerLayer>.FirstOrDefault<AnimatorControllerLayer>(Func<AnimatorControllerLayer, bool>) 的参数。
foreach (var name in keepOrder)
        {
            // found（找到的）：保存 IEnumerable<AnimatorControllerLayer>.FirstOrDefault<AnimatorControllerLayer>(Func<AnimatorControllerLayer, bool>) 的返回结果，供当前步骤使用。使用位置：作为 List<AnimatorControllerLayer>.Add(AnimatorControllerLayer) 的参数、FixDuplicateLayers.RunFix()。
var found = newLayers.FirstOrDefault( /* 中文含义：当前动画控制器控制器图层。 */l => l.name == name);
            if (found != null)
            {
                orderedLayers.Add(found);
            }
        }

        // 应用新层列表
        ctrl.layers = orderedLayers.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"层清理完成！当前层数: {orderedLayers.Count}");
        // 检查最终结果
        // l（当前动画控制器控制器图层）：每轮从角色控制器的图层集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数。
foreach (var l in ctrl.layers)
        {
            Debug.Log($"  - {l.name} (weight={l.defaultWeight})");
        }
    }
}
