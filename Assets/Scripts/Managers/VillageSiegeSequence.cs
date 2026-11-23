using System.Collections.Generic;
using GameArchitecture;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DefaultExecutionOrder(-90)]
public sealed partial class VillageSiegeSequence : MonoBehaviour
{
    [SerializeField, Tooltip("三号屋实际门洞与楼梯。")]
    private HouseThreeTraversalGeometry house;
    [SerializeField, Tooltip("农场门的村庄侧基准。")]
    private Transform farmGate;
    [SerializeField, Tooltip("农场门实际敌人的生成点。")]
    private Transform farmSpawn;
    [SerializeField, Tooltip("三号屋事件实际敌人的生成点。")]
    private Transform houseSpawn;
    [SerializeField, Tooltip("两段源时间轴共用的驱动器。")]
    private PlayableDirector director;
    [SerializeField, Tooltip("交给正式摄像机入口接管的镜头。")]
    private CinemachineCamera shot;
    [SerializeField, Tooltip("csa007 原始时间轴。")]
    private TimelineAsset houseTimeline;
    [SerializeField, Tooltip("csa008 原始时间轴。")]
    private TimelineAsset farmTimeline;
    [SerializeField, Tooltip("三号屋源演员与道具舞台。")]
    private VillageSiegeNativeStage houseStage;
    [SerializeField, Tooltip("农场门源演员与道具舞台。")]
    private VillageSiegeNativeStage farmStage;
    [SerializeField, Tooltip("三号屋参与事件的二楼室内窗梯。")]
    private WindowTraversalPoint siegeWindow;

    private PlayableDirector houseDirector;
    private PlayableDirector farmDirector;
    private VillageCinematicPlayback housePlayback;
    private VillageCinematicPlayback farmPlayback;
    private VillageCinematicPlayback currentPlayback;
    private VillageSoundscape soundscape;
    private ParticleSystem[] sceneParticles;
    internal PlayableDirector CurrentDirector => director;

    internal bool OwnsWindow(WindowTraversalPoint window)
    {
        return window != null && window == siegeWindow;
    }

