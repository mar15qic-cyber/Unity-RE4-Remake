using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

[DefaultExecutionOrder(330)]
public sealed class LeonTrapAvatar : MonoBehaviour
{
    [SerializeField, Tooltip("夹口闭合时播放的里昂受击语音。")]
    private AudioEventData caughtVoice;
    [SerializeField, Tooltip("左脚踩夹时的吃痛和持续挣扎。")]
    private AnimationClip leftCatch;
    [SerializeField, Tooltip("左脚脱离夹口并起身。")]
    private AnimationClip leftEscape;
    [SerializeField, Tooltip("右脚踩夹时的吃痛和持续挣扎。")]
    private AnimationClip rightCatch;
    [SerializeField, Tooltip("右脚脱离夹口并起身。")]
    private AnimationClip rightEscape;

    private LeonController player;
    private LeonActionMotionDriver motion;
    private LeonAnimationActionCoordinator coordinator;
    private LeonWeaponVisualController weapon;
    private Animator animator;
    private Transform pelvis;
    private EnvironmentPoseOffset pelvisOffset;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform pinnedFoot;
    private VillageLegTrap trap;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable catchPose;
    private AnimationClipPlayable escapePose;
    private int transaction;
    private FullBodyActionType action;
    private float caughtTime;
    private float escapeTime;
    private float poseWeight;
    private float interruptedTime;
    private bool jawsOpen;

    internal bool IsPinned => trap != null;
    internal bool CanEscape => IsPinned && transaction == 0 && interruptedTime <= 0f
        && player.CanActWithoutTrap() && !coordinator.HasActiveFullBodyAction;

