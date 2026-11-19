using System;

/// <summary>
/// 音量服务接口：界面模块通过此接口控制音频音量
/// 界面模块不得直接访问 PlayerPrefs、AudioListener.volume 或 AudioMixer
/// </summary>
public interface IAudioVolumeService
{
    /// <summary>中文含义：主控音量。当前总音量（0~1）</summary>
    float MasterVolume { get; }

    /// <summary>中文含义：主控音量变化。总音量变化事件</summary>
    event Action<float> MasterVolumeChanged;
    /// <summary>设置总音量（0~1），立即生效</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.OnMasterVolumeChanged(float)、SettingsApplicationAdapter.ResetPreviousPanelSet(IAudioVolumeService)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。</param>
    void SetMasterVolume(float value);
    /// <summary>保存音量设置</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.SaveAllSettings()、OptionsTabManager.Hide()、SettingsApplicationAdapter.ResetPreviousPanelSet(IAudioVolumeService)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void SaveVolumeSettings();
    /// <summary>获取分类音量（0~1）</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.LoadVolumeSettings()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">中文含义：类别。</param>
    float GetCategoryVolume(AudioCategory category);
    /// <summary>设置分类音量（0~1），立即生效</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.OnBgmChanged(float)、OptionsMenu.OnSfxChanged(float)、SettingsApplicationAdapter.ResetPreviousPanelSet(IAudioVolumeService)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">中文含义：类别。</param>
    /// <param name="value">中文含义：值。</param>
    void SetCategoryVolume(AudioCategory category, float value);
    /// <summary>中文含义：类别音量变化。分类音量变化事件</summary>
    event Action<AudioCategory, float> CategoryVolumeChanged;
}
