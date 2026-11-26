using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>武器、命中与战斗组件 DestructibleWoodenBox，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
public class DestructibleWoodenBox : MonoBehaviour, IDamageable, IInteractable
{
    /// <summary>场景箱体集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<DestructibleWoodenBox> sceneBoxes = new List<DestructibleWoodenBox>();
    /// <summary>近战视线命中结果。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly RaycastHit[] meleeLosHits = new RaycastHit[16];
    /// <summary>交互者碰撞体缓冲区。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<Collider> interactorColliderBuffer = new List<Collider>(32);
    /// <summary>最大生命值。</summary>
    [Header("耐久")]
    public float maxHealth = 15f;
    /// <summary>破坏持续时间。</summary>
    [Header("破碎表现")]
    public float breakDuration = 0.75f;
    /// <summary>散射距离。</summary>
    public float scatterDistance = 0.75f;
    /// <summary>向上散射。</summary>
    public float upwardScatter = 0.25f;
    /// <summary>旋转度。</summary>
    public float spinDegrees = 75f;
    /// <summary>最终缩放。</summary>
    public float finalScale = 0.15f;
    /// <summary>是否隐藏之后破坏。</summary>
    public bool hideAfterBreak = true;
    /// <summary>是否移除子级光源集合。</summary>
    [Header("导入模型清理")]
    public bool removeChildLights = true;
    /// <summary>是否移除子级摄像机集合。</summary>
    public bool removeChildCameras = true;
    /// <summary>是否使用程序化碎屑。</summary>
    [Header("程序化碎片")]
    public bool useProceduralDebris = true;
    [SerializeField] private GameObject nativeBreakPieces;
    /// <summary>木板碎片数量。</summary>
    public int plankFragmentCount = 10;
    /// <summary>小碎片碎片数量。</summary>
    public int chipFragmentCount = 18;
    /// <summary>木板长度范围。</summary>
    public Vector2 plankLengthRange = new Vector2(0.35f, 0.75f);
    /// <summary>木板宽度范围。</summary>
    public Vector2 plankWidthRange = new Vector2(0.06f, 0.14f);
    /// <summary>木板厚度范围。</summary>
    public Vector2 plankThicknessRange = new Vector2(0.025f, 0.055f);
    /// <summary>碎片冲量。</summary>
    public float fragmentImpulse = 3.2f;
    /// <summary>碎片向上冲量。</summary>
    public float fragmentUpwardImpulse = 1.2f;
    /// <summary>碎片扭矩。</summary>
    public float fragmentTorque = 9f;
    /// <summary>碎片存活时长。</summary>
    public float fragmentLifetime = 3.5f;
    /// <summary>碎片缩小持续时间。</summary>
    public float fragmentShrinkDuration = 0.8f;
    /// <summary>是否生成尘土效果。</summary>
    public bool spawnDustEffect = true;
    /// <summary>尘土粒子数量。</summary>
    public int dustParticleCount = 28;
    /// <summary>尘土颜色。</summary>
    public Color dustColor = new Color(0.48f, 0.36f, 0.24f, 0.55f);
    /// <summary>碎片材质覆盖。</summary>
    [Header("碎片材质")]
    public Material fragmentMaterialOverride;
    /// <summary>是否力度运行时碎片材质。</summary>
    public bool forceRuntimeFragmentMaterial = true;
    /// <summary>碎片色调。</summary>
    public Color fragmentTint = new Color(0.72f, 0.52f, 0.31f, 1f);
    /// <summary>是否允许近战破坏。</summary>
    [Header("体术交互")]
    public bool allowMeleeBreak = true;
    /// <summary>近战破坏范围。</summary>
    public float meleeBreakRange = 2f;
    /// <summary>是否需要近战朝向。</summary>
    public bool requireMeleeFacing = false;
    /// <summary>近战朝向角度。</summary>
    [Range(10f, 180f)]
    public float meleeFacingAngle = 130f;
    /// <summary>近战提示文字。</summary>
    public string meleePromptText = "破坏木箱";
    [Tooltip("木桶使用踢击桶身。放在高处时仍使用肘击。")]
    public bool isBarrel;
    /// <summary>是否生成战利品事件破坏。</summary>
    [Header("箱内掉落")]
    public bool spawnLootOnBreak = true;
    /// <summary>是否使用掉落表战利品。</summary>
    public bool useDropTableLoot = true;
    /// <summary>是否启用后与敌人死亡一样独立摇取金钱。启用后与敌人死亡一样独立摇取金钱。</summary>
    [Tooltip("启用后与敌人死亡一样独立摇取金钱")]
    public bool includeMoneyDrop = true;
    /// <summary>是否仅用于特殊奖励箱时开启。关闭时保留与敌人死亡相同的不掉落权重；仅用于特殊奖励箱时开启。</summary>
    [Tooltip("关闭时保留与敌人死亡相同的不掉落权重；仅用于特殊奖励箱时开启")]
    public bool guaranteeDropTableLoot = false;
    /// <summary>固定战利品。</summary>
    public ItemData[] fixedLoot;
    /// <summary>固定战利品数量范围。</summary>
    public Vector2Int fixedLootCountRange = new Vector2Int(1, 1);
    /// <summary>战利品散射半径。</summary>
    public float lootScatterRadius = 0.35f;
    /// <summary>破坏效果。</summary>
    [Header("可选特效")]
    public ParticleSystem breakEffect;
    /// <summary>破坏音频。</summary>
    public AudioSource breakAudio;
    [Tooltip("木箱尚未破坏时的碰撞声")]
    public AudioEventData hitAudioEvent;
    [Tooltip("木箱首次破坏时的断裂声")]
    public AudioEventData breakAudioEvent;
    [Tooltip("随破坏播放一次的碎片声")]
    public AudioEventData debrisAudioEvent;

    internal bool HasDamageAudio => hitAudioEvent != null || breakAudioEvent != null;
    /// <summary>当前生命值。</summary>
    private float currentHealth;
    /// <summary>是否损坏。</summary>
    private bool broken;
    internal bool IsBroken => broken;
    /// <summary>缓存碰撞体集合。</summary>
    private Collider[] cachedColliders;
    /// <summary>缓存渲染器集合。</summary>
    private Renderer[] cachedRenderers;
    /// <summary>碎片集合。</summary>
    private FragmentState[] fragments;
    /// <summary>运行时碎片材质。</summary>
    private Material runtimeFragmentMaterial;
    /// <summary>运行时尘土材质。</summary>
    private Material runtimeDustMaterial;
    /// <summary>上一次交互者。</summary>
    private Transform lastInteractor;
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => meleeBreakRange;

