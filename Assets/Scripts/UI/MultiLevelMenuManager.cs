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
    /// <summary>主要菜单容器。</summary>
    [Header("一级菜单")]
    public Transform primaryMenuContainer;
    /// <summary>主要菜单物品预制件。</summary>
    public GameObject primaryMenuItemPrefab;
    /// <summary>主要选定颜色。</summary>
    public Color primarySelectedColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>主要普通状态颜色。</summary>
    public Color primaryNormalColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>主要禁用状态颜色。</summary>
    public Color primaryDisabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    /// <summary>次要面板容器。</summary>
    [Header("二级子面板")]
    public Transform secondaryPanelContainer;
    /// <summary>次要面板预制件。</summary>
    public GameObject secondaryPanelPrefab;
    /// <summary>次要滑移偏移量。</summary>
    public float secondarySlideOffset = 800f;
    /// <summary>次要滑移持续时间。</summary>
    public float secondarySlideDuration = 0.3f;
    /// <summary>遮暗层图像。</summary>
    [Header("背景变暗")]
    public Image dimmerImage;
    /// <summary>遮暗层淡入淡出时长。</summary>
    public float dimmerFadeDuration = 0.25f;
    /// <summary>遮暗层透明度。</summary>
    public float dimmerAlpha = 0.6f;
    /// <summary>主要菜单分组。</summary>
    [Header("菜单层级")]
    public CanvasGroup primaryMenuGroup;
    /// <summary>次要面板分组。</summary>
    public CanvasGroup secondaryPanelGroup;
    /// <summary>主要菜单物品列表。</summary>
    [Header("菜单项定义")]
    public List<PrimaryMenuItem> primaryMenuItems = new List<PrimaryMenuItem>();
    /// <summary>悬停声音。</summary>
    [Header("声音效果")]
    public string hoverSound = "ui_hover";
    /// <summary>确认声音。</summary>
    public string confirmSound = "ui_confirm";
    /// <summary>取消声音。</summary>
    public string cancelSound = "ui_cancel";
    // 私有状态
    /// <summary>当前主要索引。</summary>
    private int currentPrimaryIndex = -1;
    /// <summary>当前次要索引。</summary>
    private int currentSecondaryIndex = -1;
    /// <summary>是否次要面板打开。</summary>
    private bool isSecondaryPanelOpen = false;
    /// <summary>是否动画播放。</summary>
    private bool isAnimating = false;
    /// <summary>实例化的主要物品列表。</summary>
    private List<GameObject> instantiatedPrimaryItems = new List<GameObject>();
    /// <summary>实例化的次要面板集合。</summary>
    private List<GameObject> instantiatedSecondaryPanels = new List<GameObject>();
    /// <summary>当前主要菜单物品。</summary>
    private PrimaryMenuItem currentPrimaryMenuItem;
    // 事件
    /// <summary>主要菜单选定时的通知回调。事件通信成员 OnPrimaryMenuSelected，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int> OnPrimaryMenuSelected;
    /// <summary>次要菜单选定时的通知回调。事件通信成员 OnSecondaryMenuSelected，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int, int> OnSecondaryMenuSelected;
    /// <summary>菜单已关闭时的通知回调。事件通信成员 OnMenuClosed，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action OnMenuClosed;
    /// <summary>
    /// 一级菜单项数据结构
    /// </summary>
    [Serializable]
    public class PrimaryMenuItem
    {
        /// <summary>显示名称。</summary>
        public string displayName;
        /// <summary>图标。</summary>
        public Sprite icon;
        /// <summary>是否启用。</summary>
        public bool isEnabled = true;
        /// <summary>次要物品列表。</summary>
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
        /// <summary>显示名称。</summary>
        public string displayName;
        /// <summary>图标。</summary>
        public Sprite icon;
        /// <summary>是否启用。</summary>
        public bool isEnabled = true;
        /// <summary>中文含义：自定义面板。</summary>
        public GameObject customPanel; // 可选的自定义面板
        /// <summary>选定时的通知回调。</summary>
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

        // 当前遍历索引。
for (int i = 0; i < primaryMenuItems.Count; i++)
        {
            // 菜单物品。
var menuItem = primaryMenuItems[i];
            // 物品对象。
GameObject itemObj = Instantiate(primaryMenuItemPrefab, primaryMenuContainer);
            itemObj.name = $"PrimaryItem_{i}_{menuItem.displayName}";
            // 设置显示文本
            // 文字组件。
Text textComponent = itemObj.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = menuItem.displayName;
            }

            // 设置图标
            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue1 = itemObj.transform.Find("Icon");
            // 图标组件。
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
            // 按钮。
Button button = itemObj.GetComponent<Button>();
            if (button != null)
            {
                // 当前元素索引。
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
    /// <param name="index">索引。</param>
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
        // 判空前缓存的主要菜单选定时的通知回调。
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
    /// <param name="primaryIndex">主要索引。</param>
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
            // 主要物品。
var primaryItem = primaryMenuItems[primaryIndex];
            // 创建二级面板容器
            // 面板容器。
GameObject panelContainer = new GameObject($"SecondaryPanel_{primaryItem.displayName}");
            // 容器矩形。
RectTransform containerRect = panelContainer.AddComponent<RectTransform>();
            containerRect.SetParent(secondaryPanelContainer, false);
            // 设置容器位置（从右侧滑入）
            containerRect.anchorMin = new Vector2(1, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(1, 0.5f);
            containerRect.sizeDelta = new Vector2(secondarySlideOffset, 0);
            containerRect.anchoredPosition = new Vector2(secondarySlideOffset, 0);
            // 添加CanvasGroup用于动画
            // 容器分组。
CanvasGroup containerGroup = panelContainer.AddComponent<CanvasGroup>();
            // 生成二级菜单项
            // 当前遍历索引。
for (int i = 0; i < primaryItem.secondaryItems.Count; i++)
            {
                // 次要物品。
var secondaryItem = primaryItem.secondaryItems[i];
                // 物品对象。
GameObject itemObj = Instantiate(secondaryPanelPrefab, containerRect);
                itemObj.name = $"SecondaryItem_{i}_{secondaryItem.displayName}";
                // 设置显示文本
                // 文字组件。
Text textComponent = itemObj.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = secondaryItem.displayName;
                }

                // 设置图标
                // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue4 = itemObj.transform.Find("Icon");
                // 图标组件。
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
                // 按钮。
Button button = itemObj.GetComponent<Button>();
                if (button != null)
                {
                    // 主要索引。
int primaryIdx = primaryIndex;
                    // 次要索引。
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
    /// <param name="primaryIndex">主要索引。</param>
    /// <param name="secondaryIndex">次要索引。</param>
    private void OnSecondaryMenuItemClicked(int primaryIndex, int secondaryIndex)
    {
        if (isAnimating)
        {
            return;
        }

        currentSecondaryIndex = secondaryIndex;
        PlaySound(confirmSound);
        // 判空前缓存的次要菜单选定时的通知回调。
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
    /// <param name="primaryIndex">主要索引。</param>
    /// <param name="secondaryIndex">次要索引。</param>
    private void ExecuteSecondaryMenuAction(int primaryIndex, int secondaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // 主要物品。
var primaryItem = primaryMenuItems[primaryIndex];
        if (secondaryIndex < 0 || secondaryIndex >= primaryItem.secondaryItems.Count)
        {
            return;
        }

        // 次要物品。
var secondaryItem = primaryItem.secondaryItems[secondaryIndex];
        // 执行自定义动作
        // 判空前缓存的次要物品的选定时的通知回调。
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
    /// <param name="secondaryIndex">次要索引。</param>
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
        // 判空前缓存的菜单已关闭时的通知回调。
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
        // 已用。
float elapsed = 0f;
        // 持续时间。
float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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
    /// <param name="panelRect">面板矩形。</param>
    /// <param name="panelGroup">面板分组。</param>
    private IEnumerator SlideInSecondaryPanel(RectTransform panelRect, CanvasGroup panelGroup)
    {
        if (panelRect == null)
        {
            yield break;
        }

        isAnimating = true;
        // 起始位置。
Vector2 startPos = panelRect.anchoredPosition;
        // 结束位置。
Vector2 endPos = Vector2.zero;
        // 已用。
float elapsed = 0f;
        while (elapsed < secondarySlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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
        // 已用。
float elapsed = 0f;
        while (elapsed < secondarySlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / secondarySlideDuration;
            t = t * t * (3f - 2f * t);
            // panel（面板）：每轮从实例化的次要面板集合取得一个元素，仅代表当前这一项。使用位置：MultiLevelMenuManager.SlideOutSecondaryPanels()。
foreach (var panel in instantiatedSecondaryPanels)
            {
                if (panel != null)
                {
                    // 矩形。
RectTransform rect = panel.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        // 位置。
Vector2 pos = rect.anchoredPosition;
                        pos.x = Mathf.Lerp(0, secondarySlideOffset, t);
                        rect.anchoredPosition = pos;
                    }

                    // 分组。
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
    /// <param name="fadeIn">是否淡入。</param>
    private IEnumerator FadeDimmer(bool fadeIn)
    {
        if (dimmerImage == null)
        {
            yield break;
        }

        dimmerImage.raycastTarget = fadeIn;
        // 已用。
float elapsed = 0f;
        // 起始透明度。
float startAlpha = dimmerImage.color.a;
        // 结束透明度。
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
            // 当前界面动画的归一化进度。
float t = elapsed / dimmerFadeDuration;
            t = t * t * (3f - 2f * t);
            // 当前透明度。
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
    /// <param name="direction">方向。</param>
    private void NavigatePrimaryMenu(int direction)
    {
        if (primaryMenuItems.Count == 0)
        {
            return;
        }

        // 旧索引。
int oldIndex = currentPrimaryIndex;
        // 新索引。
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
        // 尝试次数。
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
    /// <param name="direction">方向。</param>
    private void NavigateSecondaryMenu(int direction)
    {
        if (currentPrimaryIndex < 0 || currentPrimaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // 主要物品。
var primaryItem = primaryMenuItems[currentPrimaryIndex];
        if (primaryItem.secondaryItems.Count == 0)
        {
            return;
        }

        // 旧索引。
int oldIndex = currentSecondaryIndex;
        // 新索引。
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
        // 尝试次数。
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
        // 当前遍历索引。
for (int i = 0; i < instantiatedPrimaryItems.Count; i++)
        {
            // 是否选定。
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
        // 当前遍历索引。
for (int i = 0; i < instantiatedSecondaryPanels.Count; i++)
        {
            // 是否选定。
bool isSelected = (i == currentSecondaryIndex);
            UpdateSecondaryItemColor(i, isSelected);
        }
    }

    /// <summary>
    /// 更新一级菜单项颜色
    /// </summary>
    /// <remarks>静态调用链：上游 MultiLevelMenuManager.GeneratePrimaryMenuItems()、MultiLevelMenuManager.UpdatePrimaryMenuSelection()；下游（含内部回调体） GameObject.GetComponentInChildren&lt;Text&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    /// <param name="isSelected">是否选定。</param>
    private void UpdatePrimaryItemColor(int index, bool isSelected)
    {
        if (index < 0 || index >= instantiatedPrimaryItems.Count)
        {
            return;
        }

        // 当前游戏对象。
GameObject item = instantiatedPrimaryItems[index];
        if (item == null)
        {
            return;
        }

        // 文字组件。
Text textComponent = item.GetComponentInChildren<Text>();
        if (textComponent != null)
        {
            if (primaryMenuItems[index].isEnabled)
            {
                // 赋值前保存的目标文字组件。
global::UnityEngine.UI.Text assignmentReceiver10 = textComponent;
                if (isSelected)
                {
                    assignmentReceiver10.color = primarySelectedColor;
                }
                else
                {
                    assignmentReceiver10.color = primaryNormalColor;
                }

                // 赋值前保存的目标文字组件。
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
    /// <param name="index">索引。</param>
    /// <param name="isSelected">是否选定。</param>
    private void UpdateSecondaryItemColor(int index, bool isSelected)
    {
        if (index < 0 || index >= instantiatedSecondaryPanels.Count)
        {
            return;
        }

        // 当前游戏对象。
GameObject item = instantiatedSecondaryPanels[index];
        if (item == null)
        {
            return;
        }

        // 文字组件。
Text textComponent = item.GetComponentInChildren<Text>();
        if (textComponent != null)
        {
            if (currentPrimaryMenuItem != null && index < currentPrimaryMenuItem.secondaryItems.Count)
            {
                // 次要物品。
var secondaryItem = currentPrimaryMenuItem.secondaryItems[index];
                if (secondaryItem.isEnabled)
                {
                    // 赋值前保存的目标文字组件。
global::UnityEngine.UI.Text assignmentReceiver14 = textComponent;
                    if (isSelected)
                    {
                        assignmentReceiver14.color = primarySelectedColor;
                    }
                    else
                    {
                        assignmentReceiver14.color = primaryNormalColor;
                    }

                    // 赋值前保存的目标文字组件。
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
    /// <param name="soundName">声音名称。</param>
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
    /// <param name="displayName">显示名称。</param>
    /// <param name="icon">图标。</param>
    /// <param name="isEnabled">是否启用。</param>
    public void AddPrimaryMenuItem(string displayName, Sprite icon = null, bool isEnabled = true)
    {
        // 新物品。
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
    /// <param name="primaryIndex">主要索引。</param>
    /// <param name="displayName">显示名称。</param>
    /// <param name="icon">图标。</param>
    /// <param name="isEnabled">是否启用。</param>
    public void AddSecondaryMenuItem(int primaryIndex, string displayName, Sprite icon = null, bool isEnabled = true)
    {
        if (primaryIndex < 0 || primaryIndex >= primaryMenuItems.Count)
        {
            return;
        }

        // 新物品。
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
    /// <param name="primaryIndex">主要索引。</param>
    /// <param name="secondaryIndex">次要索引。</param>
    /// <param name="isEnabled">是否启用。</param>
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
