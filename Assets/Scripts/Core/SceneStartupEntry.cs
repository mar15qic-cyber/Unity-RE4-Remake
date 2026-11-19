using UnityEngine;

/// <summary>
/// 主场景启动的统一入口。Unity 先执行各服务启动，再由此组件验证引用、准备任务并开放输入。
/// 通过序列化引用直接调用本局宿主；启动许可句柄在准备期间阻断输入、计时和模拟，失败时保留阻断。
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(GameManager))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "场景启动入口")]
public sealed class SceneStartupEntry : MonoBehaviour
{
    /// <summary>启动状态；由入口写入，诊断和验证代码只读，枚举顺序保留序列化兼容。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "场景启动入口+启动阶段")]
    public enum StartupPhase
    {
        /// <summary>中文含义：准备。初始准备阶段，许可占用尚未释放。</summary>
        [UnityEngine.InspectorName("准备")]
        Prepare,
        /// <summary>中文含义：运行时。引用和任务准备完成，已开始开放本局玩法。</summary>
        [UnityEngine.InspectorName("运行")]
        Runtime,
        /// <summary>中文含义：配置错误。验证或任务准备失败，维持启动阻断并记录错误。</summary>
        [UnityEngine.InspectorName("配置错误")]
        ConfigurationError,
        /// <summary>中文含义：是否具有关闭。组件已停用，停止任务推进并关闭输入放行。</summary>
        [UnityEngine.InspectorName("已关闭")]
        HasClose
    }

    /// <summary>中文含义：阶段。当前启动阶段；Start、ReportFailure 和 OnDisable 更新此状态。</summary>
    public StartupPhase Phase { get; private set; }

    /// <summary>中文含义：对局。同一对象上的对局宿主，提供终局模型和启动占用服务。</summary>
    [Header("本局服务")]
    [UnityEngine.Serialization.FormerlySerializedAs("对局")]
    [UnityEngine.InspectorName("对局")]
    public GameManager match;
    /// <summary>中文含义：任务。任务宿主；验证后按 Prepare、OpenReleaseMission 顺序启动。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("任务")]
    [UnityEngine.InspectorName("任务")]
    public ChapterOneMissionManager mission;
    /// <summary>中文含义：敌群。本局敌群；验证玩家绑定，再登记本局运行服务。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("敌群")]
    [UnityEngine.InspectorName("敌群")]
    public EnemyAIManager enemyGroup;
    /// <summary>中文含义：界面。界面宿主；任务进度先绑定，启动占用释放后再连接整体界面。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("界面")]
    [UnityEngine.InspectorName("界面")]
    public GameUIManager interfaceValue;
    /// <summary>中文含义：背包。本局背包宿主，必须绑定当前玩家。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("背包")]
    [UnityEngine.InspectorName("背包")]
    public InventoryManager inventory;
    /// <summary>中文含义：仓库。本局仓库宿主，必须绑定当前玩家及同一背包。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("仓库")]
    [UnityEngine.InspectorName("仓库")]
    public StorageBoxManager storage;
    /// <summary>中文含义：教学。教学宿主，验证输入桥、武器与提示引用后连接教学。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("教学")]
    [UnityEngine.InspectorName("教学")]
    public TutorialManager tutorial;
    /// <summary>中文含义：背包界面。背包场景加载入口，启动时验证主相机与游戏画布引用。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("背包界面")]
    [UnityEngine.InspectorName("背包界面")]
    public InventorySceneLoader inventoryInterface;
    /// <summary>中文含义：通路。三号屋通路几何，任务生成与分区导航必须使用同一实例。</summary>
    [Header("本局路线")]
    [UnityEngine.Serialization.FormerlySerializedAs("通路")]
    [UnityEngine.InspectorName("通路")]
    public HouseThreeTraversalGeometry traversal;
    /// <summary>中文含义：分区导航。正式分区导航实例；播放时同时核对其运行期登记。</summary>
    [Header("正式分区导航")]
    [UnityEngine.Serialization.FormerlySerializedAs("分区导航")]
    [UnityEngine.InspectorName("分区导航")]
    public HouseThreeRegionNavigation regionNavigation;
    /// <summary>中文含义：玩家楼梯。玩家楼梯辅助，任务准备成功后调用 PrepareTraversal。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("玩家楼梯")]
    [UnityEngine.InspectorName("玩家楼梯")]
    public LeonPlayerStairRampRuntime playerStair;
    /// <summary>中文含义：启动占用。入口持有的启动阻断句柄；成功后只释放自己的占用，停用运行入口时重新申请。</summary>
    private GameArchitecture.GameplayPermissionHandle startupOccupancy;
    private UnityEngine.UI.Image startupCover;
    /// <summary>Unity 初始回调；仅在对局宿主绑定正确时申请启动阻断。</summary>
    void Awake()
    {
        if (match != null && match.gameObject == gameObject)
        {
            startupOccupancy = RequestClosePermission();
            if (Application.isPlaying && gameObject.scene.path == VillageBellSequence.VillageScenePath)
            {
                var cover = new GameObject("村庄首帧遮罩", typeof(Canvas));
                cover.transform.SetParent(transform, false);
                var canvas = cover.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                var surface = new GameObject("全屏遮罩", typeof(UnityEngine.UI.Image));
                surface.transform.SetParent(cover.transform, false);
                startupCover = surface.GetComponent<UnityEngine.UI.Image>();
                startupCover.rectTransform.anchorMin = Vector2.zero;
                startupCover.rectTransform.anchorMax = Vector2.one;
                startupCover.rectTransform.offsetMin = Vector2.zero;
                startupCover.rectTransform.offsetMax = Vector2.zero;
                startupCover.color = Color.black;
                startupCover.raycastTarget = false;
            }
        }
    }

