using System.Collections.Generic;
using GameArchitecture;

/// <summary>武器运行状态的值快照，由 WeaponRuntimeModule.Query 返回给武器宿主及表现；不包含正式库存引用。</summary>
public readonly struct WeaponRuntimeSnapshot
{
    /// <summary>中文含义：弹匣中当前的子弹数量。查询时的弹匣数量。</summary>
    public readonly int magazine;
    /// <summary>中文含义：是否持有有效的换弹事务。查询时是否持有有效装填事务。</summary>
    public readonly bool isReload;
    /// <summary>中文含义：上一次开火时间。最近一次成功射击的宿主时间戳。</summary>
    public readonly float lastFireTime;
    /// <summary>中文含义：当前换弹流程的事务编号。当前装填事务编号；负数表示没有装填事务。</summary>
    public readonly int reloadTransaction;
    /// <summary>运行模块查询时复制状态，不让外部取得可写的内部武器记录。</summary>
    /// <param name="magazine">中文含义：弹匣中当前的子弹数量。当前弹匣数量。</param>
    /// <param name="isReload">中文含义：是否持有有效的换弹事务。当前是否处于装填事务中。</param>
    /// <param name="lastFireTime">中文含义：上一次开火时间。最近一次成功射击时间。</param>
    /// <param name="reloadTransaction">中文含义：当前换弹流程的事务编号。当前装填事务编号。</param>
    public WeaponRuntimeSnapshot(int magazine, bool isReload, float lastFireTime, int reloadTransaction)
    {
        this.magazine = magazine;
        this.isReload = isReload;
        this.lastFireTime = lastFireTime;
        this.reloadTransaction = reloadTransaction;
    }
}

/// <summary>
/// 武器运行数据拥有者。LeonWeaponHandler 和 WeaponBase 分别持有对应实例，通过同步方法提交装备、射击与装填。
/// 本模块不读取场景或动画；正式备弹由宿主传入并通过扣除委托提交，兼容备弹数组仅服务既有兼容入口。
/// </summary>
public sealed class WeaponRuntimeModule
{
    /// <summary>单件武器的可写内部记录，按武器身份索引，不向外部暴露。</summary>
    private sealed class WeaponRuntimeState
    {
        /// <summary>中文含义：弹匣中当前的子弹数量。当前弹匣数量，由初始化、开火和装填提交修改。</summary>
        public int magazine;
        /// <summary>中文含义：上一次开火时间。最近成功射击时间，由 TryFire 更新以执行冷却。</summary>
        public float lastFireTime;
        /// <summary>中文含义：当前换弹流程的事务编号。当前装填编号；-1 表示没有有效装填。</summary>
        public int reloadTransaction = -1;
    }

    /// <summary>中文含义：当前键值映射表。按武器实例身份保存弹匣、冷却和事务记录。</summary>
    private readonly Dictionary<int, WeaponRuntimeState> weapon = new Dictionary<int, WeaponRuntimeState>();
    /// <summary>中文含义：兼容背包备弹。旧接口使用的备弹数组，以兼容武器枚举数值索引；正式库存由背包拥有。</summary>
    private readonly int[] compatibleReserveAmmo = new int[4];
    /// <summary>中文含义：装备是否已初始化。装备初始状态是否已写入，防止重复初始化覆盖现有装备和兼容备弹。</summary>
    private bool equipmentInitialized;
    /// <summary>中文含义：当前装备。已提交的当前装备种类，仅初始化与 CommitEquipment 修改。</summary>
    private WeaponType currentEquipment;
    /// <summary>中文含义：装备。装备宿主及消费者读取的当前装备种类。</summary>
    public WeaponType Equipment => currentEquipment;

    /// <summary>装备宿主首次准备时调用；仅第一次写入初始装备及兼容备弹。</summary>
    /// <param name="initialEquipment">中文含义：初始装备。初始装备种类。</param>
    /// <param name="pistolReserveAmmo">中文含义：手枪背包备弹。兼容手枪备弹初始数量。</param>
    /// <param name="rifleReserveAmmo">中文含义：步枪背包备弹。兼容步枪备弹初始数量。</param>
    /// <param name="shotgunAmmoReserveAmmo">中文含义：霰弹枪弹药背包备弹。兼容霰弹枪备弹初始数量。</param>
    public void InitializeEquipment(WeaponType initialEquipment, int pistolReserveAmmo, int rifleReserveAmmo, int shotgunAmmoReserveAmmo)
    {
        if (equipmentInitialized)
        {
            return;
        }

        equipmentInitialized = true;
        currentEquipment = initialEquipment;
        compatibleReserveAmmo[(int)WeaponType.Pistol] = pistolReserveAmmo;
        compatibleReserveAmmo[(int)WeaponType.Rifle] = rifleReserveAmmo;
        compatibleReserveAmmo[(int)WeaponType.Shotgun] = shotgunAmmoReserveAmmo;
    }

