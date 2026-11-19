using GameArchitecture;
using RE4.ChapterOne;
using UnityEngine;
using System.Collections.Generic;

public sealed class VillageEntranceGate : MonoBehaviour, IInteractable
{
    [Header("铁门原生铰链")]
    [Tooltip("从出生侧朝村庄观察时的两扇门叶。")]
    public Transform leftLeaf;
    public Transform rightLeaf;
    [Tooltip("从原版候选动作提取的门叶角度，单位为度，横轴为归一化动作进度。")]
    public AnimationCurve leftAngle;
    public AnimationCurve rightAngle;
    [Tooltip("出生侧动作起点，朝向指向村内。")]
    public Transform playerStart;

    [Header("原版门声")]
    public AudioEventData latchSound;
    public AudioEventData firstLeafSound;
    public AudioEventData secondLeafSound;
    [Range(0f, 1f)] public float latchSoundProgress = 35f / 330f;
    [Range(0f, 1f)] public float firstSoundProgress = 80f / 330f;
    [Range(0f, 1f)] public float secondSoundProgress = 166f / 330f;
    [Range(0f, 1f)] public float commitProgress = 270f / 330f;

    public float InteractRange => 2.3f;
    public bool IsOpen { get; private set; }

    private Quaternion leftRest;
    private Quaternion rightRest;
    private Vector3 leftAxis;
    private Vector3 rightAxis;
    private LeonController candidate;
    private LeonController player;
    private LeonAnimationActionCoordinator actions;
    private LeonActionMotionDriver motion;
    private LeonAnimatorDriver animatorDriver;
    private LeonWeaponVisualController weaponVisuals;
    private ChapterOnePairMotion.Track rootTrack;
    private MotionControlToken motionToken;
    private int transactionId;
    private float requestedAt;
    private float progress;
    private bool started;
    private bool latchSoundPlayed;
    private bool firstSoundPlayed;
    private bool secondSoundPlayed;
    private Vector3 approachPosition;
    private Quaternion approachRotation;
    private Vector3 alignedPosition;
    private CapsuleCollider pushingCapsule;
    private readonly List<Collider> ignoredLeaves = new List<Collider>();
    private Collider[] leftContacts;
    private Collider[] rightContacts;

    private void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    internal Vector3 PromptPosition => transform.position + transform.up * 1.05f;

    private void Start()
    {
        if (leftLeaf != null && rightLeaf != null)
        {
            leftRest = leftLeaf.localRotation;
            rightRest = rightLeaf.localRotation;
            leftAxis = leftLeaf.parent.InverseTransformDirection(transform.up);
            rightAxis = rightLeaf.parent.InverseTransformDirection(transform.up);
            leftContacts = leftLeaf.GetComponentsInChildren<Collider>(true);
            rightContacts = rightLeaf.GetComponentsInChildren<Collider>(true);
        }
    }

    private void OnDisable()
    {
        InteractionSystem.Unregister(this);
        if (OwnsTransaction())
        {
            actions.CancelTraversal();
        }

        Finish(progress >= commitProgress);
    }

    public bool CanInteract(Transform interactor)
    {
        candidate = null;
        if (IsOpen || transactionId != 0 || playerStart == null || leftLeaf == null || rightLeaf == null)
        {
            return false;
        }

        Vector3 relative = transform.InverseTransformPoint(interactor.position);
        if (relative.z > -0.08f || relative.z < -InteractRange || Mathf.Abs(relative.x) > 0.9f ||
            Mathf.Abs(relative.y) > 0.65f || Vector3.Dot(interactor.forward, playerStart.forward) < 0.35f)
        {
            return false;
        }

        var controller = interactor.GetComponent<LeonController>();
        if (controller == null || !controller.CanAct() || controller.IsCrouching || !controller.IsGrounded ||
            (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput))
        {
            return false;
        }

        candidate = controller;
        return true;
    }

    public string GetPromptText() => "推开";

