using UnityEngine;

public sealed class VillageLegTrap : MonoBehaviour, IInteractable, IDamageable
{
    [SerializeField, Tooltip("两侧可动夹臂。")]
    private Transform leftJaw;
    [SerializeField]
    private Transform rightJaw;
    [SerializeField, Tooltip("夹口中心与脚踝锚点。")]
    private Transform ankle;
    [SerializeField, Tooltip("扣合及掰开的声音。")]
    private AudioEventData catchAudio;
    [SerializeField]
    private AudioEventData releaseAudio;

    private LeonTrapAvatar occupant;
    private EnemyCombatModule enemyOccupant;
    private bool spent;
    private Animation mechanism;
    private bool snapping;
    private float snapTime;
    private AnimationClip activeClip;
    private AnimationEvent[] markers;
    private int nextMarker;
    internal Vector3 Ankle => ankle.position;
    internal Vector3 LeftGrip => leftJaw.TransformPoint(Vector3.up * 0.05f);
    internal Vector3 RightGrip => rightJaw.TransformPoint(Vector3.up * 0.05f);
    public float InteractRange => 0.95f;

    private void Awake()
    {
        mechanism = GetComponentInChildren<Animation>(true);
        if (mechanism != null && mechanism.GetClip("0001") != null)
            return;
        mechanism = null;
        var prefab = Resources.Load<GameObject>("VillageLegTrapMechanism");
        if (prefab == null)
            return;
        var mount = transform.parent;
        var original = mount == null ? null : mount.GetComponent<MeshFilter>();
        bool hasOriginal = original != null && original.sharedMesh != null && original.sharedMesh.name == "edfa94f79806de613c1f";
        var model = Instantiate(prefab, hasOriginal ? mount : transform, false);
        model.transform.SetParent(transform, true);
        mechanism = model.GetComponent<Animation>();
        if (hasOriginal)
        {
            var renderer = original.GetComponent<MeshRenderer>();
            foreach (var part in model.GetComponentsInChildren<MeshRenderer>())
                part.sharedMaterials = renderer.sharedMaterials;
            renderer.enabled = false;
            var collider = original.GetComponent<MeshCollider>();
            if (collider != null) collider.enabled = false;
        }
        SampleTrack("0001", 0f, true);
    }

    private void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    private void OnDisable()
    {
        if (snapping)
        {
            snapping = false;
            SampleTrack("0002", 0f, true);
        }
        InteractionSystem.Unregister(this);
        if (enemyOccupant != null)
        {
            enemyOccupant.CancelNativeCombat(true);
        }
        if (occupant != null)
        {
            occupant.ReleaseTrap();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (spent || occupant != null || enemyOccupant != null || ankle == null || leftJaw == null || rightJaw == null)
        {
            return;
        }

        var enemy = other.GetComponentInParent<VillagerController>();
        if (enemy != null)
        {
            bool footInRange = false;
            if (enemy.combatModule != null)
            {
                if (enemy.combatModule.HasCachedTrapFeet)
                {
                    footInRange = enemy.combatModule.IsFootNearLegTrap(Ankle, 0.36f);
                }
                else
                {
                    footInRange = (other.ClosestPoint(Ankle) - Ankle).sqrMagnitude <= .22f * .22f;
                }
            }

            if (footInRange && enemy.combatModule != null && enemy.combatModule.TryCatchLegTrap(this))
            {
                enemyOccupant = enemy.combatModule;
                SampleEnemy(0f);
            }
            return;
        }
        var player = other.GetComponentInParent<LeonController>();
        if (player != null && player.TrapAvatar != null && player.TrapAvatar.TryCatch(this))
        {
            occupant = player.TrapAvatar;
        }
    }

    internal void SamplePlayer(bool right, bool escaping, float seconds, bool seek = false)
    {
        string id = escaping ? (right ? "0026" : "0016") : (right ? "0025" : "0015");
        SampleTrack(id, seconds, seek);
    }

    internal void SampleEnemy(float seconds) => SampleTrack("0004", seconds);

    private void SampleTrack(string id, float seconds, bool seek = false)
    {
        if (mechanism == null) return;
        var clip = mechanism.GetClip(id);
        if (clip == null) return;
        if (activeClip != clip || seek)
        {
            activeClip = clip;
            markers = clip.events;
            nextMarker = 0;
        }
        float time = Mathf.Clamp(seconds, 0f, clip.length);
        clip.SampleAnimation(mechanism.gameObject, time);
        while (nextMarker < markers.Length && markers[nextMarker].time <= time)
        {
            var marker = markers[nextMarker++];
            var sound = marker.objectReferenceParameter as AudioEventData;
            if (sound == null) sound = id == "0016" || id == "0026" ? releaseAudio : catchAudio;
            if (!seek) PlaySound(sound);
        }
    }

    private void StartSnap()
    {
        snapTime = 0f;
        snapping = true;
    }

    private void Update()
    {
        if (!snapping) return;
        snapTime += Time.deltaTime;
        SampleTrack("0003", snapTime);
        snapping = activeClip != null && snapTime < activeClip.length;
    }

    internal bool TryDisarm()
    {
        if (!isActiveAndEnabled || spent || occupant != null || enemyOccupant != null)
            return false;
        spent = true;
        StartSnap();
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (damage > 0f) TryDisarm();
    }

    public void TakeDamage(float damage, Vector3 hitPoint) => TakeDamage(damage);

    internal void Release(LeonTrapAvatar avatar)
    {
        if (occupant != avatar)
        {
            return;
        }

        occupant = null;
        OpenJaws();
        SampleTrack("0002", 0f, true);
    }

    internal void ReleaseEnemy(EnemyCombatModule enemy)
    {
        if (enemyOccupant == enemy)
        {
            enemyOccupant = null;
            OpenJaws();
            SampleTrack("0002", 0f, true);
        }
    }

    internal void OpenJaws()
    {
        if (spent)
        {
            return;
        }
        spent = true;
        snapping = false;
    }

    public bool CanInteract(Transform interactor)
    {
        return occupant != null && occupant.transform == interactor && occupant.CanEscape;
    }

    public void Interact()
    {
        if (occupant != null)
        {
            occupant.TryEscape();
        }
    }

    public string GetPromptText() => "挣脱夹子";

    private void PlaySound(AudioEventData effect)
    {
        if (effect != null && AudioManager.HasInstance)
        {
            AudioManager.Instance.Play3D(effect, ankle.position);
        }
    }
}
