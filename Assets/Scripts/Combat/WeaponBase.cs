using UnityEngine;

/// <summary>
/// 抽象武器开火动画触发接口，由武器模型的视觉组件实现（如 SimpleShoot）。
/// WeaponBase 在确认开火成功后通过此接口通知视觉层播放 Fire 动画。
/// </summary>
public interface IWeaponFireAnimation
{
    /// <summary>TriggerFireAnimation 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void TriggerFireAnimation();
}

/// <summary>
/// 武器视觉动画可选提供开火节奏，用于泵动散弹枪这类需要等待枪械动画结束的武器。
/// </summary>
public interface IWeaponFireCadenceProvider
{
    /// <summary>中文含义：开火间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    float FireInterval { get; }
}

/// <summary>武器、命中与战斗组件 WeaponBase，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>枪械射线与散弹命中的引擎宿主，弹匣、装填和冷却沿 WeaponRuntimeModule 提交，命中经 EnemyHitbox 与 DamageResolutionModule 结算，再发送表现事实。</remarks>
public partial class WeaponBase : MonoBehaviour, ICrosshairDataProvider
{
    /// <summary>中文含义：当前武器配置。使用位置：作为 AudioManager.PlaySFX3D(AudioClip, Vector3) 的参数、作为 CombatEventDispatcher.PublishWeaponFired(Vector3, float) 的参数、作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数 等共 47 处。</summary>
    public WeaponData data;
    /// <summary>中文含义：视觉表现模型。使用位置：WeaponBase.VisualModel.get。</summary>
    [Header("视觉模型引用（Inspector 绑定）")]
    [SerializeField]
    private GameObject visualModel;
    /// <summary>中文含义：武器音频。使用位置：WeaponBase.FireInternal(Vector3, Vector3)、WeaponBase.Initialize(LeonController)、WeaponBase.StartReload() 等共 4 处。</summary>
    [SerializeField]
    private WeaponAudio weaponAudio;
    /// <summary>中文含义：视觉表现开火。使用位置：WeaponBase.Initialize(LeonController)。</summary>
    [SerializeField]
    private MonoBehaviour visualFire;
    /// <summary>中文含义：枪口点。使用位置：WeaponBase.FireAt(Vector3)。</summary>
    [Header("枪口")]
    public Transform muzzlePoint;
    /// <summary>中文含义：射击遮罩。使用位置：WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    [Header("命中检测")]
    public LayerMask shotMask;
    /// <summary>中文含义：当前弹匣弹量。使用位置：作为 Action&lt;WeaponType, int, int&gt;.Invoke(WeaponType, int, int) 的参数、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.CanReload() 等共 9 处。</summary>
    public int currentAmmo => runtimeModule.Query(GetInstanceID()).magazine;
    /// <summary>中文含义：是否正在换弹。使用位置：LeonWeaponHandler.StartReload()、WeaponBase.CanReload()。</summary>
    public bool isReloading => runtimeModule.Query(GetInstanceID()).isReload;

    /// <summary>中文含义：运行时模块。使用位置：WeaponBase.BeginReload(int)、WeaponBase.CancelReload()、WeaponBase.CanFire() 等共 8 处。</summary>
    private WeaponRuntimeModule runtimeModule = new WeaponRuntimeModule();
    /// <summary>中文含义：兼容换弹事务。使用位置：作为 WeaponBase.BeginReload(int) 的参数、WeaponBase.Reload()。</summary>
    private int compatibleReloadTransaction;
    /// <summary>中文含义：待处理完成兼容事务。使用位置：作为 WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;) 的参数、WeaponBase.CancelReload()、WeaponBase.FinishReload() 等共 4 处。</summary>
    private int pendingCompleteCompatibleTransaction = -1;
    /// <summary>中文含义：射击编号。使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    private long shootId;
    /// <summary>中文含义：武器租用版本。使用位置：作为 ObjectIdentity.ObjectIdentity(int, long) 的参数、WeaponBase.Initialize(LeonController)、WeaponBase.OnEnable()。</summary>
    private long weaponLeaseVersion;
    /// <summary>中文含义：命中序号。使用位置：作为 DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。</summary>
    private int hitSequence;
    // 暴击判定使用独立随机源，避免改变既有弹道散布和敌人复起的随机序列。
    private static readonly System.Random pistolCriticalRandom = new System.Random();
    private readonly System.Collections.Generic.List<(VillagerController target, Vector3 position, Vector3 direction, HitboxType part, int index)> pelletBlood
        = new System.Collections.Generic.List<(VillagerController, Vector3, Vector3, HitboxType, int)>(6);
    private Vector3 pelletDirection;
    private readonly System.Collections.Generic.Dictionary<VillagerController, ShotgunTerminalHit> terminalHits
        = new System.Collections.Generic.Dictionary<VillagerController, ShotgunTerminalHit>();
    private readonly System.Collections.Generic.Dictionary<VillagerController, (Vector3 position, bool head)> pelletImpacts
        = new System.Collections.Generic.Dictionary<VillagerController, (Vector3 position, bool head)>();
    /// <summary>中文含义：里昂玩家控制器。使用位置：WeaponBase.FireAt(Vector3)、WeaponBase.FireInternal(Vector3, Vector3)、WeaponBase.Initialize(LeonController)。</summary>
    protected LeonController controller;
    /// <summary>中文含义：当前散布。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 6 处。</summary>
    protected float currentSpread;
    /// <summary>中文含义：武器音频引用。缓存 Inspector 绑定的 WeaponAudio</summary>
    public WeaponAudio WeaponAudioRef => weaponAudio;
    /// <summary>中文含义：视觉表现模型。Inspector 绑定的视觉模型</summary>
    public GameObject VisualModel => visualModel;

