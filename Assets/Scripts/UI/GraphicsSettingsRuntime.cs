using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 负责保存并应用可在运行时修改的画面设置。
/// 图形接口在程序启动前确定，因此这里只负责显示当前接口。
/// </summary>
public static class GraphicsSettingsRuntime
{
    /// <summary>质量预设键。编译期常量，保持既有值与消费者映射。</summary>
    public const string QualityPresetKey = "GraphicsQualityPreset";
    /// <summary>帧速率键。编译期常量，保持既有值与消费者映射。</summary>
    public const string FrameRateKey = "GraphicsFrameRate";
    /// <summary>向量同步键。编译期常量，保持既有值与消费者映射。</summary>
    public const string VSyncKey = "GraphicsVSync";
    /// <summary>抗锯齿键。编译期常量，保持既有值与消费者映射。</summary>
    public const string AntiAliasingKey = "GraphicsAntiAliasing";
    /// <summary>图形接口键。编译期常量，保持既有值与消费者映射。</summary>
    public const string GraphicsApiKey = "GraphicsApi";
    /// <summary>帧速率集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int[] FrameRates =
    {
        -1,
        30,
        60,
        90,
        120,
        144
    };
    /// <summary>抗锯齿级别集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int[] AntiAliasingLevels =
    {
        0,
        2,
        4,
        8
    };
    /// <summary>ApplyAtStartup 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAtStartup()
    {
        if (!EnsurePreferredGraphicsApi())
        {
            return;
        }

        ApplySavedSettings();
    }

    /// <summary>
    /// 应用已保存的画质、帧率、垂直同步、抗锯齿与显示设置。
    /// </summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplyAtStartup()；下游（含内部回调体） GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.ResolveQualityLevel(int)、Mathf.Clamp(int, int, int)、QualitySettings.SetQualityLevel(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void ApplySavedSettings()
    {
        // 预设索引。
int presetIndex = Mathf.Clamp(GameSettingsData.ReadInteger(QualityPresetKey, 1), 0, 2);
        QualitySettings.SetQualityLevel(ResolveQualityLevel(presetIndex), true);
        if (GameSettingsData.Exists(AntiAliasingKey))
        {
            ApplyAntiAliasing(GameSettingsData.ReadInteger(AntiAliasingKey, 1));
        }

        // 按原顺序缓存的实参向量同步键。
string argumentValue2 = VSyncKey;
        // 分支选出的1。
int selectedValue1;
        if (QualitySettings.vSyncCount > 0)
        {
            selectedValue1 = 1;
        }
        else
        {
            selectedValue1 = 0;
        }

        // 是否使用向量同步。
bool useVSync = GameSettingsData.ReadInteger(argumentValue2, selectedValue1) == 1;
        ApplyVSync(useVSync);
#if !UNITY_EDITOR
        if (GameSettingsData.Exists("Fullscreen"))
        {
            Screen.fullScreen = GameSettingsData.ReadInteger("Fullscreen", 1) == 1;
        }

        if (GameSettingsData.Exists("ResolutionWidth") && GameSettingsData.Exists("ResolutionHeight"))
        {
            // 宽度。
int width = GameSettingsData.ReadInteger("ResolutionWidth", Screen.currentResolution.width);
            // 高度。
int height = GameSettingsData.ReadInteger("ResolutionHeight", Screen.currentResolution.height);
            Screen.SetResolution(width, height, Screen.fullScreen);
        }
#endif
    }

    /// <summary>SetQualityPreset 的业务处理入口；本方法直接依赖 GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.ResolveQualityLevel(int)、Mathf.Clamp(int, int, int) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnQualityChanged(int)；下游（含内部回调体） GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.ResolveQualityLevel(int)、Mathf.Clamp(int, int, int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="presetIndex">预设索引。</param>
    public static void SetQualityPreset(int presetIndex)
    {
        presetIndex = Mathf.Clamp(presetIndex, 0, 2);
        GameSettingsData.WriteInteger(QualityPresetKey, presetIndex);
        QualitySettings.SetQualityLevel(ResolveQualityLevel(presetIndex), true);
        if (GameSettingsData.Exists(AntiAliasingKey))
        {
            ApplyAntiAliasing(GameSettingsData.ReadInteger(AntiAliasingKey, 1));
        }

        // 按原顺序缓存的实参向量同步键。
string argumentValue4 = VSyncKey;
        // 分支选出的1。
int selectedValue3;
        if (QualitySettings.vSyncCount > 0)
        {
            selectedValue3 = 1;
        }
        else
        {
            selectedValue3 = 0;
        }

        ApplyVSync(GameSettingsData.ReadInteger(argumentValue4, selectedValue3) == 1);
    }

