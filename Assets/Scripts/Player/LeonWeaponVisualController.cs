using UnityEngine;
using System;

/// <summary>
/// 正式武器视觉控制者。
/// 唯一负责手枪、手持步枪和背部步枪视觉对象显隐的正式组件。
/// 不修改WeaponType、Animator参数、IK权重或弹药。
/// 通过订阅Coordinator的视觉标记事件驱动显隐时序。
/// </summary>
public partial class LeonWeaponVisualController : MonoBehaviour
{
    /// <summary>战斗小刀配置。</summary>
    private KnifeCombatConfiguration combatKnifeConfiguration;
    /// <summary>combatBladeBase 表示“战斗刀刃基础”；combatRightHand 表示“战斗右手”。</summary>
    private Transform combatBladeBase, combatRightHand;
    /// <summary>combatKnifeOriginalPosition 表示“战斗小刀原始位置”；combatKnifeHandInnerPosition 表示“战斗小刀手部内部位置”。</summary>
    private Vector3 combatKnifeOriginalPosition, combatKnifeHandInnerPosition;
    /// <summary>combatKnifeOriginalRotation 表示“战斗小刀原始旋转”；combatKnifeHandInnerRotation 表示“战斗小刀手部内部旋转”。</summary>
    private Quaternion combatKnifeOriginalRotation, combatKnifeHandInnerRotation;
    /// <summary>combatGripPoseWeight 表示“战斗握持姿态权重”；combatGripPoseTarget 表示“战斗握持姿态目标”；combatGripPoseBlend 表示“战斗握持姿态混合”。</summary>
    private float combatGripPoseWeight, combatGripPoseTarget, combatGripPoseBlend = .08f;
    /// <summary>战斗手指骨骼。</summary>
    private Transform[] combatFingerBone;
    /// <summary>ApplyKnifeGripAfterPose 的业务处理入口；本方法直接依赖 Quaternion.Slerp(Quaternion, Quaternion, float)、Transform.TransformPoint(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） Quaternion.Slerp(Quaternion, Quaternion, float)、Transform.TransformPoint(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ApplyKnifeGripAfterPose()
    {
        if (combatKnifeConfiguration == null || combatBladeBase == null || combatRightHand == null || assassinationKnifePoseActive)
        {
            return;
        }

        // 位置。
Vector3 position = combatKnifeConfiguration.combinedGripCenter + combatKnifeConfiguration.combinedRotation * (combatKnifeHandInnerPosition - combatKnifeConfiguration.originalGripCenter);
        combatBladeBase.position = combatRightHand.TransformPoint(Vector3.Lerp(combatKnifeHandInnerPosition, position, combatGripPoseWeight));
        combatBladeBase.rotation = combatRightHand.rotation * Quaternion.Slerp(combatKnifeHandInnerRotation, combatKnifeConfiguration.combinedRotation * combatKnifeHandInnerRotation, combatGripPoseWeight);
    }

    /// <summary>SetCombatKnifeGripPose 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CommitCurrentAnimation()；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="configuration">配置。</param>
    /// <param name="originalGripPose">是否原始握持姿态。</param>
    /// <param name="blend">混合。</param>
    public void SetCombatKnifeGripPose(KnifeCombatConfiguration configuration, bool originalGripPose, float blend)
    {
        if (combatBladeBase == null || combatRightHand == null)
        {
            return;
        }

        combatKnifeConfiguration = configuration;
        if (originalGripPose)
        {
            combatGripPoseTarget = 0f;
        }
        else
        {
            combatGripPoseTarget = 1f;
        }

        combatGripPoseBlend = Mathf.Max(.001f, blend);
    }

    /// <summary>EndCombatKnifeGripPose 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.EndTransaction(bool)、LeonWeaponVisualController.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndCombatKnifeGripPose()
    {
        combatKnifeConfiguration = null;
        combatGripPoseWeight = combatGripPoseTarget = 0;
        if (combatBladeBase != null)
        {
            combatBladeBase.localPosition = combatKnifeOriginalPosition;
            combatBladeBase.localRotation = combatKnifeOriginalRotation;
        }
    }

