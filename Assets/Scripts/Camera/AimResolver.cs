using UnityEngine;

/// <summary>
/// 正式摄像机和武器共用的瞄准解算入口。RE4CameraController 配置镜头与目标，并按武器射程调用 Resolve；
/// 解算器保存本次命中快照并更新世界瞄准目标，其他模块读取同一结果，避免各自计算不同瞄准点。
/// </summary>
[DisallowMultipleComponent]
public sealed class AimResolver : MonoBehaviour
{
    /// <summary>中文含义：主摄像机。发射视口射线的主相机，由正式摄像机入口绑定。</summary>
    [Header("引用")]
    public Camera mainCamera;
    /// <summary>中文含义：世界空间瞄准目标。解算成功后更新的世界目标节点，供角色瞄准与武器读取。</summary>
    public Transform worldAimTarget;
    /// <summary>中文含义：瞄准遮罩。世界瞄准检测层，射线忽略触发器。</summary>
    [Header("检测")]
    public LayerMask aimMask;
    /// <summary>中文含义：视口点。射线起始视口位置，默认屏幕中心。</summary>
    public Vector2 viewportPoint = new Vector2(0.5f, 0.5f);
    /// <summary>中文含义：后备距离。调用方未提供正射程或射程非有限值时沿用的距离配置。</summary>
    [Min(1f)]
    public float fallbackDistance = 100f;
    /// <summary>中文含义：当前瞄准点。最近成功解算提交的世界瞄准点；失败分支仅返回安全点，不额外重写此属性。</summary>
    private readonly HitQueryBuffer aimQueries = new HitQueryBuffer();

    public Vector3 CurrentAimPoint { get; private set; }
    /// <summary>中文含义：是否具有世界空间命中。最近一次解算是否命中世界碰撞体；配置或数值检查失败时清为 false。</summary>
    public bool HasWorldHit { get; private set; }
    /// <summary>中文含义：当前命中。当前解算命中信息；未命中或解算失败时恢复默认值。</summary>
    public RaycastHit CurrentHit { get; private set; }
    /// <summary>中文含义：是否配置完成。主相机和世界目标均有效时才允许执行射线和目标位姿提交。</summary>
    public bool IsConfigured => mainCamera != null && worldAimTarget != null;

    /// <summary>RE4CameraController.Start 调用，显式传入当前场景绑定，不自行查找或创建节点。</summary>
    /// <param name="cameraReference">中文含义：摄像机引用。发射视口射线的主相机。</param>
    /// <param name="targetReference">中文含义：目标引用。接收世界瞄准结果的节点。</param>
    public void Configure(Camera cameraReference, Transform targetReference)
    {
        mainCamera = cameraReference;
        worldAimTarget = targetReference;
    }

    /// <summary>摄像机更新或武器查询调用；依次校验配置、相机数值和射线，射线命中或延伸点有效后统一提交结果。</summary>
    /// <param name="maximumDistance">中文含义：最大距离。当前武器射程；非正值使用回退距离。</param>
    /// <returns>成功时返回本次提交点，失败时返回既有安全点。</returns>
    public Vector3 Resolve(float maximumDistance)
    {
        if (!IsConfigured)
        {
            HasWorldHit = false;
            CurrentHit = default;
            return GetSafeAimPoint();
        }

        if (!IsFinite(mainCamera.transform.position) || !IsFinite(mainCamera.transform.rotation) || !IsFinite(mainCamera.projectionMatrix) || !IsFinite(mainCamera.worldToCameraMatrix))
        {
            HasWorldHit = false;
            CurrentHit = default;
            return GetSafeAimPoint();
        }

        // 本次实际检测距离，保持先按正值选择、再做有限值检查的原顺序。
        // 中文含义：distance 为“距离”。
float distance;
        if (maximumDistance > 0f)
        {
            distance = maximumDistance;
        }
        else
        {
            distance = fallbackDistance;
        }

        if (!IsFinite(distance))
        {
            distance = fallbackDistance;
        }

        // 从配置视口点发出的世界射线；无效或接近零的方向拒绝进入物理检测。
        // 中文含义：ray 为“射线”。
Ray ray = mainCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
        if (!IsFinite(ray.origin) || !IsFinite(ray.direction) || ray.direction.sqrMagnitude < 0.0001f)
        {
            HasWorldHit = false;
            CurrentHit = default;
            return GetSafeAimPoint();
        }

        // hit 接收本次物理命中，只有 HasWorldHit 为 true 才读取它作为世界目标。
        RaycastHit[] hits = aimQueries.Raycast(ray.origin, ray.direction, distance, aimMask, QueryTriggerInteraction.Collide, out int hitCount);
        HasWorldHit = WeaponBase.TrySelectCombatHit(hits, hitCount, out RaycastHit hit);
        if (HasWorldHit)
        {
            CurrentHit = hit;
        }
        else
        {
            CurrentHit = default;
        }

        if (HasWorldHit)
        {
            CurrentAimPoint = hit.point;
        }
        else
        {
            CurrentAimPoint = ray.origin + ray.direction * distance;
        }

        if (!IsFinite(CurrentAimPoint))
        {
            HasWorldHit = false;
            CurrentHit = default;
            return GetSafeAimPoint();
        }

        worldAimTarget.position = CurrentAimPoint;
        worldAimTarget.rotation = Quaternion.LookRotation(ray.direction, Vector3.up);
        return CurrentAimPoint;
    }

    /// <summary>Resolve 的失败分支共用；依次使用有限的已存点、世界目标、主相机前方点或原点，不提交目标位姿。</summary>
    /// <returns>按既有回退优先级选出的安全位置。</returns>
    private Vector3 GetSafeAimPoint()
    {
        if (IsFinite(CurrentAimPoint))
        {
            return CurrentAimPoint;
        }

        if (worldAimTarget != null && IsFinite(worldAimTarget.position))
        {
            return worldAimTarget.position;
        }

        if (mainCamera != null && IsFinite(mainCamera.transform.position))
        {
            return mainCamera.transform.position + Vector3.forward * fallbackDistance;
        }

        return Vector3.zero;
    }

    /// <summary>所有相机、射程和分量检查共用的标量有限值判断。</summary>
    /// <param name="value">中文含义：值。待检查标量。</param>
    /// <returns>既不是 NaN 也不是无穷大时返回 true。</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>相机位置、射线和目标检查共用，按原短路顺序核对向量三个分量。</summary>
    /// <param name="value">中文含义：值。待检查向量。</param>
    /// <returns>所有分量均有限时返回 true。</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>相机旋转检查调用，只核对四个分量是否有限，不改变四元数归一化状态。</summary>
    /// <param name="value">中文含义：值。待检查旋转。</param>
    /// <returns>四个分量均有限时返回 true。</returns>
    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    /// <summary>相机投影与视图矩阵检查调用，逐元素校验，首次遇到非有限值立即返回失败。</summary>
    /// <param name="value">中文含义：当前矩阵4横向4。待检查矩阵。</param>
    /// <returns>十六个元素均有限时返回 true。</returns>
    private static bool IsFinite(Matrix4x4 value)
    {
        // 当前矩阵行索引，按原有从零到三的顺序检查。
        // 中文含义：row 为“行”。
for (int row = 0; row < 4; row++)
        {
            // 当前行内列索引，按原有从零到三的顺序检查。
            // 中文含义：column 为“列”。
for (int column = 0; column < 4; column++)
            {
                if (!IsFinite(value[row, column]))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
