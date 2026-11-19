using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>武器、命中与战斗组件 DestructibleWoodenBox，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
public class DestructibleWoodenBox : MonoBehaviour, IDamageable, IInteractable
{
    /// <summary>中文含义：场景箱体集合。只读字段，初始化后不由普通写入口替换。使用位置：DestructibleWoodenBox.OnDisable()、DestructibleWoodenBox.OnEnable()、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。</summary>
    private static readonly List<DestructibleWoodenBox> sceneBoxes = new List<DestructibleWoodenBox>();
    /// <summary>中文含义：近战视线命中结果。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。</summary>
    private static readonly RaycastHit[] meleeLosHits = new RaycastHit[16];
    /// <summary>中文含义：交互者碰撞体缓冲区。只读字段，初始化后不由普通写入口替换。使用位置：作为 Component.GetComponentsInChildren&lt;Collider&gt;(bool, List&lt;Collider&gt;) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。</summary>
    private static readonly List<Collider> interactorColliderBuffer = new List<Collider>(32);
    /// <summary>中文含义：最大生命值。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("耐久")]
    public float maxHealth = 15f;
    /// <summary>中文含义：破坏持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("破碎表现")]
    public float breakDuration = 0.75f;
    /// <summary>中文含义：散射距离。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
    public float scatterDistance = 0.75f;
    /// <summary>中文含义：向上散射。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
    public float upwardScatter = 0.25f;
    /// <summary>中文含义：旋转度。使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。</summary>
    public float spinDegrees = 75f;
    /// <summary>中文含义：最终缩放。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    public float finalScale = 0.15f;
    /// <summary>中文含义：是否隐藏之后破坏。使用位置：DestructibleWoodenBox.PlayBreakAnimation()。</summary>
    public bool hideAfterBreak = true;
    /// <summary>中文含义：是否移除子级光源集合。使用位置：DestructibleWoodenBox.RemoveImportedChildLights()。</summary>
    [Header("导入模型清理")]
    public bool removeChildLights = true;
    /// <summary>中文含义：是否移除子级摄像机集合。使用位置：DestructibleWoodenBox.RemoveImportedChildCameras()。</summary>
    public bool removeChildCameras = true;
    /// <summary>中文含义：是否使用程序化碎屑。使用位置：DestructibleWoodenBox.Break(Vector3)。</summary>
    [Header("程序化碎片")]
    public bool useProceduralDebris = true;
    [SerializeField] private GameObject nativeBreakPieces;
    /// <summary>中文含义：木板碎片数量。使用位置：作为 Mathf.Max(int, int) 的参数。</summary>
    public int plankFragmentCount = 10;
    /// <summary>中文含义：小碎片碎片数量。使用位置：作为 Mathf.Max(int, int) 的参数。</summary>
    public int chipFragmentCount = 18;
    /// <summary>中文含义：木板长度范围。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public Vector2 plankLengthRange = new Vector2(0.35f, 0.75f);
    /// <summary>中文含义：木板宽度范围。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public Vector2 plankWidthRange = new Vector2(0.06f, 0.14f);
    /// <summary>中文含义：木板厚度范围。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public Vector2 plankThicknessRange = new Vector2(0.025f, 0.055f);
    /// <summary>中文含义：碎片冲量。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public float fragmentImpulse = 3.2f;
    /// <summary>中文含义：碎片向上冲量。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public float fragmentUpwardImpulse = 1.2f;
    /// <summary>中文含义：碎片扭矩。使用位置：作为 Random.Range(float, float) 的参数。</summary>
    public float fragmentTorque = 9f;
    /// <summary>中文含义：碎片存活时长。使用位置：作为 DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float) 的参数。</summary>
    public float fragmentLifetime = 3.5f;
    /// <summary>中文含义：碎片缩小持续时间。使用位置：作为 DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float) 的参数。</summary>
    public float fragmentShrinkDuration = 0.8f;
    /// <summary>中文含义：是否生成尘土效果。使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。</summary>
    public bool spawnDustEffect = true;
    /// <summary>中文含义：尘土粒子数量。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。</summary>
    public int dustParticleCount = 28;
    /// <summary>中文含义：尘土颜色。使用位置：作为 DestructibleWoodenBox.ApplyMaterialColor(Material, Color) 的参数、DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。</summary>
    public Color dustColor = new Color(0.48f, 0.36f, 0.24f, 0.55f);
    /// <summary>中文含义：碎片材质覆盖。使用位置：作为 DestructibleWoodenBox.IsUsableMaterial(Material) 的参数、DestructibleWoodenBox.ResolveFragmentMaterial()。</summary>
    [Header("碎片材质")]
    public Material fragmentMaterialOverride;
    /// <summary>中文含义：是否力度运行时碎片材质。使用位置：DestructibleWoodenBox.ResolveFragmentMaterial()。</summary>
    public bool forceRuntimeFragmentMaterial = true;
    /// <summary>中文含义：碎片色调。使用位置：作为 DestructibleWoodenBox.ApplyMaterialColor(Material, Color) 的参数。</summary>
    public Color fragmentTint = new Color(0.72f, 0.52f, 0.31f, 1f);
    /// <summary>中文含义：是否允许近战破坏。使用位置：DestructibleWoodenBox.CanInteract(Transform)。</summary>
    [Header("体术交互")]
    public bool allowMeleeBreak = true;
    /// <summary>中文含义：近战破坏范围。使用位置：DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.InteractRange.get。</summary>
    public float meleeBreakRange = 2f;
    /// <summary>中文含义：是否需要近战朝向。使用位置：DestructibleWoodenBox.CanInteract(Transform)。</summary>
    public bool requireMeleeFacing = false;
    /// <summary>中文含义：近战朝向角度。使用位置：DestructibleWoodenBox.IsInteractorFacingBox(Transform)。</summary>
    [Range(10f, 180f)]
    public float meleeFacingAngle = 130f;
    /// <summary>中文含义：近战提示文字。使用位置：DestructibleWoodenBox.GetPromptText()。</summary>
    public string meleePromptText = "破坏木箱";
    [Tooltip("木桶使用踢击桶身。放在高处时仍使用肘击。")]
    public bool isBarrel;
    /// <summary>中文含义：是否生成战利品事件破坏。使用位置：DestructibleWoodenBox.SpawnLoot(Vector3)。</summary>
    [Header("箱内掉落")]
    public bool spawnLootOnBreak = true;
    /// <summary>中文含义：是否使用掉落表战利品。使用位置：DestructibleWoodenBox.SpawnLoot(Vector3)。</summary>
    public bool useDropTableLoot = true;
    /// <summary>中文含义：是否启用后与敌人死亡一样独立摇取金钱。启用后与敌人死亡一样独立摇取金钱。使用位置：作为 DropManager.OnContainerBreak(Vector3, bool, bool) 的参数。</summary>
    [Tooltip("启用后与敌人死亡一样独立摇取金钱")]
    public bool includeMoneyDrop = true;
    /// <summary>中文含义：是否仅用于特殊奖励箱时开启。关闭时保留与敌人死亡相同的不掉落权重；仅用于特殊奖励箱时开启。使用位置：作为 DropManager.OnContainerBreak(Vector3, bool, bool) 的参数。</summary>
    [Tooltip("关闭时保留与敌人死亡相同的不掉落权重；仅用于特殊奖励箱时开启")]
    public bool guaranteeDropTableLoot = false;
    /// <summary>中文含义：固定战利品。使用位置：DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)。</summary>
    public ItemData[] fixedLoot;
    /// <summary>中文含义：固定战利品数量范围。使用位置：作为 Mathf.Max(int, int) 的参数、作为 Mathf.Min(int, int) 的参数。</summary>
    public Vector2Int fixedLootCountRange = new Vector2Int(1, 1);
    /// <summary>中文含义：战利品散射半径。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float lootScatterRadius = 0.35f;
    /// <summary>中文含义：破坏效果。使用位置：DestructibleWoodenBox.Break(Vector3)。</summary>
    [Header("可选特效")]
    public ParticleSystem breakEffect;
    /// <summary>中文含义：破坏音频。使用位置：DestructibleWoodenBox.Break(Vector3)。</summary>
    public AudioSource breakAudio;
    [Tooltip("木箱尚未破坏时的碰撞声")]
    public AudioEventData hitAudioEvent;
    [Tooltip("木箱首次破坏时的断裂声")]
    public AudioEventData breakAudioEvent;
    [Tooltip("随破坏播放一次的碎片声")]
    public AudioEventData debrisAudioEvent;