    /// <summary>UpdateCombatKnifeGripPose 的业务处理入口；本方法直接依赖 Mathf.MoveTowards(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Transform.TransformPoint(Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.LateUpdate()；下游（含内部回调体） Mathf.MoveTowards(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Transform.TransformPoint(Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateCombatKnifeGripPose()
    {
        if (combatKnifeConfiguration == null || combatBladeBase == null || combatRightHand == null || assassinationKnifePoseActive)
        {
            return;
        }

        combatGripPoseWeight = Mathf.MoveTowards(combatGripPoseWeight, combatGripPoseTarget, Time.deltaTime / combatGripPoseBlend);
        // 位置值。
var positionValue = combatKnifeConfiguration.combinedGripCenter + combatKnifeConfiguration.combinedRotation * (combatKnifeHandInnerPosition - combatKnifeConfiguration.originalGripCenter);
        combatBladeBase.position = combatRightHand.TransformPoint(Vector3.Lerp(combatKnifeHandInnerPosition, positionValue, combatGripPoseWeight));
        combatBladeBase.rotation = combatRightHand.rotation * Quaternion.Slerp(combatKnifeHandInnerRotation, combatKnifeConfiguration.combinedRotation * combatKnifeHandInnerRotation, combatGripPoseWeight);
        if (combatFingerBone != null && combatKnifeConfiguration.fingerBoneName != null && combatKnifeConfiguration.fingerBoneRotation != null)
        {
            // bone（骨骼）：每轮从战斗手指骨骼取得一个元素，仅代表当前这一项。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、LeonWeaponVisualController.UpdateCombatKnifeGripPose()。
foreach (var bone in combatFingerBone)
            {
                // 当前元素索引。
for (int index = 0; index < combatKnifeConfiguration.fingerBoneName.Length && index < combatKnifeConfiguration.fingerBoneRotation.Length; index++)
                {
                    if (bone.name == combatKnifeConfiguration.fingerBoneName[index])
                    {
                        bone.localRotation = Quaternion.Slerp(bone.localRotation, combatKnifeConfiguration.fingerBoneRotation[index], combatGripPoseWeight);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>当前玩家动画动作协调器。</summary>
    [Header("组件引用")]
    [SerializeField]
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>里昂玩家控制器。</summary>
    [SerializeField]
    private LeonController controller;
    /// <summary>纯视觉对象（SetActive控制）。</summary>
    [Header("手枪视觉绑定")]
    [Tooltip("纯视觉对象（SetActive控制）")]
    [SerializeField]
    private GameObject[] pistolVisualObjects;
    /// <summary>手枪视觉表现渲染器集合。Renderer（enabled控制，与必要组件共享GameObject）。</summary>
    [Tooltip("Renderer（enabled控制，与必要组件共享GameObject）")]
    [SerializeField]
    private Renderer[] pistolVisualRenderers;
    /// <summary>纯视觉对象（SetActive控制）。</summary>
    [Header("步枪视觉绑定")]
    [Tooltip("纯视觉对象（SetActive控制）")]
    [SerializeField]
    private GameObject[] rifleVisualObjects;
    /// <summary>步枪视觉表现渲染器集合。Renderer（enabled控制，与必要组件共享GameObject）。</summary>
    [Tooltip("Renderer（enabled控制，与必要组件共享GameObject）")]
    [SerializeField]
    private Renderer[] rifleVisualRenderers;
    /// <summary>纯视觉对象（SetActive控制）。</summary>
    [Header("霰弹枪视觉绑定")]
    [Tooltip("纯视觉对象（SetActive控制）")]
    [SerializeField]
    private GameObject[] shotgunVisualObjects;
    /// <summary>霰弹枪视觉表现渲染器集合。Renderer（enabled控制，与必要组件共享GameObject）。</summary>
    [Tooltip("Renderer（enabled控制，与必要组件共享GameObject）")]
    [SerializeField]
    private Renderer[] shotgunVisualRenderers;
    /// <summary>背部步枪Renderer（enabled控制）。</summary>
    [Header("背部步枪视觉绑定")]
    [Tooltip("背部步枪Renderer（enabled控制）")]
    [SerializeField]
    private Renderer[] rifleBackRenderers;
    /// <summary>背部霰弹枪Renderer（enabled控制）。</summary>
    [Header("背部霰弹枪视觉绑定")]
    [Tooltip("背部霰弹枪Renderer（enabled控制）")]
    [SerializeField]
    private Renderer[] shotgunBackRenderers;
    /// <summary>手持手枪时隐藏，切换到其他枪械时显示。</summary>
    [Header("腰间待机手枪视觉绑定")]
    [Tooltip("手持手枪时隐藏，切换到其他枪械时显示")]
    public Renderer[] holsteredPistolRenderers;
    /// <summary>小刀纯视觉对象。</summary>
    [Header("小刀视觉绑定")]
    [Tooltip("小刀纯视觉对象")]
    [SerializeField]
    private GameObject[] knifeVisualObjects;
    /// <summary>小刀渲染器。</summary>
    [Tooltip("小刀渲染器")]
    [SerializeField]
    private Renderer[] knifeVisualRenderers;
    /// <summary>掏刀动作进行到此比例后显示小刀。</summary>
    [Header("小刀显隐时机")]
    [Tooltip("掏刀动作进行到此比例后显示小刀")]
    [Range(0f, 0.8f)]
    [SerializeField]
    private float knifeTakeRevealNormalizedTime = 0.12f;
    /// <summary>收刀动作进行到此比例后隐藏小刀。</summary>
    [Tooltip("收刀动作进行到此比例后隐藏小刀")]
    [Range(0.2f, 1f)]
    [SerializeField]
    private float knifeHideConcealNormalizedTime = 0.52f;
    /// <summary>留空时从小刀视觉绑定查找。整把刀的公共视觉根节点，必须位于右手骨骼下；留空时从小刀视觉绑定查找。</summary>
    [Header("暗杀专用刀姿态")]
    [Tooltip("整把刀的公共视觉根节点，必须位于右手骨骼下；留空时从小刀视觉绑定查找")]
    public Transform assassinationKnifePoseRoot;
    /// <summary>刀柄握持中心在公共视觉根节点中的局部坐标。</summary>
    [Tooltip("刀柄握持中心在公共视觉根节点中的局部坐标")]
    public Vector3 assassinationKnifeGripLocalPosition = new Vector3(0.60403f, -1.3868f, 0.0044f);
    /// <summary>刀柄握持中心在右手骨骼中的局部坐标。</summary>
    [Tooltip("刀柄握持中心在右手骨骼中的局部坐标")]
    public Vector3 assassinationKnifeHandGripLocalPosition = new Vector3(0.01556f, 0.07819f, 0.02582f);
    /// <summary>整把刀相对右手的刺杀旋转，刀刃朝向喉部。</summary>
    [Tooltip("整把刀相对右手的刺杀旋转，刀刃朝向喉部")]
    public Vector3 assassinationKnifeHandLocalEulerAngles = new Vector3(0f, 85.42607f, 78.72514f);
    /// <summary>暗杀右手。</summary>
    private Transform assassinationRightHand;
    /// <summary>生效暗杀小刀姿态根节点。</summary>
    private Transform activeAssassinationKnifePoseRoot;
    /// <summary>暗杀小刀原始位置。</summary>
    private Vector3 assassinationKnifeOriginalPosition;
    /// <summary>暗杀小刀原始旋转。</summary>
    private Quaternion assassinationKnifeOriginalRotation;
    /// <summary>暗杀小刀姿态是否生效。</summary>
    private bool assassinationKnifePoseActive;
    /// <summary>尚未切换过长枪时，背部默认显示的武器。</summary>
    [Header("背部长枪互斥状态")]
    [Tooltip("尚未切换过长枪时，背部默认显示的武器")]
    [SerializeField]
    private WeaponType defaultBackLongGun = WeaponType.Shotgun;
    /// <summary>后方长枪。</summary>
    [SerializeField]
    private WeaponType backLongGun = WeaponType.Shotgun;
    /// <summary>当前武器。</summary>
    [Header("状态")]
    [SerializeField]
    private WeaponType currentWeapon = WeaponType.Pistol;
    /// <summary>当前视觉表现动作事务编号。</summary>
    private int currentVisualTxId = -1;
    /// <summary>已隐藏依据配对动作会话编号。</summary>
    private int hiddenByQTESessionId = -1;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>背包数据与操作宿主。</summary>
    private InventoryManager inventoryManager;
    /// <summary>背包是否已绑定事件。</summary>
    private bool inventoryEventsBound;
    /// <summary>小刀模式是否可见。</summary>
    private ItemData presentedKnife;
    private GameObject spareKnifeVisual;
    private GameObject loadedSparePrefab;
    private Renderer[] bladeRenderers;
    private Transform spareKnifeMount;

    internal void SetKnifeAppearance(GridItem item)
    {
        presentedKnife = null;
        if (item != null)
        {
            presentedKnife = item.item;
        }
    }

    private void UpdateSpareKnifeAppearance(Transform mount, bool visible, Vector3 bladeStart, Vector3 bladeEnd)
    {
        GameObject model = null;
        if (visible && presentedKnife != null &&
            (presentedKnife.itemId == InventoryManager.KitchenKnifeId || presentedKnife.itemId == InventoryManager.BootKnifeId))
        {
            model = presentedKnife.worldModelPrefab;
        }

        if (spareKnifeMount != mount || loadedSparePrefab != model)
        {
            if (spareKnifeVisual != null)
            {
                spareKnifeVisual.SetActive(false);
                Destroy(spareKnifeVisual);
                spareKnifeVisual = null;
            }
            if (bladeRenderers != null)
            {
                foreach (Renderer mesh in bladeRenderers)
                {
                    if (mesh != null)
                    {
                        mesh.enabled = true;
                    }
                }
            }
            spareKnifeMount = mount;
            loadedSparePrefab = model;
            bladeRenderers = null;
            if (mount != null && model != null)
            {
                bladeRenderers = mount.GetComponentsInChildren<Renderer>(true);
                spareKnifeVisual = Instantiate(model, mount, false);
                Vector3 bladeAxis = bladeEnd - bladeStart;
                if (bladeAxis.sqrMagnitude > .00001f)
                {
                    Quaternion localRotation = Quaternion.FromToRotation(Vector3.forward, bladeAxis.normalized);
                    float scale = bladeAxis.magnitude / .183f;
                    spareKnifeVisual.transform.localRotation = localRotation;
                    spareKnifeVisual.transform.localScale = Vector3.one * scale;
                    spareKnifeVisual.transform.localPosition = bladeStart - localRotation * (Vector3.forward * (.076f * scale));
                }
            }
        }
        if (spareKnifeVisual != null && bladeRenderers != null)
        {
            foreach (Renderer mesh in bladeRenderers)
            {
                if (mesh != null)
                {
                    mesh.enabled = false;
                }
            }
        }
    }

    private bool knifeModeVisible;
    private bool nativePistolHolsterBound;
    internal bool IsCombatKnifeVisible => knifeModeVisible;
    /// <summary>后方长枪是否已初始化。</summary>
    private bool backLongGunInitialized;
    /// <summary>小刀时序动作事务编号。</summary>
    private int knifeTimingTxId = -1;
    /// <summary>小刀时序动作。</summary>
    private WeaponActionType knifeTimingAction = WeaponActionType.None;
    /// <summary>是否小刀时序已应用。</summary>
    private bool knifeTimingApplied;
    /// <summary>当前已装备武器。</summary>
    public WeaponType CurrentEquippedWeapon => currentWeapon;

    /// <summary>GetWeaponVisualRoot 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.FindCommonVisualRoot(GameObject[], Renderer[])，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.Start()、LeonFingerIKController.FindWeaponRoot(WeaponType)、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter() 等共 8 处；下游（含内部回调体） LeonWeaponVisualController.FindCommonVisualRoot(GameObject[], Renderer[])。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    public Transform GetWeaponVisualRoot(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Pistol:
                return FindCommonVisualRoot(pistolVisualObjects, pistolVisualRenderers);
            case WeaponType.Rifle:
                return FindCommonVisualRoot(rifleVisualObjects, rifleVisualRenderers);
            case WeaponType.Shotgun:
                return FindCommonVisualRoot(shotgunVisualObjects, shotgunVisualRenderers);
            case WeaponType.Knife:
                return FindCommonVisualRoot(knifeVisualObjects, knifeVisualRenderers);
            default:
                return null;
        }
    }

    /// <summary>FindCommonVisualRoot 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.FindCommonAncestor(Transform, Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType)；下游（含内部回调体） LeonWeaponVisualController.FindCommonAncestor(Transform, Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objects">对象集合。</param>
    /// <param name="renderers">渲染器集合。</param>
    private static Transform FindCommonVisualRoot(GameObject[] objects, Renderer[] renderers)
    {
        // 通用。
Transform common = null;
        if (objects != null)
        {
            // visualObject（视觉表现对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonWeaponVisualController.FindCommonAncestor(Transform, Transform) 的参数、LeonWeaponVisualController.FindCommonVisualRoot(GameObject[], Renderer[])。
foreach (GameObject visualObject in objects)
            {
                if (visualObject == null)
                {
                    continue;
                }

                if (common == null)
                {
                    common = visualObject.transform;
                }
                else
                {
                    common = FindCommonAncestor(common, visualObject.transform);
                }
            }
        }

        if (renderers != null)
        {
            // visualRenderer（视觉表现渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonWeaponVisualController.FindCommonAncestor(Transform, Transform) 的参数、LeonWeaponVisualController.FindCommonVisualRoot(GameObject[], Renderer[])。
foreach (Renderer visualRenderer in renderers)
            {
                if (visualRenderer == null)
                {
                    continue;
                }

                if (common == null)
                {
                    common = visualRenderer.transform;
                }
                else
                {
                    common = FindCommonAncestor(common, visualRenderer.transform);
                }
            }
        }

        return common;
    }

    /// <summary>FindCommonAncestor 的业务处理入口；本方法直接依赖 Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.FindCommonVisualRoot(GameObject[], Renderer[])；下游（含内部回调体） Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">第一个变换节点。</param>
    /// <param name="second">第二个变换节点。</param>
    private static Transform FindCommonAncestor(Transform first, Transform second)
    {
        if (first == null || second == null)
        {
            return null;
        }

        // 候选。
Transform candidate = first;
        while (candidate != null)
        {
            if (second == candidate || second.IsChildOf(candidate))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return null;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponentsInChildren&lt;Transform&gt;(bool)、LeonWeaponVisualController.BindCoordinatorEvents()、LeonWeaponVisualController.BindInventoryEvents() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }

        // 动画控制器。
Animator animator = GetComponent<Animator>();
        assassinationRightHand = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightHand);

        combatRightHand = assassinationRightHand;
        if (combatRightHand != null)
        {
            combatFingerBone = combatRightHand.GetComponentsInChildren<Transform>(true);
        }

        combatBladeBase = GetWeaponVisualRoot(WeaponType.Knife);
        if (combatBladeBase != null && combatRightHand != null)
        {
            combatKnifeOriginalPosition = combatBladeBase.localPosition;
            combatKnifeOriginalRotation = combatBladeBase.localRotation;
            combatKnifeHandInnerPosition = combatRightHand.InverseTransformPoint(combatBladeBase.position);
            combatKnifeHandInnerRotation = Quaternion.Inverse(combatRightHand.rotation) * combatBladeBase.rotation;
        }

        if (defaultBackLongGun == WeaponType.Rifle)
        {
            backLongGun = WeaponType.Rifle;
        }
        else
        {
            backLongGun = WeaponType.Shotgun;
        }

        backLongGunInitialized = false;
        BindCoordinatorEvents();
        BindInventoryEvents();
        OnInventoryChanged();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.BindCoordinatorEvents()、LeonWeaponVisualController.BindInventoryEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        BindCoordinatorEvents();
        BindInventoryEvents();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.EndAssassinationKnifePose()、LeonWeaponVisualController.EndCombatKnifeGripPose()、LeonWeaponVisualController.UnbindCoordinatorEvents()、LeonWeaponVisualController.UnbindInventoryEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        EndNativePairKnife();
        EndCombatKnifeGripPose();
        EndAssassinationKnifePose();
        UnbindCoordinatorEvents();
        UnbindInventoryEvents();
    }

    /// <summary>BindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnEnable()、LeonWeaponVisualController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindCoordinatorEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnHolsterAnimationCompleted += OnHolsterCompleted;
        coordinator.OnEquipAnimationCompleted += OnEquipCompleted;
        coordinator.OnWeaponActionInterrupted += OnWeaponInterrupted;
        coordinator.OnWeaponActionProgress += OnWeaponActionProgress;
        coordinator.OnVisualMarker += OnVisualMarker;
        coordinator.OnQTEWeaponVisibilityRequested += OnQTEWeaponVisibilityRequested;
        eventsBound = true;
    }

    /// <summary>UnbindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindCoordinatorEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (coordinator != null)
        {
            coordinator.OnHolsterAnimationCompleted -= OnHolsterCompleted;
            coordinator.OnEquipAnimationCompleted -= OnEquipCompleted;
            coordinator.OnWeaponActionInterrupted -= OnWeaponInterrupted;
            coordinator.OnWeaponActionProgress -= OnWeaponActionProgress;
            coordinator.OnVisualMarker -= OnVisualMarker;
            coordinator.OnQTEWeaponVisibilityRequested -= OnQTEWeaponVisibilityRequested;
        }

        eventsBound = false;
    }

