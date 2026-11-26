/// <summary>
/// 音效事件名称常量
/// 业务脚本通过这些常量引用事件名称，避免硬编码字符串
/// </summary>
public static class AudioEventNames
{
    // 武器
    /// <summary>中文含义：手枪开火。编译期常量，保持既有值与消费者映射。</summary>
    public const string PistolFire = "pistol_fire";
    /// <summary>中文含义：手枪无弹开火。编译期常量，保持既有值与消费者映射。</summary>
    public const string PistolDryFire = "pistol_dry_fire";
    /// <summary>中文含义：手枪换弹。编译期常量，保持既有值与消费者映射。</summary>
    public const string PistolReload = "pistol_reload";
    /// <summary>中文含义：步枪开火。编译期常量，保持既有值与消费者映射。</summary>
    public const string RifleFire = "rifle_fire";
    /// <summary>中文含义：步枪无弹开火。编译期常量，保持既有值与消费者映射。</summary>
    public const string RifleDryFire = "rifle_dry_fire";
    /// <summary>中文含义：步枪换弹。编译期常量，保持既有值与消费者映射。</summary>
    public const string RifleReload = "rifle_reload";
    /// <summary>中文含义：近战攻击。编译期常量，保持既有值与消费者映射。</summary>
    public const string MeleeAttack = "melee_attack";
    /// <summary>近战命中。编译期常量，保持既有值与消费者映射。</summary>
    public const string MeleeHit = "melee_hit";
    public const string MeleeHitNormal = "melee_hit_normal";
    public const string MeleeHitCritical = "melee_hit_critical";
    public const string MeleeHitCriticalCrack = "melee_hit_critical_crack";
    public const string MeleeHitCriticalTail = "melee_hit_critical_tail";
    /// <summary>村庄门打开。编译期常量，保持既有值与消费者映射。</summary>
    public const string VillageDoorOpen = "village_door_open";
    /// <summary>电锯起始。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawStart = "chainsaw_start";
    /// <summary>电锯待机循环。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawIdleLoop = "chainsaw_idle_loop";
    /// <summary>电锯反向。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawRev = "chainsaw_rev";
    /// <summary>电锯攻击。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawAttack = "chainsaw_attack";
    /// <summary>电锯命中。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawHit = "chainsaw_hit";
    /// <summary>电锯停止。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawStop = "chainsaw_stop";
    // 脚步
    /// <summary>中文含义：脚步声行走。编译期常量，保持既有值与消费者映射。</summary>
    public const string FootstepWalk = "footstep_walk";
    /// <summary>中文含义：脚步声运行。编译期常量，保持既有值与消费者映射。</summary>
    public const string FootstepRun = "footstep_run";
    // 敌人
    /// <summary>中文含义：敌人攻击。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieAttack = "zombie_attack";
    /// <summary>中文含义：敌人撕咬。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieBite = "zombie_bite";
    /// <summary>中文含义：敌人已蓄力攻击。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieChargedAttack = "zombie_charged_attack";
    /// <summary>中文含义：敌人受伤。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieHurt = "zombie_hurt";
    /// <summary>中文含义：敌人爆头。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieHeadshot = "zombie_headshot";
    /// <summary>中文含义：敌人肢体命中。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieLimbHit = "zombie_limb_hit";
    /// <summary>中文含义：敌人疼痛。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombiePain = "zombie_pain";
    /// <summary>中文含义：敌人死亡。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieDeath = "zombie_death";
    /// <summary>中文含义：敌人窒息。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieChoke = "zombie_choke";
    /// <summary>中文含义：敌人血腥效果。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieGory = "zombie_gory";
    /// <summary>中文含义：敌人待机。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieIdle = "zombie_idle";
    /// <summary>中文含义：敌人低吼。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieGrunt = "zombie_grunt";
    /// <summary>中文含义：敌人喉部。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieThroat = "zombie_throat";
    /// <summary>中文含义：敌人呼吸。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieBreathing = "zombie_breathing";
    /// <summary>中文含义：敌人检测。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieDetection = "zombie_detection";
    /// <summary>中文含义：敌人尖叫。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieScream = "zombie_scream";
    /// <summary>中文含义：敌人移动行为。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieLocomotion = "zombie_locomotion";
    /// <summary>中文含义：敌人地面。编译期常量，保持既有值与消费者映射。</summary>
    public const string ZombieGround = "zombie_ground";
    // 受击
    /// <summary>中文含义：命中身体。编译期常量，保持既有值与消费者映射。</summary>
    public const string HitBody = "hit_body";
    /// <summary>中文含义：命中头部。编译期常量，保持既有值与消费者映射。</summary>
    public const string HitHead = "hit_head";
    /// <summary>中文含义：命中肢体。编译期常量，保持既有值与消费者映射。</summary>
    public const string HitLimb = "hit_limb";
    /// <summary>玩家命中。编译期常量，保持既有值与消费者映射。</summary>
    public const string PlayerHit = "player_hit";
    public const string PlayerHitLight = "player_hit_light";
    public const string PlayerHitHeavy = "player_hit_heavy";
    /// <summary>玩家死亡。编译期常量，保持既有值与消费者映射。</summary>
    public const string PlayerDeath = "player_death";
    /// <summary>玩家呼吸。编译期常量，保持既有值与消费者映射。</summary>
    public const string PlayerBreathing = "player_breathing";
    /// <summary>玩家运行呼吸。编译期常量，保持既有值与消费者映射。</summary>
    public const string PlayerRunBreathing = "player_run_breathing";
    /// <summary>玩家武器切换。编译期常量，保持既有值与消费者映射。</summary>
    public const string PlayerWeaponSwitch = "player_weapon_switch";
    // 界面
    /// <summary>界面悬停。编译期常量，保持既有值与消费者映射。</summary>
    public const string UIHover = "ui_hover";
    /// <summary>界面确认。编译期常量，保持既有值与消费者映射。</summary>
    public const string UIConfirm = "ui_confirm";
    /// <summary>中文含义：界面取消。编译期常量，保持既有值与消费者映射。</summary>
    public const string UICancel = "ui_cancel";

