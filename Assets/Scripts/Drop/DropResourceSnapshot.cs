using System.Collections.Generic;

/// <summary>
/// DropResourceReader 构造的掉落资源值快照，规则只读取物品数量、武器持有、备弹和生命比例。
/// 构造时复制数量字典，不持有玩家、界面或可写物品实例；权重计算不再反向查询场景。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "掉落资源快照")]
public readonly struct DropResourceSnapshot
{
    /// <summary>中文含义：当前键值映射表。按物品标识复制的数量表；无背包时为空引用，外部字典变化不会影响它。</summary>
    private readonly Dictionary<string, int> item;
    /// <summary>中文含义：手枪背包备弹。读取时的手枪备弹。</summary>
    private readonly int pistolReserveAmmo;
    /// <summary>中文含义：步枪背包备弹。读取时的步枪备弹。</summary>
    private readonly int rifleReserveAmmo;
    /// <summary>中文含义：霰弹枪弹药背包备弹。读取时的霰弹枪备弹。</summary>
    private readonly int shotgunAmmoReserveAmmo;
    /// <summary>中文含义：是否具有武器模块。读取边界是否找到可用武器宿主。</summary>
    public bool HasWeaponModule { get; }
    /// <summary>中文含义：是否具有背包。构造时是否提供了背包数量表。</summary>
    public bool HasInventory { get; }
    /// <summary>中文含义：是否具有生命。读取边界是否取得玩家生命模块。</summary>
    public bool HasHealth { get; }
    /// <summary>中文含义：生命比例。读取时的生命比例，权重规则直接使用该值。</summary>
    public float HealthRatio { get; }

    /// <summary>读取边界汇总服务数据后构造；复制字典并保存各值，不触发服务查询或业务写入。</summary>
    /// <param name="count">中文含义：数量。物品标识到数量的汇总，允许为空。</param>
    /// <param name="weaponAvailable">中文含义：是否武器可用。武器宿主是否可用。</param>
    /// <param name="healthAvailable">中文含义：是否生命可用。生命模块是否可用。</param>
    /// <param name="healthRatio">中文含义：生命比例。读取到的生命比例。</param>
    /// <param name="pistol">中文含义：手枪。手枪备弹。</param>
    /// <param name="rifle">中文含义：步枪。步枪备弹。</param>
    /// <param name="shotgunAmmo">中文含义：霰弹枪弹药。霰弹枪备弹。</param>
    public DropResourceSnapshot(Dictionary<string, int> count, bool weaponAvailable, bool healthAvailable, float healthRatio, int pistol, int rifle, int shotgunAmmo)
    {
        if (count == null)
        {
            item = null;
        }
        else
        {
            item = new Dictionary<string, int>(count);
        }

        HasInventory = count != null;
        HasWeaponModule = weaponAvailable;
        HasHealth = healthAvailable;
        HealthRatio = healthRatio;
        pistolReserveAmmo = pistol;
        rifleReserveAmmo = rifle;
        shotgunAmmoReserveAmmo = shotgunAmmo;
    }

    /// <summary>材料权重和武器持有查询共用的只读数量入口。</summary>
    /// <param name="identifier">中文含义：标识符。物品定义标识。</param>
    /// <returns>快照中的数量；无字典、标识为空或无条目时返回零。</returns>
    public int Count(string identifier)
    {
        // count 仅在字典与标识有效且查询成功时取得，保持原短路顺序。
        if (item != null && identifier != null && item.TryGetValue(identifier, out int /* 中文含义：数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ count))
        {
            return count;
        }

        return 0;
    }

    /// <summary>弹药权重读取指定武器的备弹值，不重新访问库存。</summary>
    /// <param name="type">中文含义：当前武器类别。要查询的武器种类。</param>
    /// <returns>对应枪械的备弹，其他种类返回零。</returns>
    public int ReserveAmmo(WeaponType type)
    {
        if (type == WeaponType.Pistol)
        {
            return pistolReserveAmmo;
        }
        else if (type == WeaponType.Rifle)
        {
            return rifleReserveAmmo;
        }
        else if (type == WeaponType.Shotgun)
        {
            return shotgunAmmoReserveAmmo;
        }

        return 0;
    }

    /// <summary>弹药资格规则查询已持有武器，以既有物品标识映射读取数量。</summary>
    /// <param name="type">中文含义：当前武器类别。要检查的武器种类。</param>
    /// <returns>对应标识数量大于零时为 true；未知类型保留原空字符串查询语义。</returns>
    public bool HasWeapon(WeaponType type)
    {
        // 映射只用于快照查表，保留资源依赖的原字符串键。
        // 中文含义：identifier 为“标识符”。
string identifier = "";
        if (type == WeaponType.Pistol)
        {
            identifier = "pistol";
        }
        else if (type == WeaponType.Rifle)
        {
            identifier = "rifle";
        }
        else if (type == WeaponType.Shotgun)
        {
            identifier = "shotgun";
        }
        else if (type == WeaponType.Knife)
        {
            identifier = "knife";
        }

        return Count(identifier) > 0;
    }
}
