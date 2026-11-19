using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 只覆盖快速反应事件四个动作片段的运行时作用域。
/// 不修改正式动画控制器资源，结束时恢复原控制器引用。
/// </summary>
public sealed class QTEAnimatorOverrideScope
{
    /// <summary>中文含义：动画控制器。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)、QTEAnimatorOverrideScope.Clear()。</summary>
    private Animator animator;
    /// <summary>中文含义：原始控制器。使用位置：作为 AnimatorOverrideController.AnimatorOverrideController(RuntimeAnimatorController) 的参数、QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)、QTEAnimatorOverrideScope.Clear()。</summary>
    private RuntimeAnimatorController originalController;
    /// <summary>中文含义：覆盖控制器。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)、QTEAnimatorOverrideScope.Clear()。</summary>
    private AnimatorOverrideController overrideController;
    /// <summary>中文含义：是否已应用。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)、QTEAnimatorOverrideScope.Clear()。</summary>
    private bool applied;
    /// <summary>Apply 的业务处理入口；本方法直接依赖 AnimatorOverrideController.AnimatorOverrideController(RuntimeAnimatorController)、AnimatorOverrideController.GetOverrides(List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;)、List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;.List()、QTEAnimatorOverrideScope.Clear()、QTEAnimatorOverrideScope.IsEnterClip(string)、QTEAnimatorOverrideScope.IsFailureClip(string)、QTEAnimatorOverrideScope.IsHoldClip(string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)、VillagerQTEParticipant.ApplyAnimationVariant(QTEAnimationVariant)；下游（含内部回调体） AnimatorOverrideController.AnimatorOverrideController(RuntimeAnimatorController)、AnimatorOverrideController.GetOverrides(List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;)、List&lt;KeyValuePair&lt;AnimationClip, AnimationClip&gt; &gt;.List()、QTEAnimatorOverrideScope.Clear()、QTEAnimatorOverrideScope.IsEnterClip(string)、QTEAnimatorOverrideScope.IsFailureClip(string)、QTEAnimatorOverrideScope.IsHoldClip(string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标动画控制器。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="enter">中文含义：进入。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="hold">中文含义：保持。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="success">中文含义：成功。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="failure">中文含义：失败。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    public bool Apply(Animator target, AnimationClip enter, AnimationClip hold, AnimationClip success, AnimationClip failure)
    {
        Clear();
        if (target == null || target.runtimeAnimatorController == null || enter == null || hold == null || success == null || failure == null)
        {
            return false;
        }

        animator = target;
        originalController = target.runtimeAnimatorController;
        // overrides（覆盖项）：使用位置：作为 AnimatorOverrideController.GetOverrides(List<KeyValuePair<AnimationClip, AnimationClip>>) 的参数、QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        if (originalController is AnimatorOverrideController inherited)
        {
            overrideController = new AnimatorOverrideController(inherited.runtimeAnimatorController);
            inherited.GetOverrides(overrides);
            overrideController.ApplyOverrides(overrides);
        }
        else
        {
            overrideController = new AnimatorOverrideController(originalController);
            overrideController.GetOverrides(overrides);
        }
        // enterFound（进入是否已找到）：使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
bool enterFound = false;
        // holdFound（保持是否已找到）：使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
bool holdFound = false;
        // successFound（成功是否已找到）：使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
bool successFound = false;
        // failureFound（失败是否已找到）：使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
bool failureFound = false;
        // pair（当前键值配对）：每轮从覆盖项取得一个元素，仅代表当前这一项。使用位置：QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
foreach (var pair in overrides)
        {
            // original（原始）：使用位置：作为 AnimatorOverrideController.this[AnimationClip] 的参数、QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
AnimationClip original = pair.Key;
            if (original == null)
            {
                continue;
            }

            // name（名称）：保存 string.ToLowerInvariant() 的返回结果，供当前步骤使用。使用位置：作为 QTEAnimatorOverrideScope.IsEnterClip(string) 的参数、作为 QTEAnimatorOverrideScope.IsFailureClip(string) 的参数、作为 QTEAnimatorOverrideScope.IsHoldClip(string) 的参数 等共 4 处。
            // 变体替换四个基础抓取占位片段。枪械动画也有 Start、Hold 和 End 后缀。
            string name = original.name;
            if (!name.Contains("_正面抓取_"))
            {
                continue;
            }
            if (!enterFound && IsEnterClip(name))
            {
                overrideController[original] = enter;
                enterFound = true;
            }
            else if (!holdFound && IsHoldClip(name))
            {
                overrideController[original] = hold;
                holdFound = true;
            }
            else if (!successFound && IsSuccessClip(name))
            {
                overrideController[original] = success;
                successFound = true;
            }
            else if (!failureFound && IsFailureClip(name))
            {
                overrideController[original] = failure;
                failureFound = true;
            }
        }

        if (!enterFound || !holdFound || !successFound || !failureFound)
        {
            Clear();
            return false;
        }

        target.runtimeAnimatorController = overrideController;
        applied = true;
        return true;
    }

    /// <summary>Clear 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.TryClearQTEAnimationVariantAfterFullBodyExit()、QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)、VillagerQTEParticipant.ClearAnimationVariant()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Clear()
    {
        if (animator != null && originalController != null && applied)
        {
            animator.runtimeAnimatorController = originalController;
        }

        if (overrideController != null)
        {
            UnityEngine.Object.Destroy(overrideController);
        }
        animator = null;
        originalController = null;
        overrideController = null;
        applied = false;
    }

    /// <summary>IsEnterClip 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：QTEAnimatorOverrideScope.IsEnterClip(string)。</param>
    private static bool IsEnterClip(string name)
    {
        return name.EndsWith("_进入", System.StringComparison.Ordinal);
    }

    /// <summary>IsHoldClip 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：QTEAnimatorOverrideScope.IsHoldClip(string)。</param>
    private static bool IsHoldClip(string name)
    {
        return name.EndsWith("_挣扎", System.StringComparison.Ordinal);
    }

    /// <summary>IsSuccessClip 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：QTEAnimatorOverrideScope.IsSuccessClip(string)。</param>
    private static bool IsSuccessClip(string name)
    {
        return name.EndsWith("推开_原地驱动", System.StringComparison.Ordinal);
    }

    /// <summary>IsFailureClip 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：QTEAnimatorOverrideScope.IsFailureClip(string)。</param>
    private static bool IsFailureClip(string name)
    {
        return name.EndsWith("_咬颈_原地驱动", System.StringComparison.Ordinal);
    }
}