    internal bool HasDamageAudio => hitAudioEvent != null || breakAudioEvent != null;
    /// <summary>中文含义：当前生命值。使用位置：DestructibleWoodenBox.CacheSceneReferences()、DestructibleWoodenBox.TakeDamage(float, Vector3)。</summary>
    private float currentHealth;
    /// <summary>中文含义：是否损坏。使用位置：DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.GetPromptText() 等共 4 处。</summary>
    private bool broken;
    internal bool IsBroken => broken;
    /// <summary>中文含义：缓存碰撞体集合。使用位置：作为 DestructibleWoodenBox.IsColliderInArray(Collider, Collider[]) 的参数、DestructibleWoodenBox.Break(Vector3)、DestructibleWoodenBox.CacheSceneReferences() 等共 8 处。</summary>
    private Collider[] cachedColliders;
    /// <summary>中文含义：缓存渲染器集合。使用位置：DestructibleWoodenBox.CacheSceneReferences()、DestructibleWoodenBox.CacheSceneReferencesIfNeeded()、DestructibleWoodenBox.PlayBreakAnimation() 等共 6 处。</summary>
    private Renderer[] cachedRenderers;
    /// <summary>中文含义：碎片集合。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)、DestructibleWoodenBox.PlayBreakAnimation()。</summary>
    private FragmentState[] fragments;
    /// <summary>中文含义：运行时碎片材质。使用位置：作为 DestructibleWoodenBox.ApplyMaterialColor(Material, Color) 的参数、作为 DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture) 的参数、作为 Object.Destroy(Object) 的参数 等共 5 处。</summary>
    private Material runtimeFragmentMaterial;
    /// <summary>中文含义：运行时尘土材质。使用位置：作为 DestructibleWoodenBox.ApplyMaterialColor(Material, Color) 的参数、作为 Object.Destroy(Object) 的参数、DestructibleWoodenBox.OnDestroy() 等共 4 处。</summary>
    private Material runtimeDustMaterial;
    /// <summary>中文含义：上一次交互者。使用位置：DestructibleWoodenBox.CanInteract(Transform)、DestructibleWoodenBox.Interact()。</summary>
    private Transform lastInteractor;
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => meleeBreakRange;

