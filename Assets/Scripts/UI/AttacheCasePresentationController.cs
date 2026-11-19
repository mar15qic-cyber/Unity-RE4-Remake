using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只负责公文包和物品的三维表现，背包规则仍由原管理器提交。</summary>
/// <remarks>公文包三维展示宿主，读取物品与展示配置建立模型和渲染纹理，并向界面提供屏幕投影换算；背包数量和布局规则仍归数据模型。</remarks>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "公文包展示控制器")]
public class AttacheCasePresentationController : MonoBehaviour
{
    /// <summary>界面交互与显示使用的 PresentationState 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "公文包展示控制器+展示状态")]
    public enum PresentationState
    {
        /// <summary>中文含义：关闭。分类成员“关闭”，对应既有数值 0；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
        [UnityEngine.InspectorName("关闭")]
        Close,
        /// <summary>中文含义：打开中间。分类成员“打开中”，对应既有数值 1；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
        [UnityEngine.InspectorName("打开中")]
        OpenMiddle,
        /// <summary>中文含义：当前展示是否允许交互。分类成员“可操作”，对应既有数值 2；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
        [UnityEngine.InspectorName("可操作")]
        Interactive,
        /// <summary>中文含义：关闭中间。分类成员“关闭中”，对应既有数值 3；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
        [UnityEngine.InspectorName("关闭中")]
        CloseMiddle
    }

    /// <summary>中文含义：网格。使用位置：作为 AttacheCasePresentationController.UpdateSpaceOverride(Image) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.RoundToInt(float) 的参数 等共 13 处。</summary>
    [Header("显式引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("网格")]
    [UnityEngine.InspectorName("网格")]
    public InventoryGridView mesh;
    /// <summary>中文含义：配置。使用位置：作为 Material.Material(Material) 的参数、作为 Mathf.Min(int, int) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数 等共 11 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("配置")]
    [UnityEngine.InspectorName("配置")]
    public InventoryPresentationConfiguration configuration;
    /// <summary>中文含义：打开时长。使用位置：作为 Mathf.Max(float, float) 的参数、AttacheCasePresentationController.PlayOpenLid()、InventorySceneLoader.HideInventoryScene()。</summary>
    [Header("开合时间")]
    [UnityEngine.Serialization.FormerlySerializedAs("打开时长")]
    [UnityEngine.InspectorName("打开时长")]
    public float openDuration = 0.45f;
    /// <summary>中文含义：关闭时长。使用位置：作为 Mathf.Max(float, float) 的参数、AttacheCasePresentationController.CloseLid()、InventorySceneLoader.HideInventoryScene()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("关闭时长")]
    [UnityEngine.InspectorName("关闭时长")]
    public float closeDuration = 0.3f;
    /// <summary>中文含义：整理倾角。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Header("透视构图")]
    [UnityEngine.Serialization.FormerlySerializedAs("整理倾角")]
    [UnityEngine.InspectorName("整理倾角")]
    public float arrangeTiltAngle = 18f;
    /// <summary>中文含义：合箱倾角。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("合箱倾角")]
    [UnityEngine.InspectorName("合箱倾角")]
    public float closeCaseTiltAngle = 32f;
    /// <summary>中文含义：箱盖打开角度。使用位置：作为 Quaternion.Euler(float, float, float) 的参数。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("箱盖打开角度")]
    [UnityEngine.InspectorName("箱盖打开角度")]
    public float caseLidOpenAngle = 95f;
    /// <summary>中文含义：当前状态。使用位置：AttacheCasePresentationController.CloseLid()、AttacheCasePresentationController.ImmediateHide()、AttacheCasePresentationController.Interactive.get 等共 6 处。</summary>
    public PresentationState CurrentState { get; private set; }
    /// <summary>中文含义：当前展示是否允许交互。使用位置：作为 GameObject.SetActive(bool) 的参数、AttacheCaseProjectionInput.Find(Vector2)、AttacheCaseProjectionInput.OnDrag(PointerEventData) 等共 7 处。</summary>
    public bool Interactive => CurrentState == PresentationState.Interactive;

    /// <summary>中文含义：打开完成。事件通信成员 OpenComplete，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：AttacheCasePresentationController.PlayOpenLid()。</summary>
    public event Action OpenComplete;
    /// <summary>中文含义：关闭完成。事件通信成员 CloseComplete，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：AttacheCasePresentationController.CloseLid()。</summary>
    public event Action CloseComplete;
    /// <summary>界面交互与显示模块 PresentationInstance；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "公文包展示控制器+展示实例")]
    class PresentationInstance
    {
        /// <summary>中文含义：数据。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("数据")]
        [UnityEngine.InspectorName("数据")]
        public GridItem data;
        /// <summary>中文含义：根游戏对象。使用位置：AttacheCasePresentationController.LateUpdate()、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("根")]
        [UnityEngine.InspectorName("根")]
        public GameObject root;
        /// <summary>中文含义：中心。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("中心")]
        [UnityEngine.InspectorName("中心")]
        public Vector3 center;
        /// <summary>中文含义：尺寸。使用位置：作为 Mathf.Max(float, float) 的参数、AttacheCasePresentationController.LateUpdate()、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("尺寸")]
        [UnityEngine.InspectorName("尺寸")]
        public Vector3 size;
        /// <summary>中文含义：配置。使用位置：AttacheCasePresentationController.LateUpdate()、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("配置")]
        [UnityEngine.InspectorName("配置")]
        public InventoryPresentationConfiguration.ItemPresentation configuration;
    }

    /// <summary>中文含义：表现物品。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.LateUpdate()、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
    readonly Dictionary<GridItem, PresentationInstance> presentationItem = new Dictionary<GridItem, PresentationInstance>();
    /// <summary>中文含义：恢复列表。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
    readonly List<GridItem> recoveryList = new List<GridItem>();
    /// <summary>中文含义：对象池。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
    readonly Dictionary<string, Stack<PresentationInstance>> objectPool = new Dictionary<string, Stack<PresentationInstance>>();
    /// <summary>中文含义：阶段。使用位置：作为 AttacheCasePresentationController.SetLayer(GameObject) 的参数、作为 Object.Destroy(Object) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数 等共 16 处。</summary>
    GameObject stage;
    /// <summary>中文含义：公文包身体。使用位置：AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.UpdateComposition()。</summary>
    Transform caseBody;
    /// <summary>中文含义：公文包箱盖。使用位置：AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.UpdateCaseLid()。</summary>
    Transform caseLid;
    AttacheCaseModelBinding caseModelBinding;
    /// <summary>中文含义：物品根节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.UpdateCaseLid()。</summary>
    Transform itemRoot;
    /// <summary>中文含义：表现摄像机。使用位置：作为 Mathf.RoundToInt(float) 的参数、AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.MeshRotateScreen(Vector2) 等共 8 处。</summary>
    Camera presentationCamera;
    /// <summary>中文含义：纹理值。使用位置：作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.LateUpdate()、AttacheCasePresentationController.ReleaseTexture() 等共 4 处。</summary>
    RenderTexture textureValue;
    /// <summary>中文含义：视图。使用位置：作为 Object.Destroy(Object) 的参数、作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 13 处。</summary>
    RawImage view;
    private bool craftingBackdrop;
    /// <summary>中文含义：落地点提示。使用位置：作为 AttacheCasePresentationController.UpdateSpaceOverride(Image) 的参数、作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.CreateStage() 等共 6 处。</summary>
    Image landPointPrompt;
    /// <summary>中文含义：拖拽物品。使用位置：AttacheCasePresentationController.BeginDrag(GridItem)、AttacheCasePresentationController.EndDrag()、AttacheCasePresentationController.LateUpdate()。</summary>
    GridItem dragItem;
    /// <summary>中文含义：像素比例。使用位置：作为 Vector2.Vector2(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、AttacheCasePresentationController.LateUpdate() 等共 7 处。</summary>
    float pixelRatio;
    /// <summary>中文含义：展开半尺寸。使用位置：作为 GameObject.SetActive(bool) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、作为 Quaternion.Euler(float, float, float) 的参数 等共 5 处。</summary>
    float expandExtent;
    /// <summary>中文含义：上一次宽度。使用位置：作为 Mathf.Approximately(float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。</summary>
    float lastWidth;
    /// <summary>中文含义：上一次高度。使用位置：作为 Mathf.Approximately(float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。</summary>
    float lastHeight;
    /// <summary>中文含义：构图中心。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。</summary>
    float compositionCenter;
    /// <summary>中文含义：平面网格分组。使用位置：AttacheCasePresentationController.CreateStage()。</summary>
    CanvasGroup planeMeshGroup;
    /// <summary>中文含义：空间单元格线。使用位置：作为 AttacheCasePresentationController.SetLayer(GameObject) 的参数、AttacheCasePresentationController.RebuildSpaceCellLine()。</summary>
    GameObject spaceCellLine;
    /// <summary>中文含义：单元格线网格。使用位置：作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.OnDestroy()、AttacheCasePresentationController.RebuildSpaceCellLine()。</summary>
    Mesh cellLineMesh;
    /// <summary>中文含义：单元格线材质。使用位置：作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.OnDestroy()、AttacheCasePresentationController.RebuildSpaceCellLine()。</summary>
    Material cellLineMaterial;
    /// <summary>中文含义：空间材质。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.OnDestroy()、AttacheCasePresentationController.UpdateSpaceOverride(Image)。</summary>
    readonly List<Material> spaceMaterial = new List<Material>();
    /// <summary>中文含义：覆盖映射关系。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.UpdateSpaceOverride(Image)。</summary>
    readonly Dictionary<Image, MeshRenderer> overrideMapping = new Dictionary<Image, MeshRenderer>();
    /// <summary>中文含义：数量映射关系。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.CreateCountText(GridItem)、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)、AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform)。</summary>
    readonly Dictionary<GridItem, Text> countMapping = new Dictionary<GridItem, Text>();
    /// <summary>中文含义：数量对象池。只读字段，初始化后不由普通写入口替换。使用位置：AttacheCasePresentationController.CreateCountText(GridItem)、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</summary>
    readonly Stack<Text> countPool = new Stack<Text>();
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
    }

    /// <summary>Show 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.PlayOpenLid()、Debug.LogError(object, Object)、GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()；下游（含内部回调体） AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.PlayOpenLid()、Debug.LogError(object, Object)、GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        if (CurrentState != PresentationState.Close)
        {
            return;
        }

        if (mesh == null || configuration == null || configuration.attacheCaseModel == null)
        {
            Debug.LogError("公文包缺少三维展示引用", this);
            CurrentState = PresentationState.Interactive;
            return;
        }

        CreateStage();
        expandExtent = 0f;
        UpdateCaseLid();
        stage.SetActive(true);
        view.gameObject.SetActive(true);
        CurrentState = PresentationState.OpenMiddle;
        StartCoroutine(PlayOpenLid());
    }

