using System;
using System.Collections;
using System.Collections.Generic;
using GameArchitecture;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VillageBellSequence : MonoBehaviour
{
    internal const string VillageScenePath = "Assets/RE4ChapterOneVillage/Lighting/VillageBattleLighting.unity";
    private const string ResourceName = "VillageBell/VillageBellSequence";

    [Header("鸣钟与村民退场")]
    [SerializeField] private AudioSource bellSource;
    [SerializeField] private AudioClip bellStrike;
    [SerializeField] private AudioClip bellTail;
    [Tooltip("钟楼下方的场景定位点。")]
    [SerializeField] private Vector3 exitApproach;
    [Tooltip("钟楼凝视点相对定位点的高度。")]
    [SerializeField] private float bellTowerLookHeight = 8f;
    [Tooltip("历史广场标记，仅保留序列化兼容；达成条件即可鸣钟。")]
    [SerializeField] private Bounds plazaTrigger = new Bounds(new Vector3(-5f, .25f, -.8f), new Vector3(28f, 2.4f, 10f));
    [SerializeField] private float cutAfterSeconds = 15.877f;
    [SerializeField] private float strikeInterval = 5.242f;
    [Header("过场与黑幕")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private GameObject stage;
    [Tooltip("战斗和演出共用的火堆效果根节点。")]
    [SerializeField] private Transform bonfire;
    [SerializeField] private Image darkness;
    [SerializeField] private RawImage title;

    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private readonly List<Renderer> hiddenActors = new List<Renderer>();
    private GameplayPermissionHandle inputLease;
    internal static bool IsPresenting { get; private set; }
    private bool ownsPresentation;
    private GameManager match;
    private Camera gameplayCamera;
    private Action complete;
    private Coroutine playback;
    private bool started;
    private bool ended;
    private bool completed;
    private bool cameraWasEnabled;
    private VillageSoundscape soundscape;
    private bool soundscapeWasEnabled;
    private ParticleSystem[] bonfireParticles;
    private bool[] bonfireParticleClocks;
    private Light bonfireLight;
    private float bonfireLightIntensity;

    internal static VillageBellSequence Load(Transform parent)
    {
        var prefab = Resources.Load<VillageBellSequence>(ResourceName);
        if (prefab == null || prefab.director == null || prefab.director.playableAsset == null ||
            prefab.cinematicCamera == null || prefab.stage == null || prefab.darkness == null ||
            prefab.title == null || prefab.bellSource == null || prefab.bellStrike == null)
        {
            Debug.LogError("正式村庄缺少鸣钟过场资源或绑定。", parent);
            return null;
        }

        var sequence = Instantiate(prefab, parent);
        sequence.ActivateGameplayBonfire();
        return sequence;
    }

    private void ActivateGameplayBonfire()
    {
        if (bonfire == null)
        {
            return;
        }

        bonfire.SetParent(transform, true);
        bonfire.gameObject.SetActive(true);
        bonfireParticles = bonfire.GetComponentsInChildren<ParticleSystem>(true);
        bonfireParticleClocks = new bool[bonfireParticles.Length];
        for (int i = 0; i < bonfireParticles.Length; i++)
        {
            var particle = bonfireParticles[i];
            bonfireParticleClocks[i] = particle.main.useUnscaledTime;
            if (particle.gameObject.activeInHierarchy && !particle.isPlaying)
            {
                particle.Play(false);
            }
        }

        if (cinematicCamera.TryGetComponent<VillageBellEffects>(out var effects))
        {
            bonfireLight = effects.fireLight;
            if (bonfireLight != null)
            {
                bonfireLightIntensity = bonfireLight.intensity;
            }
        }
    }

    private void SetBonfirePresentationTime(bool presenting)
    {
        if (bonfireParticles == null)
        {
            return;
        }

        for (int i = 0; i < bonfireParticles.Length; i++)
        {
            var settings = bonfireParticles[i].main;
            settings.useUnscaledTime = presenting || bonfireParticleClocks[i];
        }

        if (!presenting && bonfireLight != null)
        {
            bonfireLight.intensity = bonfireLightIntensity;
        }
    }

    internal bool IsOnPlaza(LeonController player)
    {
        return player != null && player.IsGrounded && plazaTrigger.Contains(player.transform.position);
    }

    internal void Begin(GameManager owner, EnemyAIManager enemies, Action onComplete)
    {
        if (started || owner == null || owner.IsTerminal)
        {
            return;
        }

        started = true;
        match = owner;
        VillageLeonDialogue.BeginBell(owner);
        complete = onComplete;
        match.OnPlayerDied += HandlePlayerDeath;
        playback = StartCoroutine(PlaySequence(enemies));
    }

    private IEnumerator PlaySequence(EnemyAIManager enemies)
    {
        // 等待同一帧提交的界面关闭，避免异步载入的公文包抢回相机。
        while (InventorySceneLoader.HasInstance &&
            (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading)
            || StorageBoxController.IsAnyBoxBusy || ItemInspectionUI.HasInstance && ItemInspectionUI.Instance.IsVisible)
        {
            if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
            {
                StorageBoxUI.Instance.Close();
            }
            if (ItemInspectionUI.HasInstance)
            {
                ItemInspectionUI.Instance.RequestClose();
            }
            yield return null;
        }
        float start = Time.unscaledTime;
        bellSource.ignoreListenerPause = true;
#if DEVELOPMENT_BUILD
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-villageBellPreviewAudio") >= 0)
        {
            previewAudioActive = true;
            previousListenerVolume = AudioListener.volume;
            AudioListener.volume = 1f;
        }
#endif
        float volume = GetPlaybackVolume();
        bellSource.PlayOneShot(bellStrike, volume);
        soundscape = FindFirstObjectByType<VillageSoundscape>();
        enemies.BeginBellWithdrawal(exitApproach + Vector3.up * bellTowerLookHeight, soundscape);
        if (soundscape != null)
        {
            soundscapeWasEnabled = soundscape.enabled;
            soundscape.enabled = false;
        }
        Debug.Log("村庄鸣钟：首声钟响，村民停止追击并退场。", this);
        // 图表的绑定与资源初始化分散在钟声期间，摄像机和舞台仍保持关闭。
        yield return null;
        director.timeUpdateMode = DirectorUpdateMode.DSPClock;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.RebuildGraph();
        yield return null;
        if (cinematicCamera.TryGetComponent<VillageBellEffects>(out var effects))
        {
            effects.PrepareReflection();
        }
        float interval = Mathf.Max(1f, strikeInterval);
        float cutTime = Mathf.Max(cutAfterSeconds, interval * 3f);
        int strikes = 1;
        while (true)
        {
            float elapsed = Time.unscaledTime - start;
            if (elapsed >= strikes * interval)
            {
                bellSource.PlayOneShot(bellStrike, volume);
                strikes++;
            }
            bool reachedRegularCut = strikes >= 4 && elapsed >= cutTime;
            bool nearbyReaction = strikes >= 2 && enemies.HasNearbyBellReaction(match.player);
            if ((reachedRegularCut || nearbyReaction) && CanEnterCinematic())
            {
                break;
            }
            yield return null;
        }
        // 当前钟声自然衰减，切镜不重新起音。
        EnterCinematic();
        stage.SetActive(true);
        director.time = 0;
        director.Evaluate();
        HideGameplayActors();
        SetCurtainOpacity(0f);
        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = false;
        }
        cinematicCamera.enabled = true;
        // 黑幕下先让过场相机出一帧，再开始计时和淡入。
        yield return null;
        Debug.Log("村庄鸣钟：切入 Timeline。", this);
        director.Play();
        double startClock = AudioSettings.dspTime;
        double startReal = Time.unscaledTimeAsDouble;
        double duration = director.duration;
        while (director.time < duration - 0.02 &&
            Time.unscaledTimeAsDouble - startReal < duration + 2.0)
        {
            float time = (float)director.time;
            UpdateTitle(time);
            VillageLeonDialogue.ShowBellCaption(match, director.time);
#if DEVELOPMENT_BUILD
            CapturePreviewFrame(director.time);
#endif
            yield return null;
        }

        Debug.Log($"村庄鸣钟：过场到达黑幕，音频时间 {AudioSettings.dspTime - startClock:F2} 秒。", this);
        playback = null;
        Finish();
    }

    private void SetCurtainOpacity(float alpha)
    {
        darkness.enabled = true;
        darkness.color = new Color(0f, 0f, 0f, alpha);
    }

    private bool CanEnterCinematic()
    {
        LeonController player = match.player;
        if (player == null || !player.IsGrounded || !match.Permission.Current.AllowInput
            || player.AnimCoordinator.HasActiveFullBodyAction)
        {
            return false;
        }
        var paired = player.GetComponent<LeonQTECoordinator>();
        if (paired != null && paired.IsActive)
        {
            return false;
        }

        // 从头顶检查屋顶；屋面上的玩家上方没有屋顶，仍可正常转场。
        Vector3 origin = player.transform.position + Vector3.up * 1.7f;
        if (soundscape != null)
        {
            return !soundscape.HasInteriorCover(origin, 30f);
        }
        return !Physics.Raycast(origin, Vector3.up, 30f, 1 << 0, QueryTriggerInteraction.Ignore);
    }

    private void EnterCinematic()
    {
        IsPresenting = true;
        ownsPresentation = true;
        SetBonfirePresentationTime(true);
        inputLease = match.Permission.Request(new ObjectIdentity(GetInstanceID(), 1), true, true, false, 0f);
        GameUIManager ui = GameUIManager.Instance;
        if (ui != null)
        {
            ui.ClearAllModals();
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.transform.IsChildOf(transform) && canvas.enabled)
            {
                hiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }
        }

        gameplayCamera = match.player.playerCamera;
        if (gameplayCamera != null)
        {
            cameraWasEnabled = gameplayCamera.enabled;
        }
        foreach (ParticleSystem effect in stage.GetComponentsInChildren<ParticleSystem>(true))
        {
            var settings = effect.main;
            settings.useUnscaledTime = true;
        }
        float volume = GetPlaybackVolume();
        foreach (var source in stage.GetComponentsInChildren<AudioSource>(true))
        {
            source.volume = volume;
        }
    }

    private void HideGameplayActors()
    {
        HideActor(match.player.transform);
        foreach (var enemy in FindObjectsByType<VillagerController>(FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene == gameObject.scene)
            {
                HideActor(enemy.transform);
            }
        }
    }

    private float GetPlaybackVolume()
    {
#if DEVELOPMENT_BUILD
        if (previewAudioActive)
        {
            return 1f;
        }
#endif
        float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        return Mathf.Clamp01(masterVolume / Mathf.Max(0.0001f, AudioListener.volume));
    }

    private void UpdateTitle(float time)
    {
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(75.9f, 76.65f, time));
        float fadeOut = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(84.3f, 86.92f, time));
        title.color = new Color(1f, 1f, 1f, fadeIn * fadeOut);
        darkness.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(81.5f, 82.9f, time)));
    }

    private void Finish()
    {
        if (ended)
        {
            return;
        }

        ended = true;
        completed = true;
        director.Stop();
        VillageLeonDialogue.EndBellCaption(match);
        UpdateTitle(88.2f);
        RestoreCanvases();
        var callback = complete;
        complete = null;
        if (callback != null)
        {
            callback();
        }
        StartCoroutine(ReleaseBlackScreen());
    }

    private IEnumerator ReleaseBlackScreen()
    {
        yield return new WaitForSecondsRealtime(0.75f);
        darkness.enabled = false;
        stage.SetActive(false);
        RestoreGameplay();
        Debug.Log("村庄鸣钟：已交给结尾菜单。", this);
#if DEVELOPMENT_BUILD
        if (HasPreviewArgument())
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("VillageBell-menu.png"));
        }
