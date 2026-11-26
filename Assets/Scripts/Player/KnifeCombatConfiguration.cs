using System;
using UnityEngine;
using GameArchitecture;

/// <summary>玩家控制、动作与运动模块 KnifeActionConfiguration；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀片段配置")]
public sealed class KnifeActionConfiguration
{
    /// <summary>来源动画片段。</summary>
    [Header("制作源与拆分起点")]
    public AnimationClip sourceClip;
    [Tooltip("前刺确认命中敌人后采样的配对攻击姿态")]
    public AnimationClip confirmedHitClip;
    /// <summary>来源起始。</summary>
    public float sourceStart;
    [Tooltip("格挡起手和收势的动画倍率。命中、弹反窗口与前刺收招沿用各自时序。")]
    [Min(0.1f)]
    public float guardPlaybackSpeed = 1f;
    /// <summary>动作。</summary>
    [Header("动作与片段")]
    [UnityEngine.Serialization.FormerlySerializedAs("动作")]
    [UnityEngine.InspectorName("动作")]
    [Newtonsoft.Json.JsonProperty("动作")]
    public KnifeAction action;
    /// <summary>片段。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("片段")]
    [UnityEngine.InspectorName("片段")]
    [Newtonsoft.Json.JsonProperty("片段")]
    public AnimationClip clip;
    /// <summary>时序。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("时序")]
    [UnityEngine.InspectorName("时序")]
    [Newtonsoft.Json.JsonProperty("时序")]
    public KnifeActionTiming timing;
    /// <summary>刀根轨迹。</summary>
    [Header("实际刀刃采样")]
    [UnityEngine.Serialization.FormerlySerializedAs("刀根轨迹")]
    [UnityEngine.InspectorName("刀根轨迹")]
    [Newtonsoft.Json.JsonProperty("刀根轨迹")]
    public Vector3[] bladeBaseTrajectory;
    /// <summary>刀尖轨迹。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("刀尖轨迹")]
    [UnityEngine.InspectorName("刀尖轨迹")]
    [Newtonsoft.Json.JsonProperty("刀尖轨迹")]
    public Vector3[] bladeTipTrajectory;
    /// <summary>采样间隔。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("采样间隔")]
    [UnityEngine.InspectorName("采样间隔")]
    [Newtonsoft.Json.JsonProperty("采样间隔")]
    public float sampleInterval = 1f / 120f;
    public Vector3[] planarRootTrajectory;

    internal float PlaybackSpeed
    {
        get
        {
            if (guardPlaybackSpeed > 0f && action is (KnifeAction.EnterGuard or KnifeAction.LowerGuard))
            {
                return guardPlaybackSpeed;
            }
            return 1f;
        }
    }

    internal float SourceTimeAt(double elapsed)
    {
        return sourceStart + (float)elapsed * PlaybackSpeed;
    }

    internal bool HasPlanarRoot => planarRootTrajectory != null && planarRootTrajectory.Length > 1;

    internal Vector3 PlanarRootAt(double time)
    {
        float sample = Mathf.Clamp((float)time / sampleInterval, 0f, planarRootTrajectory.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(sample), planarRootTrajectory.Length - 2);
        return Vector3.Lerp(planarRootTrajectory[index], planarRootTrajectory[index + 1], sample - index);
    }
    /// <summary>动画状态名称。</summary>
    public string StateName
    {
        get
        {
            return "KnifeAction_" + (int)action;
        }
    }

    /// <summary>状态路径。</summary>
    public string StatePath => "Weapon Action SM.小刀重构." + StateName;

    /// <summary>GetTrajectory 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.FloorToInt(float)、Mathf.Min(int, int)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.FloorToInt(float)、Mathf.Min(int, int)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    /// <param name="root">根节点。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="tip">尖端。输出参数，由本入口写入并交给调用方。</param>
    public bool GetTrajectory(double time, out Vector3 root, out Vector3 tip)
    {
        root = tip = default;
        if (bladeBaseTrajectory == null || bladeTipTrajectory == null || bladeBaseTrajectory.Length < 2 || bladeBaseTrajectory.Length != bladeTipTrajectory.Length)
        {
            return false;
        }

        // 位置。
float position = Mathf.Clamp((float)time / sampleInterval, 0, bladeBaseTrajectory.Length - 1);
        // 左侧。
int left = Mathf.Min(Mathf.FloorToInt(position), bladeBaseTrajectory.Length - 2);
        root = Vector3.Lerp(bladeBaseTrajectory[left], bladeBaseTrajectory[left + 1], position - left);
        tip = Vector3.Lerp(bladeTipTrajectory[left], bladeTipTrajectory[left + 1], position - left);
        return true;
    }
}

