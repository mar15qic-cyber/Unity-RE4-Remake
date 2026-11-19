using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 公文包三维展示的指针适配器。EventSystem 回调传入屏幕位置，展示组件投影到网格后，
/// 本组件通过 InventoryGridView 的已有委托通知背包界面处理悬停、选择和拖放；不直接修改物品或规则模型。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "公文包投影输入")]
public class AttacheCaseProjectionInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>中文含义：展示。三维展示宿主，提供交互许可和屏幕到公文包平面的投影。</summary>
    [Header("展示引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("展示")]
    [UnityEngine.InspectorName("展示")]
    public AttacheCasePresentationController presentation;
    /// <summary>中文含义：网格。背包网格视图，负责坐标换算并持有界面已经订阅的指针委托。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("网格")]
    [UnityEngine.InspectorName("网格")]
    public InventoryGridView mesh;
    /// <summary>中文含义：已按下物品。本次按下时命中的原实例，拖动和抬起继续通知同一实例，点击结束或停用时清空。</summary>
    GridItem pressedItem;
    /// <summary>中文含义：悬停物品。上帧悬停实例，按引用比较避免对同一物品重复发送进入事件。</summary>
    GridItem hoverItem;
    /// <summary>中文含义：是否指针所在内部。指针是否位于投影交互区域，进入和离开回调更新。</summary>
    bool pointerAtInner;
    /// <summary>按下、点击及悬停共用的定位入口；只在展示可交互且已有背包服务时投影，再按合法格子查询实例。</summary>
    /// <param name="position">中文含义：位置。EventSystem 或悬停轮询提供的屏幕位置。</param>
    /// <returns>对应格子的当前实例；不可交互、投影失败、越界或空格时返回 null。</returns>
    GridItem Find(Vector2 position)
    {
        if (presentation == null || !presentation.Interactive || InventoryManager.Instance == null)
        {
            return null;
        }

        // local 接收屏幕点在公文包展示平面上的网格局部位置。
        if (!presentation.ScreenRotateMesh(position, out var /* 中文含义：局部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ local))
        {
            return null;
        }

        // x 和 y 接收当前格子尺寸下的横纵网格坐标。
        mesh.LocalToGrid(local, mesh.CurrentCell, out int /* 中文含义：横向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ x, out int /* 中文含义：纵向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ y);
        if (x < 0 || y < 0 || x >= InventoryManager.Instance.CaseWidth || y >= InventoryManager.Instance.CaseHeight)
        {
            return null;
        }

        return InventoryManager.Instance.GetItemAt(x, y);
    }

    /// <summary>Unity 帧更新，区域内才定位悬停；引用变化时先退出旧物品，再登记并进入新物品。</summary>
    void Update()
    {
        // 当前帧悬停结果；指针不在区域内时不读取鼠标位置或执行投影。
        // 中文含义：next 为“下一个背包网格物品”。
GridItem next = null;
        if (pointerAtInner)
        {
            next = Find(Input.mousePosition);
        }

        if (ReferenceEquals(next, hoverItem))
        {
            return;
        }

        if (hoverItem != null)
        {
            // 保存一次旧物品退出接收者，保持原条件调用的单次委托读取。
            // 中文含义：exitHandler 为“退出处理器”。
Action exitHandler = mesh.onItemHoverExit;
            if (exitHandler != null)
            {
                exitHandler();
            }
        }

        hoverItem = next;
        if (next != null)
        {
            // 只有存在进入接收者时才发送当前实例。
            // 中文含义：enterHandler 为“进入处理器”。
Action<GridItem> enterHandler = mesh.onItemHoverEnter;
            if (enterHandler != null)
            {
                enterHandler(next);
            }
        }
    }

    /// <summary>EventSystem 指针进入回调，允许后续 Update 轮询投影悬停。</summary>
    /// <param name="data">中文含义：当前指针事件数据。系统提供的指针数据；该回调只切换区域标志。</param>
    public void OnPointerEnter(PointerEventData data)
    {
        pointerAtInner = true;
    }

    /// <summary>EventSystem 指针离开回调，下次 Update 将退出已有悬停。</summary>
    /// <param name="data">中文含义：当前指针事件数据。系统提供的指针数据；该回调不直接提交物品动作。</param>
    public void OnPointerExit(PointerEventData data)
    {
        pointerAtInner = false;
    }

    /// <summary>EventSystem 按下回调，捕获按下实例；只有左键命中才转发网格按下委托。</summary>
    /// <param name="data">中文含义：当前指针事件数据。提供按键和屏幕位置的系统指针数据。</param>
    public void OnPointerDown(PointerEventData data)
    {
        pressedItem = Find(data.position);
        if (pressedItem != null && data.button == PointerEventData.InputButton.Left)
        {
            // 单次读取按下接收者，位置参数只在接收者存在时读取。
            // 中文含义：downHandler 为“向下处理器”。
Action<GridItem, Vector2> downHandler = mesh.onItemPointerDown;
            if (downHandler != null)
            {
                downHandler(pressedItem, data.position);
            }
        }
    }

    /// <summary>保留 EventSystem 拖动开始接口；实际起始实例在 OnPointerDown 捕获，不重复触发背包事务。</summary>
    /// <param name="data">中文含义：当前指针事件数据。系统拖动开始数据，当前保留为空实现。</param>
    public void OnBeginDrag(PointerEventData data)
    {
    }

    /// <summary>EventSystem 拖动回调，展示仍可交互且左键持有原按下实例时转发已有网格拖动协议。</summary>
    /// <param name="data">中文含义：当前指针事件数据。当前指针按键与屏幕位置。</param>
    public void OnDrag(PointerEventData data)
    {
        if (presentation.Interactive && pressedItem != null && data.button == PointerEventData.InputButton.Left)
        {
            // 当前拖动接收者快照，不直接改动实例的网格位置。
            // 中文含义：dragHandler 为“拖拽处理器”。
Action<GridItem, Vector2> dragHandler = mesh.onItemDrag;
            if (dragHandler != null)
            {
                dragHandler(pressedItem, data.position);
            }
        }
    }

    /// <summary>保留 EventSystem 拖动结束接口；实际释放流程仍走 OnPointerUp，不重复提交。</summary>
    /// <param name="data">中文含义：当前指针事件数据。系统拖动结束数据，当前保留为空实现。</param>
    public void OnEndDrag(PointerEventData data)
    {
    }

    /// <summary>EventSystem 抬起回调，满足交互与左键条件时向网格发送原按下实例及当前落点。</summary>
    /// <param name="data">中文含义：当前指针事件数据。当前指针按键与屏幕位置。</param>
    public void OnPointerUp(PointerEventData data)
    {
        if (presentation.Interactive && pressedItem != null && data.button == PointerEventData.InputButton.Left)
        {
            // 当前释放接收者快照，由界面决定后续移动、组合或取消。
            // 中文含义：upHandler 为“向上处理器”。
Action<GridItem, Vector2> upHandler = mesh.onItemPointerUp;
            if (upHandler != null)
            {
                upHandler(pressedItem, data.position);
            }
        }
    }

    /// <summary>EventSystem 点击回调；命中实例时通知物品点击，否则投影并通知格子点击，最后清除按下实例。</summary>
    /// <param name="data">中文含义：当前指针事件数据。提供点击按键与屏幕位置的系统数据。</param>
    public void OnPointerClick(PointerEventData data)
    {
        if (!presentation.Interactive)
        {
            return;
        }

        // 点击时重新查询当前实例，不使用可能已移动的按下位置替代。
        // 中文含义：item 为“当前背包网格物品”。
var item = Find(data.position);
        if (item != null)
        {
            // 物品点击接收者快照，参数仍只在存在接收者时读取。
            // 中文含义：itemHandler 为“物品处理器”。
Action<GridItem, PointerEventData.InputButton, Vector2> itemHandler = mesh.onItemClicked;
            if (itemHandler != null)
            {
                itemHandler(item, data.button, data.position);
            }
        }
        // local 接收空白点击在展示平面上的局部坐标，保持原分支再次投影的行为。
        else if (presentation.ScreenRotateMesh(data.position, out var /* 中文含义：局部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ local))
        {
            // x 和 y 是空白点击对应的格子坐标，沿用原入口不额外改变边界处理。
            mesh.LocalToGrid(local, mesh.CurrentCell, out int /* 中文含义：横向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ x, out int /* 中文含义：纵向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ y);
            // 格子点击接收者快照，实际菜单或选择行为由界面订阅者处理。
            // 中文含义：cellHandler 为“单元格处理器”。
Action<int, int> cellHandler = mesh.onCellClicked;
            if (cellHandler != null)
            {
                cellHandler(x, y);
            }
        }

        pressedItem = null;
    }

    /// <summary>Unity 停用回调，清除本组件的指针临时状态，沿用既有行为不额外广播悬停或释放事件。</summary>
    void OnDisable()
    {
        pointerAtInner = false;
        pressedItem = null;
        hoverItem = null;
    }
}