    public void Interact()
    {
        var controller = candidate;
        if (controller == null || !CanInteract(controller.transform))
        {
            return;
        }

        var profile = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
        if (profile == null || !profile.TryGetNamedTrack("LeonDoors_0007", out rootTrack))
        {
            return;
        }

        actions = controller.GetComponent<LeonAnimationActionCoordinator>();
        motion = controller.GetComponent<LeonActionMotionDriver>();
        animatorDriver = controller.GetComponent<LeonAnimatorDriver>();
        if (actions == null || motion == null || animatorDriver == null || !actions.RequestVillageGateOpening())
        {
            return;
        }

        player = controller;
        transactionId = actions.CurrentFullBodyTransactionId;
        motionToken = motion.AuthorizeMotion(MotionSource.Traversal, transactionId);
        actions.OnTraversalActionCompleted += HandleCompleted;
        actions.OnTraversalActionInterrupted += HandleInterrupted;
        actions.OnTraversalActionExited += HandleInterrupted;
        if (!motionToken.Valid)
        {
            actions.CancelTraversal();
            return;
        }

        approachPosition = player.transform.position;
        approachRotation = player.transform.rotation;
        alignedPosition = playerStart.position;
        alignedPosition.y = approachPosition.y;
        requestedAt = Time.time;
        player.ResetMovementInputForWindow();
        player.SetFreeLooking(false);
        player.ChangeState(LeonState.Traversing);
        motion.StopHorizontalSpeed(motionToken);
        pushingCapsule = player.GetComponent<CapsuleCollider>();
        IgnorePushLeafCollisions(leftLeaf);
        IgnorePushLeafCollisions(rightLeaf);
        weaponVisuals = player.GetComponent<LeonWeaponVisualController>();
        if (weaponVisuals != null)
        {
            weaponVisuals.SetTraversalWeaponVisible(false);
        }
    }

    private void FixedUpdate()
    {
        if (transactionId == 0)
        {
            TryBeginFromPlayerContact();
            return;
        }

        if (!OwnsTransaction() || !motion.TokenValid(motionToken))
        {
            Finish(progress >= commitProgress);
            return;
        }

        if (!animatorDriver.TryReadGateOpeningProgress(out float next))
        {
            if (!started && Time.time - requestedAt > 0.8f)
            {
                actions.CancelTraversal();
            }

            return;
        }

        started = true;
        ApplyProgress(next, true);
    }

    private void TryBeginFromPlayerContact()
    {
        var leon = LeonController.Instance;
        if (IsOpen || leon == null || leftContacts == null || rightContacts == null
            || leon.MoveInput.sqrMagnitude < .025f || !CanInteract(leon.transform))
        {
            return;
        }
        if (!TryReadPlayerPush(leon, out Vector3 movement, out CapsuleCollider capsule)
            || Vector3.Dot(movement, playerStart.forward) < .3f)
        {
            return;
        }
        float radius = Mathf.Min(capsule.bounds.extents.x, capsule.bounds.extents.z);
        if (TouchesLeaf(leftContacts, capsule.bounds.center, radius)
            || TouchesLeaf(rightContacts, capsule.bounds.center, radius))
        {
            Interact();
        }
    }

    internal static bool TryReadPlayerPush(LeonController leon, out Vector3 movement, out CapsuleCollider capsule)
    {
        movement = Vector3.zero;
        capsule = null;
        if (leon.MoveInput.sqrMagnitude < .025f || !leon.TryGetComponent(out capsule))
        {
            return false;
        }
        var camera = leon.playerCamera;
        Vector3 forward = leon.transform.forward;
        if (camera != null)
        {
            forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        movement = forward * leon.MoveInput.z + right * leon.MoveInput.x;
        return true;
    }

    private static bool TouchesLeaf(Collider[] leaves, Vector3 center, float radius)
    {
        float reach = radius + .13f;
        foreach (var leaf in leaves)
        {
            if (leaf != null && leaf.enabled && !leaf.isTrigger
                && (leaf.ClosestPoint(center) - center).sqrMagnitude <= reach * reach)
            {
                return true;
            }
        }
        return false;
    }

    private bool OwnsTransaction()
    {
        return transactionId != 0 && actions != null && actions.HasActiveFullBodyAction &&
            actions.CurrentFullBodyTransactionId == transactionId;
    }

    private void ApplyProgress(float next, bool interpolate = false)
    {
        progress = Mathf.Max(progress, next);
        UpdateLeaves(progress);
        float seconds = progress * rootTrack.clip.length;
        Vector3 offset = rootTrack.Sample(seconds).position;
        offset.y = 0f;
        float alignment = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / 0.34f));
        Vector3 position = Vector3.Lerp(approachPosition, alignedPosition, alignment) + playerStart.rotation * offset;
        Quaternion facing = Quaternion.Slerp(approachRotation, playerStart.rotation, alignment);
        motion.CommitTraversalPose(motionToken, position, facing, interpolate);

