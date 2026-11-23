using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 交互系统 — 统一管理所有 IInteractable 对象的注册与交互调度
/// 使用 Collider.ClosestPoint 计算距离（而非 transform.position），
/// 避免大体积碰撞体的中心点距离误判
/// </summary>
public static class InteractionSystem
{
    /// <summary>中文含义：注册表。只读字段，初始化后不由普通写入口替换。使用位置：InteractionSystem.FindNearby(Transform)、InteractionSystem.InteractNearby(Transform)、InteractionSystem.Register(IInteractable) 等共 4 处。</summary>
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
    /// <param name="obj">中文含义：当前索引可交互。使用位置：作为 List&lt;IInteractable&gt;.Add(IInteractable) 的参数、作为 List&lt;IInteractable&gt;.Contains(IInteractable) 的参数。</param>
    public static void Register(IInteractable obj)
    {
        if (!registry.Contains(obj))
        {
            registry.Add(obj);
        }
    }

    /// <summary>Unregister 的业务处理入口；本方法直接依赖 List&lt;IInteractable&gt;.Remove(IInteractable)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.OnDisable()、DoorController.OnDisable()、ItemPickup.OnDisable()、StorageBoxController.OnDisable() 等共 6 处；下游（含内部回调体） List&lt;IInteractable&gt;.Remove(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="obj">中文含义：当前索引可交互。使用位置：作为 List&lt;IInteractable&gt;.Remove(IInteractable) 的参数。</param>
    public static void Unregister(IInteractable obj)
    {
        registry.Remove(obj);
    }

    /// <summary>GetDistance 的业务处理入口；本方法直接依赖 Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InteractionSystem.FindNearby(Transform)、InteractionSystem.InteractNearby(Transform)；下游（含内部回调体） Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="obj">中文含义：当前索引可交互。使用位置：InteractionSystem.GetDistance(IInteractable, Vector3)。</param>
    /// <param name="pos">中文含义：位置。使用位置：作为 Collider.ClosestPoint(Vector3) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数。</param>
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

        // comp（组件）：使用位置：InteractionSystem.GetDistance(IInteractable, Vector3)。
var comp = obj as Component;
        if (comp == null)
        {
            return float.MaxValue;
        }

        // col（列）：保存 Component.GetComponent<Collider>() 的返回结果，供当前步骤使用。使用位置：InteractionSystem.GetDistance(IInteractable, Vector3)。
var col = comp.GetComponent<Collider>();
        // closest（最近）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、InteractionSystem.GetDistance(IInteractable, Vector3)。
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 IInteractable.CanInteract(Transform) 的参数、作为 InteractionSystem.GetDistance(IInteractable, Vector3) 的参数。</param>
    public static void InteractNearby(Transform interactor)
    {
        var ladder = VillageLadderSite.FindAvailablePush(interactor);
        if (ladder != null)
        {
            ladder.Interact();
            return;
        }

        // best（最佳）：使用位置：InteractionSystem.InteractNearby(Transform)。
IInteractable best = null;
        // bestDist（最佳距离）：使用位置：InteractionSystem.InteractNearby(Transform)。
float bestDist = float.MaxValue;
        // i（当前遍历索引）：使用位置：作为 List<IInteractable>.RemoveAt(int) 的参数、作为 List<IInteractable>.this[int] 的参数、InteractionSystem.InteractNearby(Transform)。
for (int i = registry.Count - 1; i >= 0; i--)
        {
            // obj（当前索引可交互）：使用位置：作为 InteractionSystem.GetDistance(IInteractable, Vector3) 的参数、InteractionSystem.InteractNearby(Transform)。
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

            // dist（距离）：保存 InteractionSystem.GetDistance(IInteractable, Vector3) 的返回结果，供当前步骤使用。使用位置：InteractionSystem.InteractNearby(Transform)。
float dist = GetDistance(obj, interactor);
            if (dist <= obj.InteractRange && dist < bestDist)
            {
                bestDist = dist;
                best = obj;
            }
        }

        // receiverValue2（判空前缓存的最佳）：从最佳读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InteractionSystem.InteractNearby(Transform)。
global::IInteractable receiverValue2 = best;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Interact();
        }
    }

    /// <summary>FindNearby 的业务处理入口；本方法直接依赖 IInteractable.CanInteract(Transform)、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；下游（含内部回调体） IInteractable.CanInteract(Transform)、InteractionSystem.GetDistance(IInteractable, Vector3)、List&lt;IInteractable&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。使用位置：作为 IInteractable.CanInteract(Transform) 的参数、作为 InteractionSystem.GetDistance(IInteractable, Vector3) 的参数。</param>
    public static IInteractable FindNearby(Transform interactor, System.Predicate<IInteractable> accept = null)
    {
        var ladder = VillageLadderSite.FindAvailablePush(interactor);
        if (ladder != null && (accept == null || accept(ladder)))
        {
            return ladder;
        }

        // best（最佳）：使用位置：InteractionSystem.FindNearby(Transform)。
IInteractable best = null;
        // bestDist（最佳距离）：使用位置：InteractionSystem.FindNearby(Transform)。
float bestDist = float.MaxValue;
        // i（当前遍历索引）：使用位置：作为 List<IInteractable>.RemoveAt(int) 的参数、作为 List<IInteractable>.this[int] 的参数、InteractionSystem.FindNearby(Transform)。
for (int i = registry.Count - 1; i >= 0; i--)
        {
            // obj（当前索引可交互）：使用位置：作为 InteractionSystem.GetDistance(IInteractable, Vector3) 的参数、InteractionSystem.FindNearby(Transform)。
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

            // dist（距离）：保存 InteractionSystem.GetDistance(IInteractable, Vector3) 的返回结果，供当前步骤使用。使用位置：InteractionSystem.FindNearby(Transform)。
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
