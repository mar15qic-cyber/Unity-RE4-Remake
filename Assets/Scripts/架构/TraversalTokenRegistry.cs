using System.Collections.Generic;

namespace GameArchitecture
{
    /// <summary>通路宿主返回的不可变审批结果；消费者通过 HasAccept 判断，再持有 Token 或读取 Reason。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "通行申请结果")]
    public readonly struct TraversalRequestResult
    {
        /// <summary>中文含义：凭证。接受请求时返回的通行凭证，拒绝时为默认值。</summary>
        public TraversalToken Token { get; }
        /// <summary>中文含义：原因。审批原因；接受时必须为 None。</summary>
        public RejectionReason Reason { get; }
        /// <summary>中文含义：是否具有接受。只有凭证有效且没有拒绝原因才算接受，防止默认结果误放行。</summary>
        public bool HasAccept => Token.Valid && Reason == RejectionReason.None;

        /// <summary>由 Accept 或 Reject 工厂封装结果，外部不能拼接不一致的状态。</summary>
        /// <param name="token">中文含义：凭证。本次审批返回的凭证。</param>
        /// <param name="reason">中文含义：原因。本次审批的原因。</param>
        private TraversalRequestResult(TraversalToken token, RejectionReason reason)
        {
            Token = token;
            Reason = reason;
        }

        /// <summary>通路宿主申请成功后调用；无效凭证降为尚未就绪的拒绝结果。</summary>
        /// <param name="token">中文含义：凭证。登记簿发放的通行凭证。</param>
        /// <returns>包含有效凭证的接受结果，或 NotYetReady 拒绝结果。</returns>
        public static TraversalRequestResult Accept(TraversalToken token)
        {
            if (token.Valid)
            {
                return new TraversalRequestResult(token, RejectionReason.None);
            }

            return Reject(RejectionReason.NotYetReady);
        }

        /// <summary>通路宿主审批失败时调用；拒绝必须携带原因，不发放有效凭证。</summary>
        /// <param name="reason">中文含义：原因。非 None 的拒绝原因；传入 None 时抛出参数异常。</param>
        /// <returns>仅包含拒绝原因的结果。</returns>
        public static TraversalRequestResult Reject(RejectionReason reason)
        {
            if (reason == RejectionReason.None)
            {
                throw new System.ArgumentException("拒绝通行必须说明原因");
            }

            return new TraversalRequestResult(default, reason);
        }
    }

    /// <summary>
    /// 通路租约的唯一登记簿，由门洞占用、窗口及平台跳落宿主持有。
    /// 宿主同步调用验证、登记、查询和释放，凭证通过返回值通信；容量、排队和几何审批仍归具体通路。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "通行凭证登记簿")]
    public sealed class TraversalTokenRegistry
    {
        /// <summary>中文含义：占用。按角色租用身份保存当前凭证，单个角色在本登记簿最多持有一份占用。</summary>
        private readonly Dictionary<ObjectIdentity, TraversalToken> occupancy = new();
        /// <summary>中文含义：通行。当前开放的通路身份与版本；关闭时清空，重开时更新版本。</summary>
        private ObjectIdentity traversal;
        /// <summary>中文含义：租用版本。通路开放版本序列，避免重开后接纳上一轮凭证。</summary>
        private long leaseVersion;
        /// <summary>中文含义：下部元素编号。占用编号序列，贯穿多次开放，不因清空登记而复用编号。</summary>
        private long lowerElementId;
        /// <summary>中文含义：数量值。当前角色占用数量，供具体通路执行容量审批。</summary>
        public int CountValue => occupancy.Count;
        /// <summary>中文含义：身份标识。宿主构造请求目标时读取的当前通路身份。</summary>
        public ObjectIdentity Identity => traversal;

