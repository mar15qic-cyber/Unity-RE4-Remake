using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10030)]
[RequireComponent(typeof(VillagerController))]
public sealed class EnemyHeldWeapon : MonoBehaviour
{
    [SerializeField] private BoxCollider droppedCollider;
    [SerializeField] private RuntimeAnimatorController unarmedController;
    [SerializeField] private AnimationClip disarmMotion;
    [Tooltip("持械敌人成功命中时的原版武器声")]
    public AudioEventData hitAudio;

    private VillagerController owner;
    private Animator animator;
    private bool pendingDisarmRelease;
    private Transform gripSocket;
    private Vector3 heldSocketPosition;
    private Quaternion heldSocketRotation;
    private Transform[] gripFingers;
    private Quaternion[] gripFingerRotations;
    private float revivedGripWeight;

    internal bool IsHeld => droppedCollider != null;
    internal Transform WeaponTransform
    {
        get
        {
            if (droppedCollider != null)
            {
                return droppedCollider.transform;
            }
            return null;
        }
    }

    internal void PlayHitAudio(Vector3 position)
    {
        if (IsHeld && hitAudio != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3D(hitAudio, position);
        }
    }
    private Vector3 lastPosition;
    private Vector3 velocity;
    private bool hasPosition;

#if UNITY_EDITOR
    internal void Configure(BoxCollider weaponCollider, RuntimeAnimatorController emptyController, AnimationClip disarm)
    {
        droppedCollider = weaponCollider;
        unarmedController = emptyController;
        disarmMotion = disarm;
    }
#endif

    private void OnEnable()
    {
        owner = GetComponent<VillagerController>();
        animator = GetComponent<Animator>();
        owner.Died += Drop;
        pendingDisarmRelease = false;
        hasPosition = false;
        velocity = Vector3.zero;
        revivedGripWeight = 0f;
    }

    private void Start()
    {
        if (droppedCollider != null && droppedCollider.transform.parent != null
            && droppedCollider.transform.parent.name == "R_Wep")
        {
            gripSocket = droppedCollider.transform.parent;
            heldSocketPosition = gripSocket.localPosition;
            heldSocketRotation = gripSocket.localRotation;
            var fingers = new System.Collections.Generic.List<Transform>();
            foreach (var bone in gripSocket.parent.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.StartsWith("R_Thumb", System.StringComparison.Ordinal)
                    || bone.name.StartsWith("R_IndexF", System.StringComparison.Ordinal)
                    || bone.name.StartsWith("R_MiddleF", System.StringComparison.Ordinal)
                    || bone.name.StartsWith("R_RingF", System.StringComparison.Ordinal)
                    || bone.name.StartsWith("R_PinkyF", System.StringComparison.Ordinal))
                {
                    fingers.Add(bone);
                }
            }
            gripFingers = fingers.ToArray();
            gripFingerRotations = new Quaternion[gripFingers.Length];
            CaptureHeldGrip();
        }
    }

    private void CaptureHeldGrip()
    {
        heldSocketPosition = gripSocket.localPosition;
        heldSocketRotation = gripSocket.localRotation;
        for (int i = 0; i < gripFingers.Length; i++)
        {
            gripFingerRotations[i] = gripFingers[i].localRotation;
        }
    }

    private void ApplyRevivedGrip()
    {
        if (gripSocket == null || droppedCollider.transform.parent != gripSocket || owner.IsDead
            || owner.IsQTEControlled || owner.IsNavigationTraversalProtected || pendingDisarmRelease)
        {
            revivedGripWeight = 0f;
            return;
        }

        var life = owner.healthModule.LifePhase;
        bool standingGrip = owner.CurrentState == VillagerState.Idle || owner.CurrentState == VillagerState.Patrol
            || owner.CurrentState == VillagerState.Chase;
        if (life == GameArchitecture.EnemyLifePhase.Alive && standingGrip && !owner.HasPendingHit
            && !owner.combatModule.NativeCombatActive && !animator.IsInTransition(0))
        {
            CaptureHeldGrip();
        }

        float target = 0f;
        if (life == GameArchitecture.EnemyLifePhase.Reviving
            || life == GameArchitecture.EnemyLifePhase.Revived && standingGrip && !owner.HasPendingHit
                && !owner.combatModule.NativeCombatActive)
        {
            target = 1f;
        }

        if (life != GameArchitecture.EnemyLifePhase.Reviving && (owner.HasPendingHit
            || owner.CurrentState == VillagerState.Attack || owner.CurrentState == VillagerState.CrawlAttack))
        {
            revivedGripWeight = 0f;
        }
        else
        {
            revivedGripWeight = Mathf.MoveTowards(revivedGripWeight, target, 18f * Time.deltaTime);
        }
        if (revivedGripWeight <= 0f)
        {
            return;
        }

        gripSocket.SetLocalPositionAndRotation(Vector3.Lerp(gripSocket.localPosition, heldSocketPosition, revivedGripWeight),
            Quaternion.Slerp(gripSocket.localRotation, heldSocketRotation, revivedGripWeight));
        for (int i = 0; i < gripFingers.Length; i++)
        {
            var finger = gripFingers[i];
            finger.localRotation = Quaternion.Slerp(finger.localRotation, gripFingerRotations[i], revivedGripWeight);
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.Died -= Drop;
        }
    }

    public void OnDisarmRelease(AnimationEvent marker)
    {
        if (droppedCollider != null && marker.isFiredByAnimator && marker.animatorClipInfo.clip == disarmMotion
            && CanReleaseDisarm())
        {
            pendingDisarmRelease = true;
        }
    }

    private bool CanReleaseDisarm()
    {
        return !owner.IsDead && !owner.IsQTEControlled && owner.CurrentState == VillagerState.Staggered
            && owner.combatModule.ReadPendingHit
            && owner.combatModule.ReadPendingHitDirection == EnemyCombatModule.ArmedDisarmHitDirection;
    }

    private void LateUpdate()
    {
        if (droppedCollider == null || Time.deltaTime <= 0f)
        {
            return;
        }

        ApplyRevivedGrip();
        Vector3 position = droppedCollider.transform.position;
        if (hasPosition)
        {
            velocity = (position - lastPosition) / Time.deltaTime;
        }
        lastPosition = position;
        hasPosition = true;

        if (!pendingDisarmRelease)
        {
            return;
        }
        pendingDisarmRelease = false;
        if (CanReleaseDisarm())
        {
            // 两个覆盖器共用状态图和缴械片段，保留当前动作时钟。
            Drop(owner);
            animator.runtimeAnimatorController = unarmedController;
            owner.combatModule.SwitchToUnarmedAttacks();
            owner.animationPresenter.SwitchToUnarmedGanadoGait();
        }
    }

    private void Drop(VillagerController deadOwner)
    {
        if (droppedCollider == null)
        {
            return;
        }

        BoxCollider released = droppedCollider;
        droppedCollider = null;
        released.transform.SetParent(null, true);
        released.enabled = true;
        foreach (Collider ownerCollider in deadOwner.GetComponentsInChildren<Collider>())
        {
            if (ownerCollider.enabled && !ownerCollider.isTrigger)
            {
                Physics.IgnoreCollision(released, ownerCollider);
            }
        }

        var body = released.gameObject.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = Vector3.ClampMagnitude(velocity, 3f);
        Destroy(released.gameObject, 18f);
    }
}
