using UnityEngine;

/// <summary>只应用已经获准的设置修改，不访问视图或自行查找场景。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "设置应用适配器")]
public static class SettingsApplicationAdapter
{
    /// <summary>ApplyHasSaveSensitivity 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、RE4CameraController.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()；下游（含内部回调体） object.ReferenceEquals(object, object)、RE4CameraController.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontalDirection">水平方向。</param>
    /// <param name="verticalDirection">竖直方向。</param>
    /// <param name="aim">瞄准。</param>
    /// <param name="camera">摄像机。</param>
    public static void ApplyHasSaveSensitivity(float horizontalDirection, float verticalDirection, float aim, RE4CameraController camera)
    {
        // 判空前缓存的摄像机。
global::RE4CameraController receiverValue1 = camera;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.SetLookSensitivity(horizontalDirection, verticalDirection, aim, false);
        }
    }

    /// <summary>ApplyFullScreen 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.LoadDisplaySettings()、SettingsApplicationAdapter.ModifyFullScreen(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fullScreenValue">是否完整屏幕值。</param>
    public static void ApplyFullScreen(bool fullScreenValue) => Screen.fullScreen = fullScreenValue;
    /// <summary>ModifyFullScreen 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、SettingsApplicationAdapter.ApplyFullScreen(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.OnFullscreenToggled(bool)、OptionsTabManager.OnFullscreenChanged(bool)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、SettingsApplicationAdapter.ApplyFullScreen(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fullScreen">是否完整屏幕。</param>
    public static void ModifyFullScreen(bool fullScreen)
    {
        // 按原顺序缓存的实参文字“Fullscreen”。
string argumentValue3 = "Fullscreen";
        // 分支选出的1。
int selectedValue2;
        if (fullScreen)
        {
            selectedValue2 = 1;
        }
        else
        {
            selectedValue2 = 0;
        }

        GameSettingsData.WriteInteger(argumentValue3, selectedValue2);
        ApplyFullScreen(fullScreen);
    }

    /// <summary>ModifyResolution 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、Screen.SetResolution(int, int, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.OnResolutionChanged(int)、OptionsTabManager.OnResolutionChanged(int)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、Screen.SetResolution(int, int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    /// <param name="fullScreen">是否完整屏幕。</param>
    /// <param name="previousMenu">是否前一次菜单。</param>
    public static void ModifyResolution(int width, int height, bool fullScreen, bool previousMenu)
    {
        // 分支选出的文字“Resolution_Width”。
string selectedValue4;
        if (previousMenu)
        {
            selectedValue4 = "Resolution_Width";
        }
        else
        {
            selectedValue4 = "ResolutionWidth";
        }

        GameSettingsData.WriteInteger(selectedValue4, width);
        // 分支选出的文字“Resolution_Height”。
string selectedValue5;
        if (previousMenu)
        {
            selectedValue5 = "Resolution_Height";
        }
        else
        {
            selectedValue5 = "ResolutionHeight";
        }

        GameSettingsData.WriteInteger(selectedValue5, height);
        Screen.SetResolution(width, height, fullScreen);
    }

    /// <summary>ModifyVerticalAxis 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、object.ReferenceEquals(object, object)、RE4CameraController.SetInvertY(bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnInvertYChanged(bool)；下游（含内部回调体） GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、object.ReferenceEquals(object, object)、RE4CameraController.SetInvertY(bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reverse">是否反向。</param>
    /// <param name="camera">摄像机。</param>
    public static void ModifyVerticalAxis(bool reverse, RE4CameraController camera)
    {
        // 按原顺序缓存的实参文字“InvertY”。
string argumentValue7 = "InvertY";
        // 分支选出的1。
int selectedValue6;
        if (reverse)
        {
            selectedValue6 = 1;
        }
        else
        {
            selectedValue6 = 0;
        }

        GameSettingsData.WriteInteger(argumentValue7, selectedValue6);
        GameSettingsData.SaveValue();
        // 判空前缓存的摄像机。
global::RE4CameraController receiverValue8 = camera;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.SetInvertY(reverse, false);
        }
    }

    /// <summary>ModifySensitivity 的业务处理入口；本方法直接依赖 GameSettingsData.WriteSmallCount(string, float)、object.ReferenceEquals(object, object)、RE4CameraController.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnSensitivityChanged(float)；下游（含内部回调体） GameSettingsData.WriteSmallCount(string, float)、object.ReferenceEquals(object, object)、RE4CameraController.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontalDirection">水平方向。</param>
    /// <param name="verticalDirection">竖直方向。</param>
    /// <param name="aim">瞄准。</param>
    /// <param name="camera">摄像机。</param>
    public static void ModifySensitivity(float horizontalDirection, float verticalDirection, float aim, RE4CameraController camera)
    {
        // 判空前缓存的摄像机。
global::RE4CameraController receiverValue9 = camera;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.SetLookSensitivity(horizontalDirection, verticalDirection, aim);
        }

        GameSettingsData.WriteSmallCount(RE4RThirdPersonCamera.HorizontalSensitivityPreferenceKey, horizontalDirection);
        GameSettingsData.WriteSmallCount(RE4RThirdPersonCamera.VerticalSensitivityPreferenceKey, verticalDirection);
        GameSettingsData.WriteSmallCount(RE4RThirdPersonCamera.AimSensitivityPreferenceKey, aim);
    }

    /// <summary>ModifyAimMode 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.SetAimMode(int)；下游（含内部回调体） GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">模式。</param>
    public static void ModifyAimMode(int mode)
    {
        GameSettingsData.WriteInteger("AimMode", mode);
        GameSettingsData.SaveValue();
    }

    /// <summary>ResetPreviousPanelSet 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、GameSettingsData.WriteSmallCount(string, float)、IAudioVolumeService.SaveVolumeSettings()、IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.ResetAllSettings()；下游（含内部回调体） GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、GameSettingsData.WriteSmallCount(string, float)、IAudioVolumeService.SaveVolumeSettings()、IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="volumeService">音量服务。</param>
    public static void ResetPreviousPanelSet(IAudioVolumeService volumeService)
    {
        // 重置图像设置
        GameSettingsData.WriteInteger("QualityLevel", 2);
        GameSettingsData.WriteInteger("Resolution", 0);
        GameSettingsData.WriteInteger("Fullscreen", 1);
        // 重置音频设置
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue10 = // 重置音频设置
 volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.SetMasterVolume(1f);
        }

        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue11 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.SetCategoryVolume(AudioCategory.BGM, 0.8f);
        }

        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue12 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            receiverValue12.SetCategoryVolume(AudioCategory.SFX, 1f);
        }

        // 重置控制设置
        GameSettingsData.WriteSmallCount("Sensitivity_X", 1f);
        GameSettingsData.WriteSmallCount("Sensitivity_Y", 1f);
        GameSettingsData.WriteSmallCount("AimSensitivity", 0.5f);
        GameSettingsData.WriteInteger("InvertY", 0);
        // 重置游戏设置
        GameSettingsData.WriteInteger("Difficulty", 1);
        GameSettingsData.WriteInteger("AutoAim", 1);
        // 重置语言设置
        GameSettingsData.WriteInteger("Language", 0);
        // 重置HUD设置
        GameSettingsData.WriteInteger("ShowHUD", 1);
        GameSettingsData.WriteInteger("ShowMinimap", 1);
        GameSettingsData.WriteInteger("ShowHealthBar", 1);
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue13 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            receiverValue13.SaveVolumeSettings();
        }

        GameSettingsData.SaveValue();
    }
}
