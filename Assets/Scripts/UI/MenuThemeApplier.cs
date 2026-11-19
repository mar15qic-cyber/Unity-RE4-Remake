using UnityEngine;
using UnityEngine.UI;

/// <summary>菜单公用的字体、细线和交互颜色，不接管任何业务逻辑。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "菜单主题应用")]
public static class MenuThemeApplier
{
    /// <summary>Apply 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponentsInChildren&lt;Selectable&gt;(bool)、Component.GetComponentsInChildren&lt;Text&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface()、InventoryUI.OnSceneLoaded()、OptionsTabManager.Initialize()、PauseMenu.BuildInterface() 等共 8 处；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponentsInChildren&lt;Selectable&gt;(bool)、Component.GetComponentsInChildren&lt;Text&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根变换节点。使用位置：MenuThemeApplier.Apply(Transform, UIVisualTheme)。</param>
    /// <param name="theme">中文含义：主题。使用位置：作为 Color.Color(float, float, float, float) 的参数、MenuThemeApplier.Apply(Transform, UIVisualTheme)。</param>
    public static void Apply(Transform root, UIVisualTheme theme)
    {
        if (root == null || theme == null)
        {
            return;
        }

        // text（文字）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Color.Color(float, float, float, float) 的参数、MenuThemeApplier.Apply(Transform, UIVisualTheme)。
foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (theme.interfaceFont != null)
            {
                text.font = theme.interfaceFont;
            }

            text.color = new Color(theme.menuText.r, theme.menuText.g, theme.menuText.b, text.color.a);
            text.lineSpacing = 1.15f;
        }

        // control（控制）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：MenuThemeApplier.Apply(Transform, UIVisualTheme)。
foreach (Selectable control in root.GetComponentsInChildren<Selectable>(true))
        {
            // colors（颜色集合）：使用位置：MenuThemeApplier.Apply(Transform, UIVisualTheme)。
var colors = control.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.13f, 1.06f, 1f);
            colors.selectedColor = new Color(1.15f, 1.13f, 1.06f, 1f);
            colors.pressedColor = new Color(0.64f, 0.66f, 0.6f, 1f);
            colors.disabledColor = new Color(0.38f, 0.4f, 0.36f, 0.45f);
            colors.fadeDuration = theme.menuTransition;
            control.colors = colors;
            control.transition = Selectable.Transition.ColorTint;
        }
    }
}