    /// <summary>PlayOpenLid 的业务处理入口；本方法直接依赖 Action.Invoke()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.Show()；下游（含内部回调体） Action.Invoke()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator PlayOpenLid()
    {
        // time（时间）：使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数、AttacheCasePresentationController.PlayOpenLid()。
float time = 0f;
        while (time < openDuration)
        {
            time += Time.unscaledDeltaTime;
            expandExtent = Mathf.SmoothStep(0f, 1f, time / Mathf.Max(0.01f, openDuration));
            UpdateCaseLid();
            yield return null;
        }

        expandExtent = 1f;
        UpdateCaseLid();
        CurrentState = PresentationState.Interactive;
        // receiverValue1（判空前缓存的打开完成）：从打开完成读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AttacheCasePresentationController.PlayOpenLid()。
global::System.Action receiverValue1 = OpenComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke();
        }
    }

    /// <summary>CloseLid 的业务处理入口；本方法直接依赖 Action.Invoke()、AttacheCasePresentationController.EndDrag()、AttacheCasePresentationController.ImmediateHide()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.FadeOutAndCallback(Action)；下游（含内部回调体） Action.Invoke()、AttacheCasePresentationController.EndDrag()、AttacheCasePresentationController.ImmediateHide()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public IEnumerator CloseLid()
    {
        while (CurrentState == PresentationState.OpenMiddle)
        {
            yield return null;
        }

        if (CurrentState == PresentationState.Close)
        {
            yield break;
        }

        CurrentState = PresentationState.CloseMiddle;
        EndDrag();
        // time（时间）：使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数、AttacheCasePresentationController.CloseLid()。
float time = 0f;
        while (time < closeDuration)
        {
            time += Time.unscaledDeltaTime;
            expandExtent = 1f - Mathf.SmoothStep(0f, 1f, time / Mathf.Max(0.01f, closeDuration));
            UpdateCaseLid();
            yield return null;
        }

        ImmediateHide();
        // receiverValue2（判空前缓存的关闭完成）：从关闭完成读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AttacheCasePresentationController.CloseLid()。
global::System.Action receiverValue2 = CloseComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke();
        }
    }

    /// <summary>ImmediateHide 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.EndDrag()、GameObject.SetActive(bool)、MonoBehaviour.StopAllCoroutines()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CloseLid()、AttacheCasePresentationController.OnDisable()、InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） AttacheCasePresentationController.EndDrag()、GameObject.SetActive(bool)、MonoBehaviour.StopAllCoroutines()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ImmediateHide()
    {
        StopAllCoroutines();
        EndDrag();
        CurrentState = PresentationState.Close;
        if (stage != null)
        {
            stage.SetActive(false);
        }

        if (view != null)
        {
            view.gameObject.SetActive(false);
        }
    }

    /// <summary>UpdateCaseLid 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Mathf.Cos(float)、Mathf.Lerp(float, float, float)、Mathf.Sin(float)、Quaternion.Euler(float, float, float)、Transform.LookAt(Vector3, Vector3)、Transform.TransformPoint(Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CloseLid()、AttacheCasePresentationController.PlayOpenLid()、AttacheCasePresentationController.UpdateComposition()；下游（含内部回调体） GameObject.SetActive(bool)、Mathf.Cos(float)、Mathf.Lerp(float, float, float)、Mathf.Sin(float)、Quaternion.Euler(float, float, float)、Transform.LookAt(Vector3, Vector3)、Transform.TransformPoint(Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateCaseLid()
    {
        if (caseModelBinding != null)
        {
            caseModelBinding.ApplyOpenPose(expandExtent);
        }
        else if (caseLid != null)
        {
            caseLid.localRotation = Quaternion.Euler(caseLidOpenAngle * expandExtent, 0, 0);
        }

        if (itemRoot != null)
        {
            itemRoot.gameObject.SetActive(expandExtent > .18f);
        }

        if (presentationCamera != null)
        {
            // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = Mathf.Lerp(closeCaseTiltAngle, arrangeTiltAngle, expandExtent) * Mathf.Deg2Rad;
            // target（目标）：使用位置：作为 Transform.TransformPoint(Vector3) 的参数、AttacheCasePresentationController.UpdateCaseLid()。
Vector3 target = new Vector3(0, compositionCenter, 0);
            presentationCamera.transform.localPosition = target + new Vector3(0, -Mathf.Sin(angle), -Mathf.Cos(angle)) * 20f;
            presentationCamera.transform.LookAt(stage.transform.TransformPoint(target), stage.transform.up);
        }
    }

    /// <summary>CreateStage 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)、AttacheCasePresentationController.SetLayer(GameObject)、AttacheCasePresentationController.UpdateComposition()、Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;AttacheCaseProjectionInput&gt;() 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.Show()；下游（含内部回调体） AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)、AttacheCasePresentationController.SetLayer(GameObject)、AttacheCasePresentationController.UpdateComposition()、Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;AttacheCaseProjectionInput&gt;() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CreateStage()
    {
        if (stage != null)
        {
            return;
        }

        stage = new GameObject("公文包独立展示台");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage, gameObject.scene);
        stage.transform.position = Vector3.zero;
        caseBody = Instantiate(configuration.attacheCaseModel, stage.transform).transform;
        caseLid = caseBody.Find("箱盖铰链");
        caseModelBinding = caseBody.GetComponent<AttacheCaseModelBinding>();
        itemRoot = new GameObject("物品陈列").transform;
        itemRoot.SetParent(stage.transform, false);
        // cameraObject（摄像机对象）：使用位置：AttacheCasePresentationController.CreateStage()。