    /// <summary>Awake 或 OnDisable 调用，向本局许可服务申请输入、计时和模拟阻断并返回句柄。</summary>
    GameArchitecture.GameplayPermissionHandle RequestClosePermission() => match.Permission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, true, false, 0f);
    /// <summary>Start 和编辑器验证调用；按依赖顺序读取绑定，返回第一个错误字段，不修改服务状态。</summary>
    /// <returns>错误字段路径；全部有效时返回 null。</returns>
    public string ConfigurationError()
    {
        if (!CurrentSceneAvailable(match) || match.gameObject != gameObject)
        {
            return nameof(match);
        }

        if (!CurrentSceneAvailable(match.player))
        {
            return "对局.player";
        }

        if (!CurrentSceneAvailable(match.playerInput) || match.playerInput.gameObject != match.player.gameObject || !match.playerInput.waitingSceneOpenRelease)
        {
            return "对局.玩家输入";
        }

        if (!CurrentSceneAvailable(mission))
        {
            return nameof(mission);
        }

        if (mission.waveConfiguration == null)
        {
            return "任务.波次配置";
        }

        // field 接收波次资产验证返回的首个错误字段，拼入可定位的配置路径。
        if (!mission.waveConfiguration.Validate(out string /* 中文含义：字段。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ field))
        {
            return "任务.波次配置." + field;
        }

        if (mission.ganadoPrefabs == null || mission.ganadoPrefabs.Length == 0)
        {
            return "任务.ganadoPrefabs";
        }

        // template 是当前普通敌人预制件，仅验证其组件与导航参数。
        // 中文含义：template 为“模板”。每轮从任务的村民敌人预制件集合取得一个元素，仅代表当前这一项。
foreach (var template in mission.ganadoPrefabs)
        {
            // 当前模板的首个错误，不为空时立即终止启动验证。
            // 中文含义：error 为“错误”。
string error = ValidateEnemyTemplate(template, false);
            if (error != null)
            {
                return "任务.ganadoPrefabs." + error;
            }
        }

        // 电锯模板的专用验证结果，额外检查站立动画速度配置。
        // 中文含义：chainsawError 为“电锯错误”。
string chainsawError = ValidateEnemyTemplate(mission.sawmanTemplate, true);
        if (chainsawError != null)
        {
            return "任务.sawmanTemplate." + chainsawError;
        }

        if (mission.spawnPoints == null || mission.spawnPoints.Length == 0)
        {
            return "任务.spawnPoints";
        }

        // point 是当前生成点，必须存在且属于入口所在场景。
        // 中文含义：point 为“点”。每轮从任务的生成点集合取得一个元素，仅代表当前这一项。
foreach (var point in mission.spawnPoints)
        {
            if (point == null || point.gameObject.scene != gameObject.scene)
            {
                return "任务.spawnPoints";
            }
        }

        if (!CurrentSceneAvailable(enemyGroup) || enemyGroup.scenePlayer != match.player)
        {
            return "敌群.场景玩家";
        }

        // 敌群返回的调参错误路径，直接附加到启动诊断中。
        // 中文含义：tuning 为“调参”。
string tuning = enemyGroup.ValidateSceneConfiguration();
        if (tuning != null)
        {
            return "敌群." + tuning;
        }

        if (!CurrentSceneAvailable(traversal) || !traversal.IsReady || !traversal.HasEntranceSupport)
        {
            return nameof(traversal);
        }

        if (!CurrentSceneAvailable(regionNavigation) || regionNavigation.ConfigurationError() != null || regionNavigation.traversal != traversal)
        {
            return nameof(regionNavigation);
        }

        if (Application.isPlaying && HouseThreeRegionNavigation.Current != regionNavigation)
        {
            return "分区导航未启动";
        }

        if (!CurrentSceneAvailable(playerStair))
        {
            return nameof(playerStair);
        }

        // 玩家楼梯辅助返回的几何或引用配置错误。
        // 中文含义：slopeError 为“坡度错误”。
string slopeError = playerStair.ConfigurationError();
        if (slopeError != null)
        {
            return "玩家楼梯." + slopeError;
        }

        var window = match.player.GetComponent<LeonWindowTraversal>();
        if (window == null || window.platformNavigation != regionNavigation)
        {
            return "玩家窗口.楼层和路线";
        }

        if (!CurrentSceneAvailable(inventory) || inventory.currentSessionPlayer != match.player)
        {
            return nameof(inventory);
        }

        if (!CurrentSceneAvailable(storage) || storage.currentSessionPlayer != match.player || storage.currentSessionInventory != inventory)
        {
            return nameof(storage);
        }

        if (!CurrentSceneAvailable(tutorial) || tutorial.promptUI == null || tutorial.inputBridge != match.playerInput || tutorial.weaponHandler == null || tutorial.weaponHandler.gameObject != match.player.gameObject)
        {
            return nameof(tutorial);
        }

        if (!BelongsToCurrentScene(inventoryInterface) || inventoryInterface.mainCamera == null || inventoryInterface.gameCanvas == null)
        {
            return nameof(inventoryInterface);
        }

        if (!BelongsToCurrentScene(interfaceValue))
        {
            return nameof(interfaceValue);
        }

        if (interfaceValue.playerInputBridge != match.playerInput)
        {
            return "界面.playerInputBridge";
        }

        if (interfaceValue.pauseMenu == null)
        {
            return "界面.pauseMenu";
        }

        if (interfaceValue.optionsMenu == null)
        {
            return "界面.optionsMenu";
        }

        if (interfaceValue.gameOverScreen == null)
        {
            return "界面.gameOverScreen";
        }

        if (interfaceValue.missionClearedScreen == null)
        {
            return "界面.missionClearedScreen";
        }

        if (interfaceValue.missionProgressHUD == null)
        {
            return "界面.missionProgressHUD";
        }

        if (interfaceValue.waveClearedUI == null)
        {
            return "界面.waveClearedUI";
        }

        return null;
    }

    /// <summary>ConfigurationError 对每个敌人模板调用；读取角色与寻路组件，校验正式运行所需参数。</summary>
    /// <param name="template">中文含义：模板。待验证的敌人预制件，不实例化或修改。</param>
    /// <param name="chainsaw">中文含义：是否电锯。是否额外要求电锯角色启用站立动画速度适配。</param>
    /// <returns>首个不合格字段；验证通过时返回 null。</returns>
    static string ValidateEnemyTemplate(GameObject template, bool chainsaw)
    {
        if (template == null)
        {
            return "空模板";
        }

        // character 接收模板上的角色外壳，后续只读取移动表现参数。
        if (!template.TryGetComponent<VillagerController>(out var /* 中文含义：角色。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ character))
        {
            return template.name + ".村民控制器";
        }

        if (chainsaw && !character.useStandingAnimationSpeedForPath)
        {
            return template.name + ".useStandingAnimationSpeedForPath";
        }

        if (!(character.movementDirectionBlendTime >= .1f))
        {
            return template.name + ".movementDirectionBlendTime";
        }

        // pathfinding 接收模板上的 A 星路径组件，用于检查到达与重寻路阈值。
        if (!template.TryGetComponent<PathAgent>(out var /* 中文含义：寻路。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pathfinding))
        {
            return template.name + ".寻路";
        }

        if (!(pathfinding.destinationChangeThreshold >= .25f && pathfinding.destinationChangeThreshold <= .5f))
        {
            return template.name + ".destinationChangeThreshold";
        }

        if (!(pathfinding.arrivedDestinationChangeThreshold >= .1f && pathfinding.arrivedDestinationChangeThreshold <= pathfinding.destinationChangeThreshold))
        {
            return template.name + ".arrivedDestinationChangeThreshold";
        }

        if (!(pathfinding.waypointVerticalReachedTolerance >= .35f))
        {
            return template.name + ".waypointVerticalReachedTolerance";
        }

        return null;
    }

    /// <summary>配置验证共用的场景归属判断；允许组件停用，但不接受跨场景引用。</summary>
    /// <param name="component">中文含义：组件。待检查组件，允许为空。</param>
    /// <returns>对象存在且与入口处于同一场景时为 true。</returns>
    bool BelongsToCurrentScene(MonoBehaviour component) => component != null && component.gameObject.scene == gameObject.scene;
    /// <summary>在场景归属判断后检查启用状态，保证启动依赖可执行。</summary>
    /// <param name="component">中文含义：组件。待检查服务组件，允许为空。</param>
    /// <returns>同场景且启用中的组件返回 true。</returns>
    bool CurrentSceneAvailable(MonoBehaviour component) => BelongsToCurrentScene(component) && component.isActiveAndEnabled;
    /// <summary>旧手动补引用链调用；只扫描来源所在的已加载场景，正式启动使用序列化引用。</summary>
    /// <typeparam name = "T">要查找的组件类型。</typeparam>
    /// <param name="source">中文含义：来源组件。限定查找场景的来源组件。</param>
    /// <returns>首个匹配组件，包含停用对象；场景不可用或未找到时返回 null。</returns>
     // 仅供旧的手动补引用接口使用，正式启动直接使用序列化引用。
    internal static T QueryHasHasReference</* 中文含义：由调用方指定的通用类型参数。 */ T>(Component source)
        where T : Component
    {
        // 来源场景的值副本，后续枚举严格限定在该场景。
        // 中文含义：sceneValue 为“场景值”。
var sceneValue = source.gameObject.scene;
        if (!sceneValue.IsValid() || !sceneValue.isLoaded)
        {
            return null;
        }

        // root 是本轮扫描的场景根对象，不遍历其他场景。
        // 中文含义：root 为“根游戏对象”。每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。
foreach (var root in sceneValue.GetRootGameObjects())
        {
            // 当前根节点下首个匹配组件，包含停用子对象。
            // 中文含义：component 为“组件”。
var component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    /// <summary>Unity 延后启动回调：绑定对局、验证配置、准备任务与通路，最后按序开放任务、界面和输入。</summary>
    void Start()
    {
        // 实际挂在入口同一对象上的宿主，用于拒绝错误的跨对象绑定。
        // 中文含义：localMatch 为“局部对局”。
var localMatch = GetComponent<GameManager>();
        if (match != localMatch)
        {
            localMatch.Permission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, true, false, 0f);
            // 此失败分支只在原调用位置读取一次音频单例，不提前改变调用时序。
            // 中文含义：audio 为“音频”。
AudioManager audio = null;
            if (AudioManager.HasInstance)
            {
                audio = AudioManager.Instance;
            }

            localMatch.BindingCurrentSession(audio, null);
            ReportFailure(nameof(match));
            return;
        }

        // 正常启动分支绑定当前可用音频，未注册音频服务时保持空引用。
        // 中文含义：currentAudio 为“当前音频”。
AudioManager currentAudio = null;
        if (AudioManager.HasInstance)
        {
            currentAudio = AudioManager.Instance;
        }

        match.BindingCurrentSession(currentAudio, enemyGroup);
        // 启动前首个错误；静态配置通过后补充输入与生命的运行期就绪检查。
        // 中文含义：error 为“错误”。
string error = ConfigurationError();
        if (error == null && (!match.playerInput.InputReady || match.player.Health == null))
        {
            error = "玩家输入或生命未就绪";
        }

        if (error != null)
        {
            ReportFailure(error);
            return;
        }

        if (!regionNavigation.RefreshSceneryNavigation())
        {
            ReportFailure("布景局部导航");
            return;
        }

        if (!mission.Prepare(match, enemyGroup, traversal))
        {
            ReportFailure("任务生成配置或寻路图");
            return;
        }

        playerStair.PrepareTraversal();
        enemyGroup.BindingCurrentSession(mission.UsesBellEnding);
        VillageLadderSite.Install(this, regionNavigation, enemyGroup);
        interfaceValue.missionProgressHUD.BindingCurrentSession(mission, match);
        tutorial.ConnectTutorial();
        if (!mission.OpenReleaseMission())
        {
            ReportFailure("开场巡逻点");
            return;
        }
        if (mission.UsesBellEnding)
        {
            Font subtitleFont = interfaceValue.missionProgressHUD.interfaceFont;
            if (interfaceValue.pauseMenu != null && interfaceValue.pauseMenu.visualTheme != null
                && interfaceValue.pauseMenu.visualTheme.interfaceFont != null)
            {
                subtitleFont = interfaceValue.pauseMenu.visualTheme.interfaceFont;
            }
            var dialogue = gameObject.AddComponent<VillageLeonDialogue>();
            dialogue.Initialize(match, enemyGroup, subtitleFont);
        }
        Phase = StartupPhase.Runtime;
        match.Permission.Release(startupOccupancy);
        interfaceValue.BindingCurrentSession(match);
        if (startupCover == null)
        {
            match.playerInput.SetSceneOpenRelease(true);
        }
        else
        {
            StartCoroutine(RevealInitialPose());
        }
    }

    private System.Collections.IEnumerator RevealInitialPose()
    {
        // 先在黑幕下完成 Animator、持枪绑定和镜头的首轮更新。
        yield return null;
        yield return null;
        match.playerInput.SetSceneOpenRelease(true);
        float start = Time.unscaledTime;
        const float duration = .22f;
        while (Time.unscaledTime - start < duration)
        {
            float progress = (Time.unscaledTime - start) / duration;
            startupCover.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }
        RemoveStartupCover();
    }

    /// <summary>Start 的失败分支调用；记录阶段与具体字段，保留尚未释放的启动阻断。</summary>
    /// <param name="field">中文含义：字段。失败对象或配置字段路径，用于控制台定位。</param>
    void ReportFailure(string field)
    {
        Phase = StartupPhase.ConfigurationError;
        RemoveStartupCover();
        Debug.LogError("场景启动失败，未就绪的对象或字段为 " + gameObject.scene.name + "/" + name + "." + field, this);
    }

    /// <summary>Unity 停用回调；运行中的入口重新阻断玩法、关闭输入放行并停止任务推进。</summary>
    void OnDisable()
    {
        StopAllCoroutines();
        RemoveStartupCover();
        if (Phase == StartupPhase.Runtime)
        {
            if (match != null)
            {
                startupOccupancy = RequestClosePermission();
                if (match.playerInput != null)
                {
                    match.playerInput.SetSceneOpenRelease(false);
                }
            }

            if (mission != null)
            {
                mission.StopExecute();
            }
        }

        Phase = StartupPhase.HasClose;
    }

    private void RemoveStartupCover()
    {
        if (startupCover != null)
        {
            Destroy(startupCover.transform.parent.gameObject);
            startupCover = null;
        }
    }
}
