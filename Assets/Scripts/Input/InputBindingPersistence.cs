using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 改键覆盖的本地持久化边界。LeonInputBridge 启动时和 OptionsTabManager 打开设置时加载，
/// 设置提交或恢复默认时同步保存到 PlayerPrefs；动作资产持有有效覆盖，本工具不另存运行输入状态。
/// </summary>
public static class InputBindingPersistence
{
    /// <summary>中文含义：保存键。标题与游戏场景共用的既有存储键，保持中文字符串以兼容已保存改键。</summary>
    public const string saveKey = "输入绑定覆盖";
    /// <summary>输入桥或设置宿主调用；只有资产与存储均有效时加载覆盖，解析异常时记录提示并保留既有处理行为。</summary>
    /// <param name="inputAsset">中文含义：输入资源。接收覆盖的输入动作资产，为空时无操作。</param>
    public static void Load(InputActionAsset inputAsset)
    {
        if (inputAsset == null || !PlayerPrefs.HasKey(saveKey))
        {
            return;
        }

        // 本地保存的覆盖 JSON；空文本不交给输入系统解析。
        // 中文含义：json 为“序列化文本”。
string json = PlayerPrefs.GetString(saveKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            inputAsset.LoadBindingOverridesFromJson(json);
        }
        catch (System.Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
        {
            // exception 是输入系统解析覆盖时抛出的异常，只用于既有警告，不补建或替换动作资产。
            Debug.LogWarning("[输入设置] 无法加载按键覆盖，已保留默认按键：" + exception.Message);
        }
    }

    /// <summary>设置宿主完成改键后调用，把当前资产的覆盖序列化到共用键并同步落盘。</summary>
    /// <param name="inputAsset">中文含义：输入资源。已经完成改键的动作资产，为空时无操作。</param>
    public static void SaveValue(InputActionAsset inputAsset)
    {
        if (inputAsset == null)
        {
            return;
        }

        PlayerPrefs.SetString(saveKey, inputAsset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    /// <summary>设置宿主请求恢复默认时调用，移除资产覆盖和本地保存项，再同步落盘。</summary>
    /// <param name="inputAsset">中文含义：输入资源。需要恢复默认绑定的动作资产，为空时无操作。</param>
    public static void RestoreDefault(InputActionAsset inputAsset)
    {
        if (inputAsset == null)
        {
            return;
        }

        inputAsset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
    }
}