    /// <summary>中文含义：视觉表现开火。缓存 Inspector 绑定的开火动画控制器</summary>
    private IWeaponFireAnimation _visualFire;
    /// <summary>中文含义：视觉表现开火射击节奏。使用位置：作为 Mathf.Max(float, float) 的参数、WeaponBase.GetFireInterval()、WeaponBase.Initialize(LeonController)。</summary>
    private IWeaponFireCadenceProvider visualFireCadence;
    /// <summary>中文含义：命中通知回调。旧武器查询接口的局部命中通知，保留原基础伤害语义。新的受击表现订阅已结算命中。</summary>
    public event System.Action<HitInfo> OnHit;
    /// <summary>中文含义：已开火时的通知回调。成功开火事件 — 用于驱动本地准心开火扩张等即时反馈</summary>
    public event System.Action OnFired;
    /// <summary>中文含义：默认射击图层遮罩。使用位置：WeaponBase.AddLayerToMask(string)、WeaponBase.GetDefaultShotMask()。</summary>
    private static int defaultShotLayerMask = -1;
    /// <summary>中文含义：是否缺失的枪口已警告。使用位置：WeaponBase.FireAt(Vector3)。</summary>
    private bool missingMuzzleWarned;
    /// <summary>GetDefaultShotMask 的业务处理入口；本方法直接依赖 WeaponBase.AddLayerToMask(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） WeaponBase.AddLayerToMask(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static int GetDefaultShotMask()
    {
        if (defaultShotLayerMask < 0)
        {
            defaultShotLayerMask = 1 << 0;
            AddLayerToMask("Enemy");
            AddLayerToMask("EnemyHitbox");
        }

        return defaultShotLayerMask;
    }

