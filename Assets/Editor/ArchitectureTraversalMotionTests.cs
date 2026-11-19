using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构通路运动测试")]

public sealed class ArchitectureTraversalMotionTests
{
    private readonly List<GameObject> temporaryObject = new();
    private VillagerController CreateCharacter()
    {
        var solid = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemies/FemaleGanardEnemy.prefab"));
        temporaryObject.Add(solid);
        var character = solid.GetComponent<VillagerController>();
        InvokeValue(character.actionCoordinator, "RestoreRequest");
        InvokeValue(character.healthModule, "Initialize", character.maxHealth);
        typeof(VillagerController).GetField("ctx", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(character, new EnemyContext(character.healthModule, character.combatModule));
        InvokeValue(character.actionCoordinator, "InitializeAction");
        return character;
    }
    private T CreateTraversal<T>() where T : Component
    {
        var solid = new GameObject("通路验证"); temporaryObject.Add(solid);
        if (typeof(T) == typeof(DoorController)) solid.AddComponent<BoxCollider>();
        var component = solid.AddComponent<T>();
        InvokeValue(component, "OnEnable");
        return component;
    }
    private static object InvokeValue(object objectValue, string method, params object[] parameter)
        => objectValue.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(objectValue, parameter);
    private static object Read(object objectValue, string property)
        => objectValue.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(objectValue);
    [TearDown]
    public void Cleanup()
    { foreach (var objectValue in temporaryObject) if (objectValue != null) UnityEngine.Object.DestroyImmediate(objectValue); temporaryObject.Clear(); }

    [Test]
    public void MainLeonLandingPoseDoesNotRepeatTraversalDisplacement()
    {
        const string controllerPath = "Assets/RE4ChapterOneAnimationLoop/Generated/MainLeon.controller";
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        Assert.IsNotNull(controller);

        AnimationClip landing = null;
        foreach (var state in controller.layers[LeonAnimatorDriver.LayerFullBodyAction].stateMachine.states)
        {
            if (state.state.name == "WindowDropLand")
            {
                landing = state.state.motion as AnimationClip;
                break;
            }
        }

        Assert.IsNotNull(landing, "正式全身层必须绑定落地片段。");
        foreach (string axis in new[] { "x", "y", "z" })
        {
            var binding = EditorCurveBinding.FloatCurve("Armature/root ground", typeof(Transform), "m_LocalPosition." + axis);
            var curve = AnimationUtility.GetEditorCurve(landing, binding);
            Assert.IsNotNull(curve, "落地时需要明确把骨架根保持在角色原点。");
            foreach (Keyframe key in curve.keys)
            {
                Assert.That(key.value, Is.EqualTo(0f).Within(0.0001f), binding.propertyName);
                Assert.That(key.inTangent, Is.EqualTo(0f).Within(0.0001f), binding.propertyName);
                Assert.That(key.outTangent, Is.EqualTo(0f).Within(0.0001f), binding.propertyName);
            }
        }
    }

    [Test]
    public void MotionHandoffRejectPreviousActionAndOtherCharacterToken()
    {
        var character = CreateCharacter(); var other = CreateCharacter();
        character.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        var previous = character.motionExecutor.CurrentToken;
        Assert.IsTrue(character.motionExecutor.CommitPosition(previous, Vector3.one));
        character.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
        Assert.IsFalse(character.motionExecutor.CommitPosition(previous, Vector3.zero));
        Assert.IsFalse(character.motionExecutor.CommitPosition(other.motionExecutor.CurrentToken, Vector3.zero));
        var traversal = character.motionExecutor.CurrentToken;
        InvokeValue(character.actionCoordinator, "RegisterAction");
        Assert.IsFalse(character.motionExecutor.CommitPosition(traversal, Vector3.zero));
        Assert.AreEqual(Vector3.one, character.transform.position);
    }
    [Test]
    public void HealthConfirmAfterDeathWithoutRequiringWaitingBehaviorTreeAlsoRejectPreviousMotion()
    {
        var character = CreateCharacter();
        character.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
        var previous = character.motionExecutor.CurrentToken;
        InvokeValue(character.healthModule, "ConfirmDeath");
        Assert.IsTrue(character.IsDead);
        Assert.IsFalse(character.motionExecutor.CommitPosition(previous, Vector3.zero));
        character.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        Assert.IsFalse(character.motionExecutor.Valid(character.motionExecutor.CurrentToken));
    }
    [Test]
    public void DeactivateRecoveryBackPreviousMotionTokenCannotReuse()
    {
        var character = CreateCharacter(); var previous = character.motionExecutor.CurrentToken;
        InvokeValue(character.actionCoordinator, "CloseAction");
        InvokeValue(character.actionCoordinator, "RestoreRequest");
        InvokeValue(character.actionCoordinator, "InitializeAction");
        Assert.IsFalse(character.motionExecutor.Valid(previous));
        Assert.AreNotEqual(previous.Action.Object, character.motionExecutor.CurrentToken.Action.Object);
    }
    [Test]
    public void EntranceSupportRequiresAnActivePhysicalColliderInTheScene()
    {
        var route = CreateTraversal<HouseThreeTraversalGeometry>();
        var ground = new GameObject("门槛");
        temporaryObject.Add(ground);
        var collider = ground.AddComponent<BoxCollider>();
        Assert.IsFalse(route.HasEntranceSupport);
        route.nativeDoorThreshold = collider;
        Assert.IsTrue(route.HasEntranceSupport);
        collider.isTrigger = true;
        Assert.IsFalse(route.HasEntranceSupport);
        collider.isTrigger = false;
        ground.SetActive(false);
        Assert.IsFalse(route.HasEntranceSupport);
    }

    [Test]
    public void WindowPreviousReleaseCannotRevokeNewOccupancyAndDeactivateWillInvalidateToken()
    {
        var character = CreateCharacter(); var window = CreateTraversal<EnemyWindowTraversalRoute>();
        Assert.IsTrue(window.TryRequest(character, out var previous));
        Assert.IsTrue(window.ReleaseTraversal(previous, ReleaseReason.Complete));
        Assert.IsTrue(window.TryRequest(character, out var newValue));
        Assert.IsFalse(window.ReleaseTraversal(previous, ReleaseReason.Cancel));
        Assert.IsTrue(window.TraversalValid(newValue));
        InvokeValue(window, "OnDisable"); InvokeValue(window, "OnEnable");
        Assert.IsFalse(window.TraversalValid(newValue));
        Assert.IsTrue(window.TryRequest(character, out var weightOpen));
        Assert.AreNotEqual(newValue.Traversal, weightOpen.Traversal);
    }
    [Test]
    public void TwoLeafDoorEachPreserveQueueAndPreviousTokenCannotReleaseNewOccupancy()
    {
        var first = CreateCharacter(); var second = CreateCharacter();
        var doorFirst = CreateTraversal<DoorController>(); var doorSecond = CreateTraversal<DoorController>();
        var occupancyFirst = Read(doorFirst, "TraversalOccupancy"); var occupancySecond = Read(doorSecond, "TraversalOccupancy");
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(VillagerController).GetField("pursuitIngressDoor", flag).SetValue(first, doorFirst);
        typeof(VillagerController).GetField("pursuitIngressDoor", flag).SetValue(second, doorSecond);
        foreach (var character in new[] { first, second })
        {
            typeof(VillagerController).GetField("pursuitIngressSourceSide", flag).SetValue(character, 1);
            typeof(VillagerController).GetField("pursuitIngressCrossing", flag).SetValue(character, true);
        }
        InvokeValue(occupancyFirst, "Register", first, 1); InvokeValue(occupancySecond, "Register", second, 1);
        Assert.IsTrue((bool)InvokeValue(occupancyFirst, "TryOccupancy", first, 1));
        Assert.IsTrue((bool)InvokeValue(occupancySecond, "TryOccupancy", second, 1));
        var previous = (TraversalToken)InvokeValue(occupancyFirst, "QueryToken", first);
        var otherDoor = (TraversalToken)InvokeValue(occupancySecond, "QueryToken", second);
        Assert.IsTrue(doorFirst.ReleaseTraversal(previous, ReleaseReason.Complete));
        InvokeValue(occupancyFirst, "Register", first, 1); InvokeValue(occupancyFirst, "TryOccupancy", first, 1);
        Assert.IsFalse(doorFirst.ReleaseTraversal(previous, ReleaseReason.Deactivate));
        Assert.IsTrue(doorSecond.TraversalValid(otherDoor));
        InvokeValue(doorFirst, "OnDisable"); Assert.IsTrue(doorSecond.TraversalValid(otherDoor));
    }
    [Test]
    public void TraversalRequestValidateSourceObjectAndActionIdAndReturnRejectionReason()
    {
        var character = CreateCharacter(); var window = CreateTraversal<EnemyWindowTraversalRoute>();
        var action = character.actionCoordinator.CurrentHandle;
        var previousRequest = new RequestContext("旧动作测试", action.Object, window.TraversalIdentity, action.Id);
        InvokeValue(character.actionCoordinator, "RegisterAction");
        var previousResult = window.RequestTraversal(character, previousRequest);
        Assert.IsFalse(previousResult.HasAccept); Assert.AreEqual(RejectionReason.CallbackHasExpired, previousResult.Reason);
        var emptySource = new RequestContext("", character.actionCoordinator.Identity, window.TraversalIdentity, character.actionCoordinator.CurrentHandle.Id);
        Assert.AreEqual(RejectionReason.ParameterInvalid, window.RequestTraversal(character, emptySource).Reason);
        var newRequest = new RequestContext("当前动作测试", character.actionCoordinator.Identity, window.TraversalIdentity, character.actionCoordinator.CurrentHandle.Id);
        Assert.IsTrue(window.RequestTraversal(character, newRequest).HasAccept);
        InvokeValue(character.healthModule, "ConfirmDeath");
        Assert.AreEqual(RejectionReason.HasDeath, window.RequestTraversal(character, newRequest).Reason);
    }
    [Test]
    public void TraversalTokenDistinguishPhysicsTraversalAndObjectLeaseVersion()
    {
        var first = new TraversalTokenRegistry(); var second = new TraversalTokenRegistry(); first.OpenRelease(1); second.OpenRelease(2);
        var action = new ActionHandle(new ObjectIdentity(3, 1), 1);
        var token = first.Register(action);
        Assert.IsFalse(second.Release(token, ReleaseReason.Cancel));
        var newLease = first.Register(new ActionHandle(new ObjectIdentity(3, 2), 2));
        Assert.IsTrue(first.Release(token, ReleaseReason.Recovery));
        Assert.IsTrue(first.Valid(newLease));
        Assert.IsFalse(first.Release(token, ReleaseReason.Recovery));
    }
    [Test]
    public void PathCancelBackOfPreviousResultWillNotHandoffToNewRequest()
    {
        var character = CreateCharacter(); var agent = character.GetComponent<PathAgent>();
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        int previousVersion = (int)typeof(PathAgent).GetField("requestVersion", flag).GetValue(agent);
        agent.CancelPath();
        // 旧版本必须在读取结果内容前退出，空结果可证明没有继续消费旧回调。
        var type = typeof(PathAgent).GetNestedType("RequestContext", BindingFlags.NonPublic);
        var context = Activator.CreateInstance(type, flag, null, new object[] { agent }, null);
        type.GetField("version", flag).SetValue(context, previousVersion);
        Assert.DoesNotThrow(() => InvokeValue(agent, "OnPathComplete", null, context));
        Assert.AreEqual(0, ((ICollection)typeof(PathAgent).GetField("pendingPath", flag).GetValue(agent)).Count);
    }

    private HouseThreeTraversalGeometry CreateCompleteStair()
    {
        var route = CreateTraversal<HouseThreeTraversalGeometry>();
        route.entranceDoor = CreateTraversal<DoorController>();
        Transform Point(Vector3 positionValue)
        {
            var objectValue = new GameObject("通路测试点"); temporaryObject.Add(objectValue);
            objectValue.transform.position = positionValue; return objectValue.transform;
        }
        route.exteriorGate = Point(Vector3.back);
        route.doorGate = Point(Vector3.zero);
        route.interiorGate = Point(Vector3.forward);
        route.roomTurn = Point(Vector3.forward);
        route.lowerEntry = Point(Vector3.zero);
        route.lowerJunction = Point(new Vector3(0, 1, 2));
        route.upperJunction = Point(new Vector3(0, 2, 4));
        route.upperExit = Point(new Vector3(0, 3, 6));
        route.upperLandingTurn = Point(new Vector3(1, 3, 6));
        route.upperPlatformRelease = Point(new Vector3(3, 3, 6));
        return route;
    }

    [Test]
    public void NormalUseStairsWithoutRequiringExtraStairTicket()
    {
        var character = CreateCharacter();
        character.door03TraversalRoute = CreateCompleteStair();
        character.transform.position = new Vector3(0, 3, 6);
        character.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        var token = character.motionExecutor.CurrentToken;
        Assert.IsTrue(character.motionExecutor.CommitPosition(token, new Vector3(0, 2.95f, 5.9f)));
        Assert.AreEqual(new Vector3(0, 2.95f, 5.9f), character.transform.position);
        Assert.IsTrue(character.motionExecutor.CommitPosition(token, new Vector3(0, 3, 6)));
        Assert.IsTrue(character.motionExecutor.CommitPosition(token, new Vector3(1, 3, 6)));
    }

    [Test]
    public void TraversalControlAndBridgeLowerGroundUnaffectedByPlatformBoundaryFalseBlocking()
    {
        var character = CreateCharacter();
        character.door03TraversalRoute = CreateCompleteStair();
        character.transform.position = new Vector3(0, 0, 5);
        character.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        Assert.IsTrue(character.motionExecutor.CommitPosition(character.motionExecutor.CurrentToken, new Vector3(0, 0, 4)));
        character.transform.position = new Vector3(0, 3, 6);
        character.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
        Assert.IsTrue(character.motionExecutor.CommitPosition(character.motionExecutor.CurrentToken, new Vector3(0, 2.5f, 5)));
    }

    [Test]
    public void StairAttackHandoffCanOnlyByCurrentActionCommitRootDisplacement()
    {
        var character = CreateCharacter();
        character.door03TraversalRoute = CreateCompleteStair();
        character.transform.position = new Vector3(0, 3, 6);
        character.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        var walkToken = character.motionExecutor.CurrentToken;
        character.SetMovementMode(EnemyMovementMode.ActionRootMotion);
        Assert.IsFalse(character.motionExecutor.CommitPosition(walkToken, new Vector3(0, 3, 5.9f)));
        Assert.IsTrue(character.motionExecutor.CommitPosition(character.motionExecutor.CurrentToken, new Vector3(0, 3, 5.9f)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidateDoorwayTokenRevokeWaitButMustNotRemoveNewOccupancy(bool hasAcquireNewOccupancy)
    {
        var character = CreateCharacter(); var door = CreateTraversal<DoorController>();
        var occupancy = Read(door, "TraversalOccupancy");
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(VillagerController).GetField("pursuitIngressDoor", flag).SetValue(character, door);
        typeof(VillagerController).GetField("pursuitIngressSourceSide", flag).SetValue(character, 1);
        typeof(VillagerController).GetField("pursuitIngressCrossing", flag).SetValue(character, true);
        InvokeValue(character, "RegisterPursuitIngressWaiter", door);
        Assert.IsTrue((bool)InvokeValue(occupancy, "TryOccupancy", character, 1));
        var previous = (TraversalToken)InvokeValue(occupancy, "QueryToken", character);
        typeof(VillagerController).GetField("doorwayTraversalToken", flag).SetValue(character, previous);
        Assert.IsTrue(door.ReleaseTraversal(previous, ReleaseReason.Complete));
        InvokeValue(character, "RegisterPursuitIngressWaiter", door);
        if (hasAcquireNewOccupancy) Assert.IsTrue((bool)InvokeValue(occupancy, "TryOccupancy", character, 1));
        var current = (TraversalToken)InvokeValue(occupancy, "QueryToken", character);
        InvokeValue(character, "UnregisterPursuitIngressWaiter");
        var queue = (System.Collections.ObjectModel.ReadOnlyCollection<VillagerController>)InvokeValue(occupancy, "ReadQueue", 1);
        Assert.AreEqual(hasAcquireNewOccupancy ? 1 : 0, queue.Count);
        Assert.AreEqual(hasAcquireNewOccupancy, door.TraversalValid(current));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeactivateOrCloseRequestBackAlarmCannotRestoreChase(bool deactivateObject)
    {
        var character = CreateCharacter();
        bool originalCombatMarker = character.IsCombatEngaged;
        if (deactivateObject) character.gameObject.SetActive(false);
        else InvokeValue(character.actionCoordinator, "CloseAction");
        character.ReceiveVillagePursuitOrder(new Vector3(8, 0, 8));
        Assert.AreEqual(originalCombatMarker, character.IsCombatEngaged);
        Assert.IsFalse(character.RefreshChaseDestination());
        Assert.IsFalse(character.motionExecutor.Valid(character.motionExecutor.CurrentToken));
    }
}
