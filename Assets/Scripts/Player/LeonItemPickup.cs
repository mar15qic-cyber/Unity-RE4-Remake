using GameArchitecture;
using RE4.ChapterOne;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(10001)]
public sealed class LeonItemPickup : MonoBehaviour
{
    private LeonController player;
    private LeonAnimationActionCoordinator actions;
    private LeonAnimatorDriver driver;
    private LeonActionMotionDriver motion;
    private LeonInputBridge inputBridge;
    private MotionControlToken motionToken;
    private ItemPickup target;
    private ItemData firstInspectionItem;
    private int transactionId;
    private int pickupHeight;
    private float contactTime;
    private float requestedAt;
    private bool processedContact;
    private bool movingPickup;
    private float fadeCarrySpeed;
    internal float MovingEntrySpeed { get; private set; }
    private ChapterOneArmAim.Arm pickupArm;
    private Transform pickupFinger;
    private Transform pickupUpper;
    private Transform pickupLower;
    private Transform rightScapula;
    private Transform rightShoulder;
    private Transform rightUpperArm;
    private Transform rightForearm;
    private Transform spineUpper;
    private Quaternion previousRightScapula;
    private Quaternion previousRightShoulder;
    private Quaternion previousRightUpperArm;
    private Quaternion previousRightForearm;
    private Quaternion solvedRightScapula;
    private Quaternion solvedRightShoulder;
    private Quaternion solvedRightUpperArm;
    private Quaternion solvedRightForearm;
    private bool rightArmRelaxApplied;
    private Quaternion previousUpper;
    private Quaternion previousLower;
    private Quaternion previousWrist;
    private Quaternion solvedUpper;
    private Quaternion solvedLower;
    private Quaternion solvedWrist;
    private bool reachApplied;
    private float reachWeight;
    private float releasedReachWeight;
    private float reachReleasedAt = float.NegativeInfinity;
    private Quaternion reachRotation;
    private Vector3 reachStart;
    private Vector3 reachEnd;
    private Vector3 itemContact;
    private const int WorldCollisionMask = 1 << 0;
    private const float PickupSourceFrameRate = 60f;
    private const float EarlyCancelRecoveryTime = 14f / PickupSourceFrameRate;
    private const float StandingLowNaturalRecoveryTime = 26f / PickupSourceFrameRate;
    private const float StandingMiddleNaturalRecoveryTime = 34f / PickupSourceFrameRate;
    private const float StandingHighNaturalRecoveryTime = 24f / PickupSourceFrameRate;
    private const float MovingLowNaturalRecoveryTime = 9f / PickupSourceFrameRate;
    private const float MovingMiddleNaturalRecoveryTime = 18f / PickupSourceFrameRate;
    private const float MovingHighNaturalRecoveryTime = 16f / PickupSourceFrameRate;

    internal bool IsMovingPickup
    {
        get
        {
            if (transactionId != 0 && movingPickup)
            {
                return true;
            }

            return false;
        }
    }

    internal bool IsPickupMotionProtected
    {
        get
        {
            if (transactionId != 0)
            {
                return true;
            }

            if (driver != null && driver.IsItemPickupLayerBlending)
            {
                return true;
            }

            return false;
        }
    }

    internal float ReadPickupSpeedFloor(float desiredPlanarSpeed)
    {
        if (desiredPlanarSpeed <= 0.01f)
        {
            return 0f;
        }

        if (transactionId != 0 && movingPickup)
        {
            MovingEntrySpeed = Mathf.Max(MovingEntrySpeed, desiredPlanarSpeed);
            return MovingEntrySpeed;
        }

        if (driver != null && driver.IsItemPickupLayerBlending)
        {
            float layerWeight = driver.ItemPickupLayerWeight;
            if (layerWeight > 0.001f)
            {
                float carrySpeed = Mathf.Max(fadeCarrySpeed, desiredPlanarSpeed);
                return Mathf.Lerp(desiredPlanarSpeed, carrySpeed, Mathf.SmoothStep(0f, 1f, layerWeight));
            }
        }

        return 0f;
    }

