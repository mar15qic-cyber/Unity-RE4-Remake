using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家控制、动作与运动组件 LeonPlayerStairRampRuntime，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(1200)]
public class LeonPlayerStairRampRuntime : MonoBehaviour
{
    /// <summary>中文含义：玩家入口留白。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。</summary>
    private const float playerEntryPadding = 0.96f;
    /// <summary>中文含义：玩家竖直下部留白。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。</summary>
    private const float playerVerticalLowerPadding = 2.10f;
    /// <summary>中文含义：玩家竖直上部留白。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。</summary>
    private const float playerVerticalUpperPadding = 3.10f;
    /// <summary>中文含义：玩家表面净空。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</summary>
    private const float playerSurfaceClearance = 0.035f;
    /// <summary>中文含义：优先步进高度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.RoundToInt(float) 的参数。</summary>
    private const float preferredStepHeight = 0.24f;
    /// <summary>中文含义：步进支撑厚度。编译期常量，保持既有值与消费者映射。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    private const float stepSupportThickness = 0.08f;
    /// <summary>中文含义：玩家向上探测深度。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</summary>
    private const float playerUpProbeDepth = 0.42f;
    /// <summary>中文含义：玩家向下探测深度。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</summary>
    private const float playerDownProbeDepth = 0.36f;
    /// <summary>中文含义：玩家向上竖直速度最小。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    private const float playerUpVerticalSpeedMinimum = 0.85f;
    /// <summary>中文含义：玩家向上竖直速度最大。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    private const float playerUpVerticalSpeedMaximum = 2.35f;
    /// <summary>中文含义：玩家向下竖直速度最大。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    private const float playerDownVerticalSpeedMaximum = 2.80f;
    /// <summary>中文含义：玩家竖直校正量增益。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    private const float playerVerticalCorrectionGain = 7.5f;
    /// <summary>中文含义：玩家轴输入死亡区。编译期常量，保持既有值与消费者映射。使用位置：LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。</summary>
    private const float playerAxisInputDeadZone = 0.12f;
    /// <summary>中文含义：玩家高退出向上淡入淡出距离。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)。</summary>
    private const float playerHighExitUpFadeDistance = 0.58f;
    /// <summary>中文含义：玩家高退出向上停止距离。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)。</summary>
    private const float playerHighExitUpStopDistance = 0.22f;
    /// <summary>中文含义：玩家竖直辅助留白。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float) 的参数。</summary>
    private const float playerVerticalAssistPadding = 0.36f;
    /// <summary>中文含义：斜坡来源集合。使用位置：作为 LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource) 的参数、LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.RefreshRamps()。</summary>
    [Header("楼梯分段与踏面")]
    public RampSource[] rampSources;
    /// <summary>中文含义：初始角色。使用位置：LeonPlayerStairRampRuntime.PrepareTraversal()。</summary>
    [Header("场景内已有敌人")]
    [UnityEngine.Serialization.FormerlySerializedAs("初始角色")]
    [UnityEngine.InspectorName("初始角色")]
    public VillagerController[] initialCharacter;
    /// <summary>中文含义：角色。只读字段，初始化后不由普通写入口替换。使用位置：LeonPlayerStairRampRuntime.PrepareTraversal()、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()、LeonPlayerStairRampRuntime.RegisterCharacter(VillagerController) 等共 4 处。</summary>
    private readonly HashSet<VillagerController> character = new HashSet<VillagerController>();
    /// <summary>中文含义：角色碰撞。只读字段，初始化后不由普通写入口替换。使用位置：作为 Component.GetComponentsInChildren&lt;Collider&gt;(bool, List&lt;Collider&gt;) 的参数、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。</summary>
    private readonly List<Collider> characterCollision = new List<Collider>();
    /// <summary>中文含义：是否具有准备。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)、LeonPlayerStairRampRuntime.PrepareTraversal()、LeonPlayerStairRampRuntime.QueryStair(Ray, float, out RaycastHit, out bool)。</summary>
    private bool hasPrepare;
    /// <summary>中文含义：实例。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)、LeonPlayerStairRampRuntime.CurrentStair.get、LeonPlayerStairRampRuntime.EnsureSceneRamps() 等共 7 处。</summary>
    private static LeonPlayerStairRampRuntime instance;
    /// <summary>中文含义：斜坡碰撞体集合。只读字段，初始化后不由普通写入口替换。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)、LeonPlayerStairRampRuntime.QueryStair(Ray, float, out RaycastHit, out bool)、LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource) 等共 5 处。</summary>
    private readonly List<Collider> rampColliders = new List<Collider>();
    /// <summary>中文含义：敌人已忽略碰撞体集合。只读字段，初始化后不由普通写入口替换。使用位置：LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()、LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource) 等共 5 处。</summary>
    private readonly List<Collider> enemyIgnoredColliders = new List<Collider>();
    /// <summary>中文含义：当前楼梯。使用位置：作为 BipedFootPlanting.WeightBindStair(MonoBehaviour) 的参数、EnemyFootAction.LateUpdate()、EnemyFootAction.Start()。</summary>
    internal static LeonPlayerStairRampRuntime CurrentStair => instance;