    /// <summary>武器、命中与战斗的结构化数据 FragmentState；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct FragmentState
    {
        /// <summary>中文含义：变换。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)、DestructibleWoodenBox.PlayBreakAnimation()。</summary>
        public Transform transform;
        /// <summary>中文含义：起始位置。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
        public Vector3 startPosition;
        /// <summary>中文含义：起始旋转。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
        public Quaternion startRotation;
        /// <summary>中文含义：起始缩放。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
        public Vector3 startScale;
        /// <summary>中文含义：目标位置。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
        public Vector3 targetPosition;
        /// <summary>中文含义：目标旋转。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
        public Quaternion targetRotation;
        /// <summary>中文含义：目标缩放。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。</summary>
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

        // childLights（子级光源集合）：保存 Component.GetComponentsInChildren<Light>(bool) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.RemoveImportedChildLights()。
Light[] childLights = GetComponentsInChildren<Light>(true);
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.RemoveImportedChildLights()。
for (int i = childLights.Length - 1; i >= 0; i--)
        {
            // childLight（子级光源）：使用位置：作为 Object.Destroy(Object) 的参数、DestructibleWoodenBox.RemoveImportedChildLights()。
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

        // childCameras（子级摄像机集合）：保存 Component.GetComponentsInChildren<Camera>(bool) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.RemoveImportedChildCameras()。
Camera[] childCameras = GetComponentsInChildren<Camera>(true);
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.RemoveImportedChildCameras()。
for (int i = childCameras.Length - 1; i >= 0; i--)
        {
            // childCamera（子级摄像机）：使用位置：作为 Object.Destroy(Object) 的参数、DestructibleWoodenBox.RemoveImportedChildCameras()。
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
    /// <param name="damage">中文含义：伤害。使用位置：作为 DestructibleWoodenBox.TakeDamage(float, Vector3) 的参数。</param>
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position + Vector3.up * 0.5f);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.Break(Vector3)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.TakeDamage(float)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；下游（含内部回调体） DestructibleWoodenBox.Break(Vector3)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 DestructibleWoodenBox.Break(Vector3) 的参数。</param>
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.GetDistanceTo(Vector3) 的参数、作为 DestructibleWoodenBox.IsInteractorFacingBox(Transform) 的参数、作为 DestructibleWoodenBox.IsMeleePathClear(Transform) 的参数 等共 4 处。</param>
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
        // interactor（交互者）：使用位置：作为 DestructibleWoodenBox.TryBreakByMelee(Transform) 的参数、DestructibleWoodenBox.Interact()。
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

        // combat（战斗）：使用位置：DestructibleWoodenBox.Interact()。
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.CanInteract(Transform) 的参数、作为 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform) 的参数。</param>
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.TryBreakByMelee(Transform) 的参数、作为 DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox) 的参数。</param>
    /// <param name="target">中文含义：目标可破坏木制箱体。输出参数，由本入口写入并交给调用方。使用位置：作为 DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox) 的参数、DestructibleWoodenBox.TryBreakNearestByMelee(Transform, out DestructibleWoodenBox)。</param>
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.CanInteract(Transform) 的参数、作为 DestructibleWoodenBox.GetDistanceTo(Vector3) 的参数、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。</param>
    /// <param name="target">中文含义：目标可破坏木制箱体。输出参数，由本入口写入并交给调用方。使用位置：DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。</param>
    public static bool TryFindNearestByMelee(Transform interactor, out DestructibleWoodenBox target)
    {
        target = null;
        if (interactor == null)
        {
            return false;
        }

        // bestDistance（最佳距离）：使用位置：DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。
float bestDistance = float.MaxValue;
        // i（当前遍历索引）：使用位置：作为 List<DestructibleWoodenBox>.RemoveAt(int) 的参数、作为 List<DestructibleWoodenBox>.this[int] 的参数、DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。
for (int i = sceneBoxes.Count - 1; i >= 0; i--)
        {
            // box（箱体）：使用位置：DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。
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

            // distance（距离）：保存 DestructibleWoodenBox.GetDistanceTo(Vector3) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.TryFindNearestByMelee(Transform, out DestructibleWoodenBox)。
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
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 DestructibleWoodenBox.CacheFragments(Vector3) 的参数、作为 DestructibleWoodenBox.SpawnDust(Vector3, Bounds) 的参数、作为 DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material) 的参数。</param>
    private void Break(Vector3 hitPoint)
    {
        if (broken)
        {
            return;
        }

        broken = true;
        CacheFragments(hitPoint);
        // breakBounds（破坏包围盒）：保存 DestructibleWoodenBox.ResolveWorldBounds() 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.SpawnDust(Vector3, Bounds) 的参数、作为 DestructibleWoodenBox.SpawnLoot(Vector3) 的参数、作为 DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material) 的参数。
Bounds breakBounds = ResolveWorldBounds();
        // fragmentMaterial（碎片材质）：保存 DestructibleWoodenBox.ResolveFragmentMaterial() 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material) 的参数。
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
    /// <param name="center">中文含义：中心。使用位置：作为 DestructibleWoodenBox.ResolveLootPosition(Vector3) 的参数、作为 DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3) 的参数。</param>
    private void SpawnLoot(Vector3 center)
    {
        if (!spawnLootOnBreak)
        {
            return;
        }

        // dropManager（掉落管理器）：使用位置：作为 DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3) 的参数、DestructibleWoodenBox.SpawnLoot(Vector3)。
DropManager dropManager = DropManager.Instance;
        // spawnedFixedLoot（是否已生成固定战利品）：保存 DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 DropManager.OnContainerBreak(Vector3, bool, bool) 的参数。
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
    /// <param name="dropManager">中文含义：掉落管理器。使用位置：DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)。</param>
    /// <param name="center">中文含义：中心。使用位置：作为 DestructibleWoodenBox.ResolveLootPosition(Vector3) 的参数。</param>
    private bool SpawnFixedLoot(DropManager dropManager, Vector3 center)
    {
        if (dropManager == null || fixedLoot == null || fixedLoot.Length == 0)
        {
            return false;
        }

        // spawnedAny（是否已生成任意）：使用位置：DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)。
bool spawnedAny = false;
        // minCount（最小数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(int, int) 的参数、作为 Random.Range(int, int) 的参数。
int minCount = Mathf.Max(1, Mathf.Min(fixedLootCountRange.x, fixedLootCountRange.y));
        // maxCount（最大数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Random.Range(int, int) 的参数。
int maxCount = Mathf.Max(minCount, Mathf.Max(fixedLootCountRange.x, fixedLootCountRange.y));
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)。
for (int i = 0; i < fixedLoot.Length; i++)
        {
            // item（当前物品数据配置）：使用位置：作为 DropManager.SpawnItemPickup(ItemData, int, Vector3) 的参数、DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)。
ItemData item = fixedLoot[i];
            if (item == null)
            {
                continue;
            }

            // count（数量）：保存 Random.Range(int, int) 的返回结果，供当前步骤使用。使用位置：作为 DropManager.SpawnItemPickup(ItemData, int, Vector3) 的参数。
int count = Random.Range(minCount, maxCount + 1);
            dropManager.SpawnItemPickup(item, count, ResolveLootPosition(center));
            spawnedAny = true;
        }

        return spawnedAny;
    }

    /// <summary>ResolveLootPosition 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnFixedLoot(DropManager, Vector3)、DestructibleWoodenBox.SpawnLoot(Vector3)；下游（含内部回调体） Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="center">中文含义：中心。使用位置：DestructibleWoodenBox.ResolveLootPosition(Vector3)。</param>
    private Vector3 ResolveLootPosition(Vector3 center)
    {
        // offset（偏移量）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, lootScatterRadius);
        return center + new Vector3(offset.x, 0f, offset.y);
    }

    /// <summary>IsInteractorFacingBox 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CanInteract(Transform)；下游（含内部回调体） DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform) 的参数、DestructibleWoodenBox.IsInteractorFacingBox(Transform)。</param>
    private bool IsInteractorFacingBox(Transform interactor)
    {
        // toBox（目标箱体）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、DestructibleWoodenBox.IsInteractorFacingBox(Transform)。
Vector3 toBox = ResolveMeleeHitPoint(interactor) - interactor.position;
        toBox.y = 0f;
        if (toBox.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        // forward（前向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、DestructibleWoodenBox.IsInteractorFacingBox(Transform)。
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
    /// <param name="interactor">中文含义：交互者。使用位置：作为 DestructibleWoodenBox.ResolveMeleeHitPoint(Transform) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。</param>
    private bool IsMeleePathClear(Transform interactor)
    {
        CacheSceneReferencesIfNeeded();
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            return true;
        }

        // origin（原点）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。
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
        // direction（方向）：使用位置：DestructibleWoodenBox.IsMeleePathClear(Transform)。
Vector3 direction = targetPoint - origin;
        // distance（距离）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。
float distance = direction.magnitude;
        if (distance < 0.05f)
        {
            return true;
        }

        // normalizedDir（归一化方向）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 normalizedDir = direction / distance;
        // hitCount（命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.IsMeleePathClear(Transform)。
int hitCount = Physics.RaycastNonAlloc(origin, normalizedDir, meleeLosHits, distance, ~0, QueryTriggerInteraction.Ignore);
        // 收集交互者自身碰撞体，射线需忽略它们
        interactorColliderBuffer.Clear();
        interactor.GetComponentsInChildren(false, interactorColliderBuffer);
        // nearestBlockingDistance（最近阻挡中距离）：使用位置：DestructibleWoodenBox.IsMeleePathClear(Transform)。
float nearestBlockingDistance = float.MaxValue;
        // nearestBlockingCollider（最近阻挡中碰撞体）：使用位置：作为 DestructibleWoodenBox.IsColliderInArray(Collider, Collider[]) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。
Collider nearestBlockingCollider = null;
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.IsMeleePathClear(Transform)。
for (int i = 0; i < hitCount; i++)
        {
            // hit（命中）：使用位置：作为 List<Collider>.Contains(Collider) 的参数、DestructibleWoodenBox.IsMeleePathClear(Transform)。
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
    /// <param name="collider">中文含义：碰撞体。使用位置：DestructibleWoodenBox.IsColliderInArray(Collider, Collider[])。</param>
    /// <param name="array">中文含义：数组。使用位置：DestructibleWoodenBox.IsColliderInArray(Collider, Collider[])。</param>
    private static bool IsColliderInArray(Collider collider, Collider[] array)
    {
        if (array == null)
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.IsColliderInArray(Collider, Collider[])。
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
    /// <param name="interactor">中文含义：交互者。使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。</param>
    private Vector3 ResolveMeleeHitPoint(Transform interactor)
    {
        CacheSceneReferencesIfNeeded();
        // fallback（后备）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
Vector3 fallback = transform.position + Vector3.up * 0.5f;
        if (interactor == null)
        {
            return fallback;
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            return fallback;
        }

        // origin（原点）：使用位置：作为 Collider.ClosestPoint(Vector3) 的参数、DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
Vector3 origin = ResolveMeleeOrigin(interactor);
        // bestDistance（最佳距离）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
float bestDistance = float.MaxValue;
        // bestPoint（最佳点）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
Vector3 bestPoint = fallback;
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
for (int i = 0; i < cachedColliders.Length; i++)
        {
            // item（当前碰撞体）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
Collider item = cachedColliders[i];
            if (item == null || !item.enabled || item.isTrigger)
            {
                continue;
            }

            // point（点）：保存 Collider.ClosestPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
Vector3 point = item.ClosestPoint(origin);
            // distance（距离）：使用位置：DestructibleWoodenBox.ResolveMeleeHitPoint(Transform)。
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
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string) 的参数。</param>
    /// <param name="bounds">中文含义：包围盒。使用位置：作为 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string) 的参数。</param>
    /// <param name="fragmentMaterial">中文含义：碎片材质。使用位置：作为 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string) 的参数。</param>
    private void SpawnNativeBreakPieces(Vector3 hitPoint)
    {
        GameObject fragmentsRoot = Instantiate(nativeBreakPieces, transform);
        foreach (MeshFilter mesh in fragmentsRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || !mesh.TryGetComponent(out MeshRenderer renderer))
            {
                continue;
            }

            // 碎片使用原版拆分网格，碰撞仅在碎片的存活期间启用。
            GameObject part = mesh.gameObject;
            part.layer = LayerMask.NameToLayer("Ignore Raycast");
            Bounds localBounds = mesh.sharedMesh.bounds;
            BoxCollider shape = part.AddComponent<BoxCollider>();
            shape.center = localBounds.center;
            shape.size = Vector3.Max(localBounds.size, Vector3.one * 0.012f);
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
        // plankCount（木板数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)。
int plankCount = Mathf.Max(0, plankFragmentCount);
        // chipCount（小碎片数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)。
int chipCount = Mathf.Max(0, chipFragmentCount);
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)。
for (int i = 0; i < plankCount; i++)
        {
            // size（尺寸）：使用位置：作为 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string) 的参数。
Vector3 size = new Vector3(Random.Range(plankWidthRange.x, plankWidthRange.y), Random.Range(plankThicknessRange.x, plankThicknessRange.y), Random.Range(plankLengthRange.x, plankLengthRange.y));
            CreateFragment(bounds, hitPoint, size, fragmentMaterial, "木箱木板碎片");
        }

        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)。
for (int i = 0; i < chipCount; i++)
        {
            // chipSize（小碎片尺寸）：保存 Random.Range(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float chipSize = Random.Range(0.045f, 0.12f);
            // size（尺寸）：使用位置：作为 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string) 的参数。
Vector3 size = new Vector3(chipSize * Random.Range(0.6f, 1.4f), chipSize * Random.Range(0.35f, 0.8f), chipSize * Random.Range(0.7f, 1.8f));
            CreateFragment(bounds, hitPoint, size, fragmentMaterial, "木箱木屑");
        }
    }

    /// <summary>CreateFragment 的业务处理入口；本方法直接依赖 DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3)、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)、GameObject.AddComponent&lt;Rigidbody&gt;()、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Renderer&gt;()、Mathf.Abs(float)、MonoBehaviour.StartCoroutine(IEnumerator) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.SpawnProceduralDebris(Vector3, Bounds, Material)；下游（含内部回调体） DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3)、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)、GameObject.AddComponent&lt;Rigidbody&gt;()、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Renderer&gt;()、Mathf.Abs(float)、MonoBehaviour.StartCoroutine(IEnumerator) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bounds">中文含义：包围盒。使用位置：作为 DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3) 的参数、作为 Random.Range(float, float) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：作为 DestructibleWoodenBox.ClampFragmentSize(Vector3, Vector3) 的参数。</param>
    /// <param name="fragmentMaterial">中文含义：碎片材质。使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。</param>
    /// <param name="objectName">中文含义：对象名称。使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。</param>
    private void CreateFragment(Bounds bounds, Vector3 hitPoint, Vector3 size, Material fragmentMaterial, string objectName)
    {
        // spawnPosition（生成位置）：使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
Vector3 spawnPosition = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z));
        // fragment（碎片）：保存 GameObject.CreatePrimitive(PrimitiveType) 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float) 的参数、DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fragment.name = objectName;
        fragment.transform.position = spawnPosition;
        fragment.transform.rotation = transform.rotation * Random.rotationUniform;
        fragment.transform.localScale = ClampFragmentSize(size, bounds.size);
        // renderer（渲染器）：保存 GameObject.GetComponent<Renderer>() 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
Renderer renderer = fragment.GetComponent<Renderer>();
        if (renderer != null && fragmentMaterial != null)
        {
            renderer.sharedMaterial = fragmentMaterial;
        }

        // body（身体）：保存 GameObject.AddComponent<Rigidbody>() 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
Rigidbody body = fragment.AddComponent<Rigidbody>();
        body.mass = Random.Range(0.05f, 0.16f);
        body.linearDamping = 0.25f;
        body.angularDamping = 0.05f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // direction（方向）：使用位置：作为 Mathf.Abs(float) 的参数、DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
Vector3 direction = spawnPosition - hitPoint;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Random.onUnitSphere;
        }

        direction.y = Mathf.Abs(direction.y) * 0.35f + Random.Range(0.15f, 0.45f);
        direction.Normalize();
        // impulse（冲量）：使用位置：作为 Rigidbody.AddForce(Vector3, ForceMode) 的参数、DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)。
Vector3 impulse = direction * Random.Range(fragmentImpulse * 0.65f, fragmentImpulse * 1.35f);
        impulse += Vector3.up * Random.Range(fragmentUpwardImpulse * 0.45f, fragmentUpwardImpulse * 1.2f);
        body.AddForce(impulse, ForceMode.Impulse);
        body.AddTorque(Random.onUnitSphere * Random.Range(fragmentTorque * 0.45f, fragmentTorque * 1.2f), ForceMode.Impulse);
        StartCoroutine(ShrinkAndDestroy(fragment.transform, fragmentLifetime, fragmentShrinkDuration));
    }

    /// <summary>ClampFragmentSize 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="size">中文含义：尺寸。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    /// <param name="boundsSize">中文含义：包围盒尺寸。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private Vector3 ClampFragmentSize(Vector3 size, Vector3 boundsSize)
    {
        // maxExtent（最大半尺寸）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
float maxExtent = Mathf.Max(0.08f, Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z)) * 0.75f);
        return new Vector3(Mathf.Clamp(size.x, 0.02f, maxExtent), Mathf.Clamp(size.y, 0.015f, maxExtent), Mathf.Clamp(size.z, 0.03f, maxExtent));
    }

