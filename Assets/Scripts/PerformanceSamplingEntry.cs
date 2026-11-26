using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;

/// <summary>只有明确传入采样参数时创建。正式游玩不创建采样对象或计数器。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "性能采样入口")]
internal sealed class PerformanceSamplingEntry : MonoBehaviour
{
    /// <summary>分组集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly string[] groups =
    {
        "静止",
        "移动",
        "追击",
        "手枪战斗",
        "霰弹战斗",
        "背包操作",
        "仓库操作",
        "暂停返回"
    };
    /// <summary>计数器集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly string[] counters =
    {
        "CPU Main Thread Frame Time",
        "CPU Render Thread Frame Time",
        "GPU Frame Time",
        "GC Allocated In Frame",
        "Draw Calls Count",
        "Triangles Count",
        "Canvas.BuildBatch",
        "Animators.Update",
        "Physics.Simulate"
    };
    /// <summary>采样集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly double[, ] samples = new double[20000, 32];
    /// <summary>元数据。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<string> metadata = new List<string>();
    /// <summary>记录器集合。</summary>
    private ProfilerRecorder[] recorders;
    /// <summary>有效。</summary>
    private bool[] valid;
    /// <summary>文件夹。</summary>
    private string folder;
    /// <summary>stage 表示“阶段”；state 表示“状态”；sampleCount 表示“采样数量”；actionStep 表示“动作步进”；shotCount 表示“射击数量”；hitCount 表示“命中数量”。</summary>
    private int stage, state, sampleCount, actionStep = -1, shotCount, hitCount;
    /// <summary>since 表示“自从”；initializedAt 表示“已初始化所在”；measureStart 表示“测量起始”。</summary>
    private double since, initializedAt, measureStart;
    /// <summary>加载。</summary>
    private AsyncOperation loading;
    /// <summary>玩家。</summary>
    private LeonController player;
    /// <summary>桥接。</summary>
    private LeonInputBridge bridge;
    /// <summary>敌人集合。</summary>
    private VillagerController[] enemies;
    /// <summary>主摄像机。</summary>
    private Camera mainCamera;
    /// <summary>战斗。</summary>
    private LeonCombat combat;
    /// <summary>武器集合。</summary>
    private LeonWeaponHandler weapons;
    /// <summary>枪械。</summary>
    private WeaponBase gun;
    /// <summary>背包。</summary>
    private InventoryManager inventory;
    /// <summary>加载器。</summary>
    private InventorySceneLoader loader;
    /// <summary>箱体。</summary>
    private StorageBoxController box;
    /// <summary>已存储。</summary>
    private GridItem stored;
    /// <summary>requestedWidth 表示“请求的宽度”；requestedHeight 表示“请求的高度”。</summary>
    private int requestedWidth, requestedHeight;
    private bool sampleFormalVillage;
    private int sampleQualityLevel = 5;
    /// <summary>弹数。</summary>
    private int rounds = 3;
    /// <summary>warmSeconds 表示“预热秒”；sampleSeconds 表示“采样秒”。</summary>
    private double warmSeconds = 15, sampleSeconds = 60;
    /// <summary>场景用例集合。</summary>
    private int[] scenarios =
    {
        0,
        1,
        2,
        3,
        4,
        5,
        6,
        7
    };
    /// <summary>当前轮次。</summary>
    private int CurrentRound
    {
        get
        {
            // 当前元素索引。
int index = stage / rounds;
            // 结果。
int result = stage % rounds + 1;
            // 当前遍历索引。
for (int i = 0; i < index; i++)
            {
                if (scenarios[i] == scenarios[index])
                {
                    result += rounds;
                }
            }

            return result;
        }
    }

