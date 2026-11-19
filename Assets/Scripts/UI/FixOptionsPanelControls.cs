using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 修复OptionsPanel中的UI控件
/// 确保Slider、Toggle等控件有正确的子对象结构和尺寸
/// </summary>
public class FixOptionsPanelControls : MonoBehaviour
{
    /// <summary>中文含义：选项集合分页管理器。使用位置：作为 FixOptionsPanelControls.FixPanelLayout(GameObject) 的参数、作为 FixOptionsPanelControls.FixSlider(Slider, string) 的参数、作为 FixOptionsPanelControls.FixToggle(Toggle, string) 的参数 等共 7 处。</summary>
    [Header("面板引用")]
    public OptionsTabManager optionsTabManager;
    /// <summary>中文含义：是否修正事件起始。使用位置：FixOptionsPanelControls.Start()。</summary>
    [Header("修复设置")]
    public bool fixOnStart = true;
    /// <summary>中文含义：是否修正滑块集合。使用位置：FixOptionsPanelControls.FixAllControls()。</summary>
    public bool fixSliders = true;
    /// <summary>中文含义：是否修正开关集合。使用位置：FixOptionsPanelControls.FixAllControls()。</summary>
    public bool fixToggles = true;
    /// <summary>中文含义：是否修正布局。使用位置：FixOptionsPanelControls.FixAllControls()。</summary>
    public bool fixLayout = true;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） FixOptionsPanelControls.FixAllControls()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (fixOnStart)
        {
            FixAllControls();
        }
    }

    /// <summary>
    /// 修复所有控件
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixAllControlsContextMenu()、FixOptionsPanelControls.Start()；下游（含内部回调体） Debug.Log(object)、Debug.LogError(object)、FixOptionsPanelControls.FixLayout()、FixOptionsPanelControls.FixSliders()、FixOptionsPanelControls.FixToggles()、Object.FindFirstObjectByType&lt;OptionsTabManager&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void FixAllControls()
    {
        if (optionsTabManager == null)
        {
            optionsTabManager = FindFirstObjectByType<OptionsTabManager>();
        }

        if (optionsTabManager == null)
        {
            Debug.LogError("未找到OptionsTabManager！");
            return;
        }

        Debug.Log("开始修复OptionsPanel控件...");
        if (fixSliders)
        {
            FixSliders();
        }

        if (fixToggles)
        {
            FixToggles();
        }

        if (fixLayout)
        {
            FixLayout();
        }

        Debug.Log("OptionsPanel控件修复完成！");
    }

    /// <summary>
    /// 修复所有Slider控件
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixAllControls()；下游（含内部回调体） Debug.Log(object)、FixOptionsPanelControls.FixSlider(Slider, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FixSliders()
    {
        Debug.Log("修复Slider控件...");
        // 修复背景音乐滑块
        if (optionsTabManager.bgmSlider != null)
        {
            FixSlider(optionsTabManager.bgmSlider, "BGM音量");
        }

        // 修复音效滑块
        if (optionsTabManager.sfxSlider != null)
        {
            FixSlider(optionsTabManager.sfxSlider, "音效音量");
        }

        // 修复灵敏度滑块X
        if (optionsTabManager.sensitivitySliderX != null)
        {
            FixSlider(optionsTabManager.sensitivitySliderX, "灵敏度X");
        }

        // 修复灵敏度滑块Y
        if (optionsTabManager.sensitivitySliderY != null)
        {
            FixSlider(optionsTabManager.sensitivitySliderY, "灵敏度Y");
        }

        // 修复瞄准灵敏度滑块
        if (optionsTabManager.aimSensitivitySlider != null)
        {
            FixSlider(optionsTabManager.aimSensitivitySlider, "瞄准灵敏度");
        }
    }

    /// <summary>
    /// 修复单个Slider控件
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixSliders()；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、Debug.Log(object)、FixOptionsPanelControls.FixSliderChildObjects(Slider)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slider">中文含义：滑块。使用位置：作为 FixOptionsPanelControls.FixSliderChildObjects(Slider) 的参数、FixOptionsPanelControls.FixSlider(Slider, string)。</param>
    /// <param name="sliderName">中文含义：滑块名称。使用位置：作为 Debug.Log(object) 的参数。</param>
    private void FixSlider(Slider slider, string sliderName)
    {
        if (slider == null)
        {
            return;
        }

        Debug.Log($"修复Slider: {sliderName}");
        // 确保Slider可交互
        slider.interactable = true;
        // 修复RectTransform
        // sliderRect（滑块矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSlider(Slider, string)。
RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect != null)
        {
            // 设置合理的尺寸
            sliderRect.sizeDelta = new Vector2(200, 20);
            // 确保锚点正确
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 修复子对象结构
        FixSliderChildObjects(slider);
    }

    /// <summary>
    /// 修复Slider的子对象结构
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixSlider(Slider, string)；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;Image&gt;()、GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.GameObject(string)、Transform.Find(string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slider">中文含义：滑块。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。</param>
    private void FixSliderChildObjects(Slider slider)
    {
        // sliderRect（滑块矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect == null)
        {
            return;
        }

        // 确保有Background子对象
        // backgroundTransform（背景变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Transform backgroundTransform = sliderRect.Find("Background");
        if (backgroundTransform == null)
        {
            // background（背景）：使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
GameObject background = new GameObject("Background");
            background.transform.SetParent(sliderRect, false);
            backgroundTransform = background.transform;
        }

        // backgroundRect（背景矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform backgroundRect = backgroundTransform.GetComponent<RectTransform>();
        if (backgroundRect == null)
        {
            backgroundRect = backgroundTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Background
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.sizeDelta = Vector2.zero;
        backgroundRect.anchoredPosition = Vector2.zero;
        // backgroundImage（背景图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Image backgroundImage = backgroundTransform.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = backgroundTransform.gameObject.AddComponent<Image>();
        }

        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        // 确保有Fill Area子对象
        // fillAreaTransform（填充区域变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Transform fillAreaTransform = sliderRect.Find("Fill Area");
        if (fillAreaTransform == null)
        {
            // fillArea（填充区域）：使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderRect, false);
            fillAreaTransform = fillArea.transform;
        }

        // fillAreaRect（填充区域矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform fillAreaRect = fillAreaTransform.GetComponent<RectTransform>();
        if (fillAreaRect == null)
        {
            fillAreaRect = fillAreaTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Fill Area
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.sizeDelta = Vector2.zero;
        fillAreaRect.anchoredPosition = Vector2.zero;
        // 确保Fill Area有Fill子对象
        // fillTransform（填充变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Transform fillTransform = fillAreaTransform.Find("Fill");
        if (fillTransform == null)
        {
            // fill（填充）：使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillAreaTransform, false);
            fillTransform = fill.transform;
        }

        // fillRect（填充矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform fillRect = fillTransform.GetComponent<RectTransform>();
        if (fillRect == null)
        {
            fillRect = fillTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Fill
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        // fillImage（填充图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Image fillImage = fillTransform.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fillTransform.gameObject.AddComponent<Image>();
        }

        fillImage.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        // 设置Slider的fillRect引用
        slider.fillRect = fillRect;
        // 确保有Handle Slide Area子对象
        // handleAreaTransform（句柄区域变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Transform handleAreaTransform = sliderRect.Find("Handle Slide Area");
        if (handleAreaTransform == null)
        {
            // handleArea（句柄区域）：使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderRect, false);
            handleAreaTransform = handleArea.transform;
        }

        // handleAreaRect（句柄区域矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform handleAreaRect = handleAreaTransform.GetComponent<RectTransform>();
        if (handleAreaRect == null)
        {
            handleAreaRect = handleAreaTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Handle Slide Area
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;
        handleAreaRect.anchoredPosition = Vector2.zero;
        // 确保Handle Slide Area有Handle子对象
        // handleTransform（句柄变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Transform handleTransform = handleAreaTransform.Find("Handle");
        if (handleTransform == null)
        {
            // handle（句柄）：使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleAreaTransform, false);
            handleTransform = handle.transform;
        }

        // handleRect（句柄矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
RectTransform handleRect = handleTransform.GetComponent<RectTransform>();
        if (handleRect == null)
        {
            handleRect = handleTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Handle
        handleRect.anchorMin = new Vector2(0, 0.5f);
        handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.sizeDelta = new Vector2(20, 0);
        handleRect.anchoredPosition = Vector2.zero;
        // handleImage（句柄图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixSliderChildObjects(Slider)。
Image handleImage = handleTransform.GetComponent<Image>();
        if (handleImage == null)
        {
            handleImage = handleTransform.gameObject.AddComponent<Image>();
        }

        handleImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        // 设置Slider的handleRect引用
        slider.handleRect = handleRect;
    }

    /// <summary>
    /// 修复所有Toggle控件
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixAllControls()；下游（含内部回调体） Debug.Log(object)、FixOptionsPanelControls.FixToggle(Toggle, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FixToggles()
    {
        Debug.Log("修复Toggle控件...");
        // 修复全屏Toggle
        if (optionsTabManager.fullscreenToggle != null)
        {
            FixToggle(optionsTabManager.fullscreenToggle, "全屏");
        }

        // 修复反转Y轴Toggle
        if (optionsTabManager.invertYToggle != null)
        {
            FixToggle(optionsTabManager.invertYToggle, "反转Y轴");
        }
    }

    /// <summary>
    /// 修复单个Toggle控件
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixToggles()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;RectTransform&gt;()、Debug.Log(object)、GameObject.AddComponent&lt;Image&gt;()、GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.GameObject(string) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="toggle">中文含义：切换。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、FixOptionsPanelControls.FixToggle(Toggle, string)。</param>
    /// <param name="toggleName">中文含义：切换名称。使用位置：作为 Debug.Log(object) 的参数。</param>
    private void FixToggle(Toggle toggle, string toggleName)
    {
        if (toggle == null)
        {
            return;
        }

        Debug.Log($"修复Toggle: {toggleName}");
        // 确保Toggle可交互
        toggle.interactable = true;
        // 修复RectTransform
        // toggleRect（切换矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
RectTransform toggleRect = toggle.GetComponent<RectTransform>();
        if (toggleRect != null)
        {
            // 设置合理的尺寸
            toggleRect.sizeDelta = new Vector2(200, 30);
            // 确保锚点正确
            toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 修复Toggle的背景
        // toggleBackground（切换背景）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
Image toggleBackground = toggle.GetComponent<Image>();
        if (toggleBackground == null)
        {
            toggleBackground = toggle.gameObject.AddComponent<Image>();
        }

        toggleBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        // 确保有Checkmark子对象
        // checkmarkTransform（勾选标记变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
Transform checkmarkTransform = toggle.transform.Find("Checkmark");
        if (checkmarkTransform == null)
        {
            // checkmark（勾选标记）：使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggle.transform, false);
            checkmarkTransform = checkmark.transform;
        }

        // checkmarkRect（勾选标记矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
RectTransform checkmarkRect = checkmarkTransform.GetComponent<RectTransform>();
        if (checkmarkRect == null)
        {
            checkmarkRect = checkmarkTransform.gameObject.AddComponent<RectTransform>();
        }

        // 设置Checkmark
        checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkmarkRect.sizeDelta = Vector2.zero;
        checkmarkRect.anchoredPosition = Vector2.zero;
        // checkmarkImage（勾选标记图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixToggle(Toggle, string)。
Image checkmarkImage = checkmarkTransform.GetComponent<Image>();
        if (checkmarkImage == null)
        {
            checkmarkImage = checkmarkTransform.gameObject.AddComponent<Image>();
        }

        checkmarkImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        // 设置Toggle的checkmark引用
        toggle.graphic = checkmarkImage;
    }

    /// <summary>
    /// 修复布局
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixAllControls()；下游（含内部回调体） Debug.Log(object)、FixOptionsPanelControls.FixPanelLayout(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FixLayout()
    {
        Debug.Log("修复布局...");
        // 修复音频面板布局
        if (optionsTabManager.panelAudio != null)
        {
            FixPanelLayout(optionsTabManager.panelAudio);
        }

        // 修复显示面板布局
        if (optionsTabManager.panelDisplay != null)
        {
            FixPanelLayout(optionsTabManager.panelDisplay);
        }

        // 修复游戏面板布局
        if (optionsTabManager.panelGameplay != null)
        {
            FixPanelLayout(optionsTabManager.panelGameplay);
        }
    }

    /// <summary>
    /// 修复面板布局
    /// </summary>
    /// <remarks>静态调用链：上游 FixOptionsPanelControls.FixLayout()；下游（含内部回调体） GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;VerticalLayoutGroup&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;VerticalLayoutGroup&gt;()、RectOffset.RectOffset(int, int, int, int)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="panel">中文含义：面板。使用位置：FixOptionsPanelControls.FixPanelLayout(GameObject)。</param>
    private void FixPanelLayout(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        // 确保有RectTransform
        // panelRect（面板矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixPanelLayout(GameObject)。
RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            panelRect = panel.AddComponent<RectTransform>();
        }

        // 设置面板尺寸
        panelRect.sizeDelta = new Vector2(800, 600);
        // 添加垂直布局组
        // layoutGroup（布局分组）：保存 GameObject.GetComponent<VerticalLayoutGroup>() 的返回结果，供当前步骤使用。使用位置：FixOptionsPanelControls.FixPanelLayout(GameObject)。
VerticalLayoutGroup layoutGroup = panel.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = panel.AddComponent<VerticalLayoutGroup>();
        }

        // 设置布局参数
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 20;
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
    }

    /// <summary>
    /// 在编辑器中测试修复
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） FixOptionsPanelControls.FixAllControls()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("修复所有控件")]
    public void FixAllControlsContextMenu()
    {
        FixAllControls();
    }
}