    /// <summary>AddLayerToMask 的业务处理入口；本方法直接依赖 LayerMask.NameToLayer(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.GetDefaultShotMask()；下游（含内部回调体） LayerMask.NameToLayer(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="layerName">中文含义：图层名称。使用位置：作为 LayerMask.NameToLayer(string) 的参数。</param>
    private static void AddLayerToMask(string layerName)
    {
        // layer（图层）：保存 LayerMask.NameToLayer(string) 的返回结果，供当前步骤使用。使用位置：WeaponBase.AddLayerToMask(string)。
int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            defaultShotLayerMask |= 1 << layer;
        }
    }

    /// <summary>Initialize 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、Debug.LogWarning(object, Object)、Object.GetInstanceID()、Resources.Load&lt;WeaponData&gt;(string)、WeaponRuntimeModule.InitializeWeapon(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.Start()、PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、Debug.LogWarning(object, Object)、Object.GetInstanceID()、Resources.Load&lt;WeaponData&gt;(string)、WeaponRuntimeModule.InitializeWeapon(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctrl">中文含义：角色控制器。使用位置：WeaponBase.Initialize(LeonController)。</param>
    public virtual void Initialize(LeonController ctrl)
    {
        controller = ctrl;
        if (weaponLeaseVersion <= 0)
        {
            weaponLeaseVersion = 1;
        }

        if (data == null)
        {
            data = Resources.Load<WeaponData>("Weapon");
        }

        if (ctrl != null)
        {
            // holder（持有者）：保存 Component.GetComponent<LeonWeaponHandler>() 的返回结果，供当前步骤使用。使用位置：WeaponBase.Initialize(LeonController)。
var holder = ctrl.GetComponent<LeonWeaponHandler>();
            if (holder != null)
            {
                runtimeModule = holder.RuntimeModule;
            }
        }

        // callReceiver3（调用前缓存的运行时模块）：从运行时模块读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：WeaponBase.Initialize(LeonController)。
global::WeaponRuntimeModule callReceiver3 = runtimeModule;
        // argumentValue2（按原顺序缓存的实参整数计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 WeaponRuntimeModule.InitializeWeapon(int, int) 的参数。
int argumentValue2 = GetInstanceID();
        // selectedValue1（分支选出的数据的弹匣尺寸）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 WeaponRuntimeModule.InitializeWeapon(int, int) 的参数、WeaponBase.Initialize(LeonController)。
int selectedValue1;
        if (data != null)
        {
            selectedValue1 = data.magazineSize;
        }
        else
        {
            selectedValue1 = 0;
        }

        callReceiver3.InitializeWeapon(argumentValue2, selectedValue1);
        if (visualFire != null)
        {
            _visualFire = visualFire as IWeaponFireAnimation;
            visualFireCadence = visualFire as IWeaponFireCadenceProvider;
        }

        if (_visualFire == null && visualFire != null)
        {
            Debug.LogWarning($"[{name}] visualFire 未实现 IWeaponFireAnimation", this);
        }

        if (weaponAudio == null)
        {
            Debug.LogWarning($"[{name}] weaponAudio 未绑定", this);
        }
    }

    /// <summary>CanFire 的业务处理入口；本方法直接依赖 Object.GetInstanceID()、WeaponBase.GetFireInterval()、WeaponRuntimeModule.CheckFire(int, bool, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.Update()、WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） Object.GetInstanceID()、WeaponBase.GetFireInterval()、WeaponRuntimeModule.CheckFire(int, bool, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public virtual bool CanFire()
    {
        if (data == null)
        {
            return false;
        }

        return runtimeModule.CheckFire(GetInstanceID(), data.weaponType == WeaponType.Knife, Time.time, GetFireInterval()) == GameArchitecture.RejectionReason.None;
    }

    /// <summary>GetFireInterval 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.GetShootLockDuration(WeaponBase)、WeaponBase.CanFire()、WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetFireInterval()
    {
        // interval（间隔）：使用位置：作为 Mathf.Max(float, float) 的参数、WeaponBase.GetFireInterval()。
float interval;
        if (data != null)
        {
            interval = data.fireRate;
        }
        else
        {
            interval = 0f;
        }

        if (visualFireCadence != null)
        {
            interval = Mathf.Max(interval, visualFireCadence.FireInterval);
        }

        return Mathf.Max(0f, interval);
    }

    /// <summary>Fire 的业务处理入口；本方法直接依赖 WeaponBase.FireInternal(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） WeaponBase.FireInternal(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数。</param>
    public virtual bool Fire(Vector3 origin, Vector3 direction)
    {
        return FireInternal(origin, direction);
    }

    /// <summary>FireAt 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、WeaponBase.FireInternal(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonWeaponHandler.Fire()；下游（含内部回调体） Debug.LogWarning(object, Object)、WeaponBase.FireInternal(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="aimPoint">中文含义：瞄准点。使用位置：WeaponBase.FireAt(Vector3)。</param>
    public virtual bool FireAt(Vector3 aimPoint)
    {
        if (data != null && data.weaponType == WeaponType.Knife)
        {
            // meleeOrigin（近战原点）：使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数、WeaponBase.FireAt(Vector3)。
Vector3 meleeOrigin;
            if (controller != null)
            {
                meleeOrigin = controller.transform.position + Vector3.up;
            }
            else
            {
                meleeOrigin = transform.position;
            }

            // meleeDirection（近战方向）：使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数、WeaponBase.FireAt(Vector3)。
Vector3 meleeDirection;
            if (controller != null)
            {
                meleeDirection = controller.transform.forward;
            }
            else
            {
                meleeDirection = transform.forward;
            }

            return FireInternal(meleeOrigin, meleeDirection);
        }

        // originTransform（原点变换）：使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数、WeaponBase.FireAt(Vector3)。
Transform originTransform;
        if (muzzlePoint != null)
        {
            originTransform = muzzlePoint;
        }
        else
        {
            originTransform = transform;
        }

        if (muzzlePoint == null && !missingMuzzleWarned)
        {
            Debug.LogWarning($"[{name}] 未绑定 MuzzlePoint，暂时使用武器根节点。", this);
            missingMuzzleWarned = true;
        }

        // direction（方向）：使用位置：作为 WeaponBase.FireInternal(Vector3, Vector3) 的参数、WeaponBase.FireAt(Vector3)。
        Vector3 origin = ResolveMuzzleTraceOrigin(originTransform.position);
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = originTransform.forward;
        }

        return FireInternal(origin, direction.normalized);
    }

    private Vector3 ResolveMuzzleTraceOrigin(Vector3 muzzle)
    {
        if (controller == null)
        {
            return muzzle;
        }

        // 枪口穿入近身敌人或墙体时，从枪管进入表面之前发射射线。
        Vector3 anchor = controller.transform.position;
        anchor.y = muzzle.y;
        Vector3 barrel = muzzle - anchor;
        float length = barrel.magnitude;
        if (length <= 0.001f)
        {
            return muzzle;
        }

        int mask = shotMask.value;
        if (mask == 0)
        {
            mask = GetDefaultShotMask();
        }

        Vector3 direction = barrel / length;
        if (TryResolveRayHit(anchor, direction, length, mask, out RaycastHit obstruction))
        {
            return anchor + direction * Mathf.Max(0f, obstruction.distance - 0.02f);
        }

        return muzzle;
    }

    /// <summary>FireInternal 的业务处理入口；本方法直接依赖 Action.Invoke()、AudioManager.PlaySFX3D(AudioClip, Vector3)、CombatEventDispatcher.PublishWeaponFired(Vector3, float)、IWeaponFireAnimation.TriggerFireAnimation()、Mathf.Min(float, float)、Object.GetInstanceID()、object.ReferenceEquals(object, object) 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.Fire(Vector3, Vector3)、WeaponBase.FireAt(Vector3)；下游（含内部回调体） Action.Invoke()、AudioManager.PlaySFX3D(AudioClip, Vector3)、CombatEventDispatcher.PublishWeaponFired(Vector3, float)、IWeaponFireAnimation.TriggerFireAnimation()、Mathf.Min(float, float)、Object.GetInstanceID()、object.ReferenceEquals(object, object) 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 CombatEventDispatcher.PublishWeaponFired(Vector3, float) 的参数、作为 WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit) 的参数、作为 WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 WeaponBase.ApplySpread(Vector3) 的参数、作为 WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。</param>
    bool FireInternal(Vector3 origin, Vector3 direction)
    {
        if (!CanFire())
        {
            return false;
        }

        if (runtimeModule.TryFire(GetInstanceID(), data.weaponType == WeaponType.Knife, Time.time, GetFireInterval()) != GameArchitecture.RejectionReason.None)
        {
            return false;
        }

        shootId++;
        hitSequence = 0;
        if (data.fireSound != null)
        {
            // receiverValue8（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::AudioManager receiverValue8 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                receiverValue8.PlaySFX3D(data.fireSound, transform.position);
            }
        }
        else
        {
            // receiverValue9（判空前缓存的武器音频）：从武器音频读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::WeaponAudio receiverValue9 = weaponAudio;
            if (!global::System.Object.ReferenceEquals(receiverValue9, null))
            {
                receiverValue9.PlayFire();
            }
        }

        // receiverValue10（判空前缓存的视觉表现开火）：从视觉表现开火读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::IWeaponFireAnimation receiverValue10 = _visualFire;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.TriggerFireAnimation();
        }

        // receiverValue11（判空前缓存的已开火时的通知回调）：从已开火时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::System.Action receiverValue11 = OnFired;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.Invoke();
        }

        if (data.weaponType != WeaponType.Knife)
        {
            PlayChapterOneShotEffects();
            CombatEventDispatcher.PublishWeaponFired(origin, data.gunshotHearingRadius);
        }

        // mask（遮罩）：使用位置：作为 WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit) 的参数、作为 WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
