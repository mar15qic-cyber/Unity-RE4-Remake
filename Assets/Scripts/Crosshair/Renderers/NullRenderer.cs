using UnityEngine;

/// <summary>
/// 空准心渲染器 — None 策略 (Knife)
/// 所有方法为空操作，不渲染任何 UI
/// </summary>
public class NullRenderer : MonoBehaviour, ICrosshairRenderer
{
    /// <summary>ApplySpread 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="normalizedSpread">中文含义：归一化散布。</param>
    /// <param name="uiSize">中文含义：界面尺寸。</param>
    public void ApplySpread(float normalizedSpread, float uiSize)
    {
    // 无操作 — 小刀不显示准心
    }

    /// <summary>ApplyColor 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseColor">中文含义：基础颜色。</param>
    /// <param name="hitColor">中文含义：命中颜色。</param>
    /// <param name="hitBlend">中文含义：命中混合。</param>
    public void ApplyColor(Color baseColor, Color hitColor, float hitBlend)
    {
    // 无操作
    }

    /// <summary>ApplyAlpha 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="alpha">中文含义：透明度。</param>
    public void ApplyAlpha(float alpha)
    {
    // 无操作
    }

    /// <summary>OnActivate 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnActivate()
    {
    // 无操作 — 不显示任何内容
    }

    /// <summary>OnDeactivate 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnDeactivate()
    {
    // 无操作
    }
}
