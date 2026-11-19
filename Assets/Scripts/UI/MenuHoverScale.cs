using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Mac Dock 风格菜单联动缩放
/// 鼠标悬停某个菜单项时，该项放大、其他项同步缩小，移出后全部恢复
/// 纯 UGUI 实现，不依赖第三方插件
/// </summary>
public class MenuHoverScale : MonoBehaviour
{
    /// <summary>中文含义：菜单物品列表。使用位置：MenuHoverScale.Start()、MenuHoverScale.StartTransition(int)、MenuHoverScale.Update()。</summary>
    [Header("菜单项（按顺序赋值）")]
    public RectTransform[] menuItems;
    /// <summary>中文含义：悬停缩放。使用位置：MenuHoverScale.StartTransition(int)。</summary>
    [Header("缩放参数")]
    public float hoverScale = 1.18f;
    /// <summary>中文含义：缩小缩放。使用位置：MenuHoverScale.StartTransition(int)。</summary>
    public float shrinkScale = 0.88f;
    /// <summary>中文含义：默认缩放。使用位置：MenuHoverScale.Start()、MenuHoverScale.StartTransition(int)。</summary>
    public float defaultScale = 1f;
    /// <summary>中文含义：悬停透明度。使用位置：MenuHoverScale.StartTransition(int)。</summary>
    [Header("透明度参数")]
    [Range(0f, 1f)]
    public float hoverAlpha = 1f;
    /// <summary>中文含义：缩小透明度。使用位置：MenuHoverScale.StartTransition(int)。</summary>
    [Range(0f, 1f)]
    public float shrinkAlpha = 0.4f;
    /// <summary>中文含义：默认透明度。使用位置：MenuHoverScale.Start()、MenuHoverScale.StartTransition(int)。</summary>
    [Range(0f, 1f)]
    public float defaultAlpha = 0.7f;
    /// <summary>中文含义：过渡时长。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    [Header("过渡")]
    public float transitionDuration = 0.25f;
    /// <summary>中文含义：缓动中曲线。使用位置：MenuHoverScale.Update()。</summary>
    public AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>中文含义：被悬停的索引。当前悬停索引，-1 表示无悬停</summary>
    private int hoveredIndex = -1;
    /// <summary>中文含义：画布分组集合。每个项关联的 CanvasGroup（透明度控制）</summary>
    private CanvasGroup[] canvasGroups;
    /// <summary>中文含义：原始缩放集合。每个项的原始缩放（缓存）</summary>
    private Vector3[] originalScales;
    /// <summary>中文含义：来源缩放集合。每个项当前过渡的开始缩放</summary>
    private float[] fromScales;
    /// <summary>中文含义：目标缩放集合。每个项当前过渡的目标缩放</summary>
    private float[] toScales;
    /// <summary>中文含义：来源透明度集合。每个项当前过渡的开始透明度</summary>
    private float[] fromAlphas;
    /// <summary>中文含义：目标透明度集合。每个项当前过渡的目标透明度</summary>
    private float[] toAlphas;
    /// <summary>中文含义：过渡计时器。过渡计时器</summary>
    private float transitionTimer;
    /// <summary>中文含义：是否过渡中。是否正在过渡</summary>
    private bool isTransitioning;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：上游 MenuHoverScale.RefreshItems()；下游（含内部回调体） Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;EventTrigger&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;EventTrigger&gt;()、List&lt;EventTrigger.Entry&gt;.Add(EventTrigger.Entry)、MenuHoverScale.OnItemHover(int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (menuItems == null || menuItems.Length == 0)
        {
            // 自动收集子节点
            menuItems = new RectTransform[transform.childCount];
            // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、MenuHoverScale.Start()。
for (int i = 0; i < transform.childCount; i++)
            {
                menuItems[i] = transform.GetChild(i) as RectTransform;
            }
        }

        // count（数量）：使用位置：MenuHoverScale.Start()。
int count = menuItems.Length;
        canvasGroups = new CanvasGroup[count];
        originalScales = new Vector3[count];
        fromScales = new float[count];
        toScales = new float[count];
        fromAlphas = new float[count];
        toAlphas = new float[count];
        // i（当前遍历索引）：使用位置：MenuHoverScale.Start()。
for (int i = 0; i < count; i++)
        {
            if (menuItems[i] == null)
            {
                continue;
            }

            originalScales[i] = menuItems[i].localScale;
            // 获取或添加 CanvasGroup
            canvasGroups[i] = menuItems[i].GetComponent<CanvasGroup>();
            if (canvasGroups[i] == null)
            {
                canvasGroups[i] = menuItems[i].gameObject.AddComponent<CanvasGroup>();
            }

            // 获取或添加事件触发器
            // trigger（触发器）：保存 Component.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：MenuHoverScale.Start()。
EventTrigger trigger = menuItems[i].GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = menuItems[i].gameObject.AddComponent<EventTrigger>();
            }

            // 绑定 PointerEnter
            // enterEntry（进入入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、MenuHoverScale.Start()。
EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            // capturedIndex（已捕获索引）：使用位置：作为 MenuHoverScale.OnItemHover(int) 的参数、作为 MenuHoverScale.OnItemUnhover(int) 的参数。
int capturedIndex = i;
            enterEntry.callback.AddListener(( /* 中文含义：当前基础事件配置。 */data) => OnItemHover(capturedIndex));
            trigger.triggers.Add(enterEntry);
            // 绑定 PointerExit
            // exitEntry（退出入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、MenuHoverScale.Start()。
EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener(( /* 中文含义：当前基础事件配置。 */data) => OnItemUnhover(capturedIndex));
            trigger.triggers.Add(exitEntry);
            // 初始状态
            fromScales[i] = defaultScale;
            toScales[i] = defaultScale;
            fromAlphas[i] = defaultAlpha;
            toAlphas[i] = defaultAlpha;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isTransitioning)
        {
            return;
        }