    /// <summary>BindInventoryEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnEnable()、LeonWeaponVisualController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindInventoryEvents()
    {
        if (inventoryEventsBound || inventoryManager == null)
        {
            return;
        }

        inventoryManager.OnInventoryChanged += OnInventoryChanged;
        inventoryEventsBound = true;
    }

    /// <summary>UnbindInventoryEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindInventoryEvents()
    {
        if (!inventoryEventsBound)
        {
            return;
        }

        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryChanged -= OnInventoryChanged;
        }

        inventoryEventsBound = false;
    }

    /// <summary>CanProcessVisuals 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.BeginAssassinationKnifePose()、LeonWeaponVisualController.Initialize(WeaponType)、LeonWeaponVisualController.OnEquipCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnHolsterCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnQTEWeaponVisibilityRequested(int, bool) 等共 12 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanProcessVisuals()
    {
        return controller != null && controller.NewPipelineValid;
    }

    /// <summary>初始化指定武器可见</summary>
    /// <remarks>静态调用链：上游 LeonController.InitPipeline()；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.SetEquippedWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    public void Initialize(WeaponType weapon)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        SetEquippedWeapon(weapon);
    }

    /// <summary>
    /// 设置常驻装备的视觉状态。小刀属于正式武器模式，不经过枪械收拔事务。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)、LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool)、LeonWeaponVisualController.FinishKnifeEquip()、LeonWeaponVisualController.Initialize(WeaponType)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.RefreshHolsteredPistolVisual()、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    public void SetEquippedWeapon(WeaponType weapon)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        currentWeapon = weapon;
        knifeModeVisible = weapon == WeaponType.Knife;
        SetWeaponVisible(WeaponType.Pistol, weapon == WeaponType.Pistol);
        SetWeaponVisible(WeaponType.Rifle, weapon == WeaponType.Rifle);
        SetWeaponVisible(WeaponType.Shotgun, weapon == WeaponType.Shotgun);
        SetKnifeVisible(knifeModeVisible);
        RefreshHolsteredPistolVisual();
        RefreshBackLongGunVisuals();
    }

    /// <summary>
    /// 翻越期间隐藏当前手持武器，结束后按当前正式武器状态恢复。
    /// 不修改弹药、武器槽位或武器切换事务。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.ReleaseNormalTraversalGameplay()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.SetEquippedWeapon(WeaponType)、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    public void SetTraversalWeaponVisible(bool visible)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (visible)
        {
            SetEquippedWeapon(controller.currentWeapon);
            return;
        }

        SetWeaponVisible(WeaponType.Pistol, false);
        SetWeaponVisible(WeaponType.Rifle, false);
        SetWeaponVisible(WeaponType.Shotgun, false);
        SetKnifeVisible(false);
    }

    /// <summary>
    /// 进入或退出临时持刀姿态。
    /// 不改变当前枪械类型、弹药或枪械切换事务。
    /// </summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.EndTransaction(bool)、KnifeCombatModule.PrepareTransaction(bool)、LeonCombat.ApplyGuardPose()、LeonCombat.ClearParryState(bool)、LeonCombat.HandleParry() 等共 10 处；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    public void SetKnifeModeVisible(bool visible)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        knifeModeVisible = visible;
        SetKnifeVisible(visible);
        if (IsGunWeapon(currentWeapon))
        {
            SetWeaponVisible(currentWeapon, !visible && hiddenByQTESessionId < 0);
        }
        RefreshHolsteredPistolVisual();
        RefreshBackLongGunVisuals();
    }

    /// <summary>
    /// 以刀柄中心为锚点统一调整整把刀，保留配对动画对右手骨骼的控制。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.SetAssassinationKnifeVisualIfNeeded(QTEKind)；下游（含内部回调体） LeonWeaponVisualController.ApplyAssassinationKnifePose()、LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginAssassinationKnifePose()
    {
        if (!CanProcessVisuals() || assassinationKnifePoseActive || assassinationRightHand == null)
        {
            return;
        }

        // 根变换节点。
Transform root;
        if (assassinationKnifePoseRoot != null)
        {
            root = assassinationKnifePoseRoot;
        }
        else
        {
            root = GetWeaponVisualRoot(WeaponType.Knife);
        }

        // 只接受右手下的视觉节点，避免错误绑定把旋转施加到角色骨骼。
        if (root == null || root == assassinationRightHand || !root.IsChildOf(assassinationRightHand))
        {
            return;
        }

        activeAssassinationKnifePoseRoot = root;
        assassinationKnifeOriginalPosition = root.localPosition;
        assassinationKnifeOriginalRotation = root.localRotation;
        assassinationKnifePoseActive = true;
        ApplyAssassinationKnifePose();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.ApplyAssassinationKnifePose()、LeonWeaponVisualController.UpdateCombatKnifeGripPose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        UpdateCombatKnifeGripPose();
        if (assassinationKnifePoseActive)
        {
            ApplyAssassinationKnifePose();
        }
    }

    /// <summary>ApplyAssassinationKnifePose 的业务处理入口；本方法直接依赖 Quaternion.Euler(Vector3)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.BeginAssassinationKnifePose()、LeonWeaponVisualController.LateUpdate()；下游（含内部回调体） Quaternion.Euler(Vector3)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyAssassinationKnifePose()
    {
        if (activeAssassinationKnifePoseRoot == null || assassinationRightHand == null)
        {
            return;
        }

        // 根变换节点。
Transform root = activeAssassinationKnifePoseRoot;
        root.rotation = assassinationRightHand.rotation * Quaternion.Euler(assassinationKnifeHandLocalEulerAngles);
        // 先旋转，再补偿模型原点到刀柄中心的距离，保证刀柄始终留在掌心。
        root.position = assassinationRightHand.TransformPoint(assassinationKnifeHandGripLocalPosition) - root.TransformVector(assassinationKnifeGripLocalPosition);
    }

    /// <summary>
    /// 恢复暗杀前的局部位置和旋转，覆盖成功、失败、中断及组件禁用。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()、LeonWeaponVisualController.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndAssassinationKnifePose()
    {
        if (!assassinationKnifePoseActive)
        {
            return;
        }

        if (activeAssassinationKnifePoseRoot != null)
        {
            activeAssassinationKnifePoseRoot.localPosition = assassinationKnifeOriginalPosition;
            activeAssassinationKnifePoseRoot.localRotation = assassinationKnifeOriginalRotation;
        }

        activeAssassinationKnifePoseRoot = null;
        assassinationKnifePoseActive = false;
    }

    /// <summary>
    /// 掏刀动作完成后，提交小刀的常驻可见状态。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)；下游（含内部回调体） LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetEquippedWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void FinishKnifeEquip()
    {
        SetEquippedWeapon(WeaponType.Knife);
        currentVisualTxId = -1;
        ResetKnifeTiming();
    }

    /// <summary>
    /// 收刀动作结束后，为下一把枪的掏枪动画准备视觉状态。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.IsGunWeapon(WeaponType)、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.RefreshHolsteredPistolVisual()、LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    public void PrepareGunEquipAfterKnifeHide(WeaponType weapon)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (!IsGunWeapon(weapon))
        {
            return;
        }

        SetKnifeVisible(false);
        knifeModeVisible = false;
        currentWeapon = weapon;
        SetWeaponVisible(weapon, false);
        PrepareHolsteredPistolForEquip();
        RefreshBackLongGunVisuals();
        ResetKnifeTiming();
    }

    /// <summary>TryBindOrValidateVisualTransaction 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.ResetKnifeTiming()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnEquipCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnHolsterCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.OnWeaponActionProgress(AnimationActionTransaction, float)、LeonWeaponVisualController.OnWeaponInterrupted(AnimationActionTransaction)；下游（含内部回调体） LeonWeaponVisualController.ResetKnifeTiming()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private bool TryBindOrValidateVisualTransaction(AnimationActionTransaction tx)
    {
        if (!tx.isWeaponSwitch)
        {
            return false;
        }

        if (tx.pendingWeaponType != WeaponType.Pistol && tx.pendingWeaponType != WeaponType.Rifle && tx.pendingWeaponType != WeaponType.Shotgun && tx.pendingWeaponType != WeaponType.Knife)
        {
            return false;
        }

        if (currentVisualTxId == -1)
        {
            currentVisualTxId = tx.transactionId;
            return true;
        }

        if (currentVisualTxId == tx.transactionId)
        {
            return true;
        }

        // 事务编号单调递增；即使上一动作的退出通知被状态机切换吞掉，
        // 新事务也必须能够接管视觉，避免后续长枪循环全部被旧编号拒绝。
        if (tx.transactionId > currentVisualTxId)
        {
            currentVisualTxId = tx.transactionId;
            ResetKnifeTiming();
            return true;
        }

        return false;
    }

    /// <summary>OnHolsterCompleted 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshHolsteredPistolVisual()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshHolsteredPistolVisual()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnHolsterCompleted(AnimationActionTransaction tx)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (!TryBindOrValidateVisualTransaction(tx))
        {
            return;
        }

        // 隐藏旧武器
        SetWeaponVisible(currentWeapon, false);
        // 更新当前武器为目标武器（视觉暂不显示）
        currentWeapon = tx.pendingWeaponType;
        PrepareHolsteredPistolForEquip();
        if (knifeModeVisible)
        {
            SetWeaponVisible(currentWeapon, false);
        }
    }

    /// <summary>OnEquipCompleted 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnEquipCompleted(AnimationActionTransaction tx)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (!TryBindOrValidateVisualTransaction(tx))
        {
            return;
        }

        // 兜底：确保新武器可见
        SetWeaponVisible(currentWeapon, !knifeModeVisible);
        RefreshHolsteredPistolVisual();
        // 兜底：背部长枪状态正确
        RefreshBackLongGunVisuals();
        currentVisualTxId = -1;
        ResetKnifeTiming();
    }

    /// <summary>OnWeaponInterrupted 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.ResetKnifeTiming()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnWeaponInterrupted(AnimationActionTransaction tx)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (!TryBindOrValidateVisualTransaction(tx))
        {
            return;
        }

        if (currentWeapon == WeaponType.Knife)
        {
            knifeModeVisible = true;
            SetKnifeVisible(true);
        }
        else
        {
            SetWeaponVisible(currentWeapon, !knifeModeVisible);
        }

        RefreshBackLongGunVisuals();
        RefreshHolsteredPistolVisual();
        currentVisualTxId = -1;
        ResetKnifeTiming();
    }

    /// <summary>OnWeaponActionProgress 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.SetKnifeVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    /// <param name="normalizedTime">动画归一化播放时间。</param>
    private void OnWeaponActionProgress(AnimationActionTransaction tx, float normalizedTime)
    {
        if (!CanProcessVisuals() || !tx.isWeaponSwitch)
        {
            return;
        }

        if (tx.weaponActionType != WeaponActionType.KnifeTake && tx.weaponActionType != WeaponActionType.KnifeHide)
        {
            return;
        }

        if (!TryBindOrValidateVisualTransaction(tx))
        {
            return;
        }

        if (knifeTimingTxId != tx.transactionId || knifeTimingAction != tx.weaponActionType)
        {
            knifeTimingTxId = tx.transactionId;
            knifeTimingAction = tx.weaponActionType;
            knifeTimingApplied = false;
        }

        if (knifeTimingApplied)
        {
            return;
        }

        if (tx.weaponActionType == WeaponActionType.KnifeTake && normalizedTime >= knifeTakeRevealNormalizedTime)
        {
            knifeModeVisible = true;
            SetWeaponVisible(WeaponType.Pistol, false);
            SetWeaponVisible(WeaponType.Rifle, false);
            SetWeaponVisible(WeaponType.Shotgun, false);
            SetKnifeVisible(true);
            knifeTimingApplied = true;
        }
        else if (tx.weaponActionType == WeaponActionType.KnifeHide)
        {
            float concealTime = knifeHideConcealNormalizedTime;
            if (TryGetComponent<LeonAnimatorDriver>(out var driver) && driver.SupportsNativeKnifeGait)
            {
                concealTime = LeonAnimatorDriver.NativeKnifeConcealFrame / (float)LeonAnimatorDriver.NativeKnifeHideFrames;
            }

            if (normalizedTime >= concealTime)
            {
                SetKnifeVisible(false);
                knifeModeVisible = false;
                knifeTimingApplied = true;
            }
        }
    }

    /// <summary>OnVisualMarker 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisualsForHolsteredWeapon(WeaponType)、LeonWeaponVisualController.SetBackRifleVisible(bool)、LeonWeaponVisualController.SetBackShotgunVisible(bool)、LeonWeaponVisualController.SetHolsteredPistolVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisualsForHolsteredWeapon(WeaponType)、LeonWeaponVisualController.SetBackRifleVisible(bool)、LeonWeaponVisualController.SetBackShotgunVisible(bool)、LeonWeaponVisualController.SetHolsteredPistolVisible(bool)、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    /// <param name="marker">标记。</param>
    private void OnVisualMarker(AnimationActionTransaction tx, VisualMarker marker)
    {
        if (!CanProcessVisuals())
        {
            return;
        }

        if (!TryBindOrValidateVisualTransaction(tx))
        {
            return;
        }

        if (marker == VisualMarker.HolsterVisualCommit)
        {
            // 收枪视觉提交：隐藏当前武器
            SetWeaponVisible(currentWeapon, false);
            if (currentWeapon == WeaponType.Pistol)
            {
                SetHolsteredPistolVisible(true);
            }

            RefreshBackLongGunVisualsForHolsteredWeapon(currentWeapon, tx.pendingWeaponType);
        }
        else if (marker == VisualMarker.RifleBackHideBeforeReveal)
        {
            // 背部长枪在手持长枪显示前一帧隐藏
            if (tx.pendingWeaponType == WeaponType.Rifle)
            {
                SetBackRifleVisible(false);
            }
            else if (tx.pendingWeaponType == WeaponType.Shotgun)
            {
                SetBackShotgunVisible(false);
            }
        }
        else if (marker == VisualMarker.EquipVisualReveal)
        {
            // 拔枪视觉显示：显示新武器
            SetWeaponVisible(currentWeapon, true);
            if (currentWeapon == WeaponType.Pistol)
            {
                SetHolsteredPistolVisible(false);
            }
            RefreshBackLongGunVisuals();
        }
    }

    /// <summary>OnQTEWeaponVisibilityRequested 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponVisualController.CanProcessVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="visible">是否可见。</param>
    private void OnQTEWeaponVisibilityRequested(int sessionId, bool visible)
    {
        if (!CanProcessVisuals() || sessionId <= 0)
        {
            return;
        }

        if (!visible)
        {
            if (hiddenByQTESessionId != -1 && hiddenByQTESessionId != sessionId)
            {
                return;
            }

            hiddenByQTESessionId = sessionId;
            SetWeaponVisible(currentWeapon, false);
            return;
        }

        if (hiddenByQTESessionId != sessionId)
        {
            return;
        }

        hiddenByQTESessionId = -1;
        SetWeaponVisible(currentWeapon, !knifeModeVisible);
        RefreshBackLongGunVisuals();
    }

    /// <summary>OnInventoryChanged 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.RefreshBackLongGunVisuals()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.Start()；下游（含内部回调体） LeonWeaponVisualController.RefreshBackLongGunVisuals()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnInventoryChanged()
    {
        if (UsesNativePistolVisual && (coordinator == null || !coordinator.HasPendingWeaponSwitch))
        {
            RefreshHolsteredPistolVisual();
        }
        RefreshBackLongGunVisuals();
    }

    private bool UsesNativePistolVisual => TryGetComponent<LeonAnimatorDriver>(out var driver) && driver.SupportsNativePistolSwitch;

    /// <summary>HasOwnedLongGun 的业务处理入口；本方法直接依赖 InventoryManager.HasItem(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.RefreshBackLongGunVisualsForHolsteredWeapon(WeaponType)、LeonWeaponVisualController.ResolveBackLongGun()；下游（含内部回调体） InventoryManager.HasItem(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    private bool HasOwnedLongGun(WeaponType weapon)
    {
        if (inventoryManager == null)
        {
            return false;
        }

        if (weapon == WeaponType.Rifle)
        {
            return inventoryManager.HasItem("rifle");
        }

        if (weapon == WeaponType.Shotgun)
        {
            return inventoryManager.HasItem("shotgun");
        }

        return false;
    }

    /// <summary>ResolveBackLongGun 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.HasOwnedLongGun(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.RefreshBackLongGunVisuals()；下游（含内部回调体） LeonWeaponVisualController.HasOwnedLongGun(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType? ResolveBackLongGun()
    {
        // 是否具有步枪。
bool hasRifle = HasOwnedLongGun(WeaponType.Rifle);
        // 是否具有霰弹枪。
bool hasShotgun = HasOwnedLongGun(WeaponType.Shotgun);
        if (!hasRifle && !hasShotgun)
        {
            return null;
        }

        if (currentWeapon == WeaponType.Rifle)
        {
            if (hasShotgun)
            {
                return WeaponType.Shotgun;
            }
            else
            {
                return null;
            }
        }

        if (currentWeapon == WeaponType.Shotgun)
        {
            if (hasRifle)
            {
                return WeaponType.Rifle;
            }
            else
            {
                return null;
            }
        }

        if (!backLongGunInitialized)
        {
            if (defaultBackLongGun == WeaponType.Rifle && hasRifle)
            {
                backLongGun = WeaponType.Rifle;
            }
            else
            {
                if (hasShotgun)
                {
                    backLongGun = WeaponType.Shotgun;
                }
                else
                {
                    backLongGun = WeaponType.Rifle;
                }
            }

            backLongGunInitialized = true;
        }

        if (backLongGun == WeaponType.Rifle && hasRifle)
        {
            return WeaponType.Rifle;
        }

        if (backLongGun == WeaponType.Shotgun && hasShotgun)
        {
            return WeaponType.Shotgun;
        }

        // 分支选出的武器类型的霰弹枪。
global::WeaponType selectedValue10;
        if (hasShotgun)
        {
            selectedValue10 = WeaponType.Shotgun;
        }
        else
        {
            selectedValue10 = WeaponType.Rifle;
        }

        return selectedValue10;
    }

    /// <summary>RefreshBackLongGunVisuals 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.ResolveBackLongGun()、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnEquipCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnInventoryChanged()、LeonWeaponVisualController.OnQTEWeaponVisibilityRequested(int, bool)、LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.OnWeaponInterrupted(AnimationActionTransaction) 等共 9 处；下游（含内部回调体） LeonWeaponVisualController.ResolveBackLongGun()、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RefreshBackLongGunVisuals()
    {
        SetExclusiveBackLongGun(ResolveBackLongGun());
    }

    /// <summary>RefreshHolsteredPistolVisual 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.SetHolsteredPistolVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnHolsterCompleted(AnimationActionTransaction)、LeonWeaponVisualController.PrepareGunEquipAfterKnifeHide(WeaponType)、LeonWeaponVisualController.SetEquippedWeapon(WeaponType)；下游（含内部回调体） LeonWeaponVisualController.SetHolsteredPistolVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RefreshHolsteredPistolVisual()
    {
        if (UsesNativePistolVisual)
        {
            SetHolsteredPistolVisible((currentWeapon != WeaponType.Pistol || knifeModeVisible)
                && inventoryManager != null && inventoryManager.HasItem("pistol"));
            return;
        }
        // 是否应当显示。
bool shouldShow = currentWeapon == WeaponType.Rifle || currentWeapon == WeaponType.Shotgun;
        SetHolsteredPistolVisible(shouldShow);
    }

    private void PrepareHolsteredPistolForEquip()
    {
        if (currentWeapon == WeaponType.Pistol && TryGetComponent<LeonAnimatorDriver>(out var driver)
            && driver.SupportsNativePistolSwitch)
        {
            SetHolsteredPistolVisible(true);
            return;
        }
        RefreshHolsteredPistolVisual();
    }

    /// <summary>RefreshBackLongGunVisualsForHolsteredWeapon 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.HasOwnedLongGun(WeaponType)、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)；下游（含内部回调体） LeonWeaponVisualController.HasOwnedLongGun(WeaponType)、LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="holsteredWeapon">已收起武器。</param>
    private void RefreshBackLongGunVisualsForHolsteredWeapon(WeaponType holsteredWeapon, WeaponType pendingWeapon)
    {
        if ((holsteredWeapon != WeaponType.Rifle && holsteredWeapon != WeaponType.Shotgun) || !HasOwnedLongGun(holsteredWeapon))
        {
            RefreshBackLongGunVisuals();
            return;
        }

        backLongGun = holsteredWeapon;
        backLongGunInitialized = true;
        if (holsteredWeapon == WeaponType.Rifle && pendingWeapon == WeaponType.Shotgun
            && HasOwnedLongGun(pendingWeapon) && TryGetComponent<LeonAnimatorDriver>(out var driver) && driver.SupportsNativeW870Switch)
        {
            SetExclusiveBackLongGun(pendingWeapon);
            return;
        }
        SetExclusiveBackLongGun(holsteredWeapon);
    }

    /// <summary>SetExclusiveBackLongGun 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.SetBackRifleVisible(bool)、LeonWeaponVisualController.SetBackShotgunVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.RefreshBackLongGunVisuals()、LeonWeaponVisualController.RefreshBackLongGunVisualsForHolsteredWeapon(WeaponType)；下游（含内部回调体） LeonWeaponVisualController.SetBackRifleVisible(bool)、LeonWeaponVisualController.SetBackShotgunVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前可空的武器类别。</param>
    private void SetExclusiveBackLongGun(WeaponType? weapon)
    {
        SetBackRifleVisible(weapon == WeaponType.Rifle);
        SetBackShotgunVisible(weapon == WeaponType.Shotgun);
    }

    /// <summary>ResetKnifeTiming 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.FinishKnifeEquip()、LeonWeaponVisualController.OnEquipCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnWeaponInterrupted(AnimationActionTransaction)、LeonWeaponVisualController.PrepareGunEquipAfterKnifeHide(WeaponType)、LeonWeaponVisualController.TryBindOrValidateVisualTransaction(AnimationActionTransaction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetKnifeTiming()
    {
        knifeTimingTxId = -1;
        knifeTimingAction = WeaponActionType.None;
        knifeTimingApplied = false;
    }

    /// <summary>SetWeaponVisible 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、LeonWeaponVisualController.SetKnifeVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnEquipCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnHolsterCompleted(AnimationActionTransaction)、LeonWeaponVisualController.OnQTEWeaponVisibilityRequested(int, bool)、LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.OnWeaponActionProgress(AnimationActionTransaction, float) 等共 10 处；下游（含内部回调体） GameObject.SetActive(bool)、LeonWeaponVisualController.SetKnifeVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    /// <param name="visible">是否可见。</param>
    private void SetWeaponVisible(WeaponType type, bool visible)
    {
        // 对象集合。
GameObject[] objects;
        // 渲染器集合。
Renderer[] renderers;
        if (type == WeaponType.Pistol)
        {
            objects = pistolVisualObjects;
            renderers = pistolVisualRenderers;
        }
        else if (type == WeaponType.Rifle)
        {
            objects = rifleVisualObjects;
            renderers = rifleVisualRenderers;
        }
        else if (type == WeaponType.Shotgun)
        {
            objects = shotgunVisualObjects;
            renderers = shotgunVisualRenderers;
        }
        else if (type == WeaponType.Knife)
        {
            SetKnifeVisible(visible);
            return;
        }
        else
        {
            return;
        }

        if (objects != null)
        {
            // obj（当前游戏对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。
foreach (var obj in objects)
            {
                if (obj != null)
                {
                    obj.SetActive(visible);
                }
            }
        }

        if (renderers != null)
        {
            // r（当前渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetWeaponVisible(WeaponType, bool)。
foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }

    /// <summary>SetKnifeVisible 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnWeaponActionProgress(AnimationActionTransaction, float)、LeonWeaponVisualController.PrepareGunEquipAfterKnifeHide(WeaponType)、LeonWeaponVisualController.SetEquippedWeapon(WeaponType)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool) 等共 6 处；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetKnifeVisible(bool visible)
    {
        if (nativePairKnifeActive)
        {
            visible = false;
        }
        else if (!visible)
        {
            UpdateSpareKnifeAppearance(null, false, Vector3.zero, Vector3.zero);
        }

        if (knifeVisualObjects != null)
        {
            // obj（当前游戏对象）：每轮从小刀视觉表现对象集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetKnifeVisible(bool)。
foreach (var obj in knifeVisualObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(visible);
                }
            }
        }

        if (knifeVisualRenderers != null)
        {
            // rendererItem（渲染器物品）：每轮从小刀视觉表现渲染器集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetKnifeVisible(bool)。
foreach (var rendererItem in knifeVisualRenderers)
            {
                if (rendererItem != null)
                {
                    rendererItem.enabled = visible;
                }
            }
        }

        if (visible && combatBladeBase != null)
        {
            KnifeCombatModule module = GetComponent<KnifeCombatModule>();
            if (module != null && module.configuration != null)
            {
                UpdateSpareKnifeAppearance(combatBladeBase, true, module.configuration.bladeBaseLocal, module.configuration.bladeTipLocal);
            }
        }
    }

    /// <summary>SetBackRifleVisible 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetBackRifleVisible(bool visible)
    {
        if (rifleBackRenderers == null)
        {
            return;
        }

        // r（当前渲染器）：每轮从步枪后方渲染器集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetBackRifleVisible(bool)。
foreach (var r in rifleBackRenderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    /// <summary>SetBackShotgunVisible 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.SetExclusiveBackLongGun(WeaponType?)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetBackShotgunVisible(bool visible)
    {
        if (shotgunBackRenderers == null)
        {
            return;
        }

        // r（当前渲染器）：每轮从霰弹枪后方渲染器集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetBackShotgunVisible(bool)。
foreach (var r in shotgunBackRenderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    /// <summary>SetHolsteredPistolVisible 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.OnVisualMarker(AnimationActionTransaction, VisualMarker)、LeonWeaponVisualController.RefreshHolsteredPistolVisual()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetHolsteredPistolVisible(bool visible)
    {
        if (!nativePistolHolsterBound && UsesNativePistolVisual)
        {
            nativePistolHolsterBound = true;
            // 已保存的正式场景可能仍引用历史挂枪模型。优先使用角色服装自带的枪套内手枪。
            var fittedPistol = transform.Find("24_pistol_0.45_0_0");
            if (fittedPistol != null && fittedPistol.TryGetComponent(out SkinnedMeshRenderer renderer))
            {
                if (holsteredPistolRenderers != null)
                {
                    foreach (var previous in holsteredPistolRenderers)
                    {
                        if (previous != null && previous != renderer)
                        {
                            previous.enabled = false;
                        }
                    }
                }
                holsteredPistolRenderers = new Renderer[] { renderer };
            }
        }

        if (holsteredPistolRenderers == null)
        {
            return;
        }

        // rendererItem（渲染器物品）：每轮从已收起手枪渲染器集合取得一个元素，仅代表当前这一项。使用位置：LeonWeaponVisualController.SetHolsteredPistolVisible(bool)。
foreach (var rendererItem in holsteredPistolRenderers)
        {
            if (rendererItem != null)
            {
                rendererItem.enabled = visible;
            }
        }
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponVisualController.PrepareGunEquipAfterKnifeHide(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    private static bool IsGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }
}
