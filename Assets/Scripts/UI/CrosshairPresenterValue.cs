using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 动态扩散准星（已迁移输入至InputManager）
/// 保留所有准星显示逻辑：动态扩散计算、部件显隐、平滑移动
/// </summary>
/// <remarks>兼容旧资源的动态准星组件；当前正式 CrosshairUI 会停用组件并隐藏旧部件，正式显示逻辑应追踪 CrosshairUI。</remarks>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "准心")]
public class CrosshairPresenterValue : MonoBehaviour
{
    /// <summary>速度。</summary>
    [Header("瞄准设置")]
    public float speed = 15f;
    /// <summary>余量。</summary>
    public float margin = 2f;
    /// <summary>Top 表示“顶部”；Left 表示“左侧”；Bottom 表示“底部”；Right 表示“右侧”；Center 表示“中心”。</summary>
    [Header("准星部件")]
    public RectTransform Top, Left, Bottom, Right, Center;
    /// <summary>准心最小收缩尺寸（静止瞄准时的默认大小）。</summary>
    [Header("========== 动态缩放参数 ==========")]
    [Tooltip("准心最小收缩尺寸（静止瞄准时的默认大小）")]
    public float baseSpread = 5f;
    /// <summary>准心最大扩散尺寸（全速移动时）。</summary>
    [Tooltip("准心最大扩散尺寸（全速移动时）")]
    public float maxSpread = 40f;
    /// <summary>扩散时的平滑速度，越大越快。</summary>
    [Tooltip("扩散时的平滑速度，越大越快")]
    public float spreadLerpSpeed = 12f;
    /// <summary>缩小插值速度。收缩时的平滑速度，越大越快（建议比扩散速度慢，营造稳重手感）。</summary>
    [Tooltip("收缩时的平滑速度，越大越快（建议比扩散速度慢，营造稳重手感）")]
    public float shrinkLerpSpeed = 3f;
    /// <summary>静止多久后准心开始收缩（秒）。</summary>
    [Tooltip("静止多久后准心开始收缩（秒）")]
    public float stationaryTime = 1f;
    /// <summary>静止判定速度阈值，低于此值视为静止。</summary>
    [Tooltip("静止判定速度阈值，低于此值视为静止")]
    public float stationaryThreshold = 0.1f;
    /// <summary>引用最大速度。参考最大移动速度，用于扩散幅度百分比计算（一般设为角色跑步速度）。</summary>
    [Tooltip("参考最大移动速度，用于扩散幅度百分比计算（一般设为角色跑步速度）")]
    public float referenceMaxSpeed = 6f;
    /// <summary>玩家角色的Transform，用于读取移动速度。</summary>
    [Tooltip("玩家角色的Transform，用于读取移动速度")]
    public Transform playerTransform;
    /// <summary>中文含义：当前散布。当前实际显示的扩散值（平滑插值后的结果）</summary>
    public float CurrentSpread { get; private set; }
    /// <summary>中文含义：目标散布。当前目标扩散值</summary>
    public float TargetSpread { get; private set; }

    /// <summary>TopInitial 表示“顶部初始”；LeftInitial 表示“左侧初始”；BottomInitial 表示“底部初始”；RightInitial 表示“右侧初始”；CenterInitial 表示“中心初始”。</summary>
    private Vector2 TopInitial, LeftInitial, BottomInitial, RightInitial, CenterInitial;
    /// <summary>TopTarget 表示“顶部目标”；LeftTarget 表示“左侧目标”；BottomTarget 表示“底部目标”；RightTarget 表示“右侧目标”；CenterTarget 表示“中心目标”。</summary>
    private Vector2 TopTarget, LeftTarget, BottomTarget, RightTarget, CenterTarget;
    /// <summary>静止计时器。</summary>
    private float stationaryTimer;
    /// <summary>当前散布。</summary>
    private float currentSpread;
    /// <summary>目标散布。</summary>
    private float targetSpread;
    /// <summary>玩家刚体。</summary>
    private Rigidbody playerRb;
    /// <summary>是否具有中心。</summary>
    private bool hasCenter;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Rigidbody&gt;()、CrosshairPresenterValue.HideAllParts()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (Top == null || Left == null || Bottom == null || Right == null)
        {
            enabled = false;
            return;
        }

        TopInitial = Top.anchoredPosition;
        LeftInitial = Left.anchoredPosition;
        BottomInitial = Bottom.anchoredPosition;
        RightInitial = Right.anchoredPosition;
        hasCenter = Center != null;
        if (hasCenter)
        {
            CenterInitial = Center.anchoredPosition;
        }

        // 缓存玩家刚体引用
        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody>();
        }

        // 初始化为基础扩散值
        currentSpread = baseSpread;
        targetSpread = baseSpread;
        HideAllParts();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenterValue.HideAllParts()、CrosshairPresenterValue.ResetSpreadState()、CrosshairPresenterValue.ShowAllParts()、CrosshairPresenterValue.SmoothMove()、CrosshairPresenterValue.UpdateSpread()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        // 是否正在瞄准。
