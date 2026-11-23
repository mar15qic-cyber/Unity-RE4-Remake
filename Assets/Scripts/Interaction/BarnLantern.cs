using UnityEngine;

public sealed class BarnLantern : MonoBehaviour, IDamageable
{
    [SerializeField, Tooltip("牛棚里的牛。")]
    private BurningBarnCow cow;
    [SerializeField, Tooltip("挂灯完好时的火焰和灯光。")]
    private GameObject pilotLight;
    [SerializeField, Tooltip("受击后停止挡弹的挂灯碰撞。")]
    private Collider lanternCollider;
    [SerializeField, Tooltip("破裂后触发的火焰与碎片。")]
    private ParticleSystem ignition;
    [SerializeField, Tooltip("挂灯在地面点燃油料的位置。")]
    private Transform spill;
    [SerializeField, Tooltip("灯玻璃碎裂声。")]
    private AudioEventData glassAudio;
    [SerializeField, Tooltip("玻璃碎片落到地面的声音。")]
    private AudioEventData shardsAudio;
    [SerializeField, Tooltip("油料刚点燃的声音。")]
    private AudioEventData ignitionAudio;
    [SerializeField, Tooltip("地面油火的持续声音。")]
    private AudioEventData spillFireAudio;
    [SerializeField, Min(0.1f), Tooltip("地面油火的有效半径。")]
    private float ignitionRadius = 2.8f;

    private bool broken;
    private float remainingFire;
    private AudioPlayHandle spillFireSound;
    private const float FireDuration = 4f;
    private const float ShardContactDelay = .65f;
    private const float FireLoopStart = 2.243f;
    private readonly RaycastHit[] fireObstacles = new RaycastHit[8];

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (broken || damage <= 0f || !GameManager.HasInstance || GameManager.Instance.IsTerminal)
        {
            return;
        }

        broken = true;
        remainingFire = FireDuration;
        lanternCollider.enabled = false;
        pilotLight.SetActive(false);
        foreach (MeshRenderer model in GetComponentsInChildren<MeshRenderer>())
        {
            model.enabled = false;
        }

        PlayCue(glassAudio, hitPoint);
        PlayCue(ignitionAudio, spill.position);
        ignition.Play();
        TryIgniteCow();
    }

    private void Update()
    {
        if (remainingFire <= 0f)
        {
            return;
        }

        if (!GameManager.HasInstance || GameManager.Instance.IsTerminal)
        {
            StopSpillFire();
            ignition.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        float previousElapsed = FireDuration - remainingFire;
        remainingFire -= Time.deltaTime;
        if (remainingFire <= 0f)
        {
            StopSpillFire();
            return;
        }

        float elapsed = FireDuration - remainingFire;
        if (previousElapsed < ShardContactDelay && elapsed >= ShardContactDelay)
        {
            PlayCue(shardsAudio, spill.position);
        }
        if (previousElapsed < FireLoopStart && elapsed >= FireLoopStart)
        {
            spillFireSound = PlayCue(spillFireAudio, spill.position);
        }

        TryIgniteCow();
    }

    private static AudioPlayHandle PlayCue(AudioEventData effect, Vector3 position)
    {
        if (effect == null || !AudioManager.HasInstance)
        {
            return default;
        }
        return AudioManager.Instance.Play3D(effect, position);
    }

    private void StopSpillFire()
    {
        remainingFire = 0f;
        if (spillFireSound.IsValid && AudioManager.HasInstance)
        {
            AudioManager.Instance.Stop(spillFireSound);
        }
        spillFireSound = default;
    }

    private void OnDisable()
    {
        StopSpillFire();
    }

    private void TryIgniteCow()
    {
        if (cow == null)
        {
            return;
        }

        Vector3 offset = cow.transform.position - spill.position;
        if (Mathf.Abs(offset.y) >= 1.1f || offset.sqrMagnitude > ignitionRadius * ignitionRadius)
        {
            return;
        }

        Vector3 origin = spill.position + Vector3.up * .4f;
        Vector3 toCow = cow.transform.position + Vector3.up * .45f - origin;
        int count = Physics.RaycastNonAlloc(origin, toCow.normalized, fireObstacles, toCow.magnitude, 1, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            if (!fireObstacles[index].collider.transform.IsChildOf(cow.transform))
            {
                return;
            }
        }

        cow.Ignite();
    }
}
