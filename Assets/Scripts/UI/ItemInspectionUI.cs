using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首次获得物品检视界面。
/// 冻结并柔化当前画面，在右侧以独立相机展示静态三维物品。
/// </summary>
public class ItemInspectionUI : Singleton<ItemInspectionUI>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：留空时自动复用场景内已有中文界面字体。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)、ItemInspectionUI.ResolveFont()。</summary>
    [Header("界面字体")]
    [Tooltip("留空时自动复用场景内已有中文界面字体")]
    public Font interfaceFont;
    /// <summary>中文含义：进入持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Max(float, float) 的参数、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    [Header("入场与退场")]
    [Min(0.01f)]
    public float enterDuration = 0.25f;
    /// <summary>中文含义：退出持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、ItemInspectionUI.HideRoutine()。</summary>
    [Min(0.01f)]
    public float exitDuration = 0.18f;
    /// <summary>中文含义：输入防抖。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float inputDebounce = 0.25f;
    /// <summary>中文含义：背景降采样。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</summary>
    [Header("冻结背景")]
    [Range(4, 16)]
    public int backgroundDownsample = 10;
    /// <summary>中文含义：完整屏幕阴影色。使用位置：作为 ItemInspectionUI.CreateImage(string, Transform, Color) 的参数。</summary>
    public Color fullScreenShade = new Color(0f, 0f, 0f, 0.24f);
    /// <summary>中文含义：显示视野角度。使用位置：作为 Mathf.Max(float, float) 的参数、ItemInspectionUI.BuildDisplayStage()、ItemInspectionUI.PrepareModel(ItemData, GameObject)。</summary>
    [Header("三维展示")]
    [Range(15f, 55f)]
    public float displayFieldOfView = 30f;
    /// <summary>中文含义：自动帧填充。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(0.4f, 0.9f)]
    public float automaticFrameFill = 0.72f;
    /// <summary>中文含义：键光源颜色。使用位置：作为 ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float) 的参数。</summary>
    public Color keyLightColor = new Color(1f, 0.86f, 0.68f, 1f);
    /// <summary>中文含义：键光源强度。使用位置：作为 ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float) 的参数。</summary>
    [Range(0f, 3f)]
    public float keyLightIntensity = 1.35f;
    /// <summary>中文含义：填充光源颜色。使用位置：作为 ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float) 的参数。</summary>
    public Color fillLightColor = new Color(0.52f, 0.62f, 0.78f, 1f);
    /// <summary>中文含义：填充光源强度。使用位置：作为 ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float) 的参数。</summary>
    [Range(0f, 2f)]
    public float fillLightIntensity = 0.45f;
    /// <summary>中文含义：是否可见。使用位置：ItemInspectionUI.Show(ItemData)。</summary>
    public bool IsVisible => isVisible || isOpening || isClosing;

    /// <summary>中文含义：缺失的模型警告集合。只读字段，初始化后不由普通写入口替换。使用位置：ItemInspectionUI.ReportMissingModel(ItemData)。</summary>
    private readonly HashSet<string> missingModelWarnings = new HashSet<string>();
    /// <summary>中文含义：叠加层根节点。使用位置：作为 ItemInspectionUI.CreateImage(string, Transform, Color) 的参数、作为 ItemInspectionUI.CreateRawImage(string, Transform) 的参数、作为 ItemInspectionUI.Stretch(RectTransform) 的参数 等共 8 处。</summary>
    private GameObject overlayRoot;
    /// <summary>中文含义：叠加层分组。使用位置：ItemInspectionUI.BuildInterface()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private CanvasGroup overlayGroup;
    /// <summary>中文含义：物品分组。使用位置：ItemInspectionUI.BuildInterface()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private CanvasGroup itemGroup;
    /// <summary>中文含义：物品容器。使用位置：作为 ItemInspectionUI.Stretch(RectTransform) 的参数、ItemInspectionUI.BuildInterface()、ItemInspectionUI.HideRoutine() 等共 4 处。</summary>
    private RectTransform itemContainer;
    /// <summary>中文含义：冻结的背景。使用位置：作为 ItemInspectionUI.Stretch(RectTransform) 的参数、ItemInspectionUI.BuildInterface()、ItemInspectionUI.CaptureFrozenBackground() 等共 4 处。</summary>
    private RawImage frozenBackground;
    /// <summary>中文含义：模型图像。使用位置：作为 ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数、ItemInspectionUI.BuildInterface() 等共 5 处。</summary>
    private RawImage modelImage;
    /// <summary>中文含义：物品名称文字。使用位置：作为 ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2) 的参数、ItemInspectionUI.BuildInterface()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private Text itemNameText;
    /// <summary>中文含义：关闭提示文字。使用位置：作为 ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2) 的参数、ItemInspectionUI.BuildInterface()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private Text closeHintText;
    /// <summary>中文含义：冻结的纹理。使用位置：作为 Object.Destroy(Object) 的参数、ItemInspectionUI.CaptureFrozenBackground()、ItemInspectionUI.ReleaseFrozenBackground()。</summary>
    private Texture2D frozenTexture;
    /// <summary>中文含义：渐变纹理。使用位置：作为 Object.Destroy(Object) 的参数、ItemInspectionUI.BuildInterface()、ItemInspectionUI.OnDestroy()。</summary>
    private Texture2D gradientTexture;
    /// <summary>中文含义：模型纹理。使用位置：作为 Object.Destroy(Object) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)、ItemInspectionUI.ReleaseModelResources()。</summary>
    private RenderTexture modelTexture;
    /// <summary>中文含义：阶段根节点。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、ItemInspectionUI.BuildDisplayStage() 等共 4 处。</summary>
    private Transform stageRoot;
    /// <summary>中文含义：显示摄像机。使用位置：作为 Mathf.Max(float, float) 的参数、ItemInspectionUI.BuildDisplayStage()、ItemInspectionUI.PrepareModel(ItemData, GameObject) 等共 5 处。</summary>
    private Camera displayCamera;
    /// <summary>中文含义：当前模型。使用位置：作为 ItemInspectionUI.DisableModelBehaviours(GameObject) 的参数、作为 ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds) 的参数、作为 Object.Destroy(Object) 的参数 等共 6 处。</summary>
    private GameObject currentModel;
    /// <summary>中文含义：输入桥接。使用位置：ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.ReleasePresentationResources() 等共 4 处。</summary>
    private LeonInputBridge inputBridge;
    /// <summary>中文含义：过渡协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、ItemInspectionUI.HideRoutine()、ItemInspectionUI.RequestClose() 等共 5 处。</summary>
    private Coroutine transitionRoutine;
    /// <summary>中文含义：上一次鼠标位置。使用位置：ItemInspectionUI.Update()。</summary>
    private Vector3 lastMousePosition;
    /// <summary>中文含义：是否旋转。使用位置：ItemInspectionUI.Update()。</summary>
    private bool isRotation;
    /// <summary>中文含义：检视中心。使用位置：作为 Transform.RotateAround(Vector3, Vector3, float) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)。</summary>
    private Vector3 inspectionCenter;
    /// <summary>中文含义：接受关闭时间。使用位置：ItemInspectionUI.RequestClose()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private float acceptCloseTime;
    /// <summary>中文含义：是否模态已登记。使用位置：ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</summary>
    private bool modalRegistered;
    /// <summary>中文含义：是否可见。使用位置：ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.IsVisible.get 等共 6 处。</summary>
    private bool isVisible;
    /// <summary>中文含义：是否开口。使用位置：ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.IsVisible.get、ItemInspectionUI.RequestClose() 等共 5 处。</summary>
    private bool isOpening;
    /// <summary>中文含义：是否关闭中。使用位置：ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()、ItemInspectionUI.IsVisible.get 等共 5 处。</summary>
    private bool isClosing;
    /// <summary>实现基类的 Awake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ItemInspectionUI.BuildInterface()、Singleton&lt;ItemInspectionUI&gt;.Awake()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void Awake()
    {
        base.Awake();
        BuildInterface();
    }

    /// <summary>显示指定物品；缺少三维模型或界面正忙时返回失败。</summary>
    /// <remarks>静态调用链：上游 ItemPickup.Pickup()；下游（含内部回调体） ItemData.GetInspectionModelPrefab()、ItemInspectionUI.ReportMissingModel(ItemData)、ItemInspectionUI.ShowRoutine(ItemData, GameObject)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 ItemInspectionUI.ReportMissingModel(ItemData) 的参数、作为 ItemInspectionUI.ShowRoutine(ItemData, GameObject) 的参数、ItemInspectionUI.Show(ItemData)。</param>
    public bool Show(ItemData item)
    {
        if (VillageBellSequence.IsPresenting || item == null || IsVisible)
        {
            return false;
        }

        // modelPrefab（模型预制件）：保存 ItemData.GetInspectionModelPrefab() 的返回结果，供当前步骤使用。使用位置：作为 ItemInspectionUI.ShowRoutine(ItemData, GameObject) 的参数、ItemInspectionUI.Show(ItemData)。
GameObject modelPrefab = item.GetInspectionModelPrefab();
        if (modelPrefab == null)
        {
            ReportMissingModel(item);
            return false;
        }

        transitionRoutine = StartCoroutine(ShowRoutine(item, modelPrefab));
        return true;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetMouseButtonDown(int)、Input.GetMouseButtonUp(int)、ItemInspectionUI.RenderDisplayCameraWithoutCompilationPlaceholder()、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)、Transform.RotateAround(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isVisible || isOpening || isClosing || currentModel == null)
        {
            isRotation = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isRotation = RectTransformUtility.RectangleContainsScreenPoint(modelImage.rectTransform, Input.mousePosition);
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isRotation = false;
        }

        if (!isRotation)
        {
            return;
        }

        // delta（增量）：使用位置：作为 Transform.RotateAround(Vector3, Vector3, float) 的参数、ItemInspectionUI.Update()。