        if (!latchSoundPlayed && progress >= latchSoundProgress)
        {
            latchSoundPlayed = true;
            PlayLeaf(latchSound, transform.TransformPoint(new Vector3(0f, 1.25f, 0f)));
        }

        if (!firstSoundPlayed && progress >= firstSoundProgress)
        {
            firstSoundPlayed = true;
            PlayLeaf(firstLeafSound, leftLeaf.position);
        }

        if (!secondSoundPlayed && progress >= secondSoundProgress)
        {
            secondSoundPlayed = true;
            PlayLeaf(secondLeafSound, rightLeaf.position);
        }
    }

    private static void PlayLeaf(AudioEventData sound, Vector3 position)
    {
        if (sound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3D(sound, position);
        }
    }

    private void UpdateLeaves(float value)
    {
        leftLeaf.localRotation = Quaternion.AngleAxis(leftAngle.Evaluate(value), leftAxis) * leftRest;
        rightLeaf.localRotation = Quaternion.AngleAxis(rightAngle.Evaluate(value), rightAxis) * rightRest;
    }

    private void IgnorePushLeafCollisions(Transform leaf)
    {
        if (pushingCapsule == null)
        {
            return;
        }

        foreach (Collider collider in leaf.GetComponentsInChildren<Collider>())
        {
            if (!collider.isTrigger && !Physics.GetIgnoreCollision(pushingCapsule, collider))
            {
                Physics.IgnoreCollision(pushingCapsule, collider, true);
                ignoredLeaves.Add(collider);
            }
        }
    }

    private void HandleCompleted(AnimationActionTransaction transaction)
    {
        if (transaction.transactionId == transactionId)
        {
            ApplyProgress(1f);
            Finish(true);
        }
    }

    private void HandleInterrupted(AnimationActionTransaction transaction)
    {
        if (transaction.transactionId == transactionId)
        {
            Finish(progress >= commitProgress);
        }
    }

    private void Finish(bool opened)
    {
        if (transactionId == 0)
        {
            return;
        }

        transactionId = 0;
        actions.OnTraversalActionCompleted -= HandleCompleted;
        actions.OnTraversalActionInterrupted -= HandleInterrupted;
        actions.OnTraversalActionExited -= HandleInterrupted;
        IsOpen = opened;
        if (leftLeaf != null && rightLeaf != null)
        {
            if (opened)
            {
                UpdateLeaves(1f);
            }
            else
            {
                UpdateLeaves(0f);
            }
        }

        if (pushingCapsule != null)
        {
            foreach (Collider leaf in ignoredLeaves)
            {
                if (leaf != null)
                {
                    Physics.IgnoreCollision(pushingCapsule, leaf, false);
                }
            }
        }
        ignoredLeaves.Clear();
        pushingCapsule = null;
        motion.ReleaseMotion(motionToken);
        if (weaponVisuals != null)
        {
            weaponVisuals.SetTraversalWeaponVisible(true);
        }

        if (player != null && player.currentState == LeonState.Traversing)
        {
            player.ChangeState(LeonState.Idle);
        }

        started = false;
        progress = 0f;
        latchSoundPlayed = false;
        firstSoundPlayed = false;
        secondSoundPlayed = false;
        player = null;
    }
}

