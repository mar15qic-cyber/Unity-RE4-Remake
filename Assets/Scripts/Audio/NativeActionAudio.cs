using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10010)]
public sealed class NativeActionAudio : MonoBehaviour
{
    public enum Position { Body, Hand, Sheath, Weapon, LeftFoot, RightFoot, Ground }
    public enum Pace { Walk, Run, Crouch }

    [Serializable]
    public sealed class Cue
    {
        public float seconds;
        public Position position;
        public Pace pace;
        public AudioEventData sound;
        public FootstepSurfaceProfile surfaceOverride;

        public bool IsFootstep => position is Position.LeftFoot or Position.RightFoot;
    }

    [Serializable]
    public sealed class Track
    {
        public string clip;
        public Cue[] cues;
    }

    [Tooltip("动画进度与独立声音的对应表")]
    public Track[] tracks = Array.Empty<Track>();
    [Tooltip("该角色的原版脚步材质配置")]
    public FootstepSurfaceProfile footsteps;
    [Tooltip("源装填片段已经拥有霰弹入仓音时，由动作进度播放")]
    public bool w870InsertMarkers;
    public bool nativeChainsawIgnition;
    public bool nativeWeaponMarkers;

    private readonly Dictionary<string, Track> byClip = new(StringComparer.Ordinal);
    private readonly List<AnimatorClipInfo> motions = new(4);
    private Animator animator;
    private LeonController leon;
    private KnifeCombatModule knife;
    private VillagerController villager;
    private FootstepController leonFootsteps;
    private LeonAnimatorDriver leonAnimatorDriver;
    private LeonWeaponVisualController weaponVisual;
    private ChainsawManAnimationBridge chainsaw;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftHand;
    private Transform rightHand;
    private Transform body;
    private Transform sheath;
    private EnemyHeldWeapon heldWeapon;
    private Cursor[] layers;
    private Cursor knifeCursor;
    private Cursor pairCursor;
    private Cursor playerPairCursor;
    private NativePlayerActionAudioProfile playerProfile;
    private readonly Dictionary<string, Track> playerPairTracks = new(StringComparer.Ordinal);

    private const float MarkerEpsilon = 0.00001f;

    private struct Cursor
    {
        public Track track;
        public long owner;
        public float progress;
    }

    public bool OwnsFootsteps { get; private set; }

    internal bool HasWeaponCue(AnimationClip clip)
    {
        if (clip == null || !isActiveAndEnabled || !byClip.TryGetValue(clip.name, out var track))
        {
            return false;
        }
        foreach (var cue in track.cues)
        {
            if (cue.position == Position.Weapon && cue.sound != null)
            {
                return true;
            }
        }
        return false;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        leon = GetComponent<LeonController>();
        knife = GetComponent<KnifeCombatModule>();
        villager = GetComponent<VillagerController>();
        leonFootsteps = GetComponent<FootstepController>();
        leonAnimatorDriver = GetComponent<LeonAnimatorDriver>();
        weaponVisual = GetComponent<LeonWeaponVisualController>();
        chainsaw = GetComponentInChildren<ChainsawManAnimationBridge>(true);
        heldWeapon = GetComponent<EnemyHeldWeapon>();
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        leftFoot = FindBone(bones, "leg left toes", "leg left ankle", "L_Foot");
        rightFoot = FindBone(bones, "leg right toes", "leg right ankle", "R_Foot");
        leftHand = FindBone(bones, "arm left wrist", "L_Hand");
        rightHand = FindBone(bones, "arm right wrist", "R_Hand");
        body = FindBone(bones, "spine upper", "spine middle", "Spine_2");
        sheath = FindBone(bones, "leg right thigh", "root hips");
        foreach (var track in tracks)
        {
            if (track != null && !string.IsNullOrEmpty(track.clip))
            {
                byClip.Add(track.clip, track);
            }
        }
        if (leon != null)
        {
            playerProfile = Resources.Load<NativePlayerActionAudioProfile>("Combat/LeonActionAudio");
            if (playerProfile != null && playerProfile.pairTracks != null)
            {
                foreach (var track in playerProfile.pairTracks)
                {
                    if (track != null && !string.IsNullOrEmpty(track.clip) && track.cues != null && track.cues.Length > 0)
                    {
                        playerPairTracks.Add(track.clip, track);
                    }
                }
            }
        }
    }

    internal void SeekPair(string trackName, int session, float seconds)
    {
        if (byClip.TryGetValue(trackName, out var track))
        {
            pairCursor = new Cursor { track = track, owner = session, progress = seconds - MarkerEpsilon };
        }
    }