    /// <summary>Initialize 的业务处理入口；本方法直接依赖 Environment.GetCommandLineArgs()、GameObject.AddComponent&lt;PerformanceSamplingEntry&gt;()、GameObject.GameObject(string)、Object.DontDestroyOnLoad(Object)、string.StartsWith(string)、string.Substring(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Environment.GetCommandLineArgs()、GameObject.AddComponent&lt;PerformanceSamplingEntry&gt;()、GameObject.GameObject(string)、Object.DontDestroyOnLoad(Object)、string.StartsWith(string)、string.Substring(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // argument（参数）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：PerformanceSamplingEntry.Initialize()。
foreach (string argument in System.Environment.GetCommandLineArgs())
        {
            if (!argument.StartsWith("--性能采样="))
            {
                continue;
            }

            // 所属游戏对象。
var host = new GameObject("独立程序性能采样");
            DontDestroyOnLoad(host);
            host.AddComponent<PerformanceSamplingEntry>().folder = argument.Substring("--性能采样=".Length);
            break;
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Array.ConvertAll&lt;string, int&gt;(string[], Converter&lt;string, int&gt;)、Directory.CreateDirectory(string)、Environment.GetCommandLineArgs()、int.Parse(string)、List&lt;ProfilerRecorderHandle&gt;.List()、List&lt;string&gt;.Add(string)、Mathf.Clamp(int, int, int) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        System.IO.Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        // argument（参数）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Array.ConvertAll<string, int>(string[], Converter<string, int>) 的参数、作为 int.Parse(string) 的参数、PerformanceSamplingEntry.Start()。
foreach (string argument in System.Environment.GetCommandLineArgs())
        {
            if (argument == "--采样正式村庄")
            {
                sampleFormalVillage = true;
            }

            if (argument.StartsWith("--采样尺寸="))
            {
                // 尺寸。
string[] size = argument.Substring("--采样尺寸=".Length).Split('x');
                requestedWidth = int.Parse(size[0]);
                requestedHeight = int.Parse(size[1]);
            }

            if (argument.StartsWith("--采样画质="))
            {
                sampleQualityLevel = Mathf.Clamp(int.Parse(argument.Substring("--采样画质=".Length)), 0, QualitySettings.count - 1);
            }

            if (argument == "--采样探测")
            {
                rounds = 1;
                warmSeconds = 2;
                sampleSeconds = 3;
            }

            if (argument.StartsWith("--采样重复="))
            {
                rounds = Mathf.Clamp(int.Parse(argument.Substring("--采样重复=".Length)), 1, 3);
            }

            if (argument.StartsWith("--采样场景="))
            {
                scenarios = System.Array.ConvertAll(argument.Substring("--采样场景=".Length).Split(','), int.Parse);
            }
        }

        recorders = new ProfilerRecorder[counters.Length];
        valid = new bool[counters.Length];
        // 句柄集合。
var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        // 当前遍历索引。
for (int i = 0; i < counters.Length; i++)
        {
            // handle（句柄）：每轮从句柄集合取得一个元素，仅代表当前这一项。使用位置：作为 ProfilerRecorderHandle.GetDescription(ProfilerRecorderHandle) 的参数。
foreach (var handle in handles)
            {
                // 描述。
var description = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name != counters[i])
                {
                    continue;
                }

                recorders[i] = ProfilerRecorder.StartNew(description.Category, description.Name, 1);
                valid[i] = recorders[i].Valid;
                break;
            }
        }

        metadata.Add("引擎=" + Application.unityVersion + "；显卡=" + SystemInfo.graphicsDeviceName + "；图形接口=" + SystemInfo.graphicsDeviceType + "；处理器=" + SystemInfo.processorType + "；显示=" + Screen.currentResolution + "；开发版本=" + Debug.isDebugBuild);
        // 当前遍历索引。
for (int i = 0; i < counters.Length; i++)
        {
            metadata.Add(counters[i] + "；可用=" + valid[i]);
        }