    /// <summary>GetQualityPreset 的业务处理入口；本方法直接依赖 GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildGraphicsOptions()；下游（含内部回调体） GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static int GetQualityPreset()
    {
        return Mathf.Clamp(GameSettingsData.ReadInteger(QualityPresetKey, 1), 0, 2);
    }

    /// <summary>SetFrameRateIndex 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnFrameRateChanged(int)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    public static void SetFrameRateIndex(int index)
    {
        index = Mathf.Clamp(index, 0, FrameRates.Length - 1);
        GameSettingsData.WriteInteger(FrameRateKey, index);
        if (QualitySettings.vSyncCount == 0)
        {
            Application.targetFrameRate = FrameRates[index];
        }
    }

    /// <summary>GetFrameRateIndex 的业务处理入口；本方法直接依赖 GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplyVSync(bool)、OptionsTabManager.BuildGraphicsOptions()；下游（含内部回调体） GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static int GetFrameRateIndex()
    {
        return Mathf.Clamp(GameSettingsData.ReadInteger(FrameRateKey, 2), 0, FrameRates.Length - 1);
    }

    /// <summary>SetVSync 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyVSync(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnVSyncChanged(bool)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyVSync(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enabled">是否启用。</param>
    public static void SetVSync(bool enabled)
    {
        // 按原顺序缓存的实参向量同步键。
string argumentValue6 = VSyncKey;
        // 分支选出的1。
int selectedValue5;
        if (enabled)
        {
            selectedValue5 = 1;
        }
        else
        {
            selectedValue5 = 0;
        }

        GameSettingsData.WriteInteger(argumentValue6, selectedValue5);
        ApplyVSync(enabled);
    }

    /// <summary>GetVSync 的业务处理入口；本方法直接依赖 GameSettingsData.ReadInteger(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildGraphicsOptions()；下游（含内部回调体） GameSettingsData.ReadInteger(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static bool GetVSync()
    {
        // 按原顺序缓存的实参向量同步键。
string argumentValue8 = VSyncKey;
        // 分支选出的1。
int selectedValue7;
        if (QualitySettings.vSyncCount > 0)
        {
            selectedValue7 = 1;
        }
        else
        {
            selectedValue7 = 0;
        }

        return GameSettingsData.ReadInteger(argumentValue8, selectedValue7) == 1;
    }

    /// <summary>SetAntiAliasingIndex 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnAntiAliasingChanged(int)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    public static void SetAntiAliasingIndex(int index)
    {
        index = Mathf.Clamp(index, 0, AntiAliasingLevels.Length - 1);
        GameSettingsData.WriteInteger(AntiAliasingKey, index);
        ApplyAntiAliasing(index);
    }

