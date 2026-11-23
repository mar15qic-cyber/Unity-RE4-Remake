using System;
using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

/// <summary>
/// 里昂手指最终反向动力学控制器。
/// 手臂由现有的 LimbIK 控制，本组件只负责手指链、武器目标点和动作阶段权重。
/// </summary>
[DefaultExecutionOrder(9999)]
public class LeonFingerIKController : MonoBehaviour
{
    /// <summary>玩家控制、动作与运动使用的 FingerSlot 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum FingerSlot
    {
        /// <summary>中文含义：右侧索引。</summary>
        RightIndex,
        /// <summary>中文含义：右侧中间。</summary>
        RightMiddle,
        /// <summary>中文含义：右侧环。</summary>
        RightRing,
        /// <summary>中文含义：右侧小指。</summary>
        RightPinky,
        /// <summary>中文含义：右侧拇指。</summary>
        RightThumb,
        /// <summary>中文含义：左侧索引。</summary>
        LeftIndex,
        /// <summary>中文含义：左侧中间。</summary>
        LeftMiddle,
        /// <summary>中文含义：左侧环。</summary>
        LeftRing,
        /// <summary>中文含义：左侧小指。</summary>
        LeftPinky,
        /// <summary>中文含义：左侧拇指。</summary>
        LeftThumb
    }

    /// <summary>玩家控制、动作与运动模块 FingerBinding；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class FingerBinding
    {
        /// <summary>中文含义：槽位。使用位置：作为 Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.this[LeonFingerIKController.FingerSlot] 的参数、作为 Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.TryGetValue(LeonFingerIKController.FingerSlot, out Transform) 的参数、作为 LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot) 的参数 等共 5 处。</summary>
        public FingerSlot slot;
        /// <summary>中文含义：骨骼1。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数。</summary>
        public Transform bone1;
        /// <summary>中文含义：骨骼2。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数。</summary>
        public Transform bone2;
        /// <summary>中文含义：骨骼3。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数。</summary>
        public Transform bone3;
        /// <summary>中文含义：尖端。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数、作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数。</summary>
        public Transform tip;
        /// <summary>中文含义：是否左侧。使用位置：作为 LeonFingerIKController.GetFingerWeight(WeaponType, bool, float) 的参数、作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。</summary>
        public bool isLeft;
        /// <summary>中文含义：目标变换节点。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。</summary>
        public Transform target;
        /// <summary>中文含义：求解器索引。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。</summary>
        public int solverIndex = -1;
    }

    /// <summary>玩家控制、动作与运动模块 FingerTargetSet；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class FingerTargetSet
    {
        /// <summary>中文含义：当前武器类别。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。</summary>
        public WeaponType weapon;
        /// <summary>中文含义：武器根节点。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。</summary>
        public Transform weaponRoot;
        /// <summary>中文含义：目标根节点。使用位置：作为 LeonFingerIKController.DestroySafe(GameObject) 的参数、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 等共 4 处。</summary>
        public Transform targetRoot;
        /// <summary>中文含义：目标列表。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.Log(object, Object) 的参数、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform) 等共 5 处。</summary>
        public readonly Dictionary<FingerSlot, Transform> targets = new();
    }

