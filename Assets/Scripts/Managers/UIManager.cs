using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器：统一管理所有UI显示、更新、弹窗、页面跳转逻辑
/// 接管原有散落在PlayerController中的UI更新代码，
/// 接管准心（准心.cs）的显示/隐藏与动态扩散逻辑
///
/// UI元素支持两种配置方式：
/// 1. Inspector直接拖拽赋值（优先）
/// 2. 运行时通过RegisterUIElements()注册（兼容旧场景）
/// </summary>
public class UIManager : Singleton<UIManager>
{
#region ===== UI元素引用 =====
    /// <summary>中文含义：生命值滑块。血条Slider组件（Inspector拖拽或运行时注册）。使用位置：UIManager.RefreshHPDisplay(float)、UIManager.RegisterUIElements(Slider, Text, Text, Image)。</summary>
    [Header("血条UI")]
    [Tooltip("血条Slider组件（Inspector拖拽或运行时注册）")]
    public Slider hpSlider;
    /// <summary>中文含义：血量数值文本。使用位置：UIManager.RefreshHPDisplay(float)、UIManager.RegisterUIElements(Slider, Text, Text, Image)。</summary>
    [Tooltip("血量数值文本")]
    public Text hpText;
    /// <summary>中文含义：血量渐变平滑速度，越小越慢（模拟生化危机式掉血效果）。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Header("血量渐变参数")]
    [Tooltip("血量渐变平滑速度，越小越慢（模拟生化危机式掉血效果）")]
    public float hpLerpSpeed = 2f;
    /// <summary>中文含义：弹药数量文本。使用位置：UIManager.RefreshAmmoDisplay(int, int)、UIManager.RegisterUIElements(Slider, Text, Text, Image)。</summary>
    [Header("弹药UI")]
    [Tooltip("弹药数量文本")]
    public Text ammoText;
    /// <summary>中文含义：武器图标Image组件。使用位置：UIManager.RefreshGunIcon(Sprite)、UIManager.RegisterUIElements(Slider, Text, Text, Image)。</summary>
    [Header("武器图标")]
    [Tooltip("武器图标Image组件")]
    public Image gunIcon;
#endregion
#region ===== 私有变量 =====
    /// <summary>中文含义：显示生命值。UI显示用的平滑血量（滞后于实际血量，实现渐变掉血效果） 迁移自 PlayerController.displayHp</summary>
    private float displayHp;
#endregion
#region ===== 初始化 =====
    /// <summary>实现基类的 Awake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Singleton&lt;UIManager&gt;.Awake()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void Awake()
    {
        base.Awake();
        // 默认满血显示值，后续由PlayerController调用InitHP覆盖
        displayHp = 100f;
    }

    /// <summary>
    /// 运行时注册UI元素引用（兼容旧场景中UI引用挂在PlayerController上的情况）
    /// 由PlayerController.Start()调用，将Inspector中分配的UI引用传递给UIManager
    /// 仅在UIManager自身字段为空时才会被覆盖（Inspector赋值优先）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hpSliderRef">中文含义：生命值滑块引用。使用位置：UIManager.RegisterUIElements(Slider, Text, Text, Image)。</param>
    /// <param name="hpTextRef">中文含义：生命值文字引用。使用位置：UIManager.RegisterUIElements(Slider, Text, Text, Image)。</param>
    /// <param name="ammoTextRef">中文含义：弹药文字引用。使用位置：UIManager.RegisterUIElements(Slider, Text, Text, Image)。</param>
    /// <param name="gunIconRef">中文含义：枪械图标引用。使用位置：UIManager.RegisterUIElements(Slider, Text, Text, Image)。</param>
    public void RegisterUIElements(Slider hpSliderRef, Text hpTextRef, Text ammoTextRef, Image gunIconRef)
    {
        if (hpSlider == null)
        {
            hpSlider = hpSliderRef;
        }

        if (hpText == null)
        {
            hpText = hpTextRef;
        }

        if (ammoText == null)
        {
            ammoText = ammoTextRef;
        }

        if (gunIcon == null)
        {
            gunIcon = gunIconRef;
        }
    }

#endregion
#region ===== 血量显示（迁移自 PlayerController.UpdateHpSmooth / UpdateUI） =====
    /// <summary>
    /// 初始化血量显示为满血状态
    /// 在游戏开始时由PlayerController调用
    /// </summary>
    /// <param name="maxHp">中文含义：最大生命值。最大血量</param>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） UIManager.RefreshHPDisplay(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InitHP(float maxHp)
    {
        displayHp = maxHp;
        RefreshHPDisplay(maxHp);
    }

    /// <summary>
    /// 每帧更新血量显示（含渐变掉血效果）
    /// 将显示血量平滑逼近实际血量，实现生化危机式缓慢掉血条动画
    /// 迁移自 PlayerController.UpdateHpSmooth()
    /// 每帧由PlayerController.Update()调用
    /// </summary>
    /// <param name="currentHp">中文含义：当前生命值。玩家当前实际血量</param>
    /// <param name="maxHp">中文含义：最大生命值。玩家最大血量</param>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.Lerp(float, float, float)、UIManager.RefreshHPDisplay(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void UpdateHPDisplay(float currentHp, float maxHp)
    {
        // 如果显示血量已经等于实际血量，无需更新
        if (Mathf.Approximately(displayHp, currentHp))
        {
            return;
        }

        // 显示血量平滑逼近实际血量（双向支持：掉血和回血）
        displayHp = Mathf.Lerp(displayHp, currentHp, hpLerpSpeed * Time.deltaTime);
        // 接近目标值时直接到位，避免无限逼近
        if (Mathf.Abs(displayHp - currentHp) < 0.1f)
        {
            displayHp = currentHp;
        }

        // 刷新血条与文字
        RefreshHPDisplay(maxHp);
    }

    /// <summary>
    /// 刷新血条Slider和血量文本到屏幕
    /// </summary>
    /// <remarks>静态调用链：上游 UIManager.InitHP(float)、UIManager.UpdateHPDisplay(float, float)；下游（含内部回调体） Mathf.CeilToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="maxHp">中文含义：最大生命值。使用位置：UIManager.RefreshHPDisplay(float)。</param>
    private void RefreshHPDisplay(float maxHp)
    {
        if (hpSlider != null)
        {
            hpSlider.value = displayHp / maxHp;
        }

        if (hpText != null)
        {
            hpText.text = $"{Mathf.CeilToInt(displayHp)}/{maxHp}";
        }
    }

#endregion
#region ===== 弹药显示（迁移自 PlayerController.UpdateUI） =====
    /// <summary>
    /// 刷新弹药数字显示
    /// 迁移自 PlayerController.UpdateUI() 中 ammoText.text 赋值逻辑
    /// </summary>
    /// <param name="currentAmmo">中文含义：当前弹匣弹量。当前弹药数</param>
    /// <param name="maxAmmo">中文含义：最大弹量。最大弹药数</param>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RefreshAmmoDisplay(int currentAmmo, int maxAmmo)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{currentAmmo}/{maxAmmo}";
        }
    }

#endregion
#region ===== 武器图标（迁移自 PlayerController.UpdateUI） =====
    /// <summary>
    /// 刷新武器图标显示
    /// 迁移自 PlayerController.UpdateUI() 中 gunIcon.sprite / gunIcon.enabled 赋值逻辑
    /// </summary>
    /// <param name="sprite">中文含义：精灵图。武器图标Sprite</param>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RefreshGunIcon(Sprite sprite)
    {
        if (gunIcon != null)
        {
            gunIcon.sprite = sprite;
            gunIcon.enabled = true;
        }
    }
#endregion
}
