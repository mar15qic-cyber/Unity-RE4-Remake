#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 教学系统开发期工具，方便反复实测首次触发流程。
/// </summary>
public static class TutorialProgressEditorUtility
{
    /// <summary>编辑器菜单调用入口 ResetTutorialProgress；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、TutorialManager.ResetSavedProgress()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/教学/重置教学进度")]
    private static void ResetTutorialProgress()
    {
        TutorialManager.ResetSavedProgress();
        Debug.Log("[教学] 已重置移动、举枪射击、换弹教学进度。");
    }

    /// <summary>编辑器菜单调用入口 PrintTutorialState；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、Object.FindObjectsByType&lt;TutorialManager&gt;(FindObjectsInactive, FindObjectsSortMode)、Object.FindObjectsByType&lt;TutorialTrigger&gt;(FindObjectsInactive, FindObjectsSortMode)、PlayerPrefs.GetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/教学/打印教学链路")]
    private static void PrintTutorialState()
    {
        Debug.Log("[教学] 移动=" + PlayerPrefs.GetInt("教学_移动_完成", 0) + "；举枪射击=" + PlayerPrefs.GetInt("教学_举枪射击_完成", 0) + "；换弹=" + PlayerPrefs.GetInt("教学_换弹_完成", 0) + "；管理器=" + Object.FindObjectsByType<TutorialManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length + "；触发器=" + Object.FindObjectsByType<TutorialTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
    }
}
#endif
