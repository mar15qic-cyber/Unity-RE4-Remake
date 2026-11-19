using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal enum VillageSpeechCue
{
    DryFire = 0,
    KnifeBroken = 1,
    GunKill = 2,
    Kick = 3,
    LadderDown = 4,
    SawSeen = 5,
    SawParry = 6,
    SawHalfHealth = 7,
    SawKilled = 8,
    LastHealCombat = 9,
    LastHealQuiet = 10,
    ShotgunPickup = 11,
    CombatStart = 12
}

[DisallowMultipleComponent]
public sealed class VillageLeonDialogue : MonoBehaviour
{
    [Serializable]
    private sealed class DialogueData
    {
        public LineGroup[] groups = Array.Empty<LineGroup>();
        public Caption[] bell = Array.Empty<Caption>();
    }

    [Serializable]
    private sealed class LineGroup
    {
        public VillageSpeechCue cue;
        public float cooldown;
        public float delay;
        public int priority;
        public bool once;
        public bool wait;
        public int silentSlots;
        public Line[] lines = Array.Empty<Line>();
        [NonSerialized] public float nextAllowed;
        [NonSerialized] public bool played;
    }

    [Serializable]
    private sealed class Line
    {
        public string subtitle;
        public string clip;
        public bool firstOnly;
        public bool once;
        [NonSerialized] public bool played;
    }

    [Serializable]
    private sealed class Caption
    {
        public float start;
        public float end;
        public string subtitle;
    }

    private readonly struct PendingLine
    {
        internal readonly LineGroup Group;
        internal readonly float ReadyTime;

        internal PendingLine(LineGroup group, float readyTime)
        {
            Group = group;
            ReadyTime = readyTime;
        }
    }

    private const string ResourceFolder = "VillageDialogue/";
    private static VillageLeonDialogue active;
    private readonly Dictionary<VillageSpeechCue, LineGroup> groups = new();
    private readonly List<PendingLine> pending = new();
    private readonly System.Random selection = new();
    private readonly RaycastHit[] viewHits = new RaycastHit[16];
    private GameManager match;
    private EnemyAIManager enemies;
    private AudioSource voice;
    private Canvas subtitleCanvas;
    private Text subtitle;
    private DialogueData data;
    private LineGroup speaking;
    private bool voicePaused;
    private bool inCinematic;
    private bool bellStarted;
    private float nextVisibilityCheck;

    internal void Initialize(GameManager game, EnemyAIManager enemyGroup, Font font)
    {
        match = game;
        enemies = enemyGroup;
        TextAsset asset = Resources.Load<TextAsset>(ResourceFolder + "Lines");
        if (asset == null || font == null)
        {
            Debug.LogError("村庄对白缺少台词配置或字幕字体。", this);
            enabled = false;
            return;
        }

        data = JsonUtility.FromJson<DialogueData>(asset.text);
        foreach (LineGroup group in data.groups)
        {
            groups.Add(group.cue, group);
        }

        BuildPresentation(font);
        match.OnPlayerDied += StopSpeech;
        active = this;
    }

    internal static void Report(LeonController player, VillageSpeechCue cue)
    {
        if (active != null && player != null && player == active.match.player)
        {
            active.Request(cue);
        }
    }

    internal static void ReportGunKill(VillagerController target)
    {
        if (active != null && active.IsSessionEnemy(target) && target.missionEnemyType == MissionEnemyType.Ganado)
        {
            active.Request(VillageSpeechCue.GunKill);
        }
    }

    internal static void ReportEnemyHealth(VillagerController target, float before, float after)
    {
        if (active == null || !active.IsSessionEnemy(target) || target.missionEnemyType != MissionEnemyType.Sawman)
        {
            return;
        }

        float threshold = target.maxHealth * .5f;
        if (before > threshold && after > 0f && after <= threshold)
        {
            active.Request(VillageSpeechCue.SawHalfHealth);
        }
    }

