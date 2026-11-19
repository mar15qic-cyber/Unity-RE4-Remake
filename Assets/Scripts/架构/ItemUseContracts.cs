using GameArchitecture;

/// <summary>
/// 物品使用服务与已绑定角色宿主之间的业务契约。ItemUseService 读取只读状态并同步调用治疗、上限或兼容备弹入口，
/// 具体角色适配器负责落地效果；接口不提供场景查找或物品数量写入口。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.Contracts", "I物品使用目标")]
public interface IItemUseTarget
{
    /// <summary>中文含义：是否有效。目标是否仍可接受本次物品效果，由使用服务在提交前核对。</summary>
    bool Valid { get; }

    /// <summary>中文含义：当前生命值。角色当前生命值，用于判断是否需要治疗。</summary>
    float CurrentHealth { get; }

    /// <summary>中文含义：生命最大。角色生命上限，用于治疗资格及上限提升后的效果计算。</summary>
    float HealthMaximum { get; }

    /// <summary>中文含义：是否具有武器模块。是否具备武器适配能力，用于决定弹药物品能否使用。</summary>
    bool HasWeaponModule { get; }

    /// <summary>物品使用事务提交效果时调用，角色适配器负责提高生命上限。</summary>
    /// <param name="count">中文含义：数量。配置要求增加的生命上限数值。</param>
    void IncreaseHealthMaximum(float count);
    /// <summary>物品使用事务提交效果时调用，由角色生命模块实际执行治疗和上限限制。</summary>
    /// <param name="count">中文含义：数量。配置要求恢复的生命值。</param>
    void Healing(float count);
    /// <summary>物品使用事务处理弹药效果时调用，由目标适配器增加对应武器的兼容备弹。</summary>
    /// <param name="type">中文含义：当前武器类别。弹药对应的武器种类。</param>
    /// <param name="count">中文含义：数量。本次效果增加的备弹数量。</param>
    void IncreaseCompatibleReserveAmmo(WeaponType type, int count);
}

/// <summary>物品服务同步返回的提交结果，携带原请求、结论和原因；调用方通过 Committed 决定后续表现。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.Contracts", "物品使用结果")]
public readonly struct ItemUseResult
{
    /// <summary>中文含义：上下文。本次物品使用的原始请求上下文，保留身份及关联编号用于追踪。</summary>
    public RequestContext Context { get; }
    /// <summary>中文含义：结论。是否已经接受提交的结论，构造时由 commit 映射。</summary>
    public RequestConclusion Conclusion { get; }
    /// <summary>中文含义：原因。拒绝或接受的业务原因，由物品服务传入并按值保留。</summary>
    public RejectionReason Reason { get; }
    /// <summary>中文含义：是否已提交。调用方读取的提交成功标志，不重新执行物品使用。</summary>
    public bool Committed => Conclusion == RequestConclusion.Accept;

    /// <summary>ItemUseService 各接受或拒绝分支构造结果，通过返回值交还调用方。</summary>
    /// <param name="context">中文含义：上下文。原物品使用请求。</param>
    /// <param name="commit">中文含义：是否提交。本次事务是否已提交。</param>
    /// <param name="reason">中文含义：原因。事务判定的原因。</param>
    public ItemUseResult(RequestContext context, bool commit, RejectionReason reason)
    {
        Context = context;
        if (commit)
        {
            Conclusion = RequestConclusion.Accept;
        }
        else
        {
            Conclusion = RequestConclusion.Reject;
        }

        Reason = reason;
    }
}
