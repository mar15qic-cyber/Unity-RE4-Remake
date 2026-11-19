using UnityEngine;

public sealed class VillageChicken : MonoBehaviour
{
    [Header("原版小鸡动画")]
    [Tooltip("使用白鸡或棕鸡自身骨架的动画控制器。")]
    public Animator animator;

    [Header("同层活动")]
    [Tooltip("用于检查地面和前方障碍物。")]
    public LayerMask geometryMask = 1;
    [Min(1f)] public float roamRadius = 3f;
    [Min(0.1f)] public float approachDistance = 1.8f;

    private enum Activity
    {
        Idle,
        Walk,
        Peck,
        Flee
    }

    private static readonly int[] States =
    {
        Animator.StringToHash("Base Layer.Idle"),
        Animator.StringToHash("Base Layer.Walk"),
        Animator.StringToHash("Base Layer.Peck"),
        Animator.StringToHash("Base Layer.Flee")
    };

    private Transform player;
    private Vector3 home;
    private Vector3 direction;
    private Vector3 threat;
    private float nextDecision;
    private float fleeUntil;
    private Activity activity;

    private void OnEnable()
    {
        CombatEventDispatcher.OnWeaponFired += HandleGunshot;
    }

    private void Start()
    {
        home = transform.position;
        var controller = FindFirstObjectByType<LeonController>();
        if (controller != null)
        {
            player = controller.transform;
        }
        nextDecision = Time.time + Random.Range(0.6f, 2.4f);
    }

    private void OnDisable()
    {
        CombatEventDispatcher.OnWeaponFired -= HandleGunshot;
    }

    private void HandleGunshot(CombatEventDispatcher.WeaponNoiseEvent noise)
    {
        Vector3 offset = transform.position - noise.Position;
        if (Mathf.Abs(offset.y) <= 2f && offset.sqrMagnitude <= noise.Radius * noise.Radius)
        {
            StartFleeing(noise.Position);
        }
    }

    private void StartFleeing(Vector3 position)
    {
        threat = position;
        fleeUntil = Time.time + 1.25f;

    }

    private void FixedUpdate()
    {
        if (player != null)
        {
            Vector3 fromPlayer = transform.position - player.position;
            if (Mathf.Abs(fromPlayer.y) < 0.8f && fromPlayer.sqrMagnitude <= approachDistance * approachDistance)
            {
                StartFleeing(player.position);
            }
        }

        if (Time.time < fleeUntil)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - threat, Vector3.up);
            bool moved = TryStep(away, 2.7f) || TryStep(Quaternion.Euler(0f, 60f, 0f) * away, 2.7f) ||
                TryStep(Quaternion.Euler(0f, -60f, 0f) * away, 2.7f);
            if (moved)
            {
                SetActivity(Activity.Flee);
            }
            else
            {
                SetActivity(Activity.Idle);
            }
            return;
        }

        if (activity == Activity.Flee || Time.time >= nextDecision)
        {
            ChooseActivity();
        }

        if (activity == Activity.Walk && !TryStep(direction, 0.29f))
        {
            nextDecision = Time.time + 0.5f;
            SetActivity(Activity.Idle);
        }
    }

    private void ChooseActivity()
    {
        float choice = Random.value;
        if (choice < 0.38f)
        {
            direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            nextDecision = Time.time + Random.Range(1.3f, 3.4f);
            SetActivity(Activity.Walk);
        }
        else if (choice < 0.75f)
        {
            nextDecision = Time.time + Random.Range(2.0f, 4.2f);
            SetActivity(Activity.Peck);
        }
        else
        {
            nextDecision = Time.time + Random.Range(0.8f, 2.0f);
            SetActivity(Activity.Idle);
        }
    }

    private bool TryStep(Vector3 heading, float speed)
    {
        if (heading.sqrMagnitude < 0.001f)
        {
            return false;
        }

        Vector3 forward = heading.normalized;
        Vector3 current = transform.position;
        float distance = speed * Time.fixedDeltaTime;
        Vector3 target = current + forward * distance;
        Vector3 fromHome = Vector3.ProjectOnPlane(target - home, Vector3.up);
        if (fromHome.sqrMagnitude > roamRadius * roamRadius)
        {
            return false;
        }

        Vector3 chest = current + Vector3.up * 0.18f;
        if (Physics.SphereCast(chest, 0.11f, forward, out _, distance + 0.12f, geometryMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (!Physics.Raycast(target + Vector3.up * 0.28f, Vector3.down, out RaycastHit ground, 0.48f,
            geometryMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (ground.normal.y < 0.66f || Mathf.Abs(ground.point.y - home.y) > 0.34f)
        {
            return false;
        }

        target.y = ground.point.y + 0.015f;
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
        Quaternion rotation = Quaternion.RotateTowards(transform.rotation, facing, 480f * Time.fixedDeltaTime);
        transform.SetPositionAndRotation(target, rotation);
        return true;
    }

    private void SetActivity(Activity next)
    {
        if (activity == next)
        {
            return;
        }

        activity = next;
        if (animator != null)
        {
            animator.CrossFadeInFixedTime(States[(int)next], 0.13f);
        }
    }
}