    private void Start()
    {
        player = GetComponent<LeonController>();
        actions = GetComponent<LeonAnimationActionCoordinator>();
        driver = GetComponent<LeonAnimatorDriver>();
        motion = GetComponent<LeonActionMotionDriver>();
        inputBridge = GetComponent<LeonInputBridge>();
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "arm left finger index b")
            {
                pickupFinger = bone;
            }
            else if (bone.name == "arm left wrist")
            {
                pickupLower = bone.parent;
                pickupUpper = pickupLower.parent;
                pickupArm = new ChapterOneArmAim.Arm(bone);
            }
            else if (bone.name == "spine upper")
            {
                spineUpper = bone;
            }
            else if (bone.name == "arm right scapula")
            {
                rightScapula = bone;
            }
            else if (bone.name == "arm right shoulder")
            {
                rightShoulder = bone;
            }
            else if (bone.name == "arm right upperarm")
            {
                rightUpperArm = bone;
            }
            else if (bone.name == "arm right forearm")
            {
                rightForearm = bone;
            }
        }
        actions.OnTraversalActionCompleted += Complete;
        actions.OnTraversalActionInterrupted += Interrupt;
        actions.OnTraversalActionExited += Interrupt;
    }

    private void OnDisable()
    {
        RestoreRightArmRelaxation();
        DiscardBufferedInput();
        if (OwnsTransaction())
        {
            actions.CancelTraversal();
        }
        Finish();
    }

    private void OnDestroy()
    {
        if (actions != null)
        {
            actions.OnTraversalActionCompleted -= Complete;
            actions.OnTraversalActionInterrupted -= Interrupt;
            actions.OnTraversalActionExited -= Interrupt;
        }
    }

    internal bool CanReach(ItemPickup pickup)
    {
        return transactionId == 0 && player != null && player.CanAct() && CanReachAfterAction(pickup);
    }

    internal bool CanReachAfterAction(ItemPickup pickup)
    {
        if (player == null || pickup == null || !pickup.isActiveAndEnabled || !player.IsGrounded || player.IsAiming)
        {
            return false;
        }

        Vector3 offset = pickup.ContactPoint - transform.position;
        if (offset.y < -.45f || offset.y > 1.95f ||
            Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude > pickup.InteractRange * pickup.InteractRange)
        {
            return false;
        }

        return HasClearReach(pickup.ContactPoint);
    }

    public bool TryBegin(ItemPickup pickup)
    {
        if (!CanReach(pickup) || (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput))
        {
            return false;
        }

        itemContact = pickup.ContactPoint;
        Vector3 offset = itemContact - transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(offset, Vector3.up);
        int height = 0;
        contactTime = 12f / PickupSourceFrameRate;
        Vector3 sourceContact = new Vector3(.0005f, .5705f, .6568f);
        if (offset.y >= 1.14f)
        {
            height = 2;
            sourceContact = new Vector3(.3104f, 1.3924f, .6595f);
        }
        else if (offset.y >= .69f)
        {
            height = 1;
            contactTime = 16f / PickupSourceFrameRate;
            sourceContact = new Vector3(.1139f, .9686f, .6851f);
        }

        Vector3 velocity = Vector3.ProjectOnPlane(player.HorizontalSpeed, Vector3.up);
        bool hasEffectiveMove = false;
        if (inputBridge != null && inputBridge.ReadEffectiveMoveInput().sqrMagnitude > 0.01f)
        {
            hasEffectiveMove = true;
            if (player.MoveInput.sqrMagnitude <= 0.01f)
            {
                inputBridge.RestoreMovementInput();
            }
        }

        pickupHeight = height;
        bool moving = player.MoveInput.sqrMagnitude > 0.01f || hasEffectiveMove;
        if (moving)
        {
            // 移动取物继续使用正常位移，伸手轨迹随物品位置修正。
            if (!actions.RequestItemPickup(height, true))
            {
                return false;
            }
            if (height == 0)
            {
                contactTime = 11f / PickupSourceFrameRate;
            }
            movingPickup = true;
            MovingEntrySpeed = velocity.magnitude;
            fadeCarrySpeed = 0f;
            transactionId = actions.CurrentFullBodyTransactionId;
            target = pickup;
            requestedAt = Time.time;
            return true;
        }

        reachStart = transform.position;
        reachRotation = transform.rotation;
        if (direction.sqrMagnitude > .015f)
        {
            float sourceYaw = Mathf.Atan2(sourceContact.x, sourceContact.z) * Mathf.Rad2Deg;
            reachRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, -sourceYaw, 0f);
        }

        Vector3 alignment = Vector3.ProjectOnPlane(itemContact - reachRotation * sourceContact - reachStart, Vector3.up);
        alignment = Vector3.ClampMagnitude(alignment, .35f);
        if (alignment.sqrMagnitude > .0001f)
        {
            Vector3 lower = reachStart + Vector3.up * .32f;
            Vector3 upper = reachStart + Vector3.up * 1.38f;
            if (Physics.CapsuleCast(lower, upper, .27f, alignment.normalized, out RaycastHit obstacle,
                alignment.magnitude, WorldCollisionMask, QueryTriggerInteraction.Ignore))
            {
                alignment = alignment.normalized * Mathf.Max(0f, obstacle.distance - .035f);
            }
        }

        reachEnd = reachStart + alignment;
        if (!actions.RequestItemPickup(height, false))
        {
            return false;
        }

        transactionId = actions.CurrentFullBodyTransactionId;
        motionToken = motion.AuthorizeMotion(MotionSource.Traversal, transactionId);
        if (!motionToken.Valid)
        {
            actions.CancelTraversal();
            return false;
        }

        target = pickup;
        requestedAt = Time.time;
        player.SetFreeLooking(false);
        player.ChangeState(LeonState.Traversing);
        motion.StopHorizontalSpeed(motionToken);

        return true;
    }

    private void FixedUpdate()
    {
        if (transactionId == 0)
        {
            return;
        }

        if (!OwnsTransaction() || !movingPickup && !motion.TokenValid(motionToken))
        {
            DiscardBufferedInput();
            Finish();
            return;
        }

        bool inputBlocked = GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput;
        if (!player.IsGrounded || inputBlocked || movingPickup && (player.IsAiming || !player.CanAct()))
        {
            actions.CancelTraversal();
            return;
        }
        if (!movingPickup)
        {
            Quaternion next = Quaternion.RotateTowards(transform.rotation, reachRotation, 720f * Time.fixedDeltaTime);
            if (!processedContact)
            {
                float alignment = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - requestedAt) / .18f));
                Vector3 position = Vector3.Lerp(reachStart, reachEnd, alignment);
                position.y = transform.position.y;
                motion.CommitTraversalPose(motionToken, position, next, true);
            }
        }
        if (driver.TryReadItemPickupTime(out float elapsed))
        {
            if (elapsed >= contactTime)
            {
                if (!CommitContact())
                {
                    actions.CancelTraversal();
                    return;
                }

                // 物品在接触帧入包，有持续方向或战斗输入时走早取消恢复窗口，无输入时完整播完自然收手。
                bool earlyCancel = HasCancelIntent();
                float activeRecoveryTime;
                if (earlyCancel)
                {
                    activeRecoveryTime = EarlyCancelRecoveryTime;
                }
                else
                {
                    activeRecoveryTime = ReadNaturalRecoveryTime();
                }

                if (elapsed >= contactTime + activeRecoveryTime)
                {
                    actions.CompleteItemPickup(transactionId, earlyCancel);
                }
            }
        }
        else if (Time.time - requestedAt > .8f)
        {
            actions.CancelTraversal();
        }
    }

    private float ReadNaturalRecoveryTime()
    {
        if (movingPickup)
        {
            if (pickupHeight == 2)
            {
                return MovingHighNaturalRecoveryTime;
            }
            if (pickupHeight == 1)
            {
                return MovingMiddleNaturalRecoveryTime;
            }
            return MovingLowNaturalRecoveryTime;
        }

        if (pickupHeight == 2)
        {
            return StandingHighNaturalRecoveryTime;
        }
        if (pickupHeight == 1)
        {
            return StandingMiddleNaturalRecoveryTime;
        }
        return StandingLowNaturalRecoveryTime;
    }

    private bool HasCancelIntent()
    {
        if (player != null && player.MoveInput.sqrMagnitude > 0.01f)
        {
            return true;
        }

        if (inputBridge != null)
        {
            if (inputBridge.ReadEffectiveMoveInput().sqrMagnitude > 0.01f)
            {
                return true;
            }
            if (inputBridge.IsEffectiveAimPressed())
            {
                return true;
            }
        }

        return false;
    }

    private bool CommitContact()
    {
        if (processedContact)
        {
            return true;
        }

        if (target == null || !target.isActiveAndEnabled ||
            (target.ContactPoint - itemContact).sqrMagnitude > .09f || !HasClearReach(target.ContactPoint))
        {
            return false;
        }

        // 移动拾取沿用接受交互时的距离，避免动画接触帧另用更短距离拒绝。
        if (movingPickup && Vector3.ProjectOnPlane(target.ContactPoint - transform.position, Vector3.up).sqrMagnitude > target.InteractRange * target.InteractRange)
        {
            return false;
        }

        ItemData item = target.item;
        processedContact = target.TryCollect(out bool firstAcquisition);
        if (processedContact && firstAcquisition)
        {
            firstInspectionItem = item;
        }
        return processedContact;
    }

    internal void RestoreBoneOverridesBeforeDriver()
    {
        RestoreRightArmRelaxation();
        if (pickupArm != null && pickupFinger != null && reachApplied)
        {
            if (pickupUpper.localRotation.Equals(solvedUpper))
            {
                pickupUpper.localRotation = previousUpper;
            }
            if (pickupLower.localRotation.Equals(solvedLower))
            {
                pickupLower.localRotation = previousLower;
            }
            if (pickupArm.wrist.localRotation.Equals(solvedWrist))
            {
                pickupArm.wrist.localRotation = previousWrist;
            }
            reachApplied = false;
        }
    }

    private void LateUpdate()
    {
        RestoreBoneOverridesBeforeDriver();
        if (pickupArm != null && pickupFinger != null)
        {

            float weight = 0f;
            if (OwnsTransaction() && driver != null && driver.TryReadItemPickupTime(out float elapsed))
            {
                float activeRecoveryTime;
                if (HasCancelIntent())
                {
                    activeRecoveryTime = EarlyCancelRecoveryTime;
                }
                else
                {
                    activeRecoveryTime = ReadNaturalRecoveryTime();
                }

                float rise = Mathf.InverseLerp(1f / PickupSourceFrameRate, contactTime, elapsed);
                float fade = 1f - Mathf.InverseLerp(contactTime, contactTime + activeRecoveryTime, elapsed);
                weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(rise, fade));
            }
            else if (Time.time - reachReleasedAt < .12f && player != null && player.CanAct()
                && !player.IsAiming && actions != null && !actions.HasActiveFullBodyAction)
            {
                // 输入可以立即恢复，手臂修正仍平滑交还当前动画，避免接触帧直接清零。
                weight = releasedReachWeight * (1f - Mathf.SmoothStep(0f, 1f, (Time.time - reachReleasedAt) / .12f));
            }

            reachWeight = weight;
            if (weight > 0f)
            {
                Vector3 correction = Vector3.ClampMagnitude(itemContact - pickupFinger.position, 0.46f);
                previousUpper = pickupUpper.localRotation;
                previousLower = pickupLower.localRotation;
                previousWrist = pickupArm.wrist.localRotation;
                pickupArm.Solve(pickupArm.wrist.position + correction * weight, pickupArm.wrist.rotation, -transform.up);
                solvedUpper = pickupUpper.localRotation;
                solvedLower = pickupLower.localRotation;
                solvedWrist = pickupArm.wrist.localRotation;
                reachApplied = true;
            }
        }

        float rightArmRelaxWeight = 0f;
        if (driver != null && driver.IsItemPickupLayerBlending && player != null
            && player.currentState != LeonState.Dead && !player.IsHeldByTrap)
        {
            rightArmRelaxWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(driver.ItemPickupLayerWeight));
        }

        if (rightArmRelaxWeight > 0.001f)
        {
            ApplyRightArmPickupRelaxation(rightArmRelaxWeight);
        }
    }

    private void RestoreRightArmRelaxation()
    {
        if (!rightArmRelaxApplied)
        {
            return;
        }

        if (rightForearm != null && rightForearm.localRotation.Equals(solvedRightForearm))
        {
            rightForearm.localRotation = previousRightForearm;
        }
        if (rightUpperArm != null && rightUpperArm.localRotation.Equals(solvedRightUpperArm))
        {
            rightUpperArm.localRotation = previousRightUpperArm;
        }
        if (rightShoulder != null && rightShoulder.localRotation.Equals(solvedRightShoulder))
        {
            rightShoulder.localRotation = previousRightShoulder;
        }
        if (rightScapula != null && rightScapula.localRotation.Equals(solvedRightScapula))
        {
            rightScapula.localRotation = previousRightScapula;
        }

        rightArmRelaxApplied = false;
    }

    private void ApplyRightArmPickupRelaxation(float weight)
    {
        if (player == null || rightUpperArm == null)
        {
            return;
        }

        // 低/中位拾取弯腰幅度更大，高位伸手仅需轻微缓解肩臂悬空。
        float heightScale = 0.85f;
        if (pickupHeight == 0)
        {
            heightScale = 1.0f;
        }
        else if (pickupHeight == 2)
        {
            heightScale = 0.6f;
        }

        float effective = Mathf.Clamp01(weight) * heightScale;
        if (effective <= 0.001f)
        {
            return;
        }

        WeaponType weapon = player.currentWeapon;
        bool isLongGun = weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;

        if (rightScapula != null)
        {
            previousRightScapula = rightScapula.localRotation;
            // 右肩胛随躯干微沉微扣，减轻悬空僵硬感。
            Quaternion scapulaOffset = Quaternion.AngleAxis(3.5f * effective, transform.right)
                * Quaternion.AngleAxis(-2.5f * effective, transform.forward);
            rightScapula.rotation = scapulaOffset * rightScapula.rotation;
            solvedRightScapula = rightScapula.localRotation;
        }

        if (rightShoulder != null)
        {
            previousRightShoulder = rightShoulder.localRotation;
            float shoulderAbductDeg;
            if (isLongGun)
            {
                shoulderAbductDeg = 3.5f;
            }
            else
            {
                shoulderAbductDeg = -2.0f;
            }
            Quaternion shoulderOffset = Quaternion.AngleAxis(4.0f * effective, transform.right)
                * Quaternion.AngleAxis(shoulderAbductDeg * effective, transform.forward);
            rightShoulder.rotation = shoulderOffset * rightShoulder.rotation;
            solvedRightShoulder = rightShoulder.localRotation;
        }

        previousRightUpperArm = rightUpperArm.localRotation;
        if (isLongGun)
        {
            // 双手长枪左臂伸出后改为单手微低持，适度外展避开胸口与腿部碰撞。
            Quaternion longGunUpperOffset = Quaternion.AngleAxis(9.5f * effective, transform.right)
                * Quaternion.AngleAxis(7.5f * effective, transform.forward)
                * Quaternion.AngleAxis(3.5f * effective, transform.up);
            rightUpperArm.rotation = longGunUpperOffset * rightUpperArm.rotation;
        }
        else
        {
            // 手枪/小刀单手自然垂落与轻度内收，避免端臂悬空。
            Quaternion sidearmUpperOffset = Quaternion.AngleAxis(13.0f * effective, transform.right)
                * Quaternion.AngleAxis(-4.5f * effective, transform.up);
            rightUpperArm.rotation = sidearmUpperOffset * rightUpperArm.rotation;
        }
        solvedRightUpperArm = rightUpperArm.localRotation;

        if (rightForearm != null)
        {
            previousRightForearm = rightForearm.localRotation;
            float forearmRelaxDeg;
            if (isLongGun)
            {
                forearmRelaxDeg = 5.0f;
            }
            else
            {
                forearmRelaxDeg = 8.5f;
            }
            Quaternion forearmOffset = Quaternion.AngleAxis(forearmRelaxDeg * effective, transform.right);
            rightForearm.rotation = forearmOffset * rightForearm.rotation;
            solvedRightForearm = rightForearm.localRotation;
        }

        rightArmRelaxApplied = true;
    }

    private bool HasClearReach(Vector3 point)
    {
        float height = Mathf.Clamp(point.y - transform.position.y + .12f, .55f, 1.55f);
        Vector3 from = transform.position + Vector3.up * height;
        return !Physics.Linecast(from, point + Vector3.up * .055f, WorldCollisionMask, QueryTriggerInteraction.Ignore);
    }

    private bool OwnsTransaction()
    {
        return transactionId != 0 && actions != null && actions.HasActiveFullBodyAction &&
            actions.CurrentFullBodyTransactionId == transactionId;
    }

    private void Complete(AnimationActionTransaction transaction)
    {
        if (transactionId != 0 && transaction.transactionId == transactionId)
        {
            bool collected = CommitContact();
            int completedId = transactionId;
            ItemData inspection = firstInspectionItem;
            if (!collected)
            {
                DiscardBufferedInput();
            }
            Finish();
            bool openedInspection = inspection != null && ItemInspectionUI.HasInstance && ItemInspectionUI.Instance.Show(inspection);
            if (collected && inputBridge != null)
            {
                if (openedInspection)
                {
                    inputBridge.DiscardPendingPickupInput(completedId);
                }
                else
                {
                    inputBridge.ReleaseBufferedPickupInput(completedId);
                }
            }
        }
    }

    private void Interrupt(AnimationActionTransaction transaction)
    {
        if (transactionId != 0 && transaction.transactionId == transactionId)
        {
            DiscardBufferedInput();
            Finish();
        }
    }

    private void Finish()
    {
        if (transactionId == 0)
        {
            return;
        }

        if (movingPickup)
        {
            fadeCarrySpeed = MovingEntrySpeed;
        }
        else
        {
            fadeCarrySpeed = 0f;
        }

        releasedReachWeight = reachWeight;
        reachReleasedAt = Time.time;
        transactionId = 0;
        if (!movingPickup && motion != null)
        {
            motion.ReleaseMotion(motionToken);
        }
        if (!movingPickup && driver != null)
        {
            driver.ReleaseNativeGait(false);
        }
        motionToken = default;
        if (!movingPickup && player != null)
        {
            player.SyncNormalMovementAuthorityImmediate();
            if (player.currentState == LeonState.Traversing)
            {
                if (player.MoveInput.sqrMagnitude > .01f)
                {
                    player.ChangeState(LeonState.Walking);
                }
                else
                {
                    player.ChangeState(LeonState.Idle);
                }
            }
        }

        target = null;
        firstInspectionItem = null;
        processedContact = false;
        movingPickup = false;
        MovingEntrySpeed = 0f;
    }

    private void DiscardBufferedInput()
    {
        if (inputBridge != null)
        {
            inputBridge.DiscardPendingPickupInput(transactionId);
        }
    }

}

