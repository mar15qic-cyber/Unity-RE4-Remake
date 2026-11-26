using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// RE4存档箱控制器：管理箱盖开合动画与玩家交互。
/// 实现 IInteractable 接口，通过 InteractionSystem 统一调度。
/// 交互后加载 StorageBoxScene（Additive），打开寄存箱 UI。
/// </summary>
[RequireComponent(typeof(Collider))]
public class StorageBoxController : MonoBehaviour, IInteractable
{
    /// <summary>动画控制器。</summary>
    [Header("引用")]
    public Animator animator;
    /// <summary>交互范围。</summary>
    [Header("交互")]
    [Min(0.25f)]
    public float interactionRange = 0.9f;
    /// <summary>打开音效。</summary>
    [Header("音效")]
    public string openSfx = "box_open";
    /// <summary>关闭音效。</summary>
    public string closeSfx = "box_close";
    /// <summary>仓库场景名称。</summary>
    [Header("场景")]
    public string storageSceneName = "StorageBoxScene";
    /// <summary>是否打开哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int IsOpenHash = Animator.StringToHash("isOpen");
    /// <summary>是否打开。</summary>
    private bool isOpen = false;
    /// <summary>界面是否打开。</summary>
    private bool uiOpen = false;
    /// <summary>列。</summary>
    private Collider col;
    /// <summary>游戏主摄像机。</summary>
    private Camera gameMainCamera;
    /// <summary>游戏画布。</summary>
    [Header("游戏界面引用")]
    public Canvas gameCanvas;
    /// <summary>打开协程。</summary>
    private Coroutine openCoroutine;
    /// <summary>关闭协程。</summary>
    private Coroutine closeCoroutine;
    /// <summary>是否关闭中。</summary>
    private bool isClosing;
    /// <summary>生效控制器。</summary>
    private static StorageBoxController activeController;
    /// <summary>是否任意箱体忙碌。</summary>
    public static bool IsAnyBoxBusy => activeController != null && (activeController.uiOpen || activeController.isClosing);
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => interactionRange;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;Collider&gt;()、Debug.LogError(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        col = GetComponent<Collider>();
        gameMainCamera = Camera.main;
        if (gameCanvas == null && InventorySceneLoader.Instance != null)
        {
            gameCanvas = InventorySceneLoader.Instance.gameCanvas;
        }

        if (gameCanvas == null)
        {
            Debug.LogError("[寄存箱控制器] 未设置游戏主画布引用，请在检查器中指定。", this);
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Register(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Unregister(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        InteractionSystem.Unregister(this);
        if (activeController == this)
        {
            activeController = null;
            isClosing = false;
            uiOpen = false;
        }
    }

    /// <summary>CanInteract 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。</param>
    internal Vector3 GetInteractionPoint(Vector3 playerPosition)
    {
        if (col == null)
        {
            return transform.position;
        }

        Vector3 probe = new Vector3(playerPosition.x, col.bounds.center.y, playerPosition.z);
        return col.ClosestPoint(probe);
    }

    internal Vector3 PromptPosition
    {
        get
        {
            if (col != null)
            {
                return new Vector3(col.bounds.center.x, col.bounds.max.y + .12f, col.bounds.center.z);
            }
            return transform.position + Vector3.up;
        }
    }

    public bool CanInteract(Transform interactor)
    {
        return interactor != null && Mathf.Abs(interactor.position.y + .65f - PromptPosition.y) <= 1f
            && !uiOpen && !isClosing && (!IsAnyBoxBusy || activeController == this)
            && (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput);
    }

    /// <summary>Interact 的业务处理入口；本方法直接依赖 StorageBoxController.OpenBox()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()；下游（含内部回调体） StorageBoxController.OpenBox()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Interact()
    {
        if (VillageBellSequence.IsPresenting || uiOpen || isClosing || (IsAnyBoxBusy && activeController != this))
        {
            return;
        }

        OpenBox();
    }

    /// <summary>GetPromptText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetPromptText()
    {
        if (uiOpen)
        {
            return "";
        }
        else
        {
            return "打开寄存箱";
        }
    }

    /// <summary>OpenBox 的业务处理入口；本方法直接依赖 Animator.SetBool(int, bool)、AudioManager.PlaySfx(string)、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)、StorageBoxController.OpenUIWhenAnimationDone()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxController.Interact()；下游（含内部回调体） Animator.SetBool(int, bool)、AudioManager.PlaySfx(string)、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)、StorageBoxController.OpenUIWhenAnimationDone()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OpenBox()
    {
        if (isOpen || uiOpen || isClosing)
        {
            return;
        }

        if (IsAnyBoxBusy && activeController != this)
        {
            return;
        }

        activeController = this;
        isOpen = true;
        uiOpen = true;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsOpenHash, true);
        }
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue2 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.PlaySfx(openSfx);
        }

        openCoroutine = StartCoroutine(OpenUIWhenAnimationDone());
    }

