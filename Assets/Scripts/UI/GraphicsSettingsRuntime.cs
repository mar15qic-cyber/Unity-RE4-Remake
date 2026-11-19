using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 负责保存并应用可在运行时修改的画面设置。
/// 图形接口在程序启动前确定，因此这里只负责显示当前接口。
/// </summary>
public static class GraphicsSettingsRuntime
{
    /// <summary>中文含义：质量预设键。编译期常量，保持既有值与消费者映射。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、作为 GameSettingsData.WriteInteger(string, int) 的参数。</summary>
    public const string QualityPresetKey = "GraphicsQualityPreset";
    /// <summary>中文含义：帧速率键。编译期常量，保持既有值与消费者映射。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、作为 GameSettingsData.WriteInteger(string, int) 的参数。</summary>
    public const string FrameRateKey = "GraphicsFrameRate";
    /// <summary>中文含义：向量同步键。编译期常量，保持既有值与消费者映射。使用位置：GraphicsSettingsRuntime.ApplySavedSettings()、GraphicsSettingsRuntime.GetVSync()、GraphicsSettingsRuntime.SetQualityPreset(int) 等共 4 处。</summary>
    public const string VSyncKey = "GraphicsVSync";
    /// <summary>中文含义：抗锯齿键。编译期常量，保持既有值与消费者映射。使用位置：作为 GameSettingsData.Exists(string) 的参数、作为 GameSettingsData.ReadInteger(string, int) 的参数、作为 GameSettingsData.WriteInteger(string, int) 的参数。</summary>
    public const string AntiAliasingKey = "GraphicsAntiAliasing";
    /// <summary>中文含义：图形接口键。编译期常量，保持既有值与消费者映射。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、作为 GameSettingsData.WriteInteger(string, int) 的参数。</summary>
    public const string GraphicsApiKey = "GraphicsApi";
    /// <summary>中文含义：帧速率集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.SetFrameRateIndex(int)。</summary>
    private static readonly int[] FrameRates =
    {
        -1,
        30,
        60,
        90,
        120,
        144
    };
    /// <summary>中文含义：抗锯齿级别集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.GetAntiAliasingIndex()。</summary>
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
        // presetIndex（预设索引）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 GraphicsSettingsRuntime.ResolveQualityLevel(int) 的参数。
int presetIndex = Mathf.Clamp(GameSettingsData.ReadInteger(QualityPresetKey, 2), 0, 2);
        QualitySettings.SetQualityLevel(ResolveQualityLevel(presetIndex), true);
        if (GameSettingsData.Exists(AntiAliasingKey))
        {
            ApplyAntiAliasing(GameSettingsData.ReadInteger(AntiAliasingKey, 1));
        }

        // argumentValue2（按原顺序缓存的实参向量同步键）：从向量同步键读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数。
string argumentValue2 = VSyncKey;
        // selectedValue1（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、GraphicsSettingsRuntime.ApplySavedSettings()。
int selectedValue1;
        if (QualitySettings.vSyncCount > 0)
        {
            selectedValue1 = 1;
        }
        else
        {
            selectedValue1 = 0;
        }

        // useVSync（是否使用向量同步）：使用位置：作为 GraphicsSettingsRuntime.ApplyVSync(bool) 的参数。
bool useVSync = GameSettingsData.ReadInteger(argumentValue2, selectedValue1) == 1;
        ApplyVSync(useVSync);
#if !UNITY_EDITOR
        if (GameSettingsData.Exists("Fullscreen"))
        {
            Screen.fullScreen = GameSettingsData.ReadInteger("Fullscreen", 1) == 1;
        }

