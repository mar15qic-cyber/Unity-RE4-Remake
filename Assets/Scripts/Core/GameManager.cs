using UnityEngine;

/// <summary>
/// 本局服务宿主。SceneStartupEntry 完成引用验证后绑定本局服务，玩家死亡与任务完成由本类提交给 MatchFlow。
/// 许可通过 GameplayPermissionService 合并，再同步应用到时间、音频和输入；界面接收终局与准星生命周期通知。
/// </summary>
public class GameManager : Singleton<GameManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。本局单例不跨场景存活，由基类执行场景内注册与清理。</summary>
    protected override bool Persistent => false;

    /// <summary>Unity 启动回调，委托基类登记本局身份；场景绑定稍后由启动入口执行。</summary>
    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>中文含义：玩家。场景绑定的玩家控制器，用于核对存活状态与启动依赖。</summary>
    [Header("玩家")]
    public LeonController player;
    /// <summary>中文含义：玩家重生点。玩家重生位置的场景引用，由使用该位置的调用方读取。</summary>
    public Transform playerRespawnPoint;
    /// <summary>中文含义：玩家输入。本局玩家输入桥；ApplyPermission 将合并后的输入许可同步传给它。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("玩家输入")]
    [UnityEngine.InspectorName("玩家输入")]
    public LeonInputBridge playerInput;
    /// <summary>中文含义：匿名登记数。旧接口维护的匿名敌人数；正式角色数量从本局敌群名册读取。</summary>
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("totalEnemiesAlive")]
    [UnityEngine.Serialization.FormerlySerializedAs("匿名登记数")]
    [UnityEngine.InspectorName("匿名登记数")]
    private int anonymousRegisterCount;
    /// <summary>中文含义：当前会话敌人分组。启动入口绑定的敌群，提供本局正式存活敌人数。</summary>
    private EnemyAIManager currentSessionEnemyGroup;
    /// <summary>中文含义：总计敌人集合存活。供旧消费者读取的总存活数，合并匿名登记与正式名册。</summary>
    public int totalEnemiesAlive
    {
        get
        {
            // 无敌群绑定时只返回匿名登记；沿用 Unity 对已销毁组件的判空规则。
            // 中文含义：activeEnemyCount 为“生效敌人数量”。
int activeEnemyCount = 0;
            if (currentSessionEnemyGroup != null)
            {
                activeEnemyCount = currentSessionEnemyGroup.ActiveEnemyCount;
            }

            return anonymousRegisterCount + activeEnemyCount;
        }
    }

    /// <summary>中文含义：流程。本局唯一终局模型；所有死亡与胜利提交均通过此实例。</summary>
    public MatchFlow Flow { get; } = new MatchFlow();
    /// <summary>中文含义：许可。本局唯一许可服务，汇总启动、界面及终局的占用。</summary>
    public GameplayPermissionService Permission { get; } = new GameplayPermissionService();
    /// <summary>中文含义：结束状态。终局模型的当前状态，供界面恢复显示时读取。</summary>
    public GameEndState EndState => Flow.State;
    /// <summary>中文含义：是否已进入对局结束状态。当前是否已提交失败结果。</summary>
    public bool IsGameOver => EndState == GameEndState.Defeat;
    /// <summary>中文含义：是否任务已完成。当前是否已提交胜利结果。</summary>
    public bool IsMissionCompleted => EndState == GameEndState.Victory;
    /// <summary>中文含义：是否终局。任一终局结果是否已提交，用于阻止重复结算。</summary>
    public bool IsTerminal => EndState != GameEndState.Running;
    /// <summary>中文含义：是否已暂停。合并许可是否暂停模拟；不自行拥有另一份暂停状态。</summary>
    public bool IsPaused => !Permission.Current.AllowSimulation;

    /// <summary>中文含义：玩家已死亡时的通知回调。首次死亡提交成功后同步通知订阅者；保留旧委托字段接口。</summary>
    public System.Action OnPlayerDied;
    /// <summary>中文含义：波次已清除时的通知回调。波次清空通知的兼容委托入口；本类不自行判定或发送波次完成。</summary>
    public System.Action OnWaveCleared;
    /// <summary>中文含义：任务已完成时的通知回调。首次胜利提交成功后同步发送任务结果，供通关界面订阅。</summary>
    public event System.Action<MissionResult> OnMissionCompleted;
    /// <summary>中文含义：生命周期变化时的通知回调。死亡时先通知准星等表现结束玩法生命周期，再发送死亡界面通知。</summary>
    public event System.Action<CrosshairLifecycleEvent> OnLifecycleChanged;
    /// <summary>中文含义：当前会话音频。启动入口绑定的音频宿主，接收许可变化后的混音状态切换。</summary>
    private AudioManager currentSessionAudio;
    /// <summary>SceneStartupEntry.Start 调用：绑定本局服务、订阅许可并立即应用当前快照。</summary>
    /// <param name="audio">中文含义：音频。当前可用音频服务，允许为空。</param>
    /// <param name="enemyGroup">中文含义：敌人分组。本局敌群；配置失败分支允许为空以维持启动阻断。</param>
    internal void BindingCurrentSession(AudioManager audio, EnemyAIManager enemyGroup)
    {
        currentSessionEnemyGroup = enemyGroup;
        currentSessionAudio = audio;
        Permission.HasChange += ApplyPermission;
        ApplyPermission(Permission.Current);
        // 只读取一次单例；使用引用判空保留原空值条件访问对 Unity 对象的语义。
        // 中文含义：cursor 为“光标”。
UICursorManager cursor = UICursorManager.Instance;
        if (!ReferenceEquals(cursor, null))
        {
            cursor.HideCustomCursor();
        }
    }

    /// <summary>玩家死亡链进入此处；先提交失败，再阻断输入和计时，最后依次通知表现与死亡界面。</summary>
    public void OnPlayerDeath()
    {
        if (!Flow.CommitDeath())
        {
            return;
        }

        Permission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, true, false);
        // 委托快照保证本次通知只读取一次订阅列表，与原条件访问一致。
        // 中文含义：lifecycleHandler 为“生命周期处理器”。
