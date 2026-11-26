using UnityEngine;

/// <summary>
/// 世界空间可拾取物品 — 旋转 + 范围检测 + 拾取
/// 实现 IInteractable 接口，通过 InteractionSystem 统一调度
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour, IInteractable
{
    /// <summary>当前物品数据配置。物品数据，为空时可通过 itemId 从 Resources 加载。</summary>
    [Header("物品")]
    [Tooltip("物品数据，为空时可通过 itemId 从 Resources 加载")]
    public ItemData item;
    /// <summary>拾取数量。</summary>
    [Tooltip("拾取数量")]
    public int count = 1;
    /// <summary>物品编号。itemId（用于运行时从 Resources/Items 加载）。</summary>
    [Tooltip("itemId（用于运行时从 Resources/Items 加载）")]
    public string itemId;
    /// <summary>模型覆盖。覆盖 ItemData 中的世界模型预制体（留空则使用 ItemData 中的设定）。</summary>
    [Header("模型")]
    [Tooltip("覆盖 ItemData 中的世界模型预制体（留空则使用 ItemData 中的设定）")]
    public GameObject modelOverride;
    /// <summary>拾取交互距离。</summary>
    [Header("拾取")]
    [Tooltip("拾取交互距离")]
    public float pickupRange = 2f;
    /// <summary>旋转速度。</summary>
    [Tooltip("旋转速度")]
    public float rotateSpeed = 90f;
    /// <summary>上下浮动幅度。</summary>
    [Tooltip("上下浮动幅度")]
    public float bobAmplitude = 0.1f;
    /// <summary>上下浮动速度。</summary>
    [Tooltip("上下浮动速度")]
    public float bobSpeed = 2f;
    /// <summary>拾取音效。</summary>
    [Header("音效")]
    public string pickupSfx = "item_pickup";
    /// <summary>显示轴心。</summary>
    private string promptItemName;
    private string cachedPickupPrompt;

    private Transform displayPivot;
    /// <summary>显示起始位置。</summary>
    private Vector3 displayStartPosition;
    /// <summary>显示起始旋转。</summary>
    private Quaternion displayStartRotation;
    /// <summary>显示角度。</summary>
    private float displayAngle;
    /// <summary>是否已拾取向上。</summary>
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

        // 列。
var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // 模型替换：如有真实模型预制体，替换占位几何体
        // 空值判断前缓存的模型覆盖。
global::UnityEngine.GameObject availableValue1 = modelOverride;
        // 模型预制件。
GameObject modelPrefab;
        if (!global::System.Object.ReferenceEquals(availableValue1, null))
        {
            modelPrefab = availableValue1;
        }
        else
        {
            // 分支选出的物品的世界空间模型预制件。
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
            // 网格过滤器。
var mf = GetComponent<MeshFilter>();
            // 网格渲染器。
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
            // 已有模型。
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

            // 模型。
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
                // 赋值前保存的目标模型的变换。
global::UnityEngine.Transform assignmentReceiver5 = model.transform;
                if (item != null)
                {
                    assignmentReceiver5.localPosition = item.modelOffset;
                }
                else
                {
                    assignmentReceiver5.localPosition = Vector3.zero;
                }

                // 赋值前保存的目标模型的变换。
global::UnityEngine.Transform assignmentReceiver7 = model.transform;
                // 分支选出的物品的模型旋转。
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
                // 赋值前保存的目标模型的变换。
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
        // 上下起伏。
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
        // 渲染器集合。
Renderer[] renderers = GetComponentsInChildren<Renderer>();
        // 是否具有包围盒。
bool hasBounds = false;
        // 包围盒。
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

        // 轴心对象。
GameObject pivotObject = new GameObject("拾取物展示转轴");
        displayPivot = pivotObject.transform;
        displayPivot.SetParent(transform, false);
        displayPivot.position = bounds.center;
        displayStartPosition = displayPivot.localPosition;
        displayStartRotation = displayPivot.rotation;
        // 子级上的模型和图集保持编辑器中设置的世界位置。
        // 视觉表现根节点集合。
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
                // 来源网格过滤器。
MeshFilter source = GetComponent<MeshFilter>();
                if (source == null || !(renderer is MeshRenderer))
                {
                    continue;
                }

                // 视觉表现。
GameObject visual = new GameObject("拾取物展示模型");
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                // 副本。
MeshRenderer copy = visual.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                visualRoots.Add(visual.transform);
                renderer.enabled = false;
                continue;
            }

            // 根变换节点。
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
        string itemName = itemId;
        if (item != null)
        {
            itemName = item.displayName;
        }

        if (cachedPickupPrompt == null || promptItemName != itemName)
        {
            promptItemName = itemName;
            cachedPickupPrompt = "拾取 " + itemName;
        }

        return cachedPickupPrompt;
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

        // 背包。
InventoryManager inventory = InventoryManager.Instance;
        // firstAcquisition（是否首个获取）：使用位置：ItemPickup.Pickup()。
        bool newlyAcquired = !inventory.HasAcquiredThisRun(item.itemId);
        // 是否成功。
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
            // 判空前缓存的音频管理器的实例。
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
