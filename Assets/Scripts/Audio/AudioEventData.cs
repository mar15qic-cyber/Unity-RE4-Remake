using UnityEngine;

/// <summary>
/// 音频分类：用于混音器路由和分类音量控制
/// </summary>
public enum AudioCategory
{
    /// <summary>中文含义：主控。</summary>
    [Tooltip("总音量")]
    Master,
    /// <summary>中文含义：音效。</summary>
    [Tooltip("通用音效")]
    SFX,
    /// <summary>中文含义：背景音乐。</summary>
    [Tooltip("背景音乐")]
    BGM,
    /// <summary>中文含义：环境。</summary>
    [Tooltip("环境声音")]
    Ambient,
    /// <summary>中文含义：武器。</summary>
    [Tooltip("武器声音")]
    Weapon,
    /// <summary>中文含义：玩家。</summary>
    [Tooltip("玩家声音")]
    Player,
    /// <summary>中文含义：敌人。</summary>
    [Tooltip("敌人声音")]
    Enemy,
    /// <summary>中文含义：脚步声。</summary>
    [Tooltip("脚步声音")]
    Footstep,
    /// <summary>中文含义：界面。</summary>
    [Tooltip("界面声音")]
    UI
}

/// <summary>
/// 音效事件数据：单个音效事件的完整配置
/// 包含片段列表、音量音高随机化、空间模式、距离衰减、并发控制等
/// </summary>
[CreateAssetMenu(fileName = "AudioEvent", menuName = "音频/音效事件")]
public class AudioEventData : ScriptableObject
{
    /// <summary>
    /// 片段选择模式
    /// </summary>
    public enum SelectionMode
    {
        /// <summary>中文含义：随机。</summary>
        [Tooltip("随机选择")]
        Random,
        /// <summary>中文含义：顺序。</summary>
        [Tooltip("顺序选择")]
        Sequential
    }

    /// <summary>
    /// 空间播放模式
    /// </summary>
    public enum SpatialMode
    {
        /// <summary>中文含义：第二距离。</summary>
        [Tooltip("二维音效（界面音效等）")]
        TwoD,
        /// <summary>中文含义：第三距离。</summary>
        [Tooltip("三维音效（世界空间定位）")]
        ThreeD
    }

    /// <summary>
    /// 语音优先级：用于敌人语音等需要抢占的场景
    /// </summary>
    public enum VoicePriority
    {
        /// <summary>中文含义：低。</summary>
        [Tooltip("最低")]
        Low = 0,
        /// <summary>中文含义：普通状态。</summary>
        [Tooltip("普通")]
        Normal = 1,
        /// <summary>中文含义：高。</summary>
        [Tooltip("高")]
        High = 2,
        /// <summary>中文含义：暴击。</summary>
        [Tooltip("最高（死亡等不可打断）")]
        Critical = 3
    }

    /// <summary>
    /// 单个音频片段条目
    /// </summary>
    [System.Serializable]
    public class ClipEntry
    {
        /// <summary>音频片段。</summary>
        [Tooltip("音频片段")]
        public AudioClip clip;
        /// <summary>是否启用此片段。</summary>
        [Tooltip("是否启用此片段")]
        public bool enabled = true;
        /// <summary>选择权重（0表示不会被选中）。</summary>
        [Range(0f, 1f)]
        [Tooltip("选择权重（0表示不会被选中）")]
        public float weight = 1f;
    }