var cameraObject = new GameObject("公文包展示相机", typeof(Camera));
        cameraObject.transform.SetParent(stage.transform, false);
        presentationCamera = cameraObject.GetComponent<Camera>();
        presentationCamera.orthographic = false;
        presentationCamera.nearClipPlane = 0.1f;
        presentationCamera.farClipPlane = 50f;
        presentationCamera.clearFlags = CameraClearFlags.SolidColor;
        presentationCamera.backgroundColor = new Color(0, 0, 0, 0);
        presentationCamera.cullingMask = 1 << configuration.presentationLayer;
        presentationCamera.allowHDR = false;
        CreateLight("暖色主光", new Vector3(-4f, 5f, -7f), new Color(1f, 0.89f, 0.72f), 3.4f);
        CreateLight("冷色补光", new Vector3(5f, 1f, -5f), new Color(0.65f, 0.73f, 0.83f), 2.1f);
        SetLayer(stage);
        // imageObject（图像对象）：使用位置：AttacheCasePresentationController.CreateStage()。
var imageObject = new GameObject("三维公文包画面", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(mesh.gridArea, false);
        view = imageObject.GetComponent<RawImage>();
        SetCraftingBackdrop(craftingBackdrop);
        // input（输入数据）：保存 GameObject.AddComponent<AttacheCaseProjectionInput>() 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.CreateStage()。
var input = imageObject.AddComponent<AttacheCaseProjectionInput>();
        input.presentation = this;
        input.mesh = mesh;
        planeMeshGroup = mesh.gridContainer.GetComponent<CanvasGroup>();
        if (planeMeshGroup == null)
        {
            planeMeshGroup = mesh.gridContainer.gameObject.AddComponent<CanvasGroup>();
        }

        planeMeshGroup.alpha = 0;
        planeMeshGroup.blocksRaycasts = false;
        planeMeshGroup.interactable = false;
        // hintObject（提示对象）：使用位置：AttacheCasePresentationController.CreateStage()。
var hintObject = new GameObject("落点轮廓", typeof(RectTransform), typeof(Image));
        hintObject.transform.SetParent(mesh.gridContainer, false);
        landPointPrompt = hintObject.GetComponent<Image>();
        landPointPrompt.raycastTarget = false;
        landPointPrompt.gameObject.SetActive(false);
        UpdateComposition();
    }

    /// <summary>CreateLight 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Light&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CreateStage()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Light&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)。</param>
    /// <param name="colorValue">中文含义：颜色值。使用位置：AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)。</param>
    /// <param name="brightness">中文含义：亮度。使用位置：AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)。</param>
    void CreateLight(string name, Vector3 position, Color colorValue, float brightness)
    {
        // go（当前游戏对象）：使用位置：AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)。
var go = new GameObject(name, typeof(Light));
        go.transform.SetParent(stage.transform, false);
        go.transform.localPosition = position;
        // light（光源）：保存 GameObject.GetComponent<Light>() 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.CreateLight(string, Vector3, Color, float)。
var light = go.GetComponent<Light>();
        light.type = LightType.Point;
        light.range = 22f;
        light.color = colorValue;
        light.intensity = brightness;
        light.cullingMask = 1 << configuration.presentationLayer;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.45f;
    }

    /// <summary>SetLayer 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.RebuildSpaceCellLine()、AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)、AttacheCasePresentationController.UpdateSpaceOverride(Image)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。使用位置：AttacheCasePresentationController.SetLayer(GameObject)。</param>
    void SetLayer(GameObject root)
    {
        // t（当前变换节点）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：AttacheCasePresentationController.SetLayer(GameObject)。
foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = configuration.presentationLayer;
        }
    }

    /// <summary>UpdateComposition 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.RebuildSpaceCellLine()、AttacheCasePresentationController.ReleaseTexture()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Atan(float)、Mathf.Min(int, int)、Mathf.RoundToInt(float)、RenderTexture.Create() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CreateStage()、AttacheCasePresentationController.LateUpdate()；下游（含内部回调体） AttacheCasePresentationController.RebuildSpaceCellLine()、AttacheCasePresentationController.ReleaseTexture()、AttacheCasePresentationController.UpdateCaseLid()、Mathf.Atan(float)、Mathf.Min(int, int)、Mathf.RoundToInt(float)、RenderTexture.Create() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateComposition()
    {
        if (mesh.TotalWidth <= 0 || mesh.TotalHeight <= 0)
        {
            return;
        }

        lastWidth = mesh.TotalWidth;
        lastHeight = mesh.TotalHeight;
        pixelRatio = 10f / lastWidth;
        // heightValue（高度值）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。
float heightValue = lastHeight * pixelRatio;
        caseBody.localScale = new Vector3(1f, heightValue / 6f, 1f);
        // upperEdge（上部边缘）：使用位置：AttacheCasePresentationController.UpdateComposition()。
float upperEdge = heightValue * 0.42f + 0.7f;
        if (caseModelBinding != null)
        {
            // 原版箱盖向外转动时会靠近展示相机，给投影保留上沿空间。
            upperEdge += heightValue * 0.36f;
        }
        // lowerEdge（下部边缘）：使用位置：AttacheCasePresentationController.UpdateComposition()。
float lowerEdge = 1.5f;
        // renderHeight（渲染高度）：使用位置：作为 Mathf.Atan(float) 的参数、作为 Vector2.Vector2(float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。
float renderHeight = heightValue + upperEdge + lowerEdge;
        // centerHeight（中心高度）：使用位置：作为 Vector2.Vector2(float, float) 的参数、AttacheCasePresentationController.UpdateComposition()。
float centerHeight = (upperEdge - lowerEdge) * 0.5f;
        compositionCenter = centerHeight;
        presentationCamera.fieldOfView = 2f * Mathf.Atan(renderHeight * .5f / 20f) * Mathf.Rad2Deg;
        UpdateCaseLid();
        presentationCamera.aspect = 17f / renderHeight;
        // rt（界面矩形变换）：使用位置：AttacheCasePresentationController.UpdateComposition()。
var rt = view.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(17f, renderHeight) / pixelRatio;
        ApplyCaseBackdropPlacement();
        // widthValue（宽度值）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.RoundToInt(float) 的参数、作为 RenderTexture.RenderTexture(int, int, int, RenderTextureFormat) 的参数、AttacheCasePresentationController.UpdateComposition()。
int widthValue = Mathf.Min(configuration.textureWidth, SystemInfo.maxTextureSize);
        // heightValue2（高度值2）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：作为 RenderTexture.RenderTexture(int, int, int, RenderTextureFormat) 的参数、AttacheCasePresentationController.UpdateComposition()。
int heightValue2 = Mathf.RoundToInt(widthValue / presentationCamera.aspect);
        if (textureValue == null || textureValue.width != widthValue || textureValue.height != heightValue2)
        {
            ReleaseTexture();
            textureValue = new RenderTexture(widthValue, heightValue2, 24, RenderTextureFormat.ARGB32);
            textureValue.name = "公文包展示纹理";
            textureValue.antiAliasing = 2;
            textureValue.Create();
            presentationCamera.targetTexture = textureValue;
            view.texture = textureValue;
        }

        RebuildSpaceCellLine();
    }

    /// <summary>Synchronize 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.CreateCountText(GridItem)、AttacheCasePresentationController.PresentationInstance.PresentationInstance()、AttacheCasePresentationController.SetLayer(GameObject)、Bounds.Bounds(Vector3, Vector3)、Bounds.Encapsulate(Bounds)、Debug.LogError(object, Object)、Dictionary&lt;GridItem, AttacheCasePresentationController.PresentationInstance&gt;.Add(GridItem, AttacheCasePresentationController.PresentationInstance) 等共 26 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） AttacheCasePresentationController.CreateCountText(GridItem)、AttacheCasePresentationController.PresentationInstance.PresentationInstance()、AttacheCasePresentationController.SetLayer(GameObject)、Bounds.Bounds(Vector3, Vector3)、Bounds.Encapsulate(Bounds)、Debug.LogError(object, Object)、Dictionary&lt;GridItem, AttacheCasePresentationController.PresentationInstance&gt;.Add(GridItem, AttacheCasePresentationController.PresentationInstance) 等共 26 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="data">中文含义：当前背包网格物品集合。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)。</param>
    public void Synchronize(IReadOnlyList<GridItem> data)
    {
        if (stage == null || data == null)
        {
            return;
        }

        recoveryList.Clear();
        // pair（当前键值配对）：每轮从表现物品取得一个元素，仅代表当前这一项。使用位置：作为 List<GridItem>.Add(GridItem) 的参数、作为 object.ReferenceEquals(object, object) 的参数。
foreach (var pair in presentationItem)
        {
            // exists（是否存在）：使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
bool exists = false;
            // item（当前背包网格物品）：每轮从数据取得一个元素，仅代表当前这一项。使用位置：作为 object.ReferenceEquals(object, object) 的参数。
foreach (var item in data)
            {
                if (ReferenceEquals(item, pair.Key))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                recoveryList.Add(pair.Key);
            }
        }

        // key（键）：每轮从恢复列表取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<GridItem, AttacheCasePresentationController.PresentationInstance>.Remove(GridItem) 的参数、作为 Dictionary<GridItem, AttacheCasePresentationController.PresentationInstance>.this[GridItem] 的参数、作为 Dictionary<GridItem, Text>.Remove(GridItem) 的参数 等共 5 处。
foreach (var key in recoveryList)
        {
            // instance（实例）：使用位置：作为 Stack<AttacheCasePresentationController.PresentationInstance>.Push(AttacheCasePresentationController.PresentationInstance) 的参数、AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
var instance = presentationItem[key];
            instance.root.SetActive(false);
            // identifier（标识符）：使用位置：作为 Dictionary<string, Stack<AttacheCasePresentationController.PresentationInstance>>.this[string] 的参数、作为 Dictionary<string, Stack<AttacheCasePresentationController.PresentationInstance>>.TryGetValue(string, out Stack<AttacheCasePresentationController.PresentationInstance>) 的参数。
string identifier = key.item.itemId;
            if (!objectPool.TryGetValue(identifier, out var /* 中文含义：对象池。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pool))
            {
                objectPool[identifier] = pool = new Stack<PresentationInstance>();
            }

            pool.Push(instance);
            presentationItem.Remove(key);
            if (countMapping.TryGetValue(key, out var /* 中文含义：数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ count))
            {
                count.gameObject.SetActive(false);
                countPool.Push(count);
                countMapping.Remove(key);
            }
        }

        // item（当前背包网格物品）：每轮从数据取得一个元素，仅代表当前这一项。使用位置：作为 AttacheCasePresentationController.CreateCountText(GridItem) 的参数、作为 Debug.LogError(object, Object) 的参数、作为 Dictionary<GridItem, AttacheCasePresentationController.PresentationInstance>.Add(GridItem, AttacheCasePresentationController.PresentationInstance) 的参数 等共 8 处。
foreach (var item in data)
        {
            if (item == null || item.item == null || presentationItem.ContainsKey(item))
            {
                continue;
            }

            // config（当前物品表现）：保存 InventoryPresentationConfiguration.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform) 的参数、作为 Quaternion.Euler(Vector3) 的参数、AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
var config = configuration.Find(item.item.itemId);
            if (config == null || config.model == null)
            {
                Debug.LogError("缺少背包模型：" + item.item.displayName, this);
                continue;
            }

            // instance（实例）：使用位置：作为 Dictionary<GridItem, AttacheCasePresentationController.PresentationInstance>.Add(GridItem, AttacheCasePresentationController.PresentationInstance) 的参数、AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
PresentationInstance instance;
            if (objectPool.TryGetValue(item.item.itemId, out var /* 中文含义：对象池。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pool) && pool.Count > 0)
            {
                instance = pool.Pop();
                instance.root.SetActive(true);
            }
            else
            {
                // root（根游戏对象）：使用位置：作为 AttacheCasePresentationController.SetLayer(GameObject) 的参数、作为 Bounds.Bounds(Vector3, Vector3) 的参数、作为 Object.Instantiate<GameObject>(GameObject, Transform) 的参数 等共 4 处。
var root = new GameObject("陈列_" + item.item.displayName);
                root.transform.SetParent(itemRoot, false);
                // model（模型）：保存 Object.Instantiate<GameObject>(GameObject, Transform) 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
var model = Instantiate(config.model, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(config.orientation);
                root.transform.localPosition = Vector3.zero;
                // renderers（渲染器集合）：保存 GameObject.GetComponentsInChildren<Renderer>() 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
var renderers = model.GetComponentsInChildren<Renderer>();
                // bounds（包围盒）：使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
                // firstElement（是否首个元素）：使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
bool firstElement = true;
                // renderer（渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数、AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
foreach (var renderer in renderers)
                {
                    if (firstElement)
                    {
                        bounds = renderer.bounds;
                        firstElement = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                // center（中心）：使用位置：AttacheCasePresentationController.Synchronize(IReadOnlyList<GridItem>)。
Vector3 center = bounds.center - root.transform.position;
                model.transform.localPosition = -center;
                instance = new PresentationInstance
                {
                    root = root,
                    size = bounds.size,
                    configuration = config
                };
                SetLayer(root);
            }

            instance.data = item;
            presentationItem.Add(item, instance);
            CreateCountText(item);
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.UpdateComposition()、AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform)、AttacheCasePresentationController.UpdateSpaceOverride(Image)、GameObject.SetActive(bool)、InventoryGridView.GetItemWidget(GridItem)、Mathf.Approximately(float, float)、Mathf.Max(float, float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (CurrentState == PresentationState.Close || stage == null)
        {
            return;
        }

        if (!Mathf.Approximately(lastWidth, mesh.TotalWidth) || !Mathf.Approximately(lastHeight, mesh.TotalHeight))
        {
            UpdateComposition();
        }

        if (textureValue != null && !textureValue.IsCreated())
        {
            textureValue.Create();
        }

        UpdateSpaceOverride(mesh.SelectedOverride);
        UpdateSpaceOverride(mesh.HoverOverride);
        UpdateSpaceOverride(landPointPrompt);
        if (view.transform.GetSiblingIndex() != 0)
        {
            view.transform.SetAsFirstSibling();
        }

        // pair（当前键值配对）：每轮从表现物品取得一个元素，仅代表当前这一项。使用位置：AttacheCasePresentationController.LateUpdate()。
foreach (var pair in presentationItem)
        {
            // item（当前背包网格物品）：使用位置：作为 AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform) 的参数、作为 InventoryGridView.GetItemWidget(GridItem) 的参数、作为 Quaternion.Euler(float, float, float) 的参数 等共 4 处。
var item = pair.Key;
            // instance（实例）：使用位置：作为 Mathf.Max(float, float) 的参数、AttacheCasePresentationController.LateUpdate()。
var instance = pair.Value;
            // widget（界面控件）：保存 InventoryGridView.GetItemWidget(GridItem) 的返回结果，供当前步骤使用。使用位置：作为 AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform) 的参数、作为 GameObject.SetActive(bool) 的参数、AttacheCasePresentationController.LateUpdate()。
RectTransform widget = mesh.GetItemWidget(item);
            if (widget == null)
            {
                continue;
            }

            // width（宽度）：使用位置：作为 Mathf.Min(float, float) 的参数。
            PlaceItemModel(instance, item, widget);
            instance.root.SetActive(widget.gameObject.activeInHierarchy);
            UpdateCountText(item, widget);
        }
    }

    /// <summary>BeginDrag 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemPointerDown(GridItem, Vector2)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：AttacheCasePresentationController.BeginDrag(GridItem)。</param>
    private void PlaceItemModel(PresentationInstance instance, GridItem item, RectTransform widget)
    {
        Vector3 size = instance.size;
        if ((item.rotation & 1) != 0)
        {
            (size.x, size.y) = (size.y, size.x);
        }

        Vector3 cameraPosition = presentationCamera.transform.localPosition;
        Vector3 positionOnGrid = (Vector3)widget.anchoredPosition * pixelRatio + instance.configuration.offset;
        float cameraDistance = -cameraPosition.z;
        float clearance = 0.08f - positionOnGrid.z;
        if (dragItem == item)
        {
            clearance += 0.28f;
        }
        positionOnGrid.z = 0f;

        // 按透视镜头投影后的占格尺寸计算比例，模型靠近镜头时仍留在对应格子内。
        Vector2 footprint = widget.rect.size * (pixelRatio * instance.configuration.gridOccupancyRatio);
        float skewX = Mathf.Abs(positionOnGrid.x - cameraPosition.x) / cameraDistance;
        float skewY = Mathf.Abs(positionOnGrid.y - cameraPosition.y) / cameraDistance;
        float width = size.x + size.z * (skewX + footprint.x / cameraDistance);
        float height = size.y + size.z * (skewY + footprint.y / cameraDistance);
        float scale = Mathf.Min(footprint.x / Mathf.Max(0.001f, width), footprint.y / Mathf.Max(0.001f, height))
            * (1f - clearance / cameraDistance);
        float depth = 1f - (clearance + size.z * scale * 0.5f) / cameraDistance;

        Transform root = instance.root.transform;
        root.localPosition = Vector3.LerpUnclamped(cameraPosition, positionOnGrid, depth);
        root.localScale = Vector3.one * scale;
        root.localRotation = Quaternion.Euler(0f, 0f, item.rotation * 90f);
    }

    public void BeginDrag(GridItem item)
    {
        dragItem = item;
    }

    internal void ShowTransferTarget(bool visible, Color color)
    {
        if (landPointPrompt == null)
        {
            return;
        }

        // 寄存取回由背包规则寻找空位，高亮整个可投放的箱内网格。
        landPointPrompt.rectTransform.anchoredPosition = Vector2.zero;
        landPointPrompt.rectTransform.sizeDelta = new Vector2(mesh.TotalWidth, mesh.TotalHeight);
        landPointPrompt.color = color;
        landPointPrompt.gameObject.SetActive(visible);
    }

    /// <summary>EndDrag 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.CloseLid()、AttacheCasePresentationController.ImmediateHide()、InventoryUI.OnItemPointerUp(GridItem, Vector2)、InventoryUI.PlayExitAnimation(Action)；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndDrag()
    {
        dragItem = null;
        if (landPointPrompt != null)
        {
            landPointPrompt.gameObject.SetActive(false);
        }
    }

    /// <summary>PreviewLandPoint 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)、InventoryManager.CanCombine(GridItem, GridItem)、InventoryManager.GetItemAt(int, int)、Transform.SetAsLastSibling()、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemDrag(GridItem, Vector2)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)、InventoryManager.CanCombine(GridItem, GridItem)、InventoryManager.GetItemAt(int, int)、Transform.SetAsLastSibling()、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int) 的参数、作为 InventoryManager.CanCombine(GridItem, GridItem) 的参数、作为 Vector2.Vector2(float, float) 的参数 等共 4 处。</param>
    /// <param name="screenPosition">中文含义：屏幕位置。使用位置：作为 InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int) 的参数。</param>
    /// <param name="offset">中文含义：偏移量。使用位置：作为 InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int) 的参数。</param>
    public void PreviewLandPoint(GridItem item, Vector2 screenPosition, Vector2 offset)
    {
        if (landPointPrompt == null || InventoryManager.Instance == null)
        {
            return;
        }

        // mgr（当前背包管理器）：使用位置：AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
var mgr = InventoryManager.Instance;
        if (!mesh.TryGetDropGrid(item, screenPosition, offset, out int /* 中文含义：横向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ x, out int /* 中文含义：纵向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ y))
        {
            landPointPrompt.gameObject.SetActive(false);
            return;
        }

        // valid（是否有效）：使用位置：AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
bool valid = x >= 0 && y >= 0 && x + item.Width <= mgr.CaseWidth && y + item.Height <= mgr.CaseHeight;
        if (valid)
        {
            // row（行）：使用位置：作为 InventoryManager.GetItemAt(int, int) 的参数、AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
for (int row = y; row < y + item.Height; row++)
            {
                // col（列）：使用位置：作为 InventoryManager.GetItemAt(int, int) 的参数、AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
for (int col = x; col < x + item.Width; col++)
                {
                    // other（另一个背包网格物品）：保存 InventoryManager.GetItemAt(int, int) 的返回结果，供当前步骤使用。使用位置：作为 InventoryManager.CanCombine(GridItem, GridItem) 的参数、AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
var other = mgr.GetItemAt(col, row);
                    if (other != null && other != item && !mgr.CanCombine(item, other))
                    {
                        valid = false;
                    }
                }
            }
        }

        // step（步进）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float step = mesh.CurrentCell + mesh.cellSpacing;
        // size（尺寸）：使用位置：作为 Vector2.Vector2(float, float) 的参数、AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
Vector2 size = new Vector2(item.Width * step - mesh.cellSpacing, item.Height * step - mesh.cellSpacing);
        landPointPrompt.rectTransform.sizeDelta = size;
        landPointPrompt.rectTransform.anchoredPosition = new Vector2(x * step + size.x / 2f - mesh.TotalWidth / 2f, -y * step - size.y / 2f + mesh.TotalHeight / 2f);
        // assignmentReceiver5（赋值前保存的目标落地点提示）：从落地点提示读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)。
global::UnityEngine.UI.Image assignmentReceiver5 = landPointPrompt;
        if (valid)
        {
            assignmentReceiver5.color = new Color(0.68f, 0.72f, 0.61f, 0.2f);
        }
        else
        {
            assignmentReceiver5.color = new Color(0.65f, 0.15f, 0.1f, 0.3f);
        }

        landPointPrompt.gameObject.SetActive(true);
        landPointPrompt.transform.SetAsLastSibling();
    }

    // 鼠标先经过展示相机反投影，再使用原网格规则计算占格。
    /// <summary>ScreenRotateMesh 的业务处理入口；本方法直接依赖 Camera.ViewportPointToRay(Vector3)、Component.GetComponentInParent&lt;Canvas&gt;()、Plane.Plane(Vector3, Vector3)、Plane.Raycast(Ray, out float)、Ray.GetPoint(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Transform.InverseTransformPoint(Vector3) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCaseProjectionInput.Find(Vector2)、AttacheCaseProjectionInput.OnPointerClick(PointerEventData)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)；下游（含内部回调体） Camera.ViewportPointToRay(Vector3)、Component.GetComponentInParent&lt;Canvas&gt;()、Plane.Plane(Vector3, Vector3)、Plane.Raycast(Ray, out float)、Ray.GetPoint(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Transform.InverseTransformPoint(Vector3) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="screenPosition">中文含义：屏幕位置。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数。</param>
    /// <param name="meshPosition">中文含义：网格位置。输出参数，由本入口写入并交给调用方。使用位置：AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2)。</param>
    public bool ScreenRotateMesh(Vector2 screenPosition, out Vector2 meshPosition)
    {
        meshPosition = Vector2.zero;
        if (view == null || presentationCamera == null || pixelRatio <= 0)
        {
            return false;
        }

        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2)。
Canvas canvas = view.GetComponentInParent<Canvas>();
        // camera（摄像机）：使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2)。
Camera camera;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }
        else
        {
            camera = null;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view.rectTransform, screenPosition, camera, out var /* 中文含义：局部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ local))
        {
            return false;
        }

        // rect（矩形）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
Rect rect = view.rectTransform.rect;
        // uv（纹理坐标）：使用位置：作为 Camera.ViewportPointToRay(Vector3) 的参数。
Vector2 uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        // ray（射线）：保存 Camera.ViewportPointToRay(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Plane.Raycast(Ray, out float) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数。
Ray ray = presentationCamera.ViewportPointToRay(uv);
        // plane（平面）：使用位置：AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2)。
Plane plane = new Plane(stage.transform.forward, stage.transform.TransformPoint(new Vector3(0, 0, -.04f)));
        if (!plane.Raycast(ray, out float /* 中文含义：距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ distance))
        {
            return false;
        }

        // point（点）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Vector2(float, float) 的参数。
Vector3 point = stage.transform.InverseTransformPoint(ray.GetPoint(distance));
        meshPosition = new Vector2(point.x, point.y) / pixelRatio;
        return true;
    }

    /// <summary>MeshRotateScreen 的业务处理入口；本方法直接依赖 Camera.WorldToViewportPoint(Vector3)、Component.GetComponentInParent&lt;Canvas&gt;()、RectTransformUtility.WorldToScreenPoint(Camera, Vector3)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Camera.WorldToViewportPoint(Vector3)、Component.GetComponentInParent&lt;Canvas&gt;()、RectTransformUtility.WorldToScreenPoint(Camera, Vector3)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="meshPosition">中文含义：网格位置。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    public Vector2 MeshRotateScreen(Vector2 meshPosition)
    {
        // world（世界空间）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Camera.WorldToViewportPoint(Vector3) 的参数。
Vector3 world = stage.transform.TransformPoint(new Vector3(meshPosition.x * pixelRatio, meshPosition.y * pixelRatio, -.04f));
        // uv（纹理坐标）：保存 Camera.WorldToViewportPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 uv = presentationCamera.WorldToViewportPoint(world);
        // rect（矩形）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Rect rect = view.rectTransform.rect;
        // point（点）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 RectTransformUtility.WorldToScreenPoint(Camera, Vector3) 的参数。
Vector3 point = view.rectTransform.TransformPoint(new Vector3(rect.xMin + uv.x * rect.width, rect.yMin + uv.y * rect.height, 0));
        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.MeshRotateScreen(Vector2)。
Canvas canvas = view.GetComponentInParent<Canvas>();
        // selectedValue8（分支选出的画布的世界空间摄像机）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 RectTransformUtility.WorldToScreenPoint(Camera, Vector3) 的参数、AttacheCasePresentationController.MeshRotateScreen(Vector2)。
global::UnityEngine.Camera selectedValue8;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            selectedValue8 = canvas.worldCamera;
        }
        else
        {
            selectedValue8 = null;
        }

        return RectTransformUtility.WorldToScreenPoint(selectedValue8, point);
    }

    /// <summary>RebuildSpaceCellLine 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.AddCellLine(List&lt;Vector3&gt;, List&lt;int&gt;, float, float, float, float)、AttacheCasePresentationController.SetLayer(GameObject)、Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、List&lt;int&gt;.List() 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.UpdateComposition()；下游（含内部回调体） AttacheCasePresentationController.AddCellLine(List&lt;Vector3&gt;, List&lt;int&gt;, float, float, float, float)、AttacheCasePresentationController.SetLayer(GameObject)、Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、List&lt;int&gt;.List() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RebuildSpaceCellLine()
    {
        if (spaceCellLine == null)
        {
            spaceCellLine = new GameObject("箱内实体格线", typeof(MeshFilter), typeof(MeshRenderer));
            spaceCellLine.transform.SetParent(stage.transform, false);
            if (configuration.cellLineMaterial != null)
            {
                cellLineMaterial = new Material(configuration.cellLineMaterial);
            }
            else
            {
                cellLineMaterial = new Material(Shader.Find("Unlit/Color"));
            }

            cellLineMaterial.color = new Color(.34f, .36f, .33f);
            spaceCellLine.GetComponent<MeshRenderer>().sharedMaterial = cellLineMaterial;
            spaceCellLine.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetLayer(spaceCellLine);
        }

        if (cellLineMesh != null)
        {
            Destroy(cellLineMesh);
        }

        // vertices（顶点集合）：使用位置：作为 AttacheCasePresentationController.AddCellLine(List<Vector3>, List<int>, float, float, float, float) 的参数、作为 Mesh.SetVertices(List<Vector3>) 的参数。
var vertices = new List<Vector3>();
        // indices（索引集合）：使用位置：作为 AttacheCasePresentationController.AddCellLine(List<Vector3>, List<int>, float, float, float, float) 的参数、作为 Mesh.SetTriangles(List<int>, int) 的参数。
var indices = new List<int>();

        // 中文含义：width 为“宽度”。；height 为“高度”。
float width = mesh.TotalWidth * pixelRatio, height = mesh.TotalHeight * pixelRatio;
        // step（步进）：使用位置：作为 Mathf.Min(float, float) 的参数。
float step = (mesh.CurrentCell + mesh.cellSpacing) * pixelRatio;
        // columns（列数）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.RebuildSpaceCellLine()。
int columns = Mathf.RoundToInt((mesh.TotalWidth + mesh.cellSpacing) / (mesh.CurrentCell + mesh.cellSpacing));
        // rows（行数）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：AttacheCasePresentationController.RebuildSpaceCellLine()。
int rows = Mathf.RoundToInt((mesh.TotalHeight + mesh.cellSpacing) / (mesh.CurrentCell + mesh.cellSpacing));
        // i（当前遍历索引）：使用位置：作为 Mathf.Min(float, float) 的参数、AttacheCasePresentationController.RebuildSpaceCellLine()。
for (int i = 0; i <= columns; i++)
        {
            // x（横向）：使用位置：作为 AttacheCasePresentationController.AddCellLine(List<Vector3>, List<int>, float, float, float, float) 的参数。
float x = Mathf.Min(width, i * step) - width * .5f;
            AddCellLine(vertices, indices, x - .008f, -height * .5f, x + .008f, height * .5f);
        }

        // i（当前遍历索引）：使用位置：作为 Mathf.Min(float, float) 的参数、AttacheCasePresentationController.RebuildSpaceCellLine()。
for (int i = 0; i <= rows; i++)
        {
            // y（纵向）：使用位置：作为 AttacheCasePresentationController.AddCellLine(List<Vector3>, List<int>, float, float, float, float) 的参数。
float y = height * .5f - Mathf.Min(height, i * step);
            AddCellLine(vertices, indices, -width * .5f, y - .008f, width * .5f, y + .008f);
        }

        cellLineMesh = new Mesh
        {
            name = "箱内格线网格"
        };
        cellLineMesh.SetVertices(vertices);
        cellLineMesh.SetTriangles(indices, 0);
        cellLineMesh.RecalculateBounds();
        spaceCellLine.GetComponent<MeshFilter>().sharedMesh = cellLineMesh;
    }

    /// <summary>AddCellLine 的业务处理入口；本方法直接依赖 List&lt;int&gt;.Add(int)、List&lt;Vector3&gt;.Add(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.RebuildSpaceCellLine()；下游（含内部回调体） List&lt;int&gt;.Add(int)、List&lt;Vector3&gt;.Add(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="v">中文含义：当前三维向量集合。使用位置：AttacheCasePresentationController.AddCellLine(List&lt;Vector3&gt;, List&lt;int&gt;, float, float, float, float)。</param>
    /// <param name="t">中文含义：当前整数集合。使用位置：AttacheCasePresentationController.AddCellLine(List&lt;Vector3&gt;, List&lt;int&gt;, float, float, float, float)。</param>
    /// <param name="left">中文含义：左侧。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="bottom">中文含义：底部。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="right">中文含义：右侧。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="top">中文含义：顶部。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    static void AddCellLine(List<Vector3> v, List<int> t, float left, float bottom, float right, float top)
    {
        // n（添加格线前已有的顶点数量）：使用位置：作为 List<int>.Add(int) 的参数。
int n = v.Count;
        v.Add(new Vector3(left, bottom, -.04f));
        v.Add(new Vector3(left, top, -.04f));
        v.Add(new Vector3(right, top, -.04f));
        v.Add(new Vector3(right, bottom, -.04f));
        t.Add(n);
        t.Add(n + 1);
        t.Add(n + 2);
        t.Add(n);
        t.Add(n + 2);
        t.Add(n + 3);
    }

    /// <summary>UpdateSpaceOverride 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.SetLayer(GameObject)、Dictionary&lt;Image, MeshRenderer&gt;.Add(Image, MeshRenderer)、Dictionary&lt;Image, MeshRenderer&gt;.TryGetValue(Image, out MeshRenderer)、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、GameObject.SetActive(bool) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.LateUpdate()；下游（含内部回调体） AttacheCasePresentationController.SetLayer(GameObject)、Dictionary&lt;Image, MeshRenderer&gt;.Add(Image, MeshRenderer)、Dictionary&lt;Image, MeshRenderer&gt;.TryGetValue(Image, out MeshRenderer)、GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、GameObject.SetActive(bool) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源图像组件。使用位置：作为 Dictionary&lt;Image, MeshRenderer&gt;.Add(Image, MeshRenderer) 的参数、作为 Dictionary&lt;Image, MeshRenderer&gt;.TryGetValue(Image, out MeshRenderer) 的参数、作为 GameObject.SetActive(bool) 的参数 等共 5 处。</param>
    void UpdateSpaceOverride(Image source)
    {
        if (source == null)
        {
            return;
        }

        if (!overrideMapping.TryGetValue(source, out var /* 中文含义：渲染器。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ renderer))
        {
            // go（当前游戏对象）：保存 GameObject.CreatePrimitive(PrimitiveType) 的返回结果，供当前步骤使用。使用位置：作为 AttacheCasePresentationController.SetLayer(GameObject) 的参数、作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.UpdateSpaceOverride(Image)。
var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "箱内" + source.name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(stage.transform, false);
            renderer = go.GetComponent<MeshRenderer>();
            // material（材质）：使用位置：作为 List<Material>.Add(Material) 的参数、AttacheCasePresentationController.UpdateSpaceOverride(Image)。
global::UnityEngine.Material material;
            if (configuration.overrideMaterial != null)
            {
                material = new Material(configuration.overrideMaterial);
            }
            else
            {
                material = new Material(Shader.Find("菜单/箱内覆盖"));
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spaceMaterial.Add(material);
            overrideMapping.Add(source, renderer);
            SetLayer(go);
        }

        renderer.gameObject.SetActive(Interactive && source.gameObject.activeInHierarchy);
        if (!renderer.gameObject.activeSelf)
        {
            return;
        }

        // pos（位置）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector2 pos = source.rectTransform.anchoredPosition * pixelRatio;
        renderer.transform.localPosition = new Vector3(pos.x, pos.y, -.055f);
        renderer.transform.localScale = new Vector3(source.rectTransform.rect.width * pixelRatio, source.rectTransform.rect.height * pixelRatio, 1);
        renderer.sharedMaterial.color = source.color;
    }

    /// <summary>CreateCountText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Stack&lt;Text&gt;.Pop()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Stack&lt;Text&gt;.Pop()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 Dictionary&lt;GridItem, Text&gt;.this[GridItem] 的参数。</param>
    void CreateCountText(GridItem item)
    {
        // text（文字）：使用位置：AttacheCasePresentationController.CreateCountText(GridItem)。
Text text;
        if (countPool.Count > 0)
        {
            text = countPool.Pop();
        }
        else
        {
            // go（当前游戏对象）：使用位置：AttacheCasePresentationController.CreateCountText(GridItem)。
var go = new GameObject("三维物品数量", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(view.transform, false);
            text = go.GetComponent<Text>();
            // assignmentReceiver11（赋值前保存的目标文字）：从文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：AttacheCasePresentationController.CreateCountText(GridItem)。
global::UnityEngine.UI.Text assignmentReceiver11 = text;
            if (mesh.visualTheme != null)
            {
                assignmentReceiver11.font = mesh.visualTheme.interfaceFont;
            }
            else
            {
                assignmentReceiver11.font = null;
            }

            text.fontSize = 18;
            text.alignment = TextAnchor.LowerRight;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.pivot = new Vector2(1, 0);
            text.rectTransform.sizeDelta = new Vector2(64, 30);
        }

        countMapping[item] = text;
    }

    /// <summary>UpdateCountText 的业务处理入口；本方法直接依赖 Camera.WorldToViewportPoint(Vector3)、Dictionary&lt;GridItem, Text&gt;.TryGetValue(GridItem, out Text)、GameObject.SetActive(bool)、int.ToString()、Transform.TransformPoint(Vector3)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.LateUpdate()；下游（含内部回调体） Camera.WorldToViewportPoint(Vector3)、Dictionary&lt;GridItem, Text&gt;.TryGetValue(GridItem, out Text)、GameObject.SetActive(bool)、int.ToString()、Transform.TransformPoint(Vector3)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 Dictionary&lt;GridItem, Text&gt;.TryGetValue(GridItem, out Text) 的参数、作为 GameObject.SetActive(bool) 的参数、AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform)。</param>
    /// <param name="widget">中文含义：界面控件。使用位置：作为 GameObject.SetActive(bool) 的参数、作为 Vector2.Vector2(float, float) 的参数、AttacheCasePresentationController.UpdateCountText(GridItem, RectTransform)。</param>
    internal void SetCraftingBackdrop(bool visible)
    {
        craftingBackdrop = visible;
        if (view != null)
        {
            view.raycastTarget = !visible;
            if (pixelRatio > 0f)
            {
                ApplyCaseBackdropPlacement();
            }
        }
    }

    private void ApplyCaseBackdropPlacement()
    {
        RectTransform rect = view.rectTransform;
        if (craftingBackdrop)
        {
            view.color = new Color(.56f, .56f, .56f, .65f);
            rect.localScale = Vector3.one * .87f;
            rect.anchoredPosition = new Vector2(rect.sizeDelta.x * .21f, compositionCenter / pixelRatio);
        }
        else
        {
            view.color = Color.white;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(0f, compositionCenter / pixelRatio);
        }
    }

    void UpdateCountText(GridItem item, RectTransform widget)
    {
        if (!countMapping.TryGetValue(item, out var /* 中文含义：文字。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ text))
        {
            return;
        }

        text.gameObject.SetActive(!craftingBackdrop && Interactive && widget.gameObject.activeInHierarchy && item.count > 1);
        if (!text.gameObject.activeSelf)
        {
            return;
        }

        text.text = item.count.ToString();
        // corner（角落）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector2 corner = widget.anchoredPosition + new Vector2(widget.rect.width * .5f - 4, -widget.rect.height * .5f + 4);
        // world（世界空间）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Camera.WorldToViewportPoint(Vector3) 的参数。
Vector3 world = stage.transform.TransformPoint(new Vector3(corner.x * pixelRatio, corner.y * pixelRatio, -.04f));
        // uv（纹理坐标）：保存 Camera.WorldToViewportPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Vector2(float, float) 的参数。
Vector3 uv = presentationCamera.WorldToViewportPoint(world);
        // rect（矩形）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
Rect rect = view.rectTransform.rect;
        text.rectTransform.anchoredPosition = new Vector2(rect.xMin + uv.x * rect.width, rect.yMin + uv.y * rect.height);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.ImmediateHide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        ImmediateHide();
    }

    /// <summary>ReleaseTexture 的业务处理入口；本方法直接依赖 Object.Destroy(Object)、RenderTexture.Release()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.OnDestroy()、AttacheCasePresentationController.UpdateComposition()；下游（含内部回调体） Object.Destroy(Object)、RenderTexture.Release()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ReleaseTexture()
    {
        if (presentationCamera != null)
        {
            presentationCamera.targetTexture = null;
        }

        if (view != null)
        {
            view.texture = null;
        }

        if (textureValue != null)
        {
            textureValue.Release();
            Destroy(textureValue);
        }

        textureValue = null;
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.ReleaseTexture()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        ReleaseTexture();
        if (cellLineMesh != null)
        {
            Destroy(cellLineMesh);
        }

        if (cellLineMaterial != null)
        {
            Destroy(cellLineMaterial);
        }

        // material（材质）：每轮从空间材质取得一个元素，仅代表当前这一项。使用位置：作为 Object.Destroy(Object) 的参数、AttacheCasePresentationController.OnDestroy()。
foreach (var material in spaceMaterial)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        if (stage != null)
        {
            Destroy(stage);
        }

        if (view != null)
        {
            Destroy(view.gameObject);
        }

        if (landPointPrompt != null)
        {
            Destroy(landPointPrompt.gameObject);
        }
    }
}
