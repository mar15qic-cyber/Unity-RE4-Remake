using UnityEngine;

/// <summary>
/// 死亡结算界面的集中视觉配置。
/// 美术资源可以直接在检视面板中替换，死亡流程脚本不依赖资源硬编码。
/// </summary>
[CreateAssetMenu(fileName = "死亡结算视觉配置", menuName = "游戏界面/死亡结算视觉配置")]
public sealed class GameOverVisualConfig : ScriptableObject
{
    /// <summary>中文含义：标题字体。使用位置：GameOverScreen.ResolveVisualAssets()。</summary>
    [Header("字体")]
    public Font titleFont;
    /// <summary>中文含义：接口字体。使用位置：GameOverScreen.ResolveVisualAssets()。</summary>
    public Font interfaceFont;
    /// <summary>中文含义：按钮精灵图。使用位置：GameOverScreen.ResolveVisualAssets()。</summary>
    [Header("按钮")]
    public Sprite buttonSprite;
}
