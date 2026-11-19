using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音频事件库：集中管理所有音效事件，通过事件名称索引
/// 业务脚本通过事件名称从此库查找对应的 AudioEventData
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "音频/事件库")]
public class AudioLibrary : ScriptableObject
{
    /// <summary>音频配置与播放模块 Entry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [System.Serializable]
    public class Entry
    {
        /// <summary>中文含义：事件名称（业务脚本通过此名称查找）。使用位置：作为 Dictionary&lt;string, AudioEventData&gt;.ContainsKey(string) 的参数、作为 Dictionary&lt;string, AudioEventData&gt;.this[string] 的参数、作为 EditorGUILayout.LabelField(string, string, params GUILayoutOption[]) 的参数 等共 5 处。</summary>
        [Tooltip("事件名称（业务脚本通过此名称查找）")]
        public string name;
        /// <summary>中文含义：对应的音效事件数据。使用位置：作为 AudioLibraryEditor.PlayPreview(AudioEventData) 的参数、作为 EditorGUILayout.LabelField(string, string, params GUILayoutOption[]) 的参数、AudioLibrary.Initialize() 等共 7 处。</summary>
        [Tooltip("对应的音效事件数据")]
        public AudioEventData eventData;
    }

    /// <summary>中文含义：所有音效事件条目。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、AudioLibrary.Initialize()、AudioLibrary.ResetRuntimeState() 等共 6 处。</summary>
    [Header("事件列表")]
    [Tooltip("所有音效事件条目")]
    public List<Entry> entries = new List<Entry>();
    // 运行时查找字典
    /// <summary>中文含义：查找表。使用位置：AudioLibrary.GetEvent(string)、AudioLibrary.Initialize()。</summary>
    private Dictionary<string, AudioEventData> lookup;
    /// <summary>
    /// 初始化查找字典
    /// </summary>
    /// <remarks>静态调用链：上游 AudioLibrary.GetEvent(string)、AudioManager.Awake()；下游（含内部回调体） Dictionary&lt;string, AudioEventData&gt;.ContainsKey(string)、Dictionary&lt;string, AudioEventData&gt;.Dictionary()、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Initialize()
    {
        lookup = new Dictionary<string, AudioEventData>();
        if (entries == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<AudioLibrary.Entry>.this[int] 的参数、AudioLibrary.Initialize()。
for (int i = 0; i < entries.Count; i++)
        {
            // entry（当前入口）：使用位置：作为 Dictionary<string, AudioEventData>.ContainsKey(string) 的参数、作为 Dictionary<string, AudioEventData>.this[string] 的参数、作为 string.IsNullOrEmpty(string) 的参数 等共 4 处。
var entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.name) || entry.eventData == null)
            {
                continue;
            }

            if (!lookup.ContainsKey(entry.name))
            {
                lookup[entry.name] = entry.eventData;
            }
        }
    }

    /// <summary>
    /// 重置库内所有事件的运行时状态，避免播放模式重启后沿用旧冷却时间和并发计数。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()；下游（含内部回调体） AudioEventData.ResetState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetRuntimeState()
    {
        if (entries == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<AudioLibrary.Entry>.this[int] 的参数、AudioLibrary.ResetRuntimeState()。
for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].eventData != null)
            {
                entries[i].eventData.ResetState();
            }
        }
    }

    /// <summary>
    /// 根据事件名称查找音效事件数据
    /// </summary>
    /// <remarks>静态调用链：上游 AudioLibrary.HasEvent(string)、AudioManager.Play(string)、AudioManager.Play3D(string, Vector3)、ChainsawManAnimationBridge.ResolveAudioEvent(AudioEventData, string)；下游（含内部回调体） AudioLibrary.Initialize()、Dictionary&lt;string, AudioEventData&gt;.TryGetValue(string, out AudioEventData)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventName">中文含义：事件名称。使用位置：作为 Dictionary&lt;string, AudioEventData&gt;.TryGetValue(string, out AudioEventData) 的参数、作为 string.IsNullOrEmpty(string) 的参数。</param>
    public AudioEventData GetEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return null;
        }

        if (lookup == null)
        {
            Initialize();
        }

        lookup.TryGetValue(eventName, out AudioEventData /* 中文含义：当前音频事件配置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ data);
        return data;
    }

    /// <summary>
    /// 检查是否包含指定事件
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibrary.GetEvent(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventName">中文含义：事件名称。使用位置：作为 AudioLibrary.GetEvent(string) 的参数。</param>
    public bool HasEvent(string eventName)
    {
        return GetEvent(eventName) != null;
    }
}
