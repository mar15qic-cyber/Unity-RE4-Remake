using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed class BurningBarnCow : MonoBehaviour
{
    [SerializeField, Tooltip("独立于碰撞的牛模型和动画。")]
    private Transform visual;
    [SerializeField]
    private Animator modelAnimator;
    [SerializeField]
    private AnimationClip idle;
    [SerializeField]
    private AnimationClip runStart;
    [SerializeField]
    private AnimationClip runLoop;
    [SerializeField]
    private AnimationClip collapse;
    [SerializeField, Tooltip("身体阻挡。冲撞扫掠沿用同一组尺寸。")]
    private BoxCollider hull;
    [SerializeField, Tooltip("跟随牛身体的火焰和烟。")]
    private ParticleSystem flames;
    [SerializeField]
    private ParticleSystem smoke;
    [SerializeField, Tooltip("牛在棚内待机时的声音。")]
    private AudioEventData idleAudio;
    [SerializeField, Tooltip("挂灯点燃黑牛时的受惊声。")]
    private AudioEventData startleAudio;
    [SerializeField, Tooltip("着火奔跑期间的叫声。")]
    private AudioEventData bellow;
    [SerializeField]
    private AudioEventData hooves;
    [SerializeField, Tooltip("原始跑步动作的第二层声音。")]
    private AudioEventData runLayerAudio;
    [SerializeField, Tooltip("高速正面冲撞时的牛叫。")]
    private AudioEventData chargeAudio;
    [SerializeField]
    private AudioEventData burnLoop;
    [SerializeField]
    private AudioEventData bodyImpact;
    [SerializeField, Tooltip("倒地动作前段的死亡叫声。")]
    private AudioEventData collapseVoice;
    [SerializeField, Tooltip("倒地动作的身体触地声。")]
    private AudioEventData collapseImpact;

    [SerializeField, Min(1f), Tooltip("着火后持续奔走的时间。")]
    private float burnDuration = 15f;
    [SerializeField, Min(0.1f), Tooltip("正面高速顶中时的生命扣减。")]
    private float impactDamage = 32f;
    [SerializeField, Tooltip("奔走时参与碰撞的地形和角色层。")]
    private LayerMask movementMask = ~0;

    private readonly RaycastHit[] contacts = new RaycastHit[24];
    private readonly Collider[] turnContacts = new Collider[24];
    private readonly HashSet<int> struck = new HashSet<int>();
    private Rigidbody body;
    private float burnedFor;
    private float speed;
    private float segmentTime;
    private float idleAudioDelay = .35f;
    private float nextBurnVoice = 2.7f;
    private float deflectionTime;
    private float fallTime;
    private Vector3 barnPosition;
    private Vector3 heading;
    private const int IdleInput = 0;
    private const int RunStartInput = 1;
    private const int RunLoopInput = 2;
    private const int CollapseInput = 3;
    // 原始左侧倒地动作以每秒 60 帧记录声音标记。
    private const float CollapseVoiceTime = 10f / 60f;
    private const float CollapseImpactTime = 37f / 60f;
    private bool burning;
    private bool falling;
    private bool dead;
    private AudioPlayHandle fireSound;
    private AudioPlayHandle voiceSound;
    private AudioPlayHandle hoofSound;
    private AudioPlayHandle runLayerSound;
    private AudioPlayHandle landingSound;
    private PlayableGraph motionGraph;
    private AnimationMixerPlayable mixer;

    private void Start()
    {
        body = GetComponent<Rigidbody>();
        barnPosition = transform.position;
        if (modelAnimator == null || idle == null || runStart == null || runLoop == null || collapse == null)
        {
            Debug.LogError("牛棚黑牛缺少骨架或动作绑定。", this);
            enabled = false;
            return;
        }

        motionGraph = PlayableGraph.Create("牛棚步态");
        mixer = AnimationMixerPlayable.Create(motionGraph, 4);
        BindMotion(idle, IdleInput);
        BindMotion(runStart, RunStartInput);
        BindMotion(runLoop, RunLoopInput);
        BindMotion(collapse, CollapseInput);
        mixer.SetInputWeight(IdleInput, 1f);
        mixer.GetInput(IdleInput).SetSpeed(1f);
        AnimationPlayableOutput.Create(motionGraph, "牛的步态", modelAnimator).SetSourcePlayable(mixer);
        motionGraph.Play();
    }

    private void BindMotion(AnimationClip clip, int inputIndex)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(motionGraph, clip);
        playable.SetSpeed(0f);
        motionGraph.Connect(playable, 0, mixer, inputIndex);
    }

    internal void Ignite()
    {
        if (!isActiveAndEnabled || !motionGraph.IsValid() || burning || falling || dead)
        {
            return;
        }

        burning = true;
        heading = transform.forward;
        segmentTime = 1.2f;
        mixer.GetInput(RunStartInput).SetTime(0d);
        mixer.GetInput(RunStartInput).SetSpeed(1f);
        mixer.GetInput(RunLoopInput).SetTime(0d);
        flames.Play();
        smoke.Play();
        ReplaceVoice(startleAudio);
        fireSound = PlayCue(burnLoop, transform.position + Vector3.up);
    }

    private void FixedUpdate()
    {
        if (!burning)
        {
            return;
        }
        if (!GameManager.HasInstance || GameManager.Instance.IsTerminal)
        {
            EndBurn();
            return;
        }

        float step = Time.fixedDeltaTime;
        burnedFor += step;
        if (burnedFor >= burnDuration)
        {
            StartCollapse();
            return;
        }

        UpdateRunMotion();
        if (burnedFor < .52f)
        {
            return;
        }

        segmentTime -= step;
        deflectionTime = Mathf.Max(0f, deflectionTime - step);
        if (segmentTime <= 0f)
        {
            PickDirection(false);
        }

        speed = Mathf.MoveTowards(speed, 3.4f, step * 4.7f);
        Quaternion facing = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(heading), 135f * step);
        Vector3 forward = facing * Vector3.forward;
        float distance = speed * step;
        Vector3 position = body.position;
        Vector3 extents = Vector3.Scale(hull.size, hull.transform.lossyScale) * 0.5f;
        Vector3 sweepCenter = position + facing * hull.center + Vector3.up * .06f;
        int count = Physics.BoxCastNonAlloc(sweepCenter, extents * .94f, forward, contacts, facing,
            distance + .04f, movementMask, QueryTriggerInteraction.Ignore);
        float closest = distance + .05f;
        int firstHit = -1;
        for (int index = 0; index < count; index++)
        {
            if (contacts[index].collider.transform.IsChildOf(transform) || contacts[index].distance >= closest)
            {
                continue;
            }
            firstHit = index;
            closest = contacts[index].distance;
        }
        if (firstHit >= 0)
        {
            Strike(contacts[firstHit], forward);
            distance = Mathf.Clamp(closest - .05f, 0f, distance);
            PickDirection(true);
        }

        Vector3 next = position + forward * distance;
        if (!Physics.Raycast(next + Vector3.up * .55f, Vector3.down, out RaycastHit ground, 1.0f,
                1, QueryTriggerInteraction.Ignore) || ground.normal.y < .7f || Mathf.Abs(ground.point.y - position.y) > .34f)
        {
            PickDirection(true);
            next = position;
        }
        else
        {
            next.y = ground.point.y + .02f;
        }

        float allowedYaw = Quaternion.Angle(body.rotation, facing);
        Vector3 nextCenter = next + facing * hull.center + Vector3.up * .07f;
        if (allowedYaw <= .01f || CanRotate(nextCenter, extents * .89f, facing))
        {
            body.MoveRotation(facing);
        }
        body.MovePosition(next);
        if (Vector3.ProjectOnPlane(next - position, Vector3.up).sqrMagnitude > .0004f)
        {
            if (!hoofSound.IsValid)
            {
                hoofSound = PlayCue(hooves, transform.position);
            }
            if (!runLayerSound.IsValid)
            {
                runLayerSound = PlayCue(runLayerAudio, transform.position + Vector3.up * .65f);
            }
        }
    }

    private void UpdateRunMotion()
    {
        float startWeight = Mathf.InverseLerp(0f, .12f, burnedFor);
        float loopWeight = Mathf.InverseLerp(runStart.length - .16f, runStart.length, burnedFor);
        mixer.SetInputWeight(IdleInput, 1f - startWeight);
        mixer.SetInputWeight(RunStartInput, startWeight - loopWeight);
        mixer.SetInputWeight(RunLoopInput, loopWeight);
        if (loopWeight > 0f)
        {
            mixer.GetInput(RunLoopInput).SetSpeed(Mathf.Max(.25f, speed / 4.8f));
        }
    }

    private bool CanRotate(Vector3 center, Vector3 extents, Quaternion facing)
    {
        int count = Physics.OverlapBoxNonAlloc(center, extents, turnContacts, facing, movementMask, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            if (!turnContacts[index].transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private void PickDirection(bool deflect)
    {
        if (deflect && deflectionTime > 0f)
        {
            return;
        }

        float change = Random.Range(-46f, 46f);
        if (deflect)
        {
            deflectionTime = .58f;
            change = Random.Range(85f, 155f);
            if (Random.value < .5f)
            {
                change = -change;
            }
            speed = Mathf.Min(speed, .7f);
        }

        heading = Quaternion.AngleAxis(change, Vector3.up) * transform.forward;
        Vector3 toBarn = Vector3.ProjectOnPlane(barnPosition - body.position, Vector3.up);
        if (toBarn.sqrMagnitude > 140f && Vector3.Dot(heading, toBarn) < 0f)
        {
            heading = toBarn.normalized;
        }
        if (!deflect || segmentTime <= 0f)
        {
            struck.Clear();
        }
        segmentTime = Random.Range(1.15f, 2.2f);
    }

    private void Strike(RaycastHit hit, Vector3 forward)
    {
        LeonController player = hit.collider.GetComponentInParent<LeonController>();
        VillagerController enemy = hit.collider.GetComponentInParent<VillagerController>();
        if (speed < .85f || player == null && enemy == null)
        {
            return;
        }

        Component target = player;
        if (target == null)
        {
            target = enemy;
        }
        if (!struck.Add(target.GetInstanceID()))
        {
            return;
        }

        Vector3 away = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
        bool strong = speed > 2.4f && Vector3.Dot(away, forward) >= .7f;
        float damage = impactDamage * .35f;
        if (strong)
        {
            damage = impactDamage;
        }
        if (player != null)
        {
            player.TakeEnvironmentalImpact(damage, transform.position, hit.point, strong);
        }
        else if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeDamage(damage, hit.point, HitboxType.Chest, -forward);
            if (strong && enemy.combatModule != null)
            {
                enemy.combatModule.TryBeginBodyTechniqueKnockdown(transform.position, .9f);
            }
        }

        if (strong)
        {
            ReplaceVoice(chargeAudio);
        }
        PlayCue(bodyImpact, hit.point);
    }

    private void StartCollapse()
    {
        falling = true;
        EndBurn();
        StopCue(ref voiceSound);
        mixer.GetInput(CollapseInput).SetTime(0d);
        mixer.GetInput(CollapseInput).SetSpeed(1f);
    }

    private void Update()
    {
        if (!GameManager.HasInstance || GameManager.Instance.IsTerminal)
        {
            if (burning)
            {
                EndBurn();
            }
            StopCue(ref voiceSound);
            StopCue(ref landingSound);
            return;
        }

        UpdateAudio();
        if (!falling)
        {
            return;
        }
        float previousFallTime = fallTime;
        fallTime += Time.deltaTime;
        if (previousFallTime < CollapseVoiceTime && fallTime >= CollapseVoiceTime)
        {
            ReplaceVoice(collapseVoice);
        }
        if (previousFallTime < CollapseImpactTime && fallTime >= CollapseImpactTime)
        {
            landingSound = PlayCue(collapseImpact, transform.position + Vector3.up * .25f);
        }
        float weight = Mathf.Clamp01(fallTime / .15f);
        mixer.SetInputWeight(RunLoopInput, 1f - weight);
        mixer.SetInputWeight(CollapseInput, weight);
        if (fallTime >= collapse.length)
        {
            falling = false;
            dead = true;
            mixer.GetInput(CollapseInput).SetTime(collapse.length);
            mixer.GetInput(CollapseInput).SetSpeed(0f);
            hull.size = new Vector3(1.48f, .55f, hull.size.z);
            hull.center = new Vector3(hull.center.x, .29f, hull.center.z);
            var charred = new MaterialPropertyBlock();
            charred.SetColor("_Color", new Color(.14f, .12f, .10f));
            foreach (var skin in visual.GetComponentsInChildren<Renderer>())
            {
                skin.SetPropertyBlock(charred);
            }
        }
    }

    private void UpdateAudio()
    {
        Vector3 position = transform.position;
        MoveCue(fireSound, position + Vector3.up);
        MoveCue(voiceSound, position + transform.forward * .95f + Vector3.up * 1.2f);
        MoveCue(hoofSound, position + Vector3.up * .1f);
        MoveCue(runLayerSound, position + Vector3.up * .65f);
        if (voiceSound.IsValid || falling || dead)
        {
            return;
        }

        if (burning)
        {
            if (burnedFor >= nextBurnVoice)
            {
                ReplaceVoice(bellow);
                nextBurnVoice = burnedFor + Random.Range(3.8f, 5f);
            }
            return;
        }

        idleAudioDelay -= Time.deltaTime;
        if (idleAudioDelay <= 0f)
        {
            ReplaceVoice(idleAudio);
            idleAudioDelay = Random.Range(1.6f, 3f);
        }
    }

    private void ReplaceVoice(AudioEventData effect)
    {
        StopCue(ref voiceSound);
        voiceSound = PlayCue(effect, transform.position + transform.forward * .95f + Vector3.up * 1.2f);
    }

    private static AudioPlayHandle PlayCue(AudioEventData effect, Vector3 position)
    {
        if (effect == null || !AudioManager.HasInstance)
        {
            return default;
        }
        return AudioManager.Instance.Play3D(effect, position);
    }

    private static void MoveCue(AudioPlayHandle handle, Vector3 position)
    {
        if (handle.IsValid)
        {
            handle.source.transform.position = position;
        }
    }

    private static void StopCue(ref AudioPlayHandle handle)
    {
        if (handle.IsValid && AudioManager.HasInstance)
        {
            AudioManager.Instance.Stop(handle);
        }
        handle = default;
    }

    private void EndBurn()
    {
        burning = false;
        flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StopCue(ref fireSound);
        StopCue(ref hoofSound);
        StopCue(ref runLayerSound);
    }

    private void OnDisable()
    {
        if (burning || falling)
        {
            EndBurn();
        }
        StopCue(ref voiceSound);
        StopCue(ref landingSound);
        if (motionGraph.IsValid())
        {
            motionGraph.Stop();
        }
    }

    private void OnEnable()
    {
        if (motionGraph.IsValid())
        {
            motionGraph.Play();
        }
    }

    private void OnDestroy()
    {
        if (motionGraph.IsValid())
        {
            motionGraph.Destroy();
        }
    }
}