        transitionTimer += Time.unscaledDeltaTime;
        // t（当前界面动画的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 AnimationCurve.Evaluate(float) 的参数、MenuHoverScale.Update()。
float t = Mathf.Clamp01(transitionTimer / transitionDuration);
        // curvedT（弯曲进度）：保存 AnimationCurve.Evaluate(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float curvedT = easingCurve.Evaluate(t);
        // i（当前遍历索引）：使用位置：MenuHoverScale.Update()。
for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
            {
                continue;
            }

            // s（当前菜单项的缩放倍率）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：MenuHoverScale.Update()。
float s = Mathf.Lerp(fromScales[i], toScales[i], curvedT);
            menuItems[i].localScale = originalScales[i] * s;
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].alpha = Mathf.Lerp(fromAlphas[i], toAlphas[i], curvedT);
            }
        }

        if (t >= 1f)
        {
            isTransitioning = false;
        }
    }

    /// <summary>鼠标进入某个菜单项</summary>
    /// <remarks>静态调用链：上游 MenuHoverScale.Start()；下游（含内部回调体） MenuHoverScale.StartTransition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 MenuHoverScale.StartTransition(int) 的参数、MenuHoverScale.OnItemHover(int)。</param>
    void OnItemHover(int index)
    {
        // 如果已在同目标过渡中，不重复触发
        if (hoveredIndex == index)
        {
            return;
        }

        hoveredIndex = index;
        StartTransition(index);
    }

    /// <summary>鼠标离开某个菜单项</summary>
    /// <remarks>静态调用链：上游 MenuHoverScale.Start()；下游（含内部回调体） MenuHoverScale.StartTransition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：MenuHoverScale.OnItemUnhover(int)。</param>
    void OnItemUnhover(int index)
    {
        // 只有当离开的是当前悬停项时，才取消悬停
        if (hoveredIndex != index)
        {
            return;
        }

        hoveredIndex = -1;
        StartTransition(-1);
    }

    /// <summary>开始过渡到新状态</summary>
    /// <remarks>静态调用链：上游 MenuHoverScale.ClearHover()、MenuHoverScale.OnItemHover(int)、MenuHoverScale.OnItemUnhover(int)、MenuHoverScale.SetHoveredIndex(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="newHovered">中文含义：新被悬停的。使用位置：MenuHoverScale.StartTransition(int)。</param>
    void StartTransition(int newHovered)
    {
        // 记录当前值作为起点
        // i（当前遍历索引）：使用位置：MenuHoverScale.StartTransition(int)。
for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
            {
                continue;
            }

            fromScales[i] = menuItems[i].localScale.x / originalScales[i].x;
            // assignmentReceiver1（赋值前保存的目标来源透明度集合）：从来源透明度集合读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MenuHoverScale.StartTransition(int)。
float[] assignmentReceiver1 = fromAlphas;
            // assignmentIndex2（赋值前保存的索引当前遍历索引）：从索引读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MenuHoverScale.StartTransition(int)。
int assignmentIndex2 = i;
            if (canvasGroups[i] != null)
            {
                assignmentReceiver1[assignmentIndex2] = canvasGroups[i].alpha;
            }
            else
            {
                assignmentReceiver1[assignmentIndex2] = defaultAlpha;
            }
        }

        // 设定目标值
        if (newHovered >= 0)
        {
            // i（当前遍历索引）：使用位置：MenuHoverScale.StartTransition(int)。
for (int i = 0; i < menuItems.Length; i++)
            {
                if (i == newHovered)
                {
                    toScales[i] = hoverScale;
                    toAlphas[i] = hoverAlpha;
                }
                else
                {
                    toScales[i] = shrinkScale;
                    toAlphas[i] = shrinkAlpha;
                }
            }
        }
        else
        {
            // 全部恢复默认
            // i（当前遍历索引）：使用位置：MenuHoverScale.StartTransition(int)。
for (int i = 0; i < menuItems.Length; i++)
            {
                toScales[i] = defaultScale;
                toAlphas[i] = defaultAlpha;
            }
        }

        transitionTimer = 0f;
        isTransitioning = true;
    }

    /// <summary>外部强制设置悬停项（用于键盘导航联动）</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuMouse()、TitleScreenManager.NavigateMenu(int)；下游（含内部回调体） MenuHoverScale.StartTransition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：作为 MenuHoverScale.StartTransition(int) 的参数、MenuHoverScale.SetHoveredIndex(int)。</param>
    public void SetHoveredIndex(int index)
    {
        if (hoveredIndex == index)
        {
            return;
        }

        hoveredIndex = index;
        StartTransition(index);
    }

    /// <summary>外部清除悬停</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuMouse()；下游（含内部回调体） MenuHoverScale.StartTransition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearHover()
    {
        if (hoveredIndex == -1)
        {
            return;
        }

        hoveredIndex = -1;
        StartTransition(-1);
    }

    /// <summary>重新初始化（菜单结构变化时调用）</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuHoverScale.Start()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RefreshItems()
    {
        Start();
    }
}
