using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 针对本轮修复项（散弹枪下瞄无滚转修正、背后环抱 RE4R 规则、背抱释放无敌与电锯人单膝跪地补刀条件）的 EditMode 单元测试。
/// </summary>
public sealed class CameraAndQTEAlignmentTests
{
    private static readonly BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void W870AimDecomposition_HasZeroRollAndTracksSteepDownPitch()
    {
        MethodInfo decomposeMethod = typeof(LeonAnimRig).GetMethod(
            "ComputeAimCorrectionWithoutRoll",
            StaticNonPublic);
        Assert.IsNotNull(decomposeMethod, "LeonAnimRig 缺少 ComputeAimCorrectionWithoutRoll 方法。");

        Vector3[] initialDirs =
        {
            Vector3.forward,
            new Vector3(0.25f, 0.05f, 0.96f).normalized,
            new Vector3(-0.35f, -0.10f, 0.93f).normalized
        };

        float[] yawOffsets = { -18f, 0f, 15f, 32f };
        float[] steepPitches = { -55f, -65f };

        foreach (Vector3 currentDir in initialDirs)
        {
            Vector3 flatCurrent = Vector3.ProjectOnPlane(currentDir, Vector3.up).normalized;
            Vector3 currentRight = Vector3.Cross(Vector3.up, flatCurrent).normalized;

            foreach (float yaw in yawOffsets)
            {
                foreach (float pitch in steepPitches)
                {
                    Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
                    Vector3 targetDir = (targetRot * Vector3.forward).normalized;

                    object[] args = { currentDir, targetDir, currentRight, Vector3.zero, 0f };
                    Quaternion correction = (Quaternion)decomposeMethod.Invoke(null, args);
                    Vector3 pitchAxis = (Vector3)args[3];
                    float purePitchAngle = (float)args[4];

                    Assert.That(Mathf.Abs(pitchAxis.y), Is.LessThan(0.001f),
                        $"俯仰轴不应包含垂直分量 yaw={yaw}, pitch={pitch}");
                    Assert.That(purePitchAngle, Is.LessThan(-40f),
                        $"向下瞄准纯俯仰角应为负陡角 yaw={yaw}, pitch={pitch}");

                    Vector3 rotatedRight = (correction * currentRight).normalized;
                    Assert.That(Mathf.Abs(Vector3.Dot(rotatedRight, Vector3.up)), Is.LessThan(0.001f),
                        $"修正后水平右向不应产生寄生滚转 yaw={yaw}, pitch={pitch}");

                    float residualAngle = Vector3.Angle(correction * currentDir, targetDir);
                    Assert.That(residualAngle, Is.LessThan(0.5f),
                        $"修正旋转应精准对准目标方向 yaw={yaw}, pitch={pitch}");
                }
            }
        }
    }

    [Test]
    public void RearGrabGeometry_Enforces65DegreeRearSectorFacingAndCrouchAvoidance()
    {
        MethodInfo geometryMethod = typeof(VillagerQTEParticipant).GetMethod(
            "IsValidRearGrabGeometry",
            StaticNonPublic);
        Assert.IsNotNull(geometryMethod, "VillagerQTEParticipant 缺少 IsValidRearGrabGeometry 方法。");

        Vector3 playerPos = Vector3.zero;
        Vector3 playerForward = Vector3.forward;

        // 正后方（0°），敌人面向玩家。
        Vector3 rearZeroPos = new Vector3(0f, 0f, -1.2f);
        Assert.IsTrue(InvokeRearGeometry(geometryMethod, rearZeroPos, Vector3.forward, playerPos, playerForward, false));

        // 背后 ±65° 扇区内边界（60°），敌人面向玩家。
        Vector3 rear60Dir = Quaternion.Euler(0f, 60f, 0f) * Vector3.back;
        Assert.IsTrue(InvokeRearGeometry(geometryMethod, rear60Dir * 1.2f, -rear60Dir, playerPos, playerForward, false));

        // 超出背后 ±65° 扇区（75° 偏侧）。
        Vector3 rear75Dir = Quaternion.Euler(0f, 75f, 0f) * Vector3.back;
        Assert.IsFalse(InvokeRearGeometry(geometryMethod, rear75Dir * 1.2f, -rear75Dir, playerPos, playerForward, false));

        // 纯侧面与正面均不可背后抓。
        Assert.IsFalse(InvokeRearGeometry(geometryMethod, Vector3.right * 1.2f, Vector3.left, playerPos, playerForward, false));
        Assert.IsFalse(InvokeRearGeometry(geometryMethod, Vector3.forward * 1.2f, Vector3.back, playerPos, playerForward, false));

        // 敌人在正后方但背对玩家，不可抓。
        Assert.IsFalse(InvokeRearGeometry(geometryMethod, rearZeroPos, Vector3.back, playerPos, playerForward, false));

        // 玩家蹲伏可规避背后抓。
        Assert.IsFalse(InvokeRearGeometry(geometryMethod, rearZeroPos, Vector3.forward, playerPos, playerForward, true));
    }

