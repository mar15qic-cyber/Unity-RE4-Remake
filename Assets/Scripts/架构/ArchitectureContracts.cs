using System;
using UnityEngine;

namespace GameArchitecture
{
    /// <summary>对象实例与对象池租用版本组成的值身份。动作、伤害、通路和许可请求携带此值，消费者必须同时比较两个字段以拒绝旧回调。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "对象身份")]
    public readonly struct ObjectIdentity : IEquatable<ObjectIdentity>
    {
        /// <summary>中文含义：实例编号。拥有者实例编号，零为无效身份。</summary>
        public int InstanceId { get; }
        /// <summary>中文含义：租用版本。拥有者本轮租用版本，必须为正数。</summary>
        public long LeaseVersion { get; }
        /// <summary>中文含义：是否有效。实例编号非零且租用版本为正数时有效。</summary>
        public bool Valid => InstanceId != 0 && LeaseVersion > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="instanceId">中文含义：实例编号。拥有者实例编号。</param>
        /// <param name="leaseVersion">中文含义：租用版本。当前租用版本。</param>
        public ObjectIdentity(int instanceId, long leaseVersion)
        {
            this.InstanceId = instanceId;
            this.LeaseVersion = leaseVersion;
        }

        /// <summary>比较同类型身份的全部组成字段，供校验、字典与相等运算使用。</summary>
        /// <param name="other">中文含义：另一个对象身份标识。待比较的另一个值或对象。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public bool Equals(ObjectIdentity other) => InstanceId == other.InstanceId && LeaseVersion == other.LeaseVersion;
        /// <summary>对象比较入口，先通过模式变量匹配本契约类型，再复用强类型 Equals。</summary>
        /// <param name="other">中文含义：另一个对象。待比较的另一个值或对象。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public override bool Equals(object other) => other is ObjectIdentity /* 中文含义：身份标识。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ identity && Equals(identity);
        /// <summary>按与 Equals 一致的字段组合哈希，供字典和集合定位；保留原未检查溢出语义。</summary>
        /// <returns>由参与等值比较的字段组成的整数哈希值。</returns>
        public override int GetHashCode() => unchecked(InstanceId * 397 ^ LeaseVersion.GetHashCode());
        /// <summary>相等运算入口，直接复用强类型 Equals，两个组成字段均相同才相等。</summary>
        /// <param name="left">中文含义：左侧。左侧契约值。</param>
        /// <param name="right">中文含义：右侧。右侧契约值。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public static bool operator ==(ObjectIdentity left, ObjectIdentity right) => left.Equals(right);
        /// <summary>不等运算入口，对强类型 Equals 结果取反。</summary>
        /// <param name="left">中文含义：左侧。左侧契约值。</param>
        /// <param name="right">中文含义：右侧。右侧契约值。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public static bool operator !=(ObjectIdentity left, ObjectIdentity right) => !left.Equals(right);
    }

    /// <summary>单次动作的不可变凭证，由动作通道或协调器发放，动画标记和延迟回调携带它验证所属对象租用及动作编号。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "动作句柄")]
    public readonly struct ActionHandle : IEquatable<ActionHandle>
    {
        /// <summary>中文含义：对象。动作所属对象及租用版本。</summary>
        public ObjectIdentity Object { get; }
        /// <summary>中文含义：编号。所属对象上的动作编号，必须为正数。</summary>
        public long Id { get; }
        /// <summary>中文含义：是否有效。对象身份与动作编号同时有效时为 true。</summary>
        public bool Valid => Object.Valid && Id > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="objectValue">中文含义：对象值。动作所属对象身份。</param>
        /// <param name="id">中文含义：编号。动作编号。</param>
        public ActionHandle(ObjectIdentity objectValue, long id)
        {
            this.Object = objectValue;
            this.Id = id;
        }

        /// <summary>比较同类型身份的全部组成字段，供校验、字典与相等运算使用。</summary>
        /// <param name="other">中文含义：另一个动作句柄。待比较的另一个值或对象。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public bool Equals(ActionHandle other) => Object == other.Object && Id == other.Id;
        /// <summary>对象比较入口，先通过模式变量匹配本契约类型，再复用强类型 Equals。</summary>
        /// <param name="other">中文含义：另一个对象。待比较的另一个值或对象。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public override bool Equals(object other) => other is ActionHandle /* 中文含义：句柄。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ handle && Equals(handle);
        /// <summary>按与 Equals 一致的字段组合哈希，供字典和集合定位；保留原未检查溢出语义。</summary>
        /// <returns>由参与等值比较的字段组成的整数哈希值。</returns>
        public override int GetHashCode() => unchecked(Object.GetHashCode() * 397 ^ Id.GetHashCode());
        /// <summary>相等运算入口，直接复用强类型 Equals，两个组成字段均相同才相等。</summary>
        /// <param name="left">中文含义：左侧。左侧契约值。</param>
        /// <param name="right">中文含义：右侧。右侧契约值。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public static bool operator ==(ActionHandle left, ActionHandle right) => left.Equals(right);
        /// <summary>不等运算入口，对强类型 Equals 结果取反。</summary>
        /// <param name="left">中文含义：左侧。左侧契约值。</param>
        /// <param name="right">中文含义：右侧。右侧契约值。</param>
        /// <returns>是否满足该入口的等值或不等值比较条件。</returns>
        public static bool operator !=(ActionHandle left, ActionHandle right) => !left.Equals(right);
    }

