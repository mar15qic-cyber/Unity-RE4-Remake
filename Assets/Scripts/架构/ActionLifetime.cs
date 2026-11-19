using System;

namespace GameArchitecture
{
    /// <summary>
    /// 单个动作通道的租用与句柄有效期模型，只校验身份和占用，不决定业务优先级。
    /// 持有者同步调用 BeginLease、TryBegin、StillValid 和 TryRelease；结果通过返回值通信，不广播事件。
    /// 当前契约测试直接验证这些入口；实际动作审批仍由各自协调器负责。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "动作有效期")]
    public sealed class ActionLifetime
    {
        /// <summary>中文含义：身份标识。当前拥有者及租用版本；BeginLease 更新，用于拒绝前一轮对象池回调。</summary>
        private ObjectIdentity identity;
        /// <summary>中文含义：下部元素编号。动作编号序列，跨租用递增；溢出抛错而不回绕复用历史编号。</summary>
        private long lowerElementId;
        /// <summary>中文含义：当前句柄。当前通道唯一有效动作句柄；释放、关闭或换租用时清空。</summary>
        private ActionHandle currentHandle;
        /// <summary>中文含义：是否具有关闭。尚未租用或已关闭时为 true，阻止通道接受新动作。</summary>
        private bool hasClose = true;
        /// <summary>中文含义：当前。持有者可读取的当前句柄，只能经本模型的写入口改变。</summary>
        public ActionHandle Current => currentHandle;

        /// <summary>持有者开始新租用时调用；同一对象必须提高版本，并使上一租用的动作失效。</summary>
        /// <param name="newIdentity">中文含义：新身份标识。有效的新租用身份；已建立身份后不接受其他对象或旧版本。</param>
        /// <exception cref = "ArgumentException">新身份无效。</exception>
        /// <exception cref = "InvalidOperationException">替换了对象或租用版本没有递增。</exception>
        public void BeginLease(ObjectIdentity newIdentity)
        {
            if (!newIdentity.Valid)
            {
                throw new ArgumentException("租用身份无效");
            }

            if (identity.Valid && (newIdentity.InstanceId != identity.InstanceId || newIdentity.LeaseVersion <= identity.LeaseVersion))
            {
                throw new InvalidOperationException("同一拥有者必须使用递增租用版本，不能替换为其他对象");
            }

            identity = newIdentity;
            currentHandle = default;
            hasClose = false;
        }

        /// <summary>持有者完成业务审批及旧资源释放后调用；按请求、通道状态、目标身份、动作占用的顺序校验。</summary>
        /// <param name="request">中文含义：请求。携带发起方、目标和关联编号的动作请求。</param>
        /// <returns>接受时返回新句柄；拒绝时返回具体原因，不抢占已有动作。</returns>
        public ActionResult TryBegin(RequestContext request)
        {
            if (!request.Valid)
            {
                return ActionResult.Reject(RejectionReason.ParameterInvalid);
            }

            if (hasClose)
            {
                return ActionResult.Reject(RejectionReason.NotYetReady);
            }

            if (request.Target != identity)
            {
                return ActionResult.Reject(RejectionReason.ObjectHasInvalidate);
            }

            if (currentHandle.Valid)
            {
                return ActionResult.Reject(RejectionReason.ActionConflict);
            }

            currentHandle = new ActionHandle(identity, checked(++lowerElementId));
            return ActionResult.Accept(currentHandle);
        }

        /// <summary>延迟回调或释放前调用；要求通道开放且句柄与当前动作完全一致。</summary>
        /// <param name="handle">中文含义：句柄。待验证的动作句柄。</param>
        /// <returns>当前租用内仍有效时返回 true。</returns>
        public bool StillValid(ActionHandle handle)
        {
            return !hasClose && handle.Valid && handle == currentHandle;
        }

        /// <summary>动作完成或取消链调用；只有当前句柄可释放通道，旧回调不会清除新动作。</summary>
        /// <param name="handle">中文含义：句柄。调用者持有的待释放句柄。</param>
        /// <returns>本次确实释放当前动作时返回 true。</returns>
        public bool TryRelease(ActionHandle handle)
        {
            if (!StillValid(handle))
            {
                return false;
            }

            currentHandle = default;
            return true;
        }

        /// <summary>持有者关闭或回收时调用；清除有效动作并拒绝新请求，直到下一次 BeginLease。</summary>
        public void Close()
        {
            currentHandle = default;
            hasClose = true;
        }
    }
}