    /// <summary>ShrinkAndDestroy 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Object.Destroy(Object)、Vector3.Lerp(Vector3, Vector3, float)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.CreateFragment(Bounds, Vector3, Vector3, Material, string)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、Object.Destroy(Object)、Vector3.Lerp(Vector3, Vector3, float)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标变换节点。使用位置：作为 Object.Destroy(Object) 的参数、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)。</param>
    /// <param name="lifetime">中文含义：存活时长。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="shrinkDuration">中文含义：缩小持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private IEnumerator ShrinkAndDestroy(Transform target, float lifetime, float shrinkDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, lifetime));
        if (target == null)
        {
            yield break;
        }

        // startScale（起始缩放）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 startScale = target.localScale;
        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)。
float duration = Mathf.Max(0.05f, shrinkDuration);
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、DestructibleWoodenBox.ShrinkAndDestroy(Transform, float, float)。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            // alpha（透明度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数。
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
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
    /// <param name="bounds">中文含义：包围盒。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
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

        // dustObject（尘土对象）：使用位置：作为 Object.Destroy(Object, float) 的参数、DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
GameObject dustObject = new GameObject("木箱破碎尘屑");
        dustObject.transform.position = Vector3.Lerp(hitPoint, bounds.center, 0.35f);
        // particles（粒子集合）：保存 GameObject.AddComponent<ParticleSystem>() 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // main（主要）：使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
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
        // emission（发射）：使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(dustParticleCount, 1, short.MaxValue)) });
        // shape（形状）：使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.15f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.6f);
        // particleRenderer（粒子渲染器）：保存 GameObject.GetComponent<ParticleSystemRenderer>() 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