    /// <summary>模块间同步请求的上下文。调用方构造来源、双方身份和关联编号，规则及协调器读取它执行资格校验并在结果中保留。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "请求上下文")]
    public readonly struct RequestContext
    {
        /// <summary>中文含义：来源。请求来源字符串，用于校验和诊断，不得为空白。</summary>
        public string Source { get; }
        /// <summary>中文含义：发起者。请求发起方及其租用版本。</summary>
        public ObjectIdentity Initiator { get; }
        /// <summary>中文含义：目标。请求目标及其租用版本。</summary>
        public ObjectIdentity Target { get; }
        /// <summary>中文含义：关联编号。关联动作或事务编号，必须为正数。</summary>
        public long AssociationId { get; }
        /// <summary>中文含义：是否有效。来源、双方身份和关联编号均有效时为 true。</summary>
        public bool Valid => !string.IsNullOrWhiteSpace(Source) && Initiator.Valid && Target.Valid && AssociationId > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="source">中文含义：来源。业务请求来源。</param>
        /// <param name="initiator">中文含义：发起者。发起方身份。</param>
        /// <param name="target">中文含义：目标对象身份标识。目标身份。</param>
        /// <param name="associationId">中文含义：关联编号。关联动作或事务编号。</param>
        public RequestContext(string source, ObjectIdentity initiator, ObjectIdentity target, long associationId)
        {
            this.Source = source;
            this.Initiator = initiator;
            this.Target = target;
            this.AssociationId = associationId;
        }
    }

    /// <summary>请求审批结论，经同步结果返回给调用方；保留现有数值，默认值仍为拒绝。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "请求结论")]
    public enum RequestConclusion
    {
        /// <summary>中文含义：拒绝。该分类表示“拒绝”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("拒绝")]
        Reject,
        /// <summary>中文含义：接受。该分类表示“接受”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("接受")]
        Accept,
        /// <summary>中文含义：进入已有缓冲区。该分类表示“进入既有缓冲”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("进入既有缓冲")]
        EnterExistingBuffer
    }

    /// <summary>跨模块通用拒绝原因，由审批入口返回给宿主和诊断；保留现有数值及默认语义。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "拒绝原因")]
    public enum RejectionReason
    {
        /// <summary>中文含义：参数无效。该分类表示“参数无效”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("参数无效")]
        ParameterInvalid,
        /// <summary>中文含义：无（未选择或尚未发生）。该分类表示“无”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("无")]
        None,
        /// <summary>中文含义：尚未已发生就绪。该分类表示“尚未就绪”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("尚未就绪")]
        NotYetReady,
        /// <summary>中文含义：对象是否具有使失效。该分类表示“对象已失效”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("对象已失效")]
        ObjectHasInvalidate,
        /// <summary>中文含义：是否具有死亡。该分类表示“已死亡”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("已死亡")]
        HasDeath,
        /// <summary>中文含义：动作冲突。该分类表示“动作冲突”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("动作冲突")]
        ActionConflict,
        /// <summary>中文含义：冷却未完成。该分类表示“冷却未结束”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("冷却未结束")]
        CooldownUnfinished,
        /// <summary>中文含义：资源不足。该分类表示“资源不足”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("资源不足")]
        AssetInsufficient,
        /// <summary>中文含义：许可正在占用。该分类表示“许可被占用”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("许可被占用")]
        PermissionBeingOccupancy,
        /// <summary>中文含义：回调是否具有已过期。该分类表示“回调已过期”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("回调已过期")]
        CallbackHasExpired
    }