    /// <summary>中文含义：手指绑定。使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)、LeonFingerIKController.ConfigureFingerRig() 等共 7 处。</summary>
    [Header("最终反向动力学组件")]
    [SerializeField]
    private FingerRig fingerRig;
    /// <summary>中文含义：权重过渡速度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("权重过渡")]
    [SerializeField, Range(0f, 30f)]
    private float weightTransitionSpeed = 12f;
    /// <summary>中文含义：权重过渡时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [SerializeField, Range(0.05f, 0.5f)]
    private float weightTransitionDuration = 0.16f;
    /// <summary>中文含义：稳定权重。使用位置：作为 Mathf.Max(float, float) 的参数、LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    [SerializeField, Range(0f, 1f)]
    private float stableWeight = 0.9f;
    /// <summary>中文含义：换弹权重。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    [SerializeField, Range(0f, 1f)]
    private float reloadWeight = 0.35f;
    /// <summary>中文含义：过渡权重。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    [SerializeField, Range(0f, 1f)]
    private float transitionWeight = 0.2f;
    /// <summary>中文含义：步枪左侧手指权重。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</summary>
    [Header("长枪左手")]
    [SerializeField, Range(0f, 1f)]
    private float rifleLeftFingerWeight = 0.98f;
    /// <summary>中文含义：手枪左手手指权重。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</summary>
    [Header("手枪左手包握")]
    [Range(0f, 1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("手枪左手手指权重")]
    [UnityEngine.InspectorName("手枪左手手指权重")]
    public float pistolLeftHandFingerWeight = 0.98f;
    /// <summary>中文含义：手枪左手末节旋转权重。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。</summary>
    [Range(0f, 1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("手枪左手末节旋转权重")]
    [UnityEngine.InspectorName("手枪左手末节旋转权重")]
    public float pistolLeftHandEndSectionRotationWeight = 0.15f;
    /// <summary>中文含义：手枪左手拇指旋转权重。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。</summary>
    [Range(0f, 1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("手枪左手拇指旋转权重")]
    [UnityEngine.InspectorName("手枪左手拇指旋转权重")]
    public float pistolLeftHandThumbRotationWeight = 0.7f;
    /// <summary>中文含义：是否捕获缺失的武器目标列表。使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。</summary>
    [Header("目标点配置")]
    [SerializeField]
    private bool captureMissingWeaponTargets = true;
    /// <summary>中文含义：是否显示生成的目标列表。使用位置：作为 GameObject.SetActive(bool) 的参数。</summary>
    [SerializeField]
    private bool showGeneratedTargets = false;
    /// <summary>中文含义：序列化的目标武器。使用位置：作为 LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType) 的参数、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()。</summary>
    [Header("序列化目标")]
    [SerializeField]
    private WeaponType serializedTargetWeapon = WeaponType.Pistol;
    /// <summary>中文含义：绑定集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.Log(object, Object) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)、LeonFingerIKController.ApplyFingerWeights(WeaponType, float) 等共 12 处。</summary>
    private readonly List<FingerBinding> bindings = new();
    /// <summary>中文含义：目标集合列表。只读字段，初始化后不由普通写入口替换。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)、LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets() 等共 8 处。</summary>
    private readonly Dictionary<WeaponType, FingerTargetSet> targetSets = new();
    /// <summary>中文含义：缺失的武器根节点集合。只读字段，初始化后不由普通写入口替换。使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。</summary>
    private readonly HashSet<WeaponType> missingWeaponRoots = new();
    /// <summary>中文含义：动画控制器。使用位置：LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets() 等共 8 处。</summary>
    private Animator animator;
    /// <summary>中文含义：里昂玩家控制器。使用位置：LeonFingerIKController.GetCurrentWeapon()、LeonFingerIKController.Start()。</summary>
    private LeonController controller;
    /// <summary>中文含义：战斗。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonFingerIKController.LateUpdate()、LeonFingerIKController.Start()。</summary>
    private LeonCombat combat;
    /// <summary>中文含义：当前里昂动画绑定组件。使用位置：LeonFingerIKController.CanCaptureRuntimeTargets()、LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonFingerIKController.GetCurrentWeapon() 等共 5 处。</summary>
    private LeonAnimRig animRig;
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：LeonFingerIKController.CanCaptureRuntimeTargets()、LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonFingerIKController.Start()。</summary>
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.FindWeaponRoot(WeaponType)、LeonFingerIKController.Start()。</summary>
    private LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：当前权重。使用位置：作为 LeonFingerIKController.ApplyFingerWeights(WeaponType, float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数 等共 6 处。</summary>
    private float currentWeight;
    /// <summary>中文含义：权重速度。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonFingerIKController.LateUpdate()、LeonFingerIKController.TryInitialize()。</summary>
    private float weightVelocity;
    /// <summary>中文含义：上一次武器。使用位置：LeonFingerIKController.LateUpdate()。</summary>
    private WeaponType lastWeapon = (WeaponType)(-1);
    /// <summary>中文含义：是否已初始化。使用位置：LeonFingerIKController.IsInitialized.get、LeonFingerIKController.LateUpdate()、LeonFingerIKController.TryInitialize()。</summary>
    private bool initialized;
    /// <summary>中文含义：当前权重。</summary>
    public float CurrentWeight => currentWeight;
    /// <summary>中文含义：是否已初始化。</summary>
    public bool IsInitialized => initialized;
    /// <summary>中文含义：已配置手指数量。</summary>
    public int ConfiguredFingerCount => bindings.Count;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonFingerIKController.DisableLegacyFingerOverride() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<LeonController>();
        combat = GetComponent<LeonCombat>();
        animRig = GetComponent<LeonAnimRig>();
        coordinator = GetComponent<LeonAnimationActionCoordinator>();
        weaponVisualController = GetComponent<LeonWeaponVisualController>();
        DisableLegacyFingerOverride();
        TryInitialize();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType)、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)、LeonFingerIKController.CanCaptureRuntimeTargets()、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)、LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonFingerIKController.GetCurrentWeapon()、LeonFingerIKController.TryInitialize() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        bool ladderOwnsFingers = coordinator != null && coordinator.IsLadderActionActive;
        bool knifeOwnsFingers = combat != null && combat.knifeModule != null && combat.knifeModule.Active;
        if (ladderOwnsFingers || knifeOwnsFingers || controller != null && controller.IsHeldByTrap)
        {
            if (fingerRig != null)
            {
                fingerRig.weight = 0;
            }

            currentWeight = 0;
            weightVelocity = 0;
            return;
        }

        if (!initialized && !TryInitialize())
        {
            return;
        }

        if (fingerRig == null)
        {
            return;
        }

        // weapon（当前武器类别）：保存 LeonFingerIKController.GetCurrentWeapon() 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<WeaponType, LeonFingerIKController.FingerTargetSet>.ContainsKey(WeaponType) 的参数、作为 LeonFingerIKController.ApplyFingerWeights(WeaponType, float) 的参数、作为 LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 的参数 等共 5 处。
WeaponType weapon = GetCurrentWeapon();
        // allowRuntimeCapture（是否允许运行时捕获）：保存 LeonFingerIKController.CanCaptureRuntimeTargets() 的返回结果，供当前步骤使用。使用位置：作为 LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 的参数。
bool allowRuntimeCapture = CanCaptureRuntimeTargets();
        if (weapon != lastWeapon)
        {
            lastWeapon = weapon;
            EnsureWeaponTargetSet(weapon, allowRuntimeCapture);
            weightVelocity = 0f;
        }

        if (!targetSets.ContainsKey(weapon))
        {
            EnsureWeaponTargetSet(weapon, allowRuntimeCapture);
        }

        // targetWeight（目标权重）：保存 LeonFingerIKController.EvaluateTargetWeight(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonFingerIKController.LateUpdate()。
float targetWeight = EvaluateTargetWeight(weapon);
        currentWeight = Mathf.SmoothDamp(currentWeight, targetWeight, ref weightVelocity, Mathf.Max(0.01f, weightTransitionDuration), Mathf.Max(0.01f, weightTransitionSpeed), Time.deltaTime);
        if (Mathf.Abs(currentWeight - targetWeight) < 0.001f)
        {
            currentWeight = targetWeight;
            weightVelocity = 0f;
        }

        // 总权重只在逐指权重中应用一次，避免平方后在高权重区突然锁死。
        fingerRig.weight = 1f;
        ApplyFingerWeights(weapon, currentWeight);
    }

    /// <summary>TryInitialize 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Debug.Log(object, Object)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.LateUpdate()、LeonFingerIKController.Start()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Debug.Log(object, Object)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool TryInitialize()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        EnsureFingerRig();
        ConfigureFingerRig();
        if (fingerRig == null || bindings.Count == 0)
        {
            return false;
        }