/// <summary>玩家控制、动作与运动使用的共享配置资产 KnifeCombatConfiguration；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(menuName = "战斗/小刀战斗配置", fileName = "小刀战斗配置")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗配置")]
public sealed class KnifeCombatConfiguration : ScriptableObject
{
    /// <summary>姿态骨架。</summary>
    [Header("已验收预览求值")]
    public GameObject poseSkeleton;
    /// <summary>预览待机。</summary>
    public AnimationClip previewIdle;
    /// <summary>预览移动行为待机。</summary>
    public AnimationClip previewLocomotionIdle;
    /// <summary>预览移动行为。</summary>
    public AnimationClip[] previewLocomotion;

    [Tooltip("站定持刀格挡时的左转下半身片段。")]
    public AnimationClip guardTurnLeft;

    [Tooltip("站定持刀格挡时的右转下半身片段。")]
    public AnimationClip guardTurnRight;
    /// <summary>动作片段。</summary>
    [Header("动作时序")]
    [UnityEngine.Serialization.FormerlySerializedAs("动作片段")]
    [UnityEngine.InspectorName("动作片段")]
    public KnifeActionConfiguration[] actionClip;
    /// <summary>完美窗口。</summary>
    [Header("招架规则")]
    [UnityEngine.Serialization.FormerlySerializedAs("完美窗口")]
    [UnityEngine.InspectorName("完美窗口")]
    public float perfectWindow = .11f;
    /// <summary>短按窗口。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("短按窗口")]
    [UnityEngine.InspectorName("短按窗口")]
    public float tapWindow = .34f;

    internal float EffectiveTapWindow => Mathf.Max(tapWindow, .34f);
    /// <summary>防守总角度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("防守总角度")]
    [UnityEngine.InspectorName("防守总角度")]
    public float guardTotalAngle = 120f;
    /// <summary>普通停顿。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("普通停顿")]
    [UnityEngine.InspectorName("普通停顿")]
    public float normalHitStop = .035f;
    /// <summary>完美停顿。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("完美停顿")]
    [UnityEngine.InspectorName("完美停顿")]
    public float perfectHitStop = .065f;
    /// <summary>常规混合。</summary>
    [Header("过渡")]
    [UnityEngine.Serialization.FormerlySerializedAs("常规混合")]
    [UnityEngine.InspectorName("常规混合")]
    public float normalBlend = .08f;
    /// <summary>接触混合。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("接触混合")]
    [UnityEngine.InspectorName("接触混合")]
    public float contactBlend = .04f;
    /// <summary>防守步速比例。</summary>
    [Header("移动与辅助")]
    [UnityEngine.Serialization.FormerlySerializedAs("防守步速比例")]
    [UnityEngine.InspectorName("防守步速比例")]
    public float guardWalkSpeedRatio = .7f;
    /// <summary>攻击步速比例。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击步速比例")]
    [UnityEngine.InspectorName("攻击步速比例")]
    public float attackWalkSpeedRatio = .45f;
    /// <summary>有效段步速比例。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("有效段步速比例")]
    [UnityEngine.InspectorName("有效段步速比例")]
    public float activePhaseWalkSpeedRatio = .25f;
    /// <summary>辅助距离。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("辅助距离")]
    [UnityEngine.InspectorName("辅助距离")]
    public float assistDistance = 1.4f;
    /// <summary>辅助总角度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("辅助总角度")]
    [UnityEngine.InspectorName("辅助总角度")]
    public float assistTotalAngle = 30f;
    /// <summary>最大修正角度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("最大修正角度")]
    [UnityEngine.InspectorName("最大修正角度")]
    public float maximumCorrectionAngle = 12f;
    /// <summary>刀刃检测半径。</summary>
    [Header("刀刃与耐久")]
    [UnityEngine.Serialization.FormerlySerializedAs("刀刃检测半径")]
    [UnityEngine.InspectorName("刀刃检测半径")]
    public float bladeDetectRadius = .035f;
    /// <summary>命中遮罩。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("命中遮罩")]
    [UnityEngine.InspectorName("命中遮罩")]
    public LayerMask hitMask = ~0;
    /// <summary>攻击消耗。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击消耗")]
    [UnityEngine.InspectorName("攻击消耗")]
    public int attackConsumption = 2;
    /// <summary>普通招架消耗。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("普通招架消耗")]
    [UnityEngine.InspectorName("普通招架消耗")]
    public int normalParryConsumption = 10;
    /// <summary>完美招架消耗。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("完美招架消耗")]
    [UnityEngine.InspectorName("完美招架消耗")]
    public int perfectParryConsumption = 5;