    internal void SamplePair(string trackName, int session, float seconds, Transform soundOrigin)
    {
        var audio = AudioManager.Instance;
        if (isActiveAndEnabled && audio != null && !audio.IsAudioPaused && byClip.TryGetValue(trackName, out var track))
        {
            Advance(audio, ref pairCursor, track, session, seconds, 0f, 0f, soundOrigin);
        }
    }

    internal bool HasPlayerPairTrack(string trackName)
    {
        return isActiveAndEnabled && playerPairTracks.ContainsKey(trackName);
    }

    internal void SamplePlayerPair(string trackName, int session, float seconds)
    {
        var audio = AudioManager.Instance;
        if (isActiveAndEnabled && audio != null && !audio.IsAudioPaused && playerPairTracks.TryGetValue(trackName, out var track))
        {
            Advance(audio, ref playerPairCursor, track, session, seconds, 0f, 0f);
        }
    }

    internal void PlayLandingContact(Vector3 position, LayerMask groundMask)
    {
        var audio = AudioManager.Instance;
        if (!isActiveAndEnabled || audio == null || audio.IsAudioPaused || playerProfile == null
            || !FootstepSurface.TryFindGround(transform, position, 0.8f, groundMask, out var ground))
        {
            return;
        }

        var sound = FootstepSurface.Resolve(ground) switch
        {
            SurfaceType.Grass => playerProfile.grassLanding,
            SurfaceType.Concrete => playerProfile.stoneLanding,
            SurfaceType.Wood => playerProfile.woodLanding,
            _ => playerProfile.dirtLanding
        };
        if (sound != null)
        {
            audio.Play3D(sound, ground.point);
        }
    }

    private void OnDisable()
    {
        layers = null;
        knifeCursor = default;
        pairCursor = default;
        playerPairCursor = default;
        OwnsFootsteps = false;
    }

    private Transform FindBone(Transform[] bones, string name, string alternate, string enemyBone = null)
    {
        Transform fallback = transform;
        foreach (Transform bone in bones)
        {
            string boneName = bone.name;
            if (boneName == name)
            {
                return bone;
            }
            if (boneName == alternate || boneName == enemyBone)
            {
                fallback = bone;
            }
        }
        return fallback;
    }

    private void LateUpdate()
    {
        var audio = AudioManager.Instance;
        if (audio == null || audio.IsAudioPaused || Time.timeScale <= 0f)
        {
            return;
        }
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null
            || (leon != null && leon.currentState == LeonState.Dead) || (villager != null && villager.IsDead))
        {
            OnDisable();
            return;
        }
        if (layers == null || layers.Length != animator.layerCount)
        {
            layers = new Cursor[animator.layerCount];
        }
        OwnsFootsteps = false;

        bool independentKnife = knife != null && knife.Active;
        if (independentKnife)
        {
            var action = knife.configuration.Find(knife.CurrentAction);
            if (action != null && action.sourceClip != null && byClip.TryGetValue(action.sourceClip.name, out var track))
            {
                float start = action.sourceStart;
                float time = start + (float)knife.CurrentActionTime;
                bool suppressGuardFootsteps = leonAnimatorDriver != null && leonAnimatorDriver.OwnsKnifeLocomotionFootsteps
                    && !knife.IsAttack && !GameArchitecture.KnifeCombatRules.IsReaction(knife.CurrentAction);
                Advance(audio, ref knifeCursor, track, knife.CurrentInstance, time, 0f, start,
                    suppressFootsteps: suppressGuardFootsteps);
            }
            else
            {
                knifeCursor = default;
            }
        }
        else
        {
            knifeCursor = default;
        }

