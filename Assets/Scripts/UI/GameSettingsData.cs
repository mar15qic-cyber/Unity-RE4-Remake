using UnityEngine;

/// <summary>设置数据的持久化入口；沿用原键，不保存第二份运行时副本。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "设置数据")]
public static class GameSettingsData
{
    /// <summary>Exists 的业务处理入口；本方法直接依赖 PlayerPrefs.HasKey(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.GetAntiAliasingIndex()、GraphicsSettingsRuntime.SetQualityPreset(int)；下游（含内部回调体） PlayerPrefs.HasKey(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    public static bool Exists(string key) => PlayerPrefs.HasKey(key);
    /// <summary>ReadInteger 的业务处理入口；本方法直接依赖 PlayerPrefs.GetInt(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.GetAntiAliasingIndex()、GraphicsSettingsRuntime.GetFrameRateIndex()、GraphicsSettingsRuntime.GetGraphicsApiPreference()、GraphicsSettingsRuntime.GetQualityPreset() 等共 10 处；下游（含内部回调体） PlayerPrefs.GetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">默认值。</param>
    public static int ReadInteger(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);
    /// <summary>ReadSmallCount 的业务处理入口；本方法直接依赖 PlayerPrefs.GetFloat(string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()；下游（含内部回调体） PlayerPrefs.GetFloat(string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">默认值。</param>
    public static float ReadSmallCount(string key, float defaultValue = 0) => PlayerPrefs.GetFloat(key, defaultValue);
    /// <summary>ReadText 的业务处理入口；本方法直接依赖 PlayerPrefs.GetString(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PlayerPrefs.GetString(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="defaultValue">默认值。</param>
    public static string ReadText(string key, string defaultValue = "") => PlayerPrefs.GetString(key, defaultValue);
    /// <summary>WriteInteger 的业务处理入口；本方法直接依赖 PlayerPrefs.SetInt(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.SetAntiAliasingIndex(int)、GraphicsSettingsRuntime.SetFrameRateIndex(int)、GraphicsSettingsRuntime.SetGraphicsApiPreference(int)、GraphicsSettingsRuntime.SetQualityPreset(int)、GraphicsSettingsRuntime.SetVSync(bool) 等共 10 处；下游（含内部回调体） PlayerPrefs.SetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="value">值。</param>
    public static void WriteInteger(string key, int value) => PlayerPrefs.SetInt(key, value);
    /// <summary>WriteSmallCount 的业务处理入口；本方法直接依赖 PlayerPrefs.SetFloat(string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsApplicationAdapter.ModifySensitivity(float, float, float, RE4CameraController)、SettingsApplicationAdapter.ResetPreviousPanelSet(IAudioVolumeService)；下游（含内部回调体） PlayerPrefs.SetFloat(string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="value">值。</param>
    public static void WriteSmallCount(string key, float value) => PlayerPrefs.SetFloat(key, value);
    /// <summary>WriteText 的业务处理入口；本方法直接依赖 PlayerPrefs.SetString(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PlayerPrefs.SetString(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="key">键。</param>
    /// <param name="value">值。</param>
    public static void WriteText(string key, string value) => PlayerPrefs.SetString(key, value);
    /// <summary>SaveValue 的业务处理入口；本方法直接依赖 PlayerPrefs.Save()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.SetGraphicsApiPreference(int)、OptionsMenu.SaveAllSettings()、OptionsTabManager.Hide()、SettingsApplicationAdapter.ModifyAimMode(int)、SettingsApplicationAdapter.ModifyVerticalAxis(bool, RE4CameraController) 等共 6 处；下游（含内部回调体） PlayerPrefs.Save()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void SaveValue() => PlayerPrefs.Save();
}
