using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime Animator helper for LeonR4R.
/// Validates and configures the existing AnimatorController.
/// BlendTree/State setup must be done in Unity Editor (see Docs/Animator控制器配置.md).
/// This script handles runtime parameter assignment and clip overrides.
/// </summary>
public class LeonAnimSetup : MonoBehaviour
{
    /// <summary>中文含义：待机。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    [Header("Animation Clips (from PistolAnimsetPro.fbx)")]
    public AnimationClip idle;
    /// <summary>中文含义：行走前向。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip walkForward;
    /// <summary>中文含义：行走后方。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip walkBack;
    /// <summary>中文含义：行走左侧。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip walkLeft;
    /// <summary>中文含义：行走右侧。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip walkRight;
    /// <summary>中文含义：运行前向。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip runForward;
    /// <summary>中文含义：运行后方。</summary>
    public AnimationClip runBack;
    /// <summary>中文含义：运行左侧。</summary>
    public AnimationClip runLeft;
    /// <summary>中文含义：运行右侧。</summary>
    public AnimationClip runRight;
    /// <summary>中文含义：瞄准待机。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip aimIdle;
    /// <summary>中文含义：射击。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip shoot;
    /// <summary>中文含义：换弹。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip reload;
    /// <summary>中文含义：近战。使用位置：作为 LeonAnimSetup.OverrideClip(string, AnimationClip) 的参数、LeonAnimSetup.ApplyOverrides()。</summary>
    public AnimationClip melee;
    /// <summary>中文含义：动画控制器。使用位置：作为 AnimatorOverrideController.AnimatorOverrideController(RuntimeAnimatorController) 的参数、LeonAnimSetup.ApplyOverrides()、LeonAnimSetup.Start()。</summary>
    private Animator anim;
    /// <summary>中文含义：覆盖控制器。使用位置：LeonAnimSetup.ApplyOverrides()、LeonAnimSetup.OverrideClip(string, AnimationClip)。</summary>
    private AnimatorOverrideController overrideController;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Debug.LogError(object)、LeonAnimSetup.ApplyOverrides()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("[LeonAnimSetup] No Animator found!");
            return;
        }

        ApplyOverrides();
    }

    /// <summary>
    /// Creates an AnimatorOverrideController so we can swap clips at runtime
    /// without modifying the original controller asset.
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimSetup.Start()；下游（含内部回调体） AnimatorOverrideController.AnimatorOverrideController(RuntimeAnimatorController)、AnimatorOverrideController.GetOverrides(List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;)、Debug.Log(object)、Debug.LogWarning(object)、LeonAnimSetup.OverrideClip(string, AnimationClip)、List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyOverrides()
    {
        if (anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[LeonAnimSetup] No AnimatorController assigned!");
            return;
        }

        overrideController = new AnimatorOverrideController(anim.runtimeAnimatorController);
        anim.runtimeAnimatorController = overrideController;
        // Map assigned clips to known animation names
        // overrides（覆盖项）：使用位置：作为 AnimatorOverrideController.GetOverrides(List<KeyValuePair<AnimationClip, AnimationClip>>) 的参数、作为 Debug.Log(object) 的参数。
var overrides = new System.Collections.Generic.List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);
        // Try to override clips by name
        if (idle != null)
        {
            OverrideClip("Idle", idle);
        }

        if (walkForward != null)
        {
            OverrideClip("WalkForward", walkForward);
        }

        if (walkBack != null)
        {
            OverrideClip("WalkBack", walkBack);
        }

        if (walkLeft != null)
        {
            OverrideClip("WalkLeft", walkLeft);
        }

        if (walkRight != null)
        {
            OverrideClip("WalkRight", walkRight);
        }

        if (runForward != null)
        {
            OverrideClip("RunForward", runForward);
        }

        if (aimIdle != null)
        {
            OverrideClip("AimIdle", aimIdle);
        }

        if (shoot != null)
        {
            OverrideClip("Shoot", shoot);
        }

        if (reload != null)
        {
            OverrideClip("Reload", reload);
        }

        if (melee != null)
        {
            OverrideClip("Melee", melee);
        }

        Debug.Log($"[LeonAnimSetup] Animator configured with {overrides.Count} clips ready for override.");
    }

    /// <summary>OverrideClip 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimSetup.ApplyOverrides()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 AnimatorOverrideController.this[string] 的参数。</param>
    /// <param name="clip">中文含义：动画片段。使用位置：LeonAnimSetup.OverrideClip(string, AnimationClip)。</param>
    void OverrideClip(string name, AnimationClip clip)
    {
        if (overrideController[name] != null)
        {
            overrideController[name] = clip;
        }
    }
}