#endif
    }

    private void RestoreCanvases()
    {
        foreach (var canvas in hiddenCanvases)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }
        hiddenCanvases.Clear();
    }

    private void HideActor(Transform actor)
    {
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.forceRenderingOff)
            {
                hiddenActors.Add(renderer);
                renderer.forceRenderingOff = true;
            }
        }
    }

    private void RestoreGameplay()
    {
        VillageLeonDialogue.EndBellCaption(match);
        if (ownsPresentation)
        {
            IsPresenting = false;
            ownsPresentation = false;
            SetBonfirePresentationTime(false);
        }
#if DEVELOPMENT_BUILD
        if (previewAudioActive)
        {
            AudioListener.volume = previousListenerVolume;
            previewAudioActive = false;
        }
#endif
        if (match != null)
        {
            match.OnPlayerDied -= HandlePlayerDeath;
            match.Permission.Release(inputLease);
        }
        inputLease = default;
        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = false;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = cameraWasEnabled;
        }
        if (soundscape != null && !completed)
        {
            soundscape.enabled = soundscapeWasEnabled;
        }
        foreach (var renderer in hiddenActors)
        {
            if (renderer != null)
            {
                renderer.forceRenderingOff = false;
            }
        }
        hiddenActors.Clear();
        RestoreCanvases();
    }

    private void HandlePlayerDeath()
    {
        if (ended)
        {
            return;
        }

        ended = true;
        complete = null;
        if (playback != null)
        {
            StopCoroutine(playback);
            playback = null;
        }
        director.Stop();
        bellSource.Stop();
        stage.SetActive(false);
        darkness.enabled = false;
        if (cinematicCamera.TryGetComponent<VillageBellEffects>(out var effects))
        {
            effects.ReleaseReflection();
        }
        RestoreGameplay();
    }

    private void OnDisable()
    {
        if (cinematicCamera != null && cinematicCamera.TryGetComponent<VillageBellEffects>(out var effects))
        {
            effects.ReleaseReflection();
        }
        RestoreGameplay();
    }