int mask;
        if (shotMask.value != 0)
        {
            mask = shotMask.value;
        }
        else
        {
            mask = GetDefaultShotMask();
        }

        // receiverValue13（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::LeonController receiverValue13 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            // receiverValue14（判空前缓存的接收者值13的摄像机控制器）：从接收者值13的摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.FireInternal(Vector3, Vector3)。
global::RE4CameraController receiverValue14 = receiverValue13.CameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                receiverValue14.AddWeaponRecoil(data.recoilAmount);
            }
        }

        if (data.weaponType == WeaponType.Knife)
        {
            // hitResolved（是否命中解算后的）：保存 WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit) 的返回结果，供当前步骤使用。使用位置：WeaponBase.FireInternal(Vector3, Vector3)。
bool hitResolved = TryResolveSphereHit(origin, 0.5f, direction, 2f, mask, out RaycastHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit);
            if (hitResolved)
            {
                ProcessHit(hit, data.baseDamage);
            }

            return hitResolved;
        }

        currentSpread = Mathf.Min(data.maxSpread, currentSpread + data.baseSpread);
        if (data.fireMode == FireMode.Pellet)
        {
            pelletImpacts.Clear();
            pelletBlood.Clear();
            terminalHits.Clear();
            Vector3 forward = direction.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < .0001f)
            {
                right = Vector3.right;
            }
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right);
            float rotation = Random.value * 2f * Mathf.PI;
            var reactionShots = new System.Collections.Generic.Dictionary<VillagerController, FirearmBodyTechniqueShot>();
            // i（当前遍历索引）：使用位置：WeaponBase.FireInternal(Vector3, Vector3)。
for (int i = 0; i < data.pelletCount; i++)
            {
                // a1（起点1）：使用位置：作为 Quaternion.Euler(float, float, float) 的参数。
                float sector = rotation + (i - 1) * (2f * Mathf.PI / Mathf.Max(1, data.pelletCount - 1));
                float radial = Random.Range(.62f, 1f);
                if (i == 0)
                {
                    radial = Random.Range(0f, .16f);
                }
                float spread = Mathf.Tan(data.pelletSpreadAngle * Mathf.Deg2Rad) * radial;
                Vector3 offset = right * Mathf.Cos(sector) + up * Mathf.Sin(sector);
                pelletDirection = (forward + offset * spread).normalized;
                if (TryResolveRayHit(origin, pelletDirection, data.range, mask, out RaycastHit hit))
                {
                    ProcessHit(hit, data.baseDamage / data.pelletCount, reactionShots);
                }
            }

            foreach (var entry in reactionShots)
            {
                if (entry.Key != null && entry.Key.combatModule != null)
                {
                    entry.Key.combatModule.CompleteBodyTechniqueShot(entry.Value);
                }
            }

            if (weaponAudio != null)
            {
                foreach (var impact in pelletImpacts.Values)
                {
                    weaponAudio.PlayBulletImpact(impact.head, impact.position);
                }
            }

            foreach (var terminal in terminalHits)
            {
                VillagerDismemberment.Present(terminal.Key, terminal.Value, origin);
            }
            terminalHits.Clear();
            PresentPelletBlood();
            pelletImpacts.Clear();
            pelletBlood.Clear();
            return true;
        }

        // spreadDir（散布方向）：保存 WeaponBase.ApplySpread(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit) 的参数。