    private void Start()
    {
        if (!HasSceneBindings)
        {
            return;
        }
        houseDirector = director;
        var holder = new GameObject("农场过场驱动器");
        holder.transform.SetParent(transform, false);
        farmDirector = holder.AddComponent<PlayableDirector>();
        farmDirector.playOnAwake = false;
        // 先启用演出模型再绑定图，战斗分支只决定切出后由哪个敌人接管。
        houseStage.PrepareThreatActors();
        farmStage.PrepareThreatActors();
        houseStage.Bind(houseDirector, houseTimeline);
        farmStage.Bind(farmDirector, farmTimeline);
        housePlayback = new VillageCinematicPlayback(houseDirector, houseTimeline, houseStage.gameObject);
        farmPlayback = new VillageCinematicPlayback(farmDirector, farmTimeline, farmStage.gameObject);
        soundscape = FindFirstObjectByType<VillageSoundscape>();
        cameraController = FindFirstObjectByType<RE4CameraController>();
        sceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private GameManager activeMatch;
    private VillageEntrance activeEntrance;
    private VillageSiegeNativeStage currentStage;
    private VillagerController featuredEnemy;
    private VillageLadderSite windowLadder;
    private RE4CameraController cameraController;
    private GameplayPermissionHandle permission;
    private CinemachineBrain brain;
    private CinemachineBlendDefinition previousBlend;
    private bool previousIgnoreTimeScale;
    private bool outsideDoor;
    private bool latchedDuringSequence;
    private Quaternion gameplayRotation;
    private LeonAnimationActionCoordinator playerActions;
    private LeonActionMotionDriver playerMotion;
    private MotionControlToken playerToken;
    private int playerTransaction;
    private readonly List<Renderer> hiddenRenderers = new();
    private readonly List<Canvas> hiddenCanvases = new();
    private readonly List<ParticleSystem> presentationParticles = new();

    internal bool HasSceneBindings => house != null && house.IsReady && farmGate != null &&
        farmSpawn != null && houseSpawn != null && houseStage != null && farmStage != null &&
        houseTimeline != null && farmTimeline != null && director != null && shot != null;

    internal bool ControlsActor(VillagerController actor)
    {
        return activeMatch != null && featuredEnemy == actor && currentStage != null;
    }

    internal bool ObserveHouseEntrance(LeonController player)
    {
        Vector3 position = player.transform.position;
        if (!player.IsGrounded || player.currentState == LeonState.Traversing ||
            Mathf.Abs(position.y - house.DoorGate.y) > .65f || !house.IsNearEntrance(position))
        {
            outsideDoor = false;
            return false;
        }
        if (house.IsOnExteriorSide(position))
        {
            outsideDoor = true;
            return false;
        }
        if (!outsideDoor || !house.IsOnInteriorSide(position, .52f))
        {
            return false;
        }
        outsideDoor = false;
        return true;
    }

    internal bool IsApproachingFarm(LeonController player, float radius)
    {
        Vector3 offset = player.transform.position - farmGate.position;
        Vector3 side = farmSpawn.position - farmGate.position;
        if (!player.IsGrounded || Mathf.Abs(offset.y) > 1.5f)
        {
            return false;
        }
        offset.y = 0f;
        side.y = 0f;
        return offset.sqrMagnitude <= radius * radius && Vector3.Dot(offset, side) > 0f;
    }

    internal Transform GetSpawnAnchor(VillageEntrance entrance)
    {
        if (entrance == VillageEntrance.Farm)
        {
            return farmSpawn;
        }
        return houseSpawn;
    }

    internal bool CanStart(GameManager match)
    {
        LeonController player = match.player;
        if (activeMatch != null || match.IsTerminal || !match.Permission.Current.AllowInput ||
            !player.IsGrounded || player.AnimCoordinator == null ||
            player.AnimCoordinator.HasActiveFullBodyAction || player.AnimCoordinator.HasActiveWeaponAction ||
            !player.CanAct())
        {
            return false;
        }
        var pair = player.GetComponent<LeonQTECoordinator>();
        return pair == null || !pair.IsActive;
    }

    internal void Play(GameManager match, VillageEntrance entrance, VillagerController actor, bool nearFarm)
    {
        activeEntrance = entrance;
        latchedDuringSequence = false;
        currentStage = houseStage;
        director = houseDirector;
        currentPlayback = housePlayback;
        if (entrance == VillageEntrance.Farm)
        {
            currentStage = farmStage;
            director = farmDirector;
            currentPlayback = farmPlayback;
        }
        if (!HasSceneBindings || cameraController == null || cameraController.MainCamera == null ||
            cameraController.brain == null || currentStage == null || currentStage.cameraMotion == null ||
            !cameraController.RequestExternalCamera(this, shot))
        {
            LatchDoor();
            GiveLadderPriority(true);
            currentStage = null;
            return;
        }

        activeMatch = match;
        featuredEnemy = actor;
        Camera camera = cameraController.MainCamera;
        gameplayRotation = camera.transform.rotation;
        brain = cameraController.brain;
        previousBlend = brain.DefaultBlend;
        previousIgnoreTimeScale = brain.IgnoreTimeScale;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        brain.IgnoreTimeScale = true;
        permission = match.Permission.Request(new ObjectIdentity(GetInstanceID(), 1), true, true, false, 0f);
        currentStage.UpdateVolume();
        if (soundscape != null)
        {
            soundscape.SustainCombatMusic();
        }
        BeginPlayerLease();
        currentStage.gameObject.SetActive(true);
        foreach (var particle in sceneParticles)
        {
            if (particle == null || !particle.gameObject.activeInHierarchy)
            {
                continue;
            }
            var settings = particle.main;
            if (!settings.useUnscaledTime)
            {
                presentationParticles.Add(particle);
                settings.useUnscaledTime = true;
            }
        }
        // 新生成角色的生命模块在 Start 初始化；任务已登记的演员应立即出镜。
        HideGameplay(match.player.transform);
        foreach (var enemy in FindObjectsByType<VillagerController>(FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene == gameObject.scene)
            {
                HideGameplay(enemy.transform);
            }
        }
        if (entrance == VillageEntrance.House)
        {
            windowLadder = VillageLadderSite.FindWindowLadder(siegeWindow);
            if (windowLadder != null)
            {
                HideGameplay(windowLadder.transform);
            }
            HideGameplay(house.entranceDoor.transform);
        }
        else
        {
            HideGameplay(farmGate);
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.enabled)
            {
                hiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }
        }

        if (entrance == VillageEntrance.House)
        {
            VillageLeonDialogue.BeginHouseCaption(match);
        }
        currentPlayback.Begin();
        ApplyNativeCamera();
    }