    /// <summary>中文含义：下部行胶囊。使用位置：LeonPlayerStairRampRuntime.FixedUpdate()、LeonPlayerStairRampRuntime.RestoreCapsuleFriction()、LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。</summary>
    private CapsuleCollider lowerRowCapsule;
    /// <summary>中文含义：originalCapsuleMaterial 表示“原始胶囊材质”；lowerRowMaterial 表示“下部行材质”。使用位置：LeonPlayerStairRampRuntime.RestoreCapsuleFriction()、LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。；使用位置：作为 Object.Destroy(Object) 的参数、LeonPlayerStairRampRuntime.OnDisable()、LeonPlayerStairRampRuntime.RestoreCapsuleFriction() 等共 4 处。</summary>
    private PhysicsMaterial originalCapsuleMaterial, lowerRowMaterial;
    /// <summary>中文含义：下部行物理时间戳。使用位置：LeonPlayerStairRampRuntime.FixedUpdate()、LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。</summary>
    private double lowerRowPhysicsTimestamp;
    /// <summary>中文含义：下一次敌人刷新时间。使用位置：LeonPlayerStairRampRuntime.Update()。</summary>
    private float nextEnemyRefreshTime;
    /// <summary>EnsureSceneRamps 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.PrepareTraversal()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerStairRampRuntime.PrepareTraversal()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void EnsureSceneRamps()
    {
        // receiverValue1（判空前缓存的实例）：从实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonPlayerStairRampRuntime.EnsureSceneRamps()。
global::LeonPlayerStairRampRuntime receiverValue1 = instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.PrepareTraversal();
        }
    }

    /// <summary>RegisterCharacter 的业务处理入口；本方法直接依赖 HashSet&lt;VillagerController&gt;.Add(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；下游（含内部回调体） HashSet&lt;VillagerController&gt;.Add(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 HashSet&lt;VillagerController&gt;.Add(VillagerController) 的参数、LeonPlayerStairRampRuntime.RegisterCharacter(VillagerController)。</param>
    internal static void RegisterCharacter(VillagerController enemy)
    {
        if (instance != null && enemy != null)
        {
            instance.character.Add(enemy);
        }
    }

    /// <summary>RemoveCharacter 的业务处理入口；本方法直接依赖 HashSet&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnDestroy()；下游（含内部回调体） HashSet&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 HashSet&lt;VillagerController&gt;.Remove(VillagerController) 的参数。</param>
    internal static void RemoveCharacter(VillagerController enemy)
    {
        if (instance != null)
        {
            instance.character.Remove(enemy);
        }
    }

    /// <summary>ConfigurationError 的业务处理入口；本方法直接依赖 HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.HashSet()、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.PrepareTraversal()、SceneStartupEntry.ConfigurationError()；下游（含内部回调体） HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.HashSet()、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal string ConfigurationError()
    {
        if (rampSources == null || rampSources.Length == 0)
        {
            return nameof(rampSources);
        }

        // name（名称）：使用位置：LeonPlayerStairRampRuntime.ConfigurationError()。
var name = new HashSet<string>();
        // segment（线段）：每轮从斜坡来源集合取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<string>.Add(string) 的参数、作为 string.IsNullOrWhiteSpace(string) 的参数、LeonPlayerStairRampRuntime.ConfigurationError()。
foreach (var segment in rampSources)
        {
            if (segment == null || string.IsNullOrWhiteSpace(segment.rampName) || !name.Add(segment.rampName))
            {
                return "分段名称";
            }

            if (!(segment.thickness > 0f && segment.minimumWidth > 0f))
            {
                return segment.rampName + ".厚度或宽度";
            }

            if (segment.surface == null || segment.surface.gameObject.scene != gameObject.scene ||
                segment.surfaceSize.x <= 0f || segment.surfaceSize.y <= 0f)
            {
                return segment.rampName + ".踏面引用或尺寸";
            }
        }

        return null;
    }

    /// <summary>PrepareTraversal 的业务处理入口；本方法直接依赖 HashSet&lt;VillagerController&gt;.Add(VillagerController)、LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.RefreshRamps()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.EnsureSceneRamps()、SceneStartupEntry.Start()；下游（含内部回调体） HashSet&lt;VillagerController&gt;.Add(VillagerController)、LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.RefreshRamps()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void PrepareTraversal()
    {
        if (hasPrepare || ConfigurationError() != null)
        {
            return;
        }

        hasPrepare = true;
        if (initialCharacter != null)
        {
            // enemy（敌人）：每轮从初始角色取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<VillagerController>.Add(VillagerController) 的参数、LeonPlayerStairRampRuntime.PrepareTraversal()。
foreach (var enemy in initialCharacter)
            {
                if (enemy != null)
                {
                    character.Add(enemy);
                }
            }
        }

        RefreshRamps();
    }

    /// <summary>ApplyPlayerMotionAssist 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonActionMotionDriver&gt;()、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonActionMotionDriver&gt;()、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：里昂玩家控制器。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。</param>
    /// <param name="body">中文含义：身体。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。</param>
    /// <param name="capsule">中文含义：胶囊。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。</param>
    /// <param name="moveDirection">中文含义：移动方向。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。</param>
    public static bool ApplyPlayerMotionAssist(LeonController controller, Rigidbody body, CapsuleCollider capsule, Vector3 moveDirection)
    {
        // executor（执行器）：使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。
global::LeonActionMotionDriver executor;
        if (controller != null)
        {
            executor = controller.GetComponent<LeonActionMotionDriver>();
        }
        else
        {
            executor = null;
        }

        // argumentValue4（按原顺序缓存的实参里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。
global::LeonController argumentValue4 = controller;
        // argumentValue5（按原顺序缓存的实参身体）：从身体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。
global::UnityEngine.Rigidbody argumentValue5 = body;
        // argumentValue6（按原顺序缓存的实参胶囊）：从胶囊读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。
global::UnityEngine.CapsuleCollider argumentValue6 = capsule;
        // argumentValue7（按原顺序缓存的实参移动方向）：从移动方向读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。
global::UnityEngine.Vector3 argumentValue7 = moveDirection;
        // argumentValue8（按原顺序缓存的实参执行器）：从执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。
global::LeonActionMotionDriver argumentValue8 = executor;
        // selectedValue3（分支选出的执行器的当前运动凭证）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)。
global::GameArchitecture.MotionControlToken selectedValue3;
        if (executor != null)
        {
            selectedValue3 = executor.CurrentMotionToken;
        }
        else
        {
            selectedValue3 = default;
        }

        return ApplyPlayerMotionAssist(argumentValue4, argumentValue5, argumentValue6, argumentValue7, argumentValue8, selectedValue3);
    }

    /// <summary>ApplyPlayerMotionAssist 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3)；下游（含内部回调体） LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：里昂玩家控制器。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="body">中文含义：身体。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="capsule">中文含义：胶囊。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="moveDirection">中文含义：移动方向。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数。</param>
    /// <param name="executor">中文含义：执行器。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    public static bool ApplyPlayerMotionAssist(LeonController controller, Rigidbody body, CapsuleCollider capsule, Vector3 moveDirection, LeonActionMotionDriver executor, GameArchitecture.MotionControlToken token)
    {
        if (controller == null || body == null || capsule == null || executor == null || token.Source != GameArchitecture.MotionSource.NormalMovement)
        {
            return false;
        }

        return instance != null && instance.hasPrepare && instance.ApplyPlayerMotionAssistInternal(controller, body, capsule, moveDirection, executor, token);
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable() => instance = this;
    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerStairRampRuntime.RestoreCapsuleFriction()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        RestoreCapsuleFriction();
        if (lowerRowMaterial)
        {
            Destroy(lowerRowMaterial);
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>Unity 固定时间步更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerStairRampRuntime.RestoreCapsuleFriction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FixedUpdate()
    {
        if (lowerRowCapsule && lowerRowPhysicsTimestamp != Time.fixedTimeAsDouble)
        {
            RestoreCapsuleFriction();
        }
    }

    /// <summary>UseLowerRowFriction 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.RestoreCapsuleFriction()、PhysicsMaterial.PhysicsMaterial(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonPlayerStairRampRuntime.RestoreCapsuleFriction()、PhysicsMaterial.PhysicsMaterial(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="capsule">中文含义：胶囊。使用位置：LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。</param>
    private void UseLowerRowFriction(CapsuleCollider capsule)
    {
        lowerRowPhysicsTimestamp = Time.fixedTimeAsDouble;
        if (lowerRowCapsule == capsule && capsule.sharedMaterial == lowerRowMaterial)
        {
            return;
        }

        RestoreCapsuleFriction();
        originalCapsuleMaterial = capsule.sharedMaterial;
        if (!lowerRowMaterial)
        {
            lowerRowMaterial = new PhysicsMaterial("下楼通行");
        }

        lowerRowMaterial.staticFriction = lowerRowMaterial.dynamicFriction = 0f;
        lowerRowMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        // assignmentReceiver9（赋值前保存的目标下部行材质）：从下部行材质读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。
global::UnityEngine.PhysicsMaterial assignmentReceiver9 = lowerRowMaterial;
        if (originalCapsuleMaterial)
        {
            assignmentReceiver9.bounciness = originalCapsuleMaterial.bounciness;
        }
        else
        {
            assignmentReceiver9.bounciness = 0f;
        }

        // assignmentReceiver11（赋值前保存的目标下部行材质）：从下部行材质读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)。
global::UnityEngine.PhysicsMaterial assignmentReceiver11 = lowerRowMaterial;
        if (originalCapsuleMaterial)
        {
            assignmentReceiver11.bounceCombine = originalCapsuleMaterial.bounceCombine;
        }
        else
        {
            assignmentReceiver11.bounceCombine = PhysicsMaterialCombine.Average;
        }

        lowerRowCapsule = capsule;
        capsule.sharedMaterial = lowerRowMaterial;
    }

    /// <summary>RestoreCapsuleFriction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.FixedUpdate()、LeonPlayerStairRampRuntime.OnDisable()、LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestoreCapsuleFriction()
    {
        if (lowerRowCapsule && lowerRowCapsule.sharedMaterial == lowerRowMaterial)
        {
            lowerRowCapsule.sharedMaterial = originalCapsuleMaterial;
        }

        lowerRowCapsule = null;
        originalCapsuleMaterial = null;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        if (Time.time < nextEnemyRefreshTime)
        {
            return;
        }

        nextEnemyRefreshTime = Time.time + 0.5f;
        RefreshEnemyCollisionIgnores();
    }

    /// <summary>RefreshRamps 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()、LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)、List&lt;Collider&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.PrepareTraversal()；下游（含内部回调体） LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()、LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)、List&lt;Collider&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RefreshRamps()
    {
        rampColliders.Clear();
        enemyIgnoredColliders.Clear();
        // i（当前遍历索引）：使用位置：LeonPlayerStairRampRuntime.RefreshRamps()。
for (int i = 0; i < rampSources.Length; i++)
        {
            RefreshRamp(rampSources[i]);
        }

        RefreshEnemyCollisionIgnores();
    }

    /// <summary>RefreshRamp 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GetComponent&lt;BoxCollider&gt;()、GameObject.SetActive(bool)、LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.FindOrCreateChild(string)、LeonPlayerStairRampRuntime.RefreshSideGuards(GameObject, BoxCollider) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshRamps()；下游（含内部回调体） GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GetComponent&lt;BoxCollider&gt;()、GameObject.SetActive(bool)、LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.FindOrCreateChild(string)、LeonPlayerStairRampRuntime.RefreshSideGuards(GameObject, BoxCollider) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源斜坡来源。使用位置：作为 LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource) 的参数、作为 LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource) 的参数、作为 LeonPlayerStairRampRuntime.FindOrCreateChild(string) 的参数 等共 4 处。</param>
    private void RefreshRamp(RampSource source)
    {
        GameObject ramp = FindOrCreateChild(source.rampName);
        // rampBox（斜坡箱体）：保存 GameObject.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：作为 LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource) 的参数、作为 LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource) 的参数、作为 LeonPlayerStairRampRuntime.RefreshSideGuards(GameObject, BoxCollider) 的参数 等共 6 处。
BoxCollider rampBox = ramp.GetComponent<BoxCollider>();
        if (rampBox == null)
        {
            rampBox = ramp.AddComponent<BoxCollider>();
        }

        Transform surface = source.surface;
        Vector3 center = surface.position + surface.up * (source.surfaceLift - source.thickness * 0.5f);
        ramp.transform.SetPositionAndRotation(center, surface.rotation);
        ramp.transform.localScale = Vector3.one;
        ramp.layer = 0;
        rampBox.center = Vector3.zero;
        rampBox.size = new Vector3(Mathf.Max(source.minimumWidth, source.surfaceSize.x + source.widthPadding),
            source.thickness, source.surfaceSize.y + source.lengthPadding);
        rampBox.isTrigger = true;

        ramp.SetActive(true);
        rampColliders.Add(rampBox);
        enemyIgnoredColliders.Add(rampBox);
        if (source.createsSideGuards)
        {
            RefreshSideGuards(ramp, rampBox);
        }

        RefreshStepSupports(ramp, rampBox);
    }

    /// <summary>FindOrCreateChild 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)；下游（含内部回调体） GameObject.GameObject(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string) 的参数、作为 Transform.Find(string) 的参数。</param>
    private GameObject FindOrCreateChild(string objectName)
    {
        // child（子级）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.FindOrCreateChild(string)。
Transform child = transform.Find(objectName);
        if (child != null)
        {
            return child.gameObject;
        }

        // childObject（子级对象）：使用位置：LeonPlayerStairRampRuntime.FindOrCreateChild(string)。
GameObject childObject = new GameObject(objectName);
        childObject.transform.SetParent(transform, false);
        return childObject;
    }

    /// <summary>RefreshSideGuards 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)；下游（含内部回调体） LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float) 的参数。</param>
    /// <param name="rampBox">中文含义：斜坡箱体。使用位置：作为 LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float) 的参数。</param>
    private void RefreshSideGuards(GameObject ramp, BoxCollider rampBox)
    {
        ConfigureSideGuard(ramp, rampBox, "LeftGuard", -1f);
        ConfigureSideGuard(ramp, rampBox, "RightGuard", 1f);
    }

    /// <summary>ConfigureSideGuard 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;BoxCollider&gt;()、LeonPlayerStairRampRuntime.ResolveScale(Transform)、List&lt;Collider&gt;.Add(Collider)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshSideGuards(GameObject, BoxCollider)；下游（含内部回调体） GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;BoxCollider&gt;()、LeonPlayerStairRampRuntime.ResolveScale(Transform)、List&lt;Collider&gt;.Add(Collider)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。</param>
    /// <param name="rampBox">中文含义：斜坡箱体。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。</param>
    /// <param name="guardName">中文含义：防守名称。使用位置：作为 GameObject.GameObject(string) 的参数、作为 Transform.Find(string) 的参数。</param>
    /// <param name="side">中文含义：侧边。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    private void ConfigureSideGuard(GameObject ramp, BoxCollider rampBox, string guardName, float side)
    {
        // child（子级）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。
Transform child = ramp.transform.Find(guardName);
        // guard（防守）：使用位置：LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。
GameObject guard;
        if (child != null)
        {
            guard = child.gameObject;
        }
        else
        {
            guard = new GameObject(guardName);
        }

        guard.transform.SetParent(ramp.transform, false);
        guard.transform.localPosition = Vector3.zero;
        guard.transform.localRotation = Quaternion.identity;
        guard.transform.localScale = Vector3.one;
        guard.layer = ramp.layer;
        // guardBox（防守箱体）：保存 GameObject.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：作为 List<Collider>.Add(Collider) 的参数、LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。
BoxCollider guardBox = guard.GetComponent<BoxCollider>();
        if (guardBox == null)
        {
            guardBox = guard.AddComponent<BoxCollider>();
        }

        // scale（缩放）：保存 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = ResolveScale(ramp.transform);
        // localThickness（局部厚度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)。
float localThickness = 0.18f / Mathf.Max(0.001f, Mathf.Abs(scale.x));
        // localHeight（局部高度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float localHeight = 1.80f / Mathf.Max(0.001f, Mathf.Abs(scale.y));
        // localBase（局部基础）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float localBase = -0.10f / Mathf.Max(0.001f, Mathf.Abs(scale.y));
        // localLengthPadding（局部长度留白）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float localLengthPadding = 0.65f / Mathf.Max(0.001f, Mathf.Abs(scale.z));
        // localSideOffset（局部侧边偏移量）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float localSideOffset = rampBox.size.x * 0.5f + localThickness * 0.5f - 0.04f / Mathf.Max(0.001f, Mathf.Abs(scale.x));
        guardBox.center = new Vector3(rampBox.center.x + side * localSideOffset, rampBox.center.y + localBase + localHeight * 0.5f, rampBox.center.z);
        guardBox.size = new Vector3(localThickness, localHeight, rampBox.size.z + localLengthPadding);
        guardBox.isTrigger = true;
        enemyIgnoredColliders.Add(guardBox);
    }

    /// <summary>RefreshStepSupports 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;BoxCollider&gt;()、GameObject.SetActive(bool)、LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)、LeonPlayerStairRampRuntime.ResolveStepCount(float, float)、LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider) 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)；下游（含内部回调体） GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;BoxCollider&gt;()、GameObject.SetActive(bool)、LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)、LeonPlayerStairRampRuntime.ResolveStepCount(float, float)、LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider) 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。</param>
    /// <param name="rampBox">中文含义：斜坡箱体。使用位置：作为 LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider) 的参数、作为 LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float) 的参数。</param>
    private void RefreshStepSupports(GameObject ramp, BoxCollider rampBox)
    {
        // supportRoot（支撑根节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
Transform supportRoot = ramp.transform.Find("StepSupports");
        // supportRootObject（支撑根游戏对象）：使用位置：作为 LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
GameObject supportRootObject;
        if (supportRoot != null)
        {
            supportRootObject = supportRoot.gameObject;
        }
        else
        {
            supportRootObject = new GameObject("StepSupports");
        }

        supportRootObject.transform.SetParent(ramp.transform, false);
        supportRootObject.transform.localPosition = Vector3.zero;
        supportRootObject.transform.localRotation = Quaternion.identity;
        supportRootObject.transform.localScale = Vector3.one;
        if (!TryResolveRampEnds(rampBox, out Vector3 /* 中文含义：低点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lowPoint, out Vector3 /* 中文含义：高点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ highPoint, out _, out _))
        {
            supportRootObject.SetActive(false);
            return;
        }

        supportRootObject.SetActive(true);
        // stepCount（步进数量）：保存 LeonPlayerStairRampRuntime.ResolveStepCount(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
int stepCount = ResolveStepCount(lowPoint.y, highPoint.y);
        // supportCount（支撑数量）：使用位置：作为 Mathf.Max(int, int) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
int supportCount = stepCount + 1;
        // horizontalForward（水平前向）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.LookRotation(Vector3, Vector3) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
Vector3 horizontalForward = Vector3.ProjectOnPlane(highPoint - lowPoint, Vector3.up);
        if (horizontalForward.sqrMagnitude <= 0.0001f)
        {
            horizontalForward = Vector3.ProjectOnPlane(ramp.transform.forward, Vector3.up);
        }

        if (horizontalForward.sqrMagnitude <= 0.0001f)
        {
            horizontalForward = Vector3.forward;
        }

        horizontalForward.Normalize();
        // supportRotation（支撑旋转）：保存 Quaternion.LookRotation(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数。
Quaternion supportRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
        // horizontalLength（水平长度）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float horizontalLength = Vector3.Distance(Vector3.ProjectOnPlane(lowPoint, Vector3.up), Vector3.ProjectOnPlane(highPoint, Vector3.up));
        // supportDepth（支撑深度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float supportDepth = Mathf.Max(0.18f, horizontalLength / Mathf.Max(1, supportCount));
        // supportWidth（支撑宽度）：保存 LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float supportWidth = ResolveWorldWidth(rampBox);
        // rise（抬升）：使用位置：LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
float rise = highPoint.y - lowPoint.y;
        // i（当前遍历索引）：使用位置：作为 LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
for (int i = 0; i < supportCount; i++)
        {
            // support（支撑）：保存 LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
GameObject support = FindOrCreateStepSupport(supportRootObject.transform, i);
            // supportBox（支撑箱体）：保存 GameObject.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：作为 List<Collider>.Add(Collider) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
BoxCollider supportBox = support.GetComponent<BoxCollider>();
            if (supportBox == null)
            {
                supportBox = support.AddComponent<BoxCollider>();
            }

            // progress（进度）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
float progress;
            if (supportCount <= 1)
            {
                progress = 0f;
            }
            else
            {
                progress = (i + 0.5f) / supportCount;
            }

            // basePoint（基础点）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 basePoint = Vector3.Lerp(lowPoint, highPoint, progress);
            // topY（顶部纵向）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float topY = lowPoint.y + rise * (i / Mathf.Max(1f, stepCount));
            // center（中心）：使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数。
Vector3 center = new Vector3(basePoint.x, topY - stepSupportThickness * 0.5f, basePoint.z);
            support.transform.SetPositionAndRotation(center, supportRotation);
            support.transform.localScale = Vector3.one;
            support.layer = ramp.layer;
            supportBox.center = Vector3.zero;
            supportBox.size = new Vector3(supportWidth, stepSupportThickness, supportDepth);
            supportBox.isTrigger = true;
            support.SetActive(true);
            enemyIgnoredColliders.Add(supportBox);
        }

        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)。
for (int i = supportCount; i < supportRootObject.transform.childCount; i++)
        {
            supportRootObject.transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    /// <summary>FindOrCreateStepSupport 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、int.ToString(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)；下游（含内部回调体） GameObject.GameObject(string)、int.ToString(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根变换节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)。</param>
    /// <param name="index">中文含义：索引。使用位置：LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)。</param>
    private static GameObject FindOrCreateStepSupport(Transform root, int index)
    {
        // objectName（对象名称）：使用位置：作为 GameObject.GameObject(string) 的参数、作为 Transform.Find(string) 的参数。
string objectName = "StepSupport_" + index.ToString("00");
        // child（子级）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)。
Transform child = root.Find(objectName);
        if (child != null)
        {
            return child.gameObject;
        }

        // support（支撑）：使用位置：LeonPlayerStairRampRuntime.FindOrCreateStepSupport(Transform, int)。
GameObject support = new GameObject(objectName);
        support.transform.SetParent(root, false);
        return support;
    }

    /// <summary>RefreshEnemyCollisionIgnores 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Collider&gt;(bool, List&lt;Collider&gt;)、Physics.IgnoreCollision(Collider, Collider, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshRamps()、LeonPlayerStairRampRuntime.Update()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Collider&gt;(bool, List&lt;Collider&gt;)、Physics.IgnoreCollision(Collider, Collider, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RefreshEnemyCollisionIgnores()
    {
        if (enemyIgnoredColliders.Count == 0)
        {
            return;
        }

        // enemy（敌人）：每轮从角色取得一个元素，仅代表当前这一项。使用位置：LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。
foreach (var enemy in character)
        {
            if (enemy == null)
            {
                continue;
            }

            enemy.GetComponentsInChildren(true, characterCollision);
            // rampIndex（斜坡索引）：使用位置：作为 List<Collider>.this[int] 的参数、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。
for (int rampIndex = 0; rampIndex < enemyIgnoredColliders.Count; rampIndex++)
            {
                // ramp（斜坡）：使用位置：作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。
Collider ramp = enemyIgnoredColliders[rampIndex];
                if (ramp == null)
                {
                    continue;
                }

                // colliderIndex（碰撞体索引）：使用位置：作为 List<Collider>.this[int] 的参数、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。
for (int colliderIndex = 0; colliderIndex < characterCollision.Count; colliderIndex++)
                {
                    // enemyCollider（敌人碰撞体）：使用位置：作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数、LeonPlayerStairRampRuntime.RefreshEnemyCollisionIgnores()。
Collider enemyCollider = characterCollision[colliderIndex];
                    if (enemyCollider == null || enemyCollider == ramp)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ramp, enemyCollider, true);
                }
            }
        }
    }

    /// <summary>ApplyPlayerMotionAssistInternal 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float)、LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider)、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float)、LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider)、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：里昂玩家控制器。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="body">中文含义：身体。使用位置：作为 LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="capsule">中文含义：胶囊。使用位置：作为 LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider) 的参数、作为 LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3) 的参数、作为 LeonPlayerStairRampRuntime.UseLowerRowFriction(CapsuleCollider) 的参数。</param>
    /// <param name="moveDirection">中文含义：移动方向。使用位置：作为 LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool) 的参数。</param>
    /// <param name="executor">中文含义：执行器。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。</param>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数。</param>
    private bool ApplyPlayerMotionAssistInternal(LeonController controller, Rigidbody body, CapsuleCollider capsule, Vector3 moveDirection, LeonActionMotionDriver executor, GameArchitecture.MotionControlToken token)
    {
        if (rampColliders.Count == 0 || controller.currentState == LeonState.Dead || controller.currentState == LeonState.QTE || controller.currentState == LeonState.Traversing)
        {
            return false;
        }

        if (!TryFindPlayerRamp(body.position, capsule, out BoxCollider /* 中文含义：斜坡。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ ramp, out Vector3 /* 中文含义：局部位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ localPosition))
        {
            return false;
        }

        // center（中心）：使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
Vector3 center = ramp.center;
        // halfSize（一半尺寸）：使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
Vector3 halfSize = ramp.size * 0.5f;
        // coreBottom（核心底部）：使用位置：作为 LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float coreBottom = center.z - halfSize.z;
        // coreTop（核心顶部）：使用位置：作为 LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float coreTop = center.z + halfSize.z;
        // localBottom（局部底部）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
float localBottom = coreBottom - playerEntryPadding;
        // localTop（局部顶部）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
float localTop = coreTop + playerEntryPadding;
        if (!TryResolveStairInput(ramp, moveDirection, out float /* 中文含义：轴输入。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ axisInput, out bool /* 中文含义：是否移动中目标高侧边。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ movingToHighSide))
        {
            return true;
        }

        if (!IsInsideLocalRange(localPosition.z, coreBottom - playerVerticalAssistPadding, coreTop + playerVerticalAssistPadding))
        {
            return true;
        }

        if (!movingToHighSide)
        {
            UseLowerRowFriction(capsule);
        }

        // probeDepth（探测深度）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float probeDepth;
        if (movingToHighSide)
        {
            probeDepth = playerUpProbeDepth;
        }
        else
        {
            probeDepth = playerDownProbeDepth;
        }

        // probeLocalZ（探测局部深度）：保存 Mathf.Clamp(float, float, float) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float probeLocalZ = Mathf.Clamp(localPosition.z + Mathf.Sign(axisInput) * probeDepth, localBottom, localTop);
        // probeLocalPosition（探测局部位置）：使用位置：作为 LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
Vector3 probeLocalPosition = localPosition;
        probeLocalPosition.z = probeLocalZ;
        // surfaceY（表面纵向）：保存 LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float surfaceY = ResolveSteppedSurfaceY(ramp, probeLocalPosition);
        // bottomOffset（底部偏移量）：保存 LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float bottomOffset = ResolveCapsuleBottomOffset(capsule);
        // targetRootY（目标根节点纵向）：使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float targetRootY = surfaceY - bottomOffset + playerSurfaceClearance;
        // velocity（速度）：使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 5 处。
Vector3 velocity = body.linearVelocity;
        // heightDelta（高度增量）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float heightDelta = targetRootY - body.position.y;
        if (movingToHighSide && heightDelta > 0.015f)
        {
            // exitScale（退出缩放）：保存 LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float exitScale = ResolveHighExitUpAssistScale(ramp, localPosition);
            if (exitScale <= 0.001f)
            {
                if (velocity.y > 0f)
                {
                    velocity.y = 0f;
                }

                executor.CommitSpeed(token, velocity);
                return true;
            }

            velocity.y = Mathf.Max(velocity.y, Mathf.Clamp(heightDelta * playerVerticalCorrectionGain, playerUpVerticalSpeedMinimum, playerUpVerticalSpeedMaximum) * exitScale);
        }
        else if (!movingToHighSide && heightDelta < -0.015f)
        {
            // 只沿楼梯坡度向下跟随。慢走时朝下一踏面强压胶囊会使阶面摩擦抵消水平步速。
            // slopeAxis（坡度轴）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数。
Vector3 slopeAxis = ramp.transform.forward;
            // slopeRatio（坡度比例）：使用位置：LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)。
float slopeRatio = Mathf.Abs(slopeAxis.y) / Mathf.Max(.01f, Vector3.ProjectOnPlane(slopeAxis, Vector3.up).magnitude);
            // lowerRowSpeed（下部行速度）：使用位置：作为 Mathf.Min(float, float) 的参数。
float lowerRowSpeed = Mathf.Abs(Vector3.Dot(velocity, Vector3.ProjectOnPlane(slopeAxis, Vector3.up).normalized)) * slopeRatio;
            velocity.y = Mathf.Min(velocity.y, -Mathf.Min(lowerRowSpeed, playerDownVerticalSpeedMaximum));
        }

        executor.CommitSpeed(token, velocity);
        return true;
    }

    /// <summary>ResolveHighExitUpAssistScale 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ResolveScale(Transform)、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.InverseLerp(float, float, float)、Mathf.Sign(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonPlayerStairRampRuntime.ResolveScale(Transform)、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.InverseLerp(float, float, float)、Mathf.Sign(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的参数、作为 LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float) 的参数。</param>
    /// <param name="localPosition">中文含义：局部位置。使用位置：LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)。</param>
    private static float ResolveHighExitUpAssistScale(BoxCollider ramp, Vector3 localPosition)
    {
        if (!TryResolveRampEnds(ramp, out _, out _, out float /* 中文含义：低局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lowLocalZ, out float /* 中文含义：高局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ highLocalZ))
        {
            return 1f;
        }

        // directionToHighSide（方向目标高侧边）：保存 Mathf.Sign(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)。
float directionToHighSide = Mathf.Sign(highLocalZ - lowLocalZ);
        if (Mathf.Approximately(directionToHighSide, 0f))
        {
            return 1f;
        }

        // scale（缩放）：保存 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = ResolveScale(ramp.transform);
        // remainingDistance（剩余距离）：使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)。
float remainingDistance = (highLocalZ - localPosition.z) * directionToHighSide * Mathf.Abs(scale.z);
        if (remainingDistance <= playerHighExitUpStopDistance)
        {
            return 0f;
        }

        if (remainingDistance >= playerHighExitUpFadeDistance)
        {
            return 1f;
        }

        return Mathf.InverseLerp(playerHighExitUpStopDistance, playerHighExitUpFadeDistance, remainingDistance);
    }

    /// <summary>IsInsideLocalRange 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float)。</param>
    /// <param name="edgeA">中文含义：边缘起点。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</param>
    /// <param name="edgeB">中文含义：边缘终点。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</param>
    private static bool IsInsideLocalRange(float value, float edgeA, float edgeB)
    {
        // min（最小）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float)。
float min = Mathf.Min(edgeA, edgeB);
        // max（最大）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.IsInsideLocalRange(float, float, float)。
float max = Mathf.Max(edgeA, edgeB);
        return value >= min && value <= max;
    }

    /// <summary>TryResolveStairInput 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.Clamp(float, float, float)、Mathf.Sign(float)、Transform.InverseTransformDirection(Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.Clamp(float, float, float)、Mathf.Sign(float)、Transform.InverseTransformDirection(Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float) 的参数、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。</param>
    /// <param name="moveDirection">中文含义：移动方向。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数。</param>
    /// <param name="axisInput">中文含义：轴输入。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。</param>
    /// <param name="movingToHighSide">中文含义：是否移动中目标高侧边。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。</param>
    private static bool TryResolveStairInput(BoxCollider ramp, Vector3 moveDirection, out float axisInput, out bool movingToHighSide)
    {
        axisInput = 0f;
        movingToHighSide = false;
        // flatMove（平面移动）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。
Vector3 flatMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
        if (flatMove.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        // localMove（局部移动）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
Vector3 localMove = ramp.transform.InverseTransformDirection(flatMove.normalized);
        axisInput = Mathf.Clamp(localMove.z, -1f, 1f);
        if (Mathf.Abs(axisInput) <= playerAxisInputDeadZone)
        {
            return false;
        }

        if (!TryResolveRampEnds(ramp, out _, out _, out float /* 中文含义：低局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lowLocalZ, out float /* 中文含义：高局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ highLocalZ))
        {
            movingToHighSide = axisInput > 0f;
            return true;
        }

        // directionToHighSide（方向目标高侧边）：保存 Mathf.Sign(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)。
float directionToHighSide = Mathf.Sign(highLocalZ - lowLocalZ);
        if (Mathf.Approximately(directionToHighSide, 0f))
        {
            movingToHighSide = axisInput > 0f;
            return true;
        }

        movingToHighSide = Mathf.Sign(axisInput) == directionToHighSide;
        return true;
    }

    /// <summary>TryFindPlayerRamp 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ResolveCapsuleRadius(CapsuleCollider)、LeonPlayerStairRampRuntime.ResolveScale(Transform)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonPlayerStairRampRuntime.ResolveCapsuleRadius(CapsuleCollider)、LeonPlayerStairRampRuntime.ResolveScale(Transform)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。</param>
    /// <param name="capsule">中文含义：胶囊。使用位置：作为 LeonPlayerStairRampRuntime.ResolveCapsuleRadius(CapsuleCollider) 的参数。</param>
    /// <param name="bestRamp">中文含义：最佳斜坡。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。</param>
    /// <param name="bestLocalPosition">中文含义：最佳局部位置。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。</param>
    private bool TryFindPlayerRamp(Vector3 playerPosition, CapsuleCollider capsule, out BoxCollider bestRamp, out Vector3 bestLocalPosition)
    {
        bestRamp = null;
        bestLocalPosition = Vector3.zero;
        // bestScore（最佳分值）：使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float bestScore = float.MaxValue;
        // radius（半径）：保存 LeonPlayerStairRampRuntime.ResolveCapsuleRadius(CapsuleCollider) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float radius = ResolveCapsuleRadius(capsule);
        // i（当前遍历索引）：使用位置：作为 List<Collider>.this[int] 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
for (int i = 0; i < rampColliders.Count; i++)
        {
            // ramp（斜坡）：使用位置：作为 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
BoxCollider ramp = rampColliders[i] as BoxCollider;
            if (ramp == null || !ramp.enabled || !ramp.gameObject.activeInHierarchy)
            {
                continue;
            }

            // local（局部）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
Vector3 local = ramp.transform.InverseTransformPoint(playerPosition);
            // center（中心）：使用位置：作为 Mathf.Abs(float) 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
Vector3 center = ramp.center;
            // halfSize（一半尺寸）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
Vector3 halfSize = ramp.size * 0.5f;
            // scale（缩放）：保存 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = ResolveScale(ramp.transform);
            // localRadius（局部半径）：使用位置：作为 Mathf.Max(float, float) 的参数。
float localRadius = radius / Mathf.Max(0.001f, Mathf.Abs(scale.x));
            // lateralLimit（侧向限制）：使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float lateralLimit = halfSize.x + Mathf.Max(0.24f, localRadius);
            // lengthLimit（长度限制）：使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float lengthLimit = halfSize.z + playerEntryPadding;
            // verticalOffset（竖直偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float verticalOffset = local.y - center.y;
            if (Mathf.Abs(local.x - center.x) > lateralLimit || Mathf.Abs(local.z - center.z) > lengthLimit || verticalOffset < -playerVerticalLowerPadding || verticalOffset > playerVerticalUpperPadding)
            {
                continue;
            }

            // overflowZ（溢出深度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float overflowZ = Mathf.Max(0f, Mathf.Abs(local.z - center.z) - halfSize.z);
            // score（分值）：使用位置：LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)。
float score = Mathf.Abs(local.x - center.x) + overflowZ * 0.5f + Mathf.Abs(verticalOffset) * 0.05f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestRamp = ramp;
            bestLocalPosition = local;
        }

        return bestRamp != null;
    }

    /// <summary>ResolveCapsuleRadius 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="capsule">中文含义：胶囊。使用位置：LeonPlayerStairRampRuntime.ResolveCapsuleRadius(CapsuleCollider)。</param>
    private static float ResolveCapsuleRadius(CapsuleCollider capsule)
    {
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = capsule.transform.lossyScale;
        switch (capsule.direction)
        {
            case 0:
                return capsule.radius * Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            case 2:
                return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            default:
                return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }
    }

    internal bool TryReadFootTread(Vector3 foot, float reach, out float height)
    {
        height = 0f;
        float best = reach;
        bool found = false;
        foreach (Collider collider in rampColliders)
        {
            if (!(collider is BoxCollider ramp) || !ramp.enabled || !ramp.gameObject.activeInHierarchy)
            {
                continue;
            }
            Vector3 local = ramp.transform.InverseTransformPoint(foot);
            Vector3 offset = local - ramp.center;
            if (Mathf.Abs(offset.x) > ramp.size.x * 0.5f || Mathf.Abs(offset.z) > ramp.size.z * 0.5f)
            {
                continue;
            }
            float tread = ResolveSteppedSurfaceY(ramp, local);
            float difference = Mathf.Abs(foot.y - tread);
            if (difference <= best)
            {
                found = true;
                best = difference;
                height = tread;
            }
        }
        return found;
    }

    /// <summary>ResolveSteppedSurfaceY 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ResolveStepCount(float, float)、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Clamp(int, int, int)、Mathf.FloorToInt(float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） LeonPlayerStairRampRuntime.ResolveStepCount(float, float)、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)、Mathf.Clamp(int, int, int)、Mathf.FloorToInt(float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float) 的参数、LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)。</param>
    /// <param name="localPosition">中文含义：局部位置。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。</param>
    private static float ResolveSteppedSurfaceY(BoxCollider ramp, Vector3 localPosition)
    {
        if (!TryResolveRampEnds(ramp, out Vector3 /* 中文含义：低点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lowPoint, out Vector3 /* 中文含义：高点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ highPoint, out float /* 中文含义：低局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lowLocalZ, out float /* 中文含义：高局部深度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ highLocalZ))
        {
            // center（中心）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 center = ramp.center;
            // halfSize（一半尺寸）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 halfSize = ramp.size * 0.5f;
            // fallback（后备）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)。
Vector3 fallback = ramp.transform.TransformPoint(new Vector3(localPosition.x, center.y + halfSize.y, localPosition.z));
            return fallback.y;
        }

        // stepCount（步进数量）：保存 LeonPlayerStairRampRuntime.ResolveStepCount(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.FloorToInt(float) 的参数、作为 Mathf.Max(float, float) 的参数。
int stepCount = ResolveStepCount(lowPoint.y, highPoint.y);
        // progress（进度）：保存 Mathf.InverseLerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.FloorToInt(float) 的参数。
float progress = Mathf.InverseLerp(lowLocalZ, highLocalZ, localPosition.z);
        // stepIndex（步进索引）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
int stepIndex = Mathf.Clamp(Mathf.FloorToInt(progress * stepCount + 0.0001f), 0, stepCount);
        return Mathf.Lerp(lowPoint.y, highPoint.y, stepIndex / Mathf.Max(1f, stepCount));
    }

    /// <summary>TryResolveRampEnds 的业务处理入口；本方法直接依赖 Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.TryResolveStairInput(BoxCollider, Vector3, out float, out bool)；下游（含内部回调体） Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。</param>
    /// <param name="lowPoint">中文含义：低点。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。</param>
    /// <param name="highPoint">中文含义：高点。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。</param>
    /// <param name="lowLocalZ">中文含义：低局部深度。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。</param>
    /// <param name="highLocalZ">中文含义：高局部深度。输出参数，由本入口写入并交给调用方。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。</param>
    private static bool TryResolveRampEnds(BoxCollider ramp, out Vector3 lowPoint, out Vector3 highPoint, out float lowLocalZ, out float highLocalZ)
    {
        lowPoint = Vector3.zero;
        highPoint = Vector3.zero;
        lowLocalZ = 0f;
        highLocalZ = 0f;
        if (ramp == null)
        {
            return false;
        }

        // center（中心）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
Vector3 center = ramp.center;
        // halfSize（一半尺寸）：使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
Vector3 halfSize = ramp.size * 0.5f;
        // surfaceLocalY（表面局部纵向）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float surfaceLocalY = center.y + halfSize.y;
        // negativeZ（负值深度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
float negativeZ = center.z - halfSize.z;
        // positiveZ（正向深度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
float positiveZ = center.z + halfSize.z;
        // negativePoint（负值点）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
Vector3 negativePoint = ramp.transform.TransformPoint(new Vector3(center.x, surfaceLocalY, negativeZ));
        // positivePoint（正向点）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonPlayerStairRampRuntime.TryResolveRampEnds(BoxCollider, out Vector3, out Vector3, out float, out float)。
Vector3 positivePoint = ramp.transform.TransformPoint(new Vector3(center.x, surfaceLocalY, positiveZ));
        if (negativePoint.y <= positivePoint.y)
        {
            lowPoint = negativePoint;
            highPoint = positivePoint;
            lowLocalZ = negativeZ;
            highLocalZ = positiveZ;
        }
        else
        {
            lowPoint = positivePoint;
            highPoint = negativePoint;
            lowLocalZ = positiveZ;
            highLocalZ = negativeZ;
        }

        return true;
    }

    /// <summary>ResolveStepCount 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Clamp(int, int, int)、Mathf.RoundToInt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)、LeonPlayerStairRampRuntime.ResolveSteppedSurfaceY(BoxCollider, Vector3)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp(int, int, int)、Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="lowY">中文含义：低纵向。使用位置：作为 Mathf.Abs(float) 的参数。</param>
    /// <param name="highY">中文含义：高纵向。使用位置：作为 Mathf.Abs(float) 的参数。</param>
    private static int ResolveStepCount(float lowY, float highY)
    {
        // rise（抬升）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.RoundToInt(float) 的参数、LeonPlayerStairRampRuntime.ResolveStepCount(float, float)。
float rise = Mathf.Abs(highY - lowY);
        if (rise <= 0.001f)
        {
            return 1;
        }

        return Mathf.Clamp(Mathf.RoundToInt(rise / preferredStepHeight), 1, 24);
    }

    /// <summary>ResolveWorldWidth 的业务处理入口；本方法直接依赖 LeonPlayerStairRampRuntime.ResolveScale(Transform)、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.RefreshStepSupports(GameObject, BoxCollider)；下游（含内部回调体） LeonPlayerStairRampRuntime.ResolveScale(Transform)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ramp">中文含义：斜坡。使用位置：作为 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的参数、作为 Mathf.Max(float, float) 的参数、LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider)。</param>
    private static float ResolveWorldWidth(BoxCollider ramp)
    {
        if (ramp == null)
        {
            return 1f;
        }

        // scale（缩放）：保存 LeonPlayerStairRampRuntime.ResolveScale(Transform) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = ResolveScale(ramp.transform);
        return Mathf.Max(0.3f, ramp.size.x * Mathf.Abs(scale.x));
    }

    /// <summary>ResolveCapsuleBottomOffset 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="capsule">中文含义：胶囊。使用位置：LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider)。</param>
    private static float ResolveCapsuleBottomOffset(CapsuleCollider capsule)
    {
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数、LeonPlayerStairRampRuntime.ResolveCapsuleBottomOffset(CapsuleCollider)。
Vector3 scale = capsule.transform.lossyScale;
        switch (capsule.direction)
        {
            case 0:
                return capsule.center.x * scale.x - capsule.height * Mathf.Abs(scale.x) * 0.5f;
            case 2:
                return capsule.center.z * scale.z - capsule.height * Mathf.Abs(scale.z) * 0.5f;
            default:
                return capsule.center.y * scale.y - capsule.height * Mathf.Abs(scale.y) * 0.5f;
        }
    }

    /// <summary>ResolveScale 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.ConfigureSideGuard(GameObject, BoxCollider, string, float)、LeonPlayerStairRampRuntime.ResolveHighExitUpAssistScale(BoxCollider, Vector3)、LeonPlayerStairRampRuntime.ResolveWorldWidth(BoxCollider)、LeonPlayerStairRampRuntime.TryFindPlayerRamp(Vector3, CapsuleCollider, out BoxCollider, out Vector3)；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标变换节点。使用位置：LeonPlayerStairRampRuntime.ResolveScale(Transform)。</param>
    private static Vector3 ResolveScale(Transform target)
    {
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数、LeonPlayerStairRampRuntime.ResolveScale(Transform)。
Vector3 scale = target.lossyScale;
        if (Mathf.Abs(scale.x) <= 0.001f)
        {
            scale.x = 1f;
        }

        if (Mathf.Abs(scale.y) <= 0.001f)
        {
            scale.y = 1f;
        }

        if (Mathf.Abs(scale.z) <= 0.001f)
        {
            scale.z = 1f;
        }

        return scale;
    }

    /// <summary>玩家控制、动作与运动模块 RampSource；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [System.Serializable]
    public sealed class RampSource
    {
        [Tooltip("踏面中心与朝向。前轴沿上行方向，上轴垂直于踏面。")]
        public Transform surface;
        [Tooltip("场景踏面的宽度和长度，单位为米。")]
        public Vector2 surfaceSize;
        /// <summary>中文含义：桥面。使用位置：LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)。</summary>
        /// <summary>中文含义：斜坡名称。使用位置：作为 HashSet&lt;string&gt;.Add(string) 的参数、作为 LeonPlayerStairRampRuntime.FindOrCreateChild(string) 的参数、作为 string.IsNullOrWhiteSpace(string) 的参数 等共 4 处。</summary>
        public string rampName;
        /// <summary>中文含义：最小宽度。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)。</summary>
        public float minimumWidth;
        /// <summary>中文含义：宽度留白。使用位置：LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)。</summary>
        public float widthPadding;
        /// <summary>中文含义：长度留白。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)。</summary>
        public float lengthPadding;
        /// <summary>中文含义：表面抬升。使用位置：LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource)、LeonPlayerStairRampRuntime.ConfigureFromSource(GameObject, BoxCollider, BoxCollider, LeonPlayerStairRampRuntime.RampSource)。</summary>
        public float surfaceLift;
        /// <summary>中文含义：厚度。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonPlayerStairRampRuntime.ConfigurationError()、LeonPlayerStairRampRuntime.ConfigureFromFallback(GameObject, BoxCollider, LeonPlayerStairRampRuntime.RampSource) 等共 4 处。</summary>
        public float thickness;
        /// <summary>中文含义：是否创建的侧边防守集合。使用位置：LeonPlayerStairRampRuntime.RefreshRamp(LeonPlayerStairRampRuntime.RampSource)。</summary>
        public bool createsSideGuards;
    }
}
