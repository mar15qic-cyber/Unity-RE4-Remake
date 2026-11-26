using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>摄像机与瞄准组件 QTECameraPresenter，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed partial class QTECameraPresenter : MonoBehaviour
{
    /// <summary>摄像机与瞄准模块 CameraPreset；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    public sealed class CameraPreset
    {
        /// <summary>种类。</summary>
        public QTEKind kind;
        /// <summary>绑定预制件。</summary>
        public QTECameraRig rigPrefab;
        /// <summary>配置档。</summary>
        public QTECameraProfile profile;
    }

    /// <summary>摄像机与瞄准模块 RuntimePreset；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class RuntimePreset
    {
        /// <summary>绑定。</summary>
        public QTECameraRig rig;
        /// <summary>配置档。</summary>
        public QTECameraProfile profile;
    }

    /// <summary>摄像机与瞄准使用的 CameraMotion 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum CameraMotion
    {
        /// <summary>中文含义：无（未选择或尚未发生）。</summary>
        None,
        /// <summary>中文含义：进入中。</summary>
        Entering,
        /// <summary>中文含义：持有。</summary>
        Holding,
        /// <summary>中文含义：成功。</summary>
        Success,
        /// <summary>中文含义：失败预备。</summary>
        FailureAnticipation,
        /// <summary>中文含义：失败环绕轨道。</summary>
        FailureOrbit,
        /// <summary>中文含义：失败接触稳定。</summary>
        FailureContactSettle,
        /// <summary>中文含义：失败保持。</summary>
        FailureHold,
        /// <summary>中文含义：失败后备。</summary>
        FailureFallback,
        /// <summary>中文含义：正面抓取失败压力。</summary>
        FrontGrabFailurePressure,
        /// <summary>中文含义：正面抓取失败环绕轨道。</summary>
        FrontGrabFailureOrbit,
        /// <summary>中文含义：正面抓取失败接触。</summary>
        FrontGrabFailureContact,
        /// <summary>中文含义：正面抓取失败保持。</summary>
        FrontGrabFailureHold
    }

    /// <summary>当前里昂配对动作协调器。</summary>
    [Header("系统引用")]
    public LeonQTECoordinator coordinator;
    /// <summary>摄像机控制器。</summary>
    public RE4CameraController cameraController;
    /// <summary>运行时根节点。</summary>
    public Transform runtimeRoot;
    /// <summary>预设集合。</summary>
    [Header("事件镜头")]
    public CameraPreset[] presets;
    /// <summary>运行时预设集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<QTEKind, RuntimePreset> runtimePresets = new Dictionary<QTEKind, RuntimePreset>();
    /// <summary>重叠缓冲区。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Collider[] overlapBuffer = new Collider[32];
    /// <summary>射线检测缓冲区。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly RaycastHit[] raycastBuffer = new RaycastHit[32];
    /// <summary>生效预设。</summary>
    private RuntimePreset activePreset;
    /// <summary>会话上下文。</summary>
    private QTESessionContext sessionContext;
    /// <summary>运动。</summary>
    private CameraMotion motion;
    /// <summary>玩家上部胸部。</summary>
    private Transform playerUpperChest;
    /// <summary>玩家髋部。</summary>
    private Transform playerHips;
    /// <summary>玩家颈部。</summary>
    private Transform playerNeck;
    /// <summary>玩家头部。</summary>
    private Transform playerHead;
    /// <summary>玩家左侧大腿。</summary>
    private Transform playerLeftUpperLeg;
    /// <summary>玩家右侧大腿。</summary>
    private Transform playerRightUpperLeg;
    /// <summary>玩家左侧小腿。</summary>
    private Transform playerLeftLowerLeg;
    /// <summary>玩家右侧小腿。</summary>
    private Transform playerRightLowerLeg;
    /// <summary>选定小腿。</summary>
    private Transform selectedLowerLeg;
    /// <summary>敌人胸部。</summary>
    private Transform enemyChest;
    /// <summary>敌人头部。</summary>
    private Transform enemyHead;
    /// <summary>焦点速度。</summary>
    private Vector3 focusVelocity;
    /// <summary>结果起始位置。</summary>
    private Vector3 resultStartPosition;
    /// <summary>结果目标位置。</summary>
    private Vector3 resultTargetPosition;
    /// <summary>运动已用。</summary>
    private float motionElapsed;
    /// <summary>运动持续时间。</summary>
    private float motionDuration;
    /// <summary>运动起始视野角度。</summary>
    private float motionStartFieldOfView;
    /// <summary>运动目标视野角度。</summary>
    private float motionTargetFieldOfView;
    /// <summary>抓取冲击起始时间。</summary>
    private float grabImpactStartTime = -1f;
    /// <summary>撕咬冲击起始时间。</summary>
    private float biteImpactStartTime = -1f;
    /// <summary>踢击冲击起始时间。</summary>
    private float kickImpactStartTime = -1f;
    /// <summary>反馈淡入淡出起始时间。</summary>
    private float feedbackFadeStartTime = -1f;
    /// <summary>撕咬腿部侧边符号。</summary>
    private float biteLegSideSign = -1f;
    /// <summary>环绕轨道侧边符号。</summary>
    private float orbitSideSign = -1f;
    /// <summary>保持样条位置。</summary>
    private float holdSplinePosition = 1f;
    /// <summary>失败稳定起始样条位置。</summary>
    private float failureSettleStartSplinePosition;
    /// <summary>正面抓取失败起始样条位置。</summary>
    private float frontGrabFailureStartSplinePosition;
    /// <summary>正面抓取失败压力样条位置。</summary>
    private float frontGrabFailurePressureSplinePosition;
    /// <summary>正面抓取失败前置接触样条位置。</summary>
    private float frontGrabFailurePreContactSplinePosition;
    /// <summary>正面抓取失败目标样条位置。</summary>
    private float frontGrabFailureTargetSplinePosition;
    /// <summary>入口观察点。</summary>
    private Vector3 entryLookPoint;
    /// <summary>活动会话编号。</summary>
    private int activeSessionId;
    /// <summary>是否摄像机已取得。</summary>
    private bool cameraTaken;
    /// <summary>失败环绕轨道是否受阻。</summary>
    private bool failureOrbitBlocked;
    /// <summary>正面抓取路径是否受阻。</summary>
    private bool frontGrabPathBlocked;
    /// <summary>是否正面抓取接触已接收。</summary>
    private bool frontGrabContactReceived;
    /// <summary>是否已开始。</summary>
    private bool started;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.GameObject(string)、QTECameraPresenter.BindEvents()、QTECameraPresenter.CreateRuntimePresets()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (runtimeRoot == null)
        {
            // 根游戏对象。
GameObject root = new GameObject("RuntimeQTECameraRoot");
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;
        }

        CreateRuntimePresets();
        started = true;
        BindEvents();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        RegisterNativeBlendOverride();
        if (started)
        {
            BindEvents();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        UnbindEvents();
        ReleaseCamera();
        UnregisterNativeBlendOverride();
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        UnbindEvents();
        ReleaseCamera();
        UnregisterNativeBlendOverride();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.UpdateCameraFeedback()、QTECameraPresenter.UpdateFocusTarget(bool)、QTECameraPresenter.UpdateMotion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (activeSessionId != 0 && activePreset == null)
        {
            if (sessionContext.kind == QTEKind.KnifeAssassination)
            {
                UpdateAssassinationCamera();
            }
            else
            {
                UpdateNativeCamera();
            }
            return;
        }
        if (activePreset == null || activeSessionId == 0)
        {
            return;
        }

        if (!sessionContext.IsValid || sessionContext.playerRoot == null || sessionContext.enemyRoot == null)
        {
            ReleaseCamera();
            return;
        }

        UpdateFocusTarget(false);
        UpdateMotion();
        UpdateCameraFeedback();
        if (sessionContext.kind == QTEKind.RearGrab)
        {
            UpdateRearComposition();
        }
    }

    /// <summary>CreateRuntimePresets 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Add(QTEKind, QTECameraPresenter.RuntimePreset)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Clear()、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.ContainsKey(QTEKind)、GameObject.SetActive(bool)、Object.Instantiate&lt;QTECameraRig&gt;(QTECameraRig, Transform, bool)、QTECameraPresenter.CreatePresetFromResources(QTEKind, string, string, string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.Start()；下游（含内部回调体） Debug.LogWarning(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Add(QTEKind, QTECameraPresenter.RuntimePreset)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Clear()、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.ContainsKey(QTEKind)、GameObject.SetActive(bool)、Object.Instantiate&lt;QTECameraRig&gt;(QTECameraRig, Transform, bool)、QTECameraPresenter.CreatePresetFromResources(QTEKind, string, string, string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CreateRuntimePresets()
    {
        runtimePresets.Clear();
        assassinationProfile = null;
        if (presets != null)
        {
            // preset（预设）：每轮从预设集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 Dictionary<QTEKind, QTECameraPresenter.RuntimePreset>.Add(QTEKind, QTECameraPresenter.RuntimePreset) 的参数、作为 Dictionary<QTEKind, QTECameraPresenter.RuntimePreset>.ContainsKey(QTEKind) 的参数 等共 5 处。
foreach (CameraPreset preset in presets)
            {
                if (preset == null || preset.kind == QTEKind.LegBite || preset.profile == null)
                {
                    continue;
                }
                if (preset.kind == QTEKind.KnifeAssassination)
                {
                    if (assassinationProfile == null)
                    {
                        assassinationProfile = preset.profile;
                    }
                    continue;
                }
                if (preset.rigPrefab == null)
                {
                    continue;
                }

                if (runtimePresets.ContainsKey(preset.kind))
                {
                    Debug.LogWarning($"[快速反应事件摄像机] 类型 {preset.kind} 存在重复配置，已忽略后者。", this);
                    continue;
                }

                // 实例。
QTECameraRig instance = Instantiate(preset.rigPrefab, runtimeRoot, false);
                instance.name = preset.rigPrefab.name;
                instance.gameObject.SetActive(false);
                runtimePresets.Add(preset.kind, new RuntimePreset { rig = instance, profile = preset.profile });
            }
        }

        CreatePresetFromResources(QTEKind.RearGrab, "QTECamera/RearGrabQTECameraRig", "QTECamera/RearGrabQTECameraProfile", "背后环抱镜头");
        if (assassinationProfile == null)
        {
            assassinationProfile = Resources.Load<QTECameraProfile>("QTECamera/KnifeAssassinationQTECameraProfile");
        }
    }

    /// <summary>CreatePresetFromResources 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Add(QTEKind, QTECameraPresenter.RuntimePreset)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.ContainsKey(QTEKind)、GameObject.SetActive(bool)、Object.Instantiate&lt;QTECameraRig&gt;(QTECameraRig, Transform, bool)、QTECameraPresenter.RuntimePreset.RuntimePreset()、Resources.Load&lt;QTECameraProfile&gt;(string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.CreateRuntimePresets()；下游（含内部回调体） Debug.LogError(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.Add(QTEKind, QTECameraPresenter.RuntimePreset)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.ContainsKey(QTEKind)、GameObject.SetActive(bool)、Object.Instantiate&lt;QTECameraRig&gt;(QTECameraRig, Transform, bool)、QTECameraPresenter.RuntimePreset.RuntimePreset()、Resources.Load&lt;QTECameraProfile&gt;(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">种类。</param>
    /// <param name="rigPath">绑定路径。</param>
    /// <param name="profilePath">配置档路径。</param>
    /// <param name="displayName">中文含义：显示名称。</param>
    private void CreatePresetFromResources(QTEKind kind, string rigPath, string profilePath, string displayName)
    {
        if (runtimePresets.ContainsKey(kind))
        {
            return;
        }

        // 绑定预制件。
QTECameraRig rigPrefab = Resources.Load<QTECameraRig>(rigPath);
        // 配置档。
QTECameraProfile profile = Resources.Load<QTECameraProfile>(profilePath);
        if (rigPrefab == null || profile == null)
        {
            Debug.LogError("[快速反应事件摄像机] 咬腿镜头资源缺失，会话继续使用游戏摄像机。", this);
            return;
        }

        // 实例。
QTECameraRig instance = Instantiate(rigPrefab, runtimeRoot, false);
        instance.name = rigPrefab.name;
        instance.gameObject.SetActive(false);
        runtimePresets.Add(kind, new RuntimePreset { rig = instance, profile = profile });
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.OnEnable()、QTECameraPresenter.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnQTESessionStarted += HandleSessionStarted;
        coordinator.OnQTEPhaseChanged += HandlePhaseChanged;
        coordinator.OnQTEBiteContact += HandleBiteContact;
        coordinator.OnQTEAssassinationContact += HandleAssassinationContact;
        coordinator.OnQTESessionEnded += HandleSessionEnded;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.OnDestroy()、QTECameraPresenter.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnQTESessionStarted -= HandleSessionStarted;
        coordinator.OnQTEPhaseChanged -= HandlePhaseChanged;
        coordinator.OnQTEBiteContact -= HandleBiteContact;
        coordinator.OnQTEAssassinationContact -= HandleAssassinationContact;
        coordinator.OnQTESessionEnded -= HandleSessionEnded;
        eventsBound = false;
    }

    /// <summary>HandleSessionStarted 的业务处理入口；本方法直接依赖 Debug.Log(object, Object)、Debug.LogError(object, Object)、Debug.LogWarning(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.TryGetValue(QTEKind, out QTECameraPresenter.RuntimePreset)、GameObject.SetActive(bool)、LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)、QTECameraPresenter.CacheFocusBones() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object, Object)、Debug.LogError(object, Object)、Debug.LogWarning(object, Object)、Dictionary&lt;QTEKind, QTECameraPresenter.RuntimePreset&gt;.TryGetValue(QTEKind, out QTECameraPresenter.RuntimePreset)、GameObject.SetActive(bool)、LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)、QTECameraPresenter.CacheFocusBones() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="kind">种类。</param>
    private void HandleSessionStarted(int sessionId, QTEKind kind)
    {
        ReleaseCamera();
        if (kind == QTEKind.KnifeAssassination)
        {
            StartAssassinationCamera(sessionId);
            return;
        }
        if (TryStartNativeCameraSession(sessionId))
        {
            return;
        }
        if (!runtimePresets.TryGetValue(kind, out RuntimePreset /* 中文含义：预设。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ preset))
        {
            Debug.LogWarning($"[快速反应事件摄像机] 类型 {kind} 没有镜头配置，会话继续使用游戏摄像机。", this);
            return;
        }

        if (!coordinator.TryGetSessionContext(sessionId, out QTESessionContext /* 中文含义：上下文。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ context))
        {
            Debug.LogWarning("[快速反应事件摄像机] 无法读取当前会话上下文，会话继续使用游戏摄像机。", this);
            return;
        }

        if (kind == QTEKind.RearGrab && !CanUseRearGrabCamera(context))
        {
            Debug.Log("[快速反应事件摄像机] 背后环抱镜头只用于普通村民，当前敌人继续使用游戏摄像机。", this);
            return;
        }

        if (!preset.rig.IsConfigured)
        {
            Debug.LogError("[快速反应事件摄像机] 镜头预制件引用不完整，会话继续使用游戏摄像机。", preset.rig);
            return;
        }

        activePreset = preset;
        sessionContext = context;
        activeSessionId = sessionId;
        activePreset.rig.gameObject.SetActive(true);
        CacheFocusBones();
        UpdateFocusTarget(true);
        // 初始视野角度。
float initialFieldOfView;
        if (cameraController != null && cameraController.MainCamera != null)
        {
            initialFieldOfView = cameraController.MainCamera.fieldOfView;
        }
        else
        {
            initialFieldOfView = activePreset.profile.holdingFieldOfView;
        }

        activePreset.rig.Prepare(initialFieldOfView);
        grabImpactStartTime = -1f;
        biteImpactStartTime = -1f;
        kickImpactStartTime = -1f;
        feedbackFadeStartTime = -1f;
        orbitSideSign = -1f;
        holdSplinePosition = activePreset.profile.entrySplinePosition;
        failureOrbitBlocked = false;
        frontGrabPathBlocked = false;
        frontGrabContactReceived = false;
        motion = CameraMotion.None;
    }

    /// <summary>CanUseRearGrabCamera 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;VillagerController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandleSessionStarted(int, QTEKind)；下游（含内部回调体） Component.GetComponentInParent&lt;VillagerController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="context">上下文。</param>
    private static bool CanUseRearGrabCamera(QTESessionContext context)
    {
        if (context.enemyRoot == null)
        {
            return false;
        }

        // 村民。
VillagerController villager = context.enemyRoot.GetComponentInParent<VillagerController>();
        return villager != null && villager.missionEnemyType == MissionEnemyType.Ganado;
    }

    /// <summary>HandlePhaseChanged 的业务处理入口；本方法直接依赖 QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HoldAtEnd()、QTECameraPresenter.ReleaseCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HoldAtEnd()、QTECameraPresenter.ReleaseCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="phase">阶段。</param>
    private void HandlePhaseChanged(int sessionId, QTEPhase phase)
    {
        if (sessionId == 0 || sessionId != activeSessionId)
        {
            return;
        }
        if (activePreset == null)
        {
            if (sessionContext.kind == QTEKind.KnifeAssassination && phase == QTEPhase.SuccessResult
                && !coordinator.HasNativePairCameraClock(sessionId))
            {
                HandleAssassinationContact(sessionId);
            }
            if (phase is QTEPhase.Exiting or QTEPhase.Completed or QTEPhase.Cancelled)
            {
                ReleaseCamera();
            }
            return;
        }

        switch (phase)
        {
            case QTEPhase.Entering:
                BeginEntry();
                break;
            case QTEPhase.WaitingForInput:
                HoldAtEnd();
                break;
            case QTEPhase.SuccessResult:
                BeginResult(true);
                break;
            case QTEPhase.FailureResult:
                BeginResult(false);
                break;
            case QTEPhase.Exiting:
            case QTEPhase.Completed:
            case QTEPhase.Cancelled:
                ReleaseCamera();
                break;
        }
    }

    /// <summary>HandleSessionEnded 的业务处理入口；本方法直接依赖 QTECameraPresenter.ReleaseCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTECameraPresenter.ReleaseCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="endInfo">结束信息。</param>
    private void HandleSessionEnded(QTESessionEndInfo endInfo)
    {
        if (endInfo.sessionId == activeSessionId)
        {
            ReleaseCamera();
        }
    }

    /// <summary>HandleBiteContact 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    private void HandleBiteContact(int sessionId)
    {
        if (sessionId == 0 || sessionId != activeSessionId || activePreset == null || !cameraTaken)
        {
            return;
        }

        // 绑定。
QTECameraRig rig = activePreset.rig;
        // 配置档。
QTECameraProfile profile = activePreset.profile;
        if (sessionContext.kind == QTEKind.FrontGrab)
        {
            if (frontGrabContactReceived)
            {
                return;
            }

            frontGrabContactReceived = true;
            biteImpactStartTime = Time.time;
            if (frontGrabPathBlocked)
            {
                motion = CameraMotion.FrontGrabFailureHold;
                return;
            }

            failureSettleStartSplinePosition = rig.SplinePosition;
            motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
            motionTargetFieldOfView = profile.frontGrabFailureFieldOfView;
            motionElapsed = 0f;
            motionDuration = Mathf.Max(0.01f, profile.frontGrabFailureContactDuration);
            motion = CameraMotion.FrontGrabFailureContact;
            return;
        }

        if (sessionContext.kind == QTEKind.LegBite)
        {
            biteImpactStartTime = Time.time;
            return;
        }

        if (sessionContext.kind != QTEKind.RearGrab)
        {
            return;
        }

        biteImpactStartTime = Time.time;
        if (failureOrbitBlocked || rig.SplinePosition >= 0.999f)
        {
            return;
        }

        failureSettleStartSplinePosition = rig.SplinePosition;
        motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
        motionTargetFieldOfView = profile.failureFieldOfView;
        motionElapsed = 0f;
        motionDuration = Mathf.Max(0.01f, profile.biteContactSettleDuration);
        motion = CameraMotion.FailureContactSettle;
    }

    /// <summary>BeginEntry 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、Mathf.Max(float, float)、Mathf.Min(int, int)、QTECameraPresenter.GetPreferredPath(QTECameraRig, QTECameraProfile)、QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.ScorePath(SplineContainer, QTECameraProfile)、QTECameraPresenter.SetupPairRoot() 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandlePhaseChanged(int, QTEPhase)；下游（含内部回调体） Debug.LogWarning(object, Object)、Mathf.Max(float, float)、Mathf.Min(int, int)、QTECameraPresenter.GetPreferredPath(QTECameraRig, QTECameraProfile)、QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.ScorePath(SplineContainer, QTECameraProfile)、QTECameraPresenter.SetupPairRoot() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginEntry()
    {
        SetupPairRoot();
        // 绑定。
QTECameraRig rig = activePreset.rig;
        // 配置档。
QTECameraProfile profile = activePreset.profile;
        // 输出摄像机。
Camera outputCamera;
        if (cameraController != null)
        {
            outputCamera = cameraController.MainCamera;
        }
        else
        {
            outputCamera = null;
        }

        // 输出位置。
Vector3 outputPosition;
        if (outputCamera != null)
        {
            outputPosition = outputCamera.transform.position;
        }
        else
        {
            outputPosition = rig.primaryPath.transform.TransformPoint(rig.primaryPath.Spline[0].Position);
        }

        // 输出前向。
Vector3 outputForward;
        if (outputCamera != null)
        {
            outputForward = outputCamera.transform.forward;
        }
        else
        {
            outputForward = sessionContext.playerRoot.forward;
        }

        rig.ConfigureDynamicEntry(outputPosition, profile.dynamicEntryTangentScale);
        // 优先路径。
SplineContainer preferredPath = GetPreferredPath(rig, profile);
        UpdateFocusTarget(true);
        // 备选路径。
SplineContainer alternativePath;
        if (preferredPath == rig.primaryPath)
        {
            alternativePath = rig.fallbackPath;
        }
        else
        {
            alternativePath = rig.primaryPath;
        }

        // 主要分值。
int primaryScore = ScorePath(preferredPath, profile);
        // 后备分值。
int fallbackScore = ScorePath(alternativePath, profile);
        // 是否优先选择连续性。
bool preferContinuity = primaryScore <= fallbackScore + profile.sidePreferenceScoreTolerance;
        if (sessionContext.kind == QTEKind.RearGrab && outputCamera != null)
        {
            preferContinuity = ScoreRearPath(preferredPath, profile, primaryScore) <=
                ScoreRearPath(alternativePath, profile, fallbackScore);
            rig.splineDolly.enabled = false;
            rig.rotationComposer.enabled = false;
        }
        // 选定路径。
SplineContainer selectedPath;
        if (preferContinuity)
        {
            selectedPath = preferredPath;
        }
        else
        {
            selectedPath = alternativePath;
        }

        rig.SelectPath(selectedPath);
        if (selectedPath == rig.fallbackPath)
        {
            orbitSideSign = 1f;
        }
        else
        {
            orbitSideSign = -1f;
        }

        failureOrbitBlocked = sessionContext.kind == QTEKind.RearGrab && Mathf.Min(primaryScore, fallbackScore) >= profile.blockedPathScore;
        frontGrabPathBlocked = sessionContext.kind == QTEKind.FrontGrab && Mathf.Min(primaryScore, fallbackScore) >= profile.blockedPathScore;
        if (profile.entryKnotIndex >= 0)
        {
            holdSplinePosition = rig.GetNormalizedPositionAtKnot(profile.entryKnotIndex);
        }
        else
        {
            holdSplinePosition = profile.entrySplinePosition;
        }

        rig.SetSplinePosition(0f);
        rig.resultOffsetRoot.localPosition = Vector3.zero;
        rig.resultOffsetRoot.localRotation = Quaternion.identity;
        // 观察距离。
float lookDistance = Mathf.Max(2f, Vector3.Distance(outputPosition, rig.focusTarget.position));
        entryLookPoint = outputPosition + outputForward * lookDistance;
        rig.focusTarget.position = entryLookPoint;
        motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
        motionTargetFieldOfView = profile.holdingFieldOfView;
        motionElapsed = 0f;
        motionDuration = Mathf.Max(0.01f, profile.entryDuration);
        if (profile.enableCameraFeedback)
        {
            grabImpactStartTime = Time.time + profile.grabImpactDelay;
        }

        nativeExitBlendSeconds = .23f;
        nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
        cameraTaken = cameraController != null && (sessionContext.kind == QTEKind.RearGrab
            || cameraController.RequestExternalCamera(this, rig.qteCamera, true));
        if (!cameraTaken)
        {
            Debug.LogWarning("[快速反应事件摄像机] 外部镜头正被其他系统占用，本次会话继续使用游戏摄像机。", this);
            ReleaseCamera();
            return;
        }

        motion = CameraMotion.Entering;
    }

    /// <summary>HoldAtEnd 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、QTECameraRig.SetFieldOfView(float)、QTECameraRig.SetSplinePosition(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandlePhaseChanged(int, QTEPhase)；下游（含内部回调体） Mathf.Max(float, float)、QTECameraRig.SetFieldOfView(float)、QTECameraRig.SetSplinePosition(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HoldAtEnd()
    {
        if (!cameraTaken)
        {
            return;
        }

        activePreset.rig.SetSplinePosition(holdSplinePosition);
        activePreset.rig.SetFieldOfView(activePreset.profile.holdingFieldOfView);
        motionElapsed = 0f;
        motionDuration = Mathf.Max(0.01f, activePreset.profile.holdingDriftDuration);
        motion = CameraMotion.Holding;
    }

    /// <summary>BeginResult 的业务处理入口；本方法直接依赖 CameraState.GetFinalPosition()、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、QTECameraPresenter.HandleAssassinationContact(int)、QTECameraRig.GetNormalizedPositionAtKnot(int)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandlePhaseChanged(int, QTEPhase)；下游（含内部回调体） CameraState.GetFinalPosition()、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、QTECameraPresenter.HandleAssassinationContact(int)、QTECameraRig.GetNormalizedPositionAtKnot(int)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">是否成功。</param>
    private void BeginResult(bool success)
    {
        if (!cameraTaken)
        {
            return;
        }

        // 绑定。
QTECameraRig rig = activePreset.rig;
        // 配置档。
QTECameraProfile profile = activePreset.profile;
        // 摄像机位置。
Vector3 cameraPosition = rig.qteCamera.State.GetFinalPosition();
        // 焦点位置。
Vector3 focusPosition = rig.focusTarget.position;
        // 远离。
Vector3 away = cameraPosition - focusPosition;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = -sessionContext.playerRoot.forward;
        }

        away.Normalize();
        resultStartPosition = rig.resultOffsetRoot.position;
        motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
        if (success)
        {
            // 侧向偏移量。
Vector3 lateralOffset;
            if (sessionContext.kind == QTEKind.RearGrab || sessionContext.kind == QTEKind.FrontGrab)
            {
                lateralOffset = rig.pairRoot.right * orbitSideSign * profile.successSideOffset;
            }
            else
            {
                lateralOffset = Vector3.zero;
            }

            resultTargetPosition = resultStartPosition + away * profile.successPullBackDistance + Vector3.up * profile.successVerticalOffset + lateralOffset;
            motionDuration = Mathf.Max(0.01f, profile.successDuration);
            motionTargetFieldOfView = profile.successFieldOfView;
            motion = CameraMotion.Success;
            if (profile.enableCameraFeedback)
            {
                kickImpactStartTime = Time.time + profile.kickImpactDelay;
            }
        }
        else
        {
            if (sessionContext.kind == QTEKind.FrontGrab)
            {
                feedbackFadeStartTime = Time.time;
                frontGrabFailureStartSplinePosition = rig.SplinePosition;
                frontGrabFailureTargetSplinePosition = rig.GetNormalizedPositionAtKnot(profile.frontGrabFailureKnotIndex);
                frontGrabFailurePressureSplinePosition = Mathf.Lerp(frontGrabFailureStartSplinePosition, frontGrabFailureTargetSplinePosition, profile.frontGrabFailurePressureTravel);
                frontGrabFailurePreContactSplinePosition = Mathf.Lerp(frontGrabFailureStartSplinePosition, frontGrabFailureTargetSplinePosition, profile.frontGrabFailurePreContactTravel);
                motionTargetFieldOfView = profile.frontGrabFailureFieldOfView;
                motionElapsed = 0f;
                motionDuration = Mathf.Max(0.01f, profile.frontGrabFailurePressureDuration);
                if (frontGrabPathBlocked)
                {
                    motion = CameraMotion.FrontGrabFailureHold;
                }
                else
                {
                    motion = CameraMotion.FrontGrabFailurePressure;
                }

                return;
            }

            motionTargetFieldOfView = profile.failureFieldOfView;
            if (sessionContext.kind == QTEKind.RearGrab && !failureOrbitBlocked)
            {
                motionDuration = Mathf.Max(0.01f, profile.failureAnticipationDuration);
                motion = CameraMotion.FailureAnticipation;
                feedbackFadeStartTime = Time.time;
                motionElapsed = 0f;
                return;
            }

            resultTargetPosition = resultStartPosition - away * profile.failurePushInDistance + Vector3.up * profile.failureVerticalOffset;
            motionDuration = Mathf.Max(0.01f, profile.failureDuration);
            motion = CameraMotion.FailureFallback;
        }

        feedbackFadeStartTime = Time.time;
        motionElapsed = 0f;
    }

    /// <summary>UpdateMotion 的业务处理入口；本方法直接依赖 AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、QTECameraRig.SetFieldOfView(float)、QTECameraRig.SetSplinePosition(float)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.LateUpdate()；下游（含内部回调体） AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、QTECameraRig.SetFieldOfView(float)、QTECameraRig.SetSplinePosition(float)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateMotion()
    {
        if (!cameraTaken || activePreset == null)
        {
            return;
        }

        // 绑定。
QTECameraRig rig = activePreset.rig;
        // 配置档。
QTECameraProfile profile = activePreset.profile;
        switch (motion)
        {
            case CameraMotion.Entering:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved;
                if (profile.entryCurve != null)
                {
                    curved = profile.entryCurve.Evaluate(normalized);
                }
                else
                {
                    curved = normalized;
                }

                rig.SetSplinePosition(curved * holdSplinePosition);
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved));
                if (normalized >= 1f)
                {
                    motionElapsed = 0f;
                    motionDuration = Mathf.Max(0.01f, profile.holdingDriftDuration);
                    motion = CameraMotion.Holding;
                }

                break;
            }

            case CameraMotion.Holding:
            {
                if (sessionContext.kind == QTEKind.RearGrab)
                {
                    rig.SetSplinePosition(holdSplinePosition);
                    break;
                }

                if (sessionContext.kind == QTEKind.FrontGrab)
                {
                    rig.SetSplinePosition(holdSplinePosition);
                    break;
                }

                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                rig.SetSplinePosition(Mathf.Lerp(holdSplinePosition, 1f, curved));
                break;
            }

            case CameraMotion.Success:
            case CameraMotion.FailureFallback:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                rig.resultOffsetRoot.position = Vector3.Lerp(resultStartPosition, resultTargetPosition, curved);
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved));
                if (normalized >= 1f)
                {
                    motion = CameraMotion.None;
                }

                break;
            }

            case CameraMotion.FailureAnticipation:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                // 预备结束。
float anticipationEnd = Mathf.Lerp(holdSplinePosition, 1f, profile.failureAnticipationTravel);
                rig.SetSplinePosition(Mathf.Lerp(holdSplinePosition, anticipationEnd, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved * 0.25f));
                if (normalized >= 1f)
                {
                    motionElapsed = 0f;
                    motionDuration = Mathf.Max(0.01f, profile.failureOrbitDuration);
                    motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
                    motion = CameraMotion.FailureOrbit;
                }

                break;
            }

            case CameraMotion.FailureOrbit:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved;
                if (profile.failureOrbitCurve != null)
                {
                    curved = profile.failureOrbitCurve.Evaluate(normalized);
                }
                else
                {
                    curved = normalized;
                }

                // 环绕轨道起始。
float orbitStart = Mathf.Lerp(holdSplinePosition, 1f, profile.failureAnticipationTravel);
                rig.SetSplinePosition(Mathf.Lerp(orbitStart, 1f, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved));
                if (normalized >= 1f)
                {
                    motion = CameraMotion.FailureHold;
                }

                break;
            }

            case CameraMotion.FailureContactSettle:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                rig.SetSplinePosition(Mathf.Lerp(failureSettleStartSplinePosition, 1f, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved));
                if (normalized >= 1f)
                {
                    motion = CameraMotion.FailureHold;
                }

                break;
            }

            case CameraMotion.FailureHold:
                break;
            case CameraMotion.FrontGrabFailurePressure:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                rig.SetSplinePosition(Mathf.Lerp(frontGrabFailureStartSplinePosition, frontGrabFailurePressureSplinePosition, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved * 0.22f));
                if (normalized >= 1f)
                {
                    motionElapsed = 0f;
                    motionDuration = Mathf.Max(0.01f, profile.frontGrabFailureOrbitDuration);
                    motionStartFieldOfView = rig.qteCamera.Lens.FieldOfView;
                    motion = CameraMotion.FrontGrabFailureOrbit;
                }

                break;
            }

            case CameraMotion.FrontGrabFailureOrbit:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved;
                if (profile.failureOrbitCurve != null)
                {
                    curved = profile.failureOrbitCurve.Evaluate(normalized);
                }
                else
                {
                    curved = normalized;
                }

                rig.SetSplinePosition(Mathf.Lerp(frontGrabFailurePressureSplinePosition, frontGrabFailurePreContactSplinePosition, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved * 0.82f));
                if (normalized >= 1f)
                {
                    motion = CameraMotion.FrontGrabFailureHold;
                }

                break;
            }

            case CameraMotion.FrontGrabFailureContact:
            {
                motionElapsed += Time.deltaTime;
                // 归一化。
float normalized = Mathf.Clamp01(motionElapsed / motionDuration);
                // 弯曲。
float curved = normalized * normalized * (3f - 2f * normalized);
                rig.SetSplinePosition(Mathf.Lerp(failureSettleStartSplinePosition, frontGrabFailureTargetSplinePosition, curved));
                rig.SetFieldOfView(Mathf.Lerp(motionStartFieldOfView, motionTargetFieldOfView, curved));
                if (normalized >= 1f)
                {
                    motion = CameraMotion.FrontGrabFailureHold;
                }

                break;
            }

            case CameraMotion.FrontGrabFailureHold:
                if (frontGrabContactReceived && !frontGrabPathBlocked)
                {
                    rig.SetSplinePosition(frontGrabFailureTargetSplinePosition);
                }

                break;
        }
    }

    /// <summary>SetupPairRoot 的业务处理入口；本方法直接依赖 Quaternion.LookRotation(Vector3, Vector3)、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()；下游（含内部回调体） Quaternion.LookRotation(Vector3, Vector3)、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SetupPairRoot()
    {
        // 前向。
Vector3 forward = sessionContext.enemyRoot.position - sessionContext.playerRoot.position;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = sessionContext.playerRoot.forward;
        }

        forward.Normalize();
        activePreset.rig.pairRoot.SetPositionAndRotation(sessionContext.playerRoot.position, Quaternion.LookRotation(forward, Vector3.up));
        activePreset.rig.resultOffsetRoot.localPosition = Vector3.zero;
        activePreset.rig.resultOffsetRoot.localRotation = Quaternion.identity;
    }

    /// <summary>CacheFocusBones 的业务处理入口；本方法直接依赖 QTECameraPresenter.GetHumanBone(Animator, HumanBodyBones)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandleSessionStarted(int, QTEKind)；下游（含内部回调体） QTECameraPresenter.GetHumanBone(Animator, HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheFocusBones()
    {
        playerUpperChest = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.UpperChest);
        playerHips = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.Hips);
        playerNeck = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.Neck);
        playerHead = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.Head);
        playerLeftLowerLeg = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.LeftLowerLeg);
        playerRightLowerLeg = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.RightLowerLeg);
        playerLeftUpperLeg = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.LeftUpperLeg);
        playerRightUpperLeg = GetHumanBone(sessionContext.playerAnimator, HumanBodyBones.RightUpperLeg);
        enemyChest = GetHumanBone(sessionContext.enemyAnimator, HumanBodyBones.Chest);
        enemyHead = GetHumanBone(sessionContext.enemyAnimator, HumanBodyBones.Head);
    }

    /// <summary>UpdateFocusTarget 的业务处理入口；本方法直接依赖 QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.UpdateAssassinationFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateRearBiteFocusTarget(QTECameraProfile, bool) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()、QTECameraPresenter.HandleSessionStarted(int, QTEKind)、QTECameraPresenter.LateUpdate()；下游（含内部回调体） QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.UpdateAssassinationFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateRearBiteFocusTarget(QTECameraProfile, bool) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="immediate">是否立即。</param>
    private void UpdateFocusTarget(bool immediate)
    {
        if (activePreset == null)
        {
            return;
        }

        // 配置档。
QTECameraProfile profile = activePreset.profile;
        if (profile.focusMode == QTECameraFocusMode.LegBiteInteractionCenter)
        {
            UpdateLegBiteFocusTarget(profile, immediate);
            return;
        }

        if (profile.focusMode == QTECameraFocusMode.HighLegBiteInteractionCenter)
        {
            UpdateHighAngleLegBiteFocusTarget(profile, immediate);
            return;
        }

        if (profile.focusMode == QTECameraFocusMode.NeckBiteInteractionCenter)
        {
            UpdateRearBiteFocusTarget(profile, immediate);
            return;
        }

        if (profile.focusMode == QTECameraFocusMode.FrontBreakFreeInteractionCenter)
        {
            UpdateFrontGrabFocusTarget(profile, immediate);
            return;
        }

        // 玩家胸部位置。
Vector3 playerChestPosition = GetBonePosition(playerUpperChest, playerHead, sessionContext.playerRoot, 1.42f);
        // 玩家颈部位置。
Vector3 playerNeckPosition = GetBonePosition(playerNeck, playerHead, sessionContext.playerRoot, 1.62f);
        // 敌人胸部位置。
Vector3 enemyChestPosition = GetBonePosition(enemyChest, enemyHead, sessionContext.enemyRoot, 1.4f);
        // 敌人头部位置。
Vector3 enemyHeadPosition = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, 1.7f);
        // 玩家焦点。
Vector3 playerFocus = Vector3.Lerp(playerChestPosition, playerNeckPosition, profile.playerNeckBlend);
        // 敌人焦点。
Vector3 enemyFocus = Vector3.Lerp(enemyChestPosition, enemyHeadPosition, profile.enemyHeadBlend);
        // 期望。
Vector3 desired = Vector3.Lerp(playerFocus, enemyFocus, profile.enemyFocusWeight);
        desired += activePreset.rig.pairRoot.TransformVector(profile.focusOffset);
        ApplyFocusPosition(desired, profile, immediate);
    }

    /// <summary>UpdateFrontGrabFocusTarget 的业务处理入口；本方法直接依赖 QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.IsFrontGrabFailureMotion()、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateFocusTarget(bool)；下游（含内部回调体） QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.IsFrontGrabFailureMotion()、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    /// <param name="immediate">是否立即。</param>
    private void UpdateFrontGrabFocusTarget(QTECameraProfile profile, bool immediate)
    {
        // 玩家胸部位置。
Vector3 playerChestPosition = GetBonePosition(playerUpperChest, playerNeck, sessionContext.playerRoot, 1.42f);
        // 玩家头部位置。
Vector3 playerHeadPosition = GetBonePosition(playerHead, playerNeck, sessionContext.playerRoot, 1.68f);
        // 敌人胸部位置。
Vector3 enemyChestPosition = GetBonePosition(enemyChest, enemyHead, sessionContext.enemyRoot, 1.4f);
        // 敌人头部位置。
Vector3 enemyHeadPosition = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, 1.7f);
        // 玩家肩部。
Vector3 playerShoulder = Vector3.Lerp(playerChestPosition, playerHeadPosition, 0.58f);
        // 敌人面。
Vector3 enemyFace = Vector3.Lerp(enemyChestPosition, enemyHeadPosition, 0.82f);
        // 敌人权重。
float enemyWeight;
        if (IsFrontGrabFailureMotion())
        {
            enemyWeight = profile.frontGrabFailureEnemyWeight;
        }
        else
        {
            enemyWeight = profile.frontGrabEnemyFaceWeight;
        }

        // 期望。
Vector3 desired = Vector3.Lerp(playerShoulder, enemyFace, enemyWeight);
        desired += activePreset.rig.pairRoot.TransformVector(profile.focusOffset);
        if (IsFrontGrabFailureMotion())
        {
            desired += activePreset.rig.pairRoot.TransformVector(profile.frontGrabFailureFocusOffset);
        }

        ApplyFocusPosition(desired, profile, immediate);
    }

    /// <summary>UpdateRearBiteFocusTarget 的业务处理入口；本方法直接依赖 QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.IsFailureMotion()、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateFocusTarget(bool)；下游（含内部回调体） QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.IsFailureMotion()、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    /// <param name="immediate">是否立即。</param>
    private void UpdateRearBiteFocusTarget(QTECameraProfile profile, bool immediate)
    {
        // 玩家颈部位置。
Vector3 playerNeckPosition = GetBonePosition(playerNeck, playerHead, sessionContext.playerRoot, 1.58f);
        // 敌人胸部位置。
Vector3 enemyChestPosition = GetBonePosition(enemyChest, enemyHead, sessionContext.enemyRoot, 1.42f);
        // 敌人头部位置。
Vector3 enemyHeadPosition = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, 1.7f);
        // 敌人上半身。
Vector3 enemyUpperBody = Vector3.Lerp(enemyChestPosition, enemyHeadPosition, 0.72f);
        // 期望。
Vector3 desired = Vector3.Lerp(playerNeckPosition, enemyUpperBody, profile.rearBiteEnemyHeadWeight);
        desired += activePreset.rig.pairRoot.TransformVector(profile.focusOffset);
        if (IsFailureMotion())
        {
            desired += activePreset.rig.pairRoot.TransformVector(profile.rearBiteFailureFocusOffset);
        }

        ApplyFocusPosition(desired, profile, immediate);
    }

    /// <summary>UpdateLegBiteFocusTarget 的业务处理入口；本方法直接依赖 QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateFocusTarget(bool)；下游（含内部回调体） QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    /// <param name="immediate">是否立即。</param>
    private void UpdateLegBiteFocusTarget(QTECameraProfile profile, bool immediate)
    {
        SelectLowerLegIfNeeded(profile);
        // 小腿位置。
Vector3 lowerLegPosition = GetBonePosition(selectedLowerLeg, null, sessionContext.playerRoot, profile.lowerLegFallbackHeight);
        // 敌人头部位置。
Vector3 enemyHeadPosition = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, profile.lowerLegFallbackHeight);
        // 期望。
Vector3 desired = Vector3.Lerp(lowerLegPosition, enemyHeadPosition, profile.legBiteEnemyHeadWeight);
        desired += activePreset.rig.pairRoot.TransformVector(profile.focusOffset);
        ApplyFocusPosition(desired, profile, immediate);
    }

    /// <summary>UpdateHighAngleLegBiteFocusTarget 的业务处理入口；本方法直接依赖 QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateFocusTarget(bool)；下游（含内部回调体） QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float)、QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Transform.TransformVector(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    /// <param name="immediate">是否立即。</param>
    private void UpdateHighAngleLegBiteFocusTarget(QTECameraProfile profile, bool immediate)
    {
        SelectLowerLegIfNeeded(profile);
        // 大腿。
Transform upperLeg;
        if (selectedLowerLeg == playerLeftLowerLeg)
        {
            upperLeg = playerLeftUpperLeg;
        }
        else
        {
            upperLeg = playerRightUpperLeg;
        }

        // 髋部位置。
Vector3 hipsPosition = GetBonePosition(playerHips, upperLeg, sessionContext.playerRoot, 0.95f);
        // 大腿位置。
Vector3 upperLegPosition = GetBonePosition(upperLeg, selectedLowerLeg, sessionContext.playerRoot, 0.72f);
        // 玩家大腿位置。
Vector3 playerThighPosition = Vector3.Lerp(hipsPosition, upperLegPosition, profile.highAngleThighBlend);
        // 敌人胸部位置。
Vector3 enemyChestPosition = GetBonePosition(enemyChest, enemyHead, sessionContext.enemyRoot, 0.82f);
        // 敌人头部位置。
Vector3 enemyHeadPosition = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, 1.05f);
        // 敌人上半身位置。
Vector3 enemyUpperBodyPosition = Vector3.Lerp(enemyChestPosition, enemyHeadPosition, 0.28f);
        // 期望。
Vector3 desired = Vector3.Lerp(playerThighPosition, enemyUpperBodyPosition, profile.highAngleEnemyUpperBodyWeight);
        if (motion == CameraMotion.Success)
        {
            desired = Vector3.Lerp(desired, playerThighPosition, profile.highAngleSuccessPlayerWeight);
        }

        desired += activePreset.rig.pairRoot.right * biteLegSideSign * profile.highAngleBiteLegSideOffset;
        desired += activePreset.rig.pairRoot.TransformVector(profile.focusOffset);
        ApplyFocusPosition(desired, profile, immediate);
    }

    /// <summary>ApplyFocusPosition 的业务处理入口；本方法直接依赖 AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateAssassinationFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateFocusTarget(bool)、QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateLegBiteFocusTarget(QTECameraProfile, bool) 等共 6 处；下游（含内部回调体） AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="desired">期望。</param>
    /// <param name="profile">配置档。</param>
    /// <param name="immediate">是否立即。</param>
    private void ApplyFocusPosition(Vector3 desired, QTECameraProfile profile, bool immediate)
    {
        if ((sessionContext.kind == QTEKind.RearGrab || sessionContext.kind == QTEKind.FrontGrab) && motion == CameraMotion.Entering)
        {
            // 归一化。
float normalized;
            if (motionDuration > 0f)
            {
                normalized = Mathf.Clamp01(motionElapsed / motionDuration);
            }
            else
            {
                normalized = 1f;
            }

            // 弯曲。
float curved;
            if (profile.entryCurve != null)
            {
                curved = profile.entryCurve.Evaluate(normalized);
            }
            else
            {
                curved = normalized;
            }

            desired = Vector3.Lerp(entryLookPoint, desired, curved);
        }

        if (immediate || profile.focusSmoothTime <= 0f)
        {
            activePreset.rig.focusTarget.position = desired;
            focusVelocity = Vector3.zero;
            return;
        }

        activePreset.rig.focusTarget.position = Vector3.SmoothDamp(activePreset.rig.focusTarget.position, desired, ref focusVelocity, profile.focusSmoothTime, Mathf.Infinity, Time.deltaTime);
    }

    /// <summary>IsFailureMotion 的业务处理入口；本方法直接依赖 QTECameraPresenter.IsFrontGrabFailureMotion()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateRearBiteFocusTarget(QTECameraProfile, bool)；下游（含内部回调体） QTECameraPresenter.IsFrontGrabFailureMotion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFailureMotion()
    {
        return motion == CameraMotion.FailureAnticipation || motion == CameraMotion.FailureOrbit || motion == CameraMotion.FailureContactSettle || motion == CameraMotion.FailureHold || motion == CameraMotion.FailureFallback || IsFrontGrabFailureMotion();
    }

    /// <summary>IsFrontGrabFailureMotion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.IsFailureMotion()、QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFrontGrabFailureMotion()
    {
        return motion == CameraMotion.FrontGrabFailurePressure || motion == CameraMotion.FrontGrabFailureOrbit || motion == CameraMotion.FrontGrabFailureContact || motion == CameraMotion.FrontGrabFailureHold;
    }

    /// <summary>SelectLowerLegIfNeeded 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、Vector3.SqrMagnitude(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.GetPreferredPath(QTECameraRig, QTECameraProfile)、QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateLegBiteFocusTarget(QTECameraProfile, bool)；下游（含内部回调体） object.ReferenceEquals(object, object)、Vector3.SqrMagnitude(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    private void SelectLowerLegIfNeeded(QTECameraProfile profile)
    {
        if (selectedLowerLeg != null || !profile.preferClosestLowerLeg)
        {
            if (selectedLowerLeg == null)
            {
                // 空值判断前缓存的玩家左侧小腿。
global::UnityEngine.Transform availableValue18 = playerLeftLowerLeg;
                if (!global::System.Object.ReferenceEquals(availableValue18, null))
                {
                    selectedLowerLeg = availableValue18;
                }
                else
                {
                    selectedLowerLeg = playerRightLowerLeg;
                }
            }

            return;
        }

        if (playerLeftLowerLeg != null && playerRightLowerLeg != null)
        {
            // 敌人位置。
Vector3 enemyPosition;
            if (enemyHead != null)
            {
                enemyPosition = enemyHead.position;
            }
            else
            {
                enemyPosition = sessionContext.enemyRoot.position;
            }

            if (Vector3.SqrMagnitude(playerLeftLowerLeg.position - enemyPosition) <= Vector3.SqrMagnitude(playerRightLowerLeg.position - enemyPosition))
            {
                selectedLowerLeg = playerLeftLowerLeg;
            }
            else
            {
                selectedLowerLeg = playerRightLowerLeg;
            }
        }
        else
        {
            // 空值判断前缓存的玩家左侧小腿。
global::UnityEngine.Transform availableValue22 = playerLeftLowerLeg;
            if (!global::System.Object.ReferenceEquals(availableValue22, null))
            {
                selectedLowerLeg = availableValue22;
            }
            else
            {
                selectedLowerLeg = playerRightLowerLeg;
            }
        }
    }

    /// <summary>GetPreferredPath 的业务处理入口；本方法直接依赖 QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()；下游（含内部回调体） QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rig">绑定。</param>
    /// <param name="profile">配置档。</param>
    private SplineContainer GetPreferredPath(QTECameraRig rig, QTECameraProfile profile)
    {
        if ((sessionContext.kind == QTEKind.RearGrab || sessionContext.kind == QTEKind.FrontGrab) && cameraController != null && cameraController.MainCamera != null)
        {
            // 侧边。
float side = Vector3.Dot(cameraController.MainCamera.transform.position - sessionContext.playerRoot.position, rig.pairRoot.right);
            if (side >= 0f)
            {
                return rig.fallbackPath;
            }
            else
            {
                return rig.primaryPath;
            }
        }

        if (profile.focusMode != QTECameraFocusMode.LegBiteInteractionCenter && profile.focusMode != QTECameraFocusMode.HighLegBiteInteractionCenter)
        {
            return rig.primaryPath;
        }

        SelectLowerLegIfNeeded(profile);
        if (selectedLowerLeg != null)
        {
            // 侧边。
float side = Vector3.Dot(selectedLowerLeg.position - sessionContext.playerRoot.position, rig.pairRoot.right);
            if (side >= 0f)
            {
                biteLegSideSign = 1f;
            }
            else
            {
                biteLegSideSign = -1f;
            }
        }

        if (biteLegSideSign >= 0f)
        {
            return rig.fallbackPath;
        }
        else
        {
            return rig.primaryPath;
        }
    }

    /// <summary>UpdateCameraFeedback 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.PerlinNoise(float, float)、QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float)、QTECameraRig.ResetCameraFeedback()、QTECameraRig.SetCameraFeedback(Vector3, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.LateUpdate()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.PerlinNoise(float, float)、QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float)、QTECameraRig.ResetCameraFeedback()、QTECameraRig.SetCameraFeedback(Vector3, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateCameraFeedback()
    {
        if (activePreset == null || !cameraTaken)
        {
            return;
        }

        // 配置档。
QTECameraProfile profile = activePreset.profile;
        // 绑定。
QTECameraRig rig = activePreset.rig;
        if (!profile.enableCameraFeedback)
        {
            rig.ResetCameraFeedback();
            return;
        }

        // 偏移量。
Vector3 offset = Vector3.zero;
        // 镜头侧倾。
float dutch = 0f;
        // 当前时间。
float now = Time.time;
        // 反馈侧边符号。
float feedbackSideSign;
        if (sessionContext.kind == QTEKind.RearGrab || sessionContext.kind == QTEKind.FrontGrab)
        {
            feedbackSideSign = orbitSideSign;
        }
        else
        {
            feedbackSideSign = biteLegSideSign;
        }

        if (motion == CameraMotion.Holding)
        {
            // 噪声淡入淡出。
float noiseFade;
            if (feedbackFadeStartTime < 0f)
            {
                noiseFade = 1f;
            }
            else
            {
                noiseFade = Mathf.Clamp01(1f - (now - feedbackFadeStartTime) / profile.feedbackRecoveryDuration);
            }

            // 噪声时间。
float noiseTime = now * profile.holdNoiseFrequency;
            // 水平。
float horizontal = Mathf.PerlinNoise(activeSessionId * 0.31f, noiseTime) - 0.5f;
            // 竖直。
float vertical = Mathf.PerlinNoise(activeSessionId * 0.73f, noiseTime + 17.3f) - 0.5f;
            offset += new Vector3(horizontal * profile.holdNoisePosition * 2f, vertical * profile.holdNoisePosition * 2f, 0f) * noiseFade;
            dutch += feedbackSideSign * horizontal * profile.holdNoiseDutch * 2f * noiseFade;
        }

        AddImpactFeedback(ref offset, ref dutch, grabImpactStartTime, profile.grabImpactDuration, new Vector3(feedbackSideSign * profile.grabImpactSideOffset, -profile.grabImpactVerticalOffset, 0f), feedbackSideSign * profile.grabImpactDutch, now);
        AddImpactFeedback(ref offset, ref dutch, biteImpactStartTime, profile.biteImpactDuration, new Vector3(orbitSideSign * profile.biteImpactSideOffset, -profile.biteImpactVerticalOffset, 0f), orbitSideSign * profile.biteImpactDutch, now);
        AddImpactFeedback(ref offset, ref dutch, kickImpactStartTime, profile.kickImpactDuration, new Vector3(feedbackSideSign * profile.kickImpactSideOffset, 0.01f, 0.015f), feedbackSideSign * profile.kickImpactDutch, now);
        rig.SetCameraFeedback(offset, dutch);
    }

    /// <summary>AddImpactFeedback 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateCameraFeedback()；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="offset">偏移量。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="dutch">镜头侧倾。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="startTime">起始时间。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="peakOffset">峰值偏移量。</param>
    /// <param name="peakDutch">峰值镜头侧倾。</param>
    /// <param name="now">当前时间。</param>
    private static void AddImpactFeedback(ref Vector3 offset, ref float dutch, float startTime, float duration, Vector3 peakOffset, float peakDutch, float now)
    {
        if (startTime < 0f || now < startTime)
        {
            return;
        }

        // 归一化。
float normalized = (now - startTime) / Mathf.Max(0.01f, duration);
        if (normalized >= 1f)
        {
            return;
        }

        // 包络。
float envelope = 1f - normalized;
        offset += peakOffset * envelope;
        dutch += peakDutch * envelope;
    }

    /// <summary>ScorePath 的业务处理入口；本方法直接依赖 Mathf.Max(int, int)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、QTECameraPresenter.IsParticipantCollider(Collider)、SplineContainer.EvaluatePosition(float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()；下游（含内部回调体） Mathf.Max(int, int)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、QTECameraPresenter.IsParticipantCollider(Collider)、SplineContainer.EvaluatePosition(float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    /// <param name="profile">配置档。</param>
    private int ScorePath(SplineContainer path, QTECameraProfile profile)
    {
        if (path == null)
        {
            return int.MaxValue;
        }

        // 分值。
int score = 0;
        // 采样集合。
int samples = Mathf.Max(4, profile.pathProbeSamples);
        // 当前遍历索引。
for (int i = 0; i < samples; i++)
        {
            // 镜头路径上的归一化位置。
float t;
            if (samples <= 1)
            {
                t = 0f;
            }
            else
            {
                t = (float)i / (samples - 1);
            }

            // 采样。
float3 sample = path.EvaluatePosition(t);
            // 摄像机位置。
Vector3 cameraPosition = new Vector3(sample.x, sample.y, sample.z);
            // 重叠数量。
int overlapCount = Physics.OverlapSphereNonAlloc(cameraPosition, profile.pathProbeRadius, overlapBuffer, profile.environmentMask, QueryTriggerInteraction.Ignore);
            // 命中索引。
for (int hitIndex = 0; hitIndex < overlapCount; hitIndex++)
            {
                // 命中。
Collider hit = overlapBuffer[hitIndex];
                if (!IsParticipantCollider(hit))
                {
                    score += 3;
                }
            }

            // 目标焦点。
Vector3 toFocus = activePreset.rig.focusTarget.position - cameraPosition;
            // 距离。
float distance = toFocus.magnitude;
            if (distance <= 0.001f)
            {
                continue;
            }

            // 命中数量。
int hitCount = Physics.SphereCastNonAlloc(cameraPosition, profile.pathProbeRadius * 0.5f, toFocus / distance, raycastBuffer, distance, profile.environmentMask, QueryTriggerInteraction.Ignore);
            // 命中索引。
for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                if (!IsParticipantCollider(raycastBuffer[hitIndex].collider))
                {
                    score++;
                }
            }
        }

        return score;
    }

    /// <summary>IsParticipantCollider 的业务处理入口；本方法直接依赖 Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.ScorePath(SplineContainer, QTECameraProfile)；下游（含内部回调体） Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">候选。</param>
    private bool IsParticipantCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        // 候选变换。
Transform candidateTransform = candidate.transform;
        return sessionContext.playerRoot != null && candidateTransform.IsChildOf(sessionContext.playerRoot) || sessionContext.enemyRoot != null && candidateTransform.IsChildOf(sessionContext.enemyRoot);
    }

    /// <summary>ReleaseCamera 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、QTECameraRig.ResetCameraFeedback()、RE4CameraController.ReleaseExternalCamera(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()、QTECameraPresenter.HandlePhaseChanged(int, QTEPhase)、QTECameraPresenter.HandleSessionEnded(QTESessionEndInfo)、QTECameraPresenter.HandleSessionStarted(int, QTEKind)、QTECameraPresenter.LateUpdate() 等共 7 处；下游（含内部回调体） GameObject.SetActive(bool)、QTECameraRig.ResetCameraFeedback()、RE4CameraController.ReleaseExternalCamera(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ReleaseCamera()
    {
        PreserveBlendContextForFrame();
        ResetNativeCameraSession();
        assassinationEntryTime = -1f;
        assassinationContactTime = -1f;
        if (cameraTaken && cameraController != null)
        {
            cameraController.ReleaseExternalCamera(this, true);
        }

        cameraTaken = false;
        motion = CameraMotion.None;
        activeSessionId = 0;
        sessionContext = default;
        grabScreenSide = 0;
        focusVelocity = Vector3.zero;
        playerUpperChest = null;
        playerHips = null;
        playerNeck = null;
        playerHead = null;
        playerLeftUpperLeg = null;
        playerRightUpperLeg = null;
        playerLeftLowerLeg = null;
        playerRightLowerLeg = null;
        selectedLowerLeg = null;
        enemyChest = null;
        enemyHead = null;
        grabImpactStartTime = -1f;
        biteImpactStartTime = -1f;
        kickImpactStartTime = -1f;
        feedbackFadeStartTime = -1f;
        biteLegSideSign = -1f;
        orbitSideSign = -1f;
        holdSplinePosition = 1f;
        failureSettleStartSplinePosition = 0f;
        frontGrabFailureStartSplinePosition = 0f;
        frontGrabFailurePressureSplinePosition = 0f;
        frontGrabFailurePreContactSplinePosition = 0f;
        frontGrabFailureTargetSplinePosition = 0f;
        entryLookPoint = Vector3.zero;
        failureOrbitBlocked = false;
        frontGrabPathBlocked = false;
        frontGrabContactReceived = false;
        if (activePreset != null && activePreset.rig != null)
        {
            activePreset.rig.resultOffsetRoot.localPosition = Vector3.zero;
            activePreset.rig.resultOffsetRoot.localRotation = Quaternion.identity;
            activePreset.rig.ResetCameraFeedback();
            activePreset.rig.qteCamera.Priority = 0;
            activePreset.rig.qteCamera.enabled = false;
            activePreset.rig.gameObject.SetActive(false);
        }

        activePreset = null;
    }

    /// <summary>GetHumanBone 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.CacheFocusBones()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    private static Transform GetHumanBone(Animator animator, HumanBodyBones bone)
    {
        return AnimatorBoneBindings.Resolve(animator, bone);
    }

    /// <summary>GetBonePosition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateAssassinationFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateFocusTarget(bool)、QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)、QTECameraPresenter.UpdateLegBiteFocusTarget(QTECameraProfile, bool) 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preferred">优先。</param>
    /// <param name="fallback">后备。</param>
    /// <param name="root">根变换节点。</param>
    /// <param name="fallbackHeight">后备高度。</param>
    private static Vector3 GetBonePosition(Transform preferred, Transform fallback, Transform root, float fallbackHeight)
    {
        if (preferred != null)
        {
            return preferred.position;
        }

        if (fallback != null)
        {
            return fallback.position;
        }

        if (root != null)
        {
            return root.position + Vector3.up * fallbackHeight;
        }
        else
        {
            return Vector3.up * fallbackHeight;
        }
    }
}
