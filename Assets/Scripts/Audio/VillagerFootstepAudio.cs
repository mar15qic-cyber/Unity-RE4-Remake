using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(VillagerController))]
public sealed class VillagerFootstepAudio : MonoBehaviour
{
    [Tooltip("当前村民的鞋型和地表脚步配置")]
    public FootstepSurfaceProfile surfaceProfile;
    [Tooltip("供脚步查找地面的碰撞层")]
    public LayerMask groundLayer = ~0;

    private VillagerController villager;
    private NativeActionAudio nativeAudio;
    private Vector3 previousPosition;
    private float stepDistance;
    private float lastMovementTime;

    private void Start()
    {
        villager = GetComponent<VillagerController>();
        nativeAudio = GetComponent<NativeActionAudio>();
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        stepDistance = 0f;
        lastMovementTime = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        Vector3 position = transform.position;
        Vector3 displacement = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
        previousPosition = position;
        float distance = displacement.magnitude;
        if (villager == null || villager.IsDead || villager.CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion
            || distance > 0.7f || surfaceProfile == null || (nativeAudio != null && nativeAudio.OwnsFootsteps))
        {
            stepDistance = 0f;
            return;
        }

        if (distance < 0.001f)
        {
            if (Time.time - lastMovementTime > 0.15f)
            {
                stepDistance = 0f;
            }

            return;
        }

        lastMovementTime = Time.time;
        bool running = villager.CurrentState == VillagerState.Chase && villager.ArchitectureReadActualHorizontalMovementSpeed >= 1.8f;
        float stride = 0.54f;
        if (running)
        {
            stride = 0.85f;
        }

        stepDistance += distance;
        if (stepDistance < stride)
        {
            return;
        }

        stepDistance -= stride;
        AudioManager audio = AudioManager.Instance;
        if (audio == null || !FootstepSurface.TryFindGround(transform, 0.6f, groundLayer, out RaycastHit ground))
        {
            return;
        }

        SurfaceType surface = FootstepSurface.Resolve(ground);
        AudioEventData sound = surfaceProfile.GetWalkEvent(surface);
        if (running)
        {
            sound = surfaceProfile.GetRunEvent(surface);
        }

        audio.Play3D(sound, ground.point);
    }
}