    [Test]
    public void RearGrabEligibility_RejectsArmedGanadoAndAcceptsUnarmedGanado()
    {
        MethodInfo canUseRearMethod = typeof(VillagerQTEParticipant).GetMethod(
            "CanUseRearBiteNeckQTE",
            InstanceNonPublic);
        Assert.IsNotNull(canUseRearMethod, "VillagerQTEParticipant 缺少 CanUseRearBiteNeckQTE 方法。");

        MethodInfo configureWeaponMethod = typeof(EnemyHeldWeapon).GetMethod(
            "Configure",
            InstanceNonPublic);
        Assert.IsNotNull(configureWeaponMethod, "EnemyHeldWeapon 缺少 Configure 方法。");

        GameObject unarmedObj = new GameObject("TestUnarmedGanado");
        GameObject armedObj = new GameObject("TestArmedGanado");
        GameObject sawObj = new GameObject("TestSawman");
        try
        {
            VillagerController unarmedCtrl = unarmedObj.AddComponent<VillagerController>();
            unarmedCtrl.missionEnemyType = MissionEnemyType.Ganado;
            VillagerQTEParticipant unarmedQte = unarmedObj.AddComponent<VillagerQTEParticipant>();
            unarmedQte.controller = unarmedCtrl;
            Assert.IsTrue((bool)canUseRearMethod.Invoke(unarmedQte, null),
                "徒手普通村民应具备背后环抱资格。");

            VillagerController armedCtrl = armedObj.AddComponent<VillagerController>();
            armedCtrl.missionEnemyType = MissionEnemyType.Ganado;
            VillagerQTEParticipant armedQte = armedObj.AddComponent<VillagerQTEParticipant>();
            armedQte.controller = armedCtrl;
            EnemyHeldWeapon heldWeapon = armedObj.AddComponent<EnemyHeldWeapon>();
            BoxCollider weaponBox = armedObj.AddComponent<BoxCollider>();
            configureWeaponMethod.Invoke(heldWeapon, new object[] { weaponBox, null, null });
            Assert.IsFalse((bool)canUseRearMethod.Invoke(armedQte, null),
                "持械村民应禁止触发背后环抱。");

            VillagerController sawCtrl = sawObj.AddComponent<VillagerController>();
            sawCtrl.missionEnemyType = MissionEnemyType.Sawman;
            VillagerQTEParticipant sawQte = sawObj.AddComponent<VillagerQTEParticipant>();
            sawQte.controller = sawCtrl;
            Assert.IsFalse((bool)canUseRearMethod.Invoke(sawQte, null),
                "电锯人应禁止触发背后环抱。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unarmedObj);
            UnityEngine.Object.DestroyImmediate(armedObj);
            UnityEngine.Object.DestroyImmediate(sawObj);
        }

        GameObject armedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/RE4ChapterOneAnimationLoop/Generated/MainGanadoArmed.prefab");
        Assert.IsNotNull(armedPrefab, "未找到正式持械村民预制件 MainGanadoArmed.prefab。");
        VillagerQTEParticipant prefabQte = armedPrefab.GetComponent<VillagerQTEParticipant>();
        Assert.IsNotNull(prefabQte);
        SerializedObject serializedPrefabQte = new SerializedObject(prefabQte);
        SerializedProperty rearProbProp = serializedPrefabQte.FindProperty("rearGrabProbability");
        Assert.IsNotNull(rearProbProp);
        Assert.That(rearProbProp.floatValue, Is.EqualTo(0f),
            "持械村民预制件 rearGrabProbability 必须为 0。");
    }

    [Test]
    public void LeonQTEReleaseInvulnerability_BlocksOrdinaryDamageDuringWindow()
    {
        MethodInfo grantMethod = typeof(LeonController).GetMethod(
            "GrantQTEReleaseInvulnerability",
            InstanceNonPublic);
        PropertyInfo invulnProp = typeof(LeonController).GetProperty(
            "IsQTEReleaseInvulnerable",
            InstanceNonPublic);
        Assert.IsNotNull(grantMethod, "LeonController 缺少 GrantQTEReleaseInvulnerability 方法。");
        Assert.IsNotNull(invulnProp, "LeonController 缺少 IsQTEReleaseInvulnerable 属性。");

        GameObject leonObj = new GameObject("TestLeonReleaseInvuln");
        try
        {
            HealthComponent health = leonObj.AddComponent<HealthComponent>();
            health.Initialize(100f);

            LeonController leon = leonObj.AddComponent<LeonController>();
            PropertyInfo healthProp = typeof(LeonController).GetProperty(
                "Health",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(healthProp);
            healthProp.SetValue(leon, health);

            grantMethod.Invoke(leon, new object[] { 0.45f });
            Assert.IsTrue((bool)invulnProp.GetValue(leon), "授予释放无敌窗口后应处于无敌状态。");

            leon.TakeDamageFrom(25f, Vector3.back, Vector3.zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f),
                "背后环抱释放无敌窗口内的普通伤害应被拦截，不能扣血。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(leonObj);
        }
    }

    [Test]
    public void SawmanGroundFinisher_RequiresKneelStateAndRejectsStandingStagger()
    {
        FieldInfo leftDirField = typeof(EnemyCombatModule).GetField(
            "NativeLeftKneelHitDirection",
            StaticNonPublic);
        FieldInfo rightDirField = typeof(EnemyCombatModule).GetField(
            "NativeRightKneelHitDirection",
            StaticNonPublic);
        Assert.IsNotNull(leftDirField);
        Assert.IsNotNull(rightDirField);

        int leftHitDir = Convert.ToInt32(leftDirField.GetRawConstantValue());
        int rightHitDir = Convert.ToInt32(rightDirField.GetRawConstantValue());

        AnimatorController graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/RE4ChapterOneAnimationLoop/Generated/MainSalvador.controller");
        Assert.IsNotNull(graph, "找不到 MainSalvador.controller。");

        AnimatorStateMachine root = graph.layers[0].stateMachine;
        AnimatorStateMachine hitReact = null;
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
        {
            if (child.stateMachine != null && child.stateMachine.name == "HitReact")
            {
                hitReact = child.stateMachine;
                break;
            }
        }
        Assert.IsNotNull(hitReact, "MainSalvador.controller 缺少 HitReact 子状态机。");

        AssertKneelHitStateBound(hitReact, "Hit_NativeKneelLeft", leftHitDir);
        AssertKneelHitStateBound(hitReact, "Hit_NativeKneelRight", rightHitDir);

        FieldInfo kneelDamageField = typeof(EnemyCombatModule).GetField("sawmanKneelDamage", InstanceNonPublic);
        Assert.IsNotNull(kneelDamageField, "EnemyCombatModule 应包含电锯人部位加权跪地累计字段 sawmanKneelDamage。");
    }

    [Test]
    public void TerrainStepCooldown_BlocksConsecutiveHorizontalBoosts()
    {
        MethodInfo tryCommitRise = typeof(LeonActionMotionDriver).GetMethod(
            "TryCommitTerrainRise",
            InstanceNonPublic);
        FieldInfo cooldownField = typeof(LeonActionMotionDriver).GetField(
            "nextTerrainStepTime",
            InstanceNonPublic);
        Assert.IsNotNull(tryCommitRise, "LeonActionMotionDriver 缺少 TryCommitTerrainRise 方法。");
        Assert.IsNotNull(cooldownField, "LeonActionMotionDriver 缺少 nextTerrainStepTime 节流字段。");

        GameObject driverObj = new GameObject("TestMotionDriverCooldown");
        try
        {
            LeonActionMotionDriver driver = driverObj.AddComponent<LeonActionMotionDriver>();
            cooldownField.SetValue(driver, Time.fixedTime + 0.12f);

            Bounds fakeBounds = new Bounds(Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));
            bool blockedResult = (bool)tryCommitRise.Invoke(driver, new object[]
            {
                Vector3.forward,
                fakeBounds,
                0.3f,
                0.1f,
                true
            });
            Assert.IsFalse(blockedResult, "处于 0.12s 抬脚冷却期内时不应连续触发 TryCommitTerrainRise 水平前推。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(driverObj);
        }
    }

    [Test]
    public void AssassinationGeometry_CoversRearFlankAndAppliesTurnHysteresis()
    {
        MethodInfo evalMethod = typeof(LeonQTECoordinator).GetMethod(
            "EvaluateAssassinationGeometry",
            StaticNonPublic);
        Assert.IsNotNull(evalMethod, "LeonQTECoordinator 缺少 EvaluateAssassinationGeometry 方法。");

        Vector3 enemyPos = Vector3.zero;
        Vector3 enemyForward = Vector3.forward;

        // 正背后 0°：命中潜行与硬直基础区。
        Assert.IsTrue(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            Vector3.back * 1.4f, Vector3.forward, 2.25f, 0.35f, 85f, 115f, false, 20f, 0.25f, out bool baseMatch0));
        Assert.IsTrue(baseMatch0);

        // 后斜侧 75°：命中潜行基础区（85°以内）与玩家面对目标后侧 115° 内。
        Vector3 flank75Dir = Quaternion.Euler(0f, 75f, 0f) * Vector3.back;
        Assert.IsTrue(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            flank75Dir * 1.5f, -flank75Dir, 2.25f, 0.35f, 85f, 115f, false, 20f, 0.25f, out bool baseMatch75));
        Assert.IsTrue(baseMatch75);

        // 站立硬直侧后方 92°：命中硬直基础区（95°以内），不命中无滞回潜行 85° 基础区。
        Vector3 flank92Dir = Quaternion.Euler(0f, 92f, 0f) * Vector3.back;
        Assert.IsTrue(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            flank92Dir * 1.5f, -flank92Dir, 2.25f, 0.35f, 95f, 115f, false, 20f, 0.25f, out _));
        Assert.IsFalse(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            flank92Dir * 1.5f, -flank92Dir, 2.25f, 0.35f, 85f, 115f, false, 20f, 0.25f, out _));

        // 敌人轻微转身导致夹角扩大至 105°、距离 2.40m：无滞回锁存拒绝，有滞回锁存仍通过。
        Vector3 flank105Dir = Quaternion.Euler(0f, 105f, 0f) * Vector3.back;
        Assert.IsFalse(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            flank105Dir * 2.40f, -flank105Dir, 2.25f, 0.35f, 95f, 115f, false, 20f, 0.25f, out _));
        Assert.IsTrue(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            flank105Dir * 2.40f, -flank105Dir, 2.25f, 0.35f, 95f, 115f, true, 20f, 0.25f, out bool baseMatch105));
        Assert.IsFalse(baseMatch105, "滞回扩展区命中不应伪装成基础区命中。");

        // 敌人完全转向面对玩家（后方角 135° / 正面角 45° < 65°）：即使有滞回锁存也必须拒绝。
        Vector3 front45Dir = Quaternion.Euler(0f, 135f, 0f) * Vector3.back;
        Assert.IsFalse(InvokeAssassinationGeometry(evalMethod, enemyPos, enemyForward,
            front45Dir * 1.5f, -front45Dir, 2.25f, 0.35f, 95f, 115f, true, 20f, 0.25f, out _));
    }

    [Test]
    public void StandingStaggerPartition_SplitsFrontBodyTechniqueAndRearBackstabWithNoKnifeFallback()
    {
        MethodInfo resolveMaxAngleMethod = typeof(VillagerQTEParticipant).GetMethod(
            "ResolveBodyTechniqueMaxEnemyFacingAngle",
            StaticNonPublic);
        Assert.IsNotNull(resolveMaxAngleMethod, "VillagerQTEParticipant 缺少 ResolveBodyTechniqueMaxEnemyFacingAngle 方法。");

        float withKnifeMaxAngle = (float)resolveMaxAngleMethod.Invoke(null, new object[] { 165f, true, true, 95f });
        Assert.That(withKnifeMaxAngle, Is.EqualTo(85f).Within(0.01f),
            "站立硬直背刺可用且持有小刀时，正面体术上限应收束为 85°，与背后 ±95° 无死区衔接。");

        float noKnifeFallbackAngle = (float)resolveMaxAngleMethod.Invoke(null, new object[] { 165f, true, false, 95f });
        Assert.That(noKnifeFallbackAngle, Is.EqualTo(165f).Within(0.01f),
            "玩家无可用小刀时，应保留 165° 体术范围避免背后失去动作响应。");

        float kneelOrNonBackstabAngle = (float)resolveMaxAngleMethod.Invoke(null, new object[] { 165f, false, true, 95f });
        Assert.That(kneelOrNonBackstabAngle, Is.EqualTo(165f).Within(0.01f),
            "非站立背刺窗口不应收束体术角度。");
    }

    [Test]
    public void StealthAssassinationEligibility_UsesPersonalDetectionInsteadOfGlobalCombat()
    {
        GameObject enemyObj = new GameObject("TestStealthEnemy");
        GameObject leonObj = new GameObject("TestStealthLeon");
        try
        {
            VillagerController villager = enemyObj.AddComponent<VillagerController>();
            CharacterHealthModule health = enemyObj.AddComponent<CharacterHealthModule>();
            villager.healthModule = health;
            MethodInfo healthInit = typeof(CharacterHealthModule).GetMethod("Initialize", InstanceNonPublic);
            Assert.IsNotNull(healthInit, "CharacterHealthModule 缺少 Initialize 方法。");
            healthInit.Invoke(health, new object[] { 100f });
            villager.IsActive = true;

            EnemyPerceptionModule perception = enemyObj.AddComponent<EnemyPerceptionModule>();
            perception.host = villager;
            villager.perceptionModule = perception;

            FieldInfo combatEngagedField = typeof(EnemyPerceptionModule).GetField("combatEngaged", InstanceNonPublic);
            FieldInfo fullyAwareField = typeof(EnemyPerceptionModule).GetField("fullyAwareOfPlayer", InstanceNonPublic);
            Assert.IsNotNull(combatEngagedField);
            Assert.IsNotNull(fullyAwareField);

            // 即使全局接战为 true，只要该个体从未个人发现玩家，仍可潜行暗杀。
            combatEngagedField.SetValue(perception, true);
            fullyAwareField.SetValue(perception, true);

            LeonController leon = leonObj.AddComponent<LeonController>();
            MethodInfo stealthMethod = typeof(VillagerController).GetMethod("CanBeStealthAssassinatedBy", InstanceNonPublic);
            Assert.IsNotNull(stealthMethod, "VillagerController 缺少 CanBeStealthAssassinatedBy 方法。");

            Assert.IsTrue((bool)stealthMethod.Invoke(villager, new object[] { leon }),
                "个体未发现玩家时不应因全局 combatEngaged 被拦截。");

            MethodInfo markDetectedMethod = typeof(EnemyPerceptionModule).GetMethod("MarkPersonallyDetectedPlayer", InstanceNonPublic);
            Assert.IsNotNull(markDetectedMethod);
            markDetectedMethod.Invoke(perception, null);

            Assert.IsFalse((bool)stealthMethod.Invoke(villager, new object[] { leon }),
                "一旦个体个人发现玩家，必须立即失去潜行暗杀资格。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyObj);
            UnityEngine.Object.DestroyImmediate(leonObj);
        }
    }

    [Test]
    public void SawmanBackstabAssets_ContainSalvador1101And1105Tracks()
    {
        ScriptableObject sawBank = Resources.Load<ScriptableObject>("SawKnifeFollowUp");
        ScriptableObject mainBank = Resources.Load<ScriptableObject>("ChapterOnePairMotion");
        Assert.IsNotNull(sawBank, "找不到 SawKnifeFollowUp.asset。");
        Assert.IsNotNull(mainBank, "找不到 ChapterOnePairMotion.asset。");

        MethodInfo tryGetVillagerTrack = sawBank.GetType().GetMethod("TryGetVillagerTrack", InstanceNonPublic);
        Assert.IsNotNull(tryGetVillagerTrack);

        object[] s1101Args = { "Salvador", "SalvadorPair_1101", null };
        bool hasS1101 = (bool)tryGetVillagerTrack.Invoke(sawBank, s1101Args);
        Assert.IsTrue(hasS1101, "SawKnifeFollowUp.asset 缺少 SalvadorPair_1101 轨道。");

        object[] s1105Args = { "Salvador", "SalvadorPair_1105", null };
        bool hasS1105 = (bool)tryGetVillagerTrack.Invoke(sawBank, s1105Args);
        Assert.IsTrue(hasS1105, "SawKnifeFollowUp.asset 缺少 SalvadorPair_1105 轨道。");

        object[] l1101Args = { "Leon", "LeonPair_1101", null };
        Assert.IsTrue((bool)tryGetVillagerTrack.Invoke(mainBank, l1101Args), "ChapterOnePairMotion 缺少 LeonPair_1101。");

        string[] configPaths =
        {
            "Assets/Settings/QTE/里昂正面抓取配置.asset",
            "Assets/RE4ChapterOneAnimationLoop/Generated/MainLeonQTE.asset"
        };
        for (int i = 0; i < configPaths.Length; i++)
        {
            LeonQTEConfig config = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(configPaths[i]);
            Assert.IsNotNull(config, $"缺少配置资产：{configPaths[i]}");
            Assert.That(config.assassinationRearAngle, Is.GreaterThanOrEqualTo(80f),
                $"配置资产 {configPaths[i]} 的 assassinationRearAngle 应覆盖背后与左右后斜侧面。");
            Assert.That(config.staggerBackstabRearAngle, Is.GreaterThanOrEqualTo(90f),
                $"配置资产 {configPaths[i]} 的 staggerBackstabRearAngle 应与正面体术形成无缝分区。");
        }
    }

    private static bool InvokeAssassinationGeometry(MethodInfo method, Vector3 enemyPos, Vector3 enemyForward,
        Vector3 playerPos, Vector3 playerForward, float baseMaxDistance, float maxHeight, float baseRearAngle,
        float maxPlayerFacingAngle, bool hasTurnLatch, float graceAngleBonus, float graceDistanceBonus,
        out bool matchedBaseZone)
    {
        object[] args =
        {
            enemyPos,
            enemyForward,
            playerPos,
            playerForward,
            baseMaxDistance,
            maxHeight,
            baseRearAngle,
            maxPlayerFacingAngle,
            hasTurnLatch,
            graceAngleBonus,
            graceDistanceBonus,
            false
        };
        bool result = (bool)method.Invoke(null, args);
        matchedBaseZone = (bool)args[11];
        return result;
    }

    private static bool InvokeRearGeometry(MethodInfo method, Vector3 enemyPos, Vector3 enemyForward,
        Vector3 playerPos, Vector3 playerForward, bool playerIsCrouching)
    {
        return (bool)method.Invoke(null, new object[]
        {
            enemyPos,
            enemyForward,
            playerPos,
            playerForward,
            playerIsCrouching
        });
    }

    private static void AssertKneelHitStateBound(AnimatorStateMachine hitReact, string stateName, int expectedHitDir)
    {
        AnimatorState state = null;
        foreach (ChildAnimatorState child in hitReact.states)
        {
            if (child.state != null && child.state.name == stateName)
            {
                state = child.state;
                break;
            }
        }

        Assert.IsNotNull(state, $"HitReact 缺少电锯人单膝跪地状态 {stateName}。");
        Assert.IsNotNull(state.motion, $"电锯人单膝跪地状态 {stateName} 必须绑定有效动画切片。");

        bool hasTimedSmb = false;
        foreach (StateMachineBehaviour smb in state.behaviours)
        {
            if (smb is EnemyTimedHitStateSMB)
            {
                hasTimedSmb = true;
                break;
            }
        }
        Assert.IsTrue(hasTimedSmb, $"电锯人单膝跪地状态 {stateName} 缺少 EnemyTimedHitStateSMB。");

        bool hasEntryTransition = false;
        foreach (AnimatorTransition entry in hitReact.entryTransitions)
        {
            if (entry.destinationState == state)
            {
                foreach (AnimatorCondition cond in entry.conditions)
                {
                    if (cond.parameter == "HitDir"
                        && cond.mode == AnimatorConditionMode.Equals
                        && Mathf.RoundToInt(cond.threshold) == expectedHitDir)
                    {
                        hasEntryTransition = true;
                        break;
                    }
                }
            }
        }
        Assert.IsTrue(hasEntryTransition,
            $"HitReact 缺少 HitDir == {expectedHitDir} 进入 {stateName} 的入口过渡。");
    }
}