    /// <summary>武器、命中与战斗的结构化数据 FragmentState；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct FragmentState
    {
        /// <summary>变换。</summary>
        public Transform transform;
        /// <summary>起始位置。</summary>
        public Vector3 startPosition;
        /// <summary>起始旋转。</summary>
        public Quaternion startRotation;
        /// <summary>起始缩放。</summary>
        public Vector3 startScale;
        /// <summary>目标位置。</summary>
        public Vector3 targetPosition;
        /// <summary>目标旋转。</summary>
        public Quaternion targetRotation;
        /// <summary>目标缩放。</summary>
        public Vector3 targetScale;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DestructibleWoodenBox.CacheFragments(Vector3)、DestructibleWoodenBox.CacheSceneReferences()、DestructibleWoodenBox.RemoveImportedChildCameras()、DestructibleWoodenBox.RemoveImportedChildLights()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        RemoveImportedChildLights();
        RemoveImportedChildCameras();
        CacheSceneReferences();
        CacheFragments(transform.position + Vector3.up * 0.5f);
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Register(IInteractable)、List&lt;DestructibleWoodenBox&gt;.Add(DestructibleWoodenBox)、List&lt;DestructibleWoodenBox&gt;.Contains(DestructibleWoodenBox)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        if (!sceneBoxes.Contains(this))
        {
            sceneBoxes.Add(this);
        }

        InteractionSystem.Register(this);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Unregister(IInteractable)、List&lt;DestructibleWoodenBox&gt;.Remove(DestructibleWoodenBox)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        sceneBoxes.Remove(this);
        InteractionSystem.Unregister(this);
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDestroy()
    {
        if (runtimeFragmentMaterial != null)
        {
            Destroy(runtimeFragmentMaterial);
        }

        if (runtimeDustMaterial != null)
        {
            Destroy(runtimeDustMaterial);
        }
    }

    /// <summary>CacheSceneReferences 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Collider&gt;(bool)、Component.GetComponentsInChildren&lt;Renderer&gt;(bool)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CacheSceneReferencesIfNeeded()、DestructibleWoodenBox.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Collider&gt;(bool)、Component.GetComponentsInChildren&lt;Renderer&gt;(bool)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheSceneReferences()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>RemoveImportedChildLights 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Light&gt;(bool)、Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Light&gt;(bool)、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RemoveImportedChildLights()
    {
        if (!removeChildLights)
        {
            return;
        }

        // 子级光源集合。
Light[] childLights = GetComponentsInChildren<Light>(true);
        // 当前遍历索引。
for (int i = childLights.Length - 1; i >= 0; i--)
        {
            // 子级光源。
Light childLight = childLights[i];
            if (childLight == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(childLight);
            }
            else
            {
                childLight.enabled = false;
            }
        }
    }

    /// <summary>RemoveImportedChildCameras 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Camera&gt;(bool)、Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Camera&gt;(bool)、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RemoveImportedChildCameras()
    {
        if (!removeChildCameras)
        {
            return;
        }

        // 子级摄像机集合。
Camera[] childCameras = GetComponentsInChildren<Camera>(true);
        // 当前遍历索引。
for (int i = childCameras.Length - 1; i >= 0; i--)
        {
            // 子级摄像机。
Camera childCamera = childCameras[i];
            if (childCamera == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(childCamera);
            }
            else
            {
                childCamera.enabled = false;
            }
        }
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.TakeDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DestructibleWoodenBox.TakeDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position + Vector3.up * 0.5f);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.Break(Vector3)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.TakeDamage(float)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；下游（含内部回调体） DestructibleWoodenBox.Break(Vector3)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (broken || damage <= 0f)
        {
            return;
        }

        currentHealth -= damage;
        if (currentHealth > 0f)
        {
            if (hitAudioEvent != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play3D(hitAudioEvent, hitPoint);
            }

            return;
        }

        Break(hitPoint);
    }

    /// <summary>CanInteract 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.GetDistanceTo(Vector3)、DestructibleWoodenBox.IsInteractorFacingBox(Transform)、DestructibleWoodenBox.IsMeleePathClear(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.TryBreakByMelee(Transform)、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)、LeonCombat.TryStartWoodenBoxMelee(DestructibleWoodenBox)；下游（含内部回调体） DestructibleWoodenBox.GetDistanceTo(Vector3)、DestructibleWoodenBox.IsInteractorFacingBox(Transform)、DestructibleWoodenBox.IsMeleePathClear(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    public bool CanInteract(Transform interactor)
    {
        if (!allowMeleeBreak || broken || interactor == null)
        {
            return false;
        }

        Vector3 contact = ResolveMeleeHitPoint(interactor);
        Vector3 horizontalOffset = contact - interactor.position;
        horizontalOffset.y = 0f;
        if (horizontalOffset.magnitude > Mathf.Min(meleeBreakRange, 1.15f) ||
            Mathf.Abs(contact.y - ResolveMeleeOrigin(interactor).y) > 0.95f)
        {
            return false;
        }

        if (requireMeleeFacing && !IsInteractorFacingBox(interactor))
        {
            return false;
        }

        if (!IsMeleePathClear(interactor))
        {
            return false;
        }

        lastInteractor = interactor;
        return true;
    }

    /// <summary>Interact 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonCombat&gt;()、DestructibleWoodenBox.TryBreakByMelee(Transform)、LeonCombat.TryStartWoodenBoxMelee(DestructibleWoodenBox)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonCombat&gt;()、DestructibleWoodenBox.TryBreakByMelee(Transform)、LeonCombat.TryStartWoodenBoxMelee(DestructibleWoodenBox)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Interact()
    {
        // 交互者。
Transform interactor;
        if (lastInteractor != null)
        {
            interactor = lastInteractor;
        }
        else
        {
            if (LeonController.Instance != null)
            {
                interactor = LeonController.Instance.transform;
            }
            else
            {
                interactor = null;
            }
        }

        // 战斗。
LeonCombat combat;
        if (interactor != null)
        {
            combat = interactor.GetComponent<LeonCombat>();
        }
        else
        {
            combat = null;
        }

        if (combat != null)
        {
            combat.TryStartWoodenBoxMelee(this);
            return;
        }

        TryBreakByMelee(interactor);
    }