        Status("启动");
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Application.Quit(int)、Exception.ToString()、File.WriteAllText(string, string)、Path.Combine(string, string)、PerformanceSamplingEntry.Status(string)、PerformanceSamplingEntry.Tick()、PerformanceSamplingEntry.Unbind()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        try
        {
            Tick();
        }
        catch (System.Exception /* 中文含义：错误。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ error)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "异常.txt"), error.ToString());
            Unbind();
            Status("失败");
            enabled = false;
            Application.Quit(1);
        }
    }

    /// <summary>Tick 的业务处理入口；本方法直接依赖 Application.Quit()、Array.GetLength(int)、CharacterHealthModule.SetAliveCharacterValidateHealth(float)、Component.GetComponent&lt;HealthComponent&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonInputBridge&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;() 等共 39 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Update()；下游（含内部回调体） Application.Quit()、Array.GetLength(int)、CharacterHealthModule.SetAliveCharacterValidateHealth(float)、Component.GetComponent&lt;HealthComponent&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonInputBridge&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;() 等共 39 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Tick()
    {
        // 当前时间。
double now = Time.realtimeSinceStartupAsDouble;
        // 分组索引。
int groupIndex = stage / rounds;
        // 分组。
int group;
        if (groupIndex < scenarios.Length)
        {
            group = scenarios[groupIndex];
        }
        else
        {
            group = -1;
        }

        if (state == 0)
        {
            if (group < 0)
            {
                Status("完成");
                Application.Quit();
                enabled = false;
                return;
            }

            Time.timeScale = 1;
            Random.InitState(20261001);
            string sceneName = "GameScene";
            if (sampleFormalVillage)
            {
                sceneName = SceneManagerEx.MAIN_SCENE;
            }

            loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            since = now;
            initializedAt = 0;
            state = 1;
            Status("加载");
            return;
        }

        if (state == 1)
        {
            if (now - since > 120)
            {
                throw new System.Exception("场景就绪超时");
            }

            if (!loading.isDone)
            {
                return;
            }

            if (initializedAt == 0)
            {
                initializedAt = now;
                return;
            }

            if (now - initializedAt < 2)
            {
                return;
            }

            player = LeonController.Instance;
            if (player == null)
            {
                return;
            }

            enemies = Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None);
            int expectedEnemies = 8;
            if (sampleFormalVillage)
            {
                SceneStartupEntry startup = Object.FindFirstObjectByType<SceneStartupEntry>();
                if (startup == null || startup.Phase == SceneStartupEntry.StartupPhase.ConfigurationError)
                {
                    throw new System.InvalidOperationException("正式村庄启动失败。");
                }

                if (startup.Phase != SceneStartupEntry.StartupPhase.Runtime)
                {
                    return;
                }

                expectedEnemies = startup.mission.waveConfiguration.VillagePatrolPositions.Length;
                enemies = System.Array.FindAll(enemies, enemy => enemy.isActiveAndEnabled && !enemy.assassinationShowcaseIdle);
            }

            if (enemies.Length != expectedEnemies)
            {
                throw new System.InvalidOperationException("需要 " + expectedEnemies + " 名巡逻敌人，实际=" + enemies.Length);
            }

            bridge = player.GetComponent<LeonInputBridge>();
            bridge.enabled = false;
            combat = player.GetComponent<LeonCombat>();
            weapons = player.GetComponent<LeonWeaponHandler>();
            inventory = InventoryManager.Instance;
            loader = InventorySceneLoader.Instance;
            mainCamera = Camera.main;
            box = Object.FindFirstObjectByType<StorageBoxController>();
            stored = null;
            shotCount = 0;
            hitCount = 0;
            actionStep = -1;
            QualitySettings.SetQualityLevel(sampleQualityLevel, true);
            if (sampleFormalVillage)
            {
                QualitySettings.vSyncCount = 0;
            }
            else
            {
                QualitySettings.vSyncCount = 1;
            }

            Application.targetFrameRate = -1;
            if (requestedWidth > 0)
            {
                FullScreenMode screenMode = FullScreenMode.Windowed;
                if (sampleFormalVillage)
                {
                    screenMode = FullScreenMode.FullScreenWindow;
                }

                Screen.SetResolution(requestedWidth, requestedHeight, screenMode);
            }

            // 身体。
var body = player.GetComponent<Rigidbody>();
            body.position = new Vector3(11, .45f, -6);
            body.linearVelocity = Vector3.zero;
            // 生命。
var health = player.GetComponent<HealthComponent>();
            health.Initialize(1000000);
            typeof(HealthComponent).GetField("currentHealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(health, 500000f);
            health.Heal(0);
            if (group >= 2 && group <= 4)
            {
                // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：PerformanceSamplingEntry.Tick()。
foreach (var enemy in enemies)
                {
                    enemy.maxHealth = 1000000;
                    enemy.healthModule.SetAliveCharacterValidateHealth(1000000);
                }

                EnemyAIManager.Instance.UnlockCombatAtVillageEntrance(player);
                // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：PerformanceSamplingEntry.Tick()。
foreach (var enemy in enemies)
                {
                    enemy.BeginChase();
                }
            }

            if (group == 3 || group == 4)
            {
                if (group == 4)
                {
                    inventory.AddItem(Resources.Load<ItemData>("Items/Shotgun"), 1);
                    weapons.ForceSyncCurrentWeapon(WeaponType.Shotgun, true);
                }

                if (group == 4)
                {
                    gun = weapons.GetWeapon(WeaponType.Shotgun);
                }
                else
                {
                    gun = weapons.GetCurrentWeapon();
                }

                gun.OnFired += OnFired;
                CombatEventDispatcher.OnHitConfirmed += OnHit;
                combat.HandleAim(true);
            }

            if (group == 5)
            {
                loader.Show();
            }

            if (group == 6)
            {
                if (box == null)
                {
                    throw new System.Exception("寄存箱缺失");
                }

                box.Interact();
            }

            if (group == 7)
            {
                GameUIManager.Instance.ShowPause();
            }

            since = now;
            state = 2;
            Status("等待就绪");
            return;
        }

        if (state == 2)
        {
            // 是否就绪。
bool ready = group < 5 || group == 5 && loader.IsVisible || group == 6 && StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible || group == 7 && GameUIManager.Instance.IsPaused;
            if (!ready)
            {
                if (now - since > 30)
                {
                    throw new System.Exception("界面就绪超时");
                }

                return;
            }

            since = now;
            state = 3;
            Status("预热");
            metadata.Add(groups[group] + "；轮次=" + CurrentRound + "；宽=" + Screen.width + "；高=" + Screen.height + "；画质=" + QualitySettings.GetQualityLevel() + "；同步=" + QualitySettings.vSyncCount + "；限帧=" + Application.targetFrameRate + "；敌人=" + enemies.Length);
            if (sampleFormalVillage)
            {
                metadata.Add("场景=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    + "；显卡=" + SystemInfo.graphicsDeviceName + "；图形 API=" + SystemInfo.graphicsDeviceType);
            }

            return;
        }

        if (state == 3)
        {
            Drive(group, now - since);
            if (now - since < warmSeconds)
            {
                return;
            }

            measureStart = now;
            sampleCount = 0;
            actionStep = -1;
            state = 4;
            Status("采样");
            return;
        }

        // 已用。
double elapsed = now - measureStart;
        if (elapsed >= sampleSeconds)
        {
            Flush();
            Unbind();
            stage++;
            state = 0;
            return;
        }

        Drive(group, elapsed);
        if (sampleCount >= samples.GetLength(0))
        {
            throw new System.Exception("采样缓冲耗尽");
        }

        samples[sampleCount, 0] = elapsed;
        samples[sampleCount, 1] = Time.frameCount;
        samples[sampleCount, 2] = Time.unscaledDeltaTime * 1000;
        samples[sampleCount, 3] = System.GC.CollectionCount(0);
        samples[sampleCount, 4] = System.GC.CollectionCount(1);
        // 当前遍历索引。
for (int i = 0; i < counters.Length; i++)
        {
            // 赋值前保存的目标采样集合。
double[, ] assignmentReceiver3 = samples;
            // 赋值前保存的索引采样数量。
int assignmentIndex4 = sampleCount;
            // 赋值前保存的索引整数计算。
int assignmentIndex5 = 5 + i;
            // 分支选出的记录器集合的上一次值。
long selectedValue6;
            if (valid[i] && recorders[i].Count > 0)
            {
                selectedValue6 = recorders[i].LastValue;
            }
            else
            {
                selectedValue6 = -1;
            }

            assignmentReceiver3[assignmentIndex4, assignmentIndex5] = selectedValue6;
        }

        samples[sampleCount, 14] = Time.timeScale;
        samples[sampleCount, 15] = shotCount;
        samples[sampleCount, 16] = hitCount;
        // 赋值前保存的目标采样集合。
double[, ] assignmentReceiver7 = samples;
        // 赋值前保存的索引采样数量。
int assignmentIndex8 = sampleCount;
        // 赋值前保存的索引17。
int assignmentIndex9 = 17;
        // 分支选出的1。
int selectedValue10;
        if (Application.isFocused)
        {
            selectedValue10 = 1;
        }
        else
        {
            selectedValue10 = 0;
        }

        assignmentReceiver7[assignmentIndex8, assignmentIndex9] = selectedValue10;
        samples[sampleCount, 18] = player.transform.position.x;
        samples[sampleCount, 19] = player.transform.position.y;
        samples[sampleCount, 20] = player.transform.position.z;
        samples[sampleCount, 21] = CombatPerformanceCounters.snapshotCount;
        samples[sampleCount, 22] = CombatPerformanceCounters.pathfindingCommit;
        samples[sampleCount, 23] = CombatPerformanceCounters.physicsQueryCount;
        samples[sampleCount, 24] = CombatPerformanceCounters.candidateNodeCount;
        samples[sampleCount, 25] = CombatPerformanceCounters.indexRebuildCount;
        samples[sampleCount, 26] = CombatPerformanceCounters.categoryHit;
        samples[sampleCount, 27] = CombatPerformanceCounters.categoryMissed;
        samples[sampleCount, 28] = CombatPerformanceCounters.pathExpandCapacity;
        samples[sampleCount, 29] = CombatPerformanceCounters.trimCount;
        samples[sampleCount, 30] = CombatPerformanceCounters.maximumQueueUpSeconds;
        samples[sampleCount, 31] = System.GC.GetAllocatedBytesForCurrentThread();
        sampleCount++;
    }

    /// <summary>Drive 的业务处理入口；本方法直接依赖 GameUIManager.HidePause()、GameUIManager.ShowPause()、InventoryManager.GetItems()、InventoryManager.MoveItem(GridItem, int, int)、InventoryManager.RotateItem(GridItem)、InventoryManager.Sort()、LeonCombat.HandleAim(bool) 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()；下游（含内部回调体） GameUIManager.HidePause()、GameUIManager.ShowPause()、InventoryManager.GetItems()、InventoryManager.MoveItem(GridItem, int, int)、InventoryManager.RotateItem(GridItem)、InventoryManager.Sort()、LeonCombat.HandleAim(bool) 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="group">分组。</param>
    /// <param name="elapsed">已用。</param>
    private void Drive(int group, double elapsed)
    {
        // 步进。
int step = (int)(elapsed / 2);
        if (group == 1 || group == 2)
        {
            // 角度。
float angle = (float)(elapsed * System.Math.PI / 6);
            player.SetMoveInput(new Vector3(Mathf.Sin(angle) * .55f, 0, Mathf.Cos(angle) * .55f));
            player.SetLookInput(new Vector2(.1f * Time.unscaledDeltaTime * 144f, 0));
        }

        if (group == 3 || group == 4)
        {
            // 目标村民敌人控制器。
VillagerController target = null;
            // 最佳。
float best = float.MaxValue;
            // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：PerformanceSamplingEntry.Drive(int, double)。
foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                // 距离。
float distance = (enemy.transform.position - player.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = enemy;
                }
            }

            if (target != null)
            {
                // 方向。
Vector3 direction = target.transform.position - player.transform.position;
                direction.y = 0;
                // 朝向。
Vector3 facing = mainCamera.transform.forward;
                facing.y = 0;
                player.SetLookInput(new Vector2(Mathf.Clamp(Vector3.SignedAngle(facing, direction, Vector3.up) * .2f, -6, 6) * Time.unscaledDeltaTime * 144f, 0));
            }

            if (step != actionStep)
            {
                actionStep = step;
                combat.HandleAim(true);
                if (gun.currentAmmo <= 0)
                {
                    gun.Initialize(player);
                }

                combat.HandleShoot();
                combat.HandleShootRelease();
            }
        }

        if (group == 5 && step != actionStep)
        {
            actionStep = step;
            // 物品列表。
var items = inventory.GetItems();
            if (step % 3 == 0)
            {
                inventory.Sort();
            }
            else if (items.Count > 0 && step % 3 == 1)
            {
                inventory.RotateItem(items[0]);
            }
            else if (items.Count > 1)
            {
                inventory.MoveItem(items[0], items[1].gridX, items[1].gridY);
            }
        }

        if (group == 6 && step != actionStep)
        {
            actionStep = step;
            if (stored != null)
            {
                StorageBoxManager.Instance.TransferToInventory(stored);
                stored = null;
            }
            else
            {
                // item（当前背包网格物品）：每轮从取得物品列表的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 StorageBoxManager.TransferToStorage(GridItem) 的参数、PerformanceSamplingEntry.Drive(int, double)。
foreach (var item in inventory.GetItems())
                {
                    if (item.item.itemType == ItemType.Ammo)
                    {
                        if (StorageBoxManager.Instance.TransferToStorage(item))
                        {
                            // candidate（候选）：每轮从取得仓库物品列表的返回结果取得一个元素，仅代表当前这一项。使用位置：PerformanceSamplingEntry.Drive(int, double)。
foreach (var candidate in StorageBoxManager.Instance.GetStorageItems())
                            {
                                if (candidate.item == item.item)
                                {
                                    stored = candidate;
                                    break;
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        if (group == 7)
        {
            // 暂停步进。
int pauseStep = (int)(elapsed / 8);
            if (pauseStep != actionStep)
            {
                actionStep = pauseStep;
                if (pauseStep % 2 == 0)
                {
                    GameUIManager.Instance.ShowPause();
                }
                else
                {
                    GameUIManager.Instance.HidePause();
                }
            }
        }
    }

    /// <summary>OnFired 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnFired()
    {
        shotCount++;
    }

    /// <summary>OnHit 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前命中结果。</param>
    private void OnHit(HitResult result)
    {
        hitCount++;
    }

    /// <summary>Unbind 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.OnDestroy()、PerformanceSamplingEntry.Tick()、PerformanceSamplingEntry.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Unbind()
    {
        if (gun != null)
        {
            gun.OnFired -= OnFired;
        }

        gun = null;
        CombatEventDispatcher.OnHitConfirmed -= OnHit;
        if (bridge != null)
        {
            bridge.enabled = true;
        }
    }

    /// <summary>Status 的业务处理入口；本方法直接依赖 DateTime.ToString(string)、File.WriteAllText(string, string)、Path.Combine(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Start()、PerformanceSamplingEntry.Tick()、PerformanceSamplingEntry.Update()；下游（含内部回调体） DateTime.ToString(string)、File.WriteAllText(string, string)、Path.Combine(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="phase">阶段。</param>
    private void Status(string phase)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "状态.txt"), phase + "；阶段=" + stage + "；时间=" + System.DateTime.Now.ToString("O"));
    }

    /// <summary>Flush 的业务处理入口；本方法直接依赖 Array.GetLength(int)、double.ToString(IFormatProvider)、File.WriteAllLines(string, IEnumerable&lt;string&gt;)、File.WriteAllText(string, string)、Path.Combine(string, string)、string.Join(string, params string[])、StringBuilder.Append(char) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()；下游（含内部回调体） Array.GetLength(int)、double.ToString(IFormatProvider)、File.WriteAllLines(string, IEnumerable&lt;string&gt;)、File.WriteAllText(string, string)、Path.Combine(string, string)、string.Join(string, params string[])、StringBuilder.Append(char) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Flush()
    {
        // 文字。
var text = new System.Text.StringBuilder("时刻,帧号,间隔毫秒,回收零代,回收一代," + string.Join(",", counters) + ",时间倍率,开火次数,命中次数,窗口焦点,玩家横坐标,玩家高度,玩家纵坐标,快照累计,寻路提交累计,物理查询累计,候选节点累计,索引重建累计,分类命中累计,分类未命中累计,路径扩容累计,裁剪累计,最大排队秒数,主线程分配累计\n");
        // 当前遍历索引。
for (int i = 0; i < sampleCount; i++)
        {
            // 内层遍历索引。
for (int j = 0; j < samples.GetLength(1); j++)
            {
                if (j > 0)
                {
                    text.Append(',');
                }

                text.Append(samples[i, j].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            text.AppendLine();
        }

        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, groups[scenarios[stage / rounds]] + "-" + CurrentRound + ".csv"), text.ToString());
        System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, "条件.txt"), metadata);
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PerformanceSamplingEntry.Unbind()、ProfilerRecorder.Dispose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDestroy()
    {
        Unbind();
        if (recorders != null)
        {
            // 当前遍历索引。
for (int i = 0; i < recorders.Length; i++)
            {
                if (valid[i])
                {
                    recorders[i].Dispose();
                }
            }
        }
    }
}