    private void Update()
    {
        if (activeMatch == null)
        {
            return;
        }
        currentPlayback.Sample();
        if (activeMatch.IsTerminal)
        {
            EndSequence(false);
            return;
        }
        if (director.state != PlayState.Playing && director.timeUpdateMode != DirectorUpdateMode.Manual)
        {
            EndSequence(director.time >= director.duration - .0005d);
            return;
        }
        if (activeEntrance == VillageEntrance.House)
        {
            VillageLeonDialogue.ShowHouseCaption(activeMatch, director.time);
            if (director.time >= 5.8d)
            {
                LatchDoor();
            }
            if (windowLadder != null)
            {
                if (director.time < 4.5d)
                {
                    windowLadder.TryReserveSiegeClimber(this);
                }
                if (director.time >= 1243d / 60d)
                {
                    windowLadder.OpenSiegeAction(this, director.time >= 22d);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (activeMatch == null || currentStage == null)
        {
            return;
        }
        ApplyNativeCamera();
        if (activeEntrance == VillageEntrance.House && director.time >= HouseShoulderShotStart &&
            currentStage.playerRoot != null && playerMotion != null)
        {
            // 末镜期间将仍然隐藏的玩法角色跟到源动作，避免回切时跨越整间屋子。
            var root = currentStage.playerRoot;
            playerMotion.CommitTraversalPose(playerToken, root.position + Vector3.up * PlayerExitHeightOffset, root.rotation);
        }
        if (featuredEnemy != null && currentStage.threatRoot != null)
        {
            Vector3 position = currentStage.threatRoot.position - Vector3.up * currentStage.threatHeightOffset;
            Quaternion facing = Quaternion.Euler(0f, currentStage.threatRoot.eulerAngles.y, 0f);
            featuredEnemy.CommitVillageEntrancePose(this, position, facing);
        }
        if (director.time >= director.duration - .0005d)
        {
            EndSequence(true);
        }
    }

    private void LatchDoor()
    {
        if (activeEntrance != VillageEntrance.House || house == null || house.entranceDoor == null)
        {
            return;
        }
        if (!latchedDuringSequence)
        {
            latchedDuringSequence = true;
            house.entranceDoor.RequestSiegeLatch();
        }
        house.entranceDoor.TryLatchForSiege();
    }

    private void BeginPlayerLease()
    {
        var player = activeMatch.player;
        if (activeEntrance != VillageEntrance.House ||
            !player.AnimCoordinator.RequestEnvironmentalAction(FullBodyActionType.DoorBrace))
        {
            return;
        }
        playerActions = player.AnimCoordinator;
        playerMotion = player.GetComponent<LeonActionMotionDriver>();
        playerTransaction = playerActions.CurrentFullBodyTransactionId;
        playerToken = playerMotion.AuthorizeMotion(MotionSource.Traversal, playerTransaction);
        playerMotion.StopHorizontalSpeed(playerToken);
    }

    private void HideGameplay(Transform target)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
            {
                hiddenRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }
    }

    private void GiveLadderPriority(bool resume)
    {
        if (activeEntrance == VillageEntrance.House)
        {
            var site = VillageLadderSite.FindWindowLadder(siegeWindow);
            if (site != null)
            {
                if (resume || (activeMatch != null && director.time >= 22d))
                {
                    site.DeploySiegeLadder();
                }
                site.EndSiege(this, resume);
            }
        }
        windowLadder = null;
    }

    internal void Cancel()
    {
        EndSequence(false);
    }

    private void EndSequence(bool completed)
    {
        if (ReferenceEquals(activeMatch, null))
        {
            return;
        }
        LatchDoor();
        Quaternion returnRotation = gameplayRotation;
        if (completed && currentStage != null && activeEntrance == VillageEntrance.House)
        {
            // 取最后一帧的根骨和视线，避免在片段右端点读到轨道解绑后的默认姿态。
            director.time = director.duration - 1d / 60d;
            director.Evaluate();
            returnRotation = currentStage.cameraMotion.transform.rotation;
        }
        if (completed && currentStage != null && currentStage.playerRoot != null && playerMotion != null)
        {
            Vector3 destination = currentStage.playerRoot.position;
            destination.y += PlayerExitHeightOffset;
            playerMotion.CommitCinematicExitPose(playerToken, destination, currentStage.playerRoot.rotation);
        }
        if (playerTransaction != 0)
        {
            if (playerMotion != null)
            {
                playerMotion.ReleaseMotion(playerToken);
            }
            if (playerActions != null)
            {
                playerActions.EndLadderAction(playerTransaction, FullBodyActionType.DoorBrace, false);
            }
            playerTransaction = 0;
        }
        GiveLadderPriority(completed);
        if (activeEntrance == VillageEntrance.House)
        {
            VillageLeonDialogue.EndCinematicCaption(activeMatch);
        }
        currentPlayback.Stop();
        if (currentStage != null)
        {
            currentStage.gameObject.SetActive(false);
        }
        foreach (var renderer in hiddenRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        hiddenRenderers.Clear();
        foreach (var canvas in hiddenCanvases)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }
        hiddenCanvases.Clear();
        foreach (var particle in presentationParticles)
        {
            if (particle != null)
            {
                var settings = particle.main;
                settings.useUnscaledTime = false;
            }
        }
        presentationParticles.Clear();
        if (cameraController != null && brain != null)
        {
            cameraController.ReleaseExternalCamera(this);
            brain.ResetState();
            cameraController.gameplayDriver.SnapAfterVillageEntrance(returnRotation);
            brain.DefaultBlend = previousBlend;
            brain.IgnoreTimeScale = previousIgnoreTimeScale;
        }
        if (activeMatch != null)
        {
            activeMatch.Permission.Release(permission);
        }
        if (completed && featuredEnemy != null && featuredEnemy.TryGetComponent(out ChainsawManAnimationBridge bridge))
        {
            bridge.OnChainsawStartComplete();
            if (activeEntrance == VillageEntrance.House)
            {
                featuredEnemy.OrderSiegeBreach(house);
            }
        }
        featuredEnemy = null;
        activeMatch = null;
        currentStage = null;
        permission = default;
        activeEntrance = VillageEntrance.None;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