    public const string UITitleStart = "ui_title_start";
    public const string UITitleHover = "ui_title_hover";
    public const string UITitleConfirm = "ui_title_confirm";
    public const string UIQuitConfirm = "ui_quit_confirm";
    public const string UIOpen = "ui_open";
    public const string UIClose = "ui_close";
    public const string UIPage = "ui_page";
    public const string UIError = "ui_error";

    public const string UISettingsOpen = "ui_settings_open";
    public const string UISettingsHover = "ui_settings_hover";
    public const string UISettingsAdjust = "ui_settings_adjust";
    public const string UISettingsConfirm = "ui_settings_confirm";
    public const string UISettingsCancel = "ui_settings_cancel";
    public const string UISettingsError = "ui_settings_error";
    public const string UISettingsTab = "ui_settings_tab";

    public const string UICaseOpen = "ui_case_open";
    public const string UICaseClose = "ui_case_close";
    public const string UICaseHover = "ui_case_hover";
    public const string UICaseConfirm = "ui_case_confirm";
    public const string UICaseCancel = "ui_case_cancel";
    public const string UICaseError = "ui_case_error";
    public const string UICaseManipulate = "ui_case_manipulate";
    public const string UICaseCraft = "ui_case_craft";
    // 拾取与使用
    /// <summary>中文含义：物品拾取。编译期常量，保持既有值与消费者映射。</summary>
    public const string ItemPickup = "item_pickup";
    /// <summary>中文含义：物品使用。编译期常量，保持既有值与消费者映射。</summary>
    public const string ItemUse = "item_use";
    public const string ItemPickupAmmo = "item_pickup_ammo";
    public const string ItemPickupHealing = "item_pickup_healing";
    public const string ItemPickupMaterial = "item_pickup_material";
    public const string ItemPickupKey = "item_pickup_key";
    public const string ItemPickupW870 = "item_pickup_w870";
    public const string ItemHeal = "item_heal";
    // 寄存箱
    /// <summary>中文含义：仓库箱体打开。编译期常量，保持既有值与消费者映射。</summary>
    public const string StorageBoxOpen = "box_open";
    /// <summary>中文含义：仓库箱体关闭。编译期常量，保持既有值与消费者映射。</summary>
    public const string StorageBoxClose = "box_close";
}
