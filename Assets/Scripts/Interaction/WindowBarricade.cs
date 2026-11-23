using GameArchitecture;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public sealed class WindowBarricade : MonoBehaviour, IInteractable
{
    // sm84_518_0005_success 的声音轨在第 31 帧起摩擦，第 121 帧撞上窗台。
    private const float SlideSoundTime = 31f / 60f;
    private const float SeatSoundTime = 121f / 60f;
    [SerializeField, Tooltip("柜体到位后封住的窗口。")]
    private WindowTraversalPoint window;
    [SerializeField, Tooltip("使用同一窗口的敌人通行。")]
    private EnemyWindowTraversalRoute route;
    [SerializeField, Tooltip("柜体抵住窗口后的位置。")]
    private Transform destination;
    [SerializeField, Tooltip("玩家推柜的动作剪辑。")]
    private AnimationClip pushClip;
    [SerializeField, Tooltip("长按时里昂转身、抵住柜体的原版起手动作。")]
    private AnimationClip braceClip;
    [SerializeField, Min(.1f), Tooltip("从开始长按到允许起推的游戏秒数。")]
    private float holdDuration = 1.5f;
    [SerializeField] private AnimationCurve braceForward;
    [SerializeField] private AnimationCurve braceSide;
    [SerializeField] private AnimationCurve braceYaw;
    [SerializeField, Tooltip("移动前柜体的原版模型基准。")]
    private Transform shelfModel;
    [SerializeField, Tooltip("原版柜体沿窗口方向的位移比例，横轴为动作秒数。")]
    private AnimationCurve shelfTravel;
    [SerializeField, Tooltip("里昂沿推柜方向的位置。")]
    private AnimationCurve actorForward;
    [SerializeField, Tooltip("里昂沿屋内方向的位置。")]
    private AnimationCurve actorSide;
    [SerializeField, Tooltip("里昂相对推柜方向的转身角。")]
    private AnimationCurve actorYaw;
    [SerializeField, Tooltip("开始推柜时的摩擦声。")]
    private AudioEventData slideAudio;
    [SerializeField, Tooltip("柜体封住窗口时的撞击声。")]
    private AudioEventData seatAudio;

    private LeonController player;
    private LeonAnimationActionCoordinator actions;
    private LeonActionMotionDriver motion;
    private LeonWeaponVisualController weapon;
    private MotionControlToken motionToken;
    private int transaction;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable bracePlayable;
    private AnimationClipPlayable pushPlayable;
    private AnimationMixerPlayable mixer;
    private Vector3 start;
    private Vector3 playerStart;
    private bool committed;
    private LeonInputBridge holdInput;
    private float heldSince;
    private double pushSince;
    private bool pushing;
    internal bool HasActivePush => transaction != 0;
    internal bool ShowsHoldPrompt => HasActivePush && (!pushing || Time.timeAsDouble - pushSince < .1);
    internal float HoldProgress
    {
        get
        {
            if (!HasActivePush)
            {
                return 0f;
            }
            if (pushing)
            {
                return 1f;
            }
            return Mathf.Clamp01((Time.time - heldSince) / holdDuration);
        }
    }
    private Vector3 origin;
    private Vector3 pushDirection;
    private Quaternion playerStartRotation;
    private bool slideStarted;
    private bool seatSoundPlayed;

    public float InteractRange => 1.2f;

    internal Vector3 StandingPoint
    {
        get
        {
            if (shelfModel == null || destination == null || actorForward == null || actorSide == null)
            {
                return transform.position;
            }
            Vector3 direction = Vector3.ProjectOnPlane(destination.position - transform.position, Vector3.up).normalized;
            return ActorPoint(shelfModel.position, direction, 0f);
        }
    }

    internal Vector3 PromptPosition => Vector3.Lerp(StandingPoint, shelfModel.position, .4f) + Vector3.up * 1.55f;

    internal float InteractionDistance(Vector3 position)
    {
        Vector3 rear = StandingPoint;
        Vector3 contact = PromptPosition;
        Vector3 along = Vector3.ProjectOnPlane(contact - rear, Vector3.up);
        Vector3 offset = Vector3.ProjectOnPlane(position - rear, Vector3.up);
        float fraction = Mathf.Clamp01(Vector3.Dot(offset, along) / Mathf.Max(.0001f, along.sqrMagnitude));
        return (offset - along * fraction).magnitude;
    }

    private Vector3 ActorPoint(Vector3 anchor, Vector3 direction, float seconds)
    {
        Vector3 inward = Vector3.Cross(direction, Vector3.up);
        return anchor + direction * actorForward.Evaluate(seconds) + inward * actorSide.Evaluate(seconds);
    }

    private void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    private void OnDisable()
    {
        holdInput = null;
        Finish(true);
        InteractionSystem.Unregister(this);
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null || committed || transaction != 0 || window == null || destination == null ||
            braceClip == null || braceForward == null || braceSide == null || braceYaw == null ||
            pushClip == null || shelfModel == null || shelfTravel == null || actorForward == null || actorSide == null ||
            actorYaw == null || window.IsPassageReservedByOther(this) || route != null && route.HasCrossingActor)
        {
            return false;
        }

        Vector3 offset = interactor.position - StandingPoint;
        Vector3 direction = Vector3.ProjectOnPlane(destination.position - transform.position, Vector3.up).normalized;
        float along = Vector3.Dot(offset, direction);
        float inward = Vector3.Dot(offset, Vector3.Cross(direction, Vector3.up));
        if (Mathf.Abs(offset.y) > .65f || InteractionDistance(interactor.position) > InteractRange ||
            along > 1.45f || inward < -.35f || along > .65f && inward < .2f)
        {
            return false;
        }

        return interactor.TryGetComponent(out LeonController candidate) && candidate.IsGrounded && candidate.CanAct() &&
            candidate.AnimCoordinator != null && candidate.AnimCoordinator.CanRequestLadderPush();
    }

    public string GetPromptText() => "推柜封窗";

    public void Interact()
    {
        LeonController candidate = LeonController.Instance;
        if (candidate == null || !CanInteract(candidate.transform) ||
            !candidate.TryGetComponent(out LeonInputBridge input) || !input.IsInteractionHeld ||
            !candidate.TryGetComponent(out LeonActionMotionDriver driver) ||
            !candidate.TryGetComponent(out Animator animator) || !window.TryReservePassage(this))
        {
            return;
        }

        actions = candidate.AnimCoordinator;
        if (!actions.RequestEnvironmentalAction(FullBodyActionType.CabinetPush))
        {
            window.ReleasePassage(this);
            return;
        }

        player = candidate;
        holdInput = input;
        heldSince = Time.time;
        pushing = false;
        motion = driver;
        start = transform.position;
        playerStart = player.transform.position;
        playerStartRotation = player.transform.rotation;
        origin = shelfModel.position;
        pushDirection = Vector3.ProjectOnPlane(destination.position - start, Vector3.up).normalized;
        slideStarted = false;
        seatSoundPlayed = false;
        transaction = actions.CurrentFullBodyTransactionId;
        motionToken = motion.AuthorizeMotion(MotionSource.Traversal, transaction);
        if (!motionToken.Valid)
        {
            Finish(true);
            return;
        }

        player.ResetMovementInputForWindow();
        player.ChangeState(LeonState.Traversing);
        motion.StopHorizontalSpeed(motionToken);

        if (player.TryGetComponent(out weapon))
        {
            weapon.SetTraversalWeaponVisible(false);
        }

        graph = PlayableGraph.Create("三号屋推柜动作");
        bracePlayable = AnimationClipPlayable.Create(graph, braceClip);
        pushPlayable = AnimationClipPlayable.Create(graph, pushClip);
        bracePlayable.SetApplyFootIK(true);
        pushPlayable.SetApplyFootIK(true);
        // 骨骼和场景根节点按同一动作秒数采样，避免低帧率时柜体比双手提前一帧。
        bracePlayable.SetSpeed(0d);
        pushPlayable.SetSpeed(0d);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(bracePlayable, 0, mixer, 0);
        graph.Connect(pushPlayable, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        output = AnimationPlayableOutput.Create(graph, "推柜", animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSourcePlayable(mixer);
        output.SetWeight(0f);
        graph.Play();
    }

    private void Update()
    {
        if (transaction == 0)
        {
            return;
        }

        if (player == null || !actions.OwnsLadderAction(transaction, FullBodyActionType.CabinetPush) ||
            GameManager.HasInstance && GameManager.Instance.IsTerminal)
        {
            Finish(true);
            return;
        }

        motion.StopHorizontalSpeed(motionToken);
        if (!pushing)
        {
            if (holdInput == null || !holdInput.IsInteractionHeld ||
                GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
            {
                Finish(true);
                return;
            }
            float elapsed = Time.time - heldSince;
            SampleBrace(Mathf.Min(elapsed, holdDuration, braceClip.length));
            if (elapsed < holdDuration)
            {
                return;
            }
            pushing = true;
            pushSince = Time.timeAsDouble;
        }

        float seconds = Mathf.Min((float)(Time.timeAsDouble - pushSince), pushClip.length);
        pushPlayable.SetTime(seconds);
        float blend = Mathf.Clamp01(seconds * 12f);
        mixer.SetInputWeight(0, 1f - blend);
        mixer.SetInputWeight(1, blend);
        output.SetWeight(Mathf.Clamp01((pushClip.length - seconds) * 6f));
        Vector3 target = ActorPoint(origin, pushDirection, seconds);
        Quaternion facing = Quaternion.AngleAxis(actorYaw.Evaluate(seconds), Vector3.up) * Quaternion.LookRotation(pushDirection);
        motion.CommitTraversalPose(motionToken, target, facing);
        if (!committed)
        {
            float travel = Mathf.Clamp01(shelfTravel.Evaluate(seconds));
            transform.position = Vector3.Lerp(start, destination.position, travel);
            if (!slideStarted && seconds >= SlideSoundTime - 0.00001f)
            {
                slideStarted = true;
                PlaySound(slideAudio);
            }
            if (!seatSoundPlayed && seconds >= SeatSoundTime - 0.00001f)
            {
                seatSoundPlayed = true;
                PlaySound(seatAudio);
            }
            // 原始柜轨末端还有亚毫米级尾差，窗洞在柜体实际停稳时就应封住。
            if (seatSoundPlayed && (transform.position - destination.position).sqrMagnitude <= .001f * .001f)
            {
                committed = window.CommitBarricade(this);
                if (committed)
                {
                    transform.position = destination.position;
                }
            }
        }

        if (seconds >= pushClip.length)
        {
            Finish(false);
        }
    }

    private void SampleBrace(float seconds)
    {
        bracePlayable.SetTime(seconds);
        output.SetWeight(Mathf.Clamp01(seconds * 8f));
        Vector3 inward = Vector3.Cross(pushDirection, Vector3.up);
        Vector3 entry = origin + pushDirection * braceForward.Evaluate(0f) + inward * braceSide.Evaluate(0f);
        Vector3 target = origin + pushDirection * braceForward.Evaluate(seconds) + inward * braceSide.Evaluate(seconds);
        // 先沿柜面绕过柜角，再随原版起手动作靠拢；双手顶稳前收尽入位偏差。
        Vector3 offset = playerStart - entry;
        Vector3 along = Vector3.Project(offset, pushDirection);
        float alignAlong = Mathf.SmoothStep(1f, 0f, seconds / .38f);
        float alignSide = Mathf.SmoothStep(1f, 0f, seconds / .55f);
        Vector3 desired = target + along * alignAlong + (offset - along) * alignSide;
        Quaternion facing = Quaternion.AngleAxis(braceYaw.Evaluate(seconds), Vector3.up) * Quaternion.LookRotation(pushDirection);
        motion.CommitTraversalPose(motionToken, desired, Quaternion.Slerp(playerStartRotation, facing, 1f - alignSide));
    }

    private void Finish(bool interrupted)
    {
        if (transaction == 0)
        {
            return;
        }

        if (!committed)
        {
            transform.position = start;
        }

        window.ReleasePassage(this);
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        if (weapon != null)
        {
            weapon.SetTraversalWeaponVisible(true);
        }

        motion.ReleaseMotion(motionToken);
        actions.EndLadderAction(transaction, FullBodyActionType.CabinetPush, interrupted);
        if (player != null && player.currentState == LeonState.Traversing && !actions.HasActiveFullBodyAction)
        {
            player.ChangeState(LeonState.Idle);
        }

        transaction = 0;
        holdInput = null;
        pushing = false;
        player = null;
    }

    private void PlaySound(AudioEventData effect)
    {
        if (effect != null && AudioManager.HasInstance)
        {
            AudioManager.Instance.Play3D(effect, transform.position);
        }
    }
}