    private bool IsSessionEnemy(VillagerController target)
    {
        return target != null && target.gameObject.scene == gameObject.scene && target.Player == match.player;
    }

    internal static void BeginBell(GameManager owner)
    {
        if (active != null && active.match == owner)
        {
            active.bellStarted = true;
            active.StopSpeech();
        }
    }

    internal static void ShowBellCaption(GameManager owner, double timelineTime)
    {
        if (active == null || active.match != owner)
        {
            return;
        }

        active.inCinematic = true;
        foreach (Caption caption in active.data.bell)
        {
            if (timelineTime >= caption.start && timelineTime < caption.end)
            {
                active.SetSubtitle(caption.subtitle);
                return;
            }
        }

        active.SetSubtitle(null);
    }

    internal static void EndBellCaption(GameManager owner)
    {
        if (active != null && active.match == owner)
        {
            active.inCinematic = false;
            active.SetSubtitle(null);
        }
    }

    private void Request(VillageSpeechCue cue)
    {
        if (bellStarted || match.IsTerminal || !groups.TryGetValue(cue, out LineGroup group))
        {
            return;
        }

        float now = Time.time;
        if (now < group.nextAllowed || group.once && group.played)
        {
            return;
        }

        foreach (PendingLine entry in pending)
        {
            if (entry.Group == group)
            {
                return;
            }
        }

        bool immediatelyAvailable = CanSpeak() && group.delay <= 0f;
        if (immediatelyAvailable && (speaking == null || group.priority > speaking.priority))
        {
            Play(group, now);
        }
        else if (group.wait || group.delay > 0f)
        {
            pending.Add(new PendingLine(group, now + group.delay));
        }
    }

    private void Update()
    {
        if (match == null || bellStarted || inCinematic)
        {
            return;
        }

        if (!CanSpeak())
        {
            if (speaking != null && !voicePaused)
            {
                voice.Pause();
                voicePaused = true;
                subtitleCanvas.enabled = false;
            }
            return;
        }

        UpdateVoiceVolume();
        if (voicePaused)
        {
            voicePaused = false;
            voice.UnPause();
            subtitleCanvas.enabled = !string.IsNullOrEmpty(subtitle.text);
        }

        if (speaking != null && !voice.isPlaying)
        {
            speaking = null;
            SetSubtitle(null);
        }

        float now = Time.time;
        if (now >= nextVisibilityCheck)
        {
            nextVisibilityCheck = now + .12f;
            CheckFirstSaw();
        }

        int selected = -1;
        for (int index = 0; index < pending.Count; index++)
        {
            PendingLine entry = pending[index];
            if (entry.ReadyTime <= now && (speaking == null || entry.Group.priority > speaking.priority)
                && (selected < 0 || entry.Group.priority > pending[selected].Group.priority))
            {
                selected = index;
            }
        }

        if (selected >= 0)
        {
            LineGroup group = pending[selected].Group;
            pending.RemoveAt(selected);
            Play(group, now);
        }
    }

    private bool CanSpeak()
    {
        return !match.IsTerminal && match.Permission.Current.AllowInput && Time.timeScale > 0f
            && !AudioListener.pause;
    }

    private void CheckFirstSaw()
    {
        if (!groups.TryGetValue(VillageSpeechCue.SawSeen, out LineGroup entry) || entry.played
            || !enemies.IsCombatUnlocked || match.player == null)
        {
            return;
        }

        Camera camera = match.player.playerCamera;
        if (camera == null || !camera.isActiveAndEnabled)
        {
            return;
        }

        foreach (VillagerController enemy in enemies.StandingContactEnemies)
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || enemy.missionEnemyType != MissionEnemyType.Sawman)
            {
                continue;
            }

