using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>摄像机与瞄准组件 QTECameraRig，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
public sealed class QTECameraRig : MonoBehaviour
{
    /// <summary>中文含义：配对根节点。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、QTECameraPresenter.BeginResult(bool) 等共 11 处。</summary>
    [Header("运行结构")]
    public Transform pairRoot;
    /// <summary>中文含义：焦点目标。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float, float, float) 的参数、QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool) 等共 8 处。</summary>
    public Transform focusTarget;
    /// <summary>中文含义：结果偏移量根节点。使用位置：QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.ReleaseCamera() 等共 7 处。</summary>
    public Transform resultOffsetRoot;
    /// <summary>中文含义：主要路径。使用位置：作为 QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float) 的参数、作为 QTECameraRig.SelectPath(SplineContainer) 的参数、作为 Transform.TransformPoint(Vector3) 的参数 等共 7 处。</summary>
    [Header("可视轨迹")]
    public SplineContainer primaryPath;
    /// <summary>中文含义：后备路径。使用位置：作为 QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float) 的参数、QTECameraPresenter.BeginEntry()、QTECameraPresenter.GetPreferredPath(QTECameraRig, QTECameraProfile) 等共 4 处。</summary>
    public SplineContainer fallbackPath;
    /// <summary>中文含义：配对动作摄像机。使用位置：作为 RE4CameraController.RequestExternalCamera(Object, CinemachineCamera, bool) 的参数、QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool) 等共 12 处。</summary>
    [Header("电影机组件")]
    public CinemachineCamera qteCamera;
    /// <summary>中文含义：样条轨道镜头。使用位置：QTECameraRig.IsConfigured.get、QTECameraRig.Prepare(float)、QTECameraRig.ResetCameraFeedback() 等共 7 处。</summary>
    public CinemachineSplineDolly splineDolly;
    /// <summary>中文含义：旋转构图器。使用位置：QTECameraRig.IsConfigured.get。</summary>
    public CinemachineRotationComposer rotationComposer;
    /// <summary>中文含义：遮挡解除器。使用位置：QTECameraRig.IsConfigured.get、QTECameraRig.Prepare(float)。</summary>
    public CinemachineDeoccluder deoccluder;
    /// <summary>中文含义：选定路径。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 SplineUtility.GetNormalizedInterpolation&lt;Spline&gt;(Spline, float, PathIndexUnit) 的参数、QTECameraRig.GetNormalizedPositionAtKnot(int) 等共 4 处。</summary>
    public SplineContainer SelectedPath { get; private set; }

