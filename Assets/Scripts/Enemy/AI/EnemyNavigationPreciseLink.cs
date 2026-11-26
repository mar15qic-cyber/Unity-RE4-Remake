using UnityEngine;

/// <summary>
/// 标记必须精确经过端点、沿中心线通过的窄通路。
/// 普通宽平台连接继续使用寻路代理原有的到达容差。
/// </summary>
[DisallowMultipleComponent]
public class EnemyNavigationPreciseLink : MonoBehaviour
{
    /// <summary>连接。</summary>
    [Header("窄通路导航连接")]
    public Pathfinding.NodeLink2 link;
}
