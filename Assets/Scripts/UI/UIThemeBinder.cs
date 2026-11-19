using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将视觉主题中的图片应用到场景对象。
/// 一个绑定器可以服务整个画布，也可以只服务单个面板。
/// </summary>
public class UIThemeBinder : MonoBehaviour
{
    /// <summary>界面交互与显示模块 ImageBinding；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    public sealed class ImageBinding
    {
        /// <summary>中文含义：目标图像组件。使用位置：UIThemeBinder.ApplyImages()。</summary>
        public Image target;
        /// <summary>中文含义：角色职责。使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数。</summary>
        public UIVisualRole role;
        /// <summary>中文含义：是否应用图片后将图片组件颜色重置为白色。应用图片后将图片组件颜色重置为白色。使用位置：UIThemeBinder.ApplyImages()。</summary>
        [Tooltip("应用图片后将图片组件颜色重置为白色")]
        public bool resetColor = true;
        /// <summary>中文含义：是否应用图片后使用图片原始尺寸。应用图片后使用图片原始尺寸。使用位置：UIThemeBinder.ApplyImages()。</summary>
        [Tooltip("应用图片后使用图片原始尺寸")]
        public bool useNativeSize;
    }

    /// <summary>界面交互与显示模块 SelectableBinding；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    public sealed class SelectableBinding
    {
        /// <summary>中文含义：目标可选择控件。使用位置：UIThemeBinder.ApplySelectables()。</summary>
        public Selectable target;
        /// <summary>中文含义：普通状态值。使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数。</summary>
        public UIVisualRole normal = UIVisualRole.ButtonNormal;
        /// <summary>中文含义：已高亮。使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数。</summary>
        public UIVisualRole highlighted = UIVisualRole.ButtonHighlighted;
        /// <summary>中文含义：已按下。使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数。</summary>
        public UIVisualRole pressed = UIVisualRole.ButtonPressed;
        /// <summary>中文含义：停用。使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数。</summary>
        public UIVisualRole disabled = UIVisualRole.ButtonDisabled;
    }

    /// <summary>中文含义：主题。使用位置：OptionsMenu.EnsureEnhancedMenu()、UIThemeBinder.ApplyCursor()、UIThemeBinder.ApplyImages() 等共 5 处。</summary>
    [Header("主题")]
    public UIVisualTheme theme;
    /// <summary>中文含义：是否组件启用时自动应用主题。组件启用时自动应用主题。使用位置：UIThemeBinder.OnEnable()。</summary>
    [Tooltip("组件启用时自动应用主题")]
    public bool applyOnEnable = true;
    /// <summary>中文含义：图像集合。使用位置：UIThemeBinder.ApplyImages()。</summary>
    [Header("图片绑定")]
    public ImageBinding[] images;
    /// <summary>中文含义：可选择控件集合。使用位置：UIThemeBinder.ApplySelectables()。</summary>
    [Header("可交互控件绑定")]
    public SelectableBinding[] selectables;
    /// <summary>中文含义：光标管理器。使用位置：UIThemeBinder.ApplyCursor()。</summary>
    [Header("可选光标管理器")]
    public UICursorManager cursorManager;
    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） UIThemeBinder.ApplyTheme()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyTheme();
        }
    }

    /// <summary>ApplyTheme 的业务处理入口；本方法直接依赖 UIThemeBinder.ApplyCursor()、UIThemeBinder.ApplyImages()、UIThemeBinder.ApplySelectables()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UIThemeBinder.OnEnable()；下游（含内部回调体） UIThemeBinder.ApplyCursor()、UIThemeBinder.ApplyImages()、UIThemeBinder.ApplySelectables()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("应用界面主题")]
    public void ApplyTheme()
    {
        if (theme == null)
        {
            return;
        }

        ApplyImages();
        ApplySelectables();
        ApplyCursor();
    }

    /// <summary>ApplyImages 的业务处理入口；本方法直接依赖 Image.SetNativeSize()、UIVisualTheme.GetSprite(UIVisualRole)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UIThemeBinder.ApplyTheme()；下游（含内部回调体） Image.SetNativeSize()、UIVisualTheme.GetSprite(UIVisualRole)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyImages()
    {
        if (images == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：UIThemeBinder.ApplyImages()。
for (int i = 0; i < images.Length; i++)
        {
            // binding（绑定）：使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数、UIThemeBinder.ApplyImages()。
ImageBinding binding = images[i];
            if (binding == null || binding.target == null)
            {
                continue;
            }

            // sprite（精灵图）：保存 UIVisualTheme.GetSprite(UIVisualRole) 的返回结果，供当前步骤使用。使用位置：UIThemeBinder.ApplyImages()。
Sprite sprite = theme.GetSprite(binding.role);
            if (sprite == null)
            {
                continue;
            }

            binding.target.sprite = sprite;
            if (binding.resetColor)
            {
                binding.target.color = Color.white;
            }

            if (binding.useNativeSize)
            {
                binding.target.SetNativeSize();
            }
        }
    }

    /// <summary>ApplySelectables 的业务处理入口；本方法直接依赖 UIVisualTheme.GetSprite(UIVisualRole)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UIThemeBinder.ApplyTheme()；下游（含内部回调体） UIVisualTheme.GetSprite(UIVisualRole)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplySelectables()
    {
        if (selectables == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：UIThemeBinder.ApplySelectables()。
for (int i = 0; i < selectables.Length; i++)
        {
            // binding（绑定）：使用位置：作为 UIVisualTheme.GetSprite(UIVisualRole) 的参数、UIThemeBinder.ApplySelectables()。
SelectableBinding binding = selectables[i];
            if (binding == null || binding.target == null)
            {
                continue;
            }

            // image（图像）：使用位置：UIThemeBinder.ApplySelectables()。
Image image = binding.target.targetGraphic as Image;
            // normal（普通状态值）：保存 UIVisualTheme.GetSprite(UIVisualRole) 的返回结果，供当前步骤使用。使用位置：UIThemeBinder.ApplySelectables()。
Sprite normal = theme.GetSprite(binding.normal);
            if (image != null && normal != null)
            {
                image.sprite = normal;
            }

            // state（当前精灵图状态）：使用位置：UIThemeBinder.ApplySelectables()。
SpriteState state = binding.target.spriteState;
            state.highlightedSprite = theme.GetSprite(binding.highlighted);
            state.pressedSprite = theme.GetSprite(binding.pressed);
            state.disabledSprite = theme.GetSprite(binding.disabled);
            binding.target.spriteState = state;
            binding.target.transition = Selectable.Transition.SpriteSwap;
        }
    }

    /// <summary>ApplyCursor 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 UIThemeBinder.ApplyTheme()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyCursor()
    {
        if (cursorManager == null)
        {
            return;
        }

        if (theme.cursorDefault != null)
        {
            cursorManager.defaultCursorTex = theme.cursorDefault;
        }

        if (theme.cursorHighlighted != null)
        {
            cursorManager.pressedCursorTex = theme.cursorHighlighted;
        }
    }
}