    /// <summary>此事件包含的音频片段列表。</summary>
    [Header("片段列表")]
    [Tooltip("此事件包含的音频片段列表")]
    public ClipEntry[] clips = new ClipEntry[0];
    /// <summary>片段选择模式：随机或顺序。</summary>
    [Tooltip("片段选择模式：随机或顺序")]
    public SelectionMode selectionMode = SelectionMode.Random;
    /// <summary>基础音量。</summary>
    [Header("音量")]
    [Range(0f, 1f)]
    [Tooltip("基础音量")]
    public float baseVolume = 1f;
    /// <summary>随机音量范围（在基础音量上叠加，最终值会被钳制到0~1）。</summary>
    [Tooltip("随机音量范围（在基础音量上叠加，最终值会被钳制到0~1）")]
    public Vector2 volumeRange = new Vector2(1f, 1f);
    /// <summary>基础音高。</summary>
    [Header("音高")]
    [Range(0f, 3f)]
    [Tooltip("基础音高")]
    public float basePitch = 1f;
    /// <summary>随机音高变化范围。随机音高范围（在基础音高上叠加）。</summary>
    [Tooltip("随机音高范围（在基础音高上叠加）")]
    public Vector2 pitchRange = new Vector2(1f, 1f);
    /// <summary>二维或三维播放模式。</summary>
    [Header("空间设置")]
    [Tooltip("二维或三维播放模式")]
    public SpatialMode spatialMode = SpatialMode.TwoD;
    /// <summary>最小衰减距离（三维模式下生效）。</summary>
    [Range(0f, 500f)]
    [Tooltip("最小衰减距离（三维模式下生效）")]
    public float minDistance = 1f;
    /// <summary>最大衰减距离（三维模式下生效）。</summary>
    [Range(0f, 1000f)]
    [Tooltip("最大衰减距离（三维模式下生效）")]
    public float maxDistance = 50f;
    /// <summary>距离衰减模式。</summary>
    [Tooltip("距离衰减模式")]
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    /// <summary>优先级（数值越小优先级越高）。</summary>
    [Header("播放控制")]
    [Range(0, 256)]
    [Tooltip("优先级（数值越小优先级越高）")]
    public int priority = 128;
    /// <summary>冷却时间（秒），同一事件在此时间内不会重复播放。</summary>
    [Range(0f, 10f)]
    [Tooltip("冷却时间（秒），同一事件在此时间内不会重复播放")]
    public float cooldown = 0f;
    /// <summary>最大同时播放数量。</summary>
    [Range(1, 32)]
    [Tooltip("最大同时播放数量")]
    public int maxConcurrent = 8;
    /// <summary>是否循环播放。</summary>
    [Tooltip("是否循环播放")]
    public bool loop = false;
    /// <summary>音频分类（用于混音器路由和分类音量控制）。</summary>
    [Header("分类与路由")]
    [Tooltip("音频分类（用于混音器路由和分类音量控制）")]
    public AudioCategory category = AudioCategory.SFX;
    /// <summary>混音器分组路径（留空时由分类自动推断）。</summary>
    [Tooltip("混音器分组路径（留空时由分类自动推断）")]
    public string mixerGroupPath = "";
    /// <summary>语音优先级（仅敌人语音等需要抢占的场景使用）。</summary>
    [Header("语音优先级")]
    [Tooltip("语音优先级（仅敌人语音等需要抢占的场景使用）")]
    public VoicePriority voicePriority = VoicePriority.Normal;
    /// <summary>片段缺失时是否输出警告。</summary>
    [Header("诊断")]
    [Tooltip("片段缺失时是否输出警告")]
    public bool warnIfMissing = true;
    // 运行时状态（不序列化）
    /// <summary>顺序索引。</summary>
    private int sequentialIndex;
    /// <summary>上一次播放时间。</summary>
    private float lastPlayTime = -999f;
    /// <summary>生效数量。</summary>
    private int activeCount;
    /// <summary>
    /// 选择一个音频片段
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.PlayRandomPreview(bool)、AudioLibraryEditor.PlayPreview(AudioEventData)、AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、AudioManager.PlayBGM(AudioEventData) 等共 7 处；下游（含内部回调体） List&lt;AudioEventData.ClipEntry&gt;.Add(AudioEventData.ClipEntry)、List&lt;AudioEventData.ClipEntry&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public AudioClip SelectClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        // 可用。
var available = new System.Collections.Generic.List<ClipEntry>();
        // 总计权重。
float totalWeight = 0f;
        // 当前遍历索引。
for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].enabled && clips[i].clip != null && clips[i].weight > 0f)
            {
                available.Add(clips[i]);
                totalWeight += clips[i].weight;
            }
        }

        if (available.Count == 0)
        {
            return null;
        }

        if (totalWeight <= 0f)
        {
            return available[0].clip;
        }

        if (selectionMode == SelectionMode.Sequential)
        {
            sequentialIndex %= available.Count;
            // 音频片段。
var clip = available[sequentialIndex].clip;
            sequentialIndex++;
            return clip;
        }

        // 随机。
float random = Random.value * totalWeight;
        // 累计。
float cumulative = 0f;
        // 当前遍历索引。