        if (GameSettingsData.Exists("ResolutionWidth") && GameSettingsData.Exists("ResolutionHeight"))
        {
            // width（宽度）：保存 GameSettingsData.ReadInteger(string, int) 的返回结果，供当前步骤使用。使用位置：作为 Screen.SetResolution(int, int, bool) 的参数。
int width = GameSettingsData.ReadInteger("ResolutionWidth", Screen.currentResolution.width);
            // height（高度）：保存 GameSettingsData.ReadInteger(string, int) 的返回结果，供当前步骤使用。使用位置：作为 Screen.SetResolution(int, int, bool) 的参数。
int height = GameSettingsData.ReadInteger("ResolutionHeight", Screen.currentResolution.height);
            Screen.SetResolution(width, height, Screen.fullScreen);
        }
#endif
    }

    /// <summary>SetQualityPreset 的业务处理入口；本方法直接依赖 GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.ResolveQualityLevel(int)、Mathf.Clamp(int, int, int) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnQualityChanged(int)；下游（含内部回调体） GameSettingsData.Exists(string)、GameSettingsData.ReadInteger(string, int)、GameSettingsData.WriteInteger(string, int)、GraphicsSettingsRuntime.ApplyAntiAliasing(int)、GraphicsSettingsRuntime.ApplyVSync(bool)、GraphicsSettingsRuntime.ResolveQualityLevel(int)、Mathf.Clamp(int, int, int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="presetIndex">中文含义：预设索引。使用位置：作为 GameSettingsData.WriteInteger(string, int) 的参数、作为 GraphicsSettingsRuntime.ResolveQualityLevel(int) 的参数、作为 Mathf.Clamp(int, int, int) 的参数 等共 4 处。</param>
    public static void SetQualityPreset(int presetIndex)
    {
        presetIndex = Mathf.Clamp(presetIndex, 0, 2);
        GameSettingsData.WriteInteger(QualityPresetKey, presetIndex);
        QualitySettings.SetQualityLevel(ResolveQualityLevel(presetIndex), true);
        if (GameSettingsData.Exists(AntiAliasingKey))
        {
            ApplyAntiAliasing(GameSettingsData.ReadInteger(AntiAliasingKey, 1));
        }

        // argumentValue4（按原顺序缓存的实参向量同步键）：从向量同步键读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数。
string argumentValue4 = VSyncKey;
        // selectedValue3（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、GraphicsSettingsRuntime.SetQualityPreset(int)。
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
        return Mathf.Clamp(GameSettingsData.ReadInteger(QualityPresetKey, 2), 0, 2);
    }

    /// <summary>SetFrameRateIndex 的业务处理入口；本方法直接依赖 GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnFrameRateChanged(int)；下游（含内部回调体） GameSettingsData.WriteInteger(string, int)、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 GameSettingsData.WriteInteger(string, int) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、GraphicsSettingsRuntime.SetFrameRateIndex(int)。</param>
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
    /// <param name="enabled">中文含义：是否启用。使用位置：作为 GraphicsSettingsRuntime.ApplyVSync(bool) 的参数、GraphicsSettingsRuntime.SetVSync(bool)。</param>
    public static void SetVSync(bool enabled)
    {
        // argumentValue6（按原顺序缓存的实参向量同步键）：从向量同步键读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameSettingsData.WriteInteger(string, int) 的参数。
string argumentValue6 = VSyncKey;
        // selectedValue5（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameSettingsData.WriteInteger(string, int) 的参数、GraphicsSettingsRuntime.SetVSync(bool)。
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
        // argumentValue8（按原顺序缓存的实参向量同步键）：从向量同步键读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数。
string argumentValue8 = VSyncKey;
        // selectedValue7（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、GraphicsSettingsRuntime.GetVSync()。
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
    /// <param name="index">中文含义：索引。使用位置：作为 GameSettingsData.WriteInteger(string, int) 的参数、作为 GraphicsSettingsRuntime.ApplyAntiAliasing(int) 的参数、作为 Mathf.Clamp(int, int, int) 的参数 等共 4 处。</param>
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

        // currentLevel（当前级别）：使用位置：GraphicsSettingsRuntime.GetAntiAliasingIndex()。
int currentLevel = QualitySettings.antiAliasing;
        // i（当前遍历索引）：使用位置：GraphicsSettingsRuntime.GetAntiAliasingIndex()。
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
        // currentIndex（当前索引）：使用位置：作为 GameSettingsData.ReadInteger(string, int) 的参数、GraphicsSettingsRuntime.GetGraphicsApiPreference()。
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
    /// <param name="index">中文含义：索引。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</param>
    public static void SetGraphicsApiPreference(int index)
    {
        GameSettingsData.WriteInteger(GraphicsApiKey, Mathf.Clamp(index, 0, 1));
        GameSettingsData.SaveValue();
    }

    /// <summary>IsPreferredGraphicsApiActive 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.GetGraphicsApiPreference()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.OnGraphicsApiChanged(int)；下游（含内部回调体） GraphicsSettingsRuntime.GetGraphicsApiPreference()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static bool IsPreferredGraphicsApiActive()
    {
        // currentIndex（当前索引）：使用位置：GraphicsSettingsRuntime.IsPreferredGraphicsApiActive()。
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
    /// <param name="presetIndex">中文含义：预设索引。使用位置：GraphicsSettingsRuntime.ResolveQualityLevel(int)。</param>
    private static int ResolveQualityLevel(int presetIndex)
    {
        // count（数量）：使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.Min(int, int) 的参数、GraphicsSettingsRuntime.ResolveQualityLevel(int)。
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
    /// <param name="enabled">中文含义：是否启用。使用位置：GraphicsSettingsRuntime.ApplyVSync(bool)。</param>
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
    /// <param name="index">中文含义：索引。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、GraphicsSettingsRuntime.ApplyAntiAliasing(int)。</param>
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

        // arguments（参数集合）：保存 Environment.GetCommandLineArgs() 的返回结果，供当前步骤使用。使用位置：作为 string.Equals(string, string, StringComparison) 的参数、GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
string[] arguments = Environment.GetCommandLineArgs();
        // i（当前遍历索引）：使用位置：GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
for (int i = 0; i < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], "-graphics-preference-applied", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        try
        {
            // executablePath（可执行文件路径）：使用位置：作为 Path.GetDirectoryName(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数、GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                return true;
            }

            // commandLine（命令线）：使用位置：GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
System.Text.StringBuilder commandLine = new System.Text.StringBuilder();
            // i（当前遍历索引）：使用位置：GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
for (int i = 1; i < arguments.Length; i++)
            {
                // argument（参数）：使用位置：作为 GraphicsSettingsRuntime.QuoteArgument(string) 的参数、作为 string.Equals(string, string, StringComparison) 的参数。
string argument = arguments[i];
                if (string.Equals(argument, "-force-d3d11", StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-force-d3d12", StringComparison.OrdinalIgnoreCase) || string.Equals(argument, "-graphics-preference-applied", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                commandLine.Append(QuoteArgument(argument)).Append(' ');
            }

            // callReceiver3（调用前缓存的命令线）：从命令线读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
global::System.Text.StringBuilder callReceiver3 = commandLine;
            // selectedValue2（分支选出的文字“-force-d3d12 ”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 StringBuilder.Append(string) 的参数、GraphicsSettingsRuntime.EnsurePreferredGraphicsApi()。
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
            // startInfo（起始信息）：使用位置：作为 Process.Start(ProcessStartInfo) 的参数。
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
    /// <param name="argument">中文含义：参数。使用位置：作为 string.IsNullOrEmpty(string) 的参数、GraphicsSettingsRuntime.QuoteArgument(string)。</param>
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
