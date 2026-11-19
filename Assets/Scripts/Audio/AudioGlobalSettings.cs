using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全局音频设置：对象池容量、混音器引用等全局配置
/// </summary>
[CreateAssetMenu(fileName = "AudioGlobalSettings", menuName = "音频/全局设置")]
public class AudioGlobalSettings : ScriptableObject
{
    /// <summary>中文含义：三维音源对象池初始容量。使用位置：AudioManager.Awake()。</summary>
    [Header("对象池")]
    [Range(4, 64)]
    [Tooltip("三维音源对象池初始容量")]
    public int poolInitialSize = 16;
    /// <summary>中文含义：三维音源对象池最大扩展数量。使用位置：AudioManager.Awake()。</summary>
    [Range(8, 128)]
    [Tooltip("三维音源对象池最大扩展数量")]
    public int poolMaxSize = 64;
    /// <summary>中文含义：主混音器资产。使用位置：AudioManager.GetMixerGroupByPath(string)、AudioManager.GetSourceVolume(AudioCategory, float)、AudioManager.InitializeMixerGroups() 等共 8 处。</summary>
    [Header("混音器")]
    [Tooltip("主混音器资产")]
    public AudioMixer masterMixer;
    /// <summary>中文含义：总音量分组路径。</summary>
    [Header("混音分组路径")]
    [Tooltip("总音量分组路径")]
    public string masterGroupPath = "Master";
    /// <summary>中文含义：背景音乐分组路径。</summary>
    [Tooltip("背景音乐分组路径")]
    public string bgmGroupPath = "Master/背景音乐";
    /// <summary>中文含义：环境声音分组路径。</summary>
    [Tooltip("环境声音分组路径")]
    public string ambientGroupPath = "Master/环境声音";
    /// <summary>中文含义：武器声音分组路径。</summary>
    [Tooltip("武器声音分组路径")]
    public string weaponGroupPath = "Master/武器声音";
    /// <summary>中文含义：玩家声音分组路径。</summary>
    [Tooltip("玩家声音分组路径")]
    public string playerGroupPath = "Master/玩家声音";
    /// <summary>中文含义：敌人声音分组路径。</summary>
    [Tooltip("敌人声音分组路径")]
    public string enemyGroupPath = "Master/敌人声音";
    /// <summary>中文含义：脚步声音分组路径。</summary>
    [Tooltip("脚步声音分组路径")]
    public string footstepGroupPath = "Master/脚步声音";
    /// <summary>中文含义：界面声音分组路径。</summary>
    [Tooltip("界面声音分组路径")]
    public string uiGroupPath = "Master/界面声音";
    /// <summary>中文含义：总音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float) 等共 4 处。</summary>
    [Header("混音器参数名")]
    [Tooltip("总音量参数名")]
    public string masterVolumeParam = "MasterVolume";
    /// <summary>中文含义：背景音乐音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("背景音乐音量参数名")]
    public string bgmVolumeParam = "BGMVolume";
    /// <summary>中文含义：环境声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("环境声音音量参数名")]
    public string ambientVolumeParam = "AmbientVolume";
    /// <summary>中文含义：通用音效音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("通用音效音量参数名")]
    public string sfxVolumeParam = "SFXVolume";
    /// <summary>中文含义：武器声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("武器声音音量参数名")]
    public string weaponVolumeParam = "WeaponVolume";
    /// <summary>中文含义：玩家声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("玩家声音音量参数名")]
    public string playerVolumeParam = "PlayerVolume";
    /// <summary>中文含义：敌人声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("敌人声音音量参数名")]
    public string enemyVolumeParam = "EnemyVolume";
    /// <summary>中文含义：脚步声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("脚步声音音量参数名")]
    public string footstepVolumeParam = "FootstepVolume";
    /// <summary>中文含义：界面声音音量参数名。使用位置：作为 string.IsNullOrEmpty(string) 的参数、AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)。</summary>
    [Tooltip("界面声音音量参数名")]
    public string uiVolumeParam = "UIVolume";
    /// <summary>中文含义：是否在启动时自动播放背景音乐。使用位置：AudioManager.Start()。</summary>
    [Header("背景音乐")]
    [Tooltip("是否在启动时自动播放背景音乐")]
    public bool autoPlayBGM = false;
    /// <summary>中文含义：自动播放的背景音乐事件。使用位置：作为 AudioManager.PlayBGM(AudioEventData) 的参数、AudioManager.Start()。</summary>
    [Tooltip("自动播放的背景音乐事件")]
    public AudioEventData bgmEvent;
    /// <summary>中文含义：背景音乐淡入时间（秒）。使用位置：AudioManager.PlayBGM(AudioEventData)。</summary>
    [Tooltip("背景音乐淡入时间（秒）")]
    public float bgmFadeInDuration = 1f;
    /// <summary>中文含义：背景音乐淡出时间（秒）。使用位置：AudioManager.StopBGM()。</summary>
    [Tooltip("背景音乐淡出时间（秒）")]
    public float bgmFadeOutDuration = 1f;
}
