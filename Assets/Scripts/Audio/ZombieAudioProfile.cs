using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人语音类型枚举
/// </summary>
public enum ZombieVoiceType
{
    /// <summary>中文含义：攻击。</summary>
    Attack,
    /// <summary>中文含义：撕咬。</summary>
    Bite,
    /// <summary>中文含义：已蓄力攻击。</summary>
    ChargedAttack,
    /// <summary>中文含义：受伤。</summary>
    Hurt,
    /// <summary>中文含义：爆头。</summary>
    Headshot,
    /// <summary>中文含义：肢体命中。</summary>
    LimbHit,
    /// <summary>中文含义：疼痛。</summary>
    Pain,
    /// <summary>中文含义：死亡。</summary>
    Death,
    /// <summary>中文含义：窒息。</summary>
    Choke,
    /// <summary>中文含义：血腥效果。</summary>
    Gory,
    /// <summary>中文含义：待机。</summary>
    Idle,
    /// <summary>中文含义：低吼。</summary>
    Grunt,
    /// <summary>中文含义：喉部。</summary>
    Throat,
    /// <summary>中文含义：呼吸。</summary>
    Breathing,
    /// <summary>中文含义：检测。</summary>
    Detection,
    /// <summary>中文含义：尖叫。</summary>
    Scream,
    /// <summary>中文含义：移动行为。</summary>
    Locomotion,
    /// <summary>中文含义：地面。</summary>
    Ground,
    /// <summary>中文含义：呕吐。</summary>
    Vomit,
    /// <summary>中文含义：笑声。</summary>
    Laugh,
    /// <summary>中文含义：反应。</summary>
    Reaction,
    /// <summary>中文含义：进食。</summary>
    Eating,
    /// <summary>中文含义：词条。</summary>
    Word
}

/// <summary>
/// 敌人语音配置：按语音类型映射到音效事件
/// 替代原 ZombieAudioConfig 的 AudioClip[] 数组方式
/// </summary>
[CreateAssetMenu(fileName = "ZombieAudioProfile", menuName = "音频/敌人语音配置")]
public class ZombieAudioProfile : ScriptableObject
{
    /// <summary>音频配置与播放模块 VoiceEntry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [System.Serializable]
    public class VoiceEntry
    {
        /// <summary>语音类型。</summary>
        [Tooltip("语音类型")]
        public ZombieVoiceType voiceType;
        /// <summary>对应的音效事件数据。</summary>
        [Tooltip("对应的音效事件数据")]
        public AudioEventData eventData;
    }

    /// <summary>按语音类型配置的音效事件。</summary>
    [Header("语音列表")]
    [Tooltip("按语音类型配置的音效事件")]
    public List<VoiceEntry> voices = new List<VoiceEntry>();
    // 运行时查找字典
    /// <summary>查找表。</summary>
    private Dictionary<ZombieVoiceType, AudioEventData> lookup;
    /// <summary>
    /// 初始化查找字典
    /// </summary>
    /// <remarks>静态调用链：上游 ZombieAudioProfile.GetEvent(ZombieVoiceType)；下游（含内部回调体） Dictionary&lt;ZombieVoiceType, AudioEventData&gt;.ContainsKey(ZombieVoiceType)、Dictionary&lt;ZombieVoiceType, AudioEventData&gt;.Dictionary()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Initialize()
    {
        lookup = new Dictionary<ZombieVoiceType, AudioEventData>();
        if (voices == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < voices.Count; i++)
        {
            // 当前语音入口。
var entry = voices[i];
            if (entry == null || entry.eventData == null)
            {
                continue;
            }

            if (!lookup.ContainsKey(entry.voiceType))
            {
                lookup[entry.voiceType] = entry.eventData;
            }
        }
    }

    /// <summary>
    /// 根据语音类型查找音效事件
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)；下游（含内部回调体） Dictionary&lt;ZombieVoiceType, AudioEventData&gt;.TryGetValue(ZombieVoiceType, out AudioEventData)、ZombieAudioProfile.Initialize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">语音类型。</param>
    public AudioEventData GetEvent(ZombieVoiceType voiceType)
    {
        if (lookup == null)
        {
            Initialize();
        }

        lookup.TryGetValue(voiceType, out AudioEventData /* 中文含义：当前音频事件配置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ data);
        return data;
    }
}