    /// <summary>中文含义：样条位置。使用位置：QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HandleAssassinationContact(int)、QTECameraPresenter.HandleBiteContact(int)。</summary>
    public float SplinePosition
    {
        get
        {
            if (splineDolly != null)
            {
                return splineDolly.CameraPosition;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：是否配置完成。使用位置：QTECameraPresenter.HandleSessionStarted(int, QTEKind)。</summary>
    public bool IsConfigured => pairRoot != null && focusTarget != null && resultOffsetRoot != null && primaryPath != null && fallbackPath != null && qteCamera != null && splineDolly != null && rotationComposer != null && deoccluder != null;

    /// <summary>Prepare 的业务处理入口；本方法直接依赖 QTECameraRig.ResetCameraFeedback()、QTECameraRig.SelectPath(SplineContainer)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandleSessionStarted(int, QTEKind)；下游（含内部回调体） QTECameraRig.ResetCameraFeedback()、QTECameraRig.SelectPath(SplineContainer)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="initialFieldOfView">中文含义：初始视野角度。使用位置：QTECameraRig.Prepare(float)。</param>
    public void Prepare(float initialFieldOfView)
    {
        resultOffsetRoot.localPosition = Vector3.zero;
        resultOffsetRoot.localRotation = Quaternion.identity;
        SelectPath(primaryPath);
        // 轨道在接管镜头前已经完成环境碰撞评分，禁止再次实时推镜。
        // 近距离抓取时，实时推镜可能把镜头推到观察点并产生零长度方向，
        // 继而污染摄像机矩阵并触发大量非有限数值断言。
        if (deoccluder != null)
        {
            deoccluder.enabled = false;
        }

        splineDolly.PositionUnits = PathIndexUnit.Normalized;
        splineDolly.CameraPosition = 0f;
        qteCamera.LookAt = focusTarget;
        qteCamera.Lens.FieldOfView = initialFieldOfView;
        qteCamera.Priority = 0;
        ResetCameraFeedback();
        qteCamera.enabled = false;
    }

    /// <summary>SelectPath 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()、QTECameraRig.Prepare(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：QTECameraRig.SelectPath(SplineContainer)。</param>
    public void SelectPath(SplineContainer path)
    {
        if (path != null)
        {
            SelectedPath = path;
        }
        else
        {
            SelectedPath = primaryPath;
        }

        splineDolly.Spline = SelectedPath;
    }

    /// <summary>ConfigureDynamicEntry 的业务处理入口；本方法直接依赖 QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()；下游（含内部回调体） QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="cameraPosition">中文含义：摄像机位置。使用位置：作为 QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float) 的参数。</param>
    /// <param name="tangentScale">中文含义：切线缩放。使用位置：作为 QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float) 的参数。</param>
    public void ConfigureDynamicEntry(Vector3 cameraPosition, float tangentScale)
    {
        ConfigurePathStart(primaryPath, cameraPosition, tangentScale);
        ConfigurePathStart(fallbackPath, cameraPosition, tangentScale);
    }

    /// <summary>GetNormalizedPositionAtKnot 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)、SplineUtility.GetNormalizedInterpolation&lt;Spline&gt;(Spline, float, PathIndexUnit)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HandleAssassinationContact(int)；下游（含内部回调体） Mathf.Clamp(int, int, int)、SplineUtility.GetNormalizedInterpolation&lt;Spline&gt;(Spline, float, PathIndexUnit)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="knotIndex">中文含义：样条结点索引。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</param>
    public float GetNormalizedPositionAtKnot(int knotIndex)
    {
        if (SelectedPath == null || SelectedPath.Spline == null || SelectedPath.Spline.Count <= 1)
        {
            return 1f;
        }

        // clampedIndex（限制后的索引）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 SplineUtility.GetNormalizedInterpolation<Spline>(Spline, float, PathIndexUnit) 的参数。
int clampedIndex = Mathf.Clamp(knotIndex, 0, SelectedPath.Spline.Count - 1);
        return SplineUtility.GetNormalizedInterpolation(SelectedPath.Spline, clampedIndex, PathIndexUnit.Knot);
    }

    /// <summary>SetSplinePosition 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.BeginEntry()、QTECameraPresenter.HoldAtEnd()、QTECameraPresenter.UpdateMotion()；下游（含内部回调体） Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="normalizedPosition">中文含义：归一化位置。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public void SetSplinePosition(float normalizedPosition)
    {
        splineDolly.CameraPosition = Mathf.Clamp01(normalizedPosition);
    }

    /// <summary>SetFieldOfView 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HoldAtEnd()、QTECameraPresenter.UpdateMotion()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fieldOfView">中文含义：视野角度。使用位置：QTECameraRig.SetFieldOfView(float)。</param>
    public void SetFieldOfView(float fieldOfView)
    {
        qteCamera.Lens.FieldOfView = fieldOfView;
    }

    /// <summary>SetCameraFeedback 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.UpdateCameraFeedback()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="splineOffset">中文含义：样条偏移量。使用位置：QTECameraRig.SetCameraFeedback(Vector3, float)。</param>
    /// <param name="dutch">中文含义：镜头侧倾。使用位置：QTECameraRig.SetCameraFeedback(Vector3, float)。</param>
    public void SetCameraFeedback(Vector3 splineOffset, float dutch)
    {
        splineDolly.SplineOffset = splineOffset;
        qteCamera.Lens.Dutch = dutch;
    }

    /// <summary>ResetCameraFeedback 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.ReleaseCamera()、QTECameraPresenter.UpdateCameraFeedback()、QTECameraRig.Prepare(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetCameraFeedback()
    {
        splineDolly.SplineOffset = Vector3.zero;
        qteCamera.Lens.Dutch = 0f;
    }

    /// <summary>ConfigurePathStart 的业务处理入口；本方法直接依赖 BezierKnot.BezierKnot(float3, float3, float3, quaternion)、Mathf.Clamp(float, float, float)、Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTECameraRig.ConfigureDynamicEntry(Vector3, float)；下游（含内部回调体） BezierKnot.BezierKnot(float3, float3, float3, quaternion)、Mathf.Clamp(float, float, float)、Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)。</param>
    /// <param name="cameraPosition">中文含义：摄像机位置。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。</param>
    /// <param name="tangentScale">中文含义：切线缩放。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    private static void ConfigurePathStart(SplineContainer path, Vector3 cameraPosition, float tangentScale)
    {
        if (path == null || path.Spline == null || path.Spline.Count < 2)
        {
            return;
        }

        // localPosition（局部位置）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 BezierKnot.BezierKnot(float3, float3, float3, quaternion) 的参数、QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)。
Vector3 localPosition = path.transform.InverseTransformPoint(cameraPosition);
        // nextKnot（下一次样条结点）：使用位置：QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)。
BezierKnot nextKnot = path.Spline[1];
        // direction（方向）：使用位置：QTECameraRig.ConfigurePathStart(SplineContainer, Vector3, float)。
float3 direction = nextKnot.Position - (float3)localPosition;
        // tangent（切线）：使用位置：作为 BezierKnot.BezierKnot(float3, float3, float3, quaternion) 的参数。
float3 tangent = direction * Mathf.Clamp(tangentScale, 0f, 0.75f);
        path.Spline[0] = new BezierKnot(localPosition, float3.zero, tangent, quaternion.identity);
    }
}
