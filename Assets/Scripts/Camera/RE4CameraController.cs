using UnityEngine;
using Unity.Cinemachine;

/// <summary>正式场景摄像机统一入口。绑定玩家与 Cinemachine 3 镜头结构，配置 RE4RThirdPersonCamera 手感驱动及 AimResolver；武器切换事件选择配置，配对和事件镜头凭拥有者引用申请与释放接管，设置界面经此入口提交镜头参数。</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class RE4CameraController : MonoBehaviour
{
    /// <summary>中文含义：最小观察灵敏度。沿用手感驱动提供的灵敏度下限，设置消费者共用同一边界。</summary>
    public const float MinimumLookSensitivity = RE4RThirdPersonCamera.MinimumLookSensitivity;
    /// <summary>中文含义：最大观察灵敏度。沿用手感驱动提供的普通观察灵敏度上限。</summary>
    public const float MaximumLookSensitivity = RE4RThirdPersonCamera.MaximumLookSensitivity;
    /// <summary>中文含义：最大瞄准灵敏度。沿用手感驱动提供的瞄准灵敏度上限。</summary>
    public const float MaximumAimSensitivity = RE4RThirdPersonCamera.MaximumAimSensitivity;
    /// <summary>中文含义：玩家。当前绑定玩家，负责注册镜头入口并提供装备与自由环顾状态。</summary>
    [Header("玩家绑定")]
    public LeonController player;
    /// <summary>中文含义：摄像机锚点。当前玩家镜头锚点，绑定手感驱动时传入。</summary>
    public Transform cameraAnchor;
    /// <summary>中文含义：主摄像机。实际输出主相机，供手感同步与统一瞄准使用。</summary>
    [Header("主摄像机")]
    public Camera mainCamera;
    /// <summary>中文含义：镜头脑。主相机上的 CinemachineBrain，启动准备完成后启用。</summary>
    public CinemachineBrain brain;
    /// <summary>中文含义：玩法驱动器。第三人称手感驱动，承接构图、输入、碰撞、后坐及事件镜头归位。</summary>
    [Header("摄像机绑定")]
    public RE4RThirdPersonCamera gameplayDriver;
    /// <summary>中文含义：统一瞄准点解算器。本场景统一瞄准解算器，所有瞄准查询复用同一入口。</summary>
    public AimResolver aimResolver;
    /// <summary>中文含义：跟随目标。游戏镜头跟随目标，传给手感驱动管理。</summary>
    [Header("摄像机结构")]
    public Transform followTarget;
    /// <summary>中文含义：肩部目标。场景绑定的肩部参考节点，作为结构完整性检查的一部分。</summary>
    public Transform shoulderTarget;
    /// <summary>中文含义：世界空间瞄准目标。统一瞄准结果节点，同时登记给玩家。</summary>
    public Transform worldAimTarget;
    /// <summary>中文含义：玩法摄像机。正式玩法 Cinemachine 镜头，启动时设置玩法优先级。</summary>
    [Header("战斗摄像机")]
    public CinemachineCamera gameplayCamera;
    /// <summary>中文含义：第三人称跟随。Cinemachine 第三人称跟随组件，供手感驱动调整肩位和距离。</summary>
    public CinemachineThirdPersonFollow thirdPersonFollow;
    /// <summary>中文含义：水平转动竖直转动。Cinemachine 视角旋转组件，由手感驱动接入观察输入。</summary>
    public CinemachinePanTilt panTilt;
    /// <summary>中文含义：遮挡解除器。Cinemachine 遮挡避让组件，与当前检测层一起交给手感驱动。</summary>
    public CinemachineDeoccluder deoccluder;
    /// <summary>中文含义：已构建内部覆盖摄像机。内置外部镜头，LockCamera 设置其位姿后通过同一拥有者协议申请。</summary>
    [Header("外部镜头")]
    public CinemachineCamera builtInOverrideCamera;
    /// <summary>中文含义：玩法优先级。玩法镜头的既有优先级。</summary>
    public int gameplayPriority = 10;
    /// <summary>中文含义：外部优先级。获准接管的外部镜头优先级。</summary>
    public int externalPriority = 100;
    /// <summary>中文含义：手枪配置档。武器数据没有专用配置时的手枪回退配置，也用于未知类型默认值。</summary>
    [Header("配置回退")]
    public CameraProfile handgunProfile;
    /// <summary>中文含义：步枪配置档。步枪回退配置，霰弹枪未配置时也沿用它。</summary>
    public CameraProfile rifleProfile;
    /// <summary>中文含义：霰弹枪配置档。霰弹枪回退配置，允许缺失后继续回退到步枪。</summary>
    public CameraProfile shotgunProfile;
    /// <summary>中文含义：小刀配置档。小刀镜头回退配置。</summary>
    public CameraProfile knifeProfile;
    /// <summary>中文含义：摄像机碰撞遮罩。传给手感驱动的镜头碰撞检测层。</summary>
    [Header("检测层")]
    public LayerMask cameraCollisionMask;
    /// <summary>中文含义：瞄准遮罩。传给统一解算器的瞄准射线检测层。</summary>
    public LayerMask aimMask;
    /// <summary>中文含义：管理当前装备与武器切换的组件。当前绑定玩家的武器宿主，提供装备事件和各武器配置。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>中文含义：生效配置档。最近成功应用的武器镜头配置，后坐反馈读取它的倍率和恢复参数。</summary>
    private CameraProfile activeProfile;
    /// <summary>中文含义：外部拥有者。当前外部镜头的拥有者引用；其他拥有者不能覆盖或释放其接管。</summary>
    private Object externalOwner;
    /// <summary>中文含义：生效外部摄像机。当前获准接管的外部镜头，释放时降优先级并停用。</summary>
    private CinemachineCamera activeExternalCamera;
    /// <summary>中文含义：是否外部抑制的观察输入。本次外部接管是否抑制普通镜头观察输入。</summary>
    private bool externalSuppressesLookInput;
    /// <summary>中文含义：玩家。消费者只读查询当前玩家绑定。</summary>
    public LeonController Player => player;
    /// <summary>中文含义：摄像机锚点。消费者只读查询当前镜头锚点。</summary>
    public Transform CameraAnchor => cameraAnchor;
    /// <summary>中文含义：主摄像机。消费者只读查询实际输出相机。</summary>
    public Camera MainCamera => mainCamera;
    /// <summary>中文含义：世界空间瞄准目标。消费者只读查询统一瞄准目标。</summary>
    public Transform WorldAimTarget => worldAimTarget;
    /// <summary>中文含义：生效配置档。消费者只读查询最近应用的配置。</summary>
    public CameraProfile ActiveProfile => activeProfile;
    /// <summary>中文含义：是否具有外部摄像机。拥有者和外部镜头引用均有效时才表示仍有接管。</summary>
    public bool HasExternalCamera => externalOwner != null && activeExternalCamera != null;

    /// <summary>中文含义：水平观察灵敏度。设置界面读取的水平灵敏度，驱动不可用时返回既有默认值。</summary>
    public float HorizontalLookSensitivity
    {
        get
        {
            if (gameplayDriver != null)
            {
                return gameplayDriver.HorizontalLookSensitivity;
            }

            return RE4RThirdPersonCamera.DefaultHorizontalSensitivity;
        }
    }

    /// <summary>中文含义：竖直观察灵敏度。设置界面读取的垂直灵敏度，驱动不可用时返回既有默认值。</summary>
    public float VerticalLookSensitivity
    {
        get
        {
            if (gameplayDriver != null)
            {
                return gameplayDriver.VerticalLookSensitivity;
            }

            return RE4RThirdPersonCamera.DefaultVerticalSensitivity;
        }
    }

    /// <summary>中文含义：瞄准观察灵敏度。设置界面读取的瞄准倍率，驱动不可用时返回既有默认值。</summary>
    public float AimLookSensitivity
    {
        get
        {
            if (gameplayDriver != null)
            {
                return gameplayDriver.AimLookSensitivity;
            }

            return RE4RThirdPersonCamera.DefaultAimSensitivity;
        }
    }

    /// <summary>中文含义：是否配置完成。逐项检查启动及更新所需绑定，保持原短路顺序；缺失时不运行正式解算。</summary>
    public bool IsConfigured => player != null && cameraAnchor != null && mainCamera != null && brain != null && gameplayDriver != null && aimResolver != null && followTarget != null && shoulderTarget != null && worldAimTarget != null && gameplayCamera != null && thirdPersonFollow != null && panTilt != null && deoccluder != null;

    /// <summary>Unity 启动回调；验证结构后配置手感、解算与镜头优先级，向玩家登记并订阅武器切换，最后应用初始配置和瞄准点。</summary>
    void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError("[RE4CameraController] 场景引用不完整，摄像机系统停止运行。", this);
            enabled = false;
            return;
        }

        gameplayDriver.ConfigureSceneRig(player, cameraAnchor, mainCamera, gameplayCamera, thirdPersonFollow, panTilt, deoccluder, followTarget);
        gameplayDriver.SetCollisionMask(cameraCollisionMask);
        aimResolver.Configure(mainCamera, worldAimTarget);
        aimResolver.aimMask = aimMask;
        brain.enabled = true;
        gameplayCamera.Priority = gameplayPriority;
        gameplayCamera.enabled = true;
        if (builtInOverrideCamera != null)
        {
            builtInOverrideCamera.Priority = 0;
            builtInOverrideCamera.enabled = false;
        }

        player.RegisterCameraController(this, mainCamera, worldAimTarget);
        weaponHandler = player.GetComponent<LeonWeaponHandler>();
        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched += OnWeaponSwitched;
        }

        ApplyWeaponProfile(player.currentWeapon);
        aimResolver.Resolve(GetCurrentWeaponRange());
    }

    /// <summary>Unity 销毁回调，退订当前武器事件并从当前玩家注销镜头入口。</summary>
    void OnDestroy()
    {
        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched -= OnWeaponSwitched;
        }

        if (player != null)
        {
            player.UnregisterCameraController(this);
        }
    }

    /// <summary>Unity 帧末回调，结构有效时用当前武器射程更新统一瞄准结果。</summary>
    void LateUpdate()
    {
        if (!IsConfigured)
        {
            return;
        }

        aimResolver.Resolve(GetCurrentWeaponRange());
    }

    /// <summary>LeonWeaponHandler 的装备事件回调，将新武器种类交给同一配置应用入口。</summary>
    /// <param name="weaponType">中文含义：武器类型。已经提交的当前武器种类。</param>
    void OnWeaponSwitched(WeaponType weaponType)
    {
        ApplyWeaponProfile(weaponType);
    }

    /// <summary>玩家绑定链调用；先退订和注销旧对象，再绑定新玩家及锚点，配置驱动并登记新装备事件。</summary>
    /// <param name="playerController">中文含义：玩家控制器。新的玩家宿主，允许为空并沿用既有空绑定处理。</param>
    /// <param name="anchor">中文含义：当前变换节点。新玩家的镜头锚点。</param>
    public void BindPlayer(LeonController playerController, Transform anchor)
    {
        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched -= OnWeaponSwitched;
        }

        if (player != null)
        {
            player.UnregisterCameraController(this);
        }

        player = playerController;
        cameraAnchor = anchor;
        if (player != null)
        {
            weaponHandler = player.GetComponent<LeonWeaponHandler>();
        }
        else
        {
            weaponHandler = null;
        }

        if (gameplayDriver != null)
        {
            gameplayDriver.ConfigureSceneRig(player, cameraAnchor, mainCamera, gameplayCamera, thirdPersonFollow, panTilt, deoccluder, followTarget);
        }

        if (player != null)
        {
            player.RegisterCameraController(this, mainCamera, worldAimTarget);
            ApplyWeaponProfile(player.currentWeapon);
        }

        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched += OnWeaponSwitched;
        }
    }

    /// <summary>玩家退出链调用；只有当前绑定玩家可注销自身，随后清理武器订阅和玩家锚点引用。</summary>
    /// <param name="playerController">中文含义：玩家控制器。要求解除绑定的玩家，必须匹配当前玩家。</param>
    public void UnbindPlayer(LeonController playerController)
    {
        if (playerController == null || playerController != player)
        {
            return;
        }

        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched -= OnWeaponSwitched;
        }

        player.UnregisterCameraController(this);
        weaponHandler = null;
        player = null;
        cameraAnchor = null;
    }

    /// <summary>启动及装备切换共用；取得首个有效配置后保存并交给手感驱动，不改资源本身。</summary>
    /// <param name="weaponType">中文含义：武器类型。要应用镜头配置的武器种类。</param>
    public void ApplyWeaponProfile(WeaponType weaponType)
    {
        // 本次选择得到的配置，缺失时保持最近已应用配置不变。
        // 中文含义：profile 为“配置档”。
CameraProfile profile = GetWeaponProfile(weaponType);
        if (profile == null)
        {
            return;
        }

        activeProfile = profile;
        gameplayDriver.ApplyProfile(profile);
    }

    /// <summary>配置选择内部入口，优先使用匹配武器数据中的专用配置，再按种类读取场景回退配置。</summary>
    /// <param name="weaponType">中文含义：武器类型。请求的武器种类。</param>
    CameraProfile GetWeaponProfile(WeaponType weaponType)
    {
        if (weaponHandler != null)
        {
            // 武器宿主返回的目标实例，只有数据种类匹配且配置存在时优先使用。
            // 中文含义：weapon 为“当前武器组件”。
WeaponBase weapon = weaponHandler.GetWeapon(weaponType);
            if (weapon != null && weapon.data != null && weapon.data.weaponType == weaponType && weapon.data.cameraProfile != null)
            {
                return weapon.data.cameraProfile;
            }
        }

        if (weaponType == WeaponType.Pistol)
        {
            return handgunProfile;
        }
        else if (weaponType == WeaponType.Rifle)
        {
            return rifleProfile;
        }
        else if (weaponType == WeaponType.Shotgun)
        {
            if (shotgunProfile != null)
            {
                return shotgunProfile;
            }

            return rifleProfile;
        }
        else if (weaponType == WeaponType.Knife)
        {
            return knifeProfile;
        }

        return handgunProfile;
    }

    /// <summary>启动与帧末解算查询当前装备射程，缺武器数据时依次回退到解算器距离和既有常量。</summary>
    float GetCurrentWeaponRange()
    {
        // 当前装备的实例，宿主或玩家缺失时保持为空。
        // 中文含义：weapon 为“当前武器组件”。
WeaponBase weapon = null;
        if (weaponHandler != null && player != null)
        {
            weapon = weaponHandler.GetWeapon(player.currentWeapon);
        }

        if (weapon != null && weapon.data != null)
        {
            return weapon.data.range;
        }

        if (aimResolver != null)
        {
            return aimResolver.fallbackDistance;
        }

        return 100f;
    }

    /// <summary>武器等消费者查询瞄准点的统一入口，优先委托 AimResolver，缺解算器时沿用主相机前向延伸。</summary>
    /// <param name="maximumDistance">中文含义：最大距离。本次查询射程。</param>
    public Vector3 ResolveAimPoint(float maximumDistance)
    {
        if (aimResolver != null)
        {
            return aimResolver.Resolve(maximumDistance);
        }

        return mainCamera.transform.position + mainCamera.transform.forward * maximumDistance;
    }

    /// <summary>配对或事件镜头申请接管；拒绝不同拥有者，清自由环顾，更新镜头优先级并按请求抑制玩法观察。</summary>
    /// <param name="owner">中文含义：拥有者。本次接管拥有者，后续释放必须传回同一引用。</param>
    /// <param name="externalCamera">中文含义：外部摄像机。要启用的外部镜头。</param>
    /// <param name="suppressLookInput">中文含义：是否抑制观察输入。是否抑制玩法镜头观察输入。</param>
    public bool RequestExternalCamera(Object owner, CinemachineCamera externalCamera, bool suppressLookInput = true)
    {
        if (owner == null || externalCamera == null)
        {
            return false;
        }

        if (externalOwner != null && externalOwner != owner)
        {
            return false;
        }

        // 原条件访问的玩家接收者快照，存在时才重置自由环顾。
        // 中文含义：currentPlayer 为“当前玩家”。
LeonController currentPlayer = player;
        if (!ReferenceEquals(currentPlayer, null))
        {
            currentPlayer.ResetFreeView();
        }

        if (activeExternalCamera != null && activeExternalCamera != externalCamera)
        {
            activeExternalCamera.Priority = 0;
            activeExternalCamera.enabled = false;
        }

        externalOwner = owner;
        activeExternalCamera = externalCamera;
        externalSuppressesLookInput = suppressLookInput;
        externalCamera.enabled = true;
        externalCamera.Priority = externalPriority;
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetInputSuppressed(externalSuppressesLookInput);
        }

        return true;
    }

    /// <summary>配对或事件镜头结束时调用；只有当前拥有者可释放，先同步游戏镜头姿态，再停用外部镜头并恢复输入。</summary>
    /// <param name="owner">中文含义：拥有者。要求释放的拥有者引用。</param>
    public bool ReleaseExternalCamera(Object owner)
    {
        if (owner == null || externalOwner != owner)
        {
            return false;
        }

        // 事件镜头仍是主输出时先同步游戏镜头，避免停用后读取到旧姿态。
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SynchronizeFromCamera();
        }

        if (activeExternalCamera != null)
        {
            activeExternalCamera.Priority = 0;
            activeExternalCamera.enabled = false;
        }

        externalOwner = null;
        activeExternalCamera = null;
        externalSuppressesLookInput = false;
        // 同步姿态及清理接管后重新读取的驱动快照，保持第二次条件访问的求值时点。
        // 中文含义：inputDriver 为“输入驱动器”。