    /// <summary>GetPromptText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetPromptText()
    {
        if (broken)
        {
            return string.Empty;
        }
        else
        {
            return meleePromptText;
        }
    }

    /// <summary>TryBreakByMelee 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Interact()、DestructibleWoodenBox.TryBreakNearestByMelee(Transform, out DestructibleWoodenBox)、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)；下游（含内部回调体） DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    public bool TryBreakByMelee(Transform interactor)
    {
        if (!CanInteract(interactor))
        {
            return false;
        }

        Break(ResolveMeleeHitPoint(interactor));
        return true;
    }

    /// <summary>TryBreakNearestByMelee 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.TryBreakByMelee(Transform)、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DestructibleWoodenBox.TryBreakByMelee(Transform)、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    /// <param name="target">目标可破坏木制箱体。输出参数，由本入口写入并交给调用方。</param>
    internal Vector3 GetMeleeKickPoint(Transform interactor)
    {
        return ResolveMeleeHitPoint(interactor);
    }

    internal float GetMeleeDistance(Transform interactor)
    {
        Vector3 offset = ResolveMeleeHitPoint(interactor) - interactor.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    internal bool TryBreakAtAlignedContact(Transform interactor, FullBodyActionType action)
    {
        float reach = 0.74f;
        if (action == FullBodyActionType.WoodenBoxKick)
        {
            reach = 0.86f;
        }

        if (!CanInteract(interactor) || GetMeleeDistance(interactor) > reach)
        {
            return false;
        }

        Vector3 contact = ResolveMeleeHitPoint(interactor);
        Vector3 toContact = Vector3.ProjectOnPlane(contact - interactor.position, Vector3.up);
        if (toContact.sqrMagnitude > 0.0025f && Vector3.Angle(interactor.forward, toContact) > 38f)
        {
            return false;
        }

        Break(contact);
        return true;
    }

    internal FullBodyActionType SelectMeleeAction(Transform interactor, WeaponType weapon)
    {
        Bounds bounds = ResolveWorldBounds();
        if (bounds.min.y - interactor.position.y >= 0.44f)
        {
            if (weapon == WeaponType.Pistol)
            {
                return FullBodyActionType.WoodenBoxPistolElbow;
            }
            return FullBodyActionType.WoodenBoxElbow;
        }

        if (isBarrel)
        {
            return FullBodyActionType.WoodenBoxKick;
        }

        Vector3 near = bounds.ClosestPoint(interactor.position);
        near.y = interactor.position.y;
        if ((near - interactor.position).sqrMagnitude > 0.60f * 0.60f)
        {
            // 踩踏用于脚边箱体；较远的地面箱体使用前踢，避免隔着板沿原地踩空。
            return FullBodyActionType.WoodenBoxKick;
        }
        return FullBodyActionType.WoodenBoxStomp;
    }

    internal bool TryBreakAtContact(Transform interactor, Vector3 start, Vector3 end, float radius)
    {
        if (!CanInteract(interactor))
        {
            return false;
        }

        Vector3 tip = end + (end - start).normalized * radius;
        foreach (Collider shape in cachedColliders)
        {
            if (shape == null || !shape.enabled || shape.isTrigger)
            {
                continue;
            }

            for (int sample = 0; sample <= 3; sample++)
            {
                Vector3 strike = Vector3.Lerp(start, tip, sample / 3f);
                Vector3 contact = shape.ClosestPoint(strike);
                if ((contact - strike).sqrMagnitude <= radius * radius)
                {
                    Break(contact);
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryBreakNearestByMelee(Transform interactor, out DestructibleWoodenBox target)
    {
        if (!TryFindNearestByMelee(interactor, out target))
        {
            return false;
        }

        return target.TryBreakByMelee(interactor);
    }

    /// <summary>TryFindNearestByMelee 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.GetDistanceTo(Vector3)、List&lt;DestructibleWoodenBox&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.TryBreakNearestByMelee(Transform, out DestructibleWoodenBox)、LeonCombat.TryBreakNearbyWoodenBox()；下游（含内部回调体） DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.GetDistanceTo(Vector3)、List&lt;DestructibleWoodenBox&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    /// <param name="target">目标可破坏木制箱体。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindNearestByMelee(Transform interactor, out DestructibleWoodenBox target)
    {
        target = null;
        if (interactor == null)
        {
            return false;
        }

        // 最佳距离。
float bestDistance = float.MaxValue;
        // 当前遍历索引。
for (int i = sceneBoxes.Count - 1; i >= 0; i--)
        {
            // 箱体。
DestructibleWoodenBox box = sceneBoxes[i];
            if (box == null)
            {
                sceneBoxes.RemoveAt(i);
                continue;
            }

            if (!box.CanInteract(interactor))
            {
                continue;
            }

            // 距离。
float distance = box.GetMeleeDistance(interactor);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            target = box;
        }

        return target != null;
    }

    /// <summary>TestBreak 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.Break(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DestructibleWoodenBox.Break(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("测试破碎")]
    public void TestBreak()
    {
        Break(transform.position + transform.forward * -0.5f + Vector3.up * 0.5f);
    }

    /// <summary>Break 的业务处理入口；本方法直接依赖 AudioSource.Play()、DestructibleWoodenBox.CacheFragments(Vector3)、DestructibleWoodenBox.PlayBreakAnimation()、DestructibleWoodenBox.ResolveFragmentMaterial()、DestructibleWoodenBox.ResolveWorldBounds()、DestructibleWoodenBox.SetRenderersVisible(bool)、DestructibleWoodenBox.SpawnDust(Vector3, Bounds) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.TakeDamage(float, Vector3)、DestructibleWoodenBox.TestBreak()、DestructibleWoodenBox.TryBreakByMelee(Transform)；下游（含内部回调体） AudioSource.Play()、DestructibleWoodenBox.CacheFragments(Vector3)、DestructibleWoodenBox.PlayBreakAnimation()、DestructibleWoodenBox.ResolveFragmentMaterial()、DestructibleWoodenBox.ResolveWorldBounds()、DestructibleWoodenBox.SetRenderersVisible(bool)、DestructibleWoodenBox.SpawnDust(Vector3, Bounds) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    private void Break(Vector3 hitPoint)
    {
        if (broken)
        {
            return;
        }

        broken = true;
        CacheFragments(hitPoint);
        // 破坏包围盒。
Bounds breakBounds = ResolveWorldBounds();
        // 碎片材质。
Material fragmentMaterial = ResolveFragmentMaterial();
        InteractionSystem.Unregister(this);
        if (cachedColliders != null)
        {
            // item（当前碰撞体）：每轮从缓存碰撞体集合取得一个元素，仅代表当前这一项。使用位置：DestructibleWoodenBox.Break(Vector3)。
foreach (Collider item in cachedColliders)
            {
                if (item != null)
                {
                    item.enabled = false;
                }
            }
        }

        if (breakEffect != null)
        {
            breakEffect.Play(true);
        }

        AudioManager audio = AudioManager.Instance;
        if (breakAudioEvent != null && audio != null)
        {
            audio.Play3D(breakAudioEvent, hitPoint);
        }
        else if (breakAudio != null)
        {
            breakAudio.Play();
        }

        if (debrisAudioEvent != null && audio != null)
        {
            audio.Play3D(debrisAudioEvent, hitPoint);
        }

        StopAllCoroutines();
        SpawnLoot(breakBounds.center);
        if (useProceduralDebris)
        {
            SetRenderersVisible(false);
            if (nativeBreakPieces != null)
            {
                SpawnNativeBreakPieces(hitPoint);
            }
            else
            {
                SpawnProceduralDebris(hitPoint, breakBounds, fragmentMaterial);
            }
            SpawnDust(hitPoint, breakBounds);
            return;
        }

        StartCoroutine(PlayBreakAnimation());
    }

    /// <summary>SpawnLoot 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ResolveLootPosition(Vector3)、DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)、DropManager.OnContainerBreak(Vector3, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） DestructibleWoodenBox.ResolveLootPosition(Vector3)、DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)、DropManager.OnContainerBreak(Vector3, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="center">中心。</param>
    private void SpawnLoot(Vector3 center)
    {
        if (!spawnLootOnBreak)
        {
            return;
        }

        // 掉落管理器。
DropManager dropManager = DropManager.Instance;
        // 是否已生成固定战利品。
bool spawnedFixedLoot = SpawnFixedLoot(dropManager, center);
        if (!useDropTableLoot || dropManager == null)
        {
            return;
        }

        bool guaranteeItem = guaranteeDropTableLoot || gameObject.scene.path ==
            "Assets/RE4ChapterOneVillage/Lighting/VillageBattleLighting.unity";
        dropManager.OnContainerBreak(ResolveLootPosition(center), guaranteeItem && !spawnedFixedLoot, includeMoneyDrop);
    }

    /// <summary>SpawnFixedLoot 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ResolveLootPosition(Vector3)、DropManager.SpawnItemPickup(ItemData, int, Vector3)、Mathf.Max(int, int)、Mathf.Min(int, int)、Random.Range(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnLoot(Vector3)；下游（含内部回调体） DestructibleWoodenBox.ResolveLootPosition(Vector3)、DropManager.SpawnItemPickup(ItemData, int, Vector3)、Mathf.Max(int, int)、Mathf.Min(int, int)、Random.Range(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dropManager">掉落管理器。</param>
    /// <param name="center">中心。</param>
    private bool SpawnFixedLoot(DropManager dropManager, Vector3 center)
    {
        if (dropManager == null || fixedLoot == null || fixedLoot.Length == 0)
        {
            return false;
        }

        // 是否已生成任意。
bool spawnedAny = false;
        // 最小数量。
int minCount = Mathf.Max(1, Mathf.Min(fixedLootCountRange.x, fixedLootCountRange.y));
        // 最大数量。
int maxCount = Mathf.Max(minCount, Mathf.Max(fixedLootCountRange.x, fixedLootCountRange.y));
        // 当前遍历索引。
for (int i = 0; i < fixedLoot.Length; i++)
        {
            // 当前物品数据配置。
ItemData item = fixedLoot[i];
            if (item == null)
            {
                continue;
            }

            // 数量。
int count = Random.Range(minCount, maxCount + 1);
            dropManager.SpawnItemPickup(item, count, ResolveLootPosition(center));
            spawnedAny = true;
        }

        return spawnedAny;
    }

    /// <summary>ResolveLootPosition 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)、DestructibleWoodenBox.SpawnLoot(Vector3)；下游（含内部回调体） Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="center">中心。</param>
    private Vector3 ResolveLootPosition(Vector3 center)
    {
        // 偏移量。
Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, lootScatterRadius);
        return center + new Vector3(offset.x, 0f, offset.y);
    }

    /// <summary>IsInteractorFacingBox 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CanInteract(Transform)；下游（含内部回调体） DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    private bool IsInteractorFacingBox(Transform interactor)
    {
        // 目标箱体。
Vector3 toBox = ResolveMeleeHitPoint(interactor) - interactor.position;
        toBox.y = 0f;
        if (toBox.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        // 前向。
Vector3 forward = interactor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        return Vector3.Angle(forward.normalized, toBox.normalized) <= meleeFacingAngle * 0.5f;
    }

    /// <summary>
    /// 近战路径遮挡校验：从角色腰部向木箱最近碰撞点发射射线，
    /// 若先命中墙体/门/建筑等非木箱实体碰撞体则判定不可交互。
    /// </summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CanInteract(Transform)；下游（含内部回调体） Component.GetComponentsInChildren&lt;Collider&gt;(bool, List&lt;Collider&gt;)、DestructibleWoodenBox.CacheSceneReferencesIfNeeded()、DestructibleWoodenBox.IsColliderInArray(Collider, Collider[])、DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)、List&lt;Collider&gt;.Clear()、List&lt;Collider&gt;.Contains(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    private bool IsMeleePathClear(Transform interactor)
    {
        CacheSceneReferencesIfNeeded();
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            return true;
        }

        // 原点。
Vector3 origin = ResolveMeleeOrigin(interactor);
        Vector3 closest = ResolveMeleeHitPoint(interactor);
        if (HasClearMeleeLine(interactor, origin, closest))
        {
            return true;
        }

        // 抬高视线检测桌面和货架上的箱体，仍由场景碰撞体判断遮挡。
        Bounds bounds = ResolveWorldBounds();
        Vector3 upperOrigin = interactor.position + Vector3.up * 1.45f;
        Vector3 upperContact = bounds.ClosestPoint(upperOrigin);
        return HasClearMeleeLine(interactor, upperOrigin, upperContact);
    }

    private bool HasClearMeleeLine(Transform interactor, Vector3 origin, Vector3 targetPoint)
    {
        // targetPoint（目标点）：保存 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.IsMeleePathClear(Transform)。
        // 方向。
Vector3 direction = targetPoint - origin;
        // 距离。
float distance = direction.magnitude;
        if (distance < 0.05f)
        {
            return true;
        }

        // 归一化方向。
Vector3 normalizedDir = direction / distance;
        // 命中数量。
int hitCount = Physics.RaycastNonAlloc(origin, normalizedDir, meleeLosHits, distance, ~0, QueryTriggerInteraction.Ignore);
        // 收集交互者自身碰撞体，射线需忽略它们
        interactorColliderBuffer.Clear();
        interactor.GetComponentsInChildren(false, interactorColliderBuffer);
        // 最近阻挡中距离。
float nearestBlockingDistance = float.MaxValue;
        // 最近阻挡中碰撞体。
Collider nearestBlockingCollider = null;
        // 当前遍历索引。
for (int i = 0; i < hitCount; i++)
        {
            // 命中。
ref RaycastHit hit = ref meleeLosHits[i];
            if (hit.collider == null || hit.distance >= nearestBlockingDistance)
            {
                continue;
            }

            // 跳过交互者自身碰撞体
            if (interactorColliderBuffer.Contains(hit.collider))
            {
                continue;
            }

            nearestBlockingDistance = hit.distance;
            nearestBlockingCollider = hit.collider;
        }

        if (nearestBlockingCollider == null)
        {
            return true;
        }

        // 最近有效碰撞体必须属于当前木箱
        return IsColliderInArray(nearestBlockingCollider, cachedColliders);
    }

    /// <summary>IsColliderInArray 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.IsMeleePathClear(Transform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">碰撞体。</param>
    /// <param name="array">数组。</param>
    private static bool IsColliderInArray(Collider collider, Collider[] array)
    {
        if (array == null)
        {
            return false;
        }

        // 当前遍历索引。
for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == collider)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ResolveMeleeHitPoint 的业务处理入口；本方法直接依赖 Collider.ClosestPoint(Vector3)、DestructibleWoodenBox.CacheSceneReferencesIfNeeded()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.IsInteractorFacingBox(Transform)、DestructibleWoodenBox.IsMeleePathClear(Transform)、DestructibleWoodenBox.TryBreakByMelee(Transform)；下游（含内部回调体） Collider.ClosestPoint(Vector3)、DestructibleWoodenBox.CacheSceneReferencesIfNeeded()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">交互者。</param>
    private Vector3 ResolveMeleeHitPoint(Transform interactor)
    {
        CacheSceneReferencesIfNeeded();
        // 后备。
Vector3 fallback = transform.position + Vector3.up * 0.5f;
        if (interactor == null)
        {
            return fallback;
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            return fallback;
        }

        // 原点。
Vector3 origin = ResolveMeleeOrigin(interactor);
        // 最佳距离。
float bestDistance = float.MaxValue;
        // 最佳点。
Vector3 bestPoint = fallback;
        // 当前遍历索引。
for (int i = 0; i < cachedColliders.Length; i++)
        {
            // 当前碰撞体。
Collider item = cachedColliders[i];
            if (item == null || !item.enabled || item.isTrigger)
            {
                continue;
            }

            // 点。
Vector3 point = item.ClosestPoint(origin);
            // 距离。
float distance = (point - origin).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestPoint = point;
        }

        return bestPoint;
    }

    private Vector3 ResolveMeleeOrigin(Transform interactor)
    {
        Vector3 origin = interactor.position + Vector3.up * 0.8f;
        Bounds bounds = ResolveWorldBounds();
        if (bounds.min.y - interactor.position.y >= 0.44f)
        {
            // 桌面的近战从肘部高度检查净空，避免射线先命中桌沿。
            origin.y = Mathf.Clamp(bounds.center.y, origin.y, interactor.position.y + 1.38f);
        }

        return origin;
    }

    /// <summary>CacheSceneReferencesIfNeeded 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.CacheSceneReferences()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.GetDistanceTo(Vector3)、DestructibleWoodenBox.IsMeleePathClear(Transform)、DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)；下游（含内部回调体） DestructibleWoodenBox.CacheSceneReferences()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheSceneReferencesIfNeeded()
    {
        if (cachedColliders == null || cachedRenderers == null)
        {
            CacheSceneReferences();
        }
    }

    /// <summary>SpawnProceduralDebris 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)、Mathf.Max(int, int)、Random.Range(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)、Mathf.Max(int, int)、Random.Range(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="bounds">包围盒。</param>
    /// <param name="fragmentMaterial">碎片材质。</param>
    private void SpawnNativeBreakPieces(Vector3 hitPoint)
    {
        GameObject fragmentsRoot = Instantiate(nativeBreakPieces, transform);
        foreach (MeshFilter mesh in fragmentsRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || !mesh.TryGetComponent(out MeshRenderer renderer))
            {
                continue;
            }

            // 碎片使用原版拆分网格和物理散落。
            GameObject part = mesh.gameObject;
            Bounds localBounds = mesh.sharedMesh.bounds;
            BoxCollider shape = part.AddComponent<BoxCollider>();
            shape.center = localBounds.center;
            shape.size = Vector3.Max(localBounds.size, Vector3.one * 0.012f);
            ConfigureFragmentCollision(shape);
            Rigidbody body = part.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(renderer.bounds.size.magnitude * 0.4f, 0.025f, 0.3f);
            Vector3 direction = Vector3.ProjectOnPlane(renderer.bounds.center - hitPoint, Vector3.up).normalized;
            body.AddForce(direction * Random.Range(0.35f, 1.1f) * fragmentImpulse
                + Vector3.up * Random.Range(0.25f, 0.8f) * fragmentUpwardImpulse, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * fragmentTorque, ForceMode.VelocityChange);
        }

        Destroy(fragmentsRoot, fragmentLifetime);
    }

    private void SpawnProceduralDebris(Vector3 hitPoint, Bounds bounds, Material fragmentMaterial)
    {
        // 木板数量。
int plankCount = Mathf.Max(0, plankFragmentCount);
        // 小碎片数量。
int chipCount = Mathf.Max(0, chipFragmentCount);
        // 当前遍历索引。
for (int i = 0; i < plankCount; i++)
        {
            // 尺寸。
Vector3 size = new Vector3(Random.Range(plankWidthRange.x, plankWidthRange.y), Random.Range(plankThicknessRange.x, plankThicknessRange.y), Random.Range(plankLengthRange.x, plankLengthRange.y));
            CreateFragment(bounds, hitPoint, size, fragmentMaterial, "木箱木板碎片");
        }

        // 当前遍历索引。
for (int i = 0; i < chipCount; i++)
        {
            // 小碎片尺寸。
float chipSize = Random.Range(0.045f, 0.12f);
            // 尺寸。
Vector3 size = new Vector3(chipSize * Random.Range(0.6f, 1.4f), chipSize * Random.Range(0.35f, 0.8f), chipSize * Random.Range(0.7f, 1.8f));
            CreateFragment(bounds, hitPoint, size, fragmentMaterial, "木箱木屑");
        }
    }

    /// <summary>CreateFragment 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3)、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)、GameObject.AddComponent&lt;Rigidbody&gt;()、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Renderer&gt;()、Mathf.Abs(float)、MonoBehaviour.StartCoroutine(IEnumerator) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)；下游（含内部回调体） DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3)、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)、GameObject.AddComponent&lt;Rigidbody&gt;()、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Renderer&gt;()、Mathf.Abs(float)、MonoBehaviour.StartCoroutine(IEnumerator) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bounds">包围盒。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="size">尺寸。</param>
    /// <param name="fragmentMaterial">碎片材质。</param>
    /// <param name="objectName">对象名称。</param>
    private void CreateFragment(Bounds bounds, Vector3 hitPoint, Vector3 size, Material fragmentMaterial, string objectName)
    {
        // 生成位置。
Vector3 spawnPosition = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z));
        // 碎片。
GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fragment.name = objectName;
        ConfigureFragmentCollision(fragment.GetComponent<BoxCollider>());
        fragment.transform.position = spawnPosition;
        fragment.transform.rotation = transform.rotation * Random.rotationUniform;
        fragment.transform.localScale = ClampFragmentSize(size, bounds.size);
        // 渲染器。
Renderer renderer = fragment.GetComponent<Renderer>();
        if (renderer != null && fragmentMaterial != null)
        {
            renderer.sharedMaterial = fragmentMaterial;
        }

        // 身体。
Rigidbody body = fragment.AddComponent<Rigidbody>();
        body.mass = Random.Range(0.05f, 0.16f);
        body.linearDamping = 0.25f;
        body.angularDamping = 0.05f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 方向。
Vector3 direction = spawnPosition - hitPoint;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Random.onUnitSphere;
        }

        direction.y = Mathf.Abs(direction.y) * 0.35f + Random.Range(0.15f, 0.45f);
        direction.Normalize();
        // 冲量。
Vector3 impulse = direction * Random.Range(fragmentImpulse * 0.65f, fragmentImpulse * 1.35f);
        impulse += Vector3.up * Random.Range(fragmentUpwardImpulse * 0.45f, fragmentUpwardImpulse * 1.2f);
        body.AddForce(impulse, ForceMode.Impulse);
        body.AddTorque(Random.onUnitSphere * Random.Range(fragmentTorque * 0.45f, fragmentTorque * 1.2f), ForceMode.Impulse);
        StartCoroutine(ShrinkAndDestroy(fragment.transform, fragmentLifetime, fragmentShrinkDuration));
    }

