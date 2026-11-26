using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 交互系统 — 统一管理所有 IInteractable 对象的注册与交互调度
/// 使用 Collider.ClosestPoint 计算距离（而非 transform.position），
/// 避免大体积碰撞体的中心点距离误判
/// </summary>
public static class InteractionSystem
{
    /// <summary>注册表。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<IInteractable> registry = new List<IInteractable>();

    internal static void CopyRegistered(List<IInteractable> result)
    {
        result.Clear();
        foreach (IInteractable item in registry)
        {
            if (item is Component component && component != null && component.gameObject.activeInHierarchy)
            {
                result.Add(item);
            }
        }
    }
    /// <summary>Register 的业务处理入口；本方法直接依赖 List&lt;IInteractable&gt;.Add(IInteractable)、List&lt;IInteractable&gt;.Contains(IInteractable)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.OnEnable()、DoorController.OnEnable()、ItemPickup.OnEnable()、StorageBoxController.OnEnable()、WindowTraversalPoint.OnEnable()；下游（含内部回调体） List&lt;IInteractable&gt;.Add(IInteractable)、List&lt;IInteractable&gt;.Contains(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="obj">当前索引可交互。</param>
    public static void Register(IInteractable obj)
    {
        if (!registry.Contains(obj))
        {
            registry.Add(obj);
        }
    }

    /// <summary>Unregister 的业务处理入口；本方法直接依赖 List&lt;IInteractable&gt;.Remove(IInteractable)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.OnDisable()、DoorController.OnDisable()、ItemPickup.OnDisable()、StorageBoxController.OnDisable() 等共 6 处；下游（含内部回调体） List&lt;IInteractable&gt;.Remove(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="obj">当前索引可交互。</param>
    public static void Unregister(IInteractable obj)
    {
        registry.Remove(obj);
    }

    /// <summary>GetDistance 的业务处理入口；本方法直接依赖 Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InteractionSystem.FindNearby(Transform)、InteractionSystem.InteractNearby(Transform)；下游（含内部回调体） Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="obj">当前索引可交互。</param>
    /// <param name="pos">位置。</param>
    static float GetDistance(IInteractable obj, Transform interactor)
    {
        Vector3 pos = interactor.position;
        if (obj is WindowBarricade cabinet)
        {
            return cabinet.InteractionDistance(pos);
        }
        if (obj is DestructibleWoodenBox box)
        {
            return box.GetMeleeDistance(interactor);
        }

        if (obj is VillageEntranceGate gate)
        {
            return Vector3.Distance(Vector3.ProjectOnPlane(gate.PromptPosition - pos, Vector3.up), Vector3.zero);
        }

        if (obj is VillageLadderSite ladder)
        {
            return Vector3.ProjectOnPlane(ladder.PromptPosition - pos, Vector3.up).magnitude;
        }

        if (obj is WindowTraversalPoint window)
        {
            Vector3 delta = window.OpeningCenterWorld - pos;
            delta.y = 0f;
            return delta.magnitude;
        }

        if (obj is StorageBoxController storage)
        {
            Vector3 delta = storage.GetInteractionPoint(pos) - pos;
            delta.y = 0f;
            return delta.magnitude;
        }

        if (obj is ItemPickup pickup && interactor.TryGetComponent<LeonItemPickup>(out _))
        {
            Vector3 delta = pickup.ContactPoint - pos;
            delta.y = 0f;
            return delta.magnitude;
        }

        // 组件。
var comp = obj as Component;
        if (comp == null)
        {
            return float.MaxValue;
        }

        // 列。
var col = comp.GetComponent<Collider>();
        // 最近。
Vector3 closest;
        if (col != null)
        {
            closest = col.ClosestPoint(pos);
        }
        else
        {
            closest = comp.transform.position;
        }

        return Vector3.Distance(closest, pos);
    }

    /// <summary>InteractNearby 的业务处理入口；本方法直接依赖 IInteractable.CanInteract(Transform)、IInteractable.Interact()、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnInteract(InputAction.CallbackContext)；下游（含内部回调体） IInteractable.CanInteract(Transform)、IInteractable.Interact()、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    public static void InteractNearby(Transform interactor)
    {
        var ladder = VillageLadderSite.FindAvailablePush(interactor);
        if (ladder != null)
        {
            ladder.Interact();
            return;
        }

        // 最佳。
IInteractable best = null;
        // 最佳距离。
float bestDist = float.MaxValue;
        // 当前遍历索引。
for (int i = registry.Count - 1; i >= 0; i--)
        {
            // 当前索引可交互。
var obj = registry[i];
            if (obj is null || !((Object)obj))
            {
                registry.RemoveAt(i);
                continue;
            }

            if (!obj.CanInteract(interactor))
            {
                continue;
            }

            // 距离。
float dist = GetDistance(obj, interactor);
            if (dist <= obj.InteractRange && dist < bestDist)
            {
                bestDist = dist;
                best = obj;
            }
        }

        // 判空前缓存的最佳。
global::IInteractable receiverValue2 = best;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Interact();
        }
    }

    /// <summary>FindNearby 的业务处理入口；本方法直接依赖 IInteractable.CanInteract(Transform)、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；下游（含内部回调体） IInteractable.CanInteract(Transform)、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    public static IInteractable FindNearby(Transform interactor, System.Predicate<IInteractable> accept = null)
    {
        var ladder = VillageLadderSite.FindAvailablePush(interactor);
        if (ladder != null && (accept == null || accept(ladder)))
        {
            return ladder;
        }

        // 最佳。
IInteractable best = null;
        // 最佳距离。
float bestDist = float.MaxValue;
        // 当前遍历索引。
for (int i = registry.Count - 1; i >= 0; i--)
        {
            // 当前索引可交互。
var obj = registry[i];
            if (obj is null || !((Object)obj))
            {
                registry.RemoveAt(i);
                continue;
            }

            if (!obj.CanInteract(interactor))
            {
                continue;
            }

            // 距离。
float dist = GetDistance(obj, interactor);
            if (dist <= obj.InteractRange && dist < bestDist && (accept == null || accept(obj)))
            {
                bestDist = dist;
                best = obj;
            }
        }

        return best;
    }
}
