using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构敌人模块测试")]

public sealed class ArchitectureEnemyModuleTests
{
    private GameObject objectValue;
    private VillagerController character;

    [SetUp]
    public void PrepareCharacter()
    {
        objectValue = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemies/FemaleGanardEnemy.prefab"));
        character = objectValue.GetComponent<VillagerController>();
        // 编辑器测试不触发普通运行组件的启用回调，显式推进生命周期。
        InvokeValue(character.actionCoordinator, "RestoreRequest");
        InvokeValue(character.healthModule, "Initialize", character.maxHealth);
        typeof(VillagerController).GetField("ctx", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(character, new EnemyContext(character.healthModule, character.combatModule));
        typeof(VillagerController).GetField("agent", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(character, objectValue.GetComponent<PathAgent>());
        typeof(VillagerController).GetField("anim", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(character, objectValue.GetComponent<Animator>());
        InvokeValue(character.actionCoordinator, "InitializeAction");
    }

    [TearDown]
    public void CleanupCharacter() { UnityEngine.Object.DestroyImmediate(objectValue); }

    private static object InvokeValue(object target, string method, params object[] parameter)
        => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, parameter);

    [Test]
    public void StateOwnershipAndAnimationWriteBoundaryWithoutUnauthorized()
    {
        var failure = new List<string>();
        EnemyModuleBoundaryValidator.ValidateCode(failure);
        ArchitectureBoundaryValidator.ValidateNewContract(failure);
        Assert.IsEmpty(failure, string.Join("\n", failure));
        foreach (string field in new[] { "pendingHit", "lastAttackTime", "accumulatedPoiseDamage", "awareness", "lastSeenTime", "observableState", "lastVoiceTime" })
            Assert.IsNull(typeof(VillagerController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance), field);
    }

    [TestCase("Assets/Prefab/Villager.prefab")]
    [TestCase("Assets/Prefab/Enemies/FemaleGanardEnemy.prefab")]
    [TestCase("Assets/Prefab/Enemies/FemaleGanard2Enemy.prefab")]
    [TestCase("Assets/Prefab/Enemies/ChainsawManEnemy.prefab")]
    [TestCase("Assets/Prefab/Enemies/SawManEnemy.prefab")]
    public void ProductionAssetCompleteBindingAndMigrationIdempotent(string path)
    {
        var failure = new List<string>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var component in prefab.GetComponentsInChildren<VillagerController>(true))
            EnemyModuleBoundaryValidator.ValidateReference(component, path, failure);
        Assert.IsEmpty(failure);
        string[] type = { "EnemyCombatModule", "EnemyPerceptionModule", "EnemyActionCoordinator", "EnemyAnimationAdapter", "CharacterMotionExecutor" };
        string[] field = { "combatModule", "perceptionModule", "actionCoordinator", "animationPresenter", "motionExecutor" };
        for (int i = 0; i < type.Length; i++)
        {
            var plan = new ArchitectureAssetMigrationTool.MigrationPlan { version = 2, assetPath = path,
                hostType = "VillagerController", newComponentType = type[i], referenceField = field[i] };
            Assert.AreEqual(0, ArchitectureAssetMigrationTool.Preview(plan).objectValue.Count);
        }
    }

    [Test]
    public void PreviousActionExitCannotCompleteNewAction()
    {
        InvokeValue(character.actionCoordinator, "RegisterAction");
        var previousToken = character.actionCoordinator.CurrentHandle;
        InvokeValue(character.actionCoordinator, "RegisterAction");
        Assert.AreNotEqual(previousToken, character.actionCoordinator.CurrentHandle);
        bool originalAttackComplete = character.AttackFinished;
        Assert.IsFalse(character.actionCoordinator.NotifyAttackStateMachineExited(previousToken));
        Assert.AreEqual(originalAttackComplete, character.AttackFinished);
        Assert.IsFalse(character.actionCoordinator.ReceiveAnimationNotification(previousToken, "OnAttackHit"));
    }

    [Test]
    public void DeactivateAgainIterationEnableRejectPreviousLeaseCallback()
    {
        InvokeValue(character.actionCoordinator, "RegisterAction");
        var originalToken = character.actionCoordinator.CurrentHandle;
        character.actionCoordinator.enabled = false;
        InvokeValue(character.actionCoordinator, "CloseAction");
        character.actionCoordinator.enabled = true;
        InvokeValue(character.actionCoordinator, "RestoreRequest");
        InvokeValue(character.actionCoordinator, "RegisterAction");
        Assert.AreNotEqual(originalToken.Object, character.actionCoordinator.CurrentHandle.Object);
        Assert.IsFalse(character.actionCoordinator.NotifyHitStateMachineExited(originalToken));
    }

    [Test]
    public void DeathRejectActionAndCurrentAnimationCallback()
    {
        InvokeValue(character.actionCoordinator, "RegisterAction");
        var token = character.actionCoordinator.CurrentHandle;
        InvokeValue(character.healthModule, "ConfirmDeath");
        Assert.IsFalse(character.actionCoordinator.CallbackValid(token));
        Assert.AreEqual(RejectionReason.HasDeath, character.actionCoordinator.RequestAction(ActionKind.Attack, "规则验证").Reason);
    }

    [Test]
    public void PerceptionQueryNotAdvanceSampleAndAlertState()
    {
        var field = typeof(EnemyPerceptionModule).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        var originalValue = new object[field.Length];
        for (int i = 0; i < field.Length; i++) originalValue[i] = field[i].GetValue(character.perceptionModule);
        for (int count = 0; count < 10; count++) { character.CanSeePlayer(); var snapshot = character.StateSnapshot; }
        for (int i = 0; i < field.Length; i++) Assert.AreEqual(originalValue[i], field[i].GetValue(character.perceptionModule), field[i].Name);
    }
}
