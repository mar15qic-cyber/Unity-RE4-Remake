using UnityEngine;

/// <summary>
/// 集中保存界面视觉素材。
/// 创建资源后可在检视面板中直接拖入图片，逻辑脚本无需依赖具体美术文件路径。
/// </summary>
[CreateAssetMenu(fileName = "界面视觉主题", menuName = "游戏界面/视觉主题")]
public class UIVisualTheme : ScriptableObject
{
    [Header("原版菜单素材")]
    public Sprite originalSelectionBand;
    public Material originalSelectionMaterial;
    public Material originalBackdropMaterial;
    public Sprite originalConfirmKey;
    public Sprite originalBackKey;
    /// <summary>中文含义：接口字体。使用位置：AttacheCasePresentationController.CreateCountText(GridItem)、InterfaceReworkBuilder.ConfigurationTitle()、InterfaceReworkBuilder.GenerateAsset() 等共 10 处。</summary>
    [Header("统一菜单排版")]
    public Font interfaceFont;
    /// <summary>中文含义：菜单文字。使用位置：作为 Color.Color(float, float, float, float) 的参数、InterfaceReworkBuilder.ConfigurationTitle()、InterfaceReworkBuilder.GenerateAsset() 等共 8 处。</summary>
    public Color menuText = new Color(0.88f, 0.86f, 0.80f, 1f);
    /// <summary>中文含义：菜单静音的。使用位置：PauseMenu.BuildInterface()、TitleScreenManager.Start()。</summary>
    public Color menuMuted = new Color(0.51f, 0.53f, 0.50f, 1f);
    /// <summary>中文含义：菜单面板。使用位置：OptionsTabManager.Initialize()。</summary>
    public Color menuPanel = new Color(0.027f, 0.032f, 0.030f, 0.88f);
    /// <summary>中文含义：菜单选择。使用位置：InterfaceReworkBuilder.ConfigurationInventory()、OptionsTabManager.Initialize()。</summary>
    public Color menuSelection = new Color(0.55f, 0.59f, 0.53f, 0.22f);
    /// <summary>中文含义：菜单线。使用位置：作为 InterfaceReworkBuilder.NewImage(Transform, string, Color) 的参数、作为 RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color) 的参数、InterfaceReworkBuilder.GenerateAsset() 等共 5 处。</summary>
    public Color menuLine = new Color(0.64f, 0.65f, 0.59f, 0.3f);
    /// <summary>中文含义：菜单过渡。使用位置：MenuThemeApplier.Apply(Transform, UIVisualTheme)。</summary>
    public float menuTransition = 0.2f;
    /// <summary>中文含义：行高度。</summary>
    public float rowHeight = 58f;
    /// <summary>中文含义：战斗战斗显示界面帧。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    [Header("战斗界面")]
    public Sprite combatHudFrame;
    /// <summary>中文含义：生命帧。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite healthFrame;
    /// <summary>中文含义：生命填充。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite healthFill;
    /// <summary>中文含义：弹药帧。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite ammoFrame;
    /// <summary>中文含义：武器帧。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite weaponFrame;
    /// <summary>中文含义：准星。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite crosshair;
    /// <summary>中文含义：伤害暗角。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite damageVignette;
    /// <summary>中文含义：暂停背景。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    [Header("通用面板")]
    public Sprite pauseBackground;
    /// <summary>中文含义：面板背景。使用位置：OptionsMenu.EnsureEnhancedMenu()、UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite panelBackground;
    /// <summary>中文含义：对话框背景。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite dialogBackground;
    /// <summary>中文含义：选择指示器。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite selectionIndicator;
    /// <summary>中文含义：按钮常规。使用位置：InventoryUI.ApplyActionButtonStyle(Button)、OptionsMenu.EnsureEnhancedMenu()、UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    [Header("按钮")]
    public Sprite buttonNormal;
    /// <summary>中文含义：按钮已高亮。使用位置：InventoryUI.ApplyActionButtonStyle(Button)、OptionsMenu.EnsureEnhancedMenu()、UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite buttonHighlighted;
    /// <summary>中文含义：按钮已按下。使用位置：InventoryUI.ApplyActionButtonStyle(Button)、UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite buttonPressed;
    /// <summary>中文含义：按钮停用。使用位置：InventoryUI.ApplyActionButtonStyle(Button)、UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite buttonDisabled;
    /// <summary>中文含义：背包背景。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    [Header("背包与存储")]
    public Sprite inventoryBackground;
    /// <summary>中文含义：背包槽位。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite inventorySlot;
    /// <summary>中文含义：背包槽位选定。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite inventorySlotSelected;
    /// <summary>中文含义：仓库背景。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</summary>
    public Sprite storageBackground;
    /// <summary>中文含义：光标默认。使用位置：UIThemeBinder.ApplyCursor()。</summary>
    [Header("光标")]
    public Texture2D cursorDefault;
    /// <summary>中文含义：光标已高亮。使用位置：UIThemeBinder.ApplyCursor()。</summary>
    public Texture2D cursorHighlighted;
    /// <summary>中文含义：主要颜色。使用位置：InterfaceReworkBuilder.GenerateAsset()。</summary>
    [Header("主题颜色")]
    public Color primaryColor = new Color(0.72f, 0.78f, 0.72f, 1f);
    /// <summary>中文含义：强调色颜色。使用位置：InterfaceReworkBuilder.GenerateAsset()。</summary>
    public Color accentColor = new Color(0.52f, 0.08f, 0.06f, 1f);
    /// <summary>中文含义：禁用状态颜色。</summary>
    public Color disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
    /// <summary>GetSprite 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 UIThemeBinder.ApplyImages()、UIThemeBinder.ApplySelectables()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="role">中文含义：角色职责。使用位置：UIVisualTheme.GetSprite(UIVisualRole)。</param>
    public Sprite GetSprite(UIVisualRole role)
    {
        switch (role)
        {
            case UIVisualRole.CombatHudFrame:
                return combatHudFrame;
            case UIVisualRole.HealthFrame:
                return healthFrame;
            case UIVisualRole.HealthFill:
                return healthFill;
            case UIVisualRole.AmmoFrame:
                return ammoFrame;
            case UIVisualRole.WeaponFrame:
                return weaponFrame;
            case UIVisualRole.Crosshair:
                return crosshair;
            case UIVisualRole.DamageVignette:
                return damageVignette;
            case UIVisualRole.PauseBackground:
                return pauseBackground;
            case UIVisualRole.PanelBackground:
                return panelBackground;
            case UIVisualRole.DialogBackground:
                return dialogBackground;
            case UIVisualRole.SelectionIndicator:
                return selectionIndicator;
            case UIVisualRole.ButtonNormal:
                return buttonNormal;
            case UIVisualRole.ButtonHighlighted:
                return buttonHighlighted;
            case UIVisualRole.ButtonPressed:
                return buttonPressed;
            case UIVisualRole.ButtonDisabled:
                return buttonDisabled;
            case UIVisualRole.InventoryBackground:
                return inventoryBackground;
            case UIVisualRole.InventorySlot:
                return inventorySlot;
            case UIVisualRole.InventorySlotSelected:
                return inventorySlotSelected;
            case UIVisualRole.StorageBackground:
                return storageBackground;
            default:
                return null;
        }
    }
}