        for (int layer = layers.Length - 1; layer >= 0; layer--)
        {
            if ((layer != 0 && animator.GetLayerWeight(layer) < 0.001f)
                || (independentKnife && layer == AnimatorLayerID.WeaponAction))
            {
                layers[layer] = default;
                continue;
            }
            ReadLayer(audio, layer);
        }
    }

    private void ReadLayer(AudioManager audio, int layer)
    {
        bool next = animator.IsInTransition(layer) && (layer != 0 || animator.GetAnimatorTransitionInfo(layer).normalizedTime >= 0.5f);
        AnimatorStateInfo state;
        motions.Clear();
        if (next)
        {
            state = animator.GetNextAnimatorStateInfo(layer);
            animator.GetNextAnimatorClipInfo(layer, motions);
        }
        else
        {
            state = animator.GetCurrentAnimatorStateInfo(layer);
            animator.GetCurrentAnimatorClipInfo(layer, motions);
        }

        AnimationClip clip = null;
        float weight = float.NegativeInfinity;
        foreach (var info in motions)
        {
            if (info.weight > weight)
            {
                clip = info.clip;
                weight = info.weight;
            }
        }
        if (clip == null || !byClip.TryGetValue(clip.name, out var track))
        {
            layers[layer] = default;
            return;
        }

        float cycle = 0f;
        if (state.loop)
        {
            cycle = clip.length;
        }
        float time = state.normalizedTime * clip.length;
        float step = Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) * Mathf.Abs(state.speed * state.speedMultiplier * animator.speed);
        bool suppressFootsteps = layer == 0 && leonAnimatorDriver != null && leonAnimatorDriver.OwnsKnifeLocomotionFootsteps;
        Advance(audio, ref layers[layer], track, state.fullPathHash, time, cycle,
            Mathf.Max(0f, time - step - 0.0001f), suppressFootsteps: suppressFootsteps);
    }

    private void Advance(AudioManager audio, ref Cursor cursor, Track track, long owner, float time, float cycle, float entry, Transform soundOrigin = null, bool suppressFootsteps = false)
    {
        if (!float.IsFinite(time) || time < 0f)
        {
            return;
        }
        bool newInstance = cursor.track != track || cursor.owner != owner || time < cursor.progress - MarkerEpsilon;
        float before = cursor.progress;
        if (newInstance)
        {
            before = entry - 2f * MarkerEpsilon;
        }
        cursor = new Cursor { track = track, owner = owner, progress = time };
        bool alreadyOwnsFootsteps = OwnsFootsteps;

        foreach (var cue in track.cues)
        {
            if (cue.IsFootstep)
            {
                OwnsFootsteps = true;
                if (alreadyOwnsFootsteps || suppressFootsteps)
                {
                    continue;
                }
            }
            if (cycle > 0f)
            {
                int loop = Mathf.Max(0, Mathf.FloorToInt((before + MarkerEpsilon - cue.seconds) / cycle) + 1);
                int latest = Mathf.FloorToInt((time + MarkerEpsilon - cue.seconds) / cycle);
                if (latest >= loop)
                {
                    Play(audio, cue, soundOrigin);
                }
            }
            else if (cue.seconds > before + MarkerEpsilon && cue.seconds <= time + MarkerEpsilon)
            {
                Play(audio, cue, soundOrigin);
            }
        }
    }

    private void Play(AudioManager audio, Cue cue, Transform soundOrigin)
    {
        Transform origin = body;
        switch (cue.position)
        {
            case Position.Hand:
                origin = leftHand;
                break;
            case Position.Sheath:
                origin = sheath;
                break;
            case Position.Weapon:
                origin = rightHand;
                if (chainsaw != null && chainsaw.chainsawDamageOrigin != null)
                {
                    origin = chainsaw.chainsawDamageOrigin;
                }
                else if (heldWeapon != null && heldWeapon.WeaponTransform != null)
                {
                    origin = heldWeapon.WeaponTransform;
                }
                else if (weaponVisual != null && leon != null)
                {
                    var visual = weaponVisual.GetWeaponVisualRoot(leon.currentWeapon);
                    if (visual != null)
                    {
                        origin = visual;
                    }
                }
                break;
            case Position.LeftFoot:
                origin = leftFoot;
                break;
            case Position.RightFoot:
                origin = rightFoot;
                break;
            case Position.Ground:
                origin = transform;
                break;
        }

        if (cue.IsFootstep || cue.position == Position.Ground)
        {
            var profile = cue.surfaceOverride;
            if (profile == null)
            {
                profile = footsteps;
            }
            if (!FootstepSurface.TryFindGround(transform, origin.position, 0.8f, ~0, out var hit))
            {
                return;
            }

            var sound = cue.sound;
            if (profile != null && sound == null)
            {
                var surface = FootstepSurface.Resolve(hit);
                switch (cue.pace)
                {
                    case Pace.Run:
                        sound = profile.GetRunEvent(surface);
                        break;
                    case Pace.Crouch:
                        sound = profile.GetCrouchEvent(surface);
                        break;
                    default:
                        sound = profile.GetWalkEvent(surface);
                        break;
                }
            }
            if (sound != null)
            {
                audio.Play3D(sound, hit.point);
            }
            if (cue.IsFootstep && leonFootsteps != null)
            {
                if (sound != null)
                {
                    leonFootsteps.RecordNativeFootstep();
                }
                leonFootsteps.PlayNativeGrass(audio, hit.point);
            }
            return;
        }
        if (cue.sound != null)
        {
            if (soundOrigin != null)
            {
                origin = soundOrigin;
            }
            audio.Play3D(cue.sound, origin.position);
        }
    }
}