    private static void ConfigureFragmentCollision(Collider fragment)
    {
        // 木屑可以撞地滚落，玩家连贯移动与敌人寻路不会被弹起的碎片卡住。
        fragment.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        fragment.excludeLayers |= LayerMask.GetMask("player", "Enemy", "EnemyHitbox");
        LeonController leon = LeonController.Instance;
        if (leon == null)
        {
            leon = Object.FindFirstObjectByType<LeonController>();
        }
        if (leon != null)
        {
            Collider[] playerColliders = leon.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < playerColliders.Length; index++)
            {
                Collider playerCollider = playerColliders[index];
                if (playerCollider != null && playerCollider != fragment)
                {
                    Physics.IgnoreCollision(fragment, playerCollider, true);
                }
            }
        }
    }

    /// <summary>ClampFragmentSize 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="size">尺寸。</param>
    /// <param name="boundsSize">包围盒尺寸。</param>
    private Vector3 ClampFragmentSize(Vector3 size, Vector3 boundsSize)
    {
        // 最大半尺寸。
float maxExtent = Mathf.Max(0.08f, Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z)) * 0.75f);
        return new Vector3(Mathf.Clamp(size.x, 0.02f, maxExtent), Mathf.Clamp(size.y, 0.015f, maxExtent), Mathf.Clamp(size.z, 0.03f, maxExtent));
    }

    /// <summary>ShrinkAndDestroy 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Object.Destroy(Object)、Vector3.Lerp(Vector3, Vector3, float)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Object.Destroy(Object)、Vector3.Lerp(Vector3, Vector3, float)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标变换节点。</param>
    /// <param name="lifetime">存活时长。</param>
    /// <param name="shrinkDuration">缩小持续时间。</param>
    private IEnumerator ShrinkAndDestroy(Transform target, float lifetime, float shrinkDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, lifetime));
        if (target == null)
        {
            yield break;
        }

        // 起始缩放。
