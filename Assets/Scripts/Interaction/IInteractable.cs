using UnityEngine;

/// <summary>
/// 可交互对象接口 — 统一世界空间交互（拾取、开箱、开门等）
/// 实现此接口并通过 InteractionSystem.Register 注册即可被玩家交互
/// </summary>
public interface IInteractable
{
    /// <summary>中文含义：交互范围。交互距离</summary>
    float InteractRange { get; }

    /// <summary>是否可被交互（冷却、已拾取等条件下返回 false）</summary>
    /// <remarks>静态调用链：上游 InteractionSystem.FindNearby(Transform)、InteractionSystem.InteractNearby(Transform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。</param>
    bool CanInteract(Transform interactor);
    /// <summary>执行交互</summary>
    /// <remarks>静态调用链：上游 InteractionSystem.InteractNearby(Transform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Interact();
    /// <summary>提示文本（行为描述，如"拾取 手枪"，按键由主游戏提示组件统一显示）</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    string GetPromptText();
}