/// <summary>界面交互与显示使用的 UIVisualRole 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum UIVisualRole
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：战斗战斗显示界面帧。</summary>
    CombatHudFrame,
    /// <summary>中文含义：生命帧。</summary>
    HealthFrame,
    /// <summary>中文含义：生命填充。</summary>
    HealthFill,
    /// <summary>中文含义：弹药帧。</summary>
    AmmoFrame,
    /// <summary>中文含义：武器帧。</summary>
    WeaponFrame,
    /// <summary>中文含义：准星。</summary>
    Crosshair,
    /// <summary>中文含义：伤害暗角。</summary>
    DamageVignette,
    /// <summary>中文含义：暂停背景。</summary>
    PauseBackground,
    /// <summary>中文含义：面板背景。</summary>
    PanelBackground,
    /// <summary>中文含义：对话框背景。</summary>
    DialogBackground,
    /// <summary>中文含义：选择指示器。</summary>
    SelectionIndicator,
    /// <summary>中文含义：按钮常规。</summary>
    ButtonNormal,
    /// <summary>中文含义：按钮已高亮。</summary>
    ButtonHighlighted,
    /// <summary>中文含义：按钮已按下。</summary>
    ButtonPressed,
    /// <summary>中文含义：按钮停用。</summary>
    ButtonDisabled,
    /// <summary>中文含义：背包背景。</summary>
    InventoryBackground,
    /// <summary>中文含义：背包槽位。</summary>
    InventorySlot,
    /// <summary>中文含义：背包槽位选定。</summary>
    InventorySlotSelected,
    /// <summary>中文含义：仓库背景。</summary>
    StorageBackground
}
