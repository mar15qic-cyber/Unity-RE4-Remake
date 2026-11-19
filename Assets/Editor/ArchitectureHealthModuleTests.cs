using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构生命模块测试")]

public sealed class ArchitectureHealthModuleTests
{
    private GameObject objectValue;
    private CharacterHealthModule health;

    [SetUp]
    public void CreateHealthObject()
    {
        objectValue = new GameObject("生命归属验证");
        health = objectValue.AddComponent<CharacterHealthModule>();
    }
    [TearDown]
    public void ReleaseHealthObject() => UnityEngine.Object.DestroyImmediate(objectValue);

    // 编辑器程序集通过反射验证内部兼容适配器，不为测试扩大运行接口。
    private object InvokeValue(string method, params object[] parameter)
    {
        try { return typeof(CharacterHealthModule).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, parameter); }
        catch (TargetInvocationException exception) { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }

    [Test]
    public void DeductHealthMinimumAndDeathFactOnlyByHealthModuleCommit()
    {
        InvokeValue("Initialize", 100f);
        InvokeValue("CommitCompatibleDamage", 30f);
        Assert.AreEqual(70f, health.CurrentHealth);
        Assert.IsFalse(health.HasDeath);
        Assert.AreEqual(70f, InvokeValue("CommitCompatibleDamage", 1000f));
        Assert.AreEqual(0f, health.CurrentHealth);
        Assert.IsTrue(health.HasDeath);
        Assert.AreEqual(0f, InvokeValue("CommitCompatibleDamage", 10f));
        InvokeValue("CommitCompatibleDamage", -50f);
        Assert.AreEqual(0f, health.CurrentHealth);
        Assert.IsFalse((bool)InvokeValue("ConfirmDeath"));
    }

    [Test]
    public void DirectDeathOnlyConfirmOnceAndNotCanPassInitializeRevive()
    {
        InvokeValue("Initialize", 100f);
        Assert.IsTrue((bool)InvokeValue("ConfirmDeath"));
        Assert.IsFalse((bool)InvokeValue("ConfirmDeath"));
        Assert.Throws<InvalidOperationException>(() => InvokeValue("Initialize", 100f));
        Assert.Throws<InvalidOperationException>(() => InvokeValue("SetAliveCharacterValidateHealth", 100f));
        Assert.IsTrue(health.HasDeath);
    }

    [Test]
    public void DiagnosticsSetAlsoElapsedUniqueOwner()
    {
        InvokeValue("Initialize", 100f);
        InvokeValue("SetAliveCharacterValidateHealth", 1000000f);
        Assert.AreEqual(1000000f, health.CurrentHealth);
        Assert.Throws<ArgumentException>(() => InvokeValue("SetAliveCharacterValidateHealth", float.NaN));
        Assert.AreEqual(1000000f, health.CurrentHealth);
    }

    [Test]
    public void UninitializedNotAcceptDamageAndAliveTimePreservePreviousNegativeValueSemantics()
    {
        Assert.IsFalse(health.Ready);
        Assert.Throws<InvalidOperationException>(() => InvokeValue("CommitCompatibleDamage", 1f));
        InvokeValue("Initialize", 100f);
        InvokeValue("CommitCompatibleDamage", -10f);
        Assert.AreEqual(110f, health.CurrentHealth);
    }

    [Test]
    public void PreviousHealthEntryPointAndMotionContextAllAsReadOnlyProjection()
    {
        Assert.IsNull(typeof(VillagerController).GetField("currentHealth"));
        Assert.IsFalse(typeof(VillagerController).GetProperty("currentHealth").CanWrite);
        Assert.IsNull(typeof(EnemyContext).GetField("CurrentHealth"));
        Assert.IsFalse(typeof(EnemyContext).GetProperty("CurrentHealth").CanWrite);
        var failure = new List<string>();
        ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(ArchitectureBoundaryValidator.Scan(), typeof(CharacterHealthModule),
            new[] { "currentHealthValue", "deathFact", "initialized" }, failure);
        Assert.IsEmpty(failure);
    }

    [Test]
    public void DyingDamageTerminatesWithoutPublishingAnIntermediateDeath()
    {
        InvokeValue("Initialize", 100f);
        Assert.IsTrue((bool)InvokeValue("TryBeginDying", 120f, 40f, 3d));
        Assert.IsFalse(health.HasDeath);
        Assert.AreEqual(GameArchitecture.EnemyLifePhase.Dying, health.LifePhase);
        long revision = health.LifeRevision;
        Assert.AreEqual(15f, InvokeValue("CommitCompatibleDamage", 15f));
        Assert.AreEqual(25f, health.CurrentHealth);
        Assert.AreEqual(25f, InvokeValue("CommitCompatibleDamage", 100f));
        Assert.IsTrue(health.HasDeath);
        Assert.AreEqual(GameArchitecture.EnemyLifePhase.Dead, health.LifePhase);
        Assert.IsFalse((bool)InvokeValue("TryBeginReviving", revision, 4d));
        Assert.IsFalse((bool)InvokeValue("ConfirmDeath"));
        Assert.AreEqual(0f, InvokeValue("CommitCompatibleDamage", 100f));
    }

    [Test]
    public void RevivedLifeCannotEnterASecondDyingCycle()
    {
        InvokeValue("Initialize", 100f);
        Assert.IsTrue((bool)InvokeValue("TryBeginDying", 100f, 40f, 3d));
        long revision = health.LifeRevision;
        Assert.IsFalse((bool)InvokeValue("TryBeginReviving", revision, 2d));
        Assert.IsTrue((bool)InvokeValue("TryBeginReviving", revision, 3d));
        Assert.IsTrue((bool)InvokeValue("CompleteRevival", revision, 165f));
        Assert.AreEqual(165f, health.CurrentHealth);
        Assert.IsFalse((bool)InvokeValue("TryBeginDying", 200f, 40f, 6d));
        InvokeValue("CommitCompatibleDamage", 200f);
        Assert.AreEqual(GameArchitecture.EnemyLifePhase.Dead, health.LifePhase);
        Assert.IsFalse((bool)InvokeValue("CompleteRevival", revision, 165f));
    }

    [Test]
    public void DisabledLeaseRejectsOldRevivalCallbacks()
    {
        InvokeValue("Initialize", 100f);
        Assert.IsTrue((bool)InvokeValue("TryBeginDying", 100f, 40f, double.MaxValue));
        long revision = health.LifeRevision;
        // 编辑模式不调度此运行时组件的 OnDisable，显式执行与退场相同的回调。
        InvokeValue("OnDisable");
        Assert.IsFalse((bool)InvokeValue("ScheduleRevivalWait", revision, 3d));
        Assert.IsFalse((bool)InvokeValue("TryBeginReviving", revision, 4d));
        Assert.AreEqual(GameArchitecture.EnemyLifePhase.Dying, health.LifePhase);
        Assert.AreEqual(40f, health.CurrentHealth);
    }
}