    /// <summary>装备宿主完成切枪审批后提交结果；不自行切模型或触发动画。</summary>
    /// <param name="equipment">中文含义：装备。已经审批通过的目标装备。</param>
    public void CommitEquipment(WeaponType equipment)
    {
        currentEquipment = equipment;
    }

    /// <summary>兼容备弹入口共用的类型检查，同时防止非枪械枚举进入数组索引。</summary>
    /// <param name="type">中文含义：当前武器类别。待查询武器种类。</param>
    /// <returns>手枪、步枪或霰弹枪时返回 true。</returns>
    private static bool IsFirearm(WeaponType type)
    {
        return type == WeaponType.Pistol || type == WeaponType.Rifle || type == WeaponType.Shotgun;
    }

    /// <summary>旧消费者同步读取兼容备弹；正式装填的备弹可用量仍由宿主传入。</summary>
    /// <param name="type">中文含义：当前武器类别。要查询的武器种类。</param>
    /// <returns>对应兼容数量，非枪械返回零。</returns>
    public int QueryCompatibleReserveAmmo(WeaponType type)
    {
        if (IsFirearm(type))
        {
            return compatibleReserveAmmo[(int)type];
        }

        return 0;
    }

    /// <summary>兼容物品使用链增加备弹，只处理受支持的枪械。</summary>
    /// <param name="type">中文含义：当前武器类别。备弹对应武器种类。</param>
    /// <param name="count">中文含义：数量。沿用调用方传入的增加量，不额外改变旧接口数值语义。</param>
    public void IncreaseCompatibleReserveAmmo(WeaponType type, int count)
    {
        if (IsFirearm(type))
        {
            compatibleReserveAmmo[(int)type] += count;
        }
    }

    /// <summary>兼容装填链扣除备弹，将扣除后的结果限制为不小于零。</summary>
    /// <param name="type">中文含义：当前武器类别。备弹对应武器种类。</param>
    /// <param name="count">中文含义：数量。需要扣除的数量。</param>
    public void ConsumptionCompatibleReserveAmmo(WeaponType type, int count)
    {
        if (IsFirearm(type))
        {
            compatibleReserveAmmo[(int)type] = System.Math.Max(0, QueryCompatibleReserveAmmo(type) - count);
        }
    }

    /// <summary>武器宿主初始化弹匣时调用；已有记录只覆盖弹匣，不重置射击冷却或装填事务。</summary>
    /// <param name="identity">中文含义：身份标识。武器实例身份编号。</param>
    /// <param name="initialMagazine">中文含义：初始弹匣。初始化后的弹匣数量。</param>
    public void InitializeWeapon(int identity, int initialMagazine)
    {
        // state 是此武器唯一内部记录；缺失时创建并登记，再统一写入弹匣。
        if (!weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state))
        {
            state = new WeaponRuntimeState();
            weapon.Add(identity, state);
        }