bool isAiming = LeonController.Instance != null && LeonController.Instance.IsAiming;
        if (isAiming)
        {
            ShowAllParts();
            UpdateSpread();
        }
        else
        {
            HideAllParts();
            ResetSpreadState();
        }

        SmoothMove();
    }

#region ===== 动态扩散计算 =====
    /// <summary>
    /// 根据玩家移动速度和静止时间，计算准心目标扩散值
    /// </summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.Update()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSpread()
    {
        // 获取玩家水平移动速度
        // 当前速度。
float currentSpeed = 0f;
        if (playerRb != null)
        {
            // 平面速度。
Vector3 flatVel = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z);
            currentSpeed = flatVel.magnitude;
        }

        // 速度比例。
float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(referenceMaxSpeed, 0.1f));
        if (currentSpeed > stationaryThreshold)
        {
            // 正在移动：清空静止计时，准心根据速度比例扩散
            stationaryTimer = 0f;
            targetSpread = Mathf.Lerp(baseSpread, maxSpread, speedRatio);
        }
        else
        {
            // 处于静止状态：累加计时器
            stationaryTimer += Time.deltaTime;
            if (stationaryTimer >= stationaryTime)
            {
                // 静止满指定秒数：准心收缩到最小尺寸
                targetSpread = baseSpread;
            }
        // 否则保持当前目标值，不主动变化（让视觉平滑过渡）
        }

        // 根据状态选择平滑速度：扩散快、收缩慢
        // 已使用的插值速度。
float usedLerpSpeed;
        if ((currentSpeed > stationaryThreshold))
        {
            usedLerpSpeed = spreadLerpSpeed;
        }
        else
        {
            usedLerpSpeed = shrinkLerpSpeed;
        }

        currentSpread = Mathf.Lerp(currentSpread, targetSpread, usedLerpSpeed * Time.deltaTime);
        // 防止极小误差累积
        if (Mathf.Abs(currentSpread - targetSpread) < 0.01f)
        {
            currentSpread = targetSpread;
        }

        TargetSpread = targetSpread;
        CurrentSpread = currentSpread;
        // 根据当前扩散值更新四个方向的部件目标位置
        TopTarget = new Vector2(TopInitial.x, TopInitial.y + currentSpread + margin);
        BottomTarget = new Vector2(BottomInitial.x, BottomInitial.y - currentSpread - margin);
        LeftTarget = new Vector2(LeftInitial.x - currentSpread - margin, LeftInitial.y);
        RightTarget = new Vector2(RightInitial.x + currentSpread + margin, RightInitial.y);
    }

    /// <summary>
    /// 退出瞄准时重置所有扩散状态
    /// </summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.Update()；下游（含内部回调体） CrosshairPresenterValue.ResetPosition()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResetSpreadState()
    {
        stationaryTimer = 0f;
        currentSpread = baseSpread;
        targetSpread = baseSpread;
        TargetSpread = baseSpread;
        CurrentSpread = baseSpread;
        ResetPosition();
    }

#endregion
#region ===== 部件显示/隐藏 =====
    /// <summary>ShowAllParts 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.Update()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ShowAllParts()
    {
        Top.gameObject.SetActive(true);
        Left.gameObject.SetActive(true);
        Bottom.gameObject.SetActive(true);
        Right.gameObject.SetActive(true);
        if (hasCenter)
        {
            Center.gameObject.SetActive(true);
        }
    }

    /// <summary>HideAllParts 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.Start()、CrosshairPresenterValue.Update()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HideAllParts()
    {
        Top.gameObject.SetActive(false);
        Left.gameObject.SetActive(false);
        Bottom.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        if (hasCenter)
        {
            Center.gameObject.SetActive(false);
        }
    }

#endregion
#region ===== 位置设置 =====
    /// <summary>ResetPosition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.ResetSpreadState()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResetPosition()
    {
        TopTarget = TopInitial;
        LeftTarget = LeftInitial;
        BottomTarget = BottomInitial;
        RightTarget = RightInitial;
        if (hasCenter)
        {
            CenterTarget = CenterInitial;
        }
    }

#endregion
#region ===== 平滑移动 =====
    /// <summary>SmoothMove 的业务处理入口；本方法直接依赖 Vector2.Lerp(Vector2, Vector2, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenterValue.Update()；下游（含内部回调体） Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void SmoothMove()
    {
        Top.anchoredPosition = Vector2.Lerp(Top.anchoredPosition, TopTarget, speed * Time.deltaTime);
        Left.anchoredPosition = Vector2.Lerp(Left.anchoredPosition, LeftTarget, speed * Time.deltaTime);
        Bottom.anchoredPosition = Vector2.Lerp(Bottom.anchoredPosition, BottomTarget, speed * Time.deltaTime);
        Right.anchoredPosition = Vector2.Lerp(Right.anchoredPosition, RightTarget, speed * Time.deltaTime);
        if (hasCenter)
        {
            Center.anchoredPosition = Vector2.Lerp(Center.anchoredPosition, CenterTarget, speed * Time.deltaTime);
        }
    }
#endregion
}