        /// <summary>通路宿主登记前调用，校验请求目标、角色租用和动作关联编号。</summary>
        /// <param name="request">中文含义：请求。角色向当前通路提交的请求。</param>
        /// <param name="action">中文含义：动作。角色当前通行动作句柄。</param>
        /// <returns>首个拒绝原因；全部一致时返回 None。</returns>
        public RejectionReason ValidateRequest(RequestContext request, ActionHandle action)
        {
            if (!request.Valid || !action.Valid)
            {
                return RejectionReason.ParameterInvalid;
            }

            if (!traversal.Valid || request.Target != traversal || request.Initiator != action.Object)
            {
                return RejectionReason.ObjectHasInvalidate;
            }

            if (request.AssociationId == action.Id)
            {
                return RejectionReason.None;
            }

            return RejectionReason.CallbackHasExpired;
        }

        /// <summary>通路启用或开始新租用时调用，清空旧占用并生成更高版本的通路身份。</summary>
        /// <param name="instanceId">中文含义：实例编号。具体通路拥有者的实例编号。</param>
        public void OpenRelease(int instanceId)
        {
            occupancy.Clear();
            traversal = new ObjectIdentity(instanceId, ++leaseVersion);
        }

        /// <summary>通路停用或关闭时调用，清空角色占用并使全部既有凭证失效。</summary>
        public void Close()
        {
            occupancy.Clear();
            traversal = default;
        }

        /// <summary>宿主完成业务审批后登记角色；相同角色重复登记沿用已有凭证，不隐式替换动作。</summary>
        /// <param name="action">中文含义：动作。获准通行的角色动作句柄。</param>
        /// <returns>有效凭证；通路未开放或动作无效时返回默认值。</returns>
        public TraversalToken Register(ActionHandle action)
        {
            if (!traversal.Valid || !action.Valid)
            {
                return default;
            }

            // hasHas 接收该角色已经持有的凭证，保持重复登记的幂等返回。
            if (occupancy.TryGetValue(action.Object, out var /* 中文含义：是否具有。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hasHas))
            {
                return hasHas;
            }

            // 新凭证同时绑定通路租用、角色动作和唯一占用编号。
            // 中文含义：token 为“凭证”。
var token = new TraversalToken(traversal, action, ++lowerElementId);
            occupancy.Add(action.Object, token);
            return token;
        }

        /// <summary>通路宿主按角色身份查询既有占用，不创建凭证。</summary>
        /// <param name="character">中文含义：角色。角色实例和租用版本。</param>
        /// <returns>该角色的已登记凭证，未登记则返回默认值。</returns>
        public TraversalToken Query(ObjectIdentity character)
        {
            // token 接收字典中保存的凭证，只在查询成功后读取。
            if (occupancy.TryGetValue(character, out var /* 中文含义：凭证。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ token))
            {
                return token;
            }

            return default;
        }

        /// <summary>位移提交、通行更新或释放前核对凭证，拒绝旧通路、旧动作和旧占用编号。</summary>
        /// <param name="token">中文含义：凭证。调用方持有的待验证凭证。</param>
        /// <returns>与本登记簿当前记录完全一致时返回 true。</returns>
        public bool Valid(TraversalToken token)
        {
            if (!token.Valid || token.Traversal != traversal)
            {
                return false;
            }

            // current 是同一角色租用下的最新占用，必须同时匹配动作与占用编号。
            if (!occupancy.TryGetValue(token.Action.Object, out var /* 中文含义：当前通行凭证。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ current))
            {
                return false;
            }

            return current.Action == token.Action && current.OccupancyId == token.OccupancyId;
        }

        /// <summary>具体通路完成、取消或回收链调用，仅移除当前有效凭证对应的角色占用。</summary>
        /// <param name="token">中文含义：凭证。本次要释放的凭证。</param>
        /// <param name="reason">中文含义：原因。调用方提供的释放原因；当前登记簿只校验凭证，保留参数供统一契约使用。</param>
        /// <returns>实际删除当前记录时返回 true。</returns>
        public bool Release(TraversalToken token, ReleaseReason reason)
        {
            if (!Valid(token))
            {
                return false;
            }

            occupancy.Remove(token.Action.Object);
            return true;
        }
    }
}
