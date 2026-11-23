using UnityEngine;
using GameArchitecture;
using System.Collections.Generic;

/// <summary>
/// 全身受击与死亡动作的根位移驱动器。
/// 水平位移来自动画，垂直位移始终交给刚体重力与碰撞处理。
/// </summary>
[DisallowMultipleComponent]
public class LeonActionMotionDriver : MonoBehaviour
{
    /// <summary>中文含义：动画控制器。使用位置：LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.BeginHitMotion()、LeonActionMotionDriver.CancelAllMotion() 等共 9 处。</summary>
    [Header("组件引用")]
    public Animator animator;
    /// <summary>中文含义：身体。使用位置：LeonActionMotionDriver.BeginHitMotion()、LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3) 等共 11 处。</summary>
    public Rigidbody body;
    [Tooltip("可穿过的散石和碎木碰撞体，仅作用于里昂的胶囊和地面探测。")]
    [SerializeField] private Collider[] walkThroughDetails = System.Array.Empty<Collider>();
    private readonly HashSet<Collider> walkDetailSet = new HashSet<Collider>();
    private readonly List<Collider> ownedIgnorePairs = new List<Collider>();
    /// <summary>中文含义：命中运动是否生效。使用位置：LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.BeginHitMotion()、LeonActionMotionDriver.CancelAllMotion() 等共 8 处。</summary>
    private bool hitMotionActive;
    private bool footHeldByTrap;
    /// <summary>中文含义：死亡运动是否生效。使用位置：LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.BeginHitMotion() 等共 13 处。</summary>
    private bool deathMotionActive;
    private readonly CorpseBodyPhysics corpsePhysics = new CorpseBodyPhysics();
    private bool corpseBodyWasKinematic;
    internal bool CorpseFalling => corpsePhysics.Falling;
    /// <summary>中文含义：运动租用版本。使用位置：作为 ObjectIdentity.ObjectIdentity(int, long) 的参数、LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.OnEnable()。</summary>
    private long motionLeaseVersion;
    /// <summary>中文含义：运动交接编号。使用位置：LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)。</summary>
    private long motionHandoffId;
    /// <summary>中文含义：运动关联编号。使用位置：LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)。</summary>
    private int motionAssociationId;
    /// <summary>中文含义：当前运动。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数、LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.CancelAllMotion() 等共 12 处。</summary>
    private MotionControlToken currentMotion;
    /// <summary>中文含义：当前运动凭证。使用位置：作为 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion) 的参数、作为 LeonActionMotionDriver.StopHorizontalSpeed(MotionControlToken) 的参数、LeonFootAction.AllowGroundContact.get 等共 5 处。</summary>
    public MotionControlToken CurrentMotionToken => currentMotion;

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        motionLeaseVersion++;
        currentMotion = default;
        if (nativeGaitCapsule != null)
        {
            ApplyWalkThroughDetails();
        }
    }

    /// <summary>AuthorizeMotion 的业务处理入口；本方法直接依赖 ActionHandle.ActionHandle(ObjectIdentity, long)、MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.BeginHitMotion()、LeonActionMotionDriver.RequestPairedActionMotion(int)、LeonActionMotionDriver.SynchronizeNormalMotionPermission(LeonState)、LeonHighDropLanding.BeginDrop() 等共 6 处；下游（含内部回调体） ActionHandle.ActionHandle(ObjectIdentity, long)、MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源运动来源。使用位置：作为 MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long) 的参数、LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)。</param>
    /// <param name="associationId">中文含义：关联编号。使用位置：LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)。</param>
    public MotionControlToken AuthorizeMotion(MotionSource source, int associationId)
    {
        if (!isActiveAndEnabled || motionLeaseVersion <= 0 || source == MotionSource.None)
        {
            return default;
        }

        if (deathMotionActive && source != MotionSource.Death)
        {
            return default;
        }

        if (pairedActionSession != 0 && source != MotionSource.PairedActionAction && source != MotionSource.Death)
        {
            return default;
        }

        if (currentMotion.Valid && currentMotion.Source == source && motionAssociationId == associationId)
        {
            return currentMotion;
        }

        // 先撤销，再授权。旧固定更新、动画或通行回调持有的凭证立即失效。
        currentMotion = default;
        motionAssociationId = associationId;
        // id（编号）：使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long) 的参数。
long id = ++motionHandoffId;
        // identity（身份标识）：使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数。
var identity = new ObjectIdentity(GetInstanceID(), motionLeaseVersion);
        currentMotion = new MotionControlToken(new ActionHandle(identity, id), source, id);
        return currentMotion;
    }

    /// <summary>TokenValid 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)、LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)、LeonActionMotionDriver.CommitPosition(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 等共 9 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：LeonActionMotionDriver.TokenValid(MotionControlToken)。</param>
    public bool TokenValid(MotionControlToken token) => isActiveAndEnabled && token.Valid && token.Action == currentMotion.Action && token.Source == currentMotion.Source && token.HandoffId == currentMotion.HandoffId;
    /// <summary>StopHorizontalSpeed 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.StopCurrentHorizontalMotion()；下游（含内部回调体） LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数。</param>
    public void StopHorizontalSpeed(MotionControlToken token)
    {
        if (TokenValid(token))
        {
            ClearHorizontalVelocity();
        }
    }

    /// <summary>CommitSpeed 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.UpdateActiveDrop()、LeonLocomotion.HandleMovement()、LeonPlayerStairRampRuntime.ApplyPlayerMotionAssistInternal(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken)、LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics() 等共 9 处；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数。</param>
    /// <param name="speed">中文含义：速度。使用位置：LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)。</param>
    public bool CommitSpeed(MotionControlToken token, Vector3 speed)
    {
        if (!TokenValid(token) || body == null)
        {
            return false;
        }

        body.linearVelocity = speed;
        return true;
    }

    /// <summary>CommitAngleSpeed 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.EndTraversalPhysics()；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数。</param>
    /// <param name="speed">中文含义：速度。使用位置：LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)。</param>
    public bool CommitAngleSpeed(MotionControlToken token, Vector3 speed)
    {
        if (!TokenValid(token) || body == null)
        {
            return false;
        }

        body.angularVelocity = speed;
        return true;
    }

    /// <summary>CommitPosition 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.CommitDropImpact()；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数。</param>
    /// <param name="positionValue">中文含义：位置值。使用位置：LeonActionMotionDriver.CommitPosition(MotionControlToken, Vector3)。</param>
    public bool CommitPosition(MotionControlToken token, Vector3 positionValue)
    {
        if (!TokenValid(token))
        {
            return false;
        }

        if (body != null)
        {
            body.position = positionValue;
        }
        else
        {
            transform.position = positionValue;
        }

        return true;
    }

    /// <summary>CommitNormalOrientation 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion)、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)、LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool)；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数、LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)。</param>
    /// <param name="orientation">中文含义：朝向。使用位置：LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)。</param>
    public bool CommitNormalOrientation(MotionControlToken token, Quaternion orientation)
    {
        if (!TokenValid(token) || token.Source != MotionSource.NormalMovement)
        {
            return false;
        }

        if (body != null && Time.inFixedTimeStep)
        {
            // 使用同一个刚体维护物理位姿和渲染插值。
            if (Quaternion.Angle(body.rotation, orientation) > 0.01f)
            {
                body.MoveRotation(orientation);
            }
        }
        else
        {
            // 渲染帧内提交的转向也写回刚体，下一物理步和松开格挡时都承接同一朝向。
            if (body != null && Quaternion.Angle(body.rotation, orientation) > .01f)
            {
                body.rotation = orientation;
            }
            transform.rotation = orientation;
        }
        return true;
    }

    /// <summary>CommitTraversalPose 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)、Transform.SetPositionAndRotation(Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)、Transform.SetPositionAndRotation(Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数、LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion)。</param>
    /// <param name="positionValue">中文含义：位置值。使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数、LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion)。</param>
    /// <param name="orientation">中文含义：朝向。使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数、LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion)。</param>
    public bool CommitTraversalPose(MotionControlToken token, Vector3 positionValue, Quaternion orientation, bool interpolate = false)
    {
        if (!TokenValid(token) || token.Source != MotionSource.Traversal)
        {
            return false;
        }

        if (body != null)
        {
            if (interpolate)
            {
                body.MovePosition(positionValue);
                body.MoveRotation(orientation);
            }
            else
            {
                body.position = positionValue;
                body.rotation = orientation;
            }
        }
        else
        {
            transform.SetPositionAndRotation(positionValue, orientation);
        }

        return true;
    }

    internal void CommitCinematicExitPose(MotionControlToken token, Vector3 positionValue, Quaternion orientation)
    {
        if (!CommitTraversalPose(token, positionValue, orientation) || body == null)
        {
            return;
        }
        // 在首个恢复物理的帧之前就更新摄像机锚点，并清除传送前的刚体插值。
        RigidbodyInterpolation interpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;
        transform.SetPositionAndRotation(positionValue, orientation);
        Physics.SyncTransforms();
        body.interpolation = interpolation;
    }

    /// <summary>ReleaseMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.FinishDrop()、LeonWindowTraversal.ClearActiveTraversal()；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 LeonActionMotionDriver.TokenValid(MotionControlToken) 的参数。</param>
    public void ReleaseMotion(MotionControlToken token)
    {
        if (TokenValid(token))
        {
            currentMotion = default;
        }
    }

    /// <summary>SynchronizeNormalMotionPermission 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.SynchronizeMotionControlAuthority()；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前里昂状态。使用位置：LeonActionMotionDriver.SynchronizeNormalMotionPermission(LeonState)。</param>
    public void SynchronizeNormalMotionPermission(LeonState state)
    {
        if (footHeldByTrap || pairedActionSession != 0 || hitMotionActive || deathMotionActive || state == LeonState.Traversing || state == LeonState.Staggered || state == LeonState.QTE || state == LeonState.Dead)
        {
            return;
        }

        AuthorizeMotion(MotionSource.NormalMovement, 0);
    }

    /// <summary>中文含义：已配对动作会话。使用位置：作为 LeonActionMotionDriver.ReleasePairedActionMotion(int) 的参数、LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool) 等共 8 处。</summary>
    private int pairedActionSession;
    /// <summary>中文含义：已配对动作前方约束。使用位置：LeonActionMotionDriver.ReleasePairedActionMotion(int)、LeonActionMotionDriver.RequestPairedActionMotion(int)。</summary>
    private RigidbodyConstraints pairedActionFrontConstraint;
    /// <summary>中文含义：是否已配对动作前方根运动。使用位置：LeonActionMotionDriver.ReleasePairedActionMotion(int)、LeonActionMotionDriver.RequestPairedActionMotion(int)。</summary>
    private bool pairedActionFrontRootMotion;
    /// <summary>中文含义：当前已配对动作会话。</summary>
    public int CurrentPairedActionSession => pairedActionSession;

    /// <summary>RequestPairedActionMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.ClearHorizontalVelocity()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CacheAndLockPlayer()；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.ClearHorizontalVelocity()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">中文含义：会话。使用位置：作为 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int) 的参数、LeonActionMotionDriver.RequestPairedActionMotion(int)。</param>
    public bool RequestPairedActionMotion(int session)
    {
        if (session <= 0 || deathMotionActive || pairedActionSession != 0)
        {
            return false;
        }

        pairedActionSession = session;
        if (!AuthorizeMotion(MotionSource.PairedActionAction, session).Valid)
        {
            pairedActionSession = 0;
            return false;
        }

        if (body != null)
        {
            pairedActionFrontConstraint = body.constraints;
            body.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            ClearHorizontalVelocity();
        }

        if (animator != null)
        {
            pairedActionFrontRootMotion = animator.applyRootMotion;
            if (nativeGaitDriver == null)
            {
                animator.applyRootMotion = false;
            }
        }

        return true;
    }

    /// <summary>CommitPairedActionPose 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)、Transform.SetPositionAndRotation(Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.SetPlayerWorldPose(Vector3, Quaternion)、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)、Transform.SetPositionAndRotation(Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">中文含义：会话。使用位置：LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)。</param>
    /// <param name="positionValue">中文含义：位置值。使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数、LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)。</param>
    /// <param name="orientation">中文含义：朝向。使用位置：作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数、LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)。</param>
    /// <param name="updateOrientation">中文含义：是否更新朝向。使用位置：LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)。</param>
    public bool CommitPairedActionPose(int session, Vector3 positionValue, Quaternion orientation, bool updateOrientation)
    {
        if (session <= 0 || session != pairedActionSession || deathMotionActive || currentMotion.Source != MotionSource.PairedActionAction || motionAssociationId != session || !TokenValid(currentMotion))
        {
            return false;
        }

        if (body != null)
        {
            body.position = positionValue;
            if (updateOrientation)
            {
                body.rotation = orientation;
            }
        }

        if (updateOrientation)
        {
            transform.SetPositionAndRotation(positionValue, orientation);
        }
        else
        {
            transform.position = positionValue;
        }

        return true;
    }

    /// <summary>ReleasePairedActionMotion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.CancelAllMotion()、LeonQTECoordinator.RestorePlayerLock()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">中文含义：会话。使用位置：LeonActionMotionDriver.ReleasePairedActionMotion(int)。</param>
    public void ReleasePairedActionMotion(int session)
    {
        if (session <= 0 || session != pairedActionSession)
        {
            return;
        }

        pairedActionSession = 0;
        if (currentMotion.Source == MotionSource.PairedActionAction)
        {
            currentMotion = default;
        }

        if (body != null)
        {
            body.constraints = pairedActionFrontConstraint;
        }

        if (animator != null && !deathMotionActive)
        {
            animator.applyRootMotion = pairedActionFrontRootMotion;
        }
    }

    /// <summary>中文含义：命中运动是否生效。</summary>
    public bool HitMotionActive => hitMotionActive;
    /// <summary>中文含义：死亡运动是否生效。</summary>
    public bool DeathMotionActive => deathMotionActive;

    private LeonAnimatorDriver nativeGaitDriver;
    private LeonLocomotion nativeLocomotion;
    private Vector3 nativeStepOrigin;
    private Vector3 nativeStepDirection;
    private CapsuleCollider nativeGaitCapsule;
    private readonly RaycastHit[] nativeGaitHits = new RaycastHit[12];
    private float nativeStepDistance;
    private float nativeBlockedTime;
    private bool nativeBlocked;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;Rigidbody&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        nativeGaitCapsule = GetComponent<CapsuleCollider>();
        nativeLocomotion = GetComponent<LeonLocomotion>();
        ApplyWalkThroughDetails();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (animator != null)
        {
            if (animator.HasState(0, LeonAnimatorDriver.NativeIdleState))
            {
                nativeGaitDriver = GetComponent<LeonAnimatorDriver>();
                animator.updateMode = AnimatorUpdateMode.Fixed;
            }
            SetRootSampling(false);
        }
    }

    /// <summary>BeginHitMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginHitMotion()
    {
        if (!AuthorizeMotion(MotionSource.AttackRootMotion, 0).Valid)
        {
            return;
        }

        deathMotionActive = false;
        SetRootSampling(true);

        if (body != null)
        {
            // velocity（速度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 velocity = body.linearVelocity;
            body.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }

        hitMotionActive = true;
    }

    /// <summary>BeginDeathMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.ReleasePairedActionMotion(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestDeath()；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.ReleasePairedActionMotion(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginDeathMotion()
    {
        ReleasePairedActionMotion(pairedActionSession);
        AuthorizeMotion(MotionSource.Death, 0);
        hitMotionActive = false;
        deathMotionActive = true;
        SetRootSampling(true);

        ClearHorizontalVelocity();
    }

    /// <summary>EndHitMotion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndHitMotion()
    {
        hitMotionActive = false;
        if (currentMotion.Source == MotionSource.AttackRootMotion)
        {
            currentMotion = default;
        }

        if (!deathMotionActive)
        {
            SetRootSampling(false);
        }
    }

    /// <summary>EndDeathMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ClearHorizontalVelocity()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)；下游（含内部回调体） LeonActionMotionDriver.ClearHorizontalVelocity()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndDeathMotion()
    {
        if (deathMotionActive && TokenValid(currentMotion))
        {
            TryStartCorpseFall();
        }
        if (corpsePhysics.Active)
        {
            SetRootSampling(false);
            return;
        }
        deathMotionActive = false;
        if (currentMotion.Source == MotionSource.Death)
        {
            currentMotion = default;
        }

        ClearHorizontalVelocity();
        if (!hitMotionActive)
        {
            SetRootSampling(false);
        }
    }

    /// <summary>CancelAllMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.ReleasePairedActionMotion(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonActionMotionDriver.OnDisable()、LeonAnimationActionCoordinator.OnDisable()；下游（含内部回调体） LeonActionMotionDriver.ClearHorizontalVelocity()、LeonActionMotionDriver.ReleasePairedActionMotion(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelAllMotion()
    {
        if (corpsePhysics.Active)
        {
            corpsePhysics.Release();
            if (body != null)
            {
                body.isKinematic = corpseBodyWasKinematic;
            }
        }
        ReleasePairedActionMotion(pairedActionSession);
        currentMotion = default;
        hitMotionActive = false;
        deathMotionActive = false;
        ClearHorizontalVelocity();
        SetRootSampling(false);
    }

    /// <summary>Unity 固定时间步更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonActionMotionDriver.ClearHorizontalVelocity()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void FixedUpdate()
    {
        if ((deathMotionActive || footHeldByTrap) && !corpsePhysics.Active)
        {
            ClearHorizontalVelocity();
        }
    }

    private void LateUpdate()
    {
        if (!deathMotionActive || !TokenValid(currentMotion) || Time.deltaTime <= 0f)
        {
            return;
        }
        TryStartCorpseFall();
        if (corpsePhysics.Active)
        {
            Pose pose = corpsePhysics.ReadPose();
            CommitPosition(currentMotion, pose.position);
            if (body != null)
            {
                body.rotation = pose.rotation;
            }
            else
            {
                transform.rotation = pose.rotation;
            }
        }
    }

    private void TryStartCorpseFall()
    {
        if (!corpsePhysics.Active && corpsePhysics.TryBegin(animator, transform, true) && body != null)
        {
            corpseBodyWasKinematic = body.isKinematic;
            body.isKinematic = true;
        }
    }

    internal void SetTrapFootLock(bool locked)
    {
        footHeldByTrap = locked;
        ClearHorizontalVelocity();
    }

    /// <summary>ClearHorizontalVelocity 的业务处理入口；本方法直接依赖 Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonActionMotionDriver.BeginDeathMotion()、LeonActionMotionDriver.CancelAllMotion()、LeonActionMotionDriver.EndDeathMotion()、LeonActionMotionDriver.FixedUpdate()、LeonActionMotionDriver.RequestPairedActionMotion(int) 等共 6 处；下游（含内部回调体） Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearHorizontalVelocity()
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        // velocity（速度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 velocity = body.linearVelocity;
        body.linearVelocity = new Vector3(0f, velocity.y, 0f);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonActionMotionDriver.CancelAllMotion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        CancelAllMotion();
        foreach (Collider detail in ownedIgnorePairs)
        {
            if (nativeGaitCapsule != null && detail != null)
            {
                Physics.IgnoreCollision(nativeGaitCapsule, detail, false);
            }
        }
        ownedIgnorePairs.Clear();
        walkDetailSet.Clear();
    }

    private void ApplyWalkThroughDetails()
    {
        if (nativeGaitCapsule == null || walkThroughDetails == null)
        {
            return;
        }
        foreach (Collider detail in walkThroughDetails)
        {
            if (detail != null)
            {
                RegisterWalkThroughDetail(detail);
            }
        }
    }

    private void RegisterWalkThroughDetail(Collider detail)
    {
        if (walkDetailSet.Add(detail) && !Physics.GetIgnoreCollision(nativeGaitCapsule, detail))
        {
            Physics.IgnoreCollision(nativeGaitCapsule, detail);
            ownedIgnorePairs.Add(detail);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 原版摆放表中的枯藤沿村路重复使用；只放行这一件装饰网格与里昂胶囊的碰撞。
        if (nativeGaitCapsule != null && collision.collider is MeshCollider obstacle &&
            obstacle.name == "sm13_521_01" && obstacle.sharedMesh != null &&
            obstacle.sharedMesh.name == "6defebdab5550d57f8b7")
        {
            RegisterWalkThroughDetail(obstacle);
        }
    }

    internal bool IsWalkThroughDetail(Collider collider)
    {
        return walkDetailSet.Contains(collider);
    }

    /// <summary>OnAnimatorMove 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.TokenValid(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonActionMotionDriver.TokenValid(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAnimatorMove()
    {
        if (corpsePhysics.Active || footHeldByTrap || pairedActionSession != 0 || animator == null || !TokenValid(currentMotion))
        {
            return;
        }

        if (Time.inFixedTimeStep && currentMotion.Source == MotionSource.NormalMovement && nativeGaitDriver != null
            && nativeGaitDriver.TryReadNativeGaitDirection(out var heading) && body != null)
        {
            CommitNativeGaitStep(heading);
            return;
        }
        if ((!hitMotionActive && !deathMotionActive)
            || (currentMotion.Source != MotionSource.AttackRootMotion && currentMotion.Source != MotionSource.Death))
        {
            return;
        }

        // rootMotion（动画根运动）：使用位置：LeonActionMotionDriver.OnAnimatorMove()。
Vector3 rootMotion = animator.deltaPosition;
        rootMotion.y = 0f;
        if (body != null)
        {
            body.position += rootMotion;
        }
        else
        {
            transform.position += rootMotion;
        }
    }

    private void SetRootSampling(bool actionActive)
    {
        if (animator != null)
        {
            // 正式原版步态始终采样根轨，由动作凭证决定本步由谁提交。
            animator.applyRootMotion = actionActive || nativeGaitDriver != null;
        }
    }

    private void CommitNativeGaitStep(Vector3 heading)
    {
        float stepTime = Time.fixedDeltaTime;
        Vector3 velocity = Vector3.zero;
        if (!nativeBlocked && nativeGaitDriver.IsBasicRunTurning)
        {
            // 转身根轨已经包含刹步、转向和重新起步。
            velocity = Vector3.ProjectOnPlane(animator.deltaPosition, Vector3.up) / stepTime;
            Quaternion rotation = body.rotation * animator.deltaRotation;
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.01f)
            {
                CommitNormalOrientation(currentMotion, Quaternion.LookRotation(forward));
            }
        }
        else if (!nativeBlocked && nativeGaitDriver.IsBasicWalkBraking)
        {
            // 收步片段的根轨可能为零，由上一个物理步的实际速度完成减速。
            velocity = nativeGaitIncomingVelocity * Mathf.Exp(-stepTime / 0.055f);
        }
        else if (!nativeBlocked && heading.sqrMagnitude > 0.01f)
        {
            Vector3 planar = Vector3.ProjectOnPlane(animator.deltaPosition, Vector3.up);
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (nativeGaitDriver.UsesNativeDirectionalRootMotion())
            {
                float distance = Mathf.Max(0f, Vector3.Dot(planar, heading));
                velocity = heading * (distance / stepTime);
            }
            else
            {
                velocity = Quaternion.FromToRotation(facing, heading) * planar / stepTime;
            }
            if (nativeGaitDriver.IsBasicGaitActive)
            {
                float blend = 1f - Mathf.Exp(-stepTime / 0.055f);
                velocity = Vector3.Lerp(nativeGaitIncomingVelocity, velocity, blend);
            }
            if (nativePickupEntrySpeed > 0f && Vector3.Dot(velocity, heading) < nativePickupEntrySpeed)
            {
                // 上身拾取覆盖层会压低 Animator 根增量，短动作期间沿当前输入维持入场步速。
                velocity = heading * nativePickupEntrySpeed;
            }
        }
        velocity = Vector3.ClampMagnitude(velocity, 7f);
        Vector3 step = velocity * stepTime;
        velocity.y = body.linearVelocity.y;
        if (!CommitSpeed(currentMotion, velocity))
        {
            return;
        }
        if (nativeLocomotion != null)
        {
            if (nativeGaitDriver.IsBasicGaitActive && step.sqrMagnitude > 0.000001f)
            {
                heading = step.normalized;
            }
            nativeLocomotion.ApplyNativeGaitStairAssist(heading, currentMotion);
        }
        nativeStepOrigin = body.position;
        nativeStepDistance = step.magnitude;
        nativeStepDirection = step.normalized;
    }

    private Vector3 nativeGaitIncomingVelocity;
    private float nativePickupEntrySpeed;

    internal void PrepareNativeGaitStep(MotionControlToken token, Vector3 incomingVelocity, float pickupEntrySpeed)
    {
        nativeGaitIncomingVelocity = incomingVelocity;
        nativePickupEntrySpeed = pickupEntrySpeed;
        if (nativeBlocked || nativeGaitDriver == null || !nativeGaitDriver.IsBasicGaitActive
            || !nativeGaitDriver.TryReadNativeGaitDirection(out _))
        {
            StopHorizontalSpeed(token);
        }
    }

    internal bool CheckNativeGaitObstruction(Vector3 heading)
    {
        bool sameBlockedHeading = Vector3.Dot(nativeBlockedHeading, heading) > 0.8f;
        if (nativeBlocked && !sameBlockedHeading)
        {
            nativeBlockedTime = 0f;
        }
        if (nativeStepDistance > 0.004f)
        {
            float actual = Vector3.Dot(body.position - nativeStepOrigin, nativeStepDirection);
            if (actual < nativeStepDistance * 0.20f)
            {
                nativeBlockedTime += Time.fixedDeltaTime;
            }
            else
            {
                nativeBlockedTime = 0f;
            }
        }
        nativeStepDistance = 0f;
        if ((nativeBlockedTime >= 0.12f || nativeBlocked && sameBlockedHeading) && heading.sqrMagnitude > 0.01f
            && !TryStepOntoOuterPlatformSeam(heading) && !TryStepOntoLowTerrainEdge(heading)
            && HasNativeGaitObstacle(heading))
        {
            nativeBlocked = true;
            nativeBlockedHeading = heading;
            return true;
        }
        if (nativeBlocked || heading.sqrMagnitude <= 0.01f)
        {
            nativeBlockedTime = 0f;
        }
        nativeBlocked = false;
        nativeBlockedHeading = Vector3.zero;
        return false;
    }

    private Vector3 nativeBlockedHeading;

    private Vector3 lastWalkPosition;
    private Vector3 lastWalkHeading;
    private float lastWalkTime = -1f;
    private float expectedWalkDistance;

    internal void AssistWalkingStep(Vector3 heading, float speed)
    {
        if (body == null || currentMotion.Source != MotionSource.NormalMovement)
        {
            return;
        }
        Vector3 position = body.position;
        bool consecutive = Mathf.Abs(Time.fixedTime - lastWalkTime - Time.fixedDeltaTime) < 0.001f;
        bool stalled = consecutive && Vector3.Dot(lastWalkHeading, heading) > 0.85f &&
            Vector3.Dot(position - lastWalkPosition, lastWalkHeading) < expectedWalkDistance * 0.25f;
        lastWalkPosition = position;
        lastWalkHeading = heading;
        expectedWalkDistance = speed * Time.fixedDeltaTime;
        lastWalkTime = Time.fixedTime;
        if (!TryStepOntoOuterPlatformSeam(heading) && stalled)
        {
            TryStepOntoLowTerrainEdge(heading);
        }
    }

    private bool TryFindTerrainTread(Vector3 origin, float length, out RaycastHit tread)
    {
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, nativeGaitHits, length,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        tread = default;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = nativeGaitHits[i];
            if (candidate.distance < closest && candidate.normal.y >= 0.65f && candidate.rigidbody == null &&
                !IsWalkThroughDetail(candidate.collider) &&
                !Physics.GetIgnoreLayerCollision(nativeGaitCapsule.gameObject.layer, candidate.collider.gameObject.layer) &&
                !Physics.GetIgnoreCollision(nativeGaitCapsule, candidate.collider))
            {
                tread = candidate;
                closest = candidate.distance;
            }
        }
        return closest < float.PositiveInfinity;
    }

    private bool TryStepOntoLowTerrainEdge(Vector3 heading)
    {
        const float maxRise = 0.24f;
        if (body == null || nativeGaitCapsule == null || !nativeGaitCapsule.enabled
            || currentMotion.Source != MotionSource.NormalMovement || nativeLocomotion == null
            || !nativeLocomotion.HasGroundForTerrainStep)
        {
            return false;
        }

        Bounds bounds = nativeGaitCapsule.bounds;
        Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var region = HouseThreeRegionNavigation.Current;
        if (region != null && region.TreadRegion(foot) == VillageTraversalRegion.Stair)
        {
            return false;
        }

        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        for (int sample = 0; sample < 3; sample++)
        {
            Vector3 ahead = foot + heading * (radius + .06f + sample * .075f);
            Vector3 high = ahead + Vector3.up * (maxRise + .04f);
            if (!TryFindTerrainTread(high, maxRise + .05f, out RaycastHit tread))
            {
                continue;
            }

            float rise = tread.point.y - foot.y;
            if (rise < .015f || rise > maxRise || (region != null && region.TreadRegion(tread.point) != region.TreadRegion(foot)))
            {
                continue;
            }

            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized * radius * 0.4f;
            Vector3 forward = heading * 0.08f;
            if (TryFindTerrainTread(high + forward + side, maxRise + .05f, out RaycastHit left) &&
                TryFindTerrainTread(high + forward - side, maxRise + .05f, out RaycastHit right) &&
                Mathf.Abs(left.point.y - tread.point.y) < 0.045f && Mathf.Abs(right.point.y - tread.point.y) < 0.045f &&
                TryCommitTerrainRise(heading, bounds, radius, rise, true))
            {
                return true;
            }
        }

        return TryCrossLowRidge(heading, foot, bounds, radius, maxRise);
    }

    private bool TryCrossLowRidge(Vector3 heading, Vector3 foot, Bounds bounds, float radius, float maxRise)
    {
        // 窄藤或碎石的顶面可能落在两个踏面采样点之间。先找挡脚的侧面，再检查另一侧的落脚地面。
        var region = HouseThreeRegionNavigation.Current;
        Vector3 side = Vector3.Cross(Vector3.up, heading).normalized * radius * .45f;
        for (int lane = -1; lane <= 1; lane++)
        {
            Vector3 origin = foot + side * lane + Vector3.up * .055f;
            int count = Physics.RaycastNonAlloc(origin, heading, nativeGaitHits, radius + .16f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = nativeGaitHits[i];
                if (hit.rigidbody == null && hit.distance < distance && hit.normal.y < .65f &&
                    Vector3.Dot(hit.normal, heading) < -.2f && !IsWalkThroughDetail(hit.collider) &&
                    !Physics.GetIgnoreLayerCollision(nativeGaitCapsule.gameObject.layer, hit.collider.gameObject.layer) &&
                    !Physics.GetIgnoreCollision(nativeGaitCapsule, hit.collider))
                {
                    distance = hit.distance;
                }
            }
            if (float.IsInfinity(distance))
            {
                continue;
            }

            Vector3 high = origin + heading * (distance + .025f);
            high.y = foot.y + maxRise + .05f;
            if (!TryFindTerrainTread(high, maxRise + .07f, out var crest))
            {
                continue;
            }
            float rise = crest.point.y - foot.y;
            if (rise < .025f || rise > maxRise)
            {
                continue;
            }
            Vector3 landing = foot + heading * (distance + .24f) + Vector3.up * (maxRise + .05f);
            if (TryFindTerrainTread(landing + side, maxRise + .11f, out var left) &&
                TryFindTerrainTread(landing - side, maxRise + .11f, out var right) &&
                left.point.y <= crest.point.y + .035f && right.point.y <= crest.point.y + .035f &&
                (region == null || (region.TreadRegion(left.point) == region.TreadRegion(foot) &&
                region.TreadRegion(right.point) == region.TreadRegion(foot))) &&
                TryCommitTerrainRise(heading, bounds, radius, rise, true))
            {
                return true;
            }
        }
        return false;
    }

    internal bool TryStepOntoOuterPlatformSeam(Vector3 heading)
    {
        var navigation = HouseThreeRegionNavigation.Current;
        if (navigation == null || body == null || nativeGaitCapsule == null || !nativeGaitCapsule.enabled ||
            currentMotion.Source != MotionSource.NormalMovement || heading.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        Bounds bounds = nativeGaitCapsule.bounds;
        Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        if (navigation.TreadRegion(foot) != VillageTraversalRegion.SecondFloorOuterPlatform ||
            !body.SweepTest(heading, out var blocker, 0.16f, QueryTriggerInteraction.Ignore) ||
            Vector3.Dot(blocker.normal, heading) >= -0.2f)
        {
            return false;
        }

        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        if (!navigation.TryReadOuterPlatformSeam(foot, heading, radius, blocker, out float rise))
        {
            return false;
        }

        return TryCommitTerrainRise(heading, bounds, radius, rise);
    }

    private bool TryCommitTerrainRise(Vector3 heading, Bounds bounds, float radius, float rise, bool requireSupport = false)
    {
        Vector3 raise = Vector3.up * (rise + 0.02f);
        float sweepRadius = Mathf.Max(0.01f, radius - 0.01f);
        Vector3 halfSegment = Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 top = bounds.center + halfSegment;
        int overhead = Physics.SphereCastNonAlloc(top, sweepRadius, Vector3.up, nativeGaitHits, raise.y,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overhead; i++)
        {
            var obstacle = nativeGaitHits[i].collider;
            if (obstacle.attachedRigidbody != body && !IsWalkThroughDetail(obstacle) &&
                !Physics.GetIgnoreCollision(nativeGaitCapsule, obstacle) &&
                !Physics.GetIgnoreLayerCollision(nativeGaitCapsule.gameObject.layer, obstacle.gameObject.layer))
            {
                return false;
            }
        }

        Vector3 raisedCenter = bounds.center + raise;
        int count = Physics.CapsuleCastNonAlloc(raisedCenter + halfSegment, raisedCenter - halfSegment,
            sweepRadius, heading, nativeGaitHits, 0.10f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = nativeGaitHits[i];
            if (hit.collider.attachedRigidbody != body && hit.normal.y < 0.65f &&
                Vector3.Dot(hit.normal, heading) < -0.2f &&
                !Physics.GetIgnoreLayerCollision(nativeGaitCapsule.gameObject.layer, hit.collider.gameObject.layer) &&
                !Physics.GetIgnoreCollision(nativeGaitCapsule, hit.collider))
            {
                return false;
            }
        }

        Vector3 raisedPosition = body.position + raise;
        if (requireSupport)
        {
            // 越过胶囊接触公差，让物理步接住踏面，而非抬起后落回原地。
            raisedPosition += heading * 0.045f;
        }
        nativeBlockedTime = 0f;
        return CommitPosition(currentMotion, raisedPosition);
    }

    private bool HasNativeGaitObstacle(Vector3 heading)
    {
        const float clearance = .12f;
        if (body.SweepTest(heading, out var sweepHit, clearance, QueryTriggerInteraction.Ignore)
            && !IsWalkThroughDetail(sweepHit.collider) && Vector3.Dot(sweepHit.normal, heading) < -.2f && sweepHit.normal.y < .65f)
        {
            return true;
        }
        if (nativeGaitCapsule == null || !nativeGaitCapsule.enabled)
        {
            return false;
        }

        // 贴面时从后方回探，使胶囊起点避开接触公差。
        const float retreat = .08f;
        var bounds = nativeGaitCapsule.bounds;
        float radius = Mathf.Max(.01f, Mathf.Min(bounds.extents.x, bounds.extents.z) - .01f);
        var center = bounds.center - heading * retreat + Vector3.up * .025f;
        var halfSegment = Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
        int count = Physics.CapsuleCastNonAlloc(center - halfSegment, center + halfSegment, radius, heading,
            nativeGaitHits, clearance + retreat, ~0, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            var hit = nativeGaitHits[index];
            if (hit.collider.attachedRigidbody != body && hit.normal.y < .65f
                && Vector3.Dot(hit.normal, heading) < -.2f
                && !Physics.GetIgnoreLayerCollision(nativeGaitCapsule.gameObject.layer, hit.collider.gameObject.layer)
                && !Physics.GetIgnoreCollision(nativeGaitCapsule, hit.collider))
            {
                return true;
            }
        }
        return false;
    }

    internal void ClearNativeGaitObstruction()
    {
        nativeGaitIncomingVelocity = Vector3.zero;
        nativeStepDistance = 0f;
        nativeBlockedTime = 0f;
        nativeBlocked = false;
        nativeBlockedHeading = Vector3.zero;
    }
}