Vector3 startScale = target.localScale;
        // 持续时间。
float duration = Mathf.Max(0.05f, shrinkDuration);
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            // 透明度。
float alpha = Mathf.Clamp01(elapsed / duration);
            target.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, alpha));
            yield return null;
        }

        if (target != null)
        {
            Destroy(target.gameObject);
        }
    }

    /// <summary>SpawnDust 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ResolveDustMaterial()、GameObject.AddComponent&lt;ParticleSystem&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;ParticleSystemRenderer&gt;()、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)、Object.Destroy(Object, float) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） DestructibleWoodenBox.ResolveDustMaterial()、GameObject.AddComponent&lt;ParticleSystem&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;ParticleSystemRenderer&gt;()、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)、Object.Destroy(Object, float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="bounds">包围盒。</param>
    private void SpawnDust(Vector3 hitPoint, Bounds bounds)
    {
        if (!spawnDustEffect || dustParticleCount <= 0)
        {
            return;
        }

        if (ChapterOneEffects.TryPlay(ChapterOneEffect.CrateBreak, bounds.ClosestPoint(hitPoint), Vector3.up))
        {
            return;
        }

        // 尘土对象。
GameObject dustObject = new GameObject("木箱破碎尘屑");
        dustObject.transform.position = Vector3.Lerp(hitPoint, bounds.center, 0.35f);
        // 粒子集合。
ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // 主要。
ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = dustColor;
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 发射。
ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(dustParticleCount, 1, short.MaxValue)) });
        // 形状。
ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.15f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.6f);
        // 粒子渲染器。