            Vector3 root = enemy.transform.position;
            // 从实际游戏镜头检查头部和胸部，隔墙或背对电锯时不触发。
            if (IsVisible(camera, root + Vector3.up * 1.6f) || IsVisible(camera, root + Vector3.up * 1.15f))
            {
                Request(VillageSpeechCue.SawSeen);
                return;
            }
        }
    }

    private bool IsVisible(Camera camera, Vector3 point)
    {
        Vector3 viewport = camera.WorldToViewportPoint(point);
        if (viewport.z <= camera.nearClipPlane || viewport.z > camera.farClipPlane
            || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            return false;
        }

        Vector3 origin = camera.transform.position;
        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        int hits = Physics.RaycastNonAlloc(origin, offset / distance, viewHits, distance - .03f,
            1 << 0, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hits; index++)
        {
            Transform obstacle = viewHits[index].transform;
            if (obstacle != null && !obstacle.IsChildOf(match.player.transform)
                && obstacle.GetComponentInParent<VillagerController>() == null)
            {
                return false;
            }
        }

        return hits < viewHits.Length;
    }

    private void Play(LineGroup group, float now)
    {
        Line line = null;
        int choices = group.silentSlots;
        foreach (Line candidate in group.lines)
        {
            if (candidate.firstOnly && group.played || candidate.once && candidate.played)
            {
                continue;
            }

            choices++;
            if (selection.Next(choices) == 0)
            {
                line = candidate;
            }
        }

        group.nextAllowed = now + group.cooldown;
        group.played = true;
        if (line == null)
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(ResourceFolder + line.clip);
        if (clip == null)
        {
            Debug.LogError($"村庄对白缺少音频：{line.clip}", this);
            return;
        }

        line.played = true;
        speaking = group;
        voice.clip = clip;
        UpdateVoiceVolume();
        voice.Play();
        SetSubtitle(line.subtitle);
    }

    private void UpdateVoiceVolume()
    {
        AudioManager audio = AudioManager.Instance;
        float volume = .88f;
        if (audio != null)
        {
            volume *= audio.GetCategoryVolume(AudioCategory.Player);
            if (audio.settings != null && audio.settings.masterMixer != null
                && audio.settings.masterMixer.GetFloat(audio.settings.masterVolumeParam, out _))
            {
                // 本音源直通监听器，混音器接管主音量时补上相同的主音量系数。
                volume *= audio.MasterVolume;
            }
        }

        voice.volume = volume;
    }

    private void BuildPresentation(Font font)
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;

        var root = new GameObject("村庄对白字幕", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        subtitleCanvas = root.GetComponent<Canvas>();
        subtitleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        subtitleCanvas.sortingOrder = 900;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var label = new GameObject("简体中文字幕", typeof(RectTransform), typeof(Text), typeof(Shadow));
        label.transform.SetParent(root.transform, false);
        subtitle = label.GetComponent<Text>();
        subtitle.font = font;
        subtitle.fontSize = 30;
        subtitle.alignment = TextAnchor.LowerCenter;
        subtitle.color = new Color(.96f, .96f, .96f, 1f);
        subtitle.supportRichText = false;
        subtitle.raycastTarget = false;
        RectTransform rect = subtitle.rectTransform;
        rect.anchorMin = new Vector2(.08f, .058f);
        rect.anchorMax = new Vector2(.92f, .058f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = new Vector2(0f, 106f);
        rect.anchoredPosition = Vector2.zero;

        Shadow shadow = label.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .88f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        SetSubtitle(null);
    }

    private void SetSubtitle(string value)
    {
        if (subtitleCanvas == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(value))
        {
            subtitle.text = string.Empty;
            subtitleCanvas.enabled = false;
        }
        else
        {
            subtitle.text = value;
            subtitleCanvas.enabled = true;
        }
    }

    private void StopSpeech()
    {
        pending.Clear();
        speaking = null;
        voicePaused = false;
        if (voice != null)
        {
            voice.Stop();
        }
        SetSubtitle(null);
    }

    private void OnDisable()
    {
        if (match != null)
        {
            match.OnPlayerDied -= StopSpeech;
        }
        if (active == this)
        {
            active = null;
        }
        StopSpeech();
    }
}

