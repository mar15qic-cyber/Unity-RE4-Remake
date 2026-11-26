using UnityEngine;

/// <summary>
/// 教学提示总控，只订阅事件，不在每帧检查玩家状态。
/// </summary>
public class TutorialManager : Singleton<TutorialManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>提示界面。</summary>
    [Header("界面引用")]
    public TutorialPromptUI promptUI;
    /// <summary>输入桥接。</summary>
    [Header("玩家引用")]
    public LeonInputBridge inputBridge;
    /// <summary>管理当前装备与武器切换的组件。</summary>
    public LeonWeaponHandler weaponHandler;
    /// <summary>移动完成可见秒。</summary>
    [Header("显示时长")]
    [Min(0.1f)]
    public float moveCompleteVisibleSeconds = 1.5f;
    /// <summary>瞄准射击可见秒。</summary>
    [Min(0.1f)]
    public float aimShootVisibleSeconds = 4f;
    /// <summary>换弹可见秒。</summary>
    [Min(0.1f)]
    public float reloadVisibleSeconds = 3.5f;
    /// <summary>是否重置进度事件起始。</summary>
    [Header("调试")]
    public bool resetProgressOnStart;
    /// <summary>移动教学键。编译期常量，保持既有值与消费者映射。</summary>
    private const string MoveTutorialKey = "教学_移动_完成";
    /// <summary>瞄准射击教学键。编译期常量，保持既有值与消费者映射。</summary>
    private const string AimShootTutorialKey = "教学_举枪射击_完成";
    /// <summary>换弹教学键。编译期常量，保持既有值与消费者映射。</summary>
    private const string ReloadTutorialKey = "教学_换弹_完成";
    /// <summary>已绑定输入桥接。</summary>
    private LeonInputBridge boundInputBridge;
    /// <summary>已绑定装备切换组件。</summary>
    private LeonWeaponHandler boundWeaponHandler;
    /// <summary>AutoWireMissingReferences 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、SceneStartupEntry.QueryHasHasReference&lt;LeonInputBridge&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;TutorialPromptUI&gt;(Component)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、SceneStartupEntry.QueryHasHasReference&lt;LeonInputBridge&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;TutorialPromptUI&gt;(Component)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [System.Obsolete("请在场景中显式绑定教学引用")]
    public void AutoWireMissingReferences()
    {
        if (promptUI == null)
        {
            promptUI = SceneStartupEntry.QueryHasHasReference<TutorialPromptUI>(this);
        }

        if (inputBridge == null)
        {
            inputBridge = SceneStartupEntry.QueryHasHasReference<LeonInputBridge>(this);
        }

        if (weaponHandler == null && inputBridge != null)
        {
            weaponHandler = inputBridge.GetComponent<LeonWeaponHandler>();
        }
    }

    /// <summary>ConnectTutorial 的业务处理入口；本方法直接依赖 TutorialManager.BindEvents()、TutorialManager.ResetTutorialProgress()、TutorialManager.TryShowMoveTutorial()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） TutorialManager.BindEvents()、TutorialManager.ResetTutorialProgress()、TutorialManager.TryShowMoveTutorial()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ConnectTutorial()
    {
        if (resetProgressOnStart)
        {
            ResetTutorialProgress();
        }

        BindEvents();
        TryShowMoveTutorial();
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Singleton&lt;TutorialManager&gt;.OnDestroy()、TutorialManager.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        UnbindEvents();
        base.OnDestroy();
    }

    /// <summary>TryShowMoveTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.ConnectTutorial()；下游（含内部回调体） TutorialManager.IsCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryShowMoveTutorial()
    {
        if (IsCompleted(MoveTutorialKey))
        {
            return false;
        }

        if (promptUI == null)
        {
            return false;
        }

        promptUI.ShowPrompt(new[] { "W", "A", "S", "D" }, "移动", -1f, false);
        return true;
    }

    /// <summary>CompleteMoveTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.HideAfter(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.HandlePlayerMoved()；下游（含内部回调体） TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.HideAfter(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteMoveTutorial()
    {
        if (IsCompleted(MoveTutorialKey))
        {
            return;
        }

        MarkCompleted(MoveTutorialKey);
        if (promptUI != null)
        {
            promptUI.HideAfter(moveCompleteVisibleSeconds);
        }
    }

    /// <summary>TryShowAimShootTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialTrigger.OnTriggerEnter(Collider)；下游（含内部回调体） TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryShowAimShootTutorial()
    {
        if (IsCompleted(AimShootTutorialKey))
        {
            return false;
        }

        if (promptUI == null)
        {
            return false;
        }

        MarkCompleted(AimShootTutorialKey);
        promptUI.ShowPrompt(new[] { "右键", "左键" }, "举枪 / 射击", aimShootVisibleSeconds, true);
        return true;
    }

    /// <summary>TryShowReloadTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.HandleCurrentWeaponLowAmmo(WeaponType, int, int)；下游（含内部回调体） TutorialManager.IsCompleted(string)、TutorialManager.MarkCompleted(string)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryShowReloadTutorial()
    {
        if (IsCompleted(ReloadTutorialKey))
        {
            return false;
        }

        if (promptUI == null)
        {
            return false;
        }

        MarkCompleted(ReloadTutorialKey);
        promptUI.ShowPrompt(new[] { "R" }, "换弹", reloadVisibleSeconds, false);
        return true;
    }

    /// <summary>HasCompletedMoveTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.IsCompleted(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HasCompletedMoveTutorial()
    {
        return IsCompleted(MoveTutorialKey);
    }

    /// <summary>HasCompletedAimShootTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.IsCompleted(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HasCompletedAimShootTutorial()
    {
        return IsCompleted(AimShootTutorialKey);
    }

    /// <summary>HasCompletedReloadTutorial 的业务处理入口；本方法直接依赖 TutorialManager.IsCompleted(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.IsCompleted(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HasCompletedReloadTutorial()
    {
        return IsCompleted(ReloadTutorialKey);
    }

    /// <summary>ResetTutorialProgress 的业务处理入口；本方法直接依赖 TutorialManager.ResetSavedProgress()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.ConnectTutorial()；下游（含内部回调体） TutorialManager.ResetSavedProgress()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("重置教学进度")]
    public void ResetTutorialProgress()
    {
        ResetSavedProgress();
    }

    /// <summary>ResetSavedProgress 的业务处理入口；本方法直接依赖 PlayerPrefs.DeleteKey(string)、PlayerPrefs.Save()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.ResetTutorialProgress()、TutorialProgressEditorUtility.ResetTutorialProgress()；下游（含内部回调体） PlayerPrefs.DeleteKey(string)、PlayerPrefs.Save()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void ResetSavedProgress()
    {
        PlayerPrefs.DeleteKey(MoveTutorialKey);
        PlayerPrefs.DeleteKey(AimShootTutorialKey);
        PlayerPrefs.DeleteKey(ReloadTutorialKey);
        PlayerPrefs.Save();
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.ConnectTutorial()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (boundInputBridge != inputBridge)
        {
            if (boundInputBridge != null)
            {
                boundInputBridge.OnPlayerMoved -= HandlePlayerMoved;
            }

            boundInputBridge = inputBridge;
            if (boundInputBridge != null)
            {
                boundInputBridge.OnPlayerMoved += HandlePlayerMoved;
            }
        }

        if (boundWeaponHandler != weaponHandler)
        {
            if (boundWeaponHandler != null)
            {
                boundWeaponHandler.OnCurrentWeaponLowAmmo -= HandleCurrentWeaponLowAmmo;
            }

            boundWeaponHandler = weaponHandler;
            if (boundWeaponHandler != null)
            {
                boundWeaponHandler.OnCurrentWeaponLowAmmo += HandleCurrentWeaponLowAmmo;
            }
        }
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.OnDestroy()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (boundInputBridge != null)
        {
            boundInputBridge.OnPlayerMoved -= HandlePlayerMoved;
        }

        if (boundWeaponHandler != null)
        {
            boundWeaponHandler.OnCurrentWeaponLowAmmo -= HandleCurrentWeaponLowAmmo;
        }

        boundInputBridge = null;
        boundWeaponHandler = null;
    }

    /// <summary>HandlePlayerMoved 的业务处理入口；本方法直接依赖 TutorialManager.CompleteMoveTutorial()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.CompleteMoveTutorial()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandlePlayerMoved()
    {
        CompleteMoveTutorial();
    }

    /// <summary>HandleCurrentWeaponLowAmmo 的业务处理入口；本方法直接依赖 TutorialManager.TryShowReloadTutorial()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialManager.TryShowReloadTutorial()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    /// <param name="currentAmmo">中文含义：当前弹匣弹量。</param>
    /// <param name="magazineSize">中文含义：弹匣尺寸。</param>
    private void HandleCurrentWeaponLowAmmo(WeaponType weaponType, int currentAmmo, int magazineSize)
    {
        if (weaponType == WeaponType.Knife)
        {
            return;
        }

        TryShowReloadTutorial();
    }

    /// <summary>IsCompleted 的业务处理入口；本方法直接依赖 PlayerPrefs.GetInt(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.CompleteMoveTutorial()、TutorialManager.HasCompletedAimShootTutorial()、TutorialManager.HasCompletedMoveTutorial()、TutorialManager.HasCompletedReloadTutorial()、TutorialManager.TryShowAimShootTutorial() 等共 7 处；下游（含内部回调体） PlayerPrefs.GetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    private static bool IsCompleted(string key)
    {
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    /// <summary>MarkCompleted 的业务处理入口；本方法直接依赖 PlayerPrefs.Save()、PlayerPrefs.SetInt(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.CompleteMoveTutorial()、TutorialManager.TryShowAimShootTutorial()、TutorialManager.TryShowReloadTutorial()；下游（含内部回调体） PlayerPrefs.Save()、PlayerPrefs.SetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    private static void MarkCompleted(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }
}