    private void Start()
    {
        player = GetComponent<LeonController>();
        motion = GetComponent<LeonActionMotionDriver>();
        coordinator = GetComponent<LeonAnimationActionCoordinator>();
        weapon = GetComponent<LeonWeaponVisualController>();
        animator = GetComponent<Animator>();
        if (player == null || motion == null || coordinator == null || animator == null)
        {
            enabled = false;
            return;
        }
        pelvis = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.Hips);
        leftFoot = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftFoot);
        rightFoot = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightFoot);
        if (pelvis == null || leftFoot == null || rightFoot == null || leftCatch == null || leftEscape == null
            || rightCatch == null || rightEscape == null)
        {
            Debug.LogError("夹子缺少里昂骨骼或左右脚的完整动作绑定。", this);
            enabled = false;
            return;
        }
        pelvisOffset = new EnvironmentPoseOffset(pelvis);
        player.TrapAvatar = this;
    }

    private void OnEnable()
    {
        if (pelvisOffset != null)
        {
            player.TrapAvatar = this;
        }
    }

    internal bool TryCatch(VillageLegTrap source)
    {
        if (!isActiveAndEnabled || pelvisOffset == null || IsPinned || !player.IsGrounded || !player.CanAct()
            || !GameManager.HasInstance || GameManager.Instance.IsTerminal || !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        float leftDistance = (leftFoot.position - source.Ankle).sqrMagnitude;
        float rightDistance = (rightFoot.position - source.Ankle).sqrMagnitude;
        if (Mathf.Min(leftDistance, rightDistance) > .31f * .31f ||
            !coordinator.RequestEnvironmentalAction(FullBodyActionType.TrapCatch))
        {
            return false;
        }
        trap = source;
        pinnedFoot = leftFoot;
        AnimationClip caught = leftCatch;
        AnimationClip escape = leftEscape;
        if (rightDistance < leftDistance)
        {
            pinnedFoot = rightFoot;
            caught = rightCatch;
            escape = rightEscape;
        }
        action = FullBodyActionType.TrapCatch;
        transaction = coordinator.CurrentFullBodyTransactionId;
        motion.SetTrapFootLock(true);
        player.ResetMovementInputForWindow();
        player.ChangeState(LeonState.Idle);
        var combat = GetComponent<LeonCombat>();
        if (combat != null)
        {
            combat.ForceStopShoot();
            combat.HandleAim(false);
        }
        if (weapon != null)
        {
            weapon.SetTraversalWeaponVisible(false);
        }

        graph = PlayableGraph.Create("里昂踩夹与挣脱");
        mixer = AnimationMixerPlayable.Create(graph, 2);
        catchPose = ConnectClip(caught, 0);
        escapePose = ConnectClip(escape, 1);
        mixer.SetInputWeight(0, 1f);
        output = AnimationPlayableOutput.Create(graph, "里昂夹子全身动作", animator);
        output.SetSourcePlayable(mixer);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSortingOrder(100);
        output.SetWeight(0f);
        graph.Play();
        if (caughtVoice != null && AudioManager.HasInstance)
        {
            AudioManager.Instance.Play3D(caughtVoice, transform.position + Vector3.up);
        }
        return true;
    }

    private AnimationClipPlayable ConnectClip(AnimationClip clip, int input)
    {
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(0d);
        graph.Connect(playable, 0, mixer, input);
        return playable;
    }

    internal void TryEscape()
    {
        if (CanEscape && coordinator.RequestEnvironmentalAction(FullBodyActionType.TrapEscape))
        {
            transaction = coordinator.CurrentFullBodyTransactionId;
            action = FullBodyActionType.TrapEscape;
            escapeTime = 0f;
        }
    }

    internal void ReactToDamage()
    {
        if (!IsPinned)
        {
            return;
        }
        if (jawsOpen || player.Health.IsDead)
        {
            ReleaseTrap();
            return;
        }
        EndAction(true);
        interruptedTime = .32f;
        caughtTime = Mathf.Max(caughtTime, 1f);
        trap.SamplePlayer(pinnedFoot == rightFoot, false, caughtTime, true);
    }

    private void Update()
    {
        if (!IsPinned)
        {
            return;
        }
        if (player.Health.IsDead || !GameManager.HasInstance || GameManager.Instance.IsTerminal)
        {
            ReleaseTrap();
            return;
        }
        if (transaction != 0 && !coordinator.OwnsLadderAction(transaction, action))
        {
            ReactToDamage();
            return;
        }

        float step = Time.deltaTime;
        interruptedTime = Mathf.Max(0f, interruptedTime - step);
        float targetWeight = 1f;
        float escapeBlend = 0f;
        if (action == FullBodyActionType.TrapEscape)
        {
            escapeTime += step;
            float duration = escapePose.GetAnimationClip().length;
            escapePose.SetTime(Mathf.Min(escapeTime, duration));
            escapeBlend = Mathf.SmoothStep(0f, 1f, escapeTime / .22f);
            // 原版夹臂在第 30 帧开始掰动，第 72 帧附近松开。
            trap.SamplePlayer(pinnedFoot == rightFoot, true, escapeTime);
            if (!jawsOpen && escapeTime >= 72f / 60f)
            {
                jawsOpen = true;
                trap.OpenJaws();
            }
            targetWeight = Mathf.SmoothStep(0f, 1f, (duration - escapeTime) / .32f);
            if (escapeTime >= duration)
            {
                EndAction(false);
                ReleaseTrap();
                return;
            }
        }
        else
        {
            caughtTime = Mathf.Min(caughtTime + step, catchPose.GetAnimationClip().length);
            catchPose.SetTime(caughtTime);
            trap.SamplePlayer(pinnedFoot == rightFoot, false, caughtTime);
            if (action == FullBodyActionType.TrapCatch && caughtTime >= .85f)
            {
                EndAction(false);
            }
            if (interruptedTime > 0f || coordinator.HasActiveFullBodyAction && transaction == 0)
            {
                targetWeight = 0f;
            }
        }
        mixer.SetInputWeight(0, 1f - escapeBlend);
        mixer.SetInputWeight(1, escapeBlend);
        poseWeight = Mathf.MoveTowards(poseWeight, targetWeight, step * 8f);
        output.SetWeight(poseWeight);
    }

    private void LateUpdate()
    {
        if (!IsPinned)
        {
            return;
        }
        // 平移整副姿态以对齐脚踝，保留源动画的屈膝、手脚相对位置和起身幅度。
        float contactWeight = poseWeight;
        if (action == FullBodyActionType.TrapEscape)
        {
            contactWeight *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(66f, 88f, escapeTime * 60f));
        }
        pelvisOffset.Capture();
        pelvis.position += Vector3.ClampMagnitude(trap.Ankle - pinnedFoot.position, .45f) * contactWeight;
        pelvisOffset.Commit();
    }

    private void EndAction(bool interrupted)
    {
        if (transaction != 0)
        {
            coordinator.EndLadderAction(transaction, action, interrupted);
        }
        transaction = 0;
        action = FullBodyActionType.None;
    }

    internal void ReleaseTrap()
    {
        if (!IsPinned)
        {
            return;
        }
        var source = trap;
        trap = null;
        if (graph.IsValid())
        {
            graph.Destroy();
        }
        pelvisOffset.Restore();
        source.Release(this);
        EndAction(true);
        motion.SetTrapFootLock(false);
        if (weapon != null && player.Health != null && !player.Health.IsDead)
        {
            weapon.SetTraversalWeaponVisible(true);
        }
        caughtTime = 0f;
        escapeTime = 0f;
        interruptedTime = 0f;
        poseWeight = 0f;
        jawsOpen = false;
    }

    private void OnDisable()
    {
        ReleaseTrap();
        if (player != null && player.TrapAvatar == this)
        {
            player.TrapAvatar = null;
        }
    }
}
