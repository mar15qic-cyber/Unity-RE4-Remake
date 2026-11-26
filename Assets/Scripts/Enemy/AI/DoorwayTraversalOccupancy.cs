using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>每扇门独立持有双向队列和穿门方向锁，保留首次按距离插入的原规则。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "门洞通行占用")]
internal sealed class DoorwayTraversalOccupancy
{
    /// <summary>门。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly DoorController door;
    /// <summary>正向方向。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<VillagerController> positiveDirection = new();
    /// <summary>反向。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<VillagerController> reverse = new();
    /// <summary>正向方向视图。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly ReadOnlyCollection<VillagerController> positiveDirectionView;
    /// <summary>反向视图。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly ReadOnlyCollection<VillagerController> reverseView;
    /// <summary>占用方向。</summary>
    private int occupancyDirection;
    /// <summary>租用。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly GameArchitecture.TraversalTokenRegistry lease = new GameArchitecture.TraversalTokenRegistry();
    /// <summary>成员凭证。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<VillagerController, GameArchitecture.TraversalToken> memberToken = new();
    /// <summary>OpenRelease 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.ClearValue()、Object.GetInstanceID()、TraversalTokenRegistry.OpenRelease(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.OnEnable()；下游（含内部回调体） DoorwayTraversalOccupancy.ClearValue()、Object.GetInstanceID()、TraversalTokenRegistry.OpenRelease(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void OpenRelease()
    {
        ClearValue();
        lease.OpenRelease(door.GetInstanceID());
    }

