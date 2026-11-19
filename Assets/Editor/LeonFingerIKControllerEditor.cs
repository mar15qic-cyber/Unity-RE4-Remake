#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RootMotion.FinalIK;

/// <summary>编辑器配置与校验使用的共享配置资产 LeonFingerIKControllerEditor；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CustomEditor(typeof(LeonFingerIKController))]
public class LeonFingerIKControllerEditor : Editor
{
    /// <summary>实现基类的 OnInspectorGUI 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;FingerRig&gt;()、Debug.LogWarning(object, Object)、Editor.DrawDefaultInspector()、EditorGUILayout.HelpBox(string, MessageType)、EditorGUILayout.Space(float)、EditorUtility.SetDirty(Object)、GUILayout.Button(string, params GUILayoutOption[]) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox("先在非播放模式点击创建目标，然后在武器层级下逐个拖动手指目标。调整完成后保存当前场景或预制件，运行时会直接读取这些目标。", MessageType.Info);
        // controller（当前里昂手指反向动力学控制器）：使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonFingerIKControllerEditor.MarkFingerRigDirty(LeonFingerIKController) 的参数 等共 6 处。
LeonFingerIKController controller = (LeonFingerIKController)target;
        if (GUILayout.Button("创建并保存全部武器手指目标"))
        {
            Undo.RecordObject(controller, "创建里昂手指目标");
            controller.CreatePersistentWeaponTargets();
            EditorUtility.SetDirty(controller);
            MarkFingerRigDirty(controller);
            MarkOwnerDirty(controller);
        }

        if (GUILayout.Button("重新捕获当前武器目标"))
        {
            Undo.RecordObject(controller, "重新捕获里昂手指目标");
            controller.RebuildCurrentWeaponTargets();
            EditorUtility.SetDirty(controller);
            MarkFingerRigDirty(controller);
            MarkOwnerDirty(controller);
        }

        if (GUILayout.Button("写入当前武器目标到 FingerRig"))
        {
            Undo.RecordObject(controller, "写入里昂手指目标");
            // fingerRig（手指绑定）：保存 Component.GetComponent<FingerRig>() 的返回结果，供当前步骤使用。使用位置：作为 Undo.RecordObject(Object, string) 的参数、LeonFingerIKControllerEditor.OnInspectorGUI()。
FingerRig fingerRig = controller.GetComponent<FingerRig>();
            if (fingerRig != null)
            {
                Undo.RecordObject(fingerRig, "写入里昂手指目标");
            }

            // success（是否成功）：保存 LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig() 的返回结果，供当前步骤使用。使用位置：LeonFingerIKControllerEditor.OnInspectorGUI()。
bool success = controller.WriteCurrentWeaponTargetsToFingerRig();
            if (!success)
            {
                Debug.LogWarning("[里昂手指反向动力学] 当前选择的武器没有完整的持久化手指目标。", controller);
            }

            EditorUtility.SetDirty(controller);
            MarkFingerRigDirty(controller);
            MarkOwnerDirty(controller);
        }
    }

    /// <summary>MarkFingerRigDirty 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;FingerRig&gt;()、EditorUtility.SetDirty(Object)、PrefabUtility.RecordPrefabInstancePropertyModifications(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKControllerEditor.OnInspectorGUI()；下游（含内部回调体） Component.GetComponent&lt;FingerRig&gt;()、EditorUtility.SetDirty(Object)、PrefabUtility.RecordPrefabInstancePropertyModifications(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：当前里昂手指反向动力学控制器。使用位置：LeonFingerIKControllerEditor.MarkFingerRigDirty(LeonFingerIKController)。</param>
    private static void MarkFingerRigDirty(LeonFingerIKController controller)
    {
        if (controller == null)
        {
            return;
        }

        // fingerRig（手指绑定）：保存 Component.GetComponent<FingerRig>() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、LeonFingerIKControllerEditor.MarkFingerRigDirty(LeonFingerIKController)。
FingerRig fingerRig = controller.GetComponent<FingerRig>();
        if (fingerRig != null)
        {
            EditorUtility.SetDirty(fingerRig);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fingerRig);
        }
    }

    /// <summary>MarkOwnerDirty 的业务处理入口；本方法直接依赖 EditorSceneManager.MarkSceneDirty(Scene)、PrefabUtility.RecordPrefabInstancePropertyModifications(Object)、Scene.IsValid()、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKControllerEditor.OnInspectorGUI()；下游（含内部回调体） EditorSceneManager.MarkSceneDirty(Scene)、PrefabUtility.RecordPrefabInstancePropertyModifications(Object)、Scene.IsValid()、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：当前里昂手指反向动力学控制器。使用位置：作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、作为 string.IsNullOrEmpty(string) 的参数 等共 4 处。</param>
    private static void MarkOwnerDirty(LeonFingerIKController controller)
    {
        if (controller == null)
        {
            return;
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        if (controller.gameObject.scene.IsValid() && !string.IsNullOrEmpty(controller.gameObject.scene.path))
        {
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
    }
}
#endif
