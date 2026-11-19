#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Pathfinding;
using UnityEngine;
using Path = System.IO.Path;

/// <summary>仅在编辑器或开发客户端显式启动，使用正式角色和导航执行退役回归。</summary>
[DefaultExecutionOrder(8000)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp","三号屋退役验收")]
public sealed class HouseThreeRetirementValidation : MonoBehaviour
{
    [Header("验收输出与范围")]
    [UnityEngine.Serialization.FormerlySerializedAs("输出目录")]
    [UnityEngine.InspectorName("输出目录")]
    public string outputDirectory;
    [UnityEngine.Serialization.FormerlySerializedAs("专项")]
    [UnityEngine.InspectorName("专项")]
    public string specialized = "全部";
    [UnityEngine.Serialization.FormerlySerializedAs("重复次数")]
    [UnityEngine.InspectorName("重复次数")]
    public int duplicateCount = 3;
    public bool Completed { get; private set; }
    public int FailureCount { get; private set; }
    private bool automaticExit;
    private LeonController player;
    private ChapterOneMissionManager mission;
    private GameObject[] template;
    private HouseThreeTraversalGeometry stairGeometry;
    private Vector3? fixedChaseTarget;
    private readonly List<VillagerController> character = new();
    private readonly StringBuilder result = new("用例,通过,耗时,说明\n");
    private readonly StringBuilder footprint = new("角色,位置,时间,根高,左误差,右误差,骨盆高,解算器数,腿链数,左承重,右承重,正在迈步,静止秒数\n");
    private readonly StringBuilder trajectory = new("用例,时间,角色,横,高,纵,状态,路径\n");
    private readonly Vector3[] stopMovementPosition = { new(-6.30f,.2f,-13.05f), new(-6.27f,1.6f,-14.93f), new(-6.30f,2.1f,-16.4f), new(-6.30f,3.2f,-18.45f) };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CommandLineStartup()
    {
        var parameter = Environment.GetCommandLineArgs();
        int sequence = Array.IndexOf(parameter, "-楼梯验收");
        if (sequence < 0 || sequence + 1 >= parameter.Length) return;
        var acceptance = new GameObject("三号屋退役验收").AddComponent<HouseThreeRetirementValidation>();
        acceptance.outputDirectory = parameter[sequence + 1];
        acceptance.automaticExit = true;
        int range = Array.IndexOf(parameter, "-验收专项");
        if (range >= 0 && range + 1 < parameter.Length) acceptance.specialized = parameter[range + 1];
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        if (string.IsNullOrEmpty(outputDirectory)) outputDirectory = Path.Combine(Application.persistentDataPath, "楼梯退役验收");
        Directory.CreateDirectory(outputDirectory);
        player = LeonController.Instance;
        mission = FindFirstObjectByType<ChapterOneMissionManager>();
        var region = HouseThreeRegionNavigation.Current;
        if (!player || !mission || !region || region.ConfigurationError() != null)
        {
            Record("正式启动", false, 0, "玩家、任务或分区导航未就绪");
            Complete(); yield break;
        }
        mission.enabled = false;
        mission.StopAllCoroutines();
        template = mission.ganadoPrefabs.Concat(new[] { mission.sawmanTemplate }).ToArray();
        stairGeometry = FindFirstObjectByType<HouseThreeTraversalGeometry>();
        foreach (var enemy in FindObjectsByType<VillagerController>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        player.GetComponent<LeonInputBridge>().enabled = false;
        player.Health.Initialize(50000);
        EnemyAIManager.Instance.UnlockCombatAtVillageEntrance(player);
        Time.timeScale = 2;
        Record("正式启动", true, 0, "使用场景正式缓存和共享双足贴地");
        if (specialized == "屋檐" || specialized == "窗口屋檐")
            for (int round = 0; round < duplicateCount; round++)
            {
                yield return CrossLayerWindowFrontQueueUp(round);
                yield return ElevatedTargetChase(round, false);
                yield return ElevatedTargetChase(round, true);
            }
        if (specialized == "外平台")
            for (int round = 0; round < duplicateCount; round++)
            {
                yield return OuterPlatformMutualBlockingAcceptance(round, false);
                yield return OuterPlatformMutualBlockingAcceptance(round, true);
            }
        if (specialized == "平台多人") yield return OuterPlatformMultipleCharactersChase();
        if (specialized == "拐角")
        {
            var window = FindObjectsByType<EnemyWindowTraversalRoute>(FindObjectsSortMode.None)
                .Where(x => x.platformRegionId == region.outerPlatformIdentifier).ToArray();
            for (int round = 0; round < duplicateCount; round++)
            {
                foreach (var windowValue in window)
                    for (int category = 0; category < template.Length; category++)
                        yield return CornerChase(windowValue, template[category], round);
                yield return VaultChase("南侧木板向东" + round, new(-7.5f,3.4f,-23f), new(-.5f,3.4f,-19.5f), 0);
                yield return VaultChase("南侧木板向西" + round, new(-.5f,3.4f,-19.5f), new(-7.5f,3.4f,-23f), 0);
                yield return OuterPlatformMultipleCharactersChase(new Vector3(-2.6f,3.4f,-22.5f));
            }
        }
        if (specialized == "窄木板")
            for (int round = 0; round < duplicateCount; round++)
            {
                yield return VaultChase("窗外木板边缘" + round, new(-5f,3.2f,-18.5f), new(-8.1f,3.2f,-17.8f), 1);
                yield return VaultChase("窗外靠墙保持同侧" + round, new(-5f,3.2f,-18.5f), new(-7.9f,3.2f,-17.5f), 1);
                yield return VaultChase("沿木板上平台" + round, new(-9.1f,3.3f,-22f), new(-9.1f,3.55f,-14.9f), 0);
                yield return VaultChase("沿木板下平台" + round, new(-9.1f,3.55f,-14.9f), new(-9.1f,3.3f,-22f), 0);
                yield return OuterPlatformMultipleCharactersChase(new Vector3(-8.1f,3.2f,-17.8f));
            }
        if (specialized == "长帧")
            for (int round = 0; round < duplicateCount; round++) yield return LongFrameTraversalAcceptance(round);
        if (specialized == "跳落") yield return ConfigurationJumpLandAcceptance();
        if (specialized == "出口")
            for (int round = 0; round < duplicateCount; round++) yield return CrowdedExitPointAcceptance(round);
        if (specialized == "窗口屋檐") yield return OuterPlatformMultipleCharactersChase();
        if(specialized=="全部"||specialized=="整图"||specialized=="补充"||specialized=="窗口屋檐")yield return ProductionImageRuntimeRegression();
        for (int round = 0; round < duplicateCount && (specialized == "全部" || specialized == "追击" || specialized == "多人"); round++)
        {
            if (specialized != "多人")
            {
                yield return ChaseValue("中段恢复" + round, template[1], new(-6.403f,1.228f,-14.656f), new(-5.635f,.025f,-10.625f));
                yield return ChaseValue("向下追台阶" + round, template[1], new(-4.1f,3.2f,-17.6f), new(-6.058f,1.104f,-14.021f));
                yield return ChaseValue("向上追台阶" + round, template[1], new(-6.2f,.07f,-11.5f), new(-6.05f,1.52f,-15.1f));
                foreach (var prefab in template)
                    yield return ChaseValue("上下楼反向" + round, prefab, new(-9.7f,.04f,-7.4f), new(-3.92f,3.2f,-17f), true);
                yield return LongFrameTraversalAcceptance(round);
            }
            yield return MultipleCharactersChase("同向跟随" + round, 0);
            yield return MultipleCharactersChase("领头受击" + round, 1);
            yield return MultipleCharactersChase("领头死亡" + round, 2);
            yield return MultipleCharactersChase("领头回收" + round, 3);
            yield return HeadOnTraversal("迎面通行" + round);
            yield return CrowdedExitPointAcceptance(round);
        }
        if (specialized == "全部" || specialized == "整图")
        {
            yield return ChaseValue("低台沿阻塞恢复", template[template.Length-1], new(7.5412f,-.0137f,-1.6006f), new(-3.92f,3.2f,-12f), false, 90);
            yield return MaximumPresentChase();
            yield return SixIndexRoomChase();
        }
        if (specialized == "全部" || specialized == "窗口" || specialized == "窗口屋檐") yield return WindowChase();
        if (specialized == "全部" || specialized == "贴地")
        {
            yield return EnemyGroundContact();
            yield return PlayerGroundContact();
        }
        if (specialized == "全部" || specialized == "窗口" || specialized == "补充" || specialized == "窗口屋檐")
        { yield return ConnectedPlatformDirectChase(); yield return ConfigurationJumpLandAcceptance(); }
        if (specialized == "全部" || specialized == "动作" || specialized == "补充") yield return ActionHandoffAcceptance();
        if (specialized == "全部" || specialized == "感知" || specialized == "补充") yield return PerceptionAndAimAcceptance();
        if (specialized == "全部" || specialized == "运动" || specialized == "补充") yield return PlayerWalkAcceptance();
        if (specialized == "全部" || specialized == "重载" || specialized == "补充") yield return SceneReloadAcceptance();
        CleanupCharacter();
        Complete();
    }

    private IEnumerator CrossLayerWindowFrontQueueUp(int round)
    {
        CleanupCharacter();
        var window = EnemyWindowTraversalRoute.ActiveRoutes[0];
        window.TryAcquireWindowSideTread(false, out var inner);
        MovementPlayer(new Vector3(15, 0, 15));
        var downstairs = CreateValue(template[0], inner - Vector3.up * 3.2f, true);
        var windowFront = CreateValue(template[1], inner + Vector3.right * .15f, true);
        yield return new WaitForSeconds(.6f);
        downstairs.transform.position = inner - Vector3.up * 3.2f;
        windowFront.transform.position = inner + Vector3.right * .15f;
        bool planValid = window.TryBuildPlanFromSide(downstairs, false, out var lowerPlan, true) &&
            window.TryBuildPlanFromSide(windowFront, false, out _, true);
        window.TryBuildPlanFromSide(windowFront, false, out var upperPlan, true);
        window.RegisterAndGetWaitingPoint(downstairs, lowerPlan);
        window.RegisterAndGetWaitingPoint(windowFront, upperPlan);
        bool hasPass = window.TryAcquire(windowFront);
        Record("楼下不得占窗前队首" + round, planValid && hasPass, 0, "楼下先登记，同层窗前角色仍能获得通行");
        window.Release(downstairs); window.Release(windowFront);
        CleanupCharacter();
    }

    private IEnumerator ElevatedTargetChase(int round, bool corpse)
    {
        CleanupCharacter();
        string name = (corpse ? "尸体抬高窗外目标" : "西侧屋脊静止目标") + round;
        // 石沿上方紧邻主屋顶，必须从实际石沿高度放置，不能把胶囊挤上主屋顶。
        Vector3 target = corpse ? new Vector3(-.034f, 3.917f, -15.994f) : new Vector3(-7.85f, 4.6f, -12f);
        if (corpse)
        {
            for (int i = 0; i < 7; i++) CreateValue(template[i % 3], new Vector3(target.x + (i % 3 - 1) * .18f, 3.2f, target.z + (i / 3 - 1) * .18f));
            yield return new WaitForSeconds(.6f);
            foreach (var corpseValue in character) corpseValue.TakeDamage(10000);
            yield return new WaitForSeconds(1f);
            Record("尸体已进入死亡" + round, character.All(x => x.IsDead), 0, "保留实际死亡碰撞");
        }
        var elevatedProjection = HouseThreeRegionNavigation.Current.ReadChaseApproachPoint(target, true);
        Record(name + "目标投影", elevatedProjection.node != null, 0, elevatedProjection.position.ToString("F3"));
        MovementPlayer(target); fixedChaseTarget = target;
        var startPoint = new[] { new Vector3(-5,3.2f,-18.5f), new Vector3(-3.9f,3.2f,-14f),
            new Vector3(-6.3f,1.23f,-14.7f), new Vector3(-9.7f,.04f,-7.4f) };
        var chaseActor = startPoint.Select((point, i) => CreateValue(template[i % template.Length], point)).ToArray();
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in chaseActor) { enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase(); }
        float begin = Time.time, next = 0, maximumHeight = 0;
        var arrival = new HashSet<VillagerController>();
        while (Time.time - begin < 65 && arrival.Count < chaseActor.Length)
        {
            maximumHeight = Mathf.Max(maximumHeight, player.transform.position.y);
            var approach = HouseThreeRegionNavigation.Current.ReadChaseApproachPoint(player.transform.position, true);
            foreach (var enemy in chaseActor)
            {
                if (arrival.Contains(enemy)) continue;
                var action = enemy.GetComponent<EnemyWindowTraversal>();
                if (approach.node != null && !action.IsMovementProtected && action.IsOnTrackedPlatform &&
                    Vector3.Distance(enemy.transform.position, approach.position) < 1.65f &&
                    Vector3.Distance(enemy.transform.position, player.transform.position) < 2.8f)
                { arrival.Add(enemy); enemy.gameObject.SetActive(false); }
            }
            if (Time.time >= next) { next = Time.time + .5f; SampleTrajectory(name, Time.time - begin); }
            yield return null;
        }
        Record(name, arrival.Count == chaseActor.Length, Time.time - begin,
            "室内两人、楼梯及地面各一人；到合法接近位置=" + arrival.Count + "/4；玩家最高=" + maximumHeight.ToString("F3"));
        CleanupCharacter();
    }

    private void Complete()
    {
        Completed = true;
        fixedChaseTarget = null;
        Time.timeScale = 1;
        if (player) { player.SetMoveInput(Vector3.zero); player.GetComponent<LeonInputBridge>().enabled = true; }
        File.WriteAllText(Path.Combine(outputDirectory, "完成.txt"), "失败数=" + FailureCount);
        if (automaticExit) Application.Quit(FailureCount == 0 ? 0 : 2);
    }

    private void Record(string testCase, bool pass, float elapsedTime, string description)
    {
        if (!pass) FailureCount++;
        result.AppendLine($"{testCase},{pass},{elapsedTime:F2},{description.Replace(',', '，')}");
        File.WriteAllText(Path.Combine(outputDirectory, "结果.csv"), result.ToString());
    }

    private void MovementPlayer(Vector3 target)
    {
        player.SetMoveInput(Vector3.zero);
        var rigidbody = player.GetComponent<Rigidbody>();
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.position = target;
        player.transform.position = target;
        Physics.SyncTransforms();
    }

    private VillagerController CreateValue(GameObject prefab, Vector3 position, bool idle = false)
    {
        var objectValue = Instantiate(prefab, position, Quaternion.Euler(0, 180, 0));
        var enemy = objectValue.GetComponent<VillagerController>();
        enemy.assassinationShowcaseIdle = idle;
        enemy.attackDamage = 0;
        var pairedAction = objectValue.GetComponent<VillagerQTEParticipant>();
        pairedAction.frontGrabProbability = pairedAction.rearGrabProbability = 0;
        pairedAction.enableChainsawExecution = false;
        objectValue.SetActive(true);
        enemy.IsActive = true;
        character.Add(enemy);
        return enemy;
    }

    private void CleanupCharacter()
    {
        fixedChaseTarget = null;
        foreach (var enemy in character) if (enemy) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
        character.Clear();
    }

    private void LateUpdate()
    {
        if (!fixedChaseTarget.HasValue || !player) return;
        // 静止目标用例保持指定的水平站位，避免攻击推挤把测试变成另一层追击。
        // 竖向接地、动画和共享腿部解算照常运行；其它动作用例不启用此约束。
        var rigidbody = player.GetComponent<Rigidbody>();
        var positionValue = rigidbody.position;
        positionValue.x = fixedChaseTarget.Value.x;
        positionValue.z = fixedChaseTarget.Value.z;
        rigidbody.position = positionValue;
        player.transform.position = positionValue;
        rigidbody.linearVelocity = new Vector3(0, rigidbody.linearVelocity.y, 0);
    }

    private void SampleTrajectory(string nameValue, float time)
    {
        foreach (var enemy in character)
            if (enemy) trajectory.AppendLine($"{nameValue},{time:F2},{enemy.name},{enemy.transform.position.x:F4},{enemy.transform.position.y:F4},{enemy.transform.position.z:F4},{enemy.CurrentState},{enemy.Agent.CurrentPathState}");
        File.WriteAllText(Path.Combine(outputDirectory, "追击轨迹.csv"), trajectory.ToString());
    }

    private IEnumerator ChaseValue(string nameValue, GameObject prefab, Vector3 startPoint, Vector3 target, bool reverse = false, float timeLimit = 36)
    {
        CleanupCharacter(); MovementPlayer(target);
        var enemy = CreateValue(prefab, startPoint);
        yield return new WaitForSeconds(.6f);
        enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase();
        float begin = Time.time, next = 0; bool arrival = false;
        while (Time.time - begin < timeLimit)
        {
            var difference = enemy.transform.position - player.transform.position;
            if (enemy.CurrentState == VillagerState.Attack && Mathf.Abs(difference.y) < .65f && difference.magnitude < 2.5f) { arrival = true; break; }
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory(nameValue, Time.time - begin); }
            yield return null;
        }
        Record(nameValue + prefab.name, arrival, Time.time - begin, enemy.transform.position.ToString("F3"));
        if (!reverse || !arrival) yield break;
        MovementPlayer(new(-5.635f,.025f,-10.625f));
        begin = Time.time; arrival = false;
        while (Time.time - begin < 36)
        {
            if (enemy.CurrentState == VillagerState.Attack && enemy.transform.position.y < .7f) { arrival = true; break; }
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory(nameValue + "反向", Time.time - begin); }
            yield return null;
        }
        Record(nameValue + prefab.name + "反向", arrival, Time.time - begin, enemy.transform.position.ToString("F3"));
    }

    private IEnumerator MultipleCharactersChase(string name, int intervention)
    {
        CleanupCharacter(); MovementPlayer(new(-3.92f,3.2f,-17f));
        for (int i = 0; i < 3; i++) CreateValue(template[i], new(-6.3f,.1f,-12.3f + i * 1.25f));
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in character) { enemy.ReceiveSharedCombatAlert(player.transform.position); enemy.BeginChase(); }
        var arrival = new HashSet<VillagerController>();
        float begin = Time.time, next = 0, restoreTime = -1; bool hasIntervention = intervention == 0;
        while (Time.time - begin < 48)
        {
            var firstPosition = character[0];
            if (!hasIntervention && firstPosition.transform.position.y > .8f && firstPosition.transform.position.y < 2.7f)
            {
                hasIntervention = true;
                if (intervention == 1) firstPosition.TakeHeavyFrontDamage(20, firstPosition.transform.position + Vector3.up);
                if (intervention == 2) firstPosition.TakeDamage(50000);
                if (intervention == 3) { firstPosition.gameObject.SetActive(false); restoreTime = Time.time + 1; }
            }
            if (restoreTime > 0 && Time.time >= restoreTime)
            {
                firstPosition.gameObject.SetActive(true); firstPosition.IsActive = true;
                firstPosition.ReceiveSharedCombatAlert(player.transform.position); firstPosition.BeginChase(); restoreTime = -1;
            }
            foreach (var enemy in character)
                if (!enemy.IsDead && enemy.transform.position.y > 2.9f && enemy.transform.position.z < -16.5f) arrival.Add(enemy);
            if (arrival.Count >= (intervention == 2 ? 2 : 3)) break;
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory(name, Time.time - begin); }
            yield return null;
        }
        Record(name, hasIntervention && arrival.Count >= (intervention == 2 ? 2 : 3), Time.time - begin, "到达人数=" + arrival.Count);
    }

    private IEnumerator LongFrameTraversalAcceptance(int round)
    {
        float originalSample = Time.captureDeltaTime, originalMaximum = Time.maximumDeltaTime, originalPlaybackSpeed = Time.timeScale;
        Time.timeScale = 1;
        Time.maximumDeltaTime = .5f;
        Time.captureDeltaTime = .4f;
        yield return ChaseValue("长帧上下楼" + round, template[1], new(-6.3f,.18f,-12.7f), new(-3.92f,3.2f,-17f), true, 90);
        Time.captureDeltaTime = originalSample;
        Time.maximumDeltaTime = originalMaximum;
        Time.timeScale = originalPlaybackSpeed;
    }

    private IEnumerator HeadOnTraversal(string name)
    {
        CleanupCharacter(); MovementPlayer(new(10,0,10));
        var lower = CreateValue(template[1], new(-6.3f,.1f,-12.2f));
        var upper = CreateValue(template[2], new(-4.1f,3.2f,-17.6f));
        yield return new WaitForSeconds(.6f);
        var endPoint = new[] { new Vector3(-3.92f,3.2f,-17f), new Vector3(-5.6f,.03f,-10.6f) };
        for (int i = 0; i < 2; i++)
        {
            var enemy = character[i];
            var tree = enemy.GetComponent<BehaviorDesigner.Runtime.BehaviorTree>(); tree.DisableBehavior(); tree.enabled = false;
            enemy.SetPatrolTarget(endPoint[i]); enemy.BeginPatrol(); enemy.Agent.SetDestination(endPoint[i]);
        }
        float begin = Time.time, next = 0;
        var arrival = new HashSet<VillagerController>();
        while (Time.time - begin < 150 && arrival.Count < 2)
        {
            if (lower.transform.position.y > 2.9f && Vector3.Distance(lower.transform.position, endPoint[0]) < 1f) arrival.Add(lower);
            if (upper.transform.position.y < .7f && Vector3.Distance(upper.transform.position, endPoint[1]) < 1f) arrival.Add(upper);
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory(name, Time.time - begin); }
            yield return null;
        }
        Record(name, arrival.Count == 2, Time.time - begin, "双向普通行走；到达=" + arrival.Count);
    }

    private IEnumerator MaximumPresentChase()
    {
        int firstWave = mission.firstWaveGanado + mission.firstWaveSawman;
        int iterationWave = Mathf.FloorToInt(firstWave * mission.nextWaveRemainingRatio) + mission.secondWaveGanado + mission.secondWaveSawman;
        int endWave = Mathf.FloorToInt(iterationWave * mission.nextWaveRemainingRatio) + mission.thirdWaveGanado + mission.thirdWaveSawman;
        int characterCount = Mathf.Max(firstWave, Mathf.Max(iterationWave, endWave));
        CleanupCharacter(); MovementPlayer(new(-3.92f,3.2f,-12f));
        fixedChaseTarget = player.transform.position;
        var rigidbody = player.GetComponent<Rigidbody>();
        var originalConstraint = rigidbody.constraints;
        rigidbody.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        var register = new Dictionary<VillagerController, MissionEnemyType>();
        var generate = new MissionSpawnExecutor(mission, player, HouseThreeTraversalGeometry.Active,
            FindObjectsByType<EnemyNavigationPreciseLink>(FindObjectsSortMode.None), register);
        for (int i = 0; i < characterCount; i++)
        {
            var prefab = template[i % template.Length];
            if (!generate.TryGetValidSpawn(prefab, out var point, out var orientation))
            { Record("配置出生点", false, 0, "无法取得安全出生点"); break; }
            var newCharacter = CreateValue(prefab, point);
            newCharacter.transform.rotation = orientation;
            register.Add(newCharacter, MissionEnemyType.Ganado);
        }
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in character) { enemy.ReceiveSharedCombatAlert(player.transform.position); enemy.BeginChase(); }
        var arrival = new HashSet<VillagerController>(); float begin = Time.time, next = 0;
        while (Time.time - begin < 150 && arrival.Count < characterCount)
        {
            foreach (var enemy in character) if (!enemy.IsDead && enemy.transform.position.y > 2.9f && enemy.transform.position.z < -16.5f) arrival.Add(enemy);
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory("最大在场追击", Time.time - begin); }
            yield return null;
        }
        Record("最大在场追击", arrival.Count == characterCount, Time.time - begin, "配置峰值=" + characterCount + "；到达=" + arrival.Count);
        rigidbody.constraints = originalConstraint;
        fixedChaseTarget = null;
    }

    private IEnumerator CrowdedExitPointAcceptance(int round)
    {
        CleanupCharacter(); MovementPlayer(new(-3.92f,3.24f,-12f));
        fixedChaseTarget = player.transform.position;
        // 使用实际堵塞记录中的布置，验证出口转弯不被身后跟随者锁死。
        var positionValue = new[] { new Vector3(-3.2898f,3.2f,-12.3478f), new(-6.2908f,3.2f,-18.308f),
            new(-3.4577f,3.2f,-11.4513f), new(-3.8517f,3.2f,-12.8518f),
            new(-6.2856f,3.2f,-19.038f), new(-6.0864f,3.0044f,-17.6072f),
            new(-6.2832f,2.5702f,-16.9043f), new(-3.1001f,3.2f,-13.8171f) };
        for (int i = 0; i < positionValue.Length; i++) CreateValue(template[i / 2], positionValue[i]);
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in character) { enemy.ReceiveSharedCombatAlert(player.transform.position); enemy.BeginChase(); }
        float begin = Time.time, next = 0; bool clearExitPoint = false;
        while (Time.time - begin < 45)
        {
            clearExitPoint = character.All(x => x.transform.position.y > 2.9f && x.transform.position.x > -5.7f);
            if (clearExitPoint) break;
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory("拥挤出口" + round, Time.time - begin); }
            yield return null;
        }
        Record("拥挤出口" + round, clearExitPoint, Time.time - begin, "八人原失败布局；普通路径方向与避让候选共同检查净空");
        fixedChaseTarget = null;
    }

    private IEnumerator SixIndexRoomChase()
    {
        var door = FindObjectsByType<DoorController>(FindObjectsSortMode.None).First(x => x.name == "DoorRoot_06");
        if (door.IsOpen) { door.Interact(); yield return new WaitForSeconds(2); }
        bool hasClose = !door.IsOpen;
        var normal = door.PassageNormal;
        float inside = Vector3.Dot(Vector3.ProjectOnPlane(door.transform.up, Vector3.up), normal) >= 0 ? 1 : -1;
        var outer = AstarPath.active.GetNearest(door.PassageCenter - normal * inside * 2f, NearestNodeConstraint.Walkable).position;
        var inner = AstarPath.active.GetNearest(door.PassageCenter + normal * inside * 2.4f, NearestNodeConstraint.Walkable).position;
        yield return ChaseValue("六号房关门追击", template[1], outer, inner);
        bool atInner = character.Count > 0 && Vector3.Dot(character[0].transform.position - door.PassageCenter, normal) * inside > .35f;
        Record("六号房实际进屋", hasClose && door.IsPassageReady && atInner, 0, "起始关门、实际开门并进入室内");
    }

    private IEnumerator OuterPlatformMutualBlockingAcceptance(int round, bool hasMutualBlocking)
    {
        CleanupCharacter();
        Vector3 target = new(-9.1f,3.55f,-16.1f);
        MovementPlayer(target); fixedChaseTarget = target;
        Vector3[] startPoint = hasMutualBlocking
            ? new[] { new Vector3(-8.3861f,3.2406f,-17.6919f), new(-8.5504f,3.1804f,-18.4024f) }
            : new[] { new Vector3(-8.2f,3.25f,-18f), new(-9.4f,3.3f,-18.5f),
                new(-8.2f,3.3f,-19.3f), new(-9.4f,3.3f,-20f) };
        for (int i = 0; i < startPoint.Length; i++) CreateValue(template[hasMutualBlocking && i == 1 ? 2 : i % 3], startPoint[i]);
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in character) { enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase(); }
        var arrival = new HashSet<VillagerController>();
        float begin = Time.time, next = 0;
        string name = (hasMutualBlocking ? "外平台原互堵" : "外平台四人") + round;
        int stationaryPhaseAttackCharacterCount = 0;
        bool beginRecovery = false;
        while (Time.time - begin < 60 && arrival.Count < startPoint.Length)
        {
            foreach (var enemy in character)
                // 以真正追到玩家并开始攻击验收，不要求越过任意坐标线；
                // 角色可以从台沿下侧正常攻击，不能因此误判为仍然卡住。
                if (enemy.CurrentState == VillagerState.Attack &&
                    enemy.GetComponent<EnemyWindowTraversal>().IsOnTrackedPlatform &&
                    Vector3.ProjectOnPlane(enemy.transform.position - player.transform.position, Vector3.up).magnitude < 2f)
                    arrival.Add(enemy);
            if (!hasMutualBlocking && Time.time - begin >= 15)
            {
                if (!beginRecovery) { beginRecovery = true; stationaryPhaseAttackCharacterCount = arrival.Count; }
                // 前十五秒保留四人真实拥挤。攻击名额满时后排允许等候，
                // 随后回收已攻击者，要求剩余角色也实际追到并攻击，检查能否恢复。
                foreach (var enemy in arrival) enemy.gameObject.SetActive(false);
            }
            if (Time.time >= next)
            {
                next = Time.time + .25f; SampleTrajectory(name, Time.time - begin);
                foreach (var enemy in character)
                {
                    var text = new StringBuilder(); enemy.CollectCombatTimelineSnapshot().AppendValue(text);
                    File.AppendAllText(Path.Combine(outputDirectory, "外平台状态.txt"),
                        name + " " + (Time.time - begin).ToString("F2") + " " + enemy.GetInstanceID() + " " + text + "\n");
                }
            }
            yield return null;
        }
        if (!beginRecovery) stationaryPhaseAttackCharacterCount = arrival.Count;
        Record(name, arrival.Count == startPoint.Length && stationaryPhaseAttackCharacterCount > 0, Time.time - begin,
            "实际进入攻击=" + arrival.Count + "/" + startPoint.Length +
            (hasMutualBlocking ? "；保持双方在场" : "；静止阶段攻击=" + stationaryPhaseAttackCharacterCount + "；十五秒后逐个回收已攻击者"));
        fixedChaseTarget = null;
    }

    private IEnumerator OuterPlatformMultipleCharactersChase(Vector3? specifiedTarget = null)
    {
        CleanupCharacter(); Vector3 target = specifiedTarget ?? new Vector3(-9.1f,3.55f,-16.1f);
        MovementPlayer(target); fixedChaseTarget = target;
        for (int i = 0; i < 8; i++)
            CreateValue(template[i % 3], new Vector3(-5f + i % 2 * 1.1f,3.3f,-18.8f + i / 2 * 1.05f));
        yield return new WaitForSeconds(.6f);
        foreach (var enemy in character) { enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase(); }
        var arrival = new HashSet<VillagerController>();
        float begin = Time.time, next = 0;
        while (Time.time - begin < 150 && arrival.Count < 8)
        {
            foreach (var enemy in character)
                if (!arrival.Contains(enemy) && enemy.CurrentState == VillagerState.Attack &&
                    enemy.GetComponent<EnemyWindowTraversal>().IsOnTrackedPlatform)
                {
                    arrival.Add(enemy);
                    // 到达者回收，验证窗口与窄路占用释放后队尾仍能继续追击。
                    enemy.gameObject.SetActive(false);
                }
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory("八人翻窗追击与回收", Time.time - begin); }
            yield return null;
        }
        Record("八人翻窗追击与回收", arrival.Count == 8, Time.time - begin, "实际进入攻击=" + arrival.Count + "/8；到达者逐个回收");
        fixedChaseTarget = null;
    }

    private IEnumerator WindowChase()
    {
        var window = FindObjectsByType<EnemyWindowTraversalRoute>(FindObjectsSortMode.None).Where(x => x.platformRegionId == HouseThreeRegionNavigation.Current.outerPlatformIdentifier).ToArray();
        foreach (var windowValue in window)
        {
            if (!windowValue.TryAcquireWindowSideTread(false, out var inner) || !windowValue.TryAcquireWindowSideTread(true, out var outer)) { Record(windowValue.name, false, 0, "缺少落点"); continue; }
            foreach (bool exit in windowValue.allowBothDirections ? new[] { true, false } : new[] { true })
            {
                // 玩家站在窗口附近的踏面，不能固定在敌人的精确落点上，
                // 否则测试夹住双方碰撞体，会把玩家顶到敌人头上。
                Vector3 target = exit
                    ? outer + new Vector3(0, 0, outer.x < -4 ? 1.6f : -1.6f)
                    : inner + new Vector3(outer.x < -4 ? 1.5f : -1.5f, 0, 0);
                yield return VaultChase(windowValue.name + (exit ? "出窗" : "进窗"), exit ? inner : outer, target, 1);
            }
        }
        bool hasDetour = false;
        foreach (var first in window)
        foreach (var second in window)
        {
            if (hasDetour || first == second || !first.allowBothDirections || !first.TryAcquireWindowSideTread(true, out var start) || !second.TryAcquireWindowSideTread(true, out var end)) continue;
            var a = AstarPath.active.GetNearest(start, NearestNodeConstraint.Walkable).node;
            var b = AstarPath.active.GetNearest(end, NearestNodeConstraint.Walkable).node;
            if (a == null || b == null || PathUtilities.IsPathPossible(a, b)) continue;
            hasDetour = true;
            yield return VaultChase("外平台借室内绕行", start, end, 2);
        }
        if (!hasDetour && window.Length >= 2 && window[0].TryAcquireWindowSideTread(true, out var firstOuter) && window[1].TryAcquireWindowSideTread(true, out var secondOuter))
        {
            // 整段绕屋外可能比两次翻窗更远，合法的择优路线也必须允许。
            // 近拐角不许折返以及整段纯步行分别由拐角专项检查。
            firstOuter += new Vector3(0, 0, firstOuter.x < -4 ? 1.6f : -1.6f);
            secondOuter += new Vector3(0, 0, secondOuter.x < -4 ? 1.6f : -1.6f);
            yield return VaultChase("两窗外平台择优追击", firstOuter, secondOuter, 0, 2);
        }
    }

    private IEnumerator CornerChase(EnemyWindowTraversalRoute window, GameObject prefab, int round)
    {
        CleanupCharacter();
        if (!window.TryAcquireWindowSideTread(false, out var inner) || !window.TryAcquireWindowSideTread(true, out var outer))
        { Record("拐角窗口落点", false, 0, window.name); yield break; }
        bool eastSide = outer.x > -4;
        Vector3 endPoint = new(-2.6f,3.4f,-22.5f);
        // 测单次翻窗接拐角时，里昂站在出口旁的踏面，给翻窗落脚保留净空。
        Vector3 nearExit = outer + Vector3.forward * 1.6f;
        if (eastSide)
        {
            nearExit = outer - Vector3.forward * 1.6f;
        }
        MovementPlayer(nearExit);
        fixedChaseTarget = nearExit;
        var enemy = CreateValue(prefab, inner);
        var vault = enemy.GetComponent<EnemyWindowTraversal>();
        yield return new WaitForSeconds(.6f);
        enemy.ReceiveSharedCombatAlert(nearExit);
        enemy.BeginChase();
        string nameValue = "拐角" + round + window.name + prefab.name;
        float begin = Time.time, next = 0, minimumVertical = float.PositiveInfinity, maximumBacktrack = 0;
        int count = 0, phase = 0; bool previousFrame = false, arrival = false, correctLandPoint = false;
        while (Time.time - begin < 50)
        {
            bool currentFrame = vault.IsWindowTraversalActive;
            if (currentFrame && !previousFrame) count++;
            previousFrame = currentFrame;
            if (phase == 0 && count > 0 && !vault.IsMovementProtected && vault.IsOnTrackedPlatform)
            {
                correctLandPoint = Vector3.Distance(enemy.transform.position, outer) < .65f;
                phase = eastSide ? 1 : 2;
                var target = eastSide ? new Vector3(-.6f,3.5f,-22f) : endPoint;
                MovementPlayer(target); fixedChaseTarget = target;
                enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase();
            }
            if (phase == 1 && enemy.transform.position.z < -20)
            {
                phase = 2; MovementPlayer(endPoint); fixedChaseTarget = endPoint;
            }
            if (phase == 2)
            {
                if (eastSide)
                {
                    minimumVertical = Mathf.Min(minimumVertical, enemy.transform.position.z);
                    maximumBacktrack = Mathf.Max(maximumBacktrack, enemy.transform.position.z - minimumVertical);
                }
                if (!currentFrame && enemy.CurrentState == VillagerState.Attack &&
                    Vector3.Distance(enemy.transform.position, player.transform.position) < 2.6f)
                { arrival = true; break; }
            }
            if (Time.time >= next)
            {
                next = Time.time + .2f; SampleTrajectory(nameValue, Time.time - begin);
                File.AppendAllText(Path.Combine(outputDirectory, "拐角选路.txt"), nameValue + " " + (Time.time - begin).ToString("F2") +
                    " 阶段=" + phase + " 敌=" + enemy.transform.position.ToString("F3") +
                    " 玩家=" + player.transform.position.ToString("F3") + " 目标=" + enemy.Agent.Destination.ToString("F3") +
                    " 待翻窗=" + vault.HasPendingRoute + " 翻越次数=" + count + "\n");
            }
            yield return null;
        }
        Record(nameValue, arrival && correctLandPoint && count == 1 && maximumBacktrack < .8f, Time.time - begin,
            "实际出指定窗口后追到拐角；翻越次数=" + count + "；向入口折返米=" + maximumBacktrack.ToString("F3"));
        fixedChaseTarget = null;
    }

    private IEnumerator VaultChase(string name, Vector3 startPoint, Vector3 target, int atLeastVault, int maximumVault = -1)
    {
        if (maximumVault < 0) maximumVault = atLeastVault;
        CleanupCharacter(); MovementPlayer(target); fixedChaseTarget = target; var enemy = CreateValue(template[1], startPoint);
        yield return new WaitForSeconds(.6f); enemy.ReceiveSharedCombatAlert(target); enemy.BeginChase();
        var vault = enemy.GetComponent<EnemyWindowTraversal>();
        float begin = Time.time, next = 0; int count = 0; bool previousFrame = false, arrival = false;
        var traversalDirection = Vector3.ProjectOnPlane(target - startPoint, Vector3.up).normalized;
        float farthestProgress = 0, lastAdvance = begin, mostLongStagnation = 0;
        while (Time.time - begin < 48)
        {
            if (name.StartsWith("沿木板"))
            {
                float progress = Vector3.Dot(enemy.transform.position - startPoint, traversalDirection);
                if (progress > farthestProgress + .1f) { farthestProgress = progress; lastAdvance = Time.time; }
                mostLongStagnation = Mathf.Max(mostLongStagnation, Time.time - lastAdvance);
            }
            bool currentFrame = vault.IsWindowTraversalActive;
            if (currentFrame && !previousFrame) count++;
            previousFrame = currentFrame;
            if (!currentFrame && count >= atLeastVault && count <= maximumVault && enemy.CurrentState == VillagerState.Attack && Vector3.Distance(enemy.transform.position, player.transform.position) < 2.6f) { arrival = true; break; }
            if (Time.time >= next) { next = Time.time + .4f; SampleTrajectory(name, Time.time - begin); }
            yield return null;
        }
        Record(name, arrival && count >= atLeastVault && count <= maximumVault && mostLongStagnation <= 5f, Time.time - begin,
            "翻越次数=" + count + "；玩家终点=" + player.transform.position + "；最长通路停滞秒=" + mostLongStagnation.ToString("F2"));
        fixedChaseTarget = null;
    }

    private IEnumerator EnemyGroundContact()
    {
        MovementPlayer(new(10,0,10));
        foreach (var prefab in template)
        for (int i = 0; i < stopMovementPosition.Length; i++)
        {
            CleanupCharacter(); var pointValue = stopMovementPosition[i];
            if (stairGeometry.RaycastNativeStairTread(new Ray(pointValue + Vector3.up * .45f, Vector3.down), out var hit, .9f)) pointValue.y = hit.point.y;
            var enemy = CreateValue(prefab, pointValue, true);
            float begin = Time.time, next = 0; int sample = 0; bool valid = true;
            while (Time.time - begin < 11)
            {
                yield return new WaitForEndOfFrame();
                var foot = enemy.GetComponent<BipedFootPlanting>();
                if (Time.time - begin < 1 || !foot || !foot.leftLeg || Time.time < next) continue;
                next = Time.time + .1f; sample++;
                valid &= enemy.GetComponents<BipedFootPlanting>().Length == 1 && foot.leftLeg.GetComponents<RootMotion.FinalIK.LimbIK>().Length == 1 && foot.rightLeg.GetComponents<RootMotion.FinalIK.LimbIK>().Length == 1;
                SampleDualFoot(prefab.name, i, Time.time - begin, foot);
            }
            Capture(prefab.name + "_" + i, enemy.transform);
            Record(prefab.name + "停步" + i, valid && sample > 30, Time.time - begin, "样本=" + sample);
            var shared = enemy.GetComponent<BipedFootPlanting>();
            enemy.gameObject.SetActive(false); yield return null;
            var newPosition = stopMovementPosition[(i + 1) % stopMovementPosition.Length];
            if (stairGeometry.RaycastNativeStairTread(new Ray(newPosition + Vector3.up * .45f, Vector3.down), out var newTread, .9f)) newPosition.y = newTread.point.y;
            enemy.transform.position = newPosition;
            enemy.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            yield return new WaitForEndOfFrame();
            bool newLandFootPosition = shared;
            float maximumSoleError = 0;
            foreach (bool left in new[] { true, false })
            {
                var footPosition = (left ? shared.leftLeg : shared.rightLeg).solver.bone3.transform.TransformPoint(left ? shared.leftSole : shared.rightSole);
                newLandFootPosition &= stairGeometry.RaycastNativeStairTread(new Ray(footPosition + Vector3.up * .4f, Vector3.down), out var landPoint, 1f) && Mathf.Abs(footPosition.y - landPoint.point.y) < .01f;
                maximumSoleError = Mathf.Max(maximumSoleError, Mathf.Abs(footPosition.y - landPoint.point.y));
            }
            Record(prefab.name + "贴地复用" + i, newLandFootPosition && shared == enemy.GetComponent<BipedFootPlanting>() && shared.isActiveAndEnabled && enemy.GetComponents<BipedFootPlanting>().Length == 1 &&
                shared.leftLeg.GetComponents<RootMotion.FinalIK.LimbIK>().Length == 1 && shared.rightLeg.GetComponents<RootMotion.FinalIK.LimbIK>().Length == 1,
                1.5f, "异地复用原解算器与唯一腿链；鞋底误差厘米=" + maximumSoleError * 100);
        }
    }

    private void SampleDualFoot(string name, int positionValue, float time, BipedFootPlanting foot)
    {
        const System.Reflection.BindingFlags flag = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        bool SupportWeight(bool left)
        {
            var state = typeof(BipedFootPlanting).GetField(left ? "left" : "right", flag).GetValue(foot);
            if (state == null) return false;
            var type = state.GetType();
            return ((bool)type.GetField("lockValue", flag).GetValue(state) && (float)type.GetField("lockWeight", flag).GetValue(state) >= .99f) ||
                (float)type.GetField("supportBlend", flag).GetValue(state) >= .99f;
        }
        float Error(bool left)
        {
            var footPosition = (left ? foot.leftLeg : foot.rightLeg).solver.bone3.transform.TransformPoint(left ? foot.leftSole : foot.rightSole);
            return stairGeometry.RaycastNativeStairTread(new Ray(footPosition + Vector3.up * .4f, Vector3.down), out var hit, 1f) ? footPosition.y - hit.point.y : float.NaN;
        }
        bool stepping = (foot.actionSource as IFootActionState)?.IsStepping ?? false;
        float stationary = (float)typeof(BipedFootPlanting).GetField("stationaryTime", flag).GetValue(foot);
        footprint.AppendLine($"{name},{positionValue},{time:F3},{foot.transform.position.y:F6},{Error(true):F6},{Error(false):F6},{foot.pelvis.position.y:F6},{foot.GetComponents<BipedFootPlanting>().Length},{foot.GetComponentsInChildren<RootMotion.FinalIK.LimbIK>().Length},{SupportWeight(true)},{SupportWeight(false)},{stepping},{stationary:F3}");
        File.WriteAllText(Path.Combine(outputDirectory, "贴地测量.csv"), footprint.ToString());
    }

    private IEnumerator PlayerGroundContact()
    {
        CleanupCharacter(); player.SetSprinting(false); player.TrySetCrouching(false);
        var foot = player.GetComponent<BipedFootPlanting>();
        for (int i = 0; i < stopMovementPosition.Length; i++)
        {
            MovementPlayer(stopMovementPosition[i] + Vector3.up * .08f);
            // 先完成自动转身与落脚，再开始十秒静止测量，转身另有独立用例。
            var rigidbody = player.GetComponent<Rigidbody>();
            rigidbody.rotation = Quaternion.Euler(0, player.playerCamera.transform.eulerAngles.y, 0);
            player.transform.rotation = rigidbody.rotation;
            yield return new WaitForEndOfFrame();
            float waiting = Time.time;
            var stationaryField = typeof(BipedFootPlanting).GetField("stationaryTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            while (Time.time - waiting < 8 && (!player.IsGrounded || (float)stationaryField.GetValue(foot) < .5f))
                yield return new WaitForEndOfFrame();
            bool hasStandingStable = player.IsGrounded && (float)stationaryField.GetValue(foot) >= .5f;
            float begin = Time.time, next = 0;
            while (Time.time - begin < 11)
            {
                yield return new WaitForEndOfFrame();
                if (Time.time - begin > 1 && Time.time >= next) { next = Time.time + .1f; SampleDualFoot("里昂", i, Time.time - begin, foot); }
            }
            Capture("里昂_" + i, player.transform);
            Record("里昂停步" + i, hasStandingStable && foot && foot.enabled && player.IsGrounded, 11, "先站稳再静止十秒；记录左右承重状态以排除摆动脚");
        }
    }


    private static float LegWeight(BipedFootPlanting foot) => foot && foot.leftLeg && foot.rightLeg
        ? Mathf.Max(foot.leftLeg.solver.IKPositionWeight, foot.rightLeg.solver.IKPositionWeight) : -1;

    private IEnumerator ActionHandoffAcceptance()
    {
        foreach (var prefab in template)
        {
            CleanupCharacter(); MovementPlayer(new(-6.3f,3.25f,-18.6f));
            var enemy = CreateValue(prefab, new(-6.3f,2.59f,-17f));
            yield return new WaitForSeconds(.5f);
            enemy.ReceiveSharedCombatAlert(player.transform.position);enemy.BeginChase();
            float waiting = Time.time;
            while (Time.time - waiting < 18 && enemy.CurrentState != VillagerState.Attack) yield return null;
            bool begin = enemy.CurrentState == VillagerState.Attack;
            var foot = enemy.GetComponent<BipedFootPlanting>();
            yield return new WaitForSeconds(.35f);
            bool hasExit = begin && LegWeight(foot) < .01f;
            float attackHeight = enemy.transform.position.y;
            yield return new WaitForSeconds(3);
            enemy.EndAttack(); enemy.assassinationShowcaseIdle = true; enemy.BeginIdle();
            yield return new WaitForSeconds(1.5f);
            Record(prefab.name + "攻击贴地交接", begin && hasExit && LegWeight(foot) > .8f,
                Time.time - waiting, "实际进入攻击=" + begin + "；起手退出=" + hasExit + "；恢复权重=" + LegWeight(foot) + "；动作根高=" + attackHeight);
            MovementPlayer(new(-5.6f,.03f,-10.6f));
            enemy.transform.SetPositionAndRotation(new(-6.3f,1.42f,-15f),Quaternion.Euler(0,180,0));
            foot.ResetGroundContact();Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            enemy.assassinationShowcaseIdle = false;
            float damage = enemy.staggerThreshold * Mathf.Max(1,enemy.sawmanHitReactionThresholdMultiplier) / Mathf.Max(.1f,enemy.sawmanPoiseDamageMultiplier) + 1;
            enemy.TakeHeavyFrontDamage(damage, enemy.transform.position + Vector3.up);
            bool hitReactionEstablished = enemy.HasPendingHit;
            if (hitReactionEstablished) enemy.BeginHit();
            yield return new WaitForSeconds(.35f);
            hasExit = LegWeight(foot) < .01f;
            yield return new WaitForSeconds(5);
            enemy.assassinationShowcaseIdle = true;enemy.BeginIdle(); yield return new WaitForSeconds(1.5f);
            Record(prefab.name + "受击贴地交接", hitReactionEstablished && hasExit && LegWeight(foot) > .8f, 6.85f,
                "实际受击=" + hitReactionEstablished + "；退出=" + hasExit + "；恢复权重=" + LegWeight(foot));
            enemy.transform.SetPositionAndRotation(new(-6.3f,1.42f,-15f),Quaternion.Euler(0,180,0));
            foot.ResetGroundContact();Physics.SyncTransforms();yield return new WaitForSeconds(.6f);
            enemy.assassinationShowcaseIdle = false;enemy.TakeDamage(50000);enemy.BeginDeath();yield return new WaitForSeconds(2);
            bool support = HouseThreeTraversalGeometry.Active.TryResolveStairHeight(enemy.transform.position, out float stairHeight)
                && Mathf.Abs(enemy.transform.position.y - stairHeight) < .5f;
            Record(prefab.name + "死亡支撑与贴地退出", enemy.IsDead && LegWeight(foot) < .01f && support,
                2, "根高=" + enemy.transform.position.y + "；权重=" + LegWeight(foot));
            Capture(prefab.name + "死亡支撑", enemy.transform);
        }
        foreach (var type in new[] { QTEKind.FrontGrab, QTEKind.RearBiteNeck })
        {
            CleanupCharacter(); MovementPlayer(new(-5.6f,.03f,-10.6f));
            player.transform.rotation = Quaternion.identity;
            player.GetComponent<Rigidbody>().rotation = Quaternion.identity;
            var enemy = CreateValue(template[1], player.transform.position + Vector3.forward * (type == QTEKind.FrontGrab ? .8f : -.8f), true);
            enemy.transform.rotation = type == QTEKind.FrontGrab ? Quaternion.Euler(0,180,0) : Quaternion.identity;
            yield return new WaitForSeconds(.5f);
            player.transform.rotation=Quaternion.identity;player.GetComponent<Rigidbody>().rotation=Quaternion.identity;
            var camera=FindFirstObjectByType<RE4RThirdPersonCamera>();
            typeof(RE4RThirdPersonCamera).GetField("yaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(camera,0f);
            enemy.transform.position=player.transform.position+Vector3.forward*(type==QTEKind.FrontGrab?.8f:-.8f);
            enemy.transform.rotation=type==QTEKind.FrontGrab?Quaternion.Euler(0,180,0):Quaternion.identity;
            Physics.SyncTransforms();
            var coordinate = player.GetComponent<LeonQTECoordinator>();
            bool begin = coordinate.TryStart(type, enemy.GetComponent<VillagerQTEParticipant>(), out int session);
            yield return new WaitForSeconds(.8f);
            bool bothSidesExit = LegWeight(player.GetComponent<BipedFootPlanting>()) < .01f && LegWeight(enemy.GetComponent<BipedFootPlanting>()) < .01f;
            if (begin) coordinate.Cancel(session, QTECancelReason.AnimationInterrupted);
            yield return new WaitForSeconds(2);
            enemy.BeginIdle();yield return new WaitForSeconds(1.5f);
            bool restore = !coordinate.IsActive && !enemy.IsQTEControlled && LegWeight(enemy.GetComponent<BipedFootPlanting>()) > .8f;
            Record(type == QTEKind.FrontGrab ? "正面环抱贴地交接" : "背后环抱贴地交接", begin && bothSidesExit && restore,
                4.3f, "开始=" + begin + "；退出=" + bothSidesExit + "；恢复=" + restore + "；原因=" + coordinate.LastRejectionReason);
        }
        CleanupCharacter();
    }


    private IEnumerator ConnectedPlatformDirectChase()
    {
        foreach (var window in EnemyWindowTraversalRoute.ActiveRoutes)
        {
            if (window.platformRegionId != HouseThreeRegionNavigation.Current.outerPlatformIdentifier || !window.TryAcquireWindowSideTread(true, out var start)) continue;
            var startNode = AstarPath.active.GetNearest(start, NearestNodeConstraint.Walkable).node;
            Vector3 end = start; float farthest = 0;
            foreach (var image in AstarPath.active.graphs)
            {
                if (image == null) continue;
                image.GetNodes(node => {
                    if (!node.Walkable || node.Area != startNode.Area) return;
                    Vector3 point = (Vector3)node.position;
                    float distance = Vector3.Distance(point, start);
                    if (distance > farthest && distance < 4f && HouseThreeRegionNavigation.Current.TreadRegion(point) == VillageTraversalRegion.SecondFloorOuterPlatform)
                    { farthest = distance; end = point; }
                });
            }
            if (farthest < 1.5f) continue;
            yield return VaultChase("连通平台直接追击", start, end, 0);
            yield break;
        }
        Record("连通平台直接追击", false, 0, "未找到符合距离的连通平台测试点");
    }

    private IEnumerator ConfigurationJumpLandAcceptance()
    {
        foreach (var path in EnemyPlatformDropRoute.ActiveRoutes.ToArray())
        {
            CleanupCharacter(); MovementPlayer(path.transform.position + path.transform.forward * 2 + Vector3.down * 3);
            // 初始化期间先待机，避免正式感知先开始追击，改变待验证的跳落起点。
            var enemy = CreateValue(template[1], path.transform.position, true);
            yield return new WaitForSeconds(.5f);
            if (!path.TryBuildPlan(enemy, player.transform.position, out var plan))
            { Record(path.name + "配置跳落", false, 0, "无法从配置位置构建跳落"); continue; }
            enemy.transform.position = plan.sourceGround;
            MovementPlayer(plan.landingGround + Vector3.up * .08f);
            enemy.assassinationShowcaseIdle = false;
            enemy.ReceiveSharedCombatAlert(player.transform.position);enemy.BeginChase();
            var action = enemy.GetComponent<EnemyWindowTraversal>();
            bool hasJump = false, hasExit = false;float begin = Time.time;
            while (Time.time - begin < 25)
            {
                if (action.IsDropActive)
                { hasJump = true; hasExit |= LegWeight(enemy.GetComponent<BipedFootPlanting>()) < .01f; }
                if (hasJump && !action.IsMovementProtected && Mathf.Abs(enemy.transform.position.y - plan.landingGround.y) < .5f) break;
                yield return new WaitForEndOfFrame();
            }
            Record(path.name + "配置跳落", hasJump && hasExit && !action.IsMovementProtected && Mathf.Abs(enemy.transform.position.y - plan.landingGround.y) < .5f,
                Time.time - begin, "已跳=" + hasJump + "；贴地退出=" + hasExit + "；落地高度=" + enemy.transform.position.y);
        }
    }

    private IEnumerator PlayerWalkAcceptance()
    {
var p=LeonController.Instance;
var body=p.GetComponent<Rigidbody>(); var anim=p.GetComponent<Animator>(); var root=p.transform;

        var left = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.LeftFoot);
        var right = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.RightFoot);
        var hl = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Hips);
        var kl = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.LeftLowerLeg);
        var kr = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.RightLowerLeg);
