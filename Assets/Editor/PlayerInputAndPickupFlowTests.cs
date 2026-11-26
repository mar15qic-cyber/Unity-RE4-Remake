using System;
using System.Collections.Generic;
using System.Reflection;
using GameArchitecture;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class PlayerInputAndPickupFlowTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<InputDevice> createdDevices = new List<InputDevice>();

    [TearDown]
    public void Cleanup()
    {
        for (int index = 0; index < createdObjects.Count; index++)
        {
            if (createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
        }
        createdObjects.Clear();

        for (int index = 0; index < createdDevices.Count; index++)
        {
            if (createdDevices[index] != null && createdDevices[index].added)
            {
                InputSystem.RemoveDevice(createdDevices[index]);
            }
        }
        createdDevices.Clear();
    }

    private GameObject CreateTestPlayerRig(
        out LeonController controller,
        out LeonInputBridge inputBridge,
        out LeonActionMotionDriver motionDriver,
        out LeonAnimationActionCoordinator coordinator,
        out LeonAnimatorDriver animatorDriver,
        out LeonCombat combat,
        out LeonItemPickup itemPickup)
    {
        var playerObject = new GameObject("输入与拾取测试玩家");
        createdObjects.Add(playerObject);
        playerObject.AddComponent<Rigidbody>();
        var animator = playerObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/RE4ChapterOneAnimationLoop/Generated/MainLeon.controller");

        controller = playerObject.AddComponent<LeonController>();
        LeonLocomotion locomotion = playerObject.AddComponent<LeonLocomotion>();
        motionDriver = playerObject.AddComponent<LeonActionMotionDriver>();
        animatorDriver = playerObject.AddComponent<LeonAnimatorDriver>();
        coordinator = playerObject.AddComponent<LeonAnimationActionCoordinator>();
        combat = playerObject.AddComponent<LeonCombat>();
        itemPickup = playerObject.AddComponent<LeonItemPickup>();
        inputBridge = playerObject.AddComponent<LeonInputBridge>();

        SetField(controller, "animCoordinator", coordinator);
        SetField(controller, "animDriver", animatorDriver);
        SetField(controller, "locomotion", locomotion);
        SetField(controller, "inputBridge", inputBridge);
        SetField(animatorDriver, "animator", animator);
        SetField(animatorDriver, "ownerController", controller);

        InvokeNonPublic(motionDriver, "OnEnable");
        motionDriver.AuthorizeMotion(MotionSource.NormalMovement, 0);

        SetField(coordinator, "driver", animatorDriver);
        SetField(coordinator, "actionMotionDriver", motionDriver);
        coordinator.InitializePlayerState(locomotion, LeonState.Idle);

        SetField(combat, "controller", controller);
        SetField(combat, "driver", animatorDriver);
        SetField(combat, "coordinator", coordinator);
        SetField(combat, "inputBridge", inputBridge);

        SetField(itemPickup, "player", controller);
        SetField(itemPickup, "actions", coordinator);
        SetField(itemPickup, "driver", animatorDriver);
        SetField(itemPickup, "motion", motionDriver);
        SetField(itemPickup, "inputBridge", inputBridge);

        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Scripts/Input/LeonInputActions.inputactions");
        Assert.IsNotNull(asset, "未找到玩家输入动作资产 LeonInputActions.inputactions。");
        SetField(inputBridge, "inputActionAsset", asset);
        SetField(inputBridge, "controller", controller);
        SetField(inputBridge, "movementDriver", motionDriver);
        SetField(inputBridge, "actions", coordinator);
        SetField(inputBridge, "driver", animatorDriver);
        SetField(inputBridge, "combat", combat);
        InvokeNonPublic(inputBridge, "Start");

        return playerObject;
    }

    [Test]
    public void RestoreMovementInputPreservesHeldMoveAndSprintAcrossDisableEnable()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        createdDevices.Add(keyboard);

        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
        InputSystem.Update();

        inputBridge.SetGameplayInputEnabled(false);
        controller.SetMoveInput(Vector3.zero);
        controller.SetSprinting(false);

        inputBridge.SetGameplayInputEnabled(true);

        Assert.Greater(controller.MoveInput.z, 0.5f, "恢复玩法输入后应立即保留按住的 W 前进方向。");
        Assert.IsTrue(controller.IsSprinting, "恢复玩法输入后应立即保留按住的 LeftShift 冲刺状态。");
        Assert.AreEqual(MotionSource.NormalMovement, motionDriver.CurrentMotionToken.Source);
    }

    [Test]
    public void UnifiedBufferExpiresTtlAndOverridesWithLatestIntent()
    {
        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        // 先模拟一个全身阻塞动作，再压入换弹与快速切换意图，验证互斥覆盖与最新序号胜出。
        SetField(coordinator, "hasActiveFullBodyTx", true);
        AnimationActionTransaction blockingTx = new AnimationActionTransaction
        {
            transactionId = 41,
            fullBodyActionType = FullBodyActionType.Hit,
            fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal
        };
        SetField(coordinator, "fullBodyTx", blockingTx);

        double now = Time.timeAsDouble;
        InvokeNonPublic(inputBridge, "DispatchGameplayIntent", CreateGameplayIntentKind(1), now, -1); // Reload
        InvokeNonPublic(inputBridge, "DispatchGameplayIntent", CreateGameplayIntentKind(3), now + 0.05, 0); // QuickSlot

        Array intents = (Array)GetField(inputBridge, "gameplayIntents");
        Assert.IsNotNull(intents);
        object reloadIntent = intents.GetValue(1);
        object quickSlotIntent = intents.GetValue(3);
        Assert.IsFalse((bool)GetField(reloadIntent, "pending"), "更新的互斥动作意图应清空先前的旧缓冲。");
        Assert.IsTrue((bool)GetField(quickSlotIntent, "pending"), "最新按下的动作意图应保留在统一缓冲中。");

        // 将 pressedAt 拨早于 0.28s 有效期，调用 ReplayGameplayIntents 验证自动淘汰。
        SetField(quickSlotIntent, "pressedAt", now - 1.0);
        intents.SetValue(quickSlotIntent, 3);
        InvokeNonPublic(inputBridge, "ReplayGameplayIntents");

        object expiredQuickSlot = intents.GetValue(3);
        Assert.IsFalse((bool)GetField(expiredQuickSlot, "pending"), "超过 TTL 的过期预输入应在重放时被清理。");
    }

    [Test]
    public void DiscardPendingPickupInputClearsBufferedPickupIntent()
    {
        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        const int pickupTxId = 77;
        SetField(coordinator, "hasActiveFullBodyTx", true);
        AnimationActionTransaction pickupTx = new AnimationActionTransaction
        {
            transactionId = pickupTxId,
            fullBodyActionType = FullBodyActionType.ItemPickup,
            fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal
        };
        SetField(coordinator, "fullBodyTx", pickupTx);

        SetField(inputBridge, "pickupInputTransaction", pickupTxId);
        SetField(inputBridge, "pendingPickupAim", true);
        SetField(inputBridge, "restoreHeldAim", true);

        InvokeNonPublic(inputBridge, "DispatchGameplayIntent", CreateGameplayIntentKind(2), Time.timeAsDouble, -1); // Interact
        Assert.IsTrue((bool)InvokeNonPublic(inputBridge, "HasBufferedPickupInput", pickupTxId),
            "拾取期间按下的交互输入应被缓冲。");

        InvokeNonPublic(inputBridge, "DiscardPendingPickupInput", pickupTxId);
        Assert.IsFalse((bool)InvokeNonPublic(inputBridge, "HasBufferedPickupInput", pickupTxId),
            "检视界面接管或丢弃后不应残留拾取缓冲意图。");
        Assert.IsFalse((bool)GetField(inputBridge, "restoreHeldAim"), "丢弃拾取缓冲后应清除待恢复瞄准状态。");
    }

    [Test]
    public void ReloadDuringShootingDoesNotWaitInGenericBufferAndQueuesW870Reload()
    {
        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        SetField(controller, "initialWeaponValue", WeaponType.Shotgun);
        controller.ChangeState(LeonState.Shooting);

        bool blocked = (bool)InvokeNonPublic(inputBridge, "GameplayActionRecovering", CreateGameplayIntentKind(1)); // Reload
        Assert.IsFalse(blocked, "射击状态下的换弹输入应直接透传给战斗模块入队，而非在外层滞留。");
    }

    [Test]
    public void ItemPickupRecoveryDifferentiatesIdleVsEarlyCancelAndRestoresAuthority()
    {
        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        SetField(itemPickup, "movingPickup", false);
        SetField(itemPickup, "pickupHeight", 1);
        float middleNatural = (float)InvokeNonPublic(itemPickup, "ReadNaturalRecoveryTime");
        SetField(itemPickup, "pickupHeight", 0);
        float lowNatural = (float)InvokeNonPublic(itemPickup, "ReadNaturalRecoveryTime");
        Assert.Greater(middleNatural, lowNatural, "待机自然收手中位应长于低位以完整展现收手动作。");

        controller.SetMoveInput(Vector3.zero);
        Assert.IsFalse((bool)InvokeNonPublic(itemPickup, "HasCancelIntent"), "无方向与战斗输入时不应触发早取消。");

        controller.SetMoveInput(Vector3.forward);
        Assert.IsTrue((bool)InvokeNonPublic(itemPickup, "HasCancelIntent"), "持续按住方向输入时应立即命中早取消窗口。");

        // 验证站立拾取 Finish() 立即重新获取 NormalMovement 凭证并切回行走状态。
        MotionControlToken traversalToken = motionDriver.AuthorizeMotion(MotionSource.Traversal, 55);
        Assert.IsTrue(traversalToken.Valid);
        controller.ChangeState(LeonState.Traversing);
        SetField(itemPickup, "transactionId", 55);
        SetField(itemPickup, "motionToken", traversalToken);
        SetField(itemPickup, "movingPickup", false);

        InvokeNonPublic(itemPickup, "Finish");

        Assert.AreEqual(MotionSource.NormalMovement, motionDriver.CurrentMotionToken.Source,
            "站立拾取 Finish 后应同帧重新授权 NormalMovement 凭证。");
        Assert.AreEqual(LeonState.Walking, controller.currentState,
            "存在方向输入时站立拾取 Finish 应直接恢复到 Walking 状态。");
    }

    [Test]
    public void MovingPickupAndFadeOutMaintainSpeedFloorAndSkipObstructionLock()
    {
        CreateTestPlayerRig(
            out LeonController controller,
            out LeonInputBridge inputBridge,
            out LeonActionMotionDriver motionDriver,
            out LeonAnimationActionCoordinator coordinator,
            out LeonAnimatorDriver animatorDriver,
            out LeonCombat combat,
            out LeonItemPickup itemPickup);

        SetField(itemPickup, "transactionId", 88);
        SetField(itemPickup, "movingPickup", true);
        PropertyInfo movingEntryProp = typeof(LeonItemPickup).GetProperty("MovingEntrySpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(movingEntryProp);
        movingEntryProp.SetValue(itemPickup, 1.2f);

        float releasedFloor = (float)InvokeNonPublic(itemPickup, "ReadPickupSpeedFloor", 0f);
        Assert.AreEqual(0f, releasedFloor, "松开方向键时期望速度为零，不应残留滑步速度下限。");
        float floor = (float)InvokeNonPublic(itemPickup, "ReadPickupSpeedFloor", 2.4f);
        Assert.AreEqual(2.4f, floor, 0.001f, "移动拾取期间应将速度下限动态同步至当前期望步速。");
        Assert.IsTrue((bool)GetProperty(itemPickup, "IsPickupMotionProtected"));

        // 触发保护期并验证即使先前存在残留障碍计时，也不会在保护期内锁死 nativeBlocked。
        SetField(motionDriver, "nativeBlocked", true);
        SetField(motionDriver, "nativeBlockedTime", 0.5f);
        MotionControlToken normalToken = motionDriver.CurrentMotionToken;
        InvokeNonPublic(motionDriver, "PrepareNativeGaitStep", normalToken, Vector3.forward * 2.4f, floor, true);

        bool obstructed = (bool)InvokeNonPublic(motionDriver, "CheckNativeGaitObstruction", Vector3.forward);
        Assert.IsFalse(obstructed, "拾取与淡出保护期内不应触发撞墙误锁死。");
        Assert.IsFalse((bool)GetField(motionDriver, "nativeBlocked"));

        // 验证拾取事务结束进入全身层淡出阶段时，持按移动仍能保持不低于期望步速的速度下限，松键则立即归零。
        SetField(itemPickup, "transactionId", 0);
        SetField(itemPickup, "movingPickup", false);
        SetField(itemPickup, "fadeCarrySpeed", 2.8f);
        SetField(animatorDriver, "activeFullBodyAction", FullBodyActionType.ItemPickup);
        SetField(animatorDriver, "blendFullBodyLayerWeight", true);
        Animator animator = controller.GetComponent<Animator>();
        animator.SetLayerWeight(LeonAnimatorDriver.LayerFullBodyAction, 0.5f);

        float fadeFloor = (float)InvokeNonPublic(itemPickup, "ReadPickupSpeedFloor", 1.83f);
        Assert.GreaterOrEqual(fadeFloor, 1.83f, "全身层淡出期间持按方向应维持不低于期望步速的速度下限。");
        Assert.LessOrEqual(fadeFloor, 2.801f, "全身层淡出期间速度下限应在期望步速与惯性速度之间平滑过渡。");
        Assert.AreEqual(0f, (float)InvokeNonPublic(itemPickup, "ReadPickupSpeedFloor", 0f),
            "全身层淡出期间松开方向键应立即取消速度下限。");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "未找到字段: " + target.GetType().Name + "." + fieldName);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "未找到字段: " + target.GetType().Name + "." + fieldName);
        return field.GetValue(target);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property, "未找到属性: " + target.GetType().Name + "." + propertyName);
        return property.GetValue(target);
    }

    private static object InvokeNonPublic(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "未找到方法: " + target.GetType().Name + "." + methodName);
        return method.Invoke(target, args);
    }

    private static object CreateGameplayIntentKind(int index)
    {
        Type kindType = typeof(LeonInputBridge).GetNestedType("GameplayIntentKind", BindingFlags.NonPublic);
        Assert.IsNotNull(kindType, "未找到 LeonInputBridge.GameplayIntentKind 枚举类型。");
        return Enum.ToObject(kindType, index);
    }
}
