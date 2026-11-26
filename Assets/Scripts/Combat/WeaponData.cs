using UnityEngine;

/// <summary>武器、命中与战斗使用的 FireMode 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum FireMode
{
    /// <summary>中文含义：单一。</summary>
    Single,
    /// <summary>中文含义：爆发。</summary>
    Burst,
    /// <summary>中文含义：自动。</summary>
    Auto,
    /// <summary>中文含义：霰弹弹丸。</summary>
    Pellet
}

/// <summary>武器、命中与战斗使用的共享配置资产 WeaponData；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "RE4/Weapon Data")]
public class WeaponData : ScriptableObject
{
    /// <summary>武器类型。</summary>
    public WeaponType weaponType;
    /// <summary>武器名称。</summary>
    public string weaponName;
    /// <summary>基础伤害。</summary>
    [Header("Damage")]
    public float baseDamage = 15f;
    /// <summary>中文含义：爆头倍率。</summary>
    public float headshotMultiplier = 1.5f;
    /// <summary>中文含义：身体倍率。</summary>
    public float bodyMultiplier = 1.0f;
    /// <summary>中文含义：肢体倍率。</summary>
    public float limbMultiplier = 0.7f;

    [Range(0f, 1f)]
    [Tooltip("单发手枪命中村民头部时的暴击概率，0 表示关闭。")]
    public float headCriticalChance;
    [Min(1f)]
    [Tooltip("手枪头部暴击在原有部位倍率之外追加的伤害倍率。")]
    public float headCriticalDamageMultiplier = 1f;
    [Range(0f, 1f)]
    [Tooltip("单发手枪命中村民手臂时的暴击概率，左右臂共用，0 表示关闭。")]
    public float armCriticalChance;
    [Min(1f)]
    [Tooltip("手枪手臂暴击在原有部位倍率之外追加的伤害倍率。")]
    public float armCriticalDamageMultiplier = 1f;
    /// <summary>开火速率。</summary>
    [Header("Fire Rate")]
    public float fireRate = 0.4f;
    /// <summary>弹匣尺寸。</summary>
    public int magazineSize = 12;
    /// <summary>换弹时间。</summary>
    public float reloadTime = 2.17f;
    /// <summary>开火模式。射击弹道模式：Single=单发精准, Pellet=散弹散射。</summary>
    [Header("Fire Mode")]
    [Tooltip("射击弹道模式：Single=单发精准, Pellet=散弹散射")]
    public FireMode fireMode = FireMode.Single;
    /// <summary>射线检测最大距离。</summary>
    [Header("Range")]
    [Tooltip("射线检测最大距离")]
    public float range = 100f;
    /// <summary>基础散布。</summary>
    [Header("Spread")]
    public float baseSpread = 0.02f;
    /// <summary>最大散布。</summary>
    public float maxSpread = 0.08f;
    /// <summary>散布恢复。</summary>
    public float spreadRecovery = 2f;
    /// <summary>后坐力大小。</summary>
    [Header("Recoil")]
    public float recoilAmount = 2f;
    /// <summary>中文含义：后坐力恢复。</summary>
    public float recoilRecovery = 5f;
    /// <summary>该武器使用的摄像机配置。</summary>
    [Header("Camera")]
    [Tooltip("该武器使用的摄像机配置")]
    public CameraProfile cameraProfile;
    /// <summary>中文含义：初始备弹数量。</summary>
    [Header("Ammo Reserve")]
    [Tooltip("初始备弹数量")]
    public int reserveAmmo = 0;
    /// <summary>霰弹弹丸数量。</summary>
    [Header("Pellet (Pellet 模式专用)")]
    public int pelletCount = 6;
    /// <summary>霰弹弹丸散布角度。</summary>
    public float pelletSpreadAngle = 5f;
    /// <summary>枪声听觉半径。</summary>
    [Header("Audio & VFX (预留)")]
    [Min(0f)]
    public float gunshotHearingRadius = 18f;
    /// <summary>开火声音。</summary>
    public AudioClip fireSound;
    /// <summary>中文含义：换弹声音。</summary>
    public AudioClip reloadSound;
    /// <summary>中文含义：枪口闪光。</summary>
    public GameObject muzzleFlash;
    /// <summary>中文含义：冲击效果。</summary>
    public GameObject impactEffect;
    /// <summary>该武器的准心配置资产。</summary>
    [Header("Crosshair")]
    [Tooltip("该武器的准心配置资产")]
    public CrosshairConfig crosshairConfig;
    /// <summary>当前武器在战斗界面中显示的图标，可直接从图集子图片拖入。</summary>
    [Header("界面显示")]
    [Tooltip("当前武器在战斗界面中显示的图标，可直接从图集子图片拖入。")]
    public Sprite hudIcon;

    [Header("背枪挂点")]
    [Tooltip("拔枪交接帧中枪体根骨相对于上脊柱的位姿，由配套角色与枪体动作读取。")]
    public Pose stowedRootInSpine;
}
