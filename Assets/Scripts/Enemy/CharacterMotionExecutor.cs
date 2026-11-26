using UnityEngine;
using GameArchitecture;

/// <summary>敌人根对象位姿的唯一提交入口。碰撞计算和更新阶段沿用原执行流程。</summary>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "角色运动执行器")]
public sealed class CharacterMotionExecutor : MonoBehaviour
{
    /// <summary>宿主。</summary>
    [Header("角色引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("宿主")]
    [UnityEngine.InspectorName("宿主")]
    public VillagerController host;
    /// <summary>当前模式。</summary>
    private EnemyMovementMode currentMode = EnemyMovementMode.FullyStopped;
    /// <summary>控制凭证。</summary>
    private MotionControlToken controlToken;
    /// <summary>交接编号。</summary>
    private long handoffId;
    /// <summary>刚体。</summary>
    private Rigidbody rigidbody;
    /// <summary>最新实际位移。</summary>
    private Vector3 latestActualDisplacement;
    /// <summary>模式。</summary>
    public EnemyMovementMode Mode => currentMode;
    /// <summary>当前凭证。</summary>
    public MotionControlToken CurrentToken => controlToken;
    /// <summary>中文含义：实际位移。</summary>
    public Vector3 ActualDisplacement => latestActualDisplacement;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Rigidbody&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>BindingRigidbody 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reference">引用。</param>
    internal void BindingRigidbody(Rigidbody reference)
    {
        rigidbody = reference;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.Revoke()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        Revoke();
    }

    /// <summary>Authorize 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.Revoke()、MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyActionCoordinator.RegisterAction()；下游（含内部回调体） CharacterMotionExecutor.Revoke()、MotionControlToken.MotionControlToken(ActionHandle, MotionSource, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">当前敌人移动模式。</param>
    /// <param name="action">动作。</param>
    internal void Authorize(EnemyMovementMode mode, ActionHandle action)
    {
        if (host == null || !action.Valid || !isActiveAndEnabled)
        {
            return;
        }

        if (host.IsDead && host.CurrentState != VillagerState.Dead && (host.ArchitectureReadQteParticipant == null || !host.ArchitectureReadQteParticipant.IsPreservingAssassinationDeathPose))
        {
            return;
        }

        // 来源运动来源。
global::GameArchitecture.MotionSource source;
        if (host.IsQTEControlled)
        {
            source = MotionSource.PairedActionAction;
        }
        else
        {
            if (host.IsDead)
            {
                source = MotionSource.Death;
            }
            else
            {
                if (mode == EnemyMovementMode.TraversalCodeDriven)
                {
                    source = MotionSource.Traversal;
                }
                else
                {
                    if (mode == EnemyMovementMode.ActionRootMotion || mode == EnemyMovementMode.FullRootMotion)
                    {
                        source = MotionSource.AttackRootMotion;
                    }
                    else
                    {
                        if (host.DoorwayTraverseMiddle)
                        {
                            source = MotionSource.Traversal;
                        }
                        else
                        {
                            source = MotionSource.NormalMovement;
                        }
                    }
                }
            }
        }

        if (controlToken.Valid && controlToken.Action == action && controlToken.Source == source && currentMode == mode)
        {
            return;
        }

        // 先作废旧凭证，再登记新来源；旧回调不能借同一模式复用旧授权。
        Revoke();
        currentMode = mode;
        controlToken = new MotionControlToken(action, source, ++handoffId);
    }

    /// <summary>Revoke 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、CharacterMotionExecutor.OnDisable()、EnemyActionCoordinator.CloseAction()、VillagerController.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void Revoke()
    {
        controlToken = default;
        latestActualDisplacement = Vector3.zero;
    }

    /// <summary>Valid 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    public bool Valid(MotionControlToken token)
    {
        return isActiveAndEnabled && host != null && host.isActiveAndEnabled && (!host.IsDead || token.Source == MotionSource.Death || token.Source == MotionSource.PairedActionAction && host.ArchitectureReadQteParticipant != null && host.ArchitectureReadQteParticipant.IsPreservingAssassinationDeathPose) && token.Valid && controlToken.Valid && token.Action == controlToken.Action && token.Source == controlToken.Source && token.HandoffId == controlToken.HandoffId && host.actionCoordinator != null && token.Action == host.actionCoordinator.CurrentHandle;
    }

    /// <summary>CommitPose 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.Valid(MotionControlToken)、NavigationCollisionCache.MarkerPoseChange()、Quaternion.Equals(Quaternion)、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Equals(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.CommitActionPose(Vector3, Quaternion)、CharacterMotionExecutor.CommitOrientation(MotionControlToken, Quaternion)、CharacterMotionExecutor.CommitPosition(MotionControlToken, Vector3)、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、VillagerController.RecoverInvalidNavigationSpawn() 等共 6 处；下游（含内部回调体） CharacterMotionExecutor.Valid(MotionControlToken)、NavigationCollisionCache.MarkerPoseChange()、Quaternion.Equals(Quaternion)、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Equals(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="positionValue">位置值。</param>
    /// <param name="orientation">朝向。</param>
    /// <param name="synchronizeRigidbody">是否同步刚体。</param>
    public bool CommitPose(MotionControlToken token, Vector3 positionValue, Quaternion orientation, bool synchronizeRigidbody = false)
    {
        if (!Valid(token))
        {
            return false;
        }

        if (host.knifeLocalHitStop != null && host.knifeLocalHitStop.IsHitStop)
        {
            return false;
        }

        latestActualDisplacement = positionValue - transform.position;
        if (synchronizeRigidbody && rigidbody != null)
        {
            rigidbody.position = positionValue;
            rigidbody.rotation = orientation;
        }

        if (!transform.position.Equals(positionValue) || !transform.rotation.Equals(orientation))
        {
            NavigationCollisionCache.MarkerPoseChange();
        }

        transform.SetPositionAndRotation(positionValue, orientation);
        return true;
    }

    /// <summary>CommitPosition 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.CommitNormalPosition(Vector3)；下游（含内部回调体） CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="position">位置。</param>
    public bool CommitPosition(MotionControlToken token, Vector3 position) => CommitPose(token, position, transform.rotation);
    /// <summary>CommitOrientation 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)；下游（含内部回调体） CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="orientation">朝向。</param>
    public bool CommitOrientation(MotionControlToken token, Quaternion orientation) => CommitPose(token, transform.position, orientation);
    // 同步兼容桥仅供当前角色的原运动算法使用；跨帧流程必须保存并提交凭证。
    /// <summary>CommitNormalPosition 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPosition(MotionControlToken, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.OnAnimatorMove()、VillagerController.ResolveCrowdOverlap()、VillagerController.ResolveMovementOverlap(bool)、VillagerController.SnapToGround(float, float, bool) 等共 6 处；下游（含内部回调体） CharacterMotionExecutor.CommitPosition(MotionControlToken, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    internal bool CommitNormalPosition(Vector3 position)
    {
        MotionControlToken token = controlToken;
        if (currentMode == EnemyMovementMode.TraversalCodeDriven || !Valid(token))
        {
            return false;
        }

        if (token.Source == MotionSource.NormalMovement || token.Source == MotionSource.AttackRootMotion)
        {
            position = host.ConstrainSupportedEnemyMotion(position);
        }
        return CommitPosition(token, position);
    }
    /// <summary>CommitNormalOrientation 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitOrientation(MotionControlToken, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.UpdateStationaryPatrolFacing(Quaternion)、VillagerController.FacePlayerImmediately()、VillagerController.FacePosition(Vector3, float)、VillagerController.RotateTowardsDirection(Vector3, float)、VillagerController.UpdateCrawlFacingBeforeAnimator()；下游（含内部回调体） CharacterMotionExecutor.CommitOrientation(MotionControlToken, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="orientation">朝向。</param>
    internal bool CommitNormalOrientation(Quaternion orientation) => currentMode != EnemyMovementMode.TraversalCodeDriven && CommitOrientation(controlToken, orientation);
    /// <summary>CommitActionPose 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.EndAttack()；下游（含内部回调体） CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="orientation">朝向。</param>
    internal bool CommitActionPose(Vector3 position, Quaternion orientation) => controlToken.Source != MotionSource.Traversal && CommitPose(controlToken, position, orientation);
}
