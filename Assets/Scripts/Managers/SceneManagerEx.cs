using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景管理器（扩展版）：统一管理场景加载、异步切换、重新加载、退出游戏
/// 封装Unity原生SceneManager，提供项目级场景管理接口
/// </summary>
public class SceneManagerEx : Singleton<SceneManagerEx>
{
#region ===== 场景名称常量（按项目实际场景维护） =====
    /// <summary>中文含义：场景。主游戏场景名称</summary>
    public const string MAIN_SCENE = "VillageBattleLighting";
    public const string MainScenePath = "Assets/RE4ChapterOneVillage/Lighting/" + MAIN_SCENE + ".unity";
#endregion
#region ===== 属性 =====
    /// <summary>中文含义：当前场景名称。当前活动场景名称</summary>
    public string CurrentSceneName
    {
        get
        {
            return SceneManager.GetActiveScene().name;
        }
    }

    /// <summary>中文含义：是否加载。当前是否正在加载场景（异步加载中）</summary>
    public bool IsLoading { get; private set; }
    /// <summary>中文含义：加载进度。异步加载进度（0~1），仅在IsLoading为true时有意义</summary>
    public float LoadingProgress { get; private set; }

#endregion
#region ===== 场景加载 =====
    /// <summary>
    /// 同步加载场景（会卡帧，仅用于小场景或加载画面）
    /// </summary>
    /// <param name="sceneName">中文含义：场景名称。目标场景名称</param>
    /// <remarks>静态调用链：上游 SceneManagerEx.LoadMainScene()、SceneManagerEx.ReloadCurrentScene()；下游（含内部回调体） SceneManager.LoadScene(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 异步加载场景（推荐，不卡帧）
    /// </summary>
    /// <param name="sceneName">中文含义：场景名称。目标场景名称</param>
    /// <remarks>静态调用链：上游 SceneManagerEx.ReloadCurrentSceneAsync()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、SceneManagerEx.LoadSceneCoroutine(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>
    /// 异步加载场景的协程实现
    /// </summary>
    /// <remarks>静态调用链：上游 SceneManagerEx.LoadSceneAsync(string)；下游（含内部回调体） Mathf.Clamp01(float)、SceneManager.LoadSceneAsync(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sceneName">中文含义：场景名称。使用位置：作为 SceneManager.LoadSceneAsync(string) 的参数。</param>
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        IsLoading = true;
        LoadingProgress = 0f;
        // asyncOp（异步操作）：保存 SceneManager.LoadSceneAsync(string) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数、SceneManagerEx.LoadSceneCoroutine(string)。
AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp != null)
        {
            // 加载完成前不允许自动切换（可在此显示加载进度条）
            asyncOp.allowSceneActivation = true;
            while (!asyncOp.isDone)
            {
                // Unity的progress在0.9时即表示加载完成，等待allowSceneActivation
                LoadingProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                yield return null;
            }
        }

        IsLoading = false;
        LoadingProgress = 1f;
    }

#endregion
#region ===== 便捷方法 =====
    /// <summary>
    /// 重新加载当前场景（常用于玩家死亡后重来）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SceneManagerEx.LoadScene(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReloadCurrentScene()
    {
        LoadScene(CurrentSceneName);
    }

    /// <summary>
    /// 异步重新加载当前场景
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SceneManagerEx.LoadSceneAsync(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReloadCurrentSceneAsync()
    {
        LoadSceneAsync(CurrentSceneName);
    }

    /// <summary>
    /// 加载主游戏场景
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SceneManagerEx.LoadScene(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void LoadMainScene()
    {
        LoadScene(MainScenePath);
    }

#endregion
#region ===== 退出游戏 =====
    /// <summary>
    /// 退出游戏（编辑器中停止运行，构建后退出应用）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
#endregion
}