        state.magazine = initialMagazine;
    }

    /// <summary>武器宿主及表现读取状态，返回值复制，不暴露可写记录。</summary>
    /// <param name="identity">中文含义：身份标识。待查询的武器身份编号。</param>
    /// <returns>已登记武器的快照，未登记时返回空弹匣和无装填状态。</returns>
    public WeaponRuntimeSnapshot Query(int identity)
    {
        // state 只在查询成功时读取，其值不会传出模块。
        if (weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state))
        {
            return new WeaponRuntimeSnapshot(state.magazine, state.reloadTransaction >= 0, state.lastFireTime, state.reloadTransaction);
        }

        return new WeaponRuntimeSnapshot(0, false, 0f, -1);
    }

    /// <summary>TryFire 和武器宿主共用的只读审批，依次检查登记、装填冲突、弹匣与冷却。</summary>
    /// <param name="identity">中文含义：身份标识。申请开火的武器身份。</param>
    /// <param name="knife">中文含义：是否小刀。兼容小刀攻击是否免除弹匣消耗检查。</param>
    /// <param name="currentTime">中文含义：当前时间。宿主传入的当前时间。</param>
    /// <param name="interval">中文含义：间隔。本武器射击间隔。</param>
    /// <returns>首个拒绝原因，通过时为 None。</returns>
    public RejectionReason CheckFire(int identity, bool knife, float currentTime, float interval)
    {
        // state 是当前武器记录，仅用于审批读取。
        if (!weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state))
        {
            return RejectionReason.NotYetReady;
        }

        if (state.reloadTransaction >= 0)
        {
            return RejectionReason.ActionConflict;
        }

        if (!knife && state.magazine <= 0)
        {
            return RejectionReason.AssetInsufficient;
        }

        if (currentTime - state.lastFireTime < interval)
        {
            return RejectionReason.CooldownUnfinished;
        }

        return RejectionReason.None;
    }

    /// <summary>武器开火链调用，审批通过后同步扣弹并提交冷却时间；失败不改状态。</summary>
    /// <param name="identity">中文含义：身份标识。申请开火的武器身份。</param>
    /// <param name="knife">中文含义：是否小刀。是否按兼容小刀语义免除扣弹。</param>
    /// <param name="currentTime">中文含义：当前时间。本次成功开火使用的宿主时间。</param>
    /// <param name="interval">中文含义：间隔。本武器射击间隔。</param>
    /// <returns>None 表示已经提交，否则为审批拒绝原因。</returns>
    public RejectionReason TryFire(int identity, bool knife, float currentTime, float interval)
    {
        // reason 保留一次审批结果，不重复执行可能随状态变化的检查。
        // 中文含义：reason 为“原因”。
var reason = CheckFire(identity, knife, currentTime, interval);
        if (reason != RejectionReason.None)
        {
            return reason;
        }

        // 审批已经保证记录存在，此处取得同一记录提交状态。
        // 中文含义：state 为“当前武器运行数据状态”。
var state = weapon[identity];
        if (!knife)
        {
            state.magazine--;
        }

        state.lastFireTime = currentTime;
        return RejectionReason.None;
    }

    /// <summary>宿主开始换弹事务时调用，拒绝无效编号、已有事务及满弹匣。</summary>
    /// <param name="identity">中文含义：身份标识。武器身份编号。</param>
    /// <param name="capacity">中文含义：容量。宿主配置的弹匣容量。</param>
    /// <param name="transaction">中文含义：事务。动作协调器分配的非负装填事务编号。</param>
    /// <returns>是否登记本次装填事务。</returns>
    public bool BeginReload(int identity, int capacity, int transaction)
    {
        // state 是待进入装填的武器记录；保持编号检查在字典查询之前的短路顺序。
        if (transaction < 0 || !weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state) || state.reloadTransaction >= 0 || state.magazine >= capacity)
        {
            return false;
        }

        state.reloadTransaction = transaction;
        return true;
    }

    /// <summary>装填动画标记经宿主转发到此处；校验事务后在同一调用内扣备弹并补弹匣，旧或重复回调不扣库存。</summary>
    /// <param name="identity">中文含义：身份标识。装填武器的身份。</param>
    /// <param name="capacity">中文含义：容量。弹匣容量上限。</param>
    /// <param name="transaction">中文含义：事务。标记所属装填事务，必须匹配当前记录。</param>
    /// <param name="availableReserveAmmo">中文含义：可用背包备弹。宿主从正式背包或兼容适配器读取的可用备弹。</param>
    /// <param name="singleShot">中文含义：是否单一射击。是否逐发装填；为 true 时本次最多填入一发。</param>
    /// <param name="deductReserveAmmo">中文含义：扣除背包备弹。宿主提供的同步扣除入口；为空时沿用旧行为只提交弹匣。</param>
    /// <returns>本次提交到弹匣的数量。</returns>
    public int CommitReload(int identity, int capacity, int transaction, int availableReserveAmmo, bool singleShot, System.Action<int> deductReserveAmmo)
    {
        // state 是当前装填记录，编号核验成功前不调用外部扣除委托。
        if (!weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state) || transaction < 0 || state.reloadTransaction != transaction)
        {
            return 0;
        }

        // 同时受弹匣空位和外部可用备弹限制的本次提交量。
        // 中文含义：count 为“数量”。
int count = System.Math.Max(0, System.Math.Min(capacity - state.magazine, availableReserveAmmo));
        if (singleShot)
        {
            count = System.Math.Min(1, count);
        }

        if (count > 0)
        {
            if (deductReserveAmmo != null)
            {
                deductReserveAmmo(count);
            }

            state.magazine += count;
        }

        if (!singleShot || state.magazine >= capacity || availableReserveAmmo <= count)
        {
            state.reloadTransaction = -1;
        }

        return count;
    }

    /// <summary>动作取消或武器退出链调用；仅失效装填编号，保留已提交弹药和冷却。</summary>
    /// <param name="identity">中文含义：身份标识。要取消装填的武器身份。</param>
    public void CancelReload(int identity)
    {
        // state 接收已登记武器记录，未登记时保持无操作。
        if (weapon.TryGetValue(identity, out var /* 中文含义：当前武器运行数据状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ state))
        {
            state.reloadTransaction = -1;
        }
    }
}