        fingerRig.weight = 0f;
        currentWeight = 0f;
        weightVelocity = 0f;
        initialized = true;
        Debug.Log($"[里昂手指反向动力学] 已配置 {bindings.Count} 根手指，目标将在对应武器首次使用时建立。", this);
        return true;
    }

    /// <summary>RebuildCurrentWeaponTargets 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Remove(WeaponType)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.DestroySafe(GameObject)、LeonFingerIKController.EnsureFingerRig() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKControllerEditor.OnInspectorGUI()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Remove(WeaponType)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.DestroySafe(GameObject)、LeonFingerIKController.EnsureFingerRig() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("重新捕获当前武器手指目标")]
    public void RebuildCurrentWeaponTargets()
    {
        // weapon（当前武器类别）：保存 LeonFingerIKController.GetCurrentWeapon() 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<WeaponType, LeonFingerIKController.FingerTargetSet>.Remove(WeaponType) 的参数、作为 Dictionary<WeaponType, LeonFingerIKController.FingerTargetSet>.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet) 的参数、作为 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType) 的参数 等共 5 处。
WeaponType weapon = GetCurrentWeapon();
        if (!Application.isPlaying)
        {
            animator = GetComponent<Animator>();
            EnsureFingerRig();
            if (bindings.Count == 0)
            {
                ConfigureFingerRig();
            }

            CapturePersistentWeaponTargets(weapon);
            targetSets.Remove(weapon);
            EnsureWeaponTargetSet(weapon);
            WriteWeaponTargetsToFingerRig(weapon);
            return;
        }

        if (targetSets.TryGetValue(weapon, out FingerTargetSet /* 中文含义：旧集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ oldSet))
        {
            if (oldSet.targetRoot != null)
            {
                DestroySafe(oldSet.targetRoot.gameObject);
            }

            targetSets.Remove(weapon);
        }

        EnsureWeaponTargetSet(weapon);
    }

    /// <summary>CreatePersistentWeaponTargets 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Debug.Log(object, Object)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Clear()、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKControllerEditor.OnInspectorGUI()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Debug.Log(object, Object)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Clear()、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("创建并保存全部武器手指目标")]
    public void CreatePersistentWeaponTargets()
    {
        if (Application.isPlaying)
        {
            return;
        }

        animator = GetComponent<Animator>();
        EnsureFingerRig();
        ConfigureFingerRig();
        CapturePersistentWeaponTargets(WeaponType.Pistol);
        CapturePersistentWeaponTargets(WeaponType.Rifle);
        CapturePersistentWeaponTargets(WeaponType.Shotgun);
        CapturePersistentWeaponTargets(WeaponType.Knife);
        targetSets.Clear();
        WriteWeaponTargetsToFingerRig(serializedTargetWeapon);
        Debug.Log("[里昂手指反向动力学] 已创建可保存的手枪、步枪和刀具逐指目标，请保存当前场景或预制件。", this);
    }

    /// <summary>CreatePersistentTargetsForWeapon 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Remove(WeaponType)、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.RebuildKnifeIK()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Remove(WeaponType)、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Remove(WeaponType) 的参数、作为 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType) 的参数、作为 LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 的参数 等共 4 处。</param>
    public bool CreatePersistentTargetsForWeapon(WeaponType weapon)
    {
        if (Application.isPlaying)
        {
            return false;
        }

        animator = GetComponent<Animator>();
        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        EnsureFingerRig();
        ConfigureFingerRig();
        CapturePersistentWeaponTargets(weapon);
        targetSets.Remove(weapon);
        EnsureWeaponTargetSet(weapon, false);
        return WriteWeaponTargetsToFingerRig(weapon);
    }

    /// <summary>
    /// 将编辑器当前选择的武器目标写入 FingerRig 的可序列化 target 字段。
    /// 播放模式下改为写入当前实际武器，保证切换武器时仍然能够更新目标。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonFingerIKControllerEditor.OnInspectorGUI()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、LeonFingerIKController.ConfigureFingerRig()、LeonFingerIKController.EnsureFingerRig()、LeonFingerIKController.GetCurrentWeapon()、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool WriteCurrentWeaponTargetsToFingerRig()
    {
        if (!Application.isPlaying)
        {
            animator = GetComponent<Animator>();
            EnsureFingerRig();
            if (bindings.Count == 0)
            {
                ConfigureFingerRig();
            }
        }

        // weapon（当前武器类别）：使用位置：作为 LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType) 的参数、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()。
WeaponType weapon;
        if (Application.isPlaying)
        {
            weapon = GetCurrentWeapon();
        }
        else
        {
            weapon = serializedTargetWeapon;
        }

        return WriteWeaponTargetsToFingerRig(weapon);
    }

    /// <summary>DisableLegacyFingerOverride 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;FingerPoseOverride&gt;()、Debug.Log(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.Start()；下游（含内部回调体） Component.GetComponent&lt;FingerPoseOverride&gt;()、Debug.Log(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DisableLegacyFingerOverride()
    {
        // legacy（旧版）：保存 Component.GetComponent<FingerPoseOverride>() 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.DisableLegacyFingerOverride()。
FingerPoseOverride legacy = GetComponent<FingerPoseOverride>();
        if (legacy != null)
        {
            legacy.enabled = false;
            Debug.Log("[里昂手指反向动力学] 已停用旧版手指骨骼覆盖脚本。", this);
        }
    }

    /// <summary>EnsureFingerRig 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;FingerRig&gt;()、GameObject.AddComponent&lt;FingerRig&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets()、LeonFingerIKController.RebuildCurrentWeaponTargets()、LeonFingerIKController.TryInitialize()、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()；下游（含内部回调体） Component.GetComponent&lt;FingerRig&gt;()、GameObject.AddComponent&lt;FingerRig&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureFingerRig()
    {
        if (fingerRig == null)
        {
            fingerRig = GetComponent<FingerRig>();
        }

        if (fingerRig == null)
        {
            fingerRig = gameObject.AddComponent<FingerRig>();
        }
    }

    /// <summary>ConfigureFingerRig 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Array.Empty&lt;Finger&gt;()、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)、List&lt;LeonFingerIKController.FingerBinding&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets()、LeonFingerIKController.RebuildCurrentWeaponTargets()、LeonFingerIKController.TryInitialize()、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Array.Empty&lt;Finger&gt;()、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)、List&lt;LeonFingerIKController.FingerBinding&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ConfigureFingerRig()
    {
        bindings.Clear();
        fingerRig.fingers = Array.Empty<Finger>();
        // rightHand（右手）：使用位置：作为 LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool) 的参数、LeonFingerIKController.ConfigureFingerRig()。
Transform rightHand;
        if (animator != null)
        {
            rightHand = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightHand);
        }
        else
        {
            rightHand = null;
        }

        // leftHand（左手）：使用位置：作为 LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool) 的参数、LeonFingerIKController.ConfigureFingerRig()。
Transform leftHand;
        if (animator != null)
        {
            leftHand = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftHand);
        }
        else
        {
            leftHand = null;
        }

        AddFingerBinding(FingerSlot.RightIndex, rightHand, false, "arm right finger index a", "arm right finger index a/arm right finger index b", "arm right finger index a/arm right finger index b/arm right finger index c", "arm right finger index a/arm right finger index b/arm right finger index c_end", true);
        AddFingerBinding(FingerSlot.RightMiddle, rightHand, false, "arm right finger middle a", "arm right finger middle a/arm right finger middle b", "arm right finger middle a/arm right finger middle b/arm right finger middle c", "arm right finger middle a/arm right finger middle b/arm right finger middle c_end", true);
        AddFingerBinding(FingerSlot.RightRing, rightHand, false, "arm right palm/arm right finger ring a", "arm right palm/arm right finger ring a/arm right finger ring b", "arm right palm/arm right finger ring a/arm right finger ring b/arm right finger ring c", "arm right palm/arm right finger ring a/arm right finger ring b/arm right finger ring c_end", true);
        AddFingerBinding(FingerSlot.RightPinky, rightHand, false, "arm right palm/arm right finger pinky a", "arm right palm/arm right finger pinky a/arm right finger pinky b", "arm right palm/arm right finger pinky a/arm right finger pinky b/arm right finger pinky c", "arm right palm/arm right finger pinky a/arm right finger pinky b/arm right finger pinky c_end", true);
        AddFingerBinding(FingerSlot.RightThumb, rightHand, false, "arm right finger thumb a", "arm right finger thumb a/arm right finger thumb b", "arm right finger thumb a/arm right finger thumb b/arm right finger thumb c", "arm right finger thumb a/arm right finger thumb b/arm right finger thumb c_end", false);
        AddFingerBinding(FingerSlot.LeftIndex, leftHand, true, "arm left finger index a", "arm left finger index a/arm left finger index b", "arm left finger index a/arm left finger index b/arm left finger index c", "arm left finger index a/arm left finger index b/arm left finger index c_end", true);
        AddFingerBinding(FingerSlot.LeftMiddle, leftHand, true, "arm left finger middle a", "arm left finger middle a/arm left finger middle b", "arm left finger middle a/arm left finger middle b/arm left finger middle c", "arm left finger middle a/arm left finger middle b/arm left finger middle c_end", true);
        AddFingerBinding(FingerSlot.LeftRing, leftHand, true, "arm left palm/arm left finger ring a", "arm left palm/arm left finger ring a/arm left finger ring b", "arm left palm/arm left finger ring a/arm left finger ring b/arm left finger ring c", "arm left palm/arm left finger ring a/arm left finger ring b/arm left finger ring c_end", true);
        AddFingerBinding(FingerSlot.LeftPinky, leftHand, true, "arm left palm/arm left finger pinky a", "arm left palm/arm left finger pinky a/arm left finger pinky b", "arm left palm/arm left finger pinky a/arm left finger pinky b/arm left finger pinky c", "arm left palm/arm left finger pinky a/arm left finger pinky b/arm left finger pinky c_end", true);
        AddFingerBinding(FingerSlot.LeftThumb, leftHand, true, "arm left finger thumb a", "arm left finger thumb a/arm left finger thumb b", "arm left finger thumb a/arm left finger thumb b/arm left finger thumb c", "arm left finger thumb a/arm left finger thumb b/arm left finger thumb c_end", false);
    }

    /// <summary>AddFingerBinding 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform)、LeonFingerIKController.FingerBinding.FingerBinding()、List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding)、Transform.Find(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.ConfigureFingerRig()；下游（含内部回调体） Debug.LogWarning(object, Object)、FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform)、LeonFingerIKController.FingerBinding.FingerBinding()、List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding)、Transform.Find(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slot">中文含义：槽位。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数。</param>
    /// <param name="hand">中文含义：手部。使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。</param>
    /// <param name="isLeft">中文含义：是否左侧。使用位置：作为 List&lt;LeonFingerIKController.FingerBinding&gt;.Add(LeonFingerIKController.FingerBinding) 的参数。</param>
    /// <param name="bone1Path">中文含义：骨骼1路径。使用位置：作为 Transform.Find(string) 的参数。</param>
    /// <param name="bone2Path">中文含义：骨骼2路径。使用位置：作为 Transform.Find(string) 的参数。</param>
    /// <param name="bone3Path">中文含义：骨骼3路径。使用位置：作为 Transform.Find(string) 的参数。</param>
    /// <param name="tipPath">中文含义：尖端路径。使用位置：作为 Transform.Find(string) 的参数。</param>
    /// <param name="useOneRotationDegree">中文含义：是否使用第一旋转角度。使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。</param>
    private void AddFingerBinding(FingerSlot slot, Transform hand, bool isLeft, string bone1Path, string bone2Path, string bone3Path, string tipPath, bool useOneRotationDegree)
    {
        if (hand == null)
        {
            return;
        }

        // bone1（骨骼1）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform) 的参数、作为 List<LeonFingerIKController.FingerBinding>.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
Transform bone1 = hand.Find(bone1Path);
        // bone2（骨骼2）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform) 的参数、作为 List<LeonFingerIKController.FingerBinding>.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
Transform bone2 = hand.Find(bone2Path);
        // bone3（骨骼3）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform) 的参数、作为 List<LeonFingerIKController.FingerBinding>.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
Transform bone3 = hand.Find(bone3Path);
        // tip（尖端）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 FingerRig.AddFinger(Transform, Transform, Transform, Transform, Transform) 的参数、作为 List<LeonFingerIKController.FingerBinding>.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
Transform tip = hand.Find(tipPath);
        if (tip == null && bone3 != null)
        {
            tip = bone3;
        }

        if (bone1 == null || bone2 == null || tip == null)
        {
            Debug.LogWarning($"[里昂手指反向动力学] 无法绑定手指：{slot}。", this);
            return;
        }

        fingerRig.AddFinger(bone1, bone2, bone3, tip);
        // solverIndex（求解器索引）：使用位置：作为 List<LeonFingerIKController.FingerBinding>.Add(LeonFingerIKController.FingerBinding) 的参数、LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
int solverIndex = fingerRig.fingers.Length - 1;
        // finger（手指）：使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
Finger finger = fingerRig.fingers[solverIndex];
        finger.weight = 0f;
        // assignmentReceiver4（赋值前保存的目标手指）：从手指读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
global::RootMotion.FinalIK.Finger assignmentReceiver4 = finger;
        if (useOneRotationDegree)
        {
            assignmentReceiver4.rotationWeight = 0.85f;
        }
        else
        {
            assignmentReceiver4.rotationWeight = 0.7f;
        }

        // assignmentReceiver6（赋值前保存的目标手指）：从手指读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonFingerIKController.AddFingerBinding(LeonFingerIKController.FingerSlot, Transform, bool, string, string, string, string, bool)。
global::RootMotion.FinalIK.Finger assignmentReceiver6 = finger;
        if (useOneRotationDegree)
        {
            assignmentReceiver6.rotationDOF = Finger.DOF.One;
        }
        else
        {
            assignmentReceiver6.rotationDOF = Finger.DOF.Three;
        }

        finger.fixBone1Twist = true;
        bindings.Add(new FingerBinding { slot = slot, bone1 = bone1, bone2 = bone2, bone3 = bone3, tip = tip, isLeft = isLeft, solverIndex = solverIndex });
    }

    /// <summary>EnsureWeaponTargetSet 的业务处理入口；本方法直接依赖 Debug.Log(object, Object)、Debug.LogWarning(object, Object)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Add(WeaponType, LeonFingerIKController.FingerTargetSet)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType)、GameObject.GameObject(string)、GameObject.SetActive(bool)、HashSet&lt;WeaponType&gt;.Add(WeaponType) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.LateUpdate()、LeonFingerIKController.RebuildCurrentWeaponTargets()、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)；下游（含内部回调体） Debug.Log(object, Object)、Debug.LogWarning(object, Object)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Add(WeaponType, LeonFingerIKController.FingerTargetSet)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType)、GameObject.GameObject(string)、GameObject.SetActive(bool)、HashSet&lt;WeaponType&gt;.Add(WeaponType) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 Debug.Log(object, Object) 的参数、作为 Debug.LogWarning(object, Object) 的参数、作为 Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.Add(WeaponType, LeonFingerIKController.FingerTargetSet) 的参数 等共 10 处。</param>
    /// <param name="allowRuntimeCapture">中文含义：是否允许运行时捕获。使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。</param>
    private void EnsureWeaponTargetSet(WeaponType weapon, bool allowRuntimeCapture = true)
    {
        if (targetSets.ContainsKey(weapon))
        {
            return;
        }

        // weaponRoot（武器根节点）：保存 LeonFingerIKController.FindWeaponRoot(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform) 的参数、作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 4 处。
Transform weaponRoot = FindWeaponRoot(weapon);
        if (weaponRoot == null)
        {
            if (missingWeaponRoots.Add(weapon))
            {
                Debug.LogWarning($"[里昂手指反向动力学] 找不到 {weapon} 的武器模型，暂不建立目标点。", this);
            }

            return;
        }

        // persistentTargetRoot（持久目标根节点）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform) 的参数、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
Transform persistentTargetRoot = FindDeepChild(weaponRoot, GetTargetRootName(weapon));
        if (persistentTargetRoot != null)
        {
            // persistentSet（持久集合）：保存 LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<WeaponType, LeonFingerIKController.FingerTargetSet>.Add(WeaponType, LeonFingerIKController.FingerTargetSet) 的参数、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
FingerTargetSet persistentSet = CreateTargetSetFromRoot(weapon, weaponRoot, persistentTargetRoot);
            if (persistentSet.targets.Count > 0)
            {
                targetSets.Add(weapon, persistentSet);
                return;
            }
        }

        if (!allowRuntimeCapture || !captureMissingWeaponTargets || !Application.isPlaying)
        {
            return;
        }

        // targetRootObject（目标根游戏对象）：使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
GameObject targetRootObject = new GameObject($"手指目标_{weapon}");
        // targetRoot（目标根节点）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
Transform targetRoot = targetRootObject.transform;
        targetRoot.SetParent(weaponRoot, false);
        targetRoot.localPosition = Vector3.zero;
        targetRoot.localRotation = Quaternion.identity;
        targetRoot.localScale = Vector3.one;
        targetRoot.gameObject.SetActive(showGeneratedTargets);
        // set（集合）：使用位置：作为 Debug.Log(object, Object) 的参数、作为 Dictionary<WeaponType, LeonFingerIKController.FingerTargetSet>.Add(WeaponType, LeonFingerIKController.FingerTargetSet) 的参数、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
FingerTargetSet set = new FingerTargetSet
        {
            weapon = weapon,
            weaponRoot = weaponRoot,
            targetRoot = targetRoot
        };
        // binding（绑定）：每轮从绑定集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<LeonFingerIKController.FingerSlot, Transform>.this[LeonFingerIKController.FingerSlot] 的参数、作为 LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot) 的参数、作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数 等共 4 处。
foreach (FingerBinding binding in bindings)
        {
            // targetObject（目标对象）：使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
GameObject targetObject = new GameObject(GetFingerTargetName(binding.slot));
            // target（目标变换节点）：使用位置：LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。
Transform target = targetObject.transform;
            target.SetParent(targetRoot, false);
            target.SetPositionAndRotation(binding.tip.position, binding.tip.rotation);
            set.targets[binding.slot] = target;
            binding.target = target;
        }

        targetSets.Add(weapon, set);
        Debug.Log($"[里昂手指反向动力学] 已为 {weapon} 建立 {set.targets.Count} 个武器跟随目标。", this);
    }

    /// <summary>CapturePersistentWeaponTargets 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、GameObject.SetActive(bool)、LeonFingerIKController.FindDeepChild(Transform, string)、LeonFingerIKController.FindWeaponRoot(WeaponType)、LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot)、LeonFingerIKController.GetTargetRootName(WeaponType)、Transform.SetParent(Transform, bool) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets()、LeonFingerIKController.RebuildCurrentWeaponTargets()；下游（含内部回调体） GameObject.GameObject(string)、GameObject.SetActive(bool)、LeonFingerIKController.FindDeepChild(Transform, string)、LeonFingerIKController.FindWeaponRoot(WeaponType)、LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot)、LeonFingerIKController.GetTargetRootName(WeaponType)、Transform.SetParent(Transform, bool) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonFingerIKController.FindWeaponRoot(WeaponType) 的参数、作为 LeonFingerIKController.GetTargetRootName(WeaponType) 的参数。</param>
    private void CapturePersistentWeaponTargets(WeaponType weapon)
    {
        // weaponRoot（武器根节点）：保存 LeonFingerIKController.FindWeaponRoot(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)。
Transform weaponRoot = FindWeaponRoot(weapon);
        if (weaponRoot == null)
        {
            return;
        }

        // targetRoot（目标根节点）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)。
Transform targetRoot = FindDeepChild(weaponRoot, GetTargetRootName(weapon));
        if (targetRoot == null)
        {
            // targetRootObject（目标根游戏对象）：使用位置：LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)。
GameObject targetRootObject = new GameObject(GetTargetRootName(weapon));
            targetRoot = targetRootObject.transform;
            targetRoot.SetParent(weaponRoot, false);
            targetRoot.localPosition = Vector3.zero;
            targetRoot.localRotation = Quaternion.identity;
            targetRoot.localScale = Vector3.one;
        }

        targetRoot.gameObject.SetActive(true);
        // binding（绑定）：每轮从绑定集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot) 的参数、作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数。
foreach (FingerBinding binding in bindings)
        {
            // target（目标变换节点）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)。
Transform target = FindDeepChild(targetRoot, GetFingerTargetName(binding.slot));
            if (target == null)
            {
                // targetObject（目标对象）：使用位置：LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)。
GameObject targetObject = new GameObject(GetFingerTargetName(binding.slot));
                target = targetObject.transform;
                target.SetParent(targetRoot, false);
            }

            target.SetPositionAndRotation(binding.tip.position, binding.tip.rotation);
        }
    }

    /// <summary>CreateTargetSetFromRoot 的业务处理入口；本方法直接依赖 LeonFingerIKController.FindDeepChild(Transform, string)、LeonFingerIKController.FingerTargetSet.FingerTargetSet()、LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)；下游（含内部回调体） LeonFingerIKController.FindDeepChild(Transform, string)、LeonFingerIKController.FingerTargetSet.FingerTargetSet()、LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。</param>
    /// <param name="weaponRoot">中文含义：武器根节点。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。</param>
    /// <param name="targetRoot">中文含义：目标根节点。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。</param>
    private FingerTargetSet CreateTargetSetFromRoot(WeaponType weapon, Transform weaponRoot, Transform targetRoot)
    {
        // set（集合）：使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。
FingerTargetSet set = new FingerTargetSet
        {
            weapon = weapon,
            weaponRoot = weaponRoot,
            targetRoot = targetRoot
        };
        // binding（绑定）：每轮从绑定集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<LeonFingerIKController.FingerSlot, Transform>.this[LeonFingerIKController.FingerSlot] 的参数、作为 LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot) 的参数、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。
foreach (FingerBinding binding in bindings)
        {
            // target（目标变换节点）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)。
Transform target = FindDeepChild(targetRoot, GetFingerTargetName(binding.slot));
            if (target == null)
            {
                continue;
            }

            set.targets[binding.slot] = target;
            binding.target = target;
        }

        return set;
    }

    /// <summary>WriteWeaponTargetsToFingerRig 的业务处理入口；本方法直接依赖 Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.TryGetValue(LeonFingerIKController.FingerSlot, out Transform)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CreatePersistentTargetsForWeapon(WeaponType)、LeonFingerIKController.CreatePersistentWeaponTargets()、LeonFingerIKController.RebuildCurrentWeaponTargets()、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()；下游（含内部回调体） Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.TryGetValue(LeonFingerIKController.FingerSlot, out Transform)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet) 的参数、作为 LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool) 的参数。</param>
    private bool WriteWeaponTargetsToFingerRig(WeaponType weapon)
    {
        if (fingerRig == null)
        {
            return false;
        }

        if (!targetSets.TryGetValue(weapon, out FingerTargetSet /* 中文含义：集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ set))
        {
            EnsureWeaponTargetSet(weapon);
            targetSets.TryGetValue(weapon, out set);
        }

        if (set == null)
        {
            return false;
        }

        // wroteAnyTarget（是否已写入任意目标）：使用位置：LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。
bool wroteAnyTarget = false;
        // binding（绑定）：每轮从绑定集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<LeonFingerIKController.FingerSlot, Transform>.TryGetValue(LeonFingerIKController.FingerSlot, out Transform) 的参数、LeonFingerIKController.WriteWeaponTargetsToFingerRig(WeaponType)。
foreach (FingerBinding binding in bindings)
        {
            if (binding.solverIndex < 0 || binding.solverIndex >= fingerRig.fingers.Length)
            {
                continue;
            }

            if (!set.targets.TryGetValue(binding.slot, out Transform /* 中文含义：目标变换节点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target))
            {
                continue;
            }

            fingerRig.fingers[binding.solverIndex].target = target;
            binding.target = target;
            wroteAnyTarget = true;
        }

        return wroteAnyTarget;
    }

    /// <summary>ApplyFingerWeights 的业务处理入口；本方法直接依赖 Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.TryGetValue(LeonFingerIKController.FingerSlot, out Transform)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.LateUpdate()；下游（含内部回调体） Dictionary&lt;LeonFingerIKController.FingerSlot, Transform&gt;.TryGetValue(LeonFingerIKController.FingerSlot, out Transform)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet)、LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.TryGetValue(WeaponType, out LeonFingerIKController.FingerTargetSet) 的参数、作为 LeonFingerIKController.GetFingerWeight(WeaponType, bool, float) 的参数、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。</param>
    /// <param name="masterWeight">中文含义：主控权重。使用位置：作为 LeonFingerIKController.GetFingerWeight(WeaponType, bool, float) 的参数。</param>
    private void ApplyFingerWeights(WeaponType weapon, float masterWeight)
    {
        if (fingerRig == null)
        {
            return;
        }

        targetSets.TryGetValue(weapon, out FingerTargetSet /* 中文含义：集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ set);
        // binding（绑定）：每轮从绑定集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<LeonFingerIKController.FingerSlot, Transform>.TryGetValue(LeonFingerIKController.FingerSlot, out Transform) 的参数、作为 LeonFingerIKController.GetFingerWeight(WeaponType, bool, float) 的参数、LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
foreach (FingerBinding binding in bindings)
        {
            if (binding.solverIndex < 0 || binding.solverIndex >= fingerRig.fingers.Length)
            {
                continue;
            }

            // finger（手指）：使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
Finger finger = fingerRig.fingers[binding.solverIndex];
            // supportFinger（是否支撑手指）：使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
bool supportFinger = binding.isLeft && (weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun);
            // thumb（是否拇指）：使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
bool thumb = binding.slot == FingerSlot.LeftThumb || binding.slot == FingerSlot.RightThumb;
            // 包握护木时允许指根修正侧偏，降低末节旋转对指链的扭转牵制。
            // assignmentReceiver8（赋值前保存的目标手指）：从手指读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
global::RootMotion.FinalIK.Finger assignmentReceiver8 = // 包握护木时允许指根修正侧偏，降低末节旋转对指链的扭转牵制。
 finger;
            if (supportFinger || thumb)
            {
                assignmentReceiver8.rotationDOF = Finger.DOF.Three;
            }
            else
            {
                assignmentReceiver8.rotationDOF = Finger.DOF.One;
            }

            // assignmentReceiver10（赋值前保存的目标手指）：从手指读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
global::RootMotion.FinalIK.Finger assignmentReceiver10 = finger;
            if (supportFinger)
            {
                assignmentReceiver10.rotationWeight = 0.4f;
            }
            else
            {
                if (thumb)
                {
                    assignmentReceiver10.rotationWeight = 0.7f;
                }
                else
                {
                    assignmentReceiver10.rotationWeight = 0.85f;
                }
            }

            if (weapon == WeaponType.Pistol && binding.isLeft)
            {
                // 副手包住主手与握把，允许指根侧偏，避免单轴约束把手指扭开。
                finger.rotationDOF = Finger.DOF.Three;
                // assignmentReceiver13（赋值前保存的目标手指）：从手指读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonFingerIKController.ApplyFingerWeights(WeaponType, float)。
global::RootMotion.FinalIK.Finger assignmentReceiver13 = finger;
                if (thumb)
                {
                    assignmentReceiver13.rotationWeight = pistolLeftHandThumbRotationWeight;
                }
                else
                {
                    assignmentReceiver13.rotationWeight = pistolLeftHandEndSectionRotationWeight;
                }
            }

            if (set != null && set.targets.TryGetValue(binding.slot, out Transform /* 中文含义：目标变换节点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target))
            {
                finger.target = target;
            }
            else
            {
                finger.target = null;
            }

            if (binding.isLeft && coordinator != null && coordinator.IsItemPickupActive)
            {
                finger.weight = 0f;
            }
            else
            {
                finger.weight = GetFingerWeight(weapon, binding.isLeft, masterWeight);
            }
        }
    }

    /// <summary>GetFingerWeight 的业务处理入口；本方法直接依赖 LeonFingerIKController.GetSideWeight(WeaponType, bool)、Mathf.Clamp01(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.ApplyFingerWeights(WeaponType, float)；下游（含内部回调体） LeonFingerIKController.GetSideWeight(WeaponType, bool)、Mathf.Clamp01(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonFingerIKController.GetSideWeight(WeaponType, bool) 的参数、LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</param>
    /// <param name="isLeft">中文含义：是否左侧。使用位置：作为 LeonFingerIKController.GetSideWeight(WeaponType, bool) 的参数、LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</param>
    /// <param name="masterWeight">中文含义：主控权重。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</param>
    private float GetFingerWeight(WeaponType weapon, bool isLeft, float masterWeight)
    {
        if (weapon == WeaponType.Pistol && isLeft)
        {
            // 左掌离开握把时同步释放指尖，换弹、受伤和全身动作不能拉扯手指。
            // leftArmWeight（左侧手臂权重）：使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。
float leftArmWeight;
            if (animRig != null)
            {
                leftArmWeight = animRig.PistolLeftWeight;
            }
            else
            {
                leftArmWeight = 0f;
            }

            // phaseWeightValue（阶段权重值）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。
float phaseWeightValue = Mathf.Clamp01(masterWeight / Mathf.Max(0.001f, stableWeight));
            return phaseWeightValue * pistolLeftHandFingerWeight * leftArmWeight;
        }

        // 先由手臂将左掌放到护木，再由末端解算器贴合逐指目标。
        // 同时读取手臂权重，换弹或全身动作卸除左掌约束时同步释放手指。
        if ((weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun) && isLeft)
        {
            // armWeight（手臂权重）：使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。
float armWeight;
            if (animRig == null)
            {
                armWeight = 0f;
            }
            else
            {
                if (weapon == WeaponType.Rifle)
                {
                    armWeight = animRig.RifleLeftWeight;
                }
                else
                {
                    armWeight = animRig.ShotgunLeftWeight;
                }
            }

            // phaseWeight（阶段权重）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。
float phaseWeight = Mathf.Clamp01(masterWeight / Mathf.Max(0.001f, stableWeight));
            return phaseWeight * rifleLeftFingerWeight * armWeight;
        }

        if (weapon == WeaponType.Rifle && animRig != null)
        {
            return masterWeight * animRig.RifleCompatibilityIkWeight;
        }
        if (weapon == WeaponType.Shotgun && animRig != null)
        {
            return masterWeight * animRig.ShotgunCompatibilityIkWeight;
        }
        if (weapon == WeaponType.Pistol && animRig != null)
        {
            return masterWeight * GetSideWeight(weapon, isLeft) * animRig.PistolCompatibilityIkWeight;
        }

        // sideWeight（侧边权重）：保存 LeonFingerIKController.GetSideWeight(WeaponType, bool) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。
float sideWeight = GetSideWeight(weapon, isLeft);
        return masterWeight * sideWeight;
    }

    /// <summary>EvaluateTargetWeight 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.LateUpdate()；下游（含内部回调体） Animator.GetLayerWeight(int)、Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 Dictionary&lt;WeaponType, LeonFingerIKController.FingerTargetSet&gt;.ContainsKey(WeaponType) 的参数。</param>
    private float EvaluateTargetWeight(WeaponType weapon)
    {
        if (!targetSets.ContainsKey(weapon))
        {
            return 0f;
        }

        if (animRig != null && (animRig.IsDead || animRig.IKPhase == WeaponIKPhase.Hit || animRig.IKPhase == WeaponIKPhase.QTE || animRig.IKPhase == WeaponIKPhase.Dead))
        {
            return 0f;
        }

        if (coordinator != null && coordinator.IsDead)
        {
            return 0f;
        }

        if (combat != null && combat.IsKnifeAttackActive)
        {
            return 0f;
        }

        bool pickupActive = coordinator != null && coordinator.IsItemPickupActive;
        if (coordinator != null && coordinator.HasActiveFullBodyAction && !pickupActive)
        {
            return 0f;
        }

        // 全身动作事务会在状态退出通知时结束，但覆盖层仍可能处于淡出。
        // 不能在这段时间重新施加手指约束，否则会与尚未淡出的挣脱姿态争夺骨骼。
        if (!pickupActive && animator != null && animator.GetLayerWeight(LeonAnimatorDriver.LayerFullBodyAction) > 0.001f)
        {
            return 0f;
        }

        if (coordinator != null && coordinator.HasActiveWeaponAction)
        {
            if (coordinator.CurrentPhase == WeaponTransactionPhase.PlayingReload || coordinator.CurrentPhase == WeaponTransactionPhase.AwaitingReloadCommit || coordinator.CurrentPhase == WeaponTransactionPhase.WaitingForReloadExit)
            {
                return reloadWeight;
            }

            return transitionWeight;
        }

        if (combat != null && combat.IsReloading)
        {
            return reloadWeight;
        }

        return stableWeight;
    }

    /// <summary>CanCaptureRuntimeTargets 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.LateUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanCaptureRuntimeTargets()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        if (coordinator != null && coordinator.HasActiveWeaponAction)
        {
            return false;
        }

        if (animRig != null && animRig.IKPhase != WeaponIKPhase.Stable)
        {
            return false;
        }

        return true;
    }

    /// <summary>GetSideWeight 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonFingerIKController.GetSideWeight(WeaponType, bool)。</param>
    /// <param name="isLeft">中文含义：是否左侧。使用位置：LeonFingerIKController.GetSideWeight(WeaponType, bool)。</param>
    private float GetSideWeight(WeaponType weapon, bool isLeft)
    {
        if (weapon == WeaponType.Knife && isLeft)
        {
            return 0f;
        }

        return 1f;
    }

    /// <summary>GetCurrentWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.LateUpdate()、LeonFingerIKController.RebuildCurrentWeaponTargets()、LeonFingerIKController.WriteCurrentWeaponTargetsToFingerRig()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType GetCurrentWeapon()
    {
        if (controller != null)
        {
            return controller.currentWeapon;
        }

        if (animRig != null)
        {
            return animRig.CurrentWeapon;
        }

        return WeaponType.Pistol;
    }

    /// <summary>FindWeaponRoot 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonFingerIKController.FindDeepChild(Transform, string)、LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonFingerIKController.FindDeepChild(Transform, string)、LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType) 的参数、LeonFingerIKController.FindWeaponRoot(WeaponType)。</param>
    private Transform FindWeaponRoot(WeaponType weapon)
    {
        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        // visualRoot（视觉表现根节点）：使用位置：LeonFingerIKController.FindWeaponRoot(WeaponType)。
Transform visualRoot;
        if (weaponVisualController != null)
        {
            visualRoot = weaponVisualController.GetWeaponVisualRoot(weapon);
        }
        else
        {
            visualRoot = null;
        }

        if (visualRoot != null)
        {
            return visualRoot;
        }

        // names（名称列表）：使用位置：LeonFingerIKController.FindWeaponRoot(WeaponType)。
string[] names;
        switch (weapon)
        {
            case WeaponType.Pistol:
                names = new[]
                {
                    "M1911 Handgun_Black (Shooting)",
                    "M1911 Handgun_Model",
                    "手枪"
                };
                break;
            case WeaponType.Rifle:
                names = new[]
                {
                    "M16 Assault Rifle (Shooting)",
                    "M16_Rifle_Model",
                    "步枪",
                    "Rifle"
                };
                break;
            case WeaponType.Shotgun:
                names = new[]
                {
                    "Mossberg 590 Shotgun (Shooting)",
                    "Mossberg_590_Model",
                    "霰弹枪",
                    "Shotgun"
                };
                break;
            case WeaponType.Knife:
                names = new[]
                {
                    "Combat Knife",
                    "Knife",
                    "小刀"
                };
                break;
            default:
                return null;
        }

        // name（名称）：每轮从名称列表取得一个元素，仅代表当前这一项。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数。
foreach (string name in names)
        {
            // found（找到的）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.FindWeaponRoot(WeaponType)。
Transform found = FindDeepChild(transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>FindDeepChild 的业务处理入口；本方法直接依赖 LeonFingerIKController.FindDeepChild(Transform, string)、Transform.GetChild(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)、LeonFingerIKController.FindDeepChild(Transform, string)、LeonFingerIKController.FindWeaponRoot(WeaponType)；下游（含内部回调体） LeonFingerIKController.FindDeepChild(Transform, string)、Transform.GetChild(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、LeonFingerIKController.FindDeepChild(Transform, string)。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 LeonFingerIKController.FindDeepChild(Transform, string) 的参数、LeonFingerIKController.FindDeepChild(Transform, string)。</param>
    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、LeonFingerIKController.FindDeepChild(Transform, string)。
for (int i = 0; i < parent.childCount; i++)
        {
            // result（当前变换节点）：保存 LeonFingerIKController.FindDeepChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：LeonFingerIKController.FindDeepChild(Transform, string)。
Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>GetFingerDisplayName 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.GetFingerTargetName(LeonFingerIKController.FingerSlot)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slot">中文含义：槽位。使用位置：LeonFingerIKController.GetFingerDisplayName(LeonFingerIKController.FingerSlot)。</param>
    private static string GetFingerDisplayName(FingerSlot slot)
    {
        return slot switch
        {
            FingerSlot.RightIndex => "右手食指",
            FingerSlot.RightMiddle => "右手中指",
            FingerSlot.RightRing => "右手无名指",
            FingerSlot.RightPinky => "右手小指",
            FingerSlot.RightThumb => "右手拇指",
            FingerSlot.LeftIndex => "左手食指",
            FingerSlot.LeftMiddle => "左手中指",
            FingerSlot.LeftRing => "左手无名指",
            FingerSlot.LeftPinky => "左手小指",
            FingerSlot.LeftThumb => "左手拇指",
            _ => "手指"
        };
    }

    /// <summary>GetTargetRootName 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonFingerIKController.GetTargetRootName(WeaponType)。</param>
    private static string GetTargetRootName(WeaponType weapon)
    {
        return $"手指目标_{weapon}";
    }

    /// <summary>GetFingerTargetName 的业务处理入口；本方法直接依赖 LeonFingerIKController.GetFingerDisplayName(LeonFingerIKController.FingerSlot)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.CapturePersistentWeaponTargets(WeaponType)、LeonFingerIKController.CreateTargetSetFromRoot(WeaponType, Transform, Transform)、LeonFingerIKController.EnsureWeaponTargetSet(WeaponType, bool)；下游（含内部回调体） LeonFingerIKController.GetFingerDisplayName(LeonFingerIKController.FingerSlot)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slot">中文含义：槽位。使用位置：作为 LeonFingerIKController.GetFingerDisplayName(LeonFingerIKController.FingerSlot) 的参数。</param>
    private static string GetFingerTargetName(FingerSlot slot)
    {
        return GetFingerDisplayName(slot) + "目标";
    }

    /// <summary>DestroySafe 的业务处理入口；本方法直接依赖 Object.Destroy(Object)、Object.DestroyImmediate(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonFingerIKController.RebuildCurrentWeaponTargets()；下游（含内部回调体） Object.Destroy(Object)、Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：作为 Object.Destroy(Object) 的参数、作为 Object.DestroyImmediate(Object) 的参数、LeonFingerIKController.DestroySafe(GameObject)。</param>
    private static void DestroySafe(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