#if DEVELOPMENT_BUILD
    private static readonly float[] PreviewTimes = { 3f, 11.5f, 17.22f, 26f, 32f, 35f, 44.8f, 54.4f, 56f, 66f, 75f, 78f, 87.1f };
    private int previewFrame;
    private static int lastPreviewSceneHandle;
    private bool previewAudioActive;
    private float previousListenerVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadPreviewScene()
    {
        if (!HasPreviewArgument() || UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0)
        {
            return;
        }

        int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(VillageScenePath);
        if (index >= 0)
        {
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(index);
        }
    }

    internal static bool HasPreviewArgument()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        return Array.IndexOf(arguments, "-villageBellPreview") >= 0 ||
            Array.IndexOf(arguments, "-villageBellPreviewAudio") >= 0;
    }

    private void CapturePreviewFrame(double time)
    {
        if (HasPreviewArgument() && previewFrame < PreviewTimes.Length && time >= PreviewTimes[previewFrame])
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($"VillageBell-{previewFrame + 1:00}.png"));
            previewFrame++;
        }
    }

    internal static bool TryClaimPreview(int sceneHandle)
    {
        if (lastPreviewSceneHandle == sceneHandle || !HasPreviewArgument())
        {
            return false;
        }
        lastPreviewSceneHandle = sceneHandle;
        return true;
    }
#endif
}
