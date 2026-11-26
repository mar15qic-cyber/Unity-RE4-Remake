using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>独立预览不注册模态，也不参与存取拖放，仅在需要时渲染。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "寄存物品预览")]
public class StoredItemPreview : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    /// <summary>配置。</summary>
    [Header("展示资源与界面")]
    [UnityEngine.Serialization.FormerlySerializedAs("配置")]
    [UnityEngine.InspectorName("配置")]
    public InventoryPresentationConfiguration configuration;
    /// <summary>画面。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("画面")]
    [UnityEngine.InspectorName("画面")]
    public RawImage view;
    /// <summary>名称。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("名称")]
    [UnityEngine.InspectorName("名称")]
    public Text nameValue;
    /// <summary>说明。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("说明")]
    [UnityEngine.InspectorName("说明")]
    public Text descriptionValue;
    /// <summary>数量。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("数量")]
    [UnityEngine.InspectorName("数量")]
    public Text countValue;
    /// <summary>提示。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("提示")]
    [UnityEngine.InspectorName("提示")]
    public Text prompt;
    /// <summary>展示层。</summary>
    [Header("展示参数")]
    [UnityEngine.Serialization.FormerlySerializedAs("展示层")]
    [UnityEngine.InspectorName("展示层")]
    public int presentationLayer = 29;
    /// <summary>纹理尺寸。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("纹理尺寸")]
    [UnityEngine.InspectorName("纹理尺寸")]
    public int textureSize = 768;
    /// <summary>旋转速度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("旋转速度")]
    [UnityEngine.InspectorName("旋转速度")]
    public float rotationSpeed = .35f;
    /// <summary>阶段。</summary>
    GameObject stage;
    /// <summary>旋转轴。</summary>
    Transform rotateAxis;
    /// <summary>模型。</summary>
    GameObject model;
    /// <summary>摄像机。</summary>
    Camera camera;
    /// <summary>纹理值。</summary>
    RenderTexture textureValue;
    /// <summary>当前物品。</summary>
    ItemData currentItem;
    /// <summary>是否需要绘制。</summary>
    bool needsDraw;
    /// <summary>是否拖拽。</summary>
    bool drag;
    /// <summary>Select 的业务处理入口；本方法直接依赖 Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Behaviour&gt;()、GameObject.GetComponentsInChildren&lt;Collider&gt;()、GameObject.GetComponentsInChildren&lt;Renderer&gt;()、GameObject.GetComponentsInChildren&lt;Transform&gt;()、GameObject.SetActive(bool)、InventoryPresentationConfiguration.Find(string) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)、StorageBoxUI.OnInventoryItemHoverEnter(GridItem)、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)；下游（含内部回调体） Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Behaviour&gt;()、GameObject.GetComponentsInChildren&lt;Collider&gt;()、GameObject.GetComponentsInChildren&lt;Renderer&gt;()、GameObject.GetComponentsInChildren&lt;Transform&gt;()、GameObject.SetActive(bool)、InventoryPresentationConfiguration.Find(string) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    public void Select(GridItem item)
    {
        if (item == null || item.item == null)
        {
            Clear();
            return;
        }

        nameValue.text = item.item.displayName;
        descriptionValue.text = item.item.description;
        countValue.text = "数量　" + item.count + "　　占格　" + item.Width + " × " + item.Height;
        if (!ShowModel(item.item))
        {
            prompt.text = "暂无三维模型";
        }
    }

    internal bool ShowModel(ItemData item)
    {
        if (item == null || view == null)
        {
            ClearModel();
            return false;
        }
        if (currentItem == item && model != null)
        {
            return true;
        }

        CreateStage();
        if (model != null)
        {
            model.SetActive(false);
            Destroy(model);
        }

        currentItem = item;
        // 当前物品表现。
global::InventoryPresentationConfiguration.ItemPresentation c;
        if (configuration != null)
        {
            c = configuration.Find(item.itemId);
        }
        else
        {
            c = null;
        }

        // 预制件。
global::UnityEngine.GameObject prefab;
        if (c != null)
        {
            prefab = c.model;
        }
        else
        {
            prefab = item.GetInspectionModelPrefab();
        }

        if (prefab == null)
        {
            ClearModel();
            return false;
        }

        rotateAxis.localRotation = Quaternion.identity;
        model = Instantiate(prefab, rotateAxis);
        model.transform.localPosition = Vector3.zero;
        // 赋值前保存的目标模型的变换。
global::UnityEngine.Transform assignmentReceiver3 = model.transform;
        // 分支选出的候选值的朝向。
global::UnityEngine.Vector3 selectedValue4;
        if (c != null)
        {
            selectedValue4 = c.orientation;
        }
        else
        {
            selectedValue4 = item.inspectionModelRotation;
        }

        assignmentReceiver3.localRotation = Quaternion.Euler(selectedValue4);
        // b（当前行为）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：StoredItemPreview.Select(GridItem)。
foreach (var b in model.GetComponentsInChildren<Behaviour>())
        {
            b.enabled = false;
        }

        // collider（碰撞体）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：StoredItemPreview.Select(GridItem)。
foreach (var collider in model.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        // t（当前变换节点）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：StoredItemPreview.Select(GridItem)。
foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            t.gameObject.layer = presentationLayer;
        }

        // 渲染器集合。
var rr = model.GetComponentsInChildren<Renderer>();
        if (rr.Length == 0)
        {
            ClearModel();
            return false;
        }

        // 包围盒。
Bounds bounds = rr[0].bounds;
        // r（当前渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数。
foreach (var r in rr)
        {
            bounds.Encapsulate(r.bounds);
        }

        // 缩放。
float scale = 2.05f / Mathf.Max(.001f, bounds.size.magnitude);
        model.transform.localScale *= scale;
        model.transform.position = rotateAxis.position - (bounds.center - rotateAxis.position) * scale;
        rotateAxis.localRotation = Quaternion.Euler(8, -18, 0);
        if (prompt != null)
        {
            prompt.text = "拖动此处旋转物品";
        }
        view.color = Color.white;
        needsDraw = true;
        return true;
    }

    /// <summary>CreateStage 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GameObject(string)、GameObject.GetComponent&lt;Camera&gt;()、GameObject.GetComponent&lt;Light&gt;()、Mathf.Max(float, float) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StoredItemPreview.Select(GridItem)；下游（含内部回调体） Color.Color(float, float, float, float)、Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GameObject(string)、GameObject.GetComponent&lt;Camera&gt;()、GameObject.GetComponent&lt;Light&gt;()、Mathf.Max(float, float) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CreateStage()
    {
        if (stage != null)
        {
            return;
        }

        stage = new GameObject("寄存物品预览舞台");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage, gameObject.scene);
        stage.transform.position = new Vector3(0, -1000, 0);
        rotateAxis = new GameObject("检视转轴").transform;
        rotateAxis.SetParent(stage.transform, false);
        camera = new GameObject("寄存预览相机", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(stage.transform, false);
        camera.transform.localPosition = new Vector3(0, 0, -4);
        camera.orthographic = true;
        camera.orthographicSize = 1.25f;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 10;
        camera.cullingMask = 1 << presentationLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.enabled = false;
        // 宽高比。
float aspect = Mathf.Max(.2f, view.rectTransform.rect.width / Mathf.Max(1, view.rectTransform.rect.height));
        textureValue = new RenderTexture(textureSize, Mathf.RoundToInt(textureSize / aspect), 24, RenderTextureFormat.ARGB32)
        {
            name = "寄存物品预览纹理",
            antiAliasing = 2,
            filterMode = FilterMode.Bilinear
        };
        textureValue.Create();
        view.texture = textureValue;
        camera.targetTexture = textureValue;
        camera.aspect = aspect;
        // i（索引）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：StoredItemPreview.CreateStage()。
foreach (int i in new[]
        {
            0,
            1
        }

        )
        {
            // 当前光源。
var l = new GameObject("预览柔光", typeof(Light)).GetComponent<Light>();
            l.transform.SetParent(stage.transform, false);
            l.type = LightType.Directional;
            // 赋值前保存的目标左侧的变换。
global::UnityEngine.Transform assignmentReceiver5 = l.transform;
            // 分支选出的25。
int selectedValue6;
            if (i == 0)
            {
                selectedValue6 = 25;
            }
            else
            {
                selectedValue6 = -20;
            }

            // 按原顺序缓存的实参浮点计算。
float argumentValue8 = selectedValue6;
            // 分支选出的130。
int selectedValue7;
            if (i == 0)
            {
                selectedValue7 = -35;
            }
            else
            {
                selectedValue7 = 130;
            }

            assignmentReceiver5.localRotation = Quaternion.Euler(argumentValue8, selectedValue7, 0);
            // 赋值前保存的目标当前光源。
global::UnityEngine.Light assignmentReceiver9 = l;
            if (i == 0)
            {
                assignmentReceiver9.color = new Color(1, .97f, .91f);
            }
            else
            {
                assignmentReceiver9.color = new Color(.77f, .85f, 1);
            }

            // 赋值前保存的目标当前光源。
global::UnityEngine.Light assignmentReceiver11 = l;
            if (i == 0)
            {
                assignmentReceiver11.intensity = 1.5f;
            }
            else
            {
                assignmentReceiver11.intensity = .75f;
            }

            l.cullingMask = 1 << presentationLayer;
            l.shadows = LightShadows.None;
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Camera.Render()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (!needsDraw || camera == null)
        {
            return;
        }

        needsDraw = false;
        // 是否雾效。
bool fog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
#if UNITY_EDITOR
            // 是否异步。
bool async = UnityEditor.ShaderUtil.allowAsyncCompilation;
            try
            {
                UnityEditor.ShaderUtil.allowAsyncCompilation = false;
                camera.Render();
            }
            finally
            {
                UnityEditor.ShaderUtil.allowAsyncCompilation = async;
            }
#else
            camera.Render();
#endif
        }
        finally
        {
            RenderSettings.fog = fog;
        }
    }

    /// <summary>OnBeginDrag 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="e">当前指针事件数据。</param>
    public void OnBeginDrag(PointerEventData e)
    {
        drag = e.button == PointerEventData.InputButton.Left;
    }

    /// <summary>OnDrag 的业务处理入口；本方法直接依赖 Transform.Rotate(Vector3, float, Space)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Transform.Rotate(Vector3, float, Space)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="e">当前指针事件数据。</param>
    public void OnDrag(PointerEventData e)
    {
        if (!drag || rotateAxis == null)
        {
            return;
        }

        rotateAxis.Rotate(Vector3.up, -e.delta.x * rotationSpeed, Space.World);
        rotateAxis.Rotate(Vector3.right, e.delta.y * rotationSpeed, Space.World);
        needsDraw = true;
    }

    /// <summary>OnEndDrag 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="e">中文含义：当前指针事件数据。</param>
    public void OnEndDrag(PointerEventData e)
    {
        drag = false;
    }

    /// <summary>Clear 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.RequestStorageRefresh()、StorageBoxUI.Show(StorageBoxController)、StoredItemPreview.Close()、StoredItemPreview.Select(GridItem)；下游（含内部回调体） GameObject.SetActive(bool)、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearModel()
    {
        if (model != null)
        {
            model.SetActive(false);
            Destroy(model);
        }
        model = null;
        currentItem = null;
        if (view != null)
        {
            view.color = Color.clear;
        }
    }

    public void Clear()
    {
        ClearModel();
        if (nameValue != null)
        {
            nameValue.text = "物品详情";
        }

        if (descriptionValue != null)
        {
            descriptionValue.text = "选择寄存箱或背包中的物品以查看详情。";
        }

        if (countValue != null)
        {
            countValue.text = "";
        }

        if (prompt != null)
        {
            prompt.text = "";
        }
    }

    /// <summary>Close 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Object.Destroy(Object)、RenderTexture.Release()、StoredItemPreview.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Close()、StoredItemPreview.OnDestroy()、StoredItemPreview.OnDisable()；下游（含内部回调体） GameObject.SetActive(bool)、Object.Destroy(Object)、RenderTexture.Release()、StoredItemPreview.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Close()
    {
        Clear();
        drag = false;
        needsDraw = false;
        if (view != null)
        {
            view.texture = null;
        }

        if (camera != null)
        {
            camera.targetTexture = null;
        }

        if (textureValue != null)
        {
            textureValue.Release();
            Destroy(textureValue);
        }

        textureValue = null;
        if (stage != null)
        {
            stage.SetActive(false);
            Destroy(stage);
        }

        stage = null;
        camera = null;
        rotateAxis = null;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StoredItemPreview.Close()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        Close();
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StoredItemPreview.Close()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        Close();
    }
}