    /// <summary>QueryToken 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, TraversalToken&gt;.TryGetValue(VillagerController, out TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)、VillagerController.UnregisterPursuitIngressWaiter()；下游（含内部回调体） Dictionary&lt;VillagerController, TraversalToken&gt;.TryGetValue(VillagerController, out TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    internal GameArchitecture.TraversalToken QueryToken(VillagerController character)
    {
        if (memberToken.TryGetValue(character, out var /* 中文含义：凭证。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ token))
        {
            return token;
        }
        else
        {
            return default;
        }
    }

    /// <summary>TraversalValid 的业务处理入口；本方法直接依赖 TraversalTokenRegistry.Valid(TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.TraversalValid(TraversalToken)；下游（含内部回调体） TraversalTokenRegistry.Valid(TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    internal bool TraversalValid(GameArchitecture.TraversalToken token) => lease.Valid(token);
    /// <summary>ReleaseTraversal 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.Release(VillagerController)、object.ReferenceEquals(object, object)、TraversalTokenRegistry.Valid(TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.ReleaseTraversal(TraversalToken, ReleaseReason)；下游（含内部回调体） DoorwayTraversalOccupancy.Release(VillagerController)、object.ReferenceEquals(object, object)、TraversalTokenRegistry.Valid(TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="reason">中文含义：原因。</param>
    internal bool ReleaseTraversal(GameArchitecture.TraversalToken token, GameArchitecture.ReleaseReason reason)
    {
        if (!lease.Valid(token))
        {
            return false;
        }

        // 目标村民敌人控制器。
VillagerController target = null;
        // item（当前键值配对）：每轮从成员凭证取得一个元素，仅代表当前这一项。使用位置：DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)。
foreach (var item in memberToken)
        {
            if (item.Value.Action == token.Action && item.Value.OccupancyId == token.OccupancyId)
            {
                target = item.Key;
                break;
            }
        }

        if (ReferenceEquals(target, null))
        {
            return false;
        }

        Release(target);
        return true;
    }

    /// <summary>ReleaseLease 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, TraversalToken&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, TraversalToken&gt;.TryGetValue(VillagerController, out TraversalToken)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.Release(VillagerController)；下游（含内部回调体） Dictionary&lt;VillagerController, TraversalToken&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, TraversalToken&gt;.TryGetValue(VillagerController, out TraversalToken)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    private void ReleaseLease(VillagerController character)
    {
        if (memberToken.TryGetValue(character, out var /* 中文含义：凭证。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ token))
        {
            lease.Release(token, GameArchitecture.ReleaseReason.Cancel);
        }

        memberToken.Remove(character);
    }

    /// <summary>构造并初始化 DoorwayTraversalOccupancy 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 DoorController.OnEnable()；下游（含内部回调体） List&lt;VillagerController&gt;.AsReadOnly()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">门。</param>
    internal DoorwayTraversalOccupancy(DoorController door)
    {
        this.door = door;
        positiveDirectionView = positiveDirection.AsReadOnly();
        reverseView = reverse.AsReadOnly();
    }

    /// <summary>ReadQueue 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.Register(VillagerController, int)、VillagerController.IsPursuitIngressQueueHead()、VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    internal ReadOnlyCollection<VillagerController> ReadQueue(int direction)
    {
        if (direction >= 0)
        {
            return positiveDirectionView;
        }
        else
        {
            return reverseView;
        }
    }

    /// <summary>Queue 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.QueueFirstYieldRow(VillagerController, int)、DoorwayTraversalOccupancy.Register(VillagerController, int)、DoorwayTraversalOccupancy.TryOccupancyCore(VillagerController, int)、DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    private List<VillagerController> Queue(int direction)
    {
        if (direction >= 0)
        {
            return positiveDirection;
        }
        else
        {
            return reverse;
        }
    }

    /// <summary>Register 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.HorizontalDistance(Vector3, Vector3)、DoorwayTraversalOccupancy.Queue(int)、DoorwayTraversalOccupancy.ReadQueue(int)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RegisterPursuitIngressWaiter(DoorController)；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.HorizontalDistance(Vector3, Vector3)、DoorwayTraversalOccupancy.Queue(int)、DoorwayTraversalOccupancy.ReadQueue(int)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    internal ReadOnlyCollection<VillagerController> Register(VillagerController character, int direction)
    {
        CleanupInvalidate();
        // 等待。
var wait = Queue(direction);
        if (!wait.Contains(character))
        {
            // 距离。
float distance = HorizontalDistance(character.transform.position, door.PassageCenter);
            // 位置值。
int positionValue = wait.Count;
            // 当前遍历索引。
for (int i = 0; i < wait.Count; i++)
            {
                if (wait[i] != null && distance < HorizontalDistance(wait[i].transform.position, door.PassageCenter))
                {
                    positionValue = i;
                    break;
                }
            }

            wait.Insert(positionValue, character);
        }

        return ReadQueue(direction);
    }

    /// <summary>TryPromoteForwardBlocker 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.Queue(int)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryPromoteBlockingPursuitIngressWaiter(ReadOnlyCollection&lt;VillagerController&gt;)；下游（含内部回调体） DoorwayTraversalOccupancy.Queue(int)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="blocker">阻挡者。</param>
    /// <param name="direction">方向。</param>
    internal bool TryPromoteForwardBlocker(VillagerController character, VillagerController blocker, int direction)
    {
        // 等待。
var wait = Queue(direction);
        if (wait.Count < 2 || wait[0] != character || blocker == null || blocker == character || !wait.Contains(blocker) || blocker.DoorwayTraverseMiddle || blocker.CurrentDoorway != door || blocker.DoorwaySourceSide != direction)
        {
            return false;
        }

        // 自身深度。
float selfDepth = Vector3.Dot(character.transform.position - door.PassageCenter, door.PassageNormal) * direction;
        // 阻挡者深度。
float blockerDepth = Vector3.Dot(blocker.transform.position - door.PassageCenter, door.PassageNormal) * direction;
        if (blockerDepth < -0.08f || blockerDepth + 0.1f >= selfDepth)
        {
            return false;
        }

        wait.Remove(blocker);
        wait.Insert(0, blocker);
        return true;
    }

    /// <summary>QueueFirstYieldRow 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.Queue(int)、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.YieldPursuitIngressQueueHead(DoorController)；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.Queue(int)、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    internal void QueueFirstYieldRow(VillagerController character, int direction)
    {
        CleanupInvalidate();
        // 等待。
var wait = Queue(direction);
        if (wait.Count <= 1 || wait[0] != character)
        {
            return;
        }

        wait.RemoveAt(0);
        wait.Add(character);
    }

    /// <summary>DirectionAvailable 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupDirection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.TryOccupancyCore(VillagerController, int)、VillagerController.IsPursuitIngressDirectionAvailable(DoorController, int)；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupDirection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    internal bool DirectionAvailable(int direction)
    {
        CleanupDirection();
        return door != null && (occupancyDirection == 0 || occupancyDirection == direction);
    }

    /// <summary>通行身份标识。</summary>
    internal GameArchitecture.ObjectIdentity TraversalIdentity => lease.Identity;

    /// <summary>RequestTraversal 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.QueryToken(VillagerController)、DoorwayTraversalOccupancy.TryOccupancyCore(VillagerController, int)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.TryOccupancy(VillagerController, int)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.QueryToken(VillagerController)、DoorwayTraversalOccupancy.TryOccupancyCore(VillagerController, int)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    /// <param name="request">请求。</param>
    internal GameArchitecture.TraversalRequestResult RequestTraversal(VillagerController character, int direction, GameArchitecture.RequestContext request)
    {
        if (character == null || character.actionCoordinator == null || door == null || !door.isActiveAndEnabled)
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.ObjectHasInvalidate);
        }

        if (character.IsDead)
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.HasDeath);
        }

        // 原因。
var reason = lease.ValidateRequest(request, character.actionCoordinator.CurrentHandle);
        if (reason != GameArchitecture.RejectionReason.None)
        {
            return GameArchitecture.TraversalRequestResult.Reject(reason);
        }

        if (TryOccupancyCore(character, direction))
        {
            return GameArchitecture.TraversalRequestResult.Accept(QueryToken(character));
        }
        else
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.PermissionBeingOccupancy);
        }
    }

    /// <summary>TryOccupancy 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    internal bool TryOccupancy(VillagerController character, int direction)
    {
        // 动作。
global::GameArchitecture.ActionHandle action;
        if (character != null && character.actionCoordinator != null)
        {
            action = character.actionCoordinator.CurrentHandle;
        }
        else
        {
            action = default;
        }

        return RequestTraversal(character, direction, new GameArchitecture.RequestContext("门洞兼容入口", action.Object, TraversalIdentity, action.Id)).HasAccept;
    }

    /// <summary>TryOccupancyCore 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.DirectionAvailable(int)、DoorwayTraversalOccupancy.Queue(int)、TraversalTokenRegistry.Register(ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)；下游（含内部回调体） DoorwayTraversalOccupancy.DirectionAvailable(int)、DoorwayTraversalOccupancy.Queue(int)、TraversalTokenRegistry.Register(ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    private bool TryOccupancyCore(VillagerController character, int direction)
    {
        if (!DirectionAvailable(direction))
        {
            return false;
        }

        // 等待。
var wait = Queue(direction);
        if (wait.Count == 0 || wait[0] != character)
        {
            return false;
        }

        if (character.actionCoordinator == null || !character.actionCoordinator.CurrentHandle.Valid)
        {
            return false;
        }

        // 凭证。
var token = lease.Register(character.actionCoordinator.CurrentHandle);
        if (!token.Valid)
        {
            return false;
        }

        memberToken[character] = token;
        occupancyDirection = direction;
        return true;
    }

    /// <summary>Release 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.ReleaseLease(VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)、VillagerController.UnregisterPursuitIngressWaiter()；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.ReleaseLease(VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    internal void Release(VillagerController character)
    {
        positiveDirection.Remove(character);
        reverse.Remove(character);
        ReleaseLease(character);
        CleanupDirection();
    }

    /// <summary>CleanupInvalidate 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.ReleaseLease(VillagerController)、DoorwayTraversalOccupancy.Valid(VillagerController, int)、List&lt;VillagerController&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.QueueFirstYieldRow(VillagerController, int)、DoorwayTraversalOccupancy.Register(VillagerController, int)、VillagerController.CleanupPursuitIngressWaiters()；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.ReleaseLease(VillagerController)、DoorwayTraversalOccupancy.Valid(VillagerController, int)、List&lt;VillagerController&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CleanupInvalidate()
    {
        // 当前遍历索引。
for (int i = positiveDirection.Count - 1; i >= 0; i--)
        {
            if (!Valid(positiveDirection[i], 1))
            {
                ReleaseLease(positiveDirection[i]);
                positiveDirection.RemoveAt(i);
            }
        }

        // 当前遍历索引。
for (int i = reverse.Count - 1; i >= 0; i--)
        {
            if (!Valid(reverse[i], -1))
            {
                ReleaseLease(reverse[i]);
                reverse.RemoveAt(i);
            }
        }

        CleanupDirection();
    }

    /// <summary>CleanupDirection 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.Queue(int)、DoorwayTraversalOccupancy.Valid(VillagerController, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.CleanupInvalidate()、DoorwayTraversalOccupancy.DirectionAvailable(int)、DoorwayTraversalOccupancy.Release(VillagerController)；下游（含内部回调体） DoorwayTraversalOccupancy.Queue(int)、DoorwayTraversalOccupancy.Valid(VillagerController, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupDirection()
    {
        if (occupancyDirection == 0)
        {
            return;
        }

        // character（角色）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 DoorwayTraversalOccupancy.Valid(VillagerController, int) 的参数、DoorwayTraversalOccupancy.CleanupDirection()。
foreach (var character in Queue(occupancyDirection))
        {
            if (character != null && character.DoorwayTraverseMiddle && Valid(character, occupancyDirection))
            {
                return;
            }
        }

        occupancyDirection = 0;
    }

    /// <summary>Valid 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)、Vector3.Dot(Vector3, Vector3)、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.CleanupInvalidate()；下游（含内部回调体） HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)、Vector3.Dot(Vector3, Vector3)、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    /// <param name="direction">方向。</param>
    private bool Valid(VillagerController character, int direction)
    {
        if (door == null || character == null || !character.isActiveAndEnabled || !character.IsActive || character.IsDead || character.CurrentDoorway != door || character.DoorwaySourceSide != direction)
        {
            return false;
        }

        // 路线。
var route = character.CurrentStairTraversal;
        if (route != null && route.IsReady && route.entranceDoor == door && direction == VillagerController.ResolveDoor03ExteriorSourceSide(route) && route.IsOnInteriorSide(character.transform.position, 0.16f))
        {
            return false;
        }

        if (character.DoorwayTraverseMiddle)
        {
            return true;
        }

        // 偏移量。
Vector3 offset = character.transform.position - door.PassageCenter;
        offset.y = 0f;
        return Vector3.Dot(offset, door.PassageNormal) * direction >= -0.2f;
    }

    /// <summary>ClearValue 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, TraversalToken&gt;.Clear()、List&lt;VillagerController&gt;.Clear()、TraversalTokenRegistry.Close()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.OnDisable()、DoorwayTraversalOccupancy.OpenRelease()；下游（含内部回调体） Dictionary&lt;VillagerController, TraversalToken&gt;.Clear()、List&lt;VillagerController&gt;.Clear()、TraversalTokenRegistry.Close()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ClearValue()
    {
        positiveDirection.Clear();
        reverse.Clear();
        occupancyDirection = 0;
        lease.Close();
        memberToken.Clear();
    }

    /// <summary>HorizontalDistance 的业务处理入口；本方法直接依赖 Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.Register(VillagerController, int)；下游（含内部回调体） Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">首个。</param>
    /// <param name="second">第二。</param>
    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = second.y;
        return Vector3.Distance(first, second);
    }
}
