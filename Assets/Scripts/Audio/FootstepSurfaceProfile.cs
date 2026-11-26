using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 地表类型枚举
/// </summary>
public enum SurfaceType
{
    /// <summary>中文含义：混凝土。</summary>
    Concrete,
    /// <summary>中文含义：草地。</summary>
    Grass,
    /// <summary>中文含义：沙地。</summary>
    Sand,
    /// <summary>中文含义：雪地。</summary>
    Snow,
    /// <summary>中文含义：木材。</summary>
    Wood,
    /// <summary>中文含义：水面。</summary>
    Water,
    /// <summary>中文含义：泥地。</summary>
    Mud,
    /// <summary>中文含义：默认。</summary>
    Default
}

/// <summary>
/// 脚步地表音效配置：按地表类型映射走路和跑步音效事件
/// 替代 FootstepController 中硬编码的 AudioClip[] 数组
/// </summary>
[CreateAssetMenu(fileName = "FootstepSurfaceProfile", menuName = "音频/脚步地表配置")]
public class FootstepSurfaceProfile : ScriptableObject
{
    /// <summary>音频配置与播放模块 SurfaceEntry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [System.Serializable]
    public class SurfaceEntry
    {
        /// <summary>地表类型。</summary>
        [Tooltip("地表类型")]
        public SurfaceType surfaceType;
        /// <summary>走路音效事件。</summary>
        [Tooltip("走路音效事件")]
        public AudioEventData walkEvent;
        /// <summary>跑步音效事件。</summary>
        [Tooltip("跑步音效事件")]
        public AudioEventData runEvent;
        [Tooltip("蹲行音效事件，空时使用该地表的走路声")]
        public AudioEventData crouchEvent;
    }

    /// <summary>按地表类型配置的脚步音效事件。</summary>
    [Header("地表音效列表")]
    [Tooltip("按地表类型配置的脚步音效事件")]
    public List<SurfaceEntry> surfaces = new List<SurfaceEntry>();
    /// <summary>未匹配到特定地表时使用的走路音效。</summary>
    [Header("默认音效")]
    [Tooltip("未匹配到特定地表时使用的走路音效")]
    public AudioEventData defaultWalkEvent;
    /// <summary>未匹配到特定地表时使用的跑步音效。</summary>
    [Tooltip("未匹配到特定地表时使用的跑步音效")]
    public AudioEventData defaultRunEvent;
    // 运行时查找字典
    /// <summary>查找表。</summary>
    private Dictionary<SurfaceType, SurfaceEntry> lookup;
    /// <summary>
    /// 初始化查找字典
    /// </summary>
    /// <remarks>静态调用链：上游 FootstepSurfaceProfile.GetRunEvent(SurfaceType)、FootstepSurfaceProfile.GetWalkEvent(SurfaceType)；下游（含内部回调体） Dictionary&lt;SurfaceType, FootstepSurfaceProfile.SurfaceEntry&gt;.ContainsKey(SurfaceType)、Dictionary&lt;SurfaceType, FootstepSurfaceProfile.SurfaceEntry&gt;.Dictionary()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Initialize()
    {
        lookup = new Dictionary<SurfaceType, SurfaceEntry>();
        if (surfaces == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < surfaces.Count; i++)
        {
            // 当前表面入口。
var entry = surfaces[i];
            if (entry == null)
            {
                continue;
            }

            if (!lookup.ContainsKey(entry.surfaceType))
            {
                lookup[entry.surfaceType] = entry;
            }
        }
    }

    /// <summary>
    /// 获取走路音效事件
    /// </summary>
    /// <remarks>静态调用链：上游 FootstepController.PlayFootstep()；下游（含内部回调体） Dictionary&lt;SurfaceType, FootstepSurfaceProfile.SurfaceEntry&gt;.TryGetValue(SurfaceType, out FootstepSurfaceProfile.SurfaceEntry)、FootstepSurfaceProfile.Initialize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="surfaceType">表面类型。</param>
    public AudioEventData GetWalkEvent(SurfaceType surfaceType)
    {
        if (lookup == null)
        {
            Initialize();
        }

        if (lookup.TryGetValue(surfaceType, out SurfaceEntry /* 中文含义：当前表面入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ entry))
        {
            return entry.walkEvent;
        }

        return defaultWalkEvent;
    }

    /// <summary>
    /// 获取跑步音效事件
    /// </summary>
    /// <remarks>静态调用链：上游 FootstepController.PlayFootstep()；下游（含内部回调体） Dictionary&lt;SurfaceType, FootstepSurfaceProfile.SurfaceEntry&gt;.TryGetValue(SurfaceType, out FootstepSurfaceProfile.SurfaceEntry)、FootstepSurfaceProfile.Initialize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="surfaceType">表面类型。</param>
    public AudioEventData GetCrouchEvent(SurfaceType surfaceType)
    {
        if (lookup == null)
        {
            Initialize();
        }

        if (lookup.TryGetValue(surfaceType, out SurfaceEntry entry) && entry.crouchEvent != null)
        {
            return entry.crouchEvent;
        }

        return GetWalkEvent(surfaceType);
    }

    public AudioEventData GetRunEvent(SurfaceType surfaceType)
    {
        if (lookup == null)
        {
            Initialize();
        }

        if (lookup.TryGetValue(surfaceType, out SurfaceEntry /* 中文含义：当前表面入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ entry))
        {
            return entry.runEvent;
        }

        return defaultRunEvent;
    }
}
