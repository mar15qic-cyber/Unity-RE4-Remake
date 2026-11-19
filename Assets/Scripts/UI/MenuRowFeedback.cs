using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 菜单行的鼠标悬停与导航选中反馈。EventSystem 更新本组件的两种临时状态，Refresh 只写行背景和标记；
/// 导航选中后将当前行滚入父级 ScrollRect，不拦截或重新实现拖放业务。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "菜单行反馈")]
public class MenuRowFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    /// <summary>中文含义：背景。当前行的可选背景图，由反馈状态选择正常或选中颜色。</summary>
    [Header("行级反馈")]
    [UnityEngine.Serialization.FormerlySerializedAs("背景")]
    [UnityEngine.InspectorName("背景")]
    public Image background;
    /// <summary>中文含义：标记。当前行的可选选中标记，有悬停或导航焦点时启用。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("标记")]
    [UnityEngine.InspectorName("标记")]
    public Image marker;
    /// <summary>中文含义：常态的颜色。既无鼠标悬停也无导航焦点时的背景颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("常态")]
    [UnityEngine.InspectorName("常态")]
    public Color normalState = Color.clear;
    /// <summary>中文含义：选中的颜色。鼠标悬停或导航焦点任意成立时的背景颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("选中")]
    [UnityEngine.InspectorName("选中")]
    public Color selected = new Color(.8f, .82f, .78f, .12f);
    /// <summary>中文含义：是否悬停。鼠标是否停留当前行，只由指针回调或停用清理写入。</summary>
    bool hover;
    /// <summary>中文含义：是否焦点。当前行是否持有 EventSystem 导航选中，只由选中回调或停用清理写入。</summary>
    bool focus;
    /// <summary>EventSystem 指针进入回调，开启悬停并立即刷新行表现。</summary>
    /// <param name="e">中文含义：当前指针事件数据。系统指针数据；当前反馈只需要进入事实。</param>
    public void OnPointerEnter(PointerEventData e)
    {
        hover = true;
        Refresh();
    }

    /// <summary>EventSystem 指针退出回调，释放悬停；若仍有导航焦点则继续保持选中表现。</summary>
    /// <param name="e">中文含义：当前指针事件数据。系统指针数据；当前反馈只需要退出事实。</param>
    public void OnPointerExit(PointerEventData e)
    {
        hover = false;
        Refresh();
    }

    /// <summary>EventSystem 导航选中回调，开启焦点、刷新表现，并将本行滚入父视口。</summary>
    /// <param name="e">中文含义：当前基础事件配置。系统选中事件数据，当前不读取附加参数。</param>
    public void OnSelect(BaseEventData e)
    {
        focus = true;
        Refresh();
        ScrollIntoViewport();
    }

    /// <summary>EventSystem 导航取消回调，释放焦点并保留可能仍在生效的鼠标悬停。</summary>
    /// <param name="e">中文含义：当前基础事件配置。系统取消选中数据，当前不读取附加参数。</param>
    public void OnDeselect(BaseEventData e)
    {
        focus = false;
        Refresh();
    }

    /// <summary>Unity 停用回调，清除两类临时状态并恢复行表现。</summary>
    void OnDisable()
    {
        hover = false;
        focus = false;
        Refresh();
    }

    /// <summary>所有状态回调共用的表现提交，背景与标记分别按现有引用判断，不写导航或业务状态。</summary>
    void Refresh()
    {
        if (background != null)
        {
            if (hover || focus)
            {
                background.color = selected;
            }
            else
            {
                background.color = normalState;
            }
        }

        if (marker != null)
        {
            marker.enabled = hover || focus;
        }
    }

    /// <summary>导航选中或界面宿主请求时调用，完成布局后比较本行与父级视口边界，仅在越界时停止惯性并修正纵向位置。</summary>
    public void ScrollIntoViewport()
    {
        // 最近的父滚动容器；缺内容或视口时保持原无操作行为。
        // 中文含义：scroll 为“滚动”。
var scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        // 当前行的布局节点，布局完成后读取，非 RectTransform 时不执行边界换算。
        // 中文含义：r 为“当前界面矩形变换”。
var r = transform as RectTransform;
        if (r == null)
        {
            return;
        }

        // 当前行在视口坐标系中的包围盒。
        // 中文含义：bounds 为“包围盒”。
var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, r);
        // 同坐标系中的视口矩形，用于上下边界比较。
        // 中文含义：view 为“视图”。
var view = scroll.viewport.rect;
        // 纵向越界量；优先修正上边界，否则检查下边界，保留原判断顺序。
        // 中文含义：delta 为“增量”。
float delta = 0;
        if (bounds.max.y > view.yMax)
        {
            delta = bounds.max.y - view.yMax;
        }
        else if (bounds.min.y < view.yMin)
        {
            delta = bounds.min.y - view.yMin;
        }

        if (Mathf.Abs(delta) > .1f)
        {
            scroll.StopMovement();
            scroll.content.anchoredPosition -= new Vector2(0, delta);
        }
    }
}