ParticleSystemRenderer particleRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            // dustMaterial（尘土材质）：保存 DestructibleWoodenBox.ResolveDustMaterial() 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.SpawnDust(Vector3, Bounds)。
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
        // hasBounds（是否具有包围盒）：使用位置：DestructibleWoodenBox.ResolveWorldBounds()。
bool hasBounds = false;
        // bounds（包围盒）：使用位置：DestructibleWoodenBox.ResolveWorldBounds()。
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

        // size（尺寸）：使用位置：DestructibleWoodenBox.ResolveWorldBounds()。
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

        // sourceMaterial（来源材质）：保存 DestructibleWoodenBox.ResolveSourceMaterial() 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material) 的参数、作为 DestructibleWoodenBox.IsUsableMaterial(Material) 的参数、DestructibleWoodenBox.ResolveFragmentMaterial()。
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
    /// <param name="sourceMaterial">中文含义：来源材质。使用位置：作为 DestructibleWoodenBox.IsUsableMaterial(Material) 的参数、作为 DestructibleWoodenBox.ResolveMainTexture(Material) 的参数、DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)。</param>
    private Material BuildRuntimeFragmentMaterial(Material sourceMaterial)
    {
        if (runtimeFragmentMaterial != null)
        {
            return runtimeFragmentMaterial;
        }

        // shader（着色器）：保存 DestructibleWoodenBox.ResolveShader(params string[]) 的返回结果，供当前步骤使用。使用位置：作为 Material.Material(Shader) 的参数、DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)。
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
        // texture（当前处理的纹理资源）：保存 DestructibleWoodenBox.ResolveMainTexture(Material) 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture) 的参数。
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

        // shader（着色器）：保存 DestructibleWoodenBox.ResolveShader(params string[]) 的返回结果，供当前步骤使用。使用位置：作为 Material.Material(Shader) 的参数、DestructibleWoodenBox.ResolveDustMaterial()。
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
    /// <param name="shaderNames">中文含义：着色器名称列表。使用位置：作为 Shader.Find(string) 的参数、DestructibleWoodenBox.ResolveShader(params string[])。</param>
    private Shader ResolveShader(params string[] shaderNames)
    {
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.ResolveShader(params string[])。
for (int i = 0; i < shaderNames.Length; i++)
        {
            // shader（着色器）：保存 Shader.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 DestructibleWoodenBox.IsErrorShader(Shader) 的参数、DestructibleWoodenBox.ResolveShader(params string[])。
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
    /// <param name="material">中文含义：材质。使用位置：作为 DestructibleWoodenBox.IsErrorShader(Shader) 的参数、DestructibleWoodenBox.IsUsableMaterial(Material)。</param>
    private bool IsUsableMaterial(Material material)
    {
        return material != null && material.shader != null && !IsErrorShader(material.shader);
    }

    /// <summary>IsErrorShader 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.IsUsableMaterial(Material)、DestructibleWoodenBox.ResolveShader(params string[])；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="shader">中文含义：着色器。使用位置：DestructibleWoodenBox.IsErrorShader(Shader)。</param>
    private bool IsErrorShader(Shader shader)
    {
        return shader == null || shader.name.Contains("InternalErrorShader");
    }

    /// <summary>ResolveMainTexture 的业务处理入口；本方法直接依赖 Material.GetTexture(string)、Material.HasProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DestructibleWoodenBox.BuildRuntimeFragmentMaterial(Material)；下游（含内部回调体） Material.GetTexture(string)、Material.HasProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceMaterial">中文含义：来源材质。使用位置：DestructibleWoodenBox.ResolveMainTexture(Material)。</param>
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
    /// <param name="material">中文含义：材质。使用位置：DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture)。</param>
    /// <param name="texture">中文含义：当前处理的纹理资源。使用位置：作为 Material.SetTexture(string, Texture) 的参数、DestructibleWoodenBox.ApplyMaterialTexture(Material, Texture)。</param>
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
    /// <param name="material">中文含义：材质。使用位置：DestructibleWoodenBox.ApplyMaterialColor(Material, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：作为 Material.SetColor(string, Color) 的参数。</param>
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
    /// <param name="visible">中文含义：是否可见。使用位置：DestructibleWoodenBox.SetRenderersVisible(bool)。</param>
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
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。</param>
    private void CacheFragments(Vector3 hitPoint)
    {
        // renderers（渲染器集合）：保存 Component.GetComponentsInChildren<Renderer>(bool) 的返回结果，供当前步骤使用。使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。
Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        fragments = new FragmentState[renderers.Length];
        // i（当前遍历索引）：使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。
for (int i = 0; i < renderers.Length; i++)
        {
            // part（部件）：使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。
Transform part = renderers[i].transform;
            // worldDirection（世界空间方向）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Transform.InverseTransformDirection(Vector3) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。
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
            // localOffset（局部偏移量）：使用位置：DestructibleWoodenBox.CacheFragments(Vector3)。
Vector3 localOffset = transform.InverseTransformDirection(worldDirection) * scatterDistance * Random.Range(0.75f, 1.25f);
            // spinAxis（旋转轴）：使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数、DestructibleWoodenBox.CacheFragments(Vector3)。
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
        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数、DestructibleWoodenBox.PlayBreakAnimation()。
float duration = Mathf.Max(0.05f, breakDuration);
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、DestructibleWoodenBox.PlayBreakAnimation()。
float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // alpha（透明度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数。
float alpha = Mathf.Clamp01(elapsed / duration);
            // eased（缓动后的）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
float eased = Mathf.SmoothStep(0f, 1f, alpha);
            // i（当前遍历索引）：使用位置：DestructibleWoodenBox.PlayBreakAnimation()。
for (int i = 0; i < fragments.Length; i++)
            {
                // fragment（碎片）：使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、DestructibleWoodenBox.PlayBreakAnimation()。
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