    /// <summary>角色动作审批与状态快照共享的分类，协调器负责事务，行为及表现只读取分类。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "动作种类")]
    public enum ActionKind
    {
        /// <summary>中文含义：无（未选择或尚未发生）。该分类表示“无”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("无")]
        None,
        /// <summary>中文含义：待机。该分类表示“待机”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("待机")]
        Idle,
        /// <summary>中文含义：巡逻。该分类表示“巡逻”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("巡逻")]
        Patrol,
        /// <summary>中文含义：追逐。该分类表示“追击”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("追击")]
        Chase,
        /// <summary>中文含义：攻击。该分类表示“攻击”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("攻击")]
        Attack,
        /// <summary>中文含义：受击反应。该分类表示“受击”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("受击")]
        HitReaction,
        /// <summary>中文含义：爬行。该分类表示“爬行”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("爬行")]
        Crawl,
        /// <summary>中文含义：取得向上。该分类表示“起身”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("起身")]
        GetUp,
        /// <summary>中文含义：射击。该分类表示“射击”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("射击")]
        Shoot,
        /// <summary>中文含义：换弹。该分类表示“换弹”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("换弹")]
        Reload,
        /// <summary>中文含义：切换武器。该分类表示“切换武器”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("切换武器")]
        SwitchWeapon,
        /// <summary>中文含义：近战。该分类表示“近战”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("近战")]
        Melee,
        /// <summary>中文含义：通行。该分类表示“通行”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("通行")]
        Traversal,
        /// <summary>中文含义：已配对动作动作。该分类表示“配对动作”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("配对动作")]
        PairedActionAction,
        /// <summary>中文含义：死亡。该分类表示“死亡”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("死亡")]
        Death
    }

    /// <summary>位移凭证携带的来源分类，执行边界据此核对接管权限。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "运动来源")]
    public enum MotionSource
    {
        /// <summary>中文含义：无（未选择或尚未发生）。该分类表示“无”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("无")]
        None,
        /// <summary>中文含义：常规移动。该分类表示“普通移动”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("普通移动")]
        NormalMovement,
        /// <summary>中文含义：攻击根运动。该分类表示“攻击根运动”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("攻击根运动")]
        AttackRootMotion,
        /// <summary>中文含义：通行。该分类表示“通行”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("通行")]
        Traversal,
        /// <summary>中文含义：已配对动作动作。该分类表示“配对动作”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("配对动作")]
        PairedActionAction,
        /// <summary>中文含义：死亡。该分类表示“死亡”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("死亡")]
        Death
    }

    /// <summary>动作、通路和资源释放链传递的原因，供拥有者统一清理和记录。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "释放原因")]
    public enum ReleaseReason
    {
        /// <summary>中文含义：完成。该分类表示“完成”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("完成")]
        Complete,
        /// <summary>中文含义：取消。该分类表示“取消”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("取消")]
        Cancel,
        /// <summary>中文含义：已中断。该分类表示“被中断”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("被中断")]
        Interrupted,
        /// <summary>中文含义：死亡。该分类表示“死亡”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("死亡")]
        Death,
        /// <summary>中文含义：停用。该分类表示“停用”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("停用")]
        Deactivate,
        /// <summary>中文含义：恢复。该分类表示“回收”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("回收")]
        Recovery,
        /// <summary>中文含义：场景卸载。该分类表示“场景卸载”；由拥有者按对应业务状态解释。</summary>
        [UnityEngine.InspectorName("场景卸载")]
        SceneUnload
    }

    /// <summary>动作协调器的只读审批输入，包含请求上下文、目标动作及预期当前动作；调用方通过参数传入，协调器返回 ActionResult。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "动作请求")]
    public readonly struct ActionRequest
    {
        /// <summary>中文含义：上下文。请求来源、双方身份及关联编号。</summary>
        public RequestContext Context { get; }
        /// <summary>中文含义：动作。申请执行的动作种类。</summary>
        public ActionKind Action { get; }
        /// <summary>中文含义：预期当前动作。调用方预期仍有效的当前动作；默认值表示没有提供该约束。</summary>
        public ActionHandle ExpectedCurrentAction { get; }

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="context">中文含义：上下文。动作请求上下文。</param>
        /// <param name="action">中文含义：动作。申请的动作种类。</param>
        /// <param name="expectedCurrentAction">中文含义：预期当前动作。可选的预期当前动作句柄。</param>
        public ActionRequest(RequestContext context, ActionKind action, ActionHandle expectedCurrentAction = default)
        {
            this.Context = context;
            this.Action = action;
            this.ExpectedCurrentAction = expectedCurrentAction;
        }
    }

