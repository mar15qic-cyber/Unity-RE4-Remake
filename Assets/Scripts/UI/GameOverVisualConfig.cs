using UnityEngine;

/// <summary>
/// 死亡结算界面的集中视觉配置。
/// 美术资源可以直接在检视面板中替换，死亡流程脚本不依赖资源硬编码。
/// </summary>
[CreateAssetMenu(fileName = "死亡结算视觉配置", menuName = "游戏界面/死亡结算视觉配置")]
public sealed class GameOverVisualConfig : ScriptableObject
{
    /// <summary>标题字体。</summary>
    [Header("字体")]
    public Font titleFont;
    /// <summary>接口字体。</summary>
    public Font interfaceFont;
    /// <summary>按钮精灵图。</summary>
    [Header("按钮")]
    public Sprite buttonSprite;
}