    [Tooltip("一次成功暗杀或倒地补刀消耗的耐久。")]
    [Min(1)]
    public int finisherConsumption = 25;
    /// <summary>原握心。</summary>
    [Header("握柄标定")]
    [UnityEngine.Serialization.FormerlySerializedAs("原握心")]
    [UnityEngine.InspectorName("原握心")]
    public Vector3 originalGripCenter = new Vector3(.012f, .078f, .027f);
    /// <summary>联合握心。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("联合握心")]
    [UnityEngine.InspectorName("联合握心")]
    public Vector3 combinedGripCenter = new Vector3(.008308525f, .07232714f, .04972449f);
    /// <summary>联合旋转。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("联合旋转")]
    [UnityEngine.InspectorName("联合旋转")]
    public Quaternion combinedRotation = new Quaternion(.02562066f, .13881227f, -.02048905f, .9897752f);
    /// <summary>刀根局部。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("刀根局部")]
    [UnityEngine.InspectorName("刀根局部")]
    public Vector3 bladeBaseLocal;
    /// <summary>刀尖局部。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("刀尖局部")]
    [UnityEngine.InspectorName("刀尖局部")]
    public Vector3 bladeTipLocal;
    /// <summary>指骨名称。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("指骨名称")]
    [UnityEngine.InspectorName("指骨名称")]
    public string[] fingerBoneName;
    /// <summary>指骨旋转。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("指骨旋转")]
    [UnityEngine.InspectorName("指骨旋转")]
    public Quaternion[] fingerBoneRotation;
    /// <summary>挥空声音。</summary>
    [Header("独立声音")]
    [UnityEngine.Serialization.FormerlySerializedAs("挥空声音")]
    [UnityEngine.InspectorName("挥空声音")]
    public AudioClip whiffSound;
    /// <summary>切入声音。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("切入声音")]
    [UnityEngine.InspectorName("切入声音")]
    public AudioClip sliceIntoSound;
    /// <summary>硬物声音。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("硬物声音")]
    [UnityEngine.InspectorName("硬物声音")]
    public AudioClip hardSurfaceSound;
    /// <summary>普通招架声音。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("普通招架声音")]
    [UnityEngine.InspectorName("普通招架声音")]
    public AudioClip normalParrySound;
    /// <summary>完美招架声音。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("完美招架声音")]
    [UnityEngine.InspectorName("完美招架声音")]
    public AudioClip perfectParrySound;
    /// <summary>断刀声音。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("断刀声音")]
    [UnityEngine.InspectorName("断刀声音")]
    public AudioClip knifeBreakSound;

    [Header("村庄战斗音频")]
    [Tooltip("挥砍和前刺动作各自的破风声")]
    public AudioEventData slashSwingEvent;
    public AudioEventData thrustSwingEvent;
    [Tooltip("按动作和命中部位选择接触声")]
    public AudioEventData slashBodyImpactEvent;
    public AudioEventData slashHeadImpactEvent;
    public AudioEventData thrustBodyImpactEvent;
    public AudioEventData thrustHeadImpactEvent;
    [Tooltip("普通弹反与完美弹反的接触声")]
    public AudioEventData normalParryEvent;
    public AudioEventData perfectParryEvent;
    /// <summary>Find 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonAnimatorDriver.LateUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">动作。</param>
    public KnifeActionConfiguration Find(KnifeAction action)
    {
        if (actionClip != null)
        {
            // item（当前小刀动作配置）：每轮从动作动画片段取得一个元素，仅代表当前这一项。使用位置：KnifeCombatConfiguration.Find(KnifeAction)。
foreach (var item in actionClip)
            {
                if (item != null && item.action == action)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>CreateRules 的业务处理入口；本方法直接依赖 KnifeCombatRules.Configuration(KnifeAction, KnifeActionTiming)、KnifeCombatRules.KnifeCombatRules()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Start()；下游（含内部回调体） KnifeCombatRules.Configuration(KnifeAction, KnifeActionTiming)、KnifeCombatRules.KnifeCombatRules()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public KnifeCombatRules CreateRules()
    {
        // 规则。
var rules = new KnifeCombatRules
        {
            PerfectWindow = perfectWindow,
            TapWindow = EffectiveTapWindow
        };
        if (actionClip != null)
        {
            // item（当前小刀动作配置）：每轮从动作动画片段取得一个元素，仅代表当前这一项。使用位置：作为 KnifeCombatRules.Configuration(KnifeAction, KnifeActionTiming) 的参数、KnifeCombatConfiguration.CreateRules()。
foreach (var item in actionClip)
            {
                if (item != null)
                {
                    var timing = item.timing;
                    timing.duration /= item.PlaybackSpeed;
                    rules.Configuration(item.action, timing);
                }
            }
        }

        return rules;
    }
}