var soleL=new Vector3(-.00404f,.03211f,.10251f);var soleR=new Vector3(-.00715f,.07810f,.07978f);
var feet=p.GetComponent<BipedFootPlanting>();feet.enabled=true;var outDir=Path.Combine(outputDirectory,"里昂移动对照");
System.IO.Directory.CreateDirectory(outDir);var cam=new GameObject("验收观察").AddComponent<Camera>();cam.enabled=false;cam.CopyFrom(p.playerCamera);cam.enabled=false;cam.nearClipPlane=.02f;cam.useOcclusionCulling=false;cam.allowHDR=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.gray;
var rt=new RenderTexture(480,360,24);rt.Create();cam.targetTexture=rt;var tex=new Texture2D(480,360,TextureFormat.RGB24,false);
var data=new System.Text.StringBuilder("帧,阶段,根横,根高,根纵,左横,左高,左纵,右横,右高,右纵,左地,右地,左膝高,右膝高,骨盆高,接地,状态\n");
var dir=new Vector3(.035f,0,-1).normalized;var start=new Vector3(-6.35f,.28f,-12.65f);
System.Action<Vector3,Vector3> place=(point,heading)=>{p.SetMoveInput(Vector3.zero);body.linearVelocity=Vector3.zero;body.position=point;body.rotation=Quaternion.LookRotation(heading);root.SetPositionAndRotation(point,body.rotation);var driver=UnityEngine.Object.FindFirstObjectByType<RE4RThirdPersonCamera>();typeof(RE4RThirdPersonCamera).GetField("yaw",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(driver,root.eulerAngles.y);Physics.SyncTransforms();};
foreach(var e in UnityEngine.Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
var input=p.GetComponent<LeonInputBridge>();input.enabled=false;Time.timeScale=1;Time.captureDeltaTime=1f/24f;
int n=0;float stopZ=-14.86f;bool held=false;int hold=0;
place(start,dir);
while (hold < 273) {
yield return new WaitForEndOfFrame();
if(!Application.isPlaying||!cam)yield break;
if(n==239)Record("里昂连续上楼",root.position.y>3f && root.position.z < -18f,10,root.position.ToString("F3"));
if(n==479)Record("里昂连续下楼",root.position.y<.6f && root.position.z > -13.3f,10,root.position.ToString("F3"));
var mode=n<240?"上楼":n<480?"下楼":"收步";if(n==240)place(new Vector3(-6.17f,3.28f,-18.36f),-dir);if(n==480)place(start,dir);
var heading=mode=="下楼"?-dir:dir;var age=n%240;
if(mode=="收步"&&(root.position.z<=stopZ||age>82))held=true;
bool moving=mode=="收步"?age>12&&!held:age>12&&(mode=="上楼"?root.position.z>-18.34f:root.position.z<-12.6f);
var ct=p.playerCamera.transform;var cm=Vector3.ProjectOnPlane(ct.forward,Vector3.up).normalized;var cr=Vector3.Cross(Vector3.up,cm);
p.SetMoveInput(moving?new Vector3(Vector3.Dot(heading,cr),0,Vector3.Dot(heading,cm)):Vector3.zero);
if(held)hold++;
var l=left.TransformPoint(soleL);var r=right.TransformPoint(soleR);float floor(Vector3 v){return stairGeometry.RaycastNativeStairTread(new Ray(v+Vector3.up*.4f,Vector3.down),out var h,.9f)?h.point.y:float.NaN;}
string v3(Vector3 v)=>v.x.ToString("F5")+","+v.y.ToString("F5")+","+v.z.ToString("F5");
data.AppendLine(n+","+mode+","+v3(root.position)+","+v3(l)+","+v3(r)+","+floor(l)+","+floor(r)+","+kl.position.y+","+kr.position.y+","+hl.position.y+","+p.IsGrounded+","+p.currentState);
bool multi=hold==30||hold==272||n==49||n==289;
if((mode!="收步"&&age<=158&&n%2==0)||multi){
var center=root.position+Vector3.up*.40f;
var offsets=new[]{new Vector3(1.45f,.12f,.13f),new Vector3(-1.45f,.12f,.13f),new Vector3(.8f,.45f,-1.45f),new Vector3(-.75f,.5f,1.35f),new Vector3(0,2.4f,.02f),Vector3.zero};
for(int i=0;i<(multi?6:1);i++){cam.fieldOfView=i==5?p.playerCamera.fieldOfView:60;cam.transform.position=i==5?ct.position:center+offsets[i];cam.transform.rotation=i==5?ct.rotation:Quaternion.LookRotation(center-cam.transform.position);cam.nearClipPlane=i==5?.02f:Mathf.Max(.02f,offsets[i].magnitude-(i==4?.06f:.42f));var old=RenderTexture.active;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,480,360),0,0);tex.Apply();RenderTexture.active=old;var bytes=tex.EncodeToJPG(90);if(multi){var lp=cam.WorldToViewportPoint(l);var rp=cam.WorldToViewportPoint(r);System.IO.File.AppendAllText(outDir+"/画面接触点.csv",mode+","+(hold>0?hold:age)+","+i+","+lp.x+","+lp.y+","+rp.x+","+rp.y+"\n");}if(multi)System.IO.File.WriteAllBytes(outDir+"/"+mode+"_"+(hold>0?hold:age)+"_"+i+".jpg",bytes);if(i==0&&mode!="收步")System.IO.File.WriteAllBytes(outDir+"/"+mode+age.ToString("000")+".jpg",bytes);}
}
if(hold==30||hold==150||hold==272){var render=p.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("boots.rubber"));var mesh=new Mesh();render.BakeMesh(mesh);var v=mesh.vertices.Select(x=>render.transform.TransformPoint(x)).ToArray();var sb=new System.Text.StringBuilder();foreach(var side in new[]{true,false}){var leg=(side?feet.leftLeg:feet.rightLeg).solver;var sole=leg.bone3.transform.TransformPoint(side?feet.leftSole:feet.rightSole);var other=(side?feet.rightLeg:feet.leftLeg).solver.bone3.transform;var near=v.Where(x=>Vector3.Distance(x,leg.bone3.transform.position)<Vector3.Distance(x,other.position)&&Vector3.ProjectOnPlane(x-sole,Vector3.up).magnitude<.07f).ToArray();var values=near.Where(x=>stairGeometry.RaycastNativeStairTread(new Ray(x+Vector3.up*.35f,Vector3.down),out var h,.85f)&&Mathf.Abs(h.point.y-sole.y)<.06f).Select(x=>{stairGeometry.RaycastNativeStairTread(new Ray(x+Vector3.up*.35f,Vector3.down),out var h,.85f);return x.y-h.point.y;}).ToArray();var knee=Vector3.Angle(leg.bone2.transform.position-leg.bone1.transform.position,leg.bone3.transform.position-leg.bone2.transform.position);sb.AppendLine((side?"左":"右")+" "+sole.ToString("F3")+" 膝屈 "+knee.ToString("F1")+" 最近胶底与踏面 "+(values.Length>0?values.Min().ToString("F5"):"无样本")+" 顶点 "+values.Length);}
UnityEngine.Object.Destroy(mesh);System.IO.File.AppendAllText(outDir+"/鞋底网格实测.txt","收步 "+hold+" 帧\n"+sb.ToString());}
if(n%24==0||hold==273)System.IO.File.WriteAllText(outDir+"/测量.csv",data.ToString());n++;
if(hold>=273){p.SetMoveInput(Vector3.zero);Time.captureDeltaTime=0;input.enabled=true;cam.targetTexture=null;UnityEngine.Object.Destroy(cam.gameObject);UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);System.IO.File.WriteAllText(outDir+"/完成.txt","完成");}
}
Record("里昂上下楼与十秒收步", p.IsGrounded && hold >= 273, n/24f, "同基线位置、输入、帧间隔；原始测量和近景连续帧单独保存");

    }


    private IEnumerator SceneReloadAcceptance()
    {
        CleanupCharacter();
        var previousStair = LeonPlayerStairRampRuntime.CurrentStair;
        DontDestroyOnLoad(gameObject);
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(SceneManagerEx.MainScenePath);
        yield return null;
        yield return new WaitForSeconds(1);
        player = LeonController.Instance;
        mission = FindFirstObjectByType<ChapterOneMissionManager>();
        if (mission) {mission.enabled=false;mission.StopAllCoroutines();}
        var newStair = LeonPlayerStairRampRuntime.CurrentStair;
        var newFoot = player ? player.GetComponent<BipedFootPlanting>() : null;
        bool normal = !previousStair && newStair && HouseThreeRegionNavigation.Current && HouseThreeRegionNavigation.Current.ConfigurationError() == null &&
            newFoot && newFoot.isActiveAndEnabled && FindObjectsByType<HouseThreeTraversalGeometry>(FindObjectsSortMode.None).Length == 1;
        Record("场景重载绑定", normal, 1, "旧采样销毁、新采样唯一、玩家重绑、正式图正常加载");
    }


    private IEnumerator ProductionImageRuntimeRegression()
    {
        var startPoint = FindFirstObjectByType<LeonPlayerStairRampRuntime>().initialCharacter.Where(x=>x).ToArray();
        var doorGroup = FindObjectsByType<DoorController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var funnel = startPoint.Select(x=>x.GetComponent<FunnelModifier>()).FirstOrDefault(x=>x);
        int totalCount=0,failure=0,skip=0,penetrationWall=0;
        foreach(var enemy in startPoint)
        foreach(var door in doorGroup)
        foreach(float side in new[]{1f,-1f})
        foreach(float depth in new[]{1.25f,2.25f,3.25f})
        foreach(float offset in new[]{0f,.75f,-.75f})
        {
            var normal=door.PassageNormal;var tangent=new Vector3(-normal.z,0,normal.x);
            var target=door.PassageCenter+normal*side*depth+tangent*offset;
            var end=AstarPath.active.GetNearest(target,NearestNodeConstraint.Walkable);
            var start=AstarPath.active.GetNearest(enemy.transform.position,NearestNodeConstraint.Walkable);
            if(end.node==null||Vector3.ProjectOnPlane(target-(Vector3)end.position,Vector3.up).magnitude>1.5f||Mathf.Abs(target.y-((Vector3)end.position).y)>1.5f){skip++;continue;}
            totalCount++;
            if(start.node==null||start.node.Area!=end.node.Area){failure++;continue;}
            var path=ABPath.Construct(enemy.transform.position,end.position);
            AstarPath.StartPath(path);yield return path.WaitForPath();
            if(path.error||!funnel){failure++;continue;}
            bool originalThinPart=funnel.splitAtEveryPortal;funnel.splitAtEveryPortal=true;funnel.Apply(path);funnel.splitAtEveryPortal=originalThinPart;
            if(Vector3.Distance(path.vectorPath[path.vectorPath.Count-1],end.position)>.05f)path.vectorPath.Add(end.position);
            bool hitWall=false;
            for(int i=1;i<path.vectorPath.Count&&!hitWall;i++)
            {
                var startValue=path.vectorPath[i-1]+Vector3.up*1.2f;var difference=path.vectorPath[i]-path.vectorPath[i-1];
                foreach(var hit in Physics.RaycastAll(startValue,difference.normalized,difference.magnitude))
                {
                    var c=hit.collider;
                    if(!c||c.isTrigger||c.GetComponentInParent<DoorController>()||c.GetComponentInParent<VillagerController>()||c.GetComponentInParent<LeonController>())continue;
                    hitWall=true;break;
                }
            }
            if(hitWall)penetrationWall++;
        }
        Record("正式图五百零四条运行回归",totalCount==504&&failure==0&&skip==0&&penetrationWall==0,0,
            "检查="+totalCount+"；失败="+failure+"；跳过="+skip+"；穿墙="+penetrationWall);
    }


    private IEnumerator PerceptionAndAimAcceptance()
    {
        CleanupCharacter(); MovementPlayer(new(-5.6f,.04f,-10.6f));
        var enemy=CreateValue(template[1],new(-5.6f,.04f,-12.5f));enemy.transform.rotation=Quaternion.identity;
        yield return new WaitForSeconds(2);
        bool visible=enemy.CanSeePlayer();
        Record("自然发现目标",visible && (enemy.CurrentState==VillagerState.Chase||enemy.CurrentState==VillagerState.Attack),2,"视线="+visible+"；状态="+enemy.CurrentState);
        MovementPlayer(new(-25f,0,25f));yield return new WaitForSeconds(.7f);
        Record("失去目标视线",!enemy.CanSeePlayer()&&!enemy.CanAttack(),.7f,"视线阻断且不得攻击远处目标");
        enemy.transform.position=new(-5.6f,.04f,-12.5f);enemy.transform.rotation=Quaternion.identity;
        MovementPlayer(new(-5.6f,.04f,-10.6f));yield return new WaitForSeconds(1.2f);
        Record("重新获得目标视线",enemy.CanSeePlayer(),1.2f,"通过实际视线采样重新获取");
        enemy.transform.position=new(-5.6f,.04f,-10.6f);MovementPlayer(new(-5.6f,3.2f,-10.6f));
        yield return new WaitForSeconds(.2f);
        Record("正上方禁止隔层攻击",!enemy.CanAttack(),.2f,"上下层高度差="+Mathf.Abs(player.transform.position.y-enemy.transform.position.y));
        CleanupCharacter();MovementPlayer(new(-6.27f,1.65f,-14.93f));yield return new WaitForSeconds(1);
        var combat=player.GetComponent<LeonCombat>();combat.HandleAim(true);player.SetMoveInput(new Vector3(0,0,.45f));
        yield return new WaitForSeconds(.8f);player.SetMoveInput(Vector3.zero);
        bool aimGroundContact=combat.IsAiming&&player.GetComponent<LeonFootAction>().AllowGroundContact;
        float originalAngle=player.transform.eulerAngles.y;player.SetLookInput(new Vector2(12,0));
        yield return new WaitForSeconds(.6f);player.SetLookInput(Vector2.zero);yield return new WaitForSeconds(1.5f);
        float rotateAngle=Mathf.Abs(Mathf.DeltaAngle(originalAngle,player.transform.eulerAngles.y));
        Record("瞄准移动与转身贴地",aimGroundContact&&player.IsGrounded&&rotateAngle>5,.8f+2.1f,"瞄准允许贴地="+aimGroundContact+"；实际转角="+rotateAngle);
        Capture("里昂瞄准转身",player.transform);combat.HandleAim(false);
    }

    private void Capture(string name, Transform mainBody)
    {
        var camera = new GameObject("贴地取证相机").AddComponent<Camera>();
        camera.CopyFrom(player.playerCamera); camera.enabled = false; camera.nearClipPlane = .8f; camera.fieldOfView = 55;
        var center = mainBody.position + Vector3.up * .55f;
        camera.transform.position = center + new Vector3(-1.6f,.1f,.4f); camera.transform.LookAt(center);
        var drawCloth = new RenderTexture(640,480,24); var image = new Texture2D(640,480,TextureFormat.RGB24,false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = drawCloth; camera.Render(); RenderTexture.active = drawCloth;
            image.ReadPixels(new Rect(0,0,640,480),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(outputDirectory, name + ".jpg"), image.EncodeToJPG(90));
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Destroy(camera.gameObject); Destroy(image); drawCloth.Release(); Destroy(drawCloth); }
    }
}
#endif
