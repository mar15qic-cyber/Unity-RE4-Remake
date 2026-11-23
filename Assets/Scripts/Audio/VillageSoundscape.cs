using UnityEngine;

[DisallowMultipleComponent]
public sealed class VillageSoundscape : MonoBehaviour
{
    [Tooltip("村庄原版底声，进入室内后逐渐衰减")]
    public AudioEventData outdoor;
    [Tooltip("独立室内底声，确认室内原版资源后配置")]
    public AudioEventData indoor;
    [Tooltip("村庄战斗分支的原版音乐循环")]
    public AudioEventData combatMusic;
    [Tooltip("已核对的村庄位置声源")]
    public AudioEventData locationSound;
    public Transform locationAnchor;

    [Tooltip("绑定后，只有这些建筑的碰撞可判为室内遮盖。")]
    [SerializeField] private Transform[] interiorCoverRoots = System.Array.Empty<Transform>();

    private readonly RaycastHit[] interiorCoverHits = new RaycastHit[24];
    private LeonController leon;
    private AudioPlayHandle outdoorHandle;
    private AudioPlayHandle indoorHandle;
    private AudioPlayHandle locationHandle;
    private float outdoorGain;
    private float indoorGain;
    private float interiorWeight;
    private float lastThreat = float.NegativeInfinity;
    private float nextPoll;
    private float nextLocationTime;
    private bool combatPlaying;

    internal void SustainCombatMusic()
    {
        var audio = AudioManager.Instance;
        if (audio == null || audio.IsAudioPaused || combatMusic == null)
        {
            return;
        }
        lastThreat = Time.time;
        if (!combatPlaying)
        {
            audio.PlayBGM(combatMusic);
            combatPlaying = true;
        }
    }

    internal void BeginBellMusicExit(AudioEventData tail)
    {
        var audio = AudioManager.Instance;
        if (audio != null && audio.TryPlayBGMTail(combatMusic, tail))
        {
            combatPlaying = false;
        }
    }

    internal void EndBellMusicExit(AudioEventData tail)
    {
        var audio = AudioManager.Instance;
        if (audio != null)
        {
            audio.StopBGMImmediately(tail);
            audio.StopBGMImmediately(combatMusic);
        }
        combatPlaying = false;
    }

    private void Update()
    {
        var audio = AudioManager.Instance;
        if (audio == null || audio.IsAudioPaused || Time.timeScale <= 0f)
        {
            return;
        }
        if (leon == null)
        {
            leon = FindFirstObjectByType<LeonController>();
        }
        if (leon == null || leon.gameObject.scene != gameObject.scene)
        {
            return;
        }

        if (!locationHandle.IsValid && Time.time >= nextLocationTime && locationSound != null && locationAnchor != null
            && (leon.transform.position - locationAnchor.position).sqrMagnitude <= locationSound.maxDistance * locationSound.maxDistance)
        {
            locationHandle = audio.Play3D(locationSound, locationAnchor.position);
            nextLocationTime = Time.time + Random.Range(9f, 14f);
        }
        UpdateAmbience(audio);
        if (Time.time < nextPoll)
        {
            return;
        }
        nextPoll = Time.time + 0.25f;
        var game = GameManager.Instance;
        var enemies = EnemyAIManager.Instance;
        bool ended = game != null && game.IsTerminal || leon.Health != null && leon.Health.IsDead;
        if (enemies != null && enemies.IsBellWithdrawal)
        {
            ended = true;
        }
        // 追击状态跨门窗、楼层和敌人视线中断持续生效。
        bool engaged = !ended && enemies != null && enemies.IsCombatUnlocked && enemies.HasEngagedEnemy();
        if (engaged)
        {
            SustainCombatMusic();
        }
        else if (combatPlaying && (ended || Time.time - lastThreat >= 6f))
        {
            audio.StopBGM(combatMusic);
            combatPlaying = false;
        }
    }

    private void UpdateAmbience(AudioManager audio)
    {
        // 用渐变权重跨越门口，避免边界附近来回切声。
        bool covered = HasInteriorCover(leon.transform.position + Vector3.up * 2.1f);
        float target = 0f;
        if (covered)
        {
            target = 1f;
        }
        interiorWeight = Mathf.MoveTowards(interiorWeight, target, Time.deltaTime * 1.5f);
        float outdoorWeight = 1f - interiorWeight;
        if (indoor == null)
        {
            outdoorWeight = Mathf.Lerp(1f, 0.32f, interiorWeight);
        }
        SetBed(audio, outdoor, ref outdoorHandle, ref outdoorGain, outdoorWeight);
        SetBed(audio, indoor, ref indoorHandle, ref indoorGain, interiorWeight);
    }

    internal bool HasInteriorCover(Vector3 origin, float distance = 3.2f)
    {
        if (interiorCoverRoots.Length == 0)
        {
            return Physics.Raycast(origin, Vector3.up, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        RaycastHit[] hits = interiorCoverHits;
        int count = Physics.RaycastNonAlloc(origin, Vector3.up, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(origin, Vector3.up, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Transform cover = hits[i].collider.transform;
            foreach (Transform root in interiorCoverRoots)
            {
                if (root != null && cover.IsChildOf(root))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetBed(AudioManager audio, AudioEventData bed, ref AudioPlayHandle handle, ref float gain, float weight)
    {
        if (bed == null)
        {
            return;
        }
        if (!handle.IsValid)
        {
            handle = audio.Play(bed);
            if (!handle.IsValid)
            {
                return;
            }
            gain = handle.source.volume;
        }
        handle.source.volume = gain * weight;
    }

    private void OnDisable()
    {
        var audio = AudioManager.Instance;
        if (audio != null)
        {
            audio.Stop(outdoorHandle);
            audio.Stop(indoorHandle);
            audio.Stop(locationHandle);
            if (combatPlaying)
            {
                audio.StopBGM(combatMusic);
            }
        }
        outdoorHandle = default;
        indoorHandle = default;
        locationHandle = default;
        combatPlaying = false;
        lastThreat = float.NegativeInfinity;
    }
}

