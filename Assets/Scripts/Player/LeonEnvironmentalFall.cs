using GameArchitecture;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

public sealed class LeonEnvironmentalFall : MonoBehaviour
{
    [SerializeField, Tooltip("受冲撞时的摔落动作。")]
    private AnimationClip fallClip;
    [SerializeField, Tooltip("着地时播放的身体落地声。")]
    private AudioEventData landSound;

    private LeonController player;
    private LeonAnimationActionCoordinator actions;
    private LeonActionMotionDriver motion;
    private Animator animator;
    private LeonWeaponVisualController weapon;
    private Transform hip;
    private EnvironmentPoseOffset hipOffset;
    private int transaction;
    private MotionControlToken token;
    private Vector3 direction;
    private float elapsed;
    private bool landed;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;

    internal bool CanStart => player != null && hip != null && player.IsGrounded &&
        (player.IsHeldByTrap || !actions.HasActiveFullBodyAction) &&
        player.currentState != LeonState.QTE && player.currentState != LeonState.Traversing;

    private void Start()
    {
        player = GetComponent<LeonController>();
        actions = GetComponent<LeonAnimationActionCoordinator>();
        motion = GetComponent<LeonActionMotionDriver>();
        animator = GetComponent<Animator>();
        weapon = GetComponent<LeonWeaponVisualController>();
        if (animator != null)
        {
            hip = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.Hips);
            hipOffset = new EnvironmentPoseOffset(hip);
        }
    }

    internal bool TryStart(Vector3 away)
    {
        if (!CanStart || !actions.RequestEnvironmentalFall())
        {
            return false;
        }

        transaction = actions.CurrentFullBodyTransactionId;
        token = motion.AuthorizeMotion(MotionSource.Traversal, transaction);
        if (!token.Valid)
        {
            Finish(true);
            return false;
        }

        elapsed = 0f;
        landed = false;
        direction = Vector3.ProjectOnPlane(away, Vector3.up).normalized;
        player.ResetMovementInputForWindow();
        player.ChangeState(LeonState.Traversing);
        motion.StopHorizontalSpeed(token);
        if (weapon != null)
        {
            weapon.SetTraversalWeaponVisible(false);
        }

        if (fallClip != null)
        {
            graph = PlayableGraph.Create("牛撞倒地动作");
            var clip = AnimationClipPlayable.Create(graph, fallClip);
            clip.SetApplyFootIK(false);
            output = AnimationPlayableOutput.Create(graph, "倒地", animator);
            output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            output.SetSourcePlayable(clip);
            output.SetWeight(0f);
            graph.Play();
        }

        return true;
    }

    private void FixedUpdate()
    {
        if (transaction == 0 || !motion.TokenValid(token) || !actions.OwnsLadderAction(transaction, FullBodyActionType.EnvironmentalFall))
        {
            return;
        }

        motion.StopHorizontalSpeed(token);
        if (elapsed >= 0.4f || !player.TryGetCurrentWorldCapsule(out Vector3 top, out Vector3 bottom, out float radius))
        {
            return;
        }

        float move = 2.4f * (1f - elapsed / 0.4f) * Time.fixedDeltaTime;
        if (!Physics.CapsuleCast(bottom, top, radius * .94f, direction, move + .28f, 1, QueryTriggerInteraction.Ignore))
        {
            motion.CommitPosition(token, transform.position + direction * move);
        }
    }

    private void Update()
    {
        if (transaction == 0)
        {
            return;
        }

        if (!actions.OwnsLadderAction(transaction, FullBodyActionType.EnvironmentalFall) ||
            GameManager.HasInstance && GameManager.Instance.IsTerminal)
        {
            Finish(true);
            return;
        }

        elapsed += Time.deltaTime;
        if (!landed && elapsed >= 0.42f)
        {
            landed = true;
            if (landSound != null && AudioManager.HasInstance)
            {
                AudioManager.Instance.Play3D(landSound, transform.position);
            }
        }

        if (elapsed >= 1.55f)
        {
            Finish(false);
        }
        else if (graph.IsValid())
        {
            output.SetWeight(Mathf.Min(Mathf.Clamp01(elapsed * 6f), Mathf.Clamp01((1.55f - elapsed) * 3f)));
        }
    }

    private void LateUpdate()
    {
        if (transaction == 0)
        {
            return;
        }

        float down = Mathf.Min(Mathf.InverseLerp(0.05f, 0.44f, elapsed), 1f - Mathf.InverseLerp(.85f, 1.45f, elapsed));
        hipOffset.Capture();
        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        float tilt = 76f;
        if (graph.IsValid())
        {
            tilt = 45f;
        }
        hip.rotation = Quaternion.AngleAxis(tilt * down, axis) * hip.rotation;
        hip.position -= Vector3.up * (down * .61f);
        hipOffset.Commit();
    }

    private void OnDisable()
    {
        Finish(true);
    }

    private void Finish(bool interrupted)
    {
        if (transaction == 0)
        {
            return;
        }

        if (graph.IsValid())
        {
            graph.Destroy();
        }

        hipOffset.Restore();
        motion.ReleaseMotion(token);
        actions.EndLadderAction(transaction, FullBodyActionType.EnvironmentalFall, interrupted);
        if (player.currentState == LeonState.Traversing && !actions.HasActiveFullBodyAction)
        {
            player.ChangeState(LeonState.Idle);
            if (weapon != null)
            {
                weapon.SetTraversalWeaponVisible(true);
            }
        }

        transaction = 0;
    }
}