RE4RThirdPersonCamera inputDriver = gameplayDriver;
        if (!ReferenceEquals(inputDriver, null))
        {
            inputDriver.SetInputSuppressed(false);
        }

        return true;
    }

    /// <summary>内置锁定镜头入口；按传入位姿或节点设置内置镜头，再以本组件作为拥有者申请接管。</summary>
    /// <param name="position">中文含义：位置。锁定世界位置。</param>
    /// <param name="rotation">中文含义：旋转。锁定世界旋转。</param>
    /// <param name="fieldOfView">中文含义：视野角度。锁定镜头视野角，默认沿用原值。</param>
    public bool LockCamera(Vector3 position, Quaternion rotation, float fieldOfView = 55f)
    {
        if (builtInOverrideCamera == null)
        {
            return false;
        }

        builtInOverrideCamera.transform.SetPositionAndRotation(position, rotation);
        builtInOverrideCamera.Follow = null;
        builtInOverrideCamera.LookAt = null;
        builtInOverrideCamera.Lens.FieldOfView = fieldOfView;
        return RequestExternalCamera(this, builtInOverrideCamera, true);
    }

    /// <summary>内置锁定镜头入口；按传入位姿或节点设置内置镜头，再以本组件作为拥有者申请接管。</summary>
    /// <param name="lockTransform">中文含义：锁定变换。提供锁定位姿的节点，为空时拒绝。</param>
    /// <param name="fieldOfView">中文含义：视野角度。锁定镜头视野角，默认沿用原值。</param>
    public bool LockCamera(Transform lockTransform, float fieldOfView = 55f)
    {
        return lockTransform != null && LockCamera(lockTransform.position, lockTransform.rotation, fieldOfView);
    }

    /// <summary>内置锁定结束链调用，以本组件拥有者身份走同一外部镜头释放协议。</summary>
    public void UnlockCamera()
    {
        ReleaseExternalCamera(this);
    }

    /// <summary>战斗或表现调用方的震动兼容入口，驱动引用存在时同步转发原时长与幅度。</summary>
    /// <param name="duration">中文含义：持续时间。震动时长。</param>
    /// <param name="magnitude">中文含义：长度。震动幅度。</param>
    public void Shake(float duration, float magnitude)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.Shake(duration, magnitude);
        }
    }

    /// <summary>战斗表现使用的另一震动兼容入口，保持原强度、时长参数顺序转发驱动。</summary>
    /// <param name="intensity">中文含义：强度。震动强度。</param>
    /// <param name="duration">中文含义：持续时间。震动时长。</param>
    public void CameraShake(float intensity, float duration)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.CameraShake(intensity, duration);
        }
    }

    /// <summary>体术表现请求镜头推移的入口，只转发驱动，不新建动作或镜头。</summary>
    public void TriggerMeleePush()
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.TriggerMeleePush();
        }
    }

    /// <summary>设置界面一次提交水平、垂直和瞄准参数，驱动负责限制有效范围和可选持久化。</summary>
    /// <param name="horizontal">中文含义：水平。水平观察灵敏度。</param>
    /// <param name="vertical">中文含义：竖直。垂直观察灵敏度。</param>
    /// <param name="aim">中文含义：瞄准。瞄准灵敏度。</param>
    /// <param name="saveToPreferences">中文含义：是否保存到用户偏好。是否由驱动保存到偏好设置。</param>
    public void SetLookSensitivity(float horizontal, float vertical, float aim, bool saveToPreferences = true)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetLookSensitivity(horizontal, vertical, aim, saveToPreferences);
        }
    }

    /// <summary>设置界面只提交水平灵敏度时调用，转发当前驱动处理。</summary>
    /// <param name="value">中文含义：值。水平灵敏度值。</param>
    /// <param name="saveToPreferences">中文含义：是否保存到用户偏好。是否保存偏好设置。</param>
    public void SetHorizontalLookSensitivity(float value, bool saveToPreferences = true)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetHorizontalLookSensitivity(value, saveToPreferences);
        }
    }

    /// <summary>设置界面只提交垂直灵敏度时调用，转发当前驱动处理。</summary>
    /// <param name="value">中文含义：值。垂直灵敏度值。</param>
    /// <param name="saveToPreferences">中文含义：是否保存到用户偏好。是否保存偏好设置。</param>
    public void SetVerticalLookSensitivity(float value, bool saveToPreferences = true)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetVerticalLookSensitivity(value, saveToPreferences);
        }
    }

    /// <summary>设置界面只提交瞄准灵敏度时调用，转发当前驱动处理。</summary>
    /// <param name="value">中文含义：值。瞄准灵敏度值。</param>
    /// <param name="saveToPreferences">中文含义：是否保存到用户偏好。是否保存偏好设置。</param>
    public void SetAimLookSensitivity(float value, bool saveToPreferences = true)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetAimLookSensitivity(value, saveToPreferences);
        }
    }

    /// <summary>设置或初始化消费者要求重新读取灵敏度时调用，实际读取由驱动执行。</summary>
    public void ReloadLookSensitivity()
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.ReloadLookSensitivity();
        }
    }

    /// <summary>设置界面提交纵轴反转，驱动负责应用与可选保存。</summary>
    /// <param name="inverted">中文含义：是否反转后的。是否反转纵向观察。</param>
    /// <param name="saveToPreferences">中文含义：是否保存到用户偏好。是否保存偏好设置。</param>
    public void SetInvertY(bool inverted, bool saveToPreferences = true)
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.SetInvertY(inverted, saveToPreferences);
        }
    }

    /// <summary>设置或初始化链要求重新读取纵轴反转时调用，转发驱动的既有读取入口。</summary>
    public void ReloadInvertY()
    {
        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            driver.ReloadInvertY();
        }
    }

    /// <summary>武器成功射击后的镜头反馈入口，先读取配置后坐倍率，仅在驱动引用存在时继续求值震动、抬枪和回正参数。</summary>
    /// <param name="recoilAmount">中文含义：后坐力大小。武器提供的基础后坐幅度。</param>
    public void AddWeaponRecoil(float recoilAmount)
    {
        // 无配置时为一的后坐缩放，在原位置先于驱动判空读取。
        // 中文含义：scale 为“缩放”。
float scale = 1f;
        if (activeProfile != null)
        {
            scale = activeProfile.recoilScale;
        }

        // 本次原条件访问接收者的单次快照；用引用判空保留 Unity 已销毁对象与托管空引用的既有区别。
        // 中文含义：driver 为“驱动组件”。
RE4RThirdPersonCamera driver = gameplayDriver;
        if (!ReferenceEquals(driver, null))
        {
            // 基础后坐乘配置倍率的本次幅度，在其他反馈参数之前计算。
            // 中文含义：scaledRecoil 为“缩放后的后坐力”。
float scaledRecoil = recoilAmount * scale;
            // 开火震动倍率，配置缺失时为一。
            // 中文含义：shakeMultiplier 为“晃动倍率”。
float shakeMultiplier = 1f;
            if (activeProfile != null)
            {
                shakeMultiplier = activeProfile.fireShakeMultiplier;
            }

            // 最大累计抬枪角，零让驱动沿用默认值。
            // 中文含义：maximumRaise 为“最大抬高”。
float maximumRaise = 0f;
            if (activeProfile != null)
            {
                maximumRaise = activeProfile.maximumAccumulatedRaiseGunAngle;
            }

            // 后坐回正时长，零让驱动沿用默认值。
            // 中文含义：recenteringTime 为“回正时间”。
float recenteringTime = 0f;
            if (activeProfile != null)
            {
                recenteringTime = activeProfile.recoilRecenteringTime;
            }

            driver.TriggerWeaponRecoil(scaledRecoil, shakeMultiplier, maximumRaise, recenteringTime);
        }
    }
}
