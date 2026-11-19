using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RE4 风格分层多级主菜单系统
/// 支持一级菜单 + 二级子面板，带滑动和淡入淡出过渡
/// </summary>
public class MultiLevelMenuManager : MonoBehaviour
{
    /// <summary>中文含义：主要菜单容器。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。</summary>
    [Header("一级菜单")]
    public Transform primaryMenuContainer;
    /// <summary>中文含义：主要菜单物品预制件。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。</summary>
    public GameObject primaryMenuItemPrefab;
    /// <summary>中文含义：主要选定颜色。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)、MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</summary>
    public Color primarySelectedColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>中文含义：主要普通状态颜色。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)、MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</summary>
    public Color primaryNormalColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>中文含义：主要禁用状态颜色。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)、MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</summary>
    public Color primaryDisabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    /// <summary>中文含义：次要面板容器。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、MultiLevelMenuManager.InitializeMenu()、MultiLevelMenuManager.ShowPrimaryMenu()。</summary>
    [Header("二级子面板")]
    public Transform secondaryPanelContainer;
    /// <summary>中文含义：次要面板预制件。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform) 的参数。</summary>
    public GameObject secondaryPanelPrefab;
    /// <summary>中文含义：次要滑移偏移量。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Vector2(float, float) 的参数。</summary>
    public float secondarySlideOffset = 800f;
    /// <summary>中文含义：次要滑移持续时间。使用位置：MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)、MultiLevelMenuManager.SlideOutSecondaryPanels()。</summary>
    public float secondarySlideDuration = 0.3f;
    /// <summary>中文含义：遮暗层图像。使用位置：MultiLevelMenuManager.FadeDimmer(bool)、MultiLevelMenuManager.InitializeMenu()。</summary>
    [Header("背景变暗")]
    public Image dimmerImage;
    /// <summary>中文含义：遮暗层淡入淡出时长。使用位置：MultiLevelMenuManager.FadeDimmer(bool)。</summary>
    public float dimmerFadeDuration = 0.25f;
    /// <summary>中文含义：遮暗层透明度。使用位置：MultiLevelMenuManager.FadeDimmer(bool)。</summary>
    public float dimmerAlpha = 0.6f;
    /// <summary>中文含义：主要菜单分组。使用位置：MultiLevelMenuManager.FadeInPrimaryMenu()、MultiLevelMenuManager.InitializeMenu()。</summary>
    [Header("菜单层级")]
    public CanvasGroup primaryMenuGroup;
    /// <summary>中文含义：次要面板分组。使用位置：MultiLevelMenuManager.InitializeMenu()。</summary>
    public CanvasGroup secondaryPanelGroup;
    /// <summary>中文含义：主要菜单物品列表。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)、MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)、MultiLevelMenuManager.ClearAllMenuItems() 等共 11 处。</summary>
    [Header("菜单项定义")]
    public List<PrimaryMenuItem> primaryMenuItems = new List<PrimaryMenuItem>();
    /// <summary>中文含义：悬停声音。使用位置：作为 MultiLevelMenuManager.PlaySound(string) 的参数。</summary>
    [Header("声音效果")]
    public string hoverSound = "ui_hover";
    /// <summary>中文含义：确认声音。使用位置：作为 MultiLevelMenuManager.PlaySound(string) 的参数。</summary>
    public string confirmSound = "ui_confirm";
    /// <summary>中文含义：取消声音。使用位置：作为 MultiLevelMenuManager.PlaySound(string) 的参数。</summary>
    public string cancelSound = "ui_cancel";
    // 私有状态
    /// <summary>中文含义：当前主要索引。使用位置：作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、作为 MultiLevelMenuManager.OnPrimaryMenuItemClicked(int) 的参数、作为 MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int) 的参数 等共 10 处。</summary>
    private int currentPrimaryIndex = -1;
    /// <summary>中文含义：当前次要索引。使用位置：作为 MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int) 的参数、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.HandleSecondaryKeyboardNavigation() 等共 7 处。</summary>
    private int currentSecondaryIndex = -1;
    /// <summary>中文含义：是否次要面板打开。使用位置：MultiLevelMenuManager.CloseSecondaryPanel()、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.OpenSecondaryPanel(int) 等共 5 处。</summary>
    private bool isSecondaryPanelOpen = false;
    /// <summary>中文含义：是否动画播放。使用位置：MultiLevelMenuManager.CloseSecondaryPanel()、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.OnPrimaryMenuItemClicked(int) 等共 7 处。</summary>
    private bool isAnimating = false;
    /// <summary>中文含义：实例化的主要物品列表。使用位置：MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)、MultiLevelMenuManager.UpdatePrimaryMenuSelection()。</summary>
    private List<GameObject> instantiatedPrimaryItems = new List<GameObject>();
    /// <summary>中文含义：实例化的次要面板集合。使用位置：MultiLevelMenuManager.ClearSecondaryPanels()、MultiLevelMenuManager.OpenSecondaryPanel(int)、MultiLevelMenuManager.SlideOutSecondaryPanels() 等共 5 处。</summary>
    private List<GameObject> instantiatedSecondaryPanels = new List<GameObject>();
    /// <summary>中文含义：当前主要菜单物品。使用位置：MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)、MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</summary>
    private PrimaryMenuItem currentPrimaryMenuItem;
    // 事件
    /// <summary>中文含义：主要菜单选定时的通知回调。事件通信成员 OnPrimaryMenuSelected，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)。</summary>
    public event Action<int> OnPrimaryMenuSelected;
    /// <summary>中文含义：次要菜单选定时的通知回调。事件通信成员 OnSecondaryMenuSelected，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)。</summary>
    public event Action<int, int> OnSecondaryMenuSelected;
    /// <summary>中文含义：菜单已关闭时的通知回调。事件通信成员 OnMenuClosed，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：MultiLevelMenuManager.CloseSecondaryPanelCoroutine()。</summary>
    public event Action OnMenuClosed;
    /// <summary>
    /// 一级菜单项数据结构
    /// </summary>
    [Serializable]
    public class PrimaryMenuItem
    {
        /// <summary>中文含义：显示名称。使用位置：作为 GameObject.GameObject(string) 的参数、MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)、MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int) 等共 4 处。</summary>
        public string displayName;
        /// <summary>中文含义：图标。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)、MultiLevelMenuManager.GeneratePrimaryMenuItems()。</summary>
        public Sprite icon;
        /// <summary>中文含义：是否启用。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)、MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.NavigatePrimaryMenu(int) 等共 6 处。</summary>
        public bool isEnabled = true;
        /// <summary>中文含义：次要物品列表。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)、MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)、MultiLevelMenuManager.NavigateSecondaryMenu(int) 等共 6 处。</summary>
        public List<SecondaryMenuItem> secondaryItems = new List<SecondaryMenuItem>();
        /// <summary>中文含义：选定时的通知回调。</summary>
        public Action<int> onSelected;
    }

    /// <summary>
    /// 二级菜单项数据结构
    /// </summary>
    [Serializable]
    public class SecondaryMenuItem
    {
        /// <summary>中文含义：显示名称。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)、MultiLevelMenuManager.OpenSecondaryPanel(int)。</summary>
        public string displayName;
        /// <summary>中文含义：图标。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)、MultiLevelMenuManager.OpenSecondaryPanel(int)。</summary>
        public Sprite icon;
        /// <summary>中文含义：是否启用。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)、MultiLevelMenuManager.NavigateSecondaryMenu(int)、MultiLevelMenuManager.OpenSecondaryPanel(int) 等共 5 处。</summary>
        public bool isEnabled = true;
        /// <summary>中文含义：自定义面板。</summary>
        public GameObject customPanel; // 可选的自定义面板
        /// <summary>中文含义：选定时的通知回调。使用位置：MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)。</summary>
        public Action<int, int> onSelected;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MultiLevelMenuManager.InitializeMenu()、MultiLevelMenuManager.SetupEventListeners()、MultiLevelMenuManager.ShowPrimaryMenu()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        InitializeMenu();
        SetupEventListeners();
        ShowPrimaryMenu();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、MultiLevelMenuManager.CloseSecondaryPanel()、MultiLevelMenuManager.HandlePrimaryKeyboardNavigation()、MultiLevelMenuManager.HandleSecondaryKeyboardNavigation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (isAnimating)
        {
            return;
        }

        // 键盘导航
        if (isSecondaryPanelOpen)
        {
            HandleSecondaryKeyboardNavigation();
        }
        else
        {
            HandlePrimaryKeyboardNavigation();
        }

        // ESC 返回
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSecondaryPanelOpen)
            {
                CloseSecondaryPanel();
            }
        }
    }

    /// <summary>
    /// 初始化菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、MultiLevelMenuManager.GeneratePrimaryMenuItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializeMenu()
    {
        // 确保组件存在
        if (primaryMenuGroup == null)
        {
            primaryMenuGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (secondaryPanelGroup == null && secondaryPanelContainer != null)
        {
            secondaryPanelGroup = secondaryPanelContainer.GetComponent<CanvasGroup>();
            if (secondaryPanelGroup == null)
            {
                secondaryPanelGroup = secondaryPanelContainer.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 设置背景变暗
        if (dimmerImage != null)
        {
            dimmerImage.color = new Color(0, 0, 0, 0);
            dimmerImage.raycastTarget = false;
        }

        // 生成一级菜单项
        GeneratePrimaryMenuItems();
    }

    /// <summary>
    /// 生成一级菜单项
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)、MultiLevelMenuManager.ClearAllMenuItems()、MultiLevelMenuManager.InitializeMenu()、MultiLevelMenuManager.SetMenuItemEnabled(int, int, bool)；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponentInChildren&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Clear()、MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)、MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void GeneratePrimaryMenuItems()
    {
        // 清除现有项
        // item（当前游戏对象）：每轮从实例化的主要物品列表取得一个元素，仅代表当前这一项。使用位置：作为 Object.Destroy(Object) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。
foreach (var item in instantiatedPrimaryItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        instantiatedPrimaryItems.Clear();
        if (primaryMenuContainer == null || primaryMenuItemPrefab == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<MultiLevelMenuManager.PrimaryMenuItem>.this[int] 的参数、作为 MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。
for (int i = 0; i < primaryMenuItems.Count; i++)
        {
            // menuItem（菜单物品）：使用位置：MultiLevelMenuManager.GeneratePrimaryMenuItems()。
var menuItem = primaryMenuItems[i];
            // itemObj（物品对象）：保存 Object.Instantiate<GameObject>(GameObject, Transform) 的返回结果，供当前步骤使用。使用位置：作为 List<GameObject>.Add(GameObject) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。
GameObject itemObj = Instantiate(primaryMenuItemPrefab, primaryMenuContainer);
            itemObj.name = $"PrimaryItem_{i}_{menuItem.displayName}";
            // 设置显示文本
            // textComponent（文字组件）：保存 GameObject.GetComponentInChildren<Text>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.GeneratePrimaryMenuItems()。
Text textComponent = itemObj.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = menuItem.displayName;
            }

            // 设置图标
            // receiverValue1（判空前缓存的查找的返回结果）：从查找的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.GeneratePrimaryMenuItems()。
global::UnityEngine.Transform receiverValue1 = itemObj.transform.Find("Icon");
            // iconComponent（图标组件）：使用位置：MultiLevelMenuManager.GeneratePrimaryMenuItems()。
Image iconComponent;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                iconComponent = receiverValue1.GetComponent<Image>();
            }
            else
            {
                iconComponent = default;
            }

            if (iconComponent != null && menuItem.icon != null)
            {
                iconComponent.sprite = menuItem.icon;
            }

            // 设置按钮交互
            // button（按钮）：保存 GameObject.GetComponent<Button>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.GeneratePrimaryMenuItems()。
Button button = itemObj.GetComponent<Button>();
            if (button != null)
            {
                // index（当前元素索引）：使用位置：作为 MultiLevelMenuManager.OnPrimaryMenuItemClicked(int) 的参数。
int index = i;
                button.onClick.AddListener(() => OnPrimaryMenuItemClicked(index));
                button.interactable = menuItem.isEnabled;
            }

            // 设置颜色
            UpdatePrimaryItemColor(i, false);
            instantiatedPrimaryItems.Add(itemObj);
        }
    }

    /// <summary>
    /// 设置事件监听器
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SetupEventListeners()
    {
    // 可以在这里添加更多事件监听
    }

    /// <summary>
    /// 显示一级菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Start()；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MultiLevelMenuManager.FadeInPrimaryMenu()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowPrimaryMenu()
    {
        currentPrimaryIndex = -1;
        currentSecondaryIndex = -1;
        isSecondaryPanelOpen = false;
        // 淡入一级菜单
        StartCoroutine(FadeInPrimaryMenu());
        // 隐藏二级面板
        if (secondaryPanelContainer != null)
        {
            secondaryPanelContainer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 一级菜单项点击事件
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.HandlePrimaryKeyboardNavigation()；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、MultiLevelMenuManager.OpenSecondaryPanel(int)、MultiLevelMenuManager.PlaySound(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 Action&lt;int&gt;.Invoke(int) 的参数、作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、作为 MultiLevelMenuManager.OpenSecondaryPanel(int) 的参数 等共 4 处。</param>
    private void OnPrimaryMenuItemClicked(int index)
    {
        if (isAnimating)
        {
            return;
        }

        if (index < 0 || index >= primaryMenuItems.Count)
        {
            return;
        }

        if (!primaryMenuItems[index].isEnabled)
        {
            return;
        }

        currentPrimaryIndex = index;
        currentPrimaryMenuItem = primaryMenuItems[index];
        PlaySound(confirmSound);
        // receiverValue3（判空前缓存的主要菜单选定时的通知回调）：从主要菜单选定时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)。
global::System.Action<int> receiverValue3 = OnPrimaryMenuSelected;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Invoke(index);
        }

        // 打开对应的二级面板
        OpenSecondaryPanel(index);
    }

    /// <summary>
    /// 打开二级面板
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponentInChildren&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject) 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="primaryIndex">中文含义：主要索引。使用位置：作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。</param>
    private void OpenSecondaryPanel(int primaryIndex)
    {
        if (isAnimating)
        {
            return;
        }

        isSecondaryPanelOpen = true;
        // 清除现有二级面板
        ClearSecondaryPanels();
        // 生成新的二级面板
        if (primaryIndex >= 0 && primaryIndex < primaryMenuItems.Count)
        {
            // primaryItem（主要物品）：使用位置：作为 GameObject.GameObject(string) 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。
var primaryItem = primaryMenuItems[primaryIndex];
            // 创建二级面板容器
            // panelContainer（面板容器）：使用位置：MultiLevelMenuManager.OpenSecondaryPanel(int)。
GameObject panelContainer = new GameObject($"SecondaryPanel_{primaryItem.displayName}");
            // containerRect（容器矩形）：保存 GameObject.AddComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup) 的参数、作为 Object.Instantiate<GameObject>(GameObject, Transform) 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。
RectTransform containerRect = panelContainer.AddComponent<RectTransform>();
            containerRect.SetParent(secondaryPanelContainer, false);
            // 设置容器位置（从右侧滑入）
            containerRect.anchorMin = new Vector2(1, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(1, 0.5f);
            containerRect.sizeDelta = new Vector2(secondarySlideOffset, 0);
            containerRect.anchoredPosition = new Vector2(secondarySlideOffset, 0);
            // 添加CanvasGroup用于动画
            // containerGroup（容器分组）：保存 GameObject.AddComponent<CanvasGroup>() 的返回结果，供当前步骤使用。使用位置：作为 MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup) 的参数。
CanvasGroup containerGroup = panelContainer.AddComponent<CanvasGroup>();
            // 生成二级菜单项
            // i（当前遍历索引）：使用位置：作为 List<MultiLevelMenuManager.SecondaryMenuItem>.this[int] 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。
for (int i = 0; i < primaryItem.secondaryItems.Count; i++)
            {
                // secondaryItem（次要物品）：使用位置：MultiLevelMenuManager.OpenSecondaryPanel(int)。
var secondaryItem = primaryItem.secondaryItems[i];
                // itemObj（物品对象）：保存 Object.Instantiate<GameObject>(GameObject, Transform) 的返回结果，供当前步骤使用。使用位置：作为 List<GameObject>.Add(GameObject) 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。
GameObject itemObj = Instantiate(secondaryPanelPrefab, containerRect);
                itemObj.name = $"SecondaryItem_{i}_{secondaryItem.displayName}";
                // 设置显示文本
                // textComponent（文字组件）：保存 GameObject.GetComponentInChildren<Text>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.OpenSecondaryPanel(int)。
Text textComponent = itemObj.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = secondaryItem.displayName;
                }

                // 设置图标
                // receiverValue4（判空前缓存的查找的返回结果）：从查找的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.OpenSecondaryPanel(int)。
global::UnityEngine.Transform receiverValue4 = itemObj.transform.Find("Icon");
                // iconComponent（图标组件）：使用位置：MultiLevelMenuManager.OpenSecondaryPanel(int)。
Image iconComponent;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    iconComponent = receiverValue4.GetComponent<Image>();
                }
                else
                {
                    iconComponent = default;
                }

                if (iconComponent != null && secondaryItem.icon != null)
                {
                    iconComponent.sprite = secondaryItem.icon;
                }

                // 设置按钮交互
                // button（按钮）：保存 GameObject.GetComponent<Button>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.OpenSecondaryPanel(int)。
Button button = itemObj.GetComponent<Button>();
                if (button != null)
                {
                    // primaryIdx（主要索引）：使用位置：作为 MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int) 的参数。
int primaryIdx = primaryIndex;
                    // secondaryIdx（次要索引）：使用位置：作为 MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int) 的参数。
int secondaryIdx = i;
                    button.onClick.AddListener(() => OnSecondaryMenuItemClicked(primaryIdx, secondaryIdx));
                    button.interactable = secondaryItem.isEnabled;
                }

                instantiatedSecondaryPanels.Add(itemObj);
            }

            // 播放滑入动画
            StartCoroutine(SlideInSecondaryPanel(containerRect, containerGroup));
        }

        // 背景变暗
        StartCoroutine(FadeDimmer(true));
    }

    /// <summary>
    /// 二级菜单项点击事件
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.HandleSecondaryKeyboardNavigation()、MultiLevelMenuManager.OpenSecondaryPanel(int)；下游（含内部回调体） Action&lt;int, int&gt;.Invoke(int, int)、MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)、MultiLevelMenuManager.PlaySound(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="primaryIndex">中文含义：主要索引。使用位置：作为 Action&lt;int, int&gt;.Invoke(int, int) 的参数、作为 MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int) 的参数。</param>
    /// <param name="secondaryIndex">中文含义：次要索引。使用位置：作为 Action&lt;int, int&gt;.Invoke(int, int) 的参数、作为 MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int) 的参数、MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)。</param>
    private void OnSecondaryMenuItemClicked(int primaryIndex, int secondaryIndex)
    {
        if (isAnimating)
        {
            return;
        }

        currentSecondaryIndex = secondaryIndex;
        PlaySound(confirmSound);
        // receiverValue6（判空前缓存的次要菜单选定时的通知回调）：从次要菜单选定时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)。
global::System.Action<int, int> receiverValue6 = OnSecondaryMenuSelected;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Invoke(primaryIndex, secondaryIndex);
        }

        // 这里可以添加具体的菜单项逻辑
        ExecuteSecondaryMenuAction(primaryIndex, secondaryIndex);
    }

    /// <summary>
    /// 执行二级菜单动作
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)；下游（含内部回调体） Action&lt;int, int&gt;.Invoke(int, int)、MultiLevelMenuManager.HandleExitGame()、MultiLevelMenuManager.HandleSettingsSecondaryMenu(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="primaryIndex">中文含义：主要索引。使用位置：作为 Action&lt;int, int&gt;.Invoke(int, int) 的参数、作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)。</param>
    /// <param name="secondaryIndex">中文含义：次要索引。使用位置：作为 Action&lt;int, int&gt;.Invoke(int, int) 的参数、作为 List&lt;MultiLevelMenuManager.SecondaryMenuItem&gt;.this[int] 的参数、作为 MultiLevelMenuManager.HandleSettingsSecondaryMenu(int) 的参数 等共 4 处。</param>
    private void ExecuteSecondaryMenuAction(int primaryIndex, int secondaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // primaryItem（主要物品）：使用位置：MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)。
var primaryItem = primaryMenuItems[primaryIndex];
        if (secondaryIndex < 0 || secondaryIndex >= primaryItem.secondaryItems.Count)
        {
            return;
        }

        // secondaryItem（次要物品）：使用位置：MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)。
var secondaryItem = primaryItem.secondaryItems[secondaryIndex];
        // 执行自定义动作
        // receiverValue7（判空前缓存的次要物品的选定时的通知回调）：从次要物品的选定时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)。
global::System.Action<int, int> receiverValue7 = // 执行自定义动作
 secondaryItem.onSelected;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.Invoke(primaryIndex, secondaryIndex);
        }

        // 这里可以根据菜单项名称执行特定逻辑
        switch (primaryItem.displayName)
        {
            case "设置选项":
                HandleSettingsSecondaryMenu(secondaryIndex);
                break;
            case "退出游戏":
                HandleExitGame();
                break;
        // 其他菜单项处理...
        }
    }

    /// <summary>
    /// 处理设置选项的二级菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="secondaryIndex">中文含义：次要索引。使用位置：作为 Debug.Log(object) 的参数。</param>
    private void HandleSettingsSecondaryMenu(int secondaryIndex)
    {
        // 这里可以打开具体的设置面板
        Debug.Log($"打开设置子菜单: {secondaryIndex}");
    // 示例：打开音频设置
    // AudioManager.Instance?.OpenAudioSettings();
    }

    /// <summary>
    /// 处理退出游戏
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.ExecuteSecondaryMenuAction(int, int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandleExitGame()
    {
        Debug.Log("退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 关闭二级面板
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Update()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.PlaySound(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CloseSecondaryPanel()
    {
        if (isAnimating || !isSecondaryPanelOpen)
        {
            return;
        }

        PlaySound(cancelSound);
        StartCoroutine(CloseSecondaryPanelCoroutine());
    }

    /// <summary>
    /// 关闭二级面板协程
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.CloseSecondaryPanel()；下游（含内部回调体） Action.Invoke()、MonoBehaviour.StartCoroutine(IEnumerator)、MultiLevelMenuManager.ClearSecondaryPanels()、MultiLevelMenuManager.FadeDimmer(bool)、MultiLevelMenuManager.SlideOutSecondaryPanels()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator CloseSecondaryPanelCoroutine()
    {
        isAnimating = true;
        // 背景恢复
        StartCoroutine(FadeDimmer(false));
        // 滑出动画
        yield return StartCoroutine(SlideOutSecondaryPanels());
        // 清除面板
        ClearSecondaryPanels();
        isSecondaryPanelOpen = false;
        currentSecondaryIndex = -1;
        isAnimating = false;
        // receiverValue8（判空前缓存的菜单已关闭时的通知回调）：从菜单已关闭时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()。
global::System.Action receiverValue8 = OnMenuClosed;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.Invoke();
        }
    }

    /// <summary>
    /// 清除二级面板
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.ClearAllMenuItems()、MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.OpenSecondaryPanel(int)；下游（含内部回调体） List&lt;GameObject&gt;.Clear()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearSecondaryPanels()
    {
        // panel（面板）：每轮从实例化的次要面板集合取得一个元素，仅代表当前这一项。使用位置：作为 Object.Destroy(Object) 的参数、MultiLevelMenuManager.ClearSecondaryPanels()。
foreach (var panel in instantiatedSecondaryPanels)
        {
            if (panel != null)
            {
                Destroy(panel);
            }
        }

        instantiatedSecondaryPanels.Clear();
    }

#region ===== 动画系统 =====
    /// <summary>
    /// 淡入一级菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.ShowPrimaryMenu()；下游（含内部回调体） Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator FadeInPrimaryMenu()
    {
        if (primaryMenuGroup == null)
        {
            yield break;
        }

        primaryMenuGroup.alpha = 0f;
        primaryMenuGroup.interactable = true;
        primaryMenuGroup.blocksRaycasts = true;
        // elapsed（已用）：使用位置：MultiLevelMenuManager.FadeInPrimaryMenu()。
float elapsed = 0f;
        // duration（持续时间）：使用位置：MultiLevelMenuManager.FadeInPrimaryMenu()。
float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、MultiLevelMenuManager.FadeInPrimaryMenu()。
float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // 平滑步进
            primaryMenuGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        primaryMenuGroup.alpha = 1f;
    }

    /// <summary>
    /// 滑入二级面板
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.OpenSecondaryPanel(int)；下游（含内部回调体） Mathf.Lerp(float, float, float)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="panelRect">中文含义：面板矩形。使用位置：MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)。</param>
    /// <param name="panelGroup">中文含义：面板分组。使用位置：MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)。</param>
    private IEnumerator SlideInSecondaryPanel(RectTransform panelRect, CanvasGroup panelGroup)
    {
        if (panelRect == null)
        {
            yield break;
        }

        isAnimating = true;
        // startPos（起始位置）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
Vector2 startPos = panelRect.anchoredPosition;
        // endPos（结束位置）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)。
Vector2 endPos = Vector2.zero;
        // elapsed（已用）：使用位置：MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)。
float elapsed = 0f;
        while (elapsed < secondarySlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、MultiLevelMenuManager.SlideInSecondaryPanel(RectTransform, CanvasGroup)。
float t = elapsed / secondarySlideDuration;
            t = t * t * (3f - 2f * t); // 平滑步进
            panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            if (panelGroup != null)
            {
                panelGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }

            yield return null;
        }

        panelRect.anchoredPosition = endPos;
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
        }

        isAnimating = false;
    }

    /// <summary>
    /// 滑出二级面板
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.CloseSecondaryPanelCoroutine()；下游（含内部回调体） GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator SlideOutSecondaryPanels()
    {
        // elapsed（已用）：使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
float elapsed = 0f;
        while (elapsed < secondarySlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、MultiLevelMenuManager.SlideOutSecondaryPanels()。
float t = elapsed / secondarySlideDuration;
            t = t * t * (3f - 2f * t);
            // panel（面板）：每轮从实例化的次要面板集合取得一个元素，仅代表当前这一项。使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
foreach (var panel in instantiatedSecondaryPanels)
            {
                if (panel != null)
                {
                    // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
RectTransform rect = panel.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        // pos（位置）：使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
Vector2 pos = rect.anchoredPosition;
                        pos.x = Mathf.Lerp(0, secondarySlideOffset, t);
                        rect.anchoredPosition = pos;
                    }

                    // group（分组）：保存 GameObject.GetComponent<CanvasGroup>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
CanvasGroup group = panel.GetComponent<CanvasGroup>();
                    if (group != null)
                    {
                        group.alpha = Mathf.Lerp(1f, 0f, t);
                    }
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 背景变暗/恢复
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.CloseSecondaryPanelCoroutine()、MultiLevelMenuManager.OpenSecondaryPanel(int)；下游（含内部回调体） Color.Color(float, float, float, float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fadeIn">中文含义：是否淡入。使用位置：MultiLevelMenuManager.FadeDimmer(bool)。</param>
    private IEnumerator FadeDimmer(bool fadeIn)
    {
        if (dimmerImage == null)
        {
            yield break;
        }

        dimmerImage.raycastTarget = fadeIn;
        // elapsed（已用）：使用位置：MultiLevelMenuManager.FadeDimmer(bool)。
float elapsed = 0f;
        // startAlpha（起始透明度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float startAlpha = dimmerImage.color.a;
        // endAlpha（结束透明度）：使用位置：作为 Color.Color(float, float, float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、MultiLevelMenuManager.FadeDimmer(bool)。
float endAlpha;
        if (fadeIn)
        {
            endAlpha = dimmerAlpha;
        }
        else
        {
            endAlpha = 0f;
        }

        while (elapsed < dimmerFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、MultiLevelMenuManager.FadeDimmer(bool)。
float t = elapsed / dimmerFadeDuration;
            t = t * t * (3f - 2f * t);
            // currentAlpha（当前透明度）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Color.Color(float, float, float, float) 的参数。
float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, t);
            dimmerImage.color = new Color(0, 0, 0, currentAlpha);
            yield return null;
        }

        dimmerImage.color = new Color(0, 0, 0, endAlpha);
    }

#endregion
#region ===== 键盘导航 =====
    /// <summary>
    /// 一级菜单键盘导航
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Update()；下游（含内部回调体） Input.GetKeyDown(KeyCode)、MultiLevelMenuManager.NavigatePrimaryMenu(int)、MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandlePrimaryKeyboardNavigation()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NavigatePrimaryMenu(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            NavigatePrimaryMenu(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (currentPrimaryIndex >= 0)
            {
                OnPrimaryMenuItemClicked(currentPrimaryIndex);
            }
        }
    }

    /// <summary>
    /// 二级菜单键盘导航
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.Update()；下游（含内部回调体） Input.GetKeyDown(KeyCode)、MultiLevelMenuManager.NavigateSecondaryMenu(int)、MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandleSecondaryKeyboardNavigation()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NavigateSecondaryMenu(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            NavigateSecondaryMenu(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (currentPrimaryIndex >= 0 && currentSecondaryIndex >= 0)
            {
                OnSecondaryMenuItemClicked(currentPrimaryIndex, currentSecondaryIndex);
            }
        }
    }

    /// <summary>
    /// 导航一级菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.HandlePrimaryKeyboardNavigation()；下游（含内部回调体） MultiLevelMenuManager.PlaySound(string)、MultiLevelMenuManager.UpdatePrimaryMenuSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：MultiLevelMenuManager.NavigatePrimaryMenu(int)。</param>
    private void NavigatePrimaryMenu(int direction)
    {
        if (primaryMenuItems.Count == 0)
        {
            return;
        }

        // oldIndex（旧索引）：使用位置：MultiLevelMenuManager.NavigatePrimaryMenu(int)。
int oldIndex = currentPrimaryIndex;
        // newIndex（新索引）：使用位置：作为 List<MultiLevelMenuManager.PrimaryMenuItem>.this[int] 的参数、MultiLevelMenuManager.NavigatePrimaryMenu(int)。
int newIndex = oldIndex + direction;
        // 循环导航
        if (newIndex < 0)
        {
            newIndex = primaryMenuItems.Count - 1;
        }

        if (newIndex >= primaryMenuItems.Count)
        {
            newIndex = 0;
        }

        // 跳过禁用项
        // attempts（尝试次数）：使用位置：MultiLevelMenuManager.NavigatePrimaryMenu(int)。
int attempts = 0;
        while (!primaryMenuItems[newIndex].isEnabled && attempts < primaryMenuItems.Count)
        {
            newIndex = (newIndex + direction + primaryMenuItems.Count) % primaryMenuItems.Count;
            attempts++;
        }

        if (newIndex != oldIndex)
        {
            currentPrimaryIndex = newIndex;
            UpdatePrimaryMenuSelection();
            PlaySound(hoverSound);
        }
    }

    /// <summary>
    /// 导航二级菜单
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.HandleSecondaryKeyboardNavigation()；下游（含内部回调体） MultiLevelMenuManager.PlaySound(string)、MultiLevelMenuManager.UpdateSecondaryMenuSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：MultiLevelMenuManager.NavigateSecondaryMenu(int)。</param>
    private void NavigateSecondaryMenu(int direction)
    {
        if (currentPrimaryIndex < 0 || currentPrimaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // primaryItem（主要物品）：使用位置：MultiLevelMenuManager.NavigateSecondaryMenu(int)。
var primaryItem = primaryMenuItems[currentPrimaryIndex];
        if (primaryItem.secondaryItems.Count == 0)
        {
            return;
        }

        // oldIndex（旧索引）：使用位置：MultiLevelMenuManager.NavigateSecondaryMenu(int)。
int oldIndex = currentSecondaryIndex;
        // newIndex（新索引）：使用位置：作为 List<MultiLevelMenuManager.SecondaryMenuItem>.this[int] 的参数、MultiLevelMenuManager.NavigateSecondaryMenu(int)。
int newIndex = oldIndex + direction;
        // 循环导航
        if (newIndex < 0)
        {
            newIndex = primaryItem.secondaryItems.Count - 1;
        }

        if (newIndex >= primaryItem.secondaryItems.Count)
        {
            newIndex = 0;
        }

        // 跳过禁用项
        // attempts（尝试次数）：使用位置：MultiLevelMenuManager.NavigateSecondaryMenu(int)。
int attempts = 0;
        while (!primaryItem.secondaryItems[newIndex].isEnabled && attempts < primaryItem.secondaryItems.Count)
        {
            newIndex = (newIndex + direction + primaryItem.secondaryItems.Count) % primaryItem.secondaryItems.Count;
            attempts++;
        }

        if (newIndex != oldIndex)
        {
            currentSecondaryIndex = newIndex;
            UpdateSecondaryMenuSelection();
            PlaySound(hoverSound);
        }
    }

    /// <summary>
    /// 更新一级菜单选择状态
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.NavigatePrimaryMenu(int)；下游（含内部回调体） MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdatePrimaryMenuSelection()
    {
        // i（当前遍历索引）：使用位置：作为 MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool) 的参数、MultiLevelMenuManager.UpdatePrimaryMenuSelection()。
for (int i = 0; i < instantiatedPrimaryItems.Count; i++)
        {
            // isSelected（是否选定）：使用位置：作为 MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool) 的参数。
bool isSelected = (i == currentPrimaryIndex);
            UpdatePrimaryItemColor(i, isSelected);
        }
    }

    /// <summary>
    /// 更新二级菜单选择状态
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.NavigateSecondaryMenu(int)；下游（含内部回调体） MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateSecondaryMenuSelection()
    {
        // i（当前遍历索引）：使用位置：作为 MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool) 的参数、MultiLevelMenuManager.UpdateSecondaryMenuSelection()。
for (int i = 0; i < instantiatedSecondaryPanels.Count; i++)
        {
            // isSelected（是否选定）：使用位置：作为 MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool) 的参数。
bool isSelected = (i == currentSecondaryIndex);
            UpdateSecondaryItemColor(i, isSelected);
        }
    }

    /// <summary>
    /// 更新一级菜单项颜色
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.UpdatePrimaryMenuSelection()；下游（含内部回调体） GameObject.GetComponentInChildren&lt;Text&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 List&lt;GameObject&gt;.this[int] 的参数、作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。</param>
    /// <param name="isSelected">中文含义：是否选定。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。</param>
    private void UpdatePrimaryItemColor(int index, bool isSelected)
    {
        if (index < 0 || index >= instantiatedPrimaryItems.Count)
        {
            return;
        }

        // item（当前游戏对象）：使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。
GameObject item = instantiatedPrimaryItems[index];
        if (item == null)
        {
            return;
        }

        // textComponent（文字组件）：保存 GameObject.GetComponentInChildren<Text>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。
Text textComponent = item.GetComponentInChildren<Text>();
        if (textComponent != null)
        {
            if (primaryMenuItems[index].isEnabled)
            {
                // assignmentReceiver10（赋值前保存的目标文字组件）：从文字组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。
global::UnityEngine.UI.Text assignmentReceiver10 = textComponent;
                if (isSelected)
                {
                    assignmentReceiver10.color = primarySelectedColor;
                }
                else
                {
                    assignmentReceiver10.color = primaryNormalColor;
                }

                // assignmentReceiver12（赋值前保存的目标文字组件）：从文字组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MultiLevelMenuManager.UpdatePrimaryItemColor(int, bool)。
global::UnityEngine.UI.Text assignmentReceiver12 = textComponent;
                if (isSelected)
                {
                    assignmentReceiver12.fontStyle = FontStyle.Bold;
                }
                else
                {
                    assignmentReceiver12.fontStyle = FontStyle.Normal;
                }
            }
            else
            {
                textComponent.color = primaryDisabledColor;
                textComponent.fontStyle = FontStyle.Normal;
            }
        }
    }

    /// <summary>
    /// 更新二级菜单项颜色
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.UpdateSecondaryMenuSelection()；下游（含内部回调体） GameObject.GetComponentInChildren&lt;Text&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 List&lt;GameObject&gt;.this[int] 的参数、作为 List&lt;MultiLevelMenuManager.SecondaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</param>
    /// <param name="isSelected">中文含义：是否选定。使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。</param>
    private void UpdateSecondaryItemColor(int index, bool isSelected)
    {
        if (index < 0 || index >= instantiatedSecondaryPanels.Count)
        {
            return;
        }

        // item（当前游戏对象）：使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。
GameObject item = instantiatedSecondaryPanels[index];
        if (item == null)
        {
            return;
        }

        // textComponent（文字组件）：保存 GameObject.GetComponentInChildren<Text>() 的返回结果，供当前步骤使用。使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。
Text textComponent = item.GetComponentInChildren<Text>();
        if (textComponent != null)
        {
            if (currentPrimaryMenuItem != null && index < currentPrimaryMenuItem.secondaryItems.Count)
            {
                // secondaryItem（次要物品）：使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。
var secondaryItem = currentPrimaryMenuItem.secondaryItems[index];
                if (secondaryItem.isEnabled)
                {
                    // assignmentReceiver14（赋值前保存的目标文字组件）：从文字组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。
global::UnityEngine.UI.Text assignmentReceiver14 = textComponent;
                    if (isSelected)
                    {
                        assignmentReceiver14.color = primarySelectedColor;
                    }
                    else
                    {
                        assignmentReceiver14.color = primaryNormalColor;
                    }

                    // assignmentReceiver16（赋值前保存的目标文字组件）：从文字组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MultiLevelMenuManager.UpdateSecondaryItemColor(int, bool)。
global::UnityEngine.UI.Text assignmentReceiver16 = textComponent;
                    if (isSelected)
                    {
                        assignmentReceiver16.fontStyle = FontStyle.Bold;
                    }
                    else
                    {
                        assignmentReceiver16.fontStyle = FontStyle.Normal;
                    }
                }
                else
                {
                    textComponent.color = primaryDisabledColor;
                    textComponent.fontStyle = FontStyle.Normal;
                }
            }
        }
    }

#endregion
#region ===== 音效 =====
    /// <summary>
    /// 播放音效
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.CloseSecondaryPanel()、MultiLevelMenuManager.NavigatePrimaryMenu(int)、MultiLevelMenuManager.NavigateSecondaryMenu(int)、MultiLevelMenuManager.OnPrimaryMenuItemClicked(int)、MultiLevelMenuManager.OnSecondaryMenuItemClicked(int, int)；下游（含内部回调体） AudioManager.PlaySfx(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="soundName">中文含义：声音名称。使用位置：作为 AudioManager.PlaySfx(string) 的参数。</param>
    private void PlaySound(string soundName)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(soundName);
        }
    }

#endregion
#region ===== 公共接口 =====
    /// <summary>
    /// 添加一级菜单项
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.Add(MultiLevelMenuManager.PrimaryMenuItem)、MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.PrimaryMenuItem.PrimaryMenuItem()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displayName">中文含义：显示名称。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)。</param>
    /// <param name="icon">中文含义：图标。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)。</param>
    /// <param name="isEnabled">中文含义：是否启用。使用位置：MultiLevelMenuManager.AddPrimaryMenuItem(string, Sprite, bool)。</param>
    public void AddPrimaryMenuItem(string displayName, Sprite icon = null, bool isEnabled = true)
    {
        // newItem（新物品）：使用位置：作为 List<MultiLevelMenuManager.PrimaryMenuItem>.Add(MultiLevelMenuManager.PrimaryMenuItem) 的参数。
PrimaryMenuItem newItem = new PrimaryMenuItem
        {
            displayName = displayName,
            icon = icon,
            isEnabled = isEnabled
        };
        primaryMenuItems.Add(newItem);
        GeneratePrimaryMenuItems();
    }

    /// <summary>
    /// 添加二级菜单项
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;MultiLevelMenuManager.SecondaryMenuItem&gt;.Add(MultiLevelMenuManager.SecondaryMenuItem)、MultiLevelMenuManager.SecondaryMenuItem.SecondaryMenuItem()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="primaryIndex">中文含义：主要索引。使用位置：作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)。</param>
    /// <param name="displayName">中文含义：显示名称。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)。</param>
    /// <param name="icon">中文含义：图标。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)。</param>
    /// <param name="isEnabled">中文含义：是否启用。使用位置：MultiLevelMenuManager.AddSecondaryMenuItem(int, string, Sprite, bool)。</param>
    public void AddSecondaryMenuItem(int primaryIndex, string displayName, Sprite icon = null, bool isEnabled = true)
    {
        if (primaryIndex < 0 || primaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // newItem（新物品）：使用位置：作为 List<MultiLevelMenuManager.SecondaryMenuItem>.Add(MultiLevelMenuManager.SecondaryMenuItem) 的参数。
SecondaryMenuItem newItem = new SecondaryMenuItem
        {
            displayName = displayName,
            icon = icon,
            isEnabled = isEnabled
        };
        primaryMenuItems[primaryIndex].secondaryItems.Add(newItem);
    }

    /// <summary>
    /// 设置菜单项启用状态
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MultiLevelMenuManager.GeneratePrimaryMenuItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="primaryIndex">中文含义：主要索引。使用位置：作为 List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.SetMenuItemEnabled(int, int, bool)。</param>
    /// <param name="secondaryIndex">中文含义：次要索引。使用位置：作为 List&lt;MultiLevelMenuManager.SecondaryMenuItem&gt;.this[int] 的参数、MultiLevelMenuManager.SetMenuItemEnabled(int, int, bool)。</param>
    /// <param name="isEnabled">中文含义：是否启用。使用位置：MultiLevelMenuManager.SetMenuItemEnabled(int, int, bool)。</param>
    public void SetMenuItemEnabled(int primaryIndex, int secondaryIndex, bool isEnabled)
    {
        if (primaryIndex < 0 || primaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        if (secondaryIndex < 0)
        {
            primaryMenuItems[primaryIndex].isEnabled = isEnabled;
        }
        else if (secondaryIndex < primaryMenuItems[primaryIndex].secondaryItems.Count)
        {
            primaryMenuItems[primaryIndex].secondaryItems[secondaryIndex].isEnabled = isEnabled;
        }

        GeneratePrimaryMenuItems();
    }

    /// <summary>
    /// 清空所有菜单项
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;MultiLevelMenuManager.PrimaryMenuItem&gt;.Clear()、MultiLevelMenuManager.ClearSecondaryPanels()、MultiLevelMenuManager.GeneratePrimaryMenuItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearAllMenuItems()
    {
        primaryMenuItems.Clear();
        GeneratePrimaryMenuItems();
        ClearSecondaryPanels();
    }
#endregion
}
