using UnityEngine;

/// <summary>
/// 世界空间可拾取物品 — 旋转 + 范围检测 + 拾取
/// 实现 IInteractable 接口，通过 InteractionSystem 统一调度
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour, IInteractable
{
    /// <summary>中文含义：当前物品数据配置。物品数据，为空时可通过 itemId 从 Resources 加载。使用位置：作为 InventoryManager.AddItem(ItemData, int) 的参数、作为 InventoryManager.HasAcquiredThisRun(string) 的参数、作为 ItemInspectionUI.Show(ItemData) 的参数 等共 10 处。</summary>
    [Header("物品")]
    [Tooltip("物品数据，为空时可通过 itemId 从 Resources 加载")]
    public ItemData item;
    /// <summary>中文含义：拾取数量。使用位置：作为 InventoryManager.AddItem(ItemData, int) 的参数、DropManager.SpawnItemPickup(ItemData, int, Vector3)。</summary>
    [Tooltip("拾取数量")]
    public int count = 1;
    /// <summary>中文含义：物品编号。itemId（用于运行时从 Resources/Items 加载）。使用位置：作为 Debug.LogWarning(object) 的参数、作为 Resources.Load&lt;ItemData&gt;(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数 等共 5 处。</summary>
    [Tooltip("itemId（用于运行时从 Resources/Items 加载）")]
    public string itemId;
    /// <summary>中文含义：模型覆盖。覆盖 ItemData 中的世界模型预制体（留空则使用 ItemData 中的设定）。使用位置：ItemPickup.Start()、VillageInterfaceRefinementConfigurator.ApplyScene()。</summary>
    [Header("模型")]
    [Tooltip("覆盖 ItemData 中的世界模型预制体（留空则使用 ItemData 中的设定）")]
    public GameObject modelOverride;
    /// <summary>中文含义：拾取交互距离。使用位置：ItemPickup.InteractRange.get。</summary>
    [Header("拾取")]
    [Tooltip("拾取交互距离")]
    public float pickupRange = 2f;
    /// <summary>中文含义：旋转速度。使用位置：作为 Mathf.Repeat(float, float) 的参数。</summary>
    [Tooltip("旋转速度")]
    public float rotateSpeed = 90f;
    /// <summary>中文含义：上下浮动幅度。使用位置：ItemPickup.Update()。</summary>
    [Tooltip("上下浮动幅度")]
    public float bobAmplitude = 0.1f;
    /// <summary>中文含义：上下浮动速度。使用位置：作为 Mathf.Sin(float) 的参数。</summary>
    [Tooltip("上下浮动速度")]
    public float bobSpeed = 2f;
    /// <summary>中文含义：拾取音效。使用位置：作为 AudioManager.PlaySfx(string) 的参数。</summary>
    [Header("音效")]
    public string pickupSfx = "item_pickup";
    /// <summary>中文含义：显示轴心。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、ItemPickup.CreateDisplayPivot()、ItemPickup.Update()。</summary>
    private Transform displayPivot;
    /// <summary>中文含义：显示起始位置。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、ItemPickup.CreateDisplayPivot()。</summary>
    private Vector3 displayStartPosition;
    /// <summary>中文含义：显示起始旋转。使用位置：ItemPickup.CreateDisplayPivot()、ItemPickup.Update()。</summary>
    private Quaternion displayStartRotation;
    /// <summary>中文含义：显示角度。使用位置：作为 Mathf.Repeat(float, float) 的参数、作为 Quaternion.AngleAxis(float, Vector3) 的参数、ItemPickup.Update()。</summary>
    private float displayAngle;
    /// <summary>中文含义：是否已拾取向上。使用位置：ItemPickup.CanInteract(Transform)、ItemPickup.Pickup()、ItemPickup.Update()。</summary>
    private bool pickedUp;
    private bool inventoryFull;
    private bool groundedDrop;
    private Vector3 groundedContact;
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => pickupRange;
    internal Vector3 ContactPoint
    {
        get
        {
            if (groundedDrop)
            {
                return transform.TransformPoint(groundedContact);
            }

            return transform.position;
        }
    }

    internal void ConfigureGroundDrop()
    {
        groundedDrop = true;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Collider&gt;()、Component.GetComponent&lt;MeshFilter&gt;()、Component.GetComponent&lt;MeshRenderer&gt;()、GameObject.GetComponentsInChildren&lt;Collider&gt;()、GameObject.SetActive(bool)、ItemPickup.CreateDisplayPivot()、Object.Destroy(Object) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (item == null && !string.IsNullOrEmpty(itemId))
        {
            item = Resources.Load<ItemData>($"Items/{itemId}");
        }

        // col（列）：保存 Component.GetComponent<Collider>() 的返回结果，供当前步骤使用。使用位置：ItemPickup.Start()。
var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // 模型替换：如有真实模型预制体，替换占位几何体
        // availableValue1（空值判断前缓存的模型覆盖）：从模型覆盖读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ItemPickup.Start()。
global::UnityEngine.GameObject availableValue1 = modelOverride;
        // modelPrefab（模型预制件）：使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform) 的参数、ItemPickup.Start()。
GameObject modelPrefab;
        if (!global::System.Object.ReferenceEquals(availableValue1, null))
        {
            modelPrefab = availableValue1;
        }
        else
        {
            // selectedValue2（分支选出的物品的世界空间模型预制件）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：ItemPickup.Start()。
global::UnityEngine.GameObject selectedValue2;
            if (item != null)
            {
                selectedValue2 = item.worldModelPrefab;
            }
            else
            {
                selectedValue2 = null;
            }

            modelPrefab = (selectedValue2);
        }

        if (modelPrefab != null)
        {
            // 移除占位 MeshFilter + MeshRenderer（保留 Collider 用于交互检测）
            // mf（网格过滤器）：保存 Component.GetComponent<MeshFilter>() 的返回结果，供当前步骤使用。使用位置：作为 Object.Destroy(Object) 的参数、ItemPickup.Start()。
var mf = GetComponent<MeshFilter>();
            // mr（网格渲染器）：保存 Component.GetComponent<MeshRenderer>() 的返回结果，供当前步骤使用。使用位置：作为 Object.Destroy(Object) 的参数、ItemPickup.Start()。
var mr = GetComponent<MeshRenderer>();
            if (mf != null)
            {
                Destroy(mf);
            }

            if (mr != null)
            {
                // 销毁在帧末执行，先隐藏占位模型，避免被展示转轴重复收集。
                mr.enabled = false;
                Destroy(mr);
            }

            // 实例化真实模型作为子物体
            // existingModel（已有模型）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Object.Destroy(Object) 的参数、ItemPickup.Start()。
Transform existingModel = transform.Find("Model");
            if (existingModel != null && modelOverride == null)
            {
                existingModel.gameObject.SetActive(false);
                Destroy(existingModel.gameObject);
                existingModel = null;
            }

            // child（子级）：每轮从变换取得一个元素，仅代表当前这一项。使用位置：ItemPickup.Start()。
foreach (Transform child in transform)
            {
                if (child.name.StartsWith("图集拾取标识"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // model（模型）：使用位置：ItemPickup.Start()。
global::UnityEngine.GameObject model;
            if (existingModel != null)
            {
                model = existingModel.gameObject;
            }
            else
            {
                model = Instantiate(modelPrefab, transform);
            }

            model.name = "Model";
            if (existingModel == null)
            {
                // assignmentReceiver5（赋值前保存的目标模型的变换）：从模型的变换读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：ItemPickup.Start()。
global::UnityEngine.Transform assignmentReceiver5 = model.transform;
                if (item != null)
                {
                    assignmentReceiver5.localPosition = item.modelOffset;
                }
                else
                {
                    assignmentReceiver5.localPosition = Vector3.zero;
                }

                // assignmentReceiver7（赋值前保存的目标模型的变换）：从模型的变换读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：ItemPickup.Start()。
global::UnityEngine.Transform assignmentReceiver7 = model.transform;
                // selectedValue8（分支选出的物品的模型旋转）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Quaternion.Euler(Vector3) 的参数、ItemPickup.Start()。
global::UnityEngine.Vector3 selectedValue8;
                if (item != null)
                {
                    selectedValue8 = item.modelRotation;
                }
                else
                {
                    selectedValue8 = Vector3.zero;
                }

                assignmentReceiver7.localRotation = Quaternion.Euler(selectedValue8);
                // assignmentReceiver9（赋值前保存的目标模型的变换）：从模型的变换读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：ItemPickup.Start()。
global::UnityEngine.Transform assignmentReceiver9 = model.transform;
                if (item != null)
                {
                    assignmentReceiver9.localScale = item.modelScale;
                }
                else
                {
                    assignmentReceiver9.localScale = Vector3.one;
                }
            }

            // 移除子物体上的 Collider（交互由父物体 Collider 负责）
            // c（当前碰撞体）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.Destroy(Object) 的参数。
foreach (var c in model.GetComponentsInChildren<Collider>())
            {
                Destroy(c);
            }
        }

        if (groundedDrop)
        {
            SeatGroundDrop();
        }
        else
        {
            CreateDisplayPivot();
        }
    }

    private void SeatGroundDrop()
    {
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        groundedContact = transform.InverseTransformPoint(transform.position + Vector3.up * .06f);
        if (hasBounds)
        {
            float lift = transform.position.y - bounds.min.y + .008f;
            Vector3 contact = bounds.center;
            contact.y = Mathf.Max(bounds.min.y + .04f, bounds.center.y);
            groundedContact = transform.InverseTransformPoint(contact);
            transform.position += Vector3.up * lift;
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Register(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Unregister(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        InteractionSystem.Unregister(this);
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Repeat(float, float)、Mathf.Sin(float)、Quaternion.AngleAxis(float, Vector3)、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (pickedUp)
        {
            return;
        }

        if (displayPivot == null)
        {
            return;
        }

        displayAngle = Mathf.Repeat(displayAngle + rotateSpeed * Time.deltaTime, 360f);
        // bob（上下起伏）：使用位置：ItemPickup.Update()。
float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        displayPivot.rotation = Quaternion.AngleAxis(displayAngle, Vector3.up) * displayStartRotation;
        displayPivot.position = transform.TransformPoint(displayStartPosition) + Vector3.up * bob;
    }

    /// <summary>CreateDisplayPivot 的业务处理入口；本方法直接依赖 Bounds.Encapsulate(Bounds)、Component.GetComponent&lt;MeshFilter&gt;()、Component.GetComponentsInChildren&lt;Renderer&gt;()、GameObject.AddComponent&lt;MeshFilter&gt;()、GameObject.AddComponent&lt;MeshRenderer&gt;()、GameObject.GameObject(string)、HashSet&lt;Transform&gt;.Add(Transform) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemPickup.Start()；下游（含内部回调体） Bounds.Encapsulate(Bounds)、Component.GetComponent&lt;MeshFilter&gt;()、Component.GetComponentsInChildren&lt;Renderer&gt;()、GameObject.AddComponent&lt;MeshFilter&gt;()、GameObject.AddComponent&lt;MeshRenderer&gt;()、GameObject.GameObject(string)、HashSet&lt;Transform&gt;.Add(Transform) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CreateDisplayPivot()
    {
        // 只驱动展示模型，交互根与碰撞体始终留在地图摆放位置。
        // 使用渲染中心作为转轴，避免偏离原点的模型绕旧位置公转。
        // renderers（渲染器集合）：保存 Component.GetComponentsInChildren<Renderer>() 的返回结果，供当前步骤使用。使用位置：ItemPickup.CreateDisplayPivot()。
Renderer[] renderers = GetComponentsInChildren<Renderer>();
        // hasBounds（是否具有包围盒）：使用位置：ItemPickup.CreateDisplayPivot()。
bool hasBounds = false;
        // bounds（包围盒）：使用位置：ItemPickup.CreateDisplayPivot()。
Bounds bounds = default;
        // renderer（渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数、ItemPickup.CreateDisplayPivot()。
foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        // pivotObject（轴心对象）：使用位置：ItemPickup.CreateDisplayPivot()。
GameObject pivotObject = new GameObject("拾取物展示转轴");
        displayPivot = pivotObject.transform;
        displayPivot.SetParent(transform, false);
        displayPivot.position = bounds.center;
        displayStartPosition = displayPivot.localPosition;
        displayStartRotation = displayPivot.rotation;
        // 子级上的模型和图集保持编辑器中设置的世界位置。
        // visualRoots（视觉表现根节点集合）：使用位置：ItemPickup.CreateDisplayPivot()。
var visualRoots = new System.Collections.Generic.HashSet<Transform>();
        // renderer（渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：ItemPickup.CreateDisplayPivot()。
foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (renderer.transform == transform)
            {
                // source（来源网格过滤器）：保存 Component.GetComponent<MeshFilter>() 的返回结果，供当前步骤使用。使用位置：ItemPickup.CreateDisplayPivot()。
MeshFilter source = GetComponent<MeshFilter>();
                if (source == null || !(renderer is MeshRenderer))
                {
                    continue;
                }

                // visual（视觉表现）：使用位置：作为 HashSet<Transform>.Add(Transform) 的参数、ItemPickup.CreateDisplayPivot()。
GameObject visual = new GameObject("拾取物展示模型");
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                // copy（副本）：保存 GameObject.AddComponent<MeshRenderer>() 的返回结果，供当前步骤使用。使用位置：ItemPickup.CreateDisplayPivot()。
MeshRenderer copy = visual.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                visualRoots.Add(visual.transform);
                renderer.enabled = false;
                continue;
            }

            // root（根变换节点）：使用位置：作为 HashSet<Transform>.Add(Transform) 的参数、ItemPickup.CreateDisplayPivot()。
Transform root = renderer.transform;
            while (root.parent != transform && root.parent != null)
            {
                root = root.parent;
            }

            visualRoots.Add(root);
        }

        // visualRoot（视觉表现根节点）：每轮从视觉表现根节点集合取得一个元素，仅代表当前这一项。使用位置：ItemPickup.CreateDisplayPivot()。
foreach (Transform visualRoot in visualRoots)
        {
            visualRoot.SetParent(displayPivot, true);
        }
    }

    /// <summary>CanInteract 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。</param>
    public bool CanInteract(Transform interactor)
    {
        if (pickedUp)
        {
            return false;
        }

        if (interactor.TryGetComponent(out LeonItemPickup pickup))
        {
            return pickup.CanReach(this);
        }

        return true;
    }

    /// <summary>Interact 的业务处理入口；本方法直接依赖 ItemPickup.Pickup()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ItemPickup.Pickup()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Interact()
    {
        var player = LeonController.Instance;
        if (player != null && player.TryGetComponent(out LeonItemPickup pickup))
        {
            pickup.TryBegin(this);
            return;
        }

        if (TryCollect(out bool firstAcquisition) && firstAcquisition && ItemInspectionUI.Instance != null)
        {
            ItemInspectionUI.Instance.Show(item);
        }
    }

    /// <summary>GetPromptText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetPromptText()
    {
        if (inventoryFull)
        {
            return "公文包空间不足";
        }
        // selectedValue11（分支选出的物品的显示名称）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：ItemPickup.GetPromptText()。
string selectedValue11;
        if (item != null)
        {
            selectedValue11 = item.displayName;
        }
        else
        {
            selectedValue11 = itemId;
        }

        return $"拾取 {(selectedValue11)}";
    }

    /// <summary>Pickup 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、Debug.LogWarning(object)、InventoryManager.AddItem(ItemData, int)、InventoryManager.HasAcquiredThisRun(string)、ItemInspectionUI.Show(ItemData)、Object.Destroy(Object)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemPickup.Interact()；下游（含内部回调体） AudioManager.PlaySfx(string)、Debug.LogWarning(object)、InventoryManager.AddItem(ItemData, int)、InventoryManager.HasAcquiredThisRun(string)、ItemInspectionUI.Show(ItemData)、Object.Destroy(Object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool TryCollect(out bool firstAcquisition)
    {
        firstAcquisition = false;
        if (pickedUp)
        {
            return false;
        }

        if (item == null)
        {
            Debug.LogWarning($"[ItemPickup] 物品数据为空: {itemId}");
            return false;
        }

        if (InventoryManager.Instance == null)
        {
            return false;
        }

        // inventory（背包）：使用位置：ItemPickup.Pickup()。
InventoryManager inventory = InventoryManager.Instance;
        // firstAcquisition（是否首个获取）：使用位置：ItemPickup.Pickup()。
        bool newlyAcquired = !inventory.HasAcquiredThisRun(item.itemId);
        // success（是否成功）：保存 InventoryManager.AddItem(ItemData, int) 的返回结果，供当前步骤使用。使用位置：ItemPickup.Pickup()。
        bool success = inventory.AddItem(item, count);
        if (!success && count == 1 && item.itemType == ItemType.Weapon && item.itemId == "shotgun")
        {
            success = inventory.Model.TryAddWeaponWithRearrangement(item);
        }
        inventoryFull = !success;
        if (inventoryFull)
        {
            AudioManager.PlayInterface(AudioEventNames.UIError);
        }

        if (success)
        {
            pickedUp = true;
            // receiverValue12（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ItemPickup.Pickup()。
global::AudioManager receiverValue12 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                string sound = pickupSfx;
                if (sound == AudioEventNames.ItemPickup && receiverValue12.library != null)
                {
                    string typedSound = AudioEventNames.ItemPickup;
                    if (item.itemType == ItemType.Ammo)
                    {
                        typedSound = AudioEventNames.ItemPickupAmmo;
                    }
                    else if (item.itemType == ItemType.Healing)
                    {
                        typedSound = AudioEventNames.ItemPickupHealing;
                    }
                    else if (item.itemType == ItemType.CraftingIngredient)
                    {
                        typedSound = AudioEventNames.ItemPickupMaterial;
                    }
                    else if (item.itemType == ItemType.Key)
                    {
                        typedSound = AudioEventNames.ItemPickupKey;
                    }
                    else if (item.itemType == ItemType.Weapon && item.itemId == "shotgun")
                    {
                        typedSound = AudioEventNames.ItemPickupW870;
                    }

                    if (receiverValue12.library.HasEvent(typedSound))
                    {
                        sound = typedSound;
                    }
                }

                receiverValue12.PlaySfx(sound);
            }

            firstAcquisition = newlyAcquired;
            if (newlyAcquired && item.itemId == "shotgun")
            {
                VillageLeonDialogue.Report(LeonController.Instance, VillageSpeechCue.ShotgunPickup);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        return success;
    }
}