    /// <summary>动作审批的不可变输出。协调器或有效期模型通过工厂建立接受、拒绝或缓冲结果，调用方读取结论、原因与句柄决定后续执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "动作结果")]
    public readonly struct ActionResult
    {
        /// <summary>中文含义：结论。审批结论，区分拒绝、接受和进入已有缓冲。</summary>
        public RequestConclusion Conclusion { get; }
        /// <summary>中文含义：原因。拒绝原因，接受或缓冲时为 None。</summary>
        public RejectionReason Reason { get; }
        /// <summary>中文含义：句柄。已获准或已登记缓冲所属的动作句柄，拒绝时为默认值。</summary>
        public ActionHandle Handle { get; }
        /// <summary>中文含义：是否具有接受。结论为接受且句柄有效时为 true，缓冲不视为已经开始动作。</summary>
        public bool HasAccept => Conclusion == RequestConclusion.Accept && Handle.Valid;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="conclusion">中文含义：结论。审批结论。</param>
        /// <param name="reason">中文含义：原因。审批原因。</param>
        /// <param name="handle">中文含义：句柄。与结论关联的动作句柄。</param>
        private ActionResult(RequestConclusion conclusion, RejectionReason reason, ActionHandle handle)
        {
            this.Conclusion = conclusion;
            this.Reason = reason;
            this.Handle = handle;
        }

        /// <summary>审批成功时构造接受结果；句柄无效则抛出参数异常。</summary>
        /// <param name="handle">中文含义：句柄。已经获准的有效动作句柄。</param>
        /// <returns>交给调用方的不可变审批结果。</returns>
        public static ActionResult Accept(ActionHandle handle)
        {
            if (!handle.Valid)
            {
                throw new ArgumentException("接受结果必须包含有效动作句柄");
            }

            return new ActionResult(RequestConclusion.Accept, RejectionReason.None, handle);
        }

        /// <summary>审批失败时构造拒绝结果；原因为 None 则抛出参数异常。</summary>
        /// <param name="reason">中文含义：原因。非 None 的拒绝原因。</param>
        /// <returns>交给调用方的不可变审批结果。</returns>
        public static ActionResult Reject(RejectionReason reason)
        {
            if (reason == RejectionReason.None)
            {
                throw new ArgumentException("拒绝结果必须说明原因");
            }

            return new ActionResult(RequestConclusion.Reject, reason, default);
        }

        /// <summary>请求进入既有动作缓冲时构造结果，必须关联有效已登记句柄。</summary>
        /// <param name="handle">中文含义：句柄。缓冲所属的有效动作句柄。</param>
        /// <returns>交给调用方的不可变审批结果。</returns>
        public static ActionResult HasBuffer(ActionHandle handle)
        {
            if (!handle.Valid)
            {
                throw new ArgumentException("缓冲结果必须关联已登记的句柄");
            }

            return new ActionResult(RequestConclusion.EnterExistingBuffer, RejectionReason.None, handle);
        }
    }

    /// <summary>位移控制的不可变授权凭证，协调器发放后由 CharacterMotionExecutor 等执行边界核验；绑定动作、来源与接管编号。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "运动控制凭证")]
    public readonly struct MotionControlToken
    {
        /// <summary>中文含义：动作。本次位移所属的有效动作。</summary>
        public ActionHandle Action { get; }
        /// <summary>中文含义：来源。位移来源，None 不能形成有效授权。</summary>
        public MotionSource Source { get; }
        /// <summary>中文含义：交接编号。本次位移接管编号，用于拒绝旧授权提交。</summary>
        public long HandoffId { get; }
        /// <summary>中文含义：是否有效。动作、来源和接管编号均有效时为 true。</summary>
        public bool Valid => Action.Valid && Source != MotionSource.None && HandoffId > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="action">中文含义：动作。所属动作句柄。</param>
        /// <param name="source">中文含义：来源运动来源。位移来源。</param>
        /// <param name="handoffId">中文含义：交接编号。接管编号。</param>
        public MotionControlToken(ActionHandle action, MotionSource source, long handoffId)
        {
            this.Action = action;
            this.Source = source;
            this.HandoffId = handoffId;
        }
    }