ParticleSystemRenderer particleRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            // 尘土材质。
Material dustMaterial = ResolveDustMaterial();
            if (dustMaterial != null)
            {
                particleRenderer.sharedMaterial = dustMaterial;
            }
        }

        particles.Play(true);
        Destroy(dustObject, 1.5f);
    }

    /// <summary>ResolveWorldBounds 的业务处理入口；本方法直接依赖 Bounds.Bounds(Vector3, Vector3)、Bounds.Encapsulate(Bounds)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） Bounds.Bounds(Vector3, Vector3)、Bounds.Encapsulate(Bounds)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Bounds ResolveWorldBounds()
    {
        // 是否具有包围盒。
bool hasBounds = false;
        // 包围盒。
Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        if (cachedRenderers != null)
        {
            // item（当前渲染器）：每轮从缓存渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数、DestructibleWoodenBox.ResolveWorldBounds()。
foreach (Renderer item in cachedRenderers)
            {
                if (item == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = item.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(item.bounds);
                }
            }
        }

        if (cachedColliders != null)
        {
            // item（当前碰撞体）：每轮从缓存碰撞体集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数、DestructibleWoodenBox.ResolveWorldBounds()。
foreach (Collider item in cachedColliders)
            {
                if (item == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = item.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(item.bounds);
                }
            }
        }

        // 尺寸。
Vector3 size = bounds.size;
        if (size.sqrMagnitude < 0.01f)
        {
            bounds.size = Vector3.one;
        }

        return bounds;
    }

    /// <summary>ResolveFragmentMaterial 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)、DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveSourceMaterial()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)、DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveSourceMaterial()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Material ResolveFragmentMaterial()
    {
        if (fragmentMaterialOverride != null && IsUsableMaterial(fragmentMaterialOverride) && !forceRuntimeFragmentMaterial)
        {
            return fragmentMaterialOverride;
        }

        // 来源材质。
Material sourceMaterial = ResolveSourceMaterial();
        if (forceRuntimeFragmentMaterial || !IsUsableMaterial(sourceMaterial))
        {
            return BuildRuntimeFragmentMaterial(sourceMaterial);
        }

        return sourceMaterial;
    }

    /// <summary>ResolveSourceMaterial 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.ResolveFragmentMaterial()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Material ResolveSourceMaterial()
    {
        if (cachedRenderers == null)
        {
            return null;
        }

        // item（当前渲染器）：每轮从缓存渲染器集合取得一个元素，仅代表当前这一项。使用位置：DestructibleWoodenBox.ResolveSourceMaterial()。
foreach (Renderer item in cachedRenderers)
        {
            if (item != null && item.sharedMaterial != null)
            {
                return item.sharedMaterial;
            }
        }

        return null;
    }

    /// <summary>BuildRuntimeFragmentMaterial 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ApplyMaterialColor(Material, Color)、DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture)、DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveMainTexture(Material)、DestructibleWoodenBox.ResolveShader(params string[])、Material.HasProperty(string)、Material.Material(Shader) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.ResolveFragmentMaterial()；下游（含内部回调体） DestructibleWoodenBox.ApplyMaterialColor(Material, Color)、DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture)、DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveMainTexture(Material)、DestructibleWoodenBox.ResolveShader(params string[])、Material.HasProperty(string)、Material.Material(Shader) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceMaterial">来源材质。</param>
    private Material BuildRuntimeFragmentMaterial(Material sourceMaterial)
    {
        if (runtimeFragmentMaterial != null)
        {
            return runtimeFragmentMaterial;
        }

        // 着色器。
Shader shader = ResolveShader("Standard", "Legacy Shaders/Diffuse", "Universal Render Pipeline/Lit", "Unlit/Texture");
        if (shader == null)
        {
            if (IsUsableMaterial(sourceMaterial))
            {
                return sourceMaterial;
            }
            else
            {
                return null;
            }
        }

        runtimeFragmentMaterial = new Material(shader);
        runtimeFragmentMaterial.name = "木箱碎片运行时材质";
        runtimeFragmentMaterial.hideFlags = HideFlags.DontSave;
        // 当前处理的纹理资源。
Texture texture = ResolveMainTexture(sourceMaterial);
        ApplyMaterialTexture(runtimeFragmentMaterial, texture);
        ApplyMaterialColor(runtimeFragmentMaterial, fragmentTint);
        if (runtimeFragmentMaterial.HasProperty("_Glossiness"))
        {
            runtimeFragmentMaterial.SetFloat("_Glossiness", 0f);
        }

        if (runtimeFragmentMaterial.HasProperty("_Metallic"))
        {
            runtimeFragmentMaterial.SetFloat("_Metallic", 0f);
        }

        return runtimeFragmentMaterial;
    }

    /// <summary>ResolveDustMaterial 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ApplyMaterialColor(Material, Color)、DestructibleWoodenBox.ResolveShader(params string[])、Material.HasProperty(string)、Material.Material(Shader)、Material.SetFloat(string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnDust(Vector3, Bounds)；下游（含内部回调体） DestructibleWoodenBox.ApplyMaterialColor(Material, Color)、DestructibleWoodenBox.ResolveShader(params string[])、Material.HasProperty(string)、Material.Material(Shader)、Material.SetFloat(string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Material ResolveDustMaterial()
    {
        if (runtimeDustMaterial != null)
        {
            return runtimeDustMaterial;
        }

        // 着色器。
Shader shader = ResolveShader("Particles/Standard Unlit", "Legacy Shaders/Particles/Alpha Blended", "Mobile/Particles/Alpha Blended", "Sprites/Default", "Unlit/Transparent");
        if (shader == null)
        {
            return null;
        }

        runtimeDustMaterial = new Material(shader);
        runtimeDustMaterial.name = "木箱尘屑运行时材质";
        runtimeDustMaterial.hideFlags = HideFlags.DontSave;
        ApplyMaterialColor(runtimeDustMaterial, dustColor);
        if (runtimeDustMaterial.HasProperty("_Mode"))
        {
            runtimeDustMaterial.SetFloat("_Mode", 2f);
        }

        if (runtimeDustMaterial.HasProperty("_SrcBlend"))
        {
            runtimeDustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (runtimeDustMaterial.HasProperty("_DstBlend"))
        {
            runtimeDustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (runtimeDustMaterial.HasProperty("_ZWrite"))
        {
            runtimeDustMaterial.SetFloat("_ZWrite", 0f);
        }

        return runtimeDustMaterial;
    }

    /// <summary>ResolveShader 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.IsErrorShader(Shader)、Shader.Find(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)、DestructibleWoodenBox.ResolveDustMaterial()；下游（含内部回调体） DestructibleWoodenBox.IsErrorShader(Shader)、Shader.Find(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="shaderNames">着色器名称列表。</param>
    private Shader ResolveShader(params string[] shaderNames)
    {
        // 当前遍历索引。
for (int i = 0; i < shaderNames.Length; i++)
        {
            // 着色器。
Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null && !IsErrorShader(shader))
            {
                return shader;
            }
        }

        return null;
    }

    /// <summary>IsUsableMaterial 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.IsErrorShader(Shader)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)、DestructibleWoodenBox.ResolveFragmentMaterial()；下游（含内部回调体） DestructibleWoodenBox.IsErrorShader(Shader)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="material">材质。</param>
    private bool IsUsableMaterial(Material material)
    {
        return material != null && material.shader != null && !IsErrorShader(material.shader);
    }

    /// <summary>IsErrorShader 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveShader(params string[])；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="shader">着色器。</param>
    private bool IsErrorShader(Shader shader)
    {
        return shader == null || shader.name.Contains("InternalErrorShader");
    }

    /// <summary>ResolveMainTexture 的业务处理入口；本方法直接依赖 Material.GetTexture(string)、Material.HasProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)；下游（含内部回调体） Material.GetTexture(string)、Material.HasProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceMaterial">来源材质。</param>
    private Texture ResolveMainTexture(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return null;
        }

        if (sourceMaterial.HasProperty("_MainTex"))
        {
            return sourceMaterial.GetTexture("_MainTex");
        }

        if (sourceMaterial.HasProperty("_BaseMap"))
        {
            return sourceMaterial.GetTexture("_BaseMap");
        }

        return null;
    }

    /// <summary>ApplyMaterialTexture 的业务处理入口；本方法直接依赖 Material.HasProperty(string)、Material.SetTexture(string, Texture)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)；下游（含内部回调体） Material.HasProperty(string)、Material.SetTexture(string, Texture)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="material">材质。</param>
    /// <param name="texture">当前处理的纹理资源。</param>
    private void ApplyMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }
    }

    /// <summary>ApplyMaterialColor 的业务处理入口；本方法直接依赖 Material.HasProperty(string)、Material.SetColor(string, Color)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)、DestructibleWoodenBox.ResolveDustMaterial()；下游（含内部回调体） Material.HasProperty(string)、Material.SetColor(string, Color)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="material">材质。</param>
    /// <param name="color">颜色。</param>
    private void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
        }
    }

    /// <summary>SetRenderersVisible 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetRenderersVisible(bool visible)
    {
        if (cachedRenderers == null)
        {
            return;
        }

        // item（当前渲染器）：每轮从缓存渲染器集合取得一个元素，仅代表当前这一项。使用位置：DestructibleWoodenBox.SetRenderersVisible(bool)。
foreach (Renderer item in cachedRenderers)
        {
            if (item != null)
            {
                item.enabled = visible;
            }
        }
    }

    /// <summary>CacheFragments 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Renderer&gt;(bool)、DestructibleWoodenBox.FragmentState.FragmentState()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Quaternion.AngleAxis(float, Vector3)、Random.Range(float, float)、Transform.InverseTransformDirection(Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Renderer&gt;(bool)、DestructibleWoodenBox.FragmentState.FragmentState()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Quaternion.AngleAxis(float, Vector3)、Random.Range(float, float)、Transform.InverseTransformDirection(Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    private void CacheFragments(Vector3 hitPoint)
    {
        // 渲染器集合。
Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        fragments = new FragmentState[renderers.Length];
        // 当前遍历索引。
for (int i = 0; i < renderers.Length; i++)
        {
            // 部件。
Transform part = renderers[i].transform;
            // 世界空间方向。
Vector3 worldDirection = part.position - hitPoint;
            if (worldDirection.sqrMagnitude < 0.01f)
            {
                worldDirection = part.position - transform.position;
            }

            if (worldDirection.sqrMagnitude < 0.01f)
            {
                worldDirection = Random.insideUnitSphere;
            }

            worldDirection.y = Mathf.Abs(worldDirection.y) + upwardScatter;
            worldDirection.Normalize();
            // 局部偏移量。
Vector3 localOffset = transform.InverseTransformDirection(worldDirection) * scatterDistance * Random.Range(0.75f, 1.25f);
            // 旋转轴。
Vector3 spinAxis = Random.onUnitSphere;
            if (spinAxis.sqrMagnitude < 0.01f)
            {
                spinAxis = Vector3.up;
            }

            fragments[i] = new FragmentState
            {
                transform = part,
                startPosition = part.localPosition,
                startRotation = part.localRotation,
                startScale = part.localScale,
                targetPosition = part.localPosition + localOffset,
                targetRotation = part.localRotation * Quaternion.AngleAxis(spinDegrees * Random.Range(0.7f, 1.3f), spinAxis),
                targetScale = part.localScale * Mathf.Clamp(finalScale, 0f, 1f)
            };
        }
    }

    /// <summary>PlayBreakAnimation 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.Break(Vector3)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator PlayBreakAnimation()
    {
        // 持续时间。
float duration = Mathf.Max(0.05f, breakDuration);
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 透明度。
float alpha = Mathf.Clamp01(elapsed / duration);
            // 缓动后的。
float eased = Mathf.SmoothStep(0f, 1f, alpha);
            // 当前遍历索引。
for (int i = 0; i < fragments.Length; i++)
            {
                // 碎片。
FragmentState fragment = fragments[i];
                if (fragment.transform == null)
                {
                    continue;
                }

                fragment.transform.localPosition = Vector3.Lerp(fragment.startPosition, fragment.targetPosition, eased);
                fragment.transform.localRotation = Quaternion.Slerp(fragment.startRotation, fragment.targetRotation, eased);
                fragment.transform.localScale = Vector3.Lerp(fragment.startScale, fragment.targetScale, eased);
            }

            yield return null;
        }

        if (!hideAfterBreak || cachedRenderers == null)
        {
            yield break;
        }

        // item（当前渲染器）：每轮从缓存渲染器集合取得一个元素，仅代表当前这一项。使用位置：DestructibleWoodenBox.PlayBreakAnimation()。
foreach (Renderer item in cachedRenderers)
        {
            if (item != null)
            {
                item.enabled = false;
            }
        }
    }
}