System.Action<CrosshairLifecycleEvent> lifecycleHandler = OnLifecycleChanged;
        if (lifecycleHandler != null)
        {
            lifecycleHandler(CrosshairLifecycleEvent.Death);
        }

        // 光标单例在本次通知阶段只查询一次。
        // 中文含义：cursor 为“光标”。
UICursorManager cursor = UICursorManager.Instance;
        if (!ReferenceEquals(cursor, null))
        {
            cursor.ShowCustomCursor();
        }

        // 死亡订阅者在准星和光标处理完成后接管界面。
        // 中文含义：deathHandler 为“死亡处理器”。
System.Action deathHandler = OnPlayerDied;
        if (deathHandler != null)
        {
            deathHandler();
        }
    }

    /// <summary>任务宿主请求通关；核对玩家生命后提交唯一胜利事实，成功才阻断玩法并发送结果。</summary>
    /// <param name="result">中文含义：当前任务结果。任务规则生成的结算结果，按值保存并发给订阅者。</param>
    /// <returns>是否接纳通关；已终局或玩家死亡时返回 false。</returns>
    public bool TryCompleteMission(MissionResult result)
    {
        if (IsTerminal)
        {
            return false;
        }

        if (player != null && player.Health != null && player.Health.IsDead)
        {
            return false;
        }

        if (!Flow.CommitLevelComplete(result, player != null && player.Health != null && player.Health.IsDead))
        {
            return false;
        }

        Permission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, true, false);
        // 复制本次通知的接收者，订阅变化不改变当前调用列表。
        // 中文含义：completionHandler 为“完成处理器”。
System.Action<MissionResult> completionHandler = OnMissionCompleted;
        if (completionHandler != null)
        {
            completionHandler(result);
        }

        return true;
    }

    /// <summary>许可变更事件及初次绑定共用的适配入口，将规则快照应用到引擎与玩家输入。</summary>
    /// <param name="state">中文含义：当前玩法许可快照。服务已提交的完整许可快照；只有当前单例可以应用。</param>
    void ApplyPermission(GameplayPermissionSnapshot state)
    {
        if (!HasInstance || Instance != this)
        {
            return;
        }

        Time.timeScale = state.TimeMultiplier;
        if (currentSessionAudio != null)
        {
            if (state.PauseAudio)
            {
                currentSessionAudio.TransitionToPaused();
            }
            else
            {
                currentSessionAudio.TransitionToNormal();
            }
        }

        if (playerInput != null)
        {
            playerInput.SetGameplayInputEnabled(state.AllowInput);
        }
    }

    /// <summary>Unity 销毁回调：先关闭许可并应用最终快照，再退订事件和释放单例登记。</summary>
    protected override void OnDestroy()
    {
        Permission.Close();
        Permission.HasChange -= ApplyPermission;
        currentSessionAudio = null;
        base.OnDestroy();
    }

    /// <summary>场景切换链调用；关闭旧局占用，并恢复引擎时间倍率供后续场景启动。</summary>
    public static void ResetScenePermission()
    {
        if (HasInstance)
        {
            Instance.Permission.Close();
        }

        Time.timeScale = 1f;
    }

    /// <summary>旧调用方的匿名登记入口；正式角色由敌群的具名名册负责。</summary>
    [System.Obsolete("正式角色请通过敌群的具名登记入口加入名册")]
    public void RegisterEnemy()
    {
        anonymousRegisterCount++;
    }

    /// <summary>旧调用方的匿名注销入口；数量至少为零，不改动正式敌群名册。</summary>
    [System.Obsolete("正式角色请通过敌群的具名注销入口离开名册")]
    public void UnregisterEnemy()
    {
        anonymousRegisterCount = Mathf.Max(0, anonymousRegisterCount - 1);
    }
}