    /// <summary>通路登记簿发放的不可变占用凭证，角色携带它提交通行更新和释放，通路同时核对自身租用、角色动作与占用编号。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "通行凭证")]
    public readonly struct TraversalToken
    {
        /// <summary>中文含义：通行。发放占用的通路身份和开放版本。</summary>
        public ObjectIdentity Traversal { get; }
        /// <summary>中文含义：动作。获准通行的角色动作句柄。</summary>
        public ActionHandle Action { get; }
        /// <summary>中文含义：占用编号。本次登记的唯一占用编号。</summary>
        public long OccupancyId { get; }
        /// <summary>中文含义：是否有效。通路、动作和占用编号均有效时为 true。</summary>
        public bool Valid => Traversal.Valid && Action.Valid && OccupancyId > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="traversal">中文含义：通行。通路身份。</param>
        /// <param name="action">中文含义：动作。获准通行动作。</param>
        /// <param name="occupancyId">中文含义：占用编号。本次占用编号。</param>
        public TraversalToken(ObjectIdentity traversal, ActionHandle action, long occupancyId)
        {
            this.Traversal = traversal;
            this.Action = action;
            this.OccupancyId = occupancyId;
        }
    }

    /// <summary>玩法许可服务返回的不可变占用句柄，请求方保存后用于 Update 或 Release；不能用其他服务或其他请求方的句柄解除占用。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "玩法许可句柄")]
    public readonly struct GameplayPermissionHandle
    {
        /// <summary>中文含义：服务。发放该占用的服务身份。</summary>
        public ObjectIdentity Service { get; }
        /// <summary>中文含义：请求方。占用所属请求方身份。</summary>
        public ObjectIdentity Requester { get; }
        /// <summary>中文含义：编号。本服务内的占用编号。</summary>
        public long Id { get; }
        /// <summary>中文含义：是否有效。服务、请求方和编号均有效时为 true。</summary>
        public bool Valid => Service.Valid && Requester.Valid && Id > 0;

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="service">中文含义：服务。发放占用的服务身份。</param>
        /// <param name="requester">中文含义：请求方。请求方身份。</param>
        /// <param name="id">中文含义：编号。占用编号。</param>
        public GameplayPermissionHandle(ObjectIdentity service, ObjectIdentity requester, long id)
        {
            this.Service = service;
            this.Requester = requester;
            this.Id = id;
        }
    }

    /// <summary>伤害结算链的只读输入，命中宿主构造后提交给结算适配器；原始命中部位由既有类型识别，规则编号由适配器解释。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "伤害请求")]
    public readonly struct DamageRequest
    {
        /// <summary>中文含义：上下文。本次命中的请求上下文。</summary>
        public RequestContext Context { get; }
        /// <summary>中文含义：攻击动作。攻击动作句柄，用于验证动作有效期。</summary>
        public ActionHandle AttackAction { get; }
        /// <summary>中文含义：命中序号。同一攻击中的命中序号，用于重复命中辨识。</summary>
        public int HitSequence { get; }
        /// <summary>中文含义：基础伤害。尚未应用规则倍率的基础伤害。</summary>
        public float BaseDamage { get; }
        /// <summary>中文含义：规则编号。交由结算适配器解释的规则编号。</summary>
        public int RulesId { get; }
        /// <summary>中文含义：命中位置。表现或结算追踪使用的世界命中位置。</summary>
        public Vector3 HitPosition { get; }

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="context">中文含义：上下文。本次命中请求上下文。</param>
        /// <param name="attackAction">中文含义：攻击动作。所属攻击动作。</param>
        /// <param name="hitSequence">中文含义：命中序号。命中序号。</param>
        /// <param name="baseDamage">中文含义：基础伤害。基础伤害。</param>
        /// <param name="rulesId">中文含义：规则编号。规则编号。</param>
        /// <param name="hitPosition">中文含义：命中位置。世界命中位置。</param>
        public DamageRequest(RequestContext context, ActionHandle attackAction, int hitSequence, float baseDamage, int rulesId, Vector3 hitPosition)
        {
            this.Context = context;
            this.AttackAction = attackAction;
            this.HitSequence = hitSequence;
            this.BaseDamage = baseDamage;
            this.RulesId = rulesId;
            this.HitPosition = hitPosition;
        }
    }

