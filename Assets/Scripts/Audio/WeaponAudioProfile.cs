using UnityEngine;

/// <summary>
/// 武器音效配置：手枪、步枪等武器的射击和换弹音效事件
/// 消除 WeaponData 与 WeaponAudio 之间的重复数据源
/// </summary>
[CreateAssetMenu(fileName = "WeaponAudioProfile", menuName = "音频/武器配置")]
public class WeaponAudioProfile : ScriptableObject
{
    /// <summary>中文含义：武器类型标识。</summary>
    [Header("武器标识")]
    [Tooltip("武器类型标识")]
    public string weaponId;
    /// <summary>中文含义：射击音效事件。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、WeaponAudio.PlayFire()。</summary>
    [Header("射击")]
    [Tooltip("射击音效事件")]
    public AudioEventData fireEvent;
    /// <summary>中文含义：弹匣为空时扣动扳机的卡壳音效事件。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、WeaponAudio.PlayDryFire()。</summary>
    [Header("卡壳")]
    [Tooltip("弹匣为空时扣动扳机的卡壳音效事件")]
    public AudioEventData dryFireEvent;
    /// <summary>中文含义：换弹音效事件。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、WeaponAudio.PlayReload()。</summary>
    [Header("换弹")]
    [Tooltip("换弹音效事件")]
    public AudioEventData reloadEvent;
    /// <summary>中文含义：近战攻击音效事件（无素材时留空）。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、WeaponAudio.PlayMelee()。</summary>
    [Header("近战")]
    [Tooltip("近战攻击音效事件（无素材时留空）")]
    public AudioEventData meleeEvent;

    [Header("动作标记")]
    [Tooltip("正式换弹从装填和机构动作标记播放")]
    public bool stagedReload;
    [Tooltip("取出弹匣时播放")]
    public AudioEventData magazineOutEvent;
    [Tooltip("装填后的枪械机构声")]
    public AudioEventData mechanismEvent;
    [Tooltip("收枪时播放")]
    public AudioEventData holsterEvent;
    [Tooltip("掏枪时播放")]
    public AudioEventData equipEvent;
    [Tooltip("武器就位时播放")]
    public AudioEventData equipSettledEvent;

    [Header("枪弹碰撞")]
    [Tooltip("枪弹命中身体或四肢时播放")]
    public AudioEventData bodyImpactEvent;
    [Tooltip("枪弹命中头部时播放")]
    public AudioEventData headImpactEvent;
}