for (int i = 0; i < available.Count; i++)
        {
            cumulative += available[i].weight;
            if (random <= cumulative)
            {
                return available[i].clip;
            }
        }

        return available[available.Count - 1].clip;
    }

    /// <summary>
    /// 计算最终音量（含随机偏移）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)、AudioLibraryEditor.PlayPreview(AudioEventData)、AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string) 等共 6 处；下游（含内部回调体） Mathf.Clamp01(float)、Random.Range(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetVolume()
    {
        // 随机化后的本次播放音量。
float v = baseVolume * Random.Range(volumeRange.x, volumeRange.y);
        return Mathf.Clamp01(v);
    }

    /// <summary>
    /// 计算最终音高（含随机偏移）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)、AudioLibraryEditor.PlayPreview(AudioEventData)、AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string) 等共 6 处；下游（含内部回调体） Random.Range(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetPitch()
    {
        return basePitch * Random.Range(pitchRange.x, pitchRange.y);
    }

    /// <summary>
    /// 检查是否可以播放（冷却和并发限制）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventData.ResetStateIfClockRewound()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float PlaybackTime
    {
        get
        {
            if (category == AudioCategory.UI)
            {
                return Time.unscaledTime;
            }

            return Time.time;
        }
    }

    public bool CanPlay()
    {
        ResetStateIfClockRewound();
        if (PlaybackTime - lastPlayTime < cooldown)
        {
            return false;
        }

        if (activeCount >= maxConcurrent)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 检查是否可以播放（带优先级抢占）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；下游（含内部回调体） AudioEventData.ResetStateIfClockRewound()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requestPriority">请求优先级。</param>
    public bool CanPlay(VoicePriority requestPriority)
    {
        ResetStateIfClockRewound();
        // 高优先级可以无视冷却
        if (requestPriority >= VoicePriority.Critical)
        {
            return activeCount < maxConcurrent;
        }

        if (PlaybackTime - lastPlayTime < cooldown)
        {
            return false;
        }

        if (activeCount >= maxConcurrent)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 播放模式重新开始时，团结引擎可能保留脚本化资产的运行时字段。
    /// 如果时钟回退而上一次播放时间仍在未来，必须清空旧状态，否则事件会被冷却逻辑永久拦截。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventData.CanPlay()、AudioEventData.CanPlay(AudioEventData.VoicePriority)；下游（含内部回调体） AudioEventData.ResetState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetStateIfClockRewound()
    {
        if (lastPlayTime > PlaybackTime)
        {
            ResetState();
        }
    }

    /// <summary>
    /// 标记播放开始
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPlayStarted()
    {
        lastPlayTime = PlaybackTime;
        activeCount++;
    }

    /// <summary>
    /// 标记播放结束（幂等：通过句柄验证）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.RecycleFinished()、AudioSourcePool.Release(int)、AudioSourcePool.ReleaseAll()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPlayFinished()
    {
        if (activeCount > 0)
        {
            activeCount--;
        }
    }

    /// <summary>
    /// 强制重置并发计数（用于抢占或场景卸载）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceResetConcurrent()
    {
        activeCount = 0;
    }

    /// <summary>
    /// 重置运行时状态
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventData.ResetStateIfClockRewound()、AudioLibrary.ResetRuntimeState()、AudioManager.OnSceneUnloaded(Scene)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetState()
    {
        sequentialIndex = 0;
        lastPlayTime = -999f;
        activeCount = 0;
    }

    /// <summary>
    /// 获取混音器分组路径（如果未配置则由分类推断）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)、AudioLibraryEditor.PlayPreview(AudioEventData)、AudioManager.GetMixerGroup(AudioEventData)；下游（含内部回调体） string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetMixerGroupPath()
    {
        if (!string.IsNullOrEmpty(mixerGroupPath))
        {
            return mixerGroupPath;
        }

        return category switch
        {
            AudioCategory.BGM => "背景音乐",
            AudioCategory.Ambient => "环境声音",
            AudioCategory.Weapon => "武器声音",
            AudioCategory.Player => "玩家声音",
            AudioCategory.Enemy => "敌人声音",
            AudioCategory.Footstep => "脚步声音",
            AudioCategory.UI => "界面声音",
            _ => "SFX"
        };
    }
}