Vector3 spreadDir = ApplySpread(direction);
        if (TryResolveRayHit(origin, spreadDir, data.range, mask, out RaycastHit /* 中文含义：命中2。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit2))
        {
            ProcessHit(hit2, data.baseDamage);
        }

        return true;
    }

    /// <summary>中文含义：命中查询集合。只读字段，初始化后不由普通写入口替换。使用位置：WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit)、WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit)。</summary>
    private readonly HitQueryBuffer hitQueries = new HitQueryBuffer();
    /// <summary>中文含义：命中距离比较器。只读字段，初始化后不由普通写入口替换。使用位置：作为 Array.Sort&lt;RaycastHit&gt;(RaycastHit[], int, int, IComparer&lt;RaycastHit&gt;) 的参数。</summary>
    private static readonly System.Collections.Generic.IComparer<RaycastHit> hitDistanceComparer = System.Collections.Generic.Comparer<RaycastHit>.Create(( /* 中文含义：第一个射线命中结果。 */first, /* 中文含义：第二个射线命中结果。 */ second) => first.distance.CompareTo(second.distance));
    /// <summary>TryResolveRayHit 的业务处理入口；本方法直接依赖 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="mask">中文含义：遮罩。使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="resolvedHit">中文含义：解算后的命中。输出参数，由本入口写入并交给调用方。使用位置：作为 WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit) 的参数。</param>
    private bool TryResolveRayHit(Vector3 origin, Vector3 direction, float distance, int mask, out RaycastHit resolvedHit)
    {
        // hits（命中结果）：保存 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit) 的参数。
RaycastHit[] hits = hitQueries.Raycast(origin, direction, distance, mask, QueryTriggerInteraction.Collide, out int /* 中文含义：命中数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hitCount);
        return TrySelectCombatHit(hits, hitCount, out resolvedHit);
    }

    /// <summary>TryResolveSphereHit 的业务处理入口；本方法直接依赖 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int)、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int)、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="mask">中文含义：遮罩。使用位置：作为 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的参数。</param>
    /// <param name="resolvedHit">中文含义：解算后的命中。输出参数，由本入口写入并交给调用方。使用位置：作为 WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit) 的参数。</param>
    private bool TryResolveSphereHit(Vector3 origin, float radius, Vector3 direction, float distance, int mask, out RaycastHit resolvedHit)
    {
        // hits（命中结果）：保存 HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int) 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit) 的参数。
RaycastHit[] hits = hitQueries.SphereCast(origin, radius, direction, distance, mask, QueryTriggerInteraction.Collide, out int /* 中文含义：命中数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hitCount);
        return TrySelectCombatHit(hits, hitCount, out resolvedHit);
    }

    /// <summary>TrySelectCombatHit 的业务处理入口；本方法直接依赖 Array.Sort&lt;RaycastHit&gt;(RaycastHit[], int, int, IComparer&lt;RaycastHit&gt;)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit)、WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit)；下游（含内部回调体） Array.Sort&lt;RaycastHit&gt;(RaycastHit[], int, int, IComparer&lt;RaycastHit&gt;)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hits">中文含义：命中结果。使用位置：作为 Array.Sort&lt;RaycastHit&gt;(RaycastHit[], int, int, IComparer&lt;RaycastHit&gt;) 的参数、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)。</param>
    /// <param name="hitCount">中文含义：命中数量。使用位置：作为 Array.Sort&lt;RaycastHit&gt;(RaycastHit[], int, int, IComparer&lt;RaycastHit&gt;) 的参数、WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)。</param>
    /// <param name="resolvedHit">中文含义：解算后的命中。输出参数，由本入口写入并交给调用方。使用位置：WeaponBase.TrySelectCombatHit(RaycastHit[], int, out RaycastHit)。</param>
    internal static bool TrySelectCombatHit(RaycastHit[] hits, int hitCount, out RaycastHit resolvedHit)
    {
        resolvedHit = default;
        if (hits == null || hitCount == 0)
        {
            return false;
        }

        System.Array.Sort(hits, 0, hitCount, hitDistanceComparer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || candidate.GetComponentInParent<LeonController>() != null)
            {
                continue;
            }

            EnemyHitbox hitbox = candidate.GetComponent<EnemyHitbox>();
            if (hitbox != null)
            {
                if (hitbox.Owner == null || hitbox.Owner.IsDead)
                {
                    continue;
                }

                resolvedHit = hits[i];
                return true;
            }

            if (candidate.isTrigger)
            {
                continue;
            }

            // 角色移动用的包围体会超出身体，只有没有部位碰撞体的旧对象才需要兜底。
            VillagerController enemy = candidate.GetComponentInParent<VillagerController>();
            if (enemy != null && (enemy.IsDead || enemy.GetComponentInChildren<EnemyHitbox>() != null))
            {
                continue;
            }

            resolvedHit = hits[i];
            return true;
        }

        return false;
    }

    /// <summary>
    /// 处理命中：优先走 Bone Hitbox，回退到 IDamageable。
    /// </summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） Action&lt;HitInfo&gt;.Invoke(HitInfo)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponentInParent&lt;IDamageable&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3)、HitInfo.HitInfo(HitType, Vector3, float)、object.ReferenceEquals(object, object) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hit">中文含义：命中。使用位置：作为 DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3) 的参数、作为 HitInfo.HitInfo(HitType, Vector3, float) 的参数、作为 VillagerController.ResolveFallbackHitboxType(Vector3) 的参数 等共 5 处。</param>
    /// <param name="baseDamage">中文含义：基础伤害。使用位置：作为 DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3) 的参数、作为 HitInfo.HitInfo(HitType, Vector3, float) 的参数、作为 WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3) 的参数。</param>
    void ProcessHit(RaycastHit hit, float baseDamage,
        System.Collections.Generic.Dictionary<VillagerController, FirearmBodyTechniqueShot> reactionShots = null)
    {
        // 优先检查 EnemyHitbox（Bone Hitbox 系统）
        // hitbox（命中盒）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、WeaponBase.ProcessHit(RaycastHit, float)。
var hitbox = hit.collider.GetComponent<EnemyHitbox>();
        if (hitbox != null)
        {
            CommitEnemyDamage(hitbox.Owner, hitbox.hitboxType, baseDamage, hit.point, hit.normal, reactionShots, hasBodyPartHitbox: true);
            // receiverValue15（判空前缓存的命中通知回调）：从命中通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.ProcessHit(RaycastHit, float)。
global::System.Action<global::HitInfo> receiverValue15 = OnHit;
            if (!global::System.Object.ReferenceEquals(receiverValue15, null))
            {
                receiverValue15.Invoke(new HitInfo(HitType.Normal, hit.point, baseDamage));
            }

            return;
        }

        // villager（村民）：保存 Component.GetComponentInParent<VillagerController>() 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、WeaponBase.ProcessHit(RaycastHit, float)。
var villager = hit.collider.GetComponentInParent<VillagerController>();
        if (villager != null)
        {
            // resolvedType（解算后的类型）：保存 VillagerController.ResolveFallbackHitboxType(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3) 的参数。
HitboxType resolvedType = villager.ResolveFallbackHitboxType(hit.point);
            CommitEnemyDamage(villager, resolvedType, baseDamage, hit.point, hit.normal, reactionShots, hasBodyPartHitbox: false);
            // receiverValue16（判空前缓存的命中通知回调）：从命中通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.ProcessHit(RaycastHit, float)。
global::System.Action<global::HitInfo> receiverValue16 = OnHit;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.Invoke(new HitInfo(HitType.Normal, hit.point, baseDamage));
            }

            return;
        }

        // 回退：IDamageable（玩家或其他未使用 Hitbox 的对象）
        // d（当前索引可受伤对象）：保存 Component.GetComponentInParent<IDamageable>() 的返回结果，供当前步骤使用。使用位置：作为 DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3) 的参数、WeaponBase.ProcessHit(RaycastHit, float)。
var d = hit.collider.GetComponentInParent<IDamageable>();
        if (d == null)
        {
            PlayChapterOneSurfaceImpact(hit);
            return;
        }

        DamageResolutionModule.CommitNormalHit(d, baseDamage, hit.point);
        if (d is DestructibleWoodenBox box && !box.IsBroken && data.weaponType != WeaponType.Knife)
        {
            PlayChapterOneImpactBurst(hit);
        }
        OnHit?.Invoke(new HitInfo(HitType.Normal, hit.point, baseDamage));
    }

    /// <summary>CommitEnemyDamage 的业务处理入口；本方法直接依赖 ActionHandle.ActionHandle(ObjectIdentity, long)、DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3)、DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.ProcessHit(RaycastHit, float)；下游（含内部回调体） ActionHandle.ActionHandle(ObjectIdentity, long)、DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3)、DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标村民敌人控制器。使用位置：作为 DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、WeaponBase.CommitEnemyDamage(VillagerController, HitboxType, float, Vector3, Vector3)。</param>
    /// <param name="bodyPart">中文含义：身体部位。使用位置：作为 DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3) 的参数、作为 DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3) 的参数。</param>
    /// <param name="baseDamage">中文含义：基础伤害。使用位置：作为 DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3) 的参数。</param>
    /// <param name="normal">中文含义：表面法线。使用位置：作为 DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3) 的参数。</param>
    private void CommitEnemyDamage(VillagerController target, HitboxType bodyPart, float baseDamage, Vector3 position, Vector3 normal,
        System.Collections.Generic.Dictionary<VillagerController, FirearmBodyTechniqueShot> reactionShots, bool hasBodyPartHitbox)
    {
        if (target == null || target.actionCoordinator == null)
        {
            return;
        }

        float criticalMultiplier = 1f;
        bool pistolCritical = hasBodyPartHitbox && baseDamage > 0f
            && TryRollPistolCritical(target, bodyPart, out criticalMultiplier);
        float requestedDamage = baseDamage * criticalMultiplier;

        // identity（身份标识）：使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。
var identity = new GameArchitecture.ObjectIdentity(GetInstanceID(), weaponLeaseVersion);
        // context（上下文）：使用位置：作为 DamageRequest.DamageRequest(RequestContext, ActionHandle, int, float, int, Vector3) 的参数。
var context = new GameArchitecture.RequestContext("武器命中", identity, target.actionCoordinator.Identity, shootId);
        // request（请求）：使用位置：作为 DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3) 的参数。
        var request = new GameArchitecture.DamageRequest(context, new GameArchitecture.ActionHandle(identity, shootId),
            hitSequence++, requestedDamage, (int)bodyPart, position);
        bool hasImpactAudio = weaponAudio != null && weaponAudio.HasBulletImpact && data.weaponType != WeaponType.Knife;
        GameArchitecture.DamageResult result;
        if (data.weaponType == WeaponType.Knife)
        {
            result = DamageResolutionModule.Commit(request, target, bodyPart, normal, hasImpactAudio, ChapterOneEffect.SlashBlood);
        }
        else
        {
            FirearmBodyTechniqueShot shot = default;
            if (reactionShots != null)
            {
                reactionShots.TryGetValue(target, out shot);
            }

            shot.pistol = data.weaponType == WeaponType.Pistol;
            var blood = ChapterOneEffect.HandgunBlood;
            if (data.weaponType == WeaponType.Shotgun)
            {
                blood = ChapterOneEffect.ShotgunBlood;
                shot.shotgun = true;
            }
            else if (data.weaponType == WeaponType.Rifle)
            {
                blood = ChapterOneEffect.RifleBlood;
            }
            shot.sourcePosition = transform.position;
            bool deferredBlood = reactionShots != null;
            Vector3 bloodDirection = normal;
            if (blood == ChapterOneEffect.RifleBlood)
            {
                bloodDirection = (position - shot.sourcePosition).normalized;
            }
            result = DamageResolutionModule.CommitFirearm(request, target, bodyPart, normal, hasImpactAudio, ref shot, blood, deferredBlood, bloodDirection);
            if (deferredBlood && result.Committed && result.ActualDeductHealth > 0f)
            {
                RecordPelletBlood(target, position, normal, bodyPart);
                if (data.weaponType == WeaponType.Shotgun)
                {
                    terminalHits.TryGetValue(target, out var terminal);
                    terminal.Record(bodyPart, result.ActualDeductHealth, position, target.IsDead);
                    terminalHits[target] = terminal;
                }
            }
            if (shot.action.Valid && target != null && target.combatModule != null)
            {
                if (reactionShots == null)
                {
                    target.combatModule.CompleteBodyTechniqueShot(shot);
                }
                else
                {
                    reactionShots[target] = shot;
                }
            }
        }
        if (pistolCritical && result.Committed && result.ActualDeductHealth > 0f && result.CurrentLethal && target != null && target.IsDead)
        {
            VillagerDismemberment.PresentPistolCritical(target, bodyPart, position, transform.position);
        }

        if (!hasImpactAudio || !result.Committed)
        {
            return;
        }

        bool head = bodyPart == HitboxType.Head;
        if (data.fireMode == FireMode.Pellet)
        {
            bool alreadyHit = pelletImpacts.TryGetValue(target, out var previous);
            if ((alreadyHit && head && !previous.head) || (!alreadyHit && result.ActualDeductHealth > 0f))
            {
                pelletImpacts[target] = (position, head);
            }
        }
        else if (result.ActualDeductHealth > 0f)
        {
            weaponAudio.PlayBulletImpact(head, position);
        }
    }

    private bool TryRollPistolCritical(VillagerController target, HitboxType bodyPart, out float damageMultiplier)
    {
        damageMultiplier = 1f;
        if (data.weaponType != WeaponType.Pistol || data.fireMode != FireMode.Single
            || target.IsDead || target.missionEnemyType != MissionEnemyType.Ganado)
        {
            return false;
        }

        float chance;
        float configuredMultiplier;
        if (bodyPart == HitboxType.Head)
        {
            chance = data.headCriticalChance;
            configuredMultiplier = data.headCriticalDamageMultiplier;
        }
        else if (bodyPart == HitboxType.LeftArm || bodyPart == HitboxType.RightArm)
        {
            chance = data.armCriticalChance;
            configuredMultiplier = data.armCriticalDamageMultiplier;
        }
        else
        {
            return false;
        }

        if (chance > 0f && pistolCriticalRandom.NextDouble() < chance)
        {
            damageMultiplier = Mathf.Max(1f, configuredMultiplier);
            return true;
        }

        return false;
    }

    private void RecordPelletBlood(VillagerController target, Vector3 position, Vector3 normal, HitboxType part)
    {
        int index = 0;
        foreach (var sample in pelletBlood)
        {
            if (sample.target != target)
            {
                continue;
            }
            if ((sample.position - position).sqrMagnitude < .0225f)
            {
                return;
            }
            index++;
        }
        if (index < 2)
        {
            Vector3 emission = (normal - pelletDirection * .45f).normalized;
            pelletBlood.Add((target, position, emission, part, index));
        }
    }

    private void PresentPelletBlood()
    {
        var presenter = EnemyHitEffectManager.Instance;
        if (presenter == null)
        {
            return;
        }
        int presented = 0;
        // 优先覆盖不同目标，再补同一目标上较远的弹着。
        for (int sampleIndex = 0; sampleIndex < 2; sampleIndex++)
        {
            foreach (var sample in pelletBlood)
            {
                if (sample.index != sampleIndex || presented >= 5)
                {
                    continue;
                }
                presenter.PlayBloodEffect(sample.position, sample.direction, sample.part,
                    playImpactSound: !pelletImpacts.ContainsKey(sample.target) && sampleIndex == 0,
                    bloodEffect: ChapterOneEffect.ShotgunBlood);
                presented++;
            }
        }
    }

    /// <summary>ApplySpread 的业务处理入口；本方法直接依赖 Quaternion.Euler(float, float, float)、Random.Range(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） Quaternion.Euler(float, float, float)、Random.Range(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dir">中文含义：方向。使用位置：WeaponBase.ApplySpread(Vector3)。</param>
    protected virtual Vector3 ApplySpread(Vector3 dir)
    {
        // angle（角度）：使用位置：作为 Random.Range(float, float) 的参数。
float angle = currentSpread * Random.Range(-1f, 1f);
        return Quaternion.Euler(Random.Range(-angle, angle), Random.Range(-angle, angle), 0) * dir;
    }

    /// <summary>Reload 的业务处理入口；本方法直接依赖 MonoBehaviour.Invoke(string, float)、WeaponBase.BeginReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MonoBehaviour.Invoke(string, float)、WeaponBase.BeginReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public virtual void Reload()
    {
        if (!BeginReload(++compatibleReloadTransaction))
        {
            return;
        }

        pendingCompleteCompatibleTransaction = compatibleReloadTransaction;
        Invoke(nameof(FinishReload), data.reloadTime);
    }

    /// <summary>FinishReload 的业务处理入口；本方法直接依赖 WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.CompleteReload()；下游（含内部回调体） WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected virtual void FinishReload()
    {
        if (data == null)
        {
            return;
        }

        CommitReload(pendingCompleteCompatibleTransaction, data.magazineSize, false, null);
        pendingCompleteCompatibleTransaction = -1;
    }

    /// <summary>CompleteReload 的业务处理入口；本方法直接依赖 WeaponBase.FinishReload()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） WeaponBase.FinishReload()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteReload()
    {
        FinishReload();
    }

    /// <summary>StartReload 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、WeaponAudio.PlayReload()、WeaponBase.BeginReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、WeaponAudio.PlayReload()、WeaponBase.BeginReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StartReload()
    {
        if (BeginReload(++compatibleReloadTransaction))
        {
            // receiverValue18（判空前缓存的武器音频）：从武器音频读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WeaponBase.StartReload()。
global::WeaponAudio receiverValue18 = weaponAudio;
            if (!global::System.Object.ReferenceEquals(receiverValue18, null))
            {
                receiverValue18.PlayReload();
            }
        }
    }

    /// <summary>BeginReload 的业务处理入口；本方法直接依赖 Object.GetInstanceID()、WeaponRuntimeModule.BeginReload(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.StartReload()、WeaponBase.Reload()、WeaponBase.StartReload()；下游（含内部回调体） Object.GetInstanceID()、WeaponRuntimeModule.BeginReload(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：作为 WeaponRuntimeModule.BeginReload(int, int, int) 的参数。</param>
    public bool BeginReload(int transaction)
    {
        return data != null && runtimeModule.BeginReload(GetInstanceID(), data.magazineSize, transaction);
    }

    /// <summary>CommitReload 的业务处理入口；本方法直接依赖 Object.GetInstanceID()、WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.CompleteReload(int)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)、WeaponBase.FinishReload()；下游（含内部回调体） Object.GetInstanceID()、WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：作为 WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;) 的参数。</param>
    /// <param name="reserveAmmo">中文含义：背包备弹。使用位置：作为 WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;) 的参数。</param>
    /// <param name="singleShot">中文含义：是否单一射击。使用位置：作为 WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;) 的参数。</param>
    /// <param name="deductReserveAmmo">中文含义：扣除背包备弹。使用位置：作为 WeaponRuntimeModule.CommitReload(int, int, int, int, bool, Action&lt;int&gt;) 的参数。</param>
    public int CommitReload(int transaction, int reserveAmmo, bool singleShot, System.Action<int> deductReserveAmmo)
    {
        if (data == null)
        {
            return 0;
        }
        else
        {
            return runtimeModule.CommitReload(GetInstanceID(), data.magazineSize, transaction, reserveAmmo, singleShot, deductReserveAmmo);
        }
    }

    /// <summary>CancelReload 的业务处理入口；本方法直接依赖 MonoBehaviour.CancelInvoke(string)、Object.GetInstanceID()、WeaponRuntimeModule.CancelReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.CancelReload(WeaponType)、LeonWeaponHandler.OnDisable()、WeaponBase.OnDisable()；下游（含内部回调体） MonoBehaviour.CancelInvoke(string)、Object.GetInstanceID()、WeaponRuntimeModule.CancelReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelReload()
    {
        CancelInvoke(nameof(FinishReload));
        pendingCompleteCompatibleTransaction = -1;
        runtimeModule.CancelReload(GetInstanceID());
    }

    /// <summary>CanReload 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanReload()
    {
        return data != null && currentAmmo < data.magazineSize && !isReloading;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        weaponLeaseVersion++;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） WeaponBase.CancelReload()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        pendingMuzzleFlashes = 0;
        CancelChapterOnePumpCase();
        CancelReload();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (data == null)
        {
            return;
        }

        currentSpread = Mathf.Max(0, currentSpread - data.spreadRecovery * Time.deltaTime);
    }

    /// <summary>GetSpreadNormalized 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.UpdateSpread()；下游（含内部回调体） Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetSpreadNormalized()
    {
        if (data == null)
        {
            return 0f;
        }

        if (data.maxSpread <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(currentSpread / data.maxSpread);
    }

    /// <summary>ICrosshairDataProvider — 返回当前武器的准心配置</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public CrosshairConfig GetCrosshairConfig()
    {
        if (data != null)
        {
            return data.crosshairConfig;
        }
        else
        {
            return null;
        }
    }
}

/// <summary>武器、命中与战斗的 IDamageable 通信契约；消费者通过成员调用实现方，状态及资源仍由实现宿主负责。</summary>
public interface IDamageable
{
    /// <summary>TakeDamage 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。</param>
    void TakeDamage(float damage);
    /// <summary>TakeDamage 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitNormalHit(IDamageable, float, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。</param>
    /// <param name="hitPoint">中文含义：命中点位置。</param>
    void TakeDamage(float damage, Vector3 hitPoint);
}
