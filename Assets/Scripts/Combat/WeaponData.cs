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
    /// <summary>中文含义：武器类型。使用位置：作为 WeaponRuntimeModule.CheckFire(int, bool, float, float) 的参数、作为 WeaponRuntimeModule.TryFire(int, bool, float, float) 的参数、CrosshairUI.RefreshWeaponConfig(bool) 等共 8 处。</summary>
    public WeaponType weaponType;
    /// <summary>中文含义：武器名称。使用位置：作为 string.IsNullOrWhiteSpace(string) 的参数、CombatHUD.RefreshAmmoText()、WeaponWheelUI.ResolveDisplayName(WeaponWheelUI.WeaponSlot)。</summary>
    public string weaponName;
    /// <summary>中文含义：基础伤害。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DestructibleWoodenBox.TakeDamage(float, Vector3) 的参数、作为 WeaponBase.ProcessHit(RaycastHit, float) 的参数。</summary>
    [Header("Damage")]
    public float baseDamage = 15f;
    /// <summary>中文含义：爆头倍率。</summary>
    public float headshotMultiplier = 1.5f;
    /// <summary>中文含义：身体倍率。</summary>
    public float bodyMultiplier = 1.0f;
    /// <summary>中文含义：肢体倍率。</summary>
    public float limbMultiplier = 0.7f;
    /// <summary>中文含义：开火速率。使用位置：WeaponBase.GetFireInterval()。</summary>
    [Header("Fire Rate")]
    public float fireRate = 0.4f;
    /// <summary>中文含义：弹匣尺寸。使用位置：作为 WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;) 的参数、作为 WeaponRuntimeModule.BeginReload(int, int, int) 的参数、作为 WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;) 的参数 等共 11 处。</summary>
    public int magazineSize = 12;
    /// <summary>中文含义：换弹时间。使用位置：作为 MonoBehaviour.Invoke(string, float) 的参数。</summary>
    public float reloadTime = 2.17f;
    /// <summary>中文含义：开火模式。射击弹道模式：Single=单发精准, Pellet=散弹散射。使用位置：LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.Update()、LeonCombat.UpdateShootStateLock() 等共 4 处。</summary>
    [Header("Fire Mode")]
    [Tooltip("射击弹道模式：Single=单发精准, Pellet=散弹散射")]
    public FireMode fireMode = FireMode.Single;
    /// <summary>中文含义：射线检测最大距离。使用位置：作为 WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit) 的参数、LeonWeaponHandler.Fire()、RE4CameraController.GetCurrentWeaponRange()。</summary>
    [Header("Range")]
    [Tooltip("射线检测最大距离")]
    public float range = 100f;
    /// <summary>中文含义：基础散布。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    [Header("Spread")]
    public float baseSpread = 0.02f;
    /// <summary>中文含义：最大散布。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Min(float, float) 的参数、WeaponBase.GetSpreadNormalized()。</summary>
    public float maxSpread = 0.08f;
    /// <summary>中文含义：散布恢复。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float spreadRecovery = 2f;
    /// <summary>中文含义：后坐力大小。使用位置：作为 RE4CameraController.AddWeaponRecoil(float) 的参数。</summary>
    [Header("Recoil")]
    public float recoilAmount = 2f;
    /// <summary>中文含义：后坐力恢复。</summary>
    public float recoilRecovery = 5f;
    /// <summary>中文含义：该武器使用的摄像机配置。使用位置：RE4CameraController.GetWeaponProfile(WeaponType)。</summary>
    [Header("Camera")]
    [Tooltip("该武器使用的摄像机配置")]
    public CameraProfile cameraProfile;
    /// <summary>中文含义：初始备弹数量。</summary>
    [Header("Ammo Reserve")]
    [Tooltip("初始备弹数量")]
    public int reserveAmmo = 0;
    /// <summary>中文含义：霰弹弹丸数量。使用位置：作为 WeaponBase.ProcessHit(RaycastHit, float) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    [Header("Pellet (Pellet 模式专用)")]
    public int pelletCount = 6;
    /// <summary>中文含义：霰弹弹丸散布角度。使用位置：WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    public float pelletSpreadAngle = 5f;
    /// <summary>中文含义：枪声听觉半径。使用位置：作为 CombatEventDispatcher.PublishWeaponFired(Vector3, float) 的参数。</summary>
    [Header("Audio & VFX (预留)")]
    [Min(0f)]
    public float gunshotHearingRadius = 18f;
    /// <summary>中文含义：开火声音。使用位置：作为 AudioManager.PlaySFX3D(AudioClip, Vector3) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    public AudioClip fireSound;
    /// <summary>中文含义：换弹声音。</summary>
    public AudioClip reloadSound;
    /// <summary>中文含义：枪口闪光。</summary>
    public GameObject muzzleFlash;
    /// <summary>中文含义：冲击效果。</summary>
    public GameObject impactEffect;
    /// <summary>中文含义：该武器的准心配置资产。使用位置：CrosshairUI.RefreshWeaponConfig(bool)、WeaponBase.GetCrosshairConfig()。</summary>
    [Header("Crosshair")]
    [Tooltip("该武器的准心配置资产")]
    public CrosshairConfig crosshairConfig;
    /// <summary>中文含义：当前武器在战斗界面中显示的图标，可直接从图集子图片拖入。使用位置：CombatHUD.RefreshAmmoText()、WeaponWheelUI.ResolveIcon(WeaponWheelUI.WeaponSlot, WeaponBase)。</summary>
    [Header("界面显示")]
    [Tooltip("当前武器在战斗界面中显示的图标，可直接从图集子图片拖入。")]
    public Sprite hudIcon;
}