    /// <summary>伤害入口返回的已提交事实，宿主或表现读取实际扣血与致死结果；不能把基础伤害或倍率计算值当作实际扣血。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "伤害结果")]
    public readonly struct DamageResult
    {
        /// <summary>中文含义：请求。本次结算对应的原始伤害请求。</summary>
        public DamageRequest Request { get; }
        /// <summary>中文含义：是否已提交。伤害是否真正提交到生命状态。</summary>
        public bool Committed { get; }
        /// <summary>中文含义：原因。接受或拒绝的原因。</summary>
        public RejectionReason Reason { get; }
        /// <summary>中文含义：实际扣除生命。生命模块实际扣除的数值。</summary>
        public float ActualDeductHealth { get; }
        /// <summary>中文含义：是否当前致死。本次提交是否造成致死结果。</summary>
        public bool CurrentLethal { get; }

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="request">中文含义：请求。原始伤害请求。</param>
        /// <param name="committed">中文含义：是否已提交。是否实际提交。</param>
        /// <param name="reason">中文含义：原因。结算原因。</param>
        /// <param name="actualDeductHealth">中文含义：实际扣除生命。实际扣血数值。</param>
        /// <param name="currentLethal">中文含义：是否当前致死。本次是否致死。</param>
        public DamageResult(DamageRequest request, bool committed, RejectionReason reason, float actualDeductHealth, bool currentLethal)
        {
            this.Request = request;
            this.Committed = committed;
            this.Reason = reason;
            this.ActualDeductHealth = actualDeductHealth;
            this.CurrentLethal = currentLethal;
        }
    }

    /// <summary>角色宿主提供的不可变状态快照，行为决策与表现通过值查询读取生命、动作和位移来源，不暴露角色、动画器或可写集合。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "角色状态快照")]
    public readonly struct CharacterStateSnapshot
    {
        /// <summary>中文含义：对象。角色实例身份及租用版本。</summary>
        public ObjectIdentity Object { get; }
        /// <summary>中文含义：当前生命值。构造快照时的当前生命。</summary>
        public float CurrentHealth { get; }
        /// <summary>中文含义：最大生命值。构造快照时的生命上限。</summary>
        public float MaximumHealth { get; }
        /// <summary>中文含义：是否具有死亡。是否已经进入死亡状态。</summary>
        public bool HasDeath { get; }
        /// <summary>中文含义：动作。当前关联动作句柄。</summary>
        public ActionHandle Action { get; }
        /// <summary>中文含义：武器动作。武器动作通道当前种类。</summary>
        public ActionKind WeaponAction { get; }
        /// <summary>中文含义：全身动作。全身动作通道当前种类。</summary>
        public ActionKind FullBodyAction { get; }
        /// <summary>中文含义：运动。当前位移来源。</summary>
        public MotionSource Motion { get; }

        /// <summary>由拥有者或对应工厂构造此值，将参数复制为只读契约，不执行额外业务或广播事件。</summary>
        /// <param name="objectValue">中文含义：对象值。角色身份。</param>
        /// <param name="currentHealth">中文含义：当前生命值。当前生命。</param>
        /// <param name="maximumHealth">中文含义：最大生命值。生命上限。</param>
        /// <param name="hasDeath">中文含义：是否具有死亡。死亡事实。</param>
        /// <param name="action">中文含义：动作。当前动作句柄。</param>
        /// <param name="weaponAction">中文含义：武器动作。武器通道动作。</param>
        /// <param name="fullBodyAction">中文含义：全身动作。全身通道动作。</param>
        /// <param name="motion">中文含义：运动。位移来源。</param>
        public CharacterStateSnapshot(ObjectIdentity objectValue, float currentHealth, float maximumHealth, bool hasDeath, ActionHandle action, ActionKind weaponAction, ActionKind fullBodyAction, MotionSource motion)
        {
            this.Object = objectValue;
            this.CurrentHealth = currentHealth;
            this.MaximumHealth = maximumHealth;
            this.HasDeath = hasDeath;
            this.Action = action;
            this.WeaponAction = weaponAction;
            this.FullBodyAction = fullBodyAction;
            this.Motion = motion;
        }
    }
}