Vector3 delta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;
        if (delta.sqrMagnitude < .01f)
        {
            return;
        }

        currentModel.transform.RotateAround(inspectionCenter, Vector3.up, -delta.x * .35f);
        currentModel.transform.RotateAround(inspectionCenter, Vector3.right, delta.y * .35f);
        RenderDisplayCameraWithoutCompilationPlaceholder();
    }

    /// <summary>请求关闭检视界面。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()；下游（含内部回调体） ItemInspectionUI.HideRoutine()、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RequestClose()
    {
        if (!isVisible || isOpening || isClosing || Time.unscaledTime < acceptCloseTime)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(HideRoutine());
    }

    /// <summary>ShowRoutine 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、ItemInspectionUI.CaptureFrozenBackground()、ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.PrepareModel(ItemData, GameObject)、LeonInputBridge.BeginItemInspectionInput(Action)、Mathf.Clamp01(float) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.Show(ItemData)；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、ItemInspectionUI.CaptureFrozenBackground()、ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.PrepareModel(ItemData, GameObject)、LeonInputBridge.BeginItemInspectionInput(Action)、Mathf.Clamp01(float) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 ItemInspectionUI.PrepareModel(ItemData, GameObject) 的参数、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。</param>
    /// <param name="modelPrefab">中文含义：模型预制件。使用位置：作为 ItemInspectionUI.PrepareModel(ItemData, GameObject) 的参数。</param>
    IEnumerator ShowRoutine(ItemData item, GameObject modelPrefab)
    {
        isOpening = true;
        // 先暂停并隐藏实时战斗信息，随后抓取的冻结背景不会残留准星和状态栏。
        // uiManager（界面管理器）：使用位置：ItemInspectionUI.ShowRoutine(ItemData, GameObject)。
GameUIManager uiManager = GameUIManager.Instance;
        if (uiManager != null)
        {
            uiManager.RegisterModal(this, GameUIManager.State.ItemInspection, true, true, true);
            modalRegistered = true;
        }

        // 屏幕截图只能在当前帧完成渲染后读取，否则部分渲染管线会返回空纹理。
        yield return new WaitForEndOfFrame();
        CaptureFrozenBackground();
        if (!PrepareModel(item, modelPrefab))
        {
            CleanupImmediately();
            transitionRoutine = null;
            yield break;
        }

        itemNameText.text = item.displayName;
        inputBridge = FindFirstObjectByType<LeonInputBridge>();
        // closeKey（关闭键）：使用位置：ItemInspectionUI.ShowRoutine(ItemData, GameObject)。
string closeKey;
        if (inputBridge != null)
        {
            closeKey = inputBridge.BeginItemInspectionInput(RequestClose);
        }
        else
        {
            closeKey = "F";
        }

        closeHintText.text = closeKey + "　关闭　　拖动物品旋转";
        overlayRoot.SetActive(true);
        overlayGroup.alpha = 0f;
        itemGroup.alpha = 0f;
        itemContainer.localScale = Vector3.one * 0.94f;
        isVisible = true;
        acceptCloseTime = Time.unscaledTime + Mathf.Max(inputDebounce, enterDuration);
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。
float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数。
float t = Mathf.Clamp01(elapsed / enterDuration);
            // eased（缓动后的）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、ItemInspectionUI.ShowRoutine(ItemData, GameObject)。
float eased = 1f - Mathf.Pow(1f - t, 3f);
            overlayGroup.alpha = eased;
            itemGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 1f, t));
            itemContainer.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
            yield return null;
        }

        overlayGroup.alpha = 1f;
        itemGroup.alpha = 1f;
        itemContainer.localScale = Vector3.one;
        isOpening = false;
        transitionRoutine = null;
    }

    /// <summary>HideRoutine 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、ItemInspectionUI.ReleasePresentationResources()、LeonInputBridge.EndItemInspectionInput()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.SmoothStep(float, float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.RequestClose()；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、ItemInspectionUI.ReleasePresentationResources()、LeonInputBridge.EndItemInspectionInput()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.SmoothStep(float, float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator HideRoutine()
    {
        isClosing = true;
        // receiverValue2（判空前缓存的输入桥接）：从输入桥接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ItemInspectionUI.HideRoutine()。
global::LeonInputBridge receiverValue2 = inputBridge;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.EndItemInspectionInput();
        }

        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、ItemInspectionUI.HideRoutine()。
float elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数、ItemInspectionUI.HideRoutine()。
float t = Mathf.Clamp01(elapsed / exitDuration);
            // eased（缓动后的）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、ItemInspectionUI.HideRoutine()。
float eased = t * t;
            overlayGroup.alpha = 1f - eased;
            itemGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            itemContainer.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, eased);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        itemGroup.alpha = 0f;
        overlayRoot.SetActive(false);
        ReleasePresentationResources();
        isVisible = false;
        isClosing = false;
        transitionRoutine = null;
        if (modalRegistered && GameUIManager.HasInstance)
        {
            GameUIManager.Instance.UnregisterModal(this);
        }

        modalRegistered = false;
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Canvas&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasScaler&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.Awake()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Canvas&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasScaler&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildInterface()
    {
        if (overlayRoot != null)
        {
            return;
        }

        ResolveFont();
        // canvasObject（画布对象）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数、ItemInspectionUI.BuildInterface()。
GameObject canvasObject = new GameObject("物品检视画布", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        // canvas（画布）：保存 GameObject.GetComponent<Canvas>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.BuildInterface()。
Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        // scaler（缩放器）：保存 GameObject.GetComponent<CanvasScaler>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.BuildInterface()。
CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        overlayRoot = new GameObject("检视根节点", typeof(RectTransform), typeof(CanvasGroup));
        overlayRoot.transform.SetParent(canvasObject.transform, false);
        Stretch(overlayRoot.GetComponent<RectTransform>());
        overlayGroup = overlayRoot.GetComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;
        frozenBackground = CreateRawImage("冻结模糊背景", overlayRoot.transform);
        Stretch(frozenBackground.rectTransform);
        // shade（阴影色）：保存 ItemInspectionUI.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 ItemInspectionUI.Stretch(RectTransform) 的参数。
Image shade = CreateImage("全屏暗化", overlayRoot.transform, fullScreenShade);
        Stretch(shade.rectTransform);
        // gradient（渐变）：保存 ItemInspectionUI.CreateRawImage(string, Transform) 的返回结果，供当前步骤使用。使用位置：作为 ItemInspectionUI.Stretch(RectTransform) 的参数、ItemInspectionUI.BuildInterface()。
RawImage gradient = CreateRawImage("右侧羽化黑幕", overlayRoot.transform);
        Stretch(gradient.rectTransform);
        gradientTexture = CreateGradientTexture();
        gradient.texture = gradientTexture;
        gradient.color = Color.white;
        // itemObject（物品对象）：使用位置：作为 ItemInspectionUI.CreateRawImage(string, Transform) 的参数、作为 ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、ItemInspectionUI.BuildInterface()。
GameObject itemObject = new GameObject("物品陈列", typeof(RectTransform), typeof(CanvasGroup));
        itemObject.transform.SetParent(overlayRoot.transform, false);
        itemContainer = itemObject.GetComponent<RectTransform>();
        Stretch(itemContainer);
        itemGroup = itemObject.GetComponent<CanvasGroup>();
        itemGroup.interactable = false;
        itemGroup.blocksRaycasts = false;
        modelImage = CreateRawImage("三维物品", itemObject.transform);
        SetAnchors(modelImage.rectTransform, new Vector2(0.48f, 0.36f), new Vector2(0.985f, 0.86f));
        itemNameText = CreateText("物品名称", itemObject.transform, string.Empty, 29, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.86f));
        SetAnchors(itemNameText.rectTransform, new Vector2(0.62f, 0.25f), new Vector2(0.95f, 0.36f));
        closeHintText = CreateText("关闭提示", itemObject.transform, "F　关闭", 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.78f));
        SetAnchors(closeHintText.rectTransform, new Vector2(0.62f, 0.10f), new Vector2(0.95f, 0.20f));
        BuildDisplayStage();
        overlayRoot.SetActive(false);
    }

    /// <summary>ResolveFont 的业务处理入口；本方法直接依赖 Object.FindFirstObjectByType&lt;Text&gt;(FindObjectsInactive)、Resources.GetBuiltinResource&lt;Font&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） Object.FindFirstObjectByType&lt;Text&gt;(FindObjectsInactive)、Resources.GetBuiltinResource&lt;Font&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolveFont()
    {
        if (interfaceFont != null)
        {
            return;
        }

        // existingText（已有文字）：保存 Object.FindFirstObjectByType<Text>(FindObjectsInactive) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.ResolveFont()。
Text existingText = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
        if (existingText != null)
        {
            interfaceFont = existingText.font;
        }

        if (interfaceFont == null)
        {
            interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>BuildDisplayStage 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GameObject(string)、GameObject.GetComponent&lt;Camera&gt;()、ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)、Transform.SetParent(Transform, bool)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GameObject(string)、GameObject.GetComponent&lt;Camera&gt;()、ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)、Transform.SetParent(Transform, bool)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildDisplayStage()
    {
        // stageObject（阶段对象）：使用位置：ItemInspectionUI.BuildDisplayStage()。
GameObject stageObject = new GameObject("物品检视渲染舞台");
        stageObject.transform.SetParent(transform, false);
        stageObject.transform.position = new Vector3(0f, -10000f, 0f);
        stageRoot = stageObject.transform;
        // cameraObject（摄像机对象）：使用位置：ItemInspectionUI.BuildDisplayStage()。
GameObject cameraObject = new GameObject("物品检视相机", typeof(Camera));
        cameraObject.transform.SetParent(stageRoot, false);
        displayCamera = cameraObject.GetComponent<Camera>();
        displayCamera.enabled = false;
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        displayCamera.fieldOfView = displayFieldOfView;
        displayCamera.nearClipPlane = 0.01f;
        displayCamera.allowHDR = true;
        displayCamera.allowMSAA = true;
        CreateLocalLight("暖色主光", new Vector3(-2.4f, 2.2f, -3.2f), keyLightColor, keyLightIntensity);
        CreateLocalLight("冷色补光", new Vector3(2.8f, -0.8f, -2.4f), fillLightColor, fillLightIntensity);
    }

    /// <summary>CreateLocalLight 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Light&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildDisplayStage()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Light&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="localPosition">中文含义：局部位置。使用位置：ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)。</param>
    /// <param name="color">中文含义：颜色。使用位置：ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)。</param>
    /// <param name="intensity">中文含义：强度。使用位置：ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)。</param>
    void CreateLocalLight(string objectName, Vector3 localPosition, Color color, float intensity)
    {
        // lightObject（光源对象）：使用位置：ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)。
GameObject lightObject = new GameObject(objectName, typeof(Light));
        lightObject.transform.SetParent(stageRoot, false);
        lightObject.transform.localPosition = localPosition;
        // light（光源）：保存 GameObject.GetComponent<Light>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CreateLocalLight(string, Vector3, Color, float)。
Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 12f;
        light.shadows = LightShadows.None;
    }

    /// <summary>PrepareModel 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、GameObject.SetActive(bool)、ItemInspectionUI.DisableModelBehaviours(GameObject)、ItemInspectionUI.ReleaseModelResources()、ItemInspectionUI.RenderDisplayCameraWithoutCompilationPlaceholder()、ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)、Mathf.Clamp(float, float, float) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.ShowRoutine(ItemData, GameObject)；下游（含内部回调体） Debug.LogError(object, Object)、GameObject.SetActive(bool)、ItemInspectionUI.DisableModelBehaviours(GameObject)、ItemInspectionUI.ReleaseModelResources()、ItemInspectionUI.RenderDisplayCameraWithoutCompilationPlaceholder()、ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)、Mathf.Clamp(float, float, float) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Quaternion.Euler(Vector3) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)。</param>
    /// <param name="modelPrefab">中文含义：模型预制件。使用位置：作为 Debug.LogError(object, Object) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数。</param>
    bool PrepareModel(ItemData item, GameObject modelPrefab)
    {
        ReleaseModelResources();
        // width（宽度）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 RenderTexture.RenderTexture(int, int, int, RenderTextureFormat) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)。
int width = Mathf.Clamp(Mathf.RoundToInt(Screen.width * 0.5f), 512, 1920);
        // height（高度）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 RenderTexture.RenderTexture(int, int, int, RenderTextureFormat) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)。
int height = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.5f), 288, 1080);
        modelTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "物品检视渲染纹理",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 2
        };
        modelTexture.Create();
        modelImage.texture = modelTexture;
        displayCamera.targetTexture = modelTexture;
        displayCamera.aspect = width / (float)height;
        displayCamera.fieldOfView = displayFieldOfView;
        currentModel = Instantiate(modelPrefab, stageRoot);
        currentModel.name = "当前检视物品_" + item.itemId;
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.Euler(item.inspectionModelRotation);
        currentModel.transform.localScale = Vector3.one;
        currentModel.SetActive(true);
        DisableModelBehaviours(currentModel);
        if (!TryGetRendererBounds(currentModel, out Bounds /* 中文含义：包围盒。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bounds))
        {
            Debug.LogError("[物品检视] 模型没有可渲染网格：" + modelPrefab.name, modelPrefab);
            ReleaseModelResources();
            return false;
        }

        // 防止长枪或弹药模型沿镜头纵深摆放，只能看到枪口或弹壳端面。
        // visibleExtent（可见半尺寸）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.PrepareModel(ItemData, GameObject)。
float visibleExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
        if (bounds.extents.z > visibleExtent * 1.25f)
        {
            currentModel.transform.localRotation *= Quaternion.Euler(0f, 65f, 0f);
            if (!TryGetRendererBounds(currentModel, out bounds))
            {
                ReleaseModelResources();
                return false;
            }
        }

        // compositionOffset（构图偏移量）：使用位置：ItemInspectionUI.PrepareModel(ItemData, GameObject)。
Vector3 compositionOffset = item.inspectionModelOffset;
        currentModel.transform.position += stageRoot.position - bounds.center + compositionOffset;
        if (!TryGetRendererBounds(currentModel, out bounds))
        {
            ReleaseModelResources();
            return false;
        }

        // aspect（宽高比）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数。
float aspect = Mathf.Max(0.1f, displayCamera.aspect);
        inspectionCenter = bounds.center;
        // halfSize（一半尺寸）：使用位置：ItemInspectionUI.PrepareModel(ItemData, GameObject)。
float halfSize = bounds.extents.magnitude / Mathf.Min(1f, aspect);
        // fill（填充）：保存 Mathf.Clamp(float, float, float) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.PrepareModel(ItemData, GameObject)。
float fill = Mathf.Clamp(automaticFrameFill * Mathf.Clamp(item.inspectionViewScale, 0.5f, 1.5f), 0.35f, 0.92f);
        // angle（角度）：使用位置：作为 Mathf.Tan(float) 的参数。
float angle = Mathf.Max(1f, displayFieldOfView) * Mathf.Deg2Rad * 0.5f;
        // distance（距离）：使用位置：作为 Mathf.Max(float, float) 的参数、ItemInspectionUI.PrepareModel(ItemData, GameObject)。
float distance = bounds.extents.z + halfSize / (Mathf.Tan(angle) * fill);
        distance = Mathf.Max(0.25f, distance);
        displayCamera.transform.position = stageRoot.position + Vector3.back * distance;
        displayCamera.transform.rotation = Quaternion.identity;
        displayCamera.farClipPlane = distance + bounds.size.magnitude * 4f + compositionOffset.magnitude + 2f;
        RenderDisplayCameraWithoutCompilationPlaceholder();
        return true;
    }

    /// <summary>
    /// 编辑器异步编译着色器时会用青色占位材质绘制首帧。
    /// 检视界面只显式渲染一次，因此该占位帧会一直保留在渲染纹理中。
    /// 此处仅在这次离屏渲染期间等待编译完成，运行版本不受影响。
    /// </summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.PrepareModel(ItemData, GameObject)、ItemInspectionUI.Update()；下游（含内部回调体） Camera.Render()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RenderDisplayCameraWithoutCompilationPlaceholder()
    {
        if (displayCamera == null)
        {
            return;
        }

#if UNITY_EDITOR
        // previousAsyncCompilation（是否前一次异步编译）：使用位置：ItemInspectionUI.RenderDisplayCameraWithoutCompilationPlaceholder()。
bool previousAsyncCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
        try
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            displayCamera.Render();
        }
        finally
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
        }
#else
        displayCamera.Render();
#endif
    }

    /// <summary>DisableModelBehaviours 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;Behaviour&gt;(bool)、GameObject.GetComponentsInChildren&lt;Collider&gt;(bool)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Rigidbody&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.PrepareModel(ItemData, GameObject)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;Behaviour&gt;(bool)、GameObject.GetComponentsInChildren&lt;Collider&gt;(bool)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、GameObject.GetComponentsInChildren&lt;Rigidbody&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。</param>
    static void DisableModelBehaviours(GameObject model)
    {
        // behaviours（行为集合）：保存 GameObject.GetComponentsInChildren<Behaviour>(bool) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
Behaviour[] behaviours = model.GetComponentsInChildren<Behaviour>(true);
        // i（当前遍历索引）：使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }

        // colliders（碰撞体集合）：保存 GameObject.GetComponentsInChildren<Collider>(bool) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        // i（当前遍历索引）：使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        // bodies（刚体集合）：保存 GameObject.GetComponentsInChildren<Rigidbody>(bool) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
        // i（当前遍历索引）：使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }

        // renderers（渲染器集合）：保存 GameObject.GetComponentsInChildren<Renderer>(bool) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        // i（当前遍历索引）：使用位置：ItemInspectionUI.DisableModelBehaviours(GameObject)。
for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    /// <summary>TryGetRendererBounds 的业务处理入口；本方法直接依赖 Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.PrepareModel(ItemData, GameObject)；下游（含内部回调体） Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)。</param>
    /// <param name="bounds">中文含义：包围盒。输出参数，由本入口写入并交给调用方。使用位置：ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)。</param>
    static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
    {
        // renderers（渲染器集合）：保存 GameObject.GetComponentsInChildren<Renderer>(bool) 的返回结果，供当前步骤使用。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数、ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)。
Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        // found（是否已找到）：使用位置：ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)。
bool found = false;
        // i（当前遍历索引）：使用位置：ItemInspectionUI.TryGetRendererBounds(GameObject, out Bounds)。
for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return found && bounds.size.sqrMagnitude > 0.000001f;
    }

    /// <summary>CaptureFrozenBackground 的业务处理入口；本方法直接依赖 Camera.Render()、ItemInspectionUI.ReleaseFrozenBackground()、LayerMask.NameToLayer(string)、List&lt;Canvas&gt;.Add(Canvas)、List&lt;Canvas&gt;.List()、Mathf.Clamp(int, int, int)、Mathf.Max(int, int) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.ShowRoutine(ItemData, GameObject)；下游（含内部回调体） Camera.Render()、ItemInspectionUI.ReleaseFrozenBackground()、LayerMask.NameToLayer(string)、List&lt;Canvas&gt;.Add(Canvas)、List&lt;Canvas&gt;.List()、Mathf.Clamp(int, int, int)、Mathf.Max(int, int) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CaptureFrozenBackground()
    {
        ReleaseFrozenBackground();
        // sourceCamera（来源摄像机）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
Camera sourceCamera;
        if (Camera.main != null)
        {
            sourceCamera = Camera.main;
        }
        else
        {
            sourceCamera = FindFirstObjectByType<Camera>();
        }

        if (sourceCamera == null)
        {
            return;
        }

        // divisor（除数）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(int, int) 的参数。
int divisor = Mathf.Clamp(backgroundDownsample, 4, 16);
        // width（宽度）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Rect.Rect(float, float, float, float) 的参数、作为 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的参数、作为 Texture2D.Texture2D(int, int, TextureFormat, bool) 的参数。
int width = Mathf.Max(96, Screen.width / divisor);
        // height（高度）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Rect.Rect(float, float, float, float) 的参数、作为 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的参数、作为 Texture2D.Texture2D(int, int, TextureFormat, bool) 的参数。
int height = Mathf.Max(54, Screen.height / divisor);
        // temporary（临时）：保存 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的返回结果，供当前步骤使用。使用位置：作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数、ItemInspectionUI.CaptureFrozenBackground()。
RenderTexture temporary = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        temporary.filterMode = FilterMode.Bilinear;
        // previousTarget（前一次目标）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
RenderTexture previousTarget = sourceCamera.targetTexture;
        // previousActive（前一次生效）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
RenderTexture previousActive = RenderTexture.active;
        // previousCullingMask（前一次剔除遮罩）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
int previousCullingMask = sourceCamera.cullingMask;
        // hiddenCanvases（已隐藏画布集合）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
var hiddenCanvases = new List<Canvas>();
        // canvases（画布集合）：保存 Object.FindObjectsByType<Canvas>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CaptureFrozenBackground()。
Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // i（当前遍历索引）：使用位置：ItemInspectionUI.CaptureFrozenBackground()。
for (int i = 0; i < canvases.Length; i++)
        {
            // canvas（画布）：使用位置：作为 List<Canvas>.Add(Canvas) 的参数、ItemInspectionUI.CaptureFrozenBackground()。
Canvas canvas = canvases[i];
            if (canvas == null || !canvas.enabled || canvas.transform.IsChildOf(transform))
            {
                continue;
            }

            canvas.enabled = false;
            hiddenCanvases.Add(canvas);
        }

        try
        {
            // 单独渲染游戏摄像机，避免把屏幕空间战斗信息烘进冻结背景。
            // uiLayer（界面图层）：保存 LayerMask.NameToLayer(string) 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CaptureFrozenBackground()。
int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                sourceCamera.cullingMask &= ~(1 << uiLayer);
            }

            sourceCamera.targetTexture = temporary;
            sourceCamera.Render();
            RenderTexture.active = temporary;
            frozenTexture = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                name = "物品检视冻结背景",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            frozenTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            frozenTexture.Apply(false, false);
        }
        finally
        {
            sourceCamera.targetTexture = previousTarget;
            sourceCamera.cullingMask = previousCullingMask;
            RenderTexture.active = previousActive;
            // i（当前遍历索引）：使用位置：作为 List<Canvas>.this[int] 的参数、ItemInspectionUI.CaptureFrozenBackground()。
for (int i = 0; i < hiddenCanvases.Count; i++)
            {
                if (hiddenCanvases[i] != null)
                {
                    hiddenCanvases[i].enabled = true;
                }
            }
        }

        RenderTexture.ReleaseTemporary(temporary);
        frozenBackground.texture = frozenTexture;
        frozenBackground.color = Color.white;
    }

    /// <summary>CreateGradientTexture 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Mathf.InverseLerp(float, float, float)、Mathf.SmoothStep(float, float, float)、Texture2D.Apply(bool, bool)、Texture2D.SetPixel(int, int, Color)、Texture2D.Texture2D(int, int, TextureFormat, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、Mathf.InverseLerp(float, float, float)、Mathf.SmoothStep(float, float, float)、Texture2D.Apply(bool, bool)、Texture2D.SetPixel(int, int, Color)、Texture2D.Texture2D(int, int, TextureFormat, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    Texture2D CreateGradientTexture()
    {
        // width（宽度）：使用位置：作为 Texture2D.Texture2D(int, int, TextureFormat, bool) 的参数、ItemInspectionUI.CreateGradientTexture()。
const int width = 512;
        // texture（当前处理的纹理资源）：使用位置：ItemInspectionUI.CreateGradientTexture()。
Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "物品检视右侧羽化",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        // x（横向）：使用位置：作为 Texture2D.SetPixel(int, int, Color) 的参数、ItemInspectionUI.CreateGradientTexture()。
for (int x = 0; x < width; x++)
        {
            // u（横向纹理坐标）：使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数。
float u = x / (float)(width - 1);
            // alpha（透明度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Color.Color(float, float, float, float) 的参数。
float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.72f, u));
            texture.SetPixel(x, 0, new Color(0f, 0f, 0f, alpha));
        }

        texture.Apply(false, false);
        return texture;
    }

    /// <summary>ReportMissingModel 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、HashSet&lt;string&gt;.Add(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.Show(ItemData)；下游（含内部回调体） Debug.LogWarning(object, Object)、HashSet&lt;string&gt;.Add(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 string.IsNullOrEmpty(string) 的参数、ItemInspectionUI.ReportMissingModel(ItemData)。</param>
    void ReportMissingModel(ItemData item)
    {
        // key（键）：使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 HashSet<string>.Add(string) 的参数、ItemInspectionUI.ReportMissingModel(ItemData)。
string key;
        if (item != null && !string.IsNullOrEmpty(item.itemId))
        {
            key = item.itemId;
        }
        else
        {
            key = "未知物品";
        }

        if (!missingModelWarnings.Add(key))
        {
            return;
        }

        // 运行时不能使用错误级日志，否则编辑器开启“错误时暂停”后会中断游戏。
        Debug.LogWarning("[物品检视] 缺少三维检视模型，已跳过界面：" + key, item);
    }

    /// <summary>ReleasePresentationResources 的业务处理入口；本方法直接依赖 ItemInspectionUI.ReleaseFrozenBackground()、ItemInspectionUI.ReleaseModelResources()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()；下游（含内部回调体） ItemInspectionUI.ReleaseFrozenBackground()、ItemInspectionUI.ReleaseModelResources()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ReleasePresentationResources()
    {
        ReleaseFrozenBackground();
        ReleaseModelResources();
        inputBridge = null;
    }

    /// <summary>ReleaseFrozenBackground 的业务处理入口；本方法直接依赖 Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.CaptureFrozenBackground()、ItemInspectionUI.ReleasePresentationResources()；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ReleaseFrozenBackground()
    {
        if (frozenBackground != null)
        {
            frozenBackground.texture = null;
        }

        if (frozenTexture == null)
        {
            return;
        }

        Destroy(frozenTexture);
        frozenTexture = null;
    }

    /// <summary>ReleaseModelResources 的业务处理入口；本方法直接依赖 Object.Destroy(Object)、RenderTexture.Release()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.PrepareModel(ItemData, GameObject)、ItemInspectionUI.ReleasePresentationResources()；下游（含内部回调体） Object.Destroy(Object)、RenderTexture.Release()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ReleaseModelResources()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        currentModel = null;
        if (displayCamera != null)
        {
            displayCamera.targetTexture = null;
        }

        if (modelImage != null)
        {
            modelImage.texture = null;
        }

        if (modelTexture != null)
        {
            modelTexture.Release();
            Destroy(modelTexture);
        }

        modelTexture = null;
    }

    /// <summary>CleanupImmediately 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、ItemInspectionUI.ReleasePresentationResources()、LeonInputBridge.EndItemInspectionInput()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.OnDestroy()、ItemInspectionUI.ShowRoutine(ItemData, GameObject)；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、ItemInspectionUI.ReleasePresentationResources()、LeonInputBridge.EndItemInspectionInput()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CleanupImmediately()
    {
        // receiverValue5（判空前缓存的输入桥接）：从输入桥接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ItemInspectionUI.CleanupImmediately()。
global::LeonInputBridge receiverValue5 = inputBridge;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.EndItemInspectionInput();
        }

        if (modalRegistered && GameUIManager.HasInstance)
        {
            GameUIManager.Instance.UnregisterModal(this);
        }

        modalRegistered = false;
        ReleasePresentationResources();
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        isVisible = false;
        isOpening = false;
        isClosing = false;
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ItemInspectionUI.CleanupImmediately()、Object.Destroy(Object)、Singleton&lt;ItemInspectionUI&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        CleanupImmediately();
        if (gradientTexture != null)
        {
            Destroy(gradientTexture);
        }

        gradientTexture = null;
        base.OnDestroy();
    }

    /// <summary>CreateRawImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RawImage&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RawImage&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    static RawImage CreateRawImage(string objectName, Transform parent)
    {
        // gameObject（游戏对象）：使用位置：ItemInspectionUI.CreateRawImage(string, Transform)。
GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        gameObject.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<RawImage>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CreateRawImage(string, Transform)。
RawImage image = gameObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：ItemInspectionUI.CreateImage(string, Transform, Color)。</param>
    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        // gameObject（游戏对象）：使用位置：ItemInspectionUI.CreateImage(string, Transform, Color)。
GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CreateImage(string, Transform, Color)。
Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="content">中文含义：内容。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="fontSize">中文含义：字体尺寸。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="alignment">中文含义：对齐状态。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    Text CreateText(string objectName, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        // gameObject（游戏对象）：使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。
GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionUI.CreateText(string, Transform, string, int, TextAnchor, Color)。
Text text = gameObject.GetComponent<Text>();
        text.font = interfaceFont;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = fontSize;
        return text;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：ItemInspectionUI.Stretch(RectTransform)。</param>
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>SetAnchors 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.BuildInterface()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="min">中文含义：最小。使用位置：ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="max">中文含义：最大。使用位置：ItemInspectionUI.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