    /// <summary>GetAntiAliasingIndex 的业务处理入口；本方法直接依赖 GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildGraphicsOptions()；下游（含内部回调体） GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static int GetAntiAliasingIndex()
    {
        if (GameSettingsData.Exists(AntiAliasingKey))
        {
            return Mathf.Clamp(GameSettingsData.ReadInteger(AntiAliasingKey, 1), 0, AntiAliasingLevels.Length - 1);
        }

        // 当前级别。
int currentLevel = QualitySettings.antiAliasing;
        // 当前遍历索引。
for (int i = 0; i < AntiAliasingLevels.Length; i++)
        {
            if (AntiAliasingLevels[i] == currentLevel)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>GetGraphicsApiLabel 的业务处理入口；本方法直接依赖 Enum.ToString()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Enum.ToString()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string GetGraphicsApiLabel()
    {
        switch (SystemInfo.graphicsDeviceType)
        {
            case GraphicsDeviceType.Direct3D11:
                return "微软图形接口 11";
            case GraphicsDeviceType.Direct3D12:
                return "微软图形接口 12";
            case GraphicsDeviceType.Vulkan:
                return "火山图形接口";
            case GraphicsDeviceType.OpenGLCore:
                return "开放图形核心接口";
            default:
                return SystemInfo.graphicsDeviceType.ToString();
        }
    }

    /// <summary>GetGraphicsApiPreference 的业务处理入口；本方法直接依赖 GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.IsPreferredGraphicsApiActive()、OptionsTabManager.BuildGraphicsOptions()；下游（含内部回调体） GameSettingsData.ReadInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static int GetGraphicsApiPreference()
    {
        // 当前索引。
int currentIndex;
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
        {
            currentIndex = 1;
        }
        else
        {
            currentIndex = 0;
        }

        return Mathf.Clamp(GameSettingsData.ReadInteger(GraphicsApiKey, currentIndex), 0, 1);
    }

    /// <summary>SetGraphicsApiPreference 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnGraphicsApiChanged(int)；下游（含内部回调体） GameSettingsData.SaveValue()、GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    public static void SetGraphicsApiPreference(int index)
    {
        GameSettingsData.WriteInteger(GraphicsApiKey, Mathf.Clamp(index, 0, 1));
        GameSettingsData.SaveValue();
    }

    /// <summary>IsPreferredGraphicsApiActive 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.GetGraphicsApiPreference()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnGraphicsApiChanged(int)；下游（含内部回调体） GraphicsSettingsRuntime.GetGraphicsApiPreference()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static bool IsPreferredGraphicsApiActive()
    {
        // 当前索引。
int currentIndex;
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
        {
            currentIndex = 1;
        }
        else
        {
            currentIndex = 0;
        }

        return GetGraphicsApiPreference() == currentIndex;
    }

    /// <summary>ResolveQualityLevel 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)、Mathf.Min(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.SetQualityPreset(int)；下游（含内部回调体） Mathf.Clamp(int, int, int)、Mathf.Min(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="presetIndex">预设索引。</param>
    private static int ResolveQualityLevel(int presetIndex)
    {
        // 数量。
int count = QualitySettings.count;
        if (count <= 1)
        {
            return 0;
        }

        switch (presetIndex)
        {
            case 0:
                return Mathf.Min(1, count - 1);
            case 1:
                return Mathf.Clamp((count - 1) / 2 + 1, 0, count - 1);
            default:
                return count - 1;
        }
    }

    /// <summary>ApplyVSync 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.GetFrameRateIndex()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.SetQualityPreset(int)、GraphicsSettingsRuntime.SetVSync(bool)；下游（含内部回调体） GraphicsSettingsRuntime.GetFrameRateIndex()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enabled">是否启用。</param>
    private static void ApplyVSync(bool enabled)
    {
        if (enabled)
        {
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }

        if (enabled)
        {
            Application.targetFrameRate = -1;
        }
        else
        {
            Application.targetFrameRate = FrameRates[GetFrameRateIndex()];
        }
    }

    /// <summary>ApplyAntiAliasing 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.SetAntiAliasingIndex(int)、GraphicsSettingsRuntime.SetQualityPreset(int)；下游（含内部回调体） Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    private static void ApplyAntiAliasing(int index)
    {
        index = Mathf.Clamp(index, 0, AntiAliasingLevels.Length - 1);
        QualitySettings.antiAliasing = AntiAliasingLevels[index];
    }

    /// <summary>EnsurePreferredGraphicsApi 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 GraphicsSettingsRuntime.ApplyAtStartup()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static bool EnsurePreferredGraphicsApi()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (IsPreferredGraphicsApiActive())
        {
            return true;
        }

        // 参数集合。
string[] arguments = Environment.GetCommandLineArgs();
        // 当前遍历索引。
for (int i = 0; i < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], "-graphics-preference-applied", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        try
        {
            // 可执行文件路径。
string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                return true;
            }

            // 命令线。
System.Text.StringBuilder commandLine = new System.Text.StringBuilder();
            // 当前遍历索引。
for (int i = 1; i < arguments.Length; i++)
            {
                // 参数。
string argument = arguments[i];
                if (string.Equals(argument, "-force-d3d11", StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-force-d3d12", StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-graphics-preference-applied", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                commandLine.Append(QuoteArgument(argument)).Append(' ');
            }

            // 调用前缓存的命令线。
global::System.Text.StringBuilder callReceiver3 = commandLine;
            // 分支选出的文字“-force-d3d12 ”。
string selectedValue2;
            if (GetGraphicsApiPreference() == 1)
            {
                selectedValue2 = "-force-d3d12 ";
            }
            else
            {
                selectedValue2 = "-force-d3d11 ";
            }

            callReceiver3.Append(selectedValue2);
            commandLine.Append("-graphics-preference-applied");
            // 起始信息。
System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = commandLine.ToString(),
                WorkingDirectory = System.IO.Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
            Application.Quit();
            return false;
        }
        catch (Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
        {
            Debug.LogError("无法使用所选图形接口重新启动游戏：" + exception.Message);
        }

#endif
        return true;
    }

    /// <summary>QuoteArgument 的业务处理入口；本方法直接依赖 string.Contains(string)、string.IsNullOrEmpty(string)、string.Replace(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） string.Contains(string)、string.IsNullOrEmpty(string)、string.Replace(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="argument">参数。</param>
    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (!argument.Contains(" ") && !argument.Contains("\"") && !argument.Contains("\t"))
        {
            return argument;
        }

        return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