    /// <summary>
    /// 等待开盖动画播放到 85% 再加载 UI 场景
    /// </summary>
    /// <remarks>静态调用链：上游 StorageBoxController.OpenBox()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.SetBool(int, bool)、AnimatorStateInfo.IsName(string)、Application.CanStreamedLevelBeLoaded(string)、Debug.LogError(object)、object.ReferenceEquals(object, object)、SceneManager.LoadSceneAsync(string, LoadSceneMode) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator OpenUIWhenAnimationDone()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            float deadline = Time.unscaledTime + 1.6f;
            while (Time.unscaledTime < deadline)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Open") || state.IsName("Opening") && state.normalizedTime >= .85f)
                {
                    break;
                }
                yield return null;
            }
        }

        if (VillageBellSequence.IsPresenting || !Application.CanStreamedLevelBeLoaded(storageSceneName))
        {
            openCoroutine = null;
            CloseBox();
            yield break;
        }

        Scene scene = SceneManager.GetSceneByName(storageSceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            yield return SceneManager.LoadSceneAsync(storageSceneName, LoadSceneMode.Additive);
        }

        openCoroutine = null;
        var storageView = StorageBoxUI.Instance;
        if (VillageBellSequence.IsPresenting || storageView == null || !storageView.TryShow(this))
        {
            CloseBox();
            yield break;
        }
        if (gameMainCamera != null)
        {
            gameMainCamera.enabled = false;
        }
        if (gameCanvas != null)
        {
            gameCanvas.enabled = false;
        }
    }

    /// <summary>关闭寄存箱 UI 并卸载场景（由 StorageBoxUI.Close 调用）</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Close()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、StorageBoxController.CloseRoutine()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CloseBox()
    {
        if (isClosing)
        {
            return;
        }

        uiOpen = false;
        isClosing = true;
        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
        }

        closeCoroutine = StartCoroutine(CloseRoutine());
    }

    /// <summary>CloseRoutine 的业务处理入口；本方法直接依赖 Animator.SetBool(int, bool)、AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)、SceneManager.UnloadSceneAsync(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxController.CloseBox()；下游（含内部回调体） Animator.SetBool(int, bool)、AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)、SceneManager.UnloadSceneAsync(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator CloseRoutine()
    {
        if (gameMainCamera != null)
        {
            gameMainCamera.enabled = true;
        }

        if (gameCanvas != null)
        {
            gameCanvas.enabled = true;
        }

        // op（操作）：保存 SceneManager.UnloadSceneAsync(string) 的返回结果，供当前步骤使用。使用位置：StorageBoxController.CloseRoutine()。
        Scene scene = SceneManager.GetSceneByName(storageSceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        // 先结束动画状态，再释放全局忙碌锁，避免关闭瞬间被背包抢占。
        isOpen = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsOpenHash, false);
        }
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue4 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.PlaySfx(closeSfx);
        }

        isClosing = false;
        if (activeController == this)
        {
            activeController = null;
        }

        closeCoroutine = null;
    }

    /// <summary>中文含义：是否打开。</summary>
    public bool IsOpen => isOpen;
}
