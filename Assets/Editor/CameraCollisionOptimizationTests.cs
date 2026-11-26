using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 摄像机避障与运镜交接优化单元测试。
/// 在远离场景几何的独立坐标处验证过滤器、顶棚下压、肩位滑移、出口舒展及穿窗连续性。
/// </summary>
public sealed class CameraCollisionOptimizationTests
{
    private static readonly Vector3 TestOrigin = new Vector3(5000f, 5000f, 5000f);
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        Physics.SyncTransforms();
    }

    private GameObject CreateBox(string name, Vector3 center, Vector3 size, int layer = 0)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.position = center;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = size;
        createdObjects.Add(go);
        return go;
    }

    private static CameraCollisionSolver.SolveRequest CreateBaseRequest(Vector3 focus, Vector3 desired)
    {
        return new CameraCollisionSolver.SolveRequest
        {
            focusPosition = focus,
            desiredPosition = desired,
            referenceUp = Vector3.up,
            cameraRight = Vector3.right,
            desiredShoulderOffset = 0.5f,
            cameraRadius = 0.2f,
            safetySkin = 0.04f,
            nearPlaneRadius = 0.22f,
            minimumDistance = 0.45f,
            collisionMask = ~0,
            playerRoot = null,
            activeWindow = null,
            inPassage = false,
            exitEaseWeight = 0f,
            deltaTime = 0.016f,
            resetState = false
        };
    }

    [Test]
    public void IgnoreEnemyAndDynamicColliders_DoesNotPullCameraIn()
    {
        Vector3 focus = TestOrigin + new Vector3(0f, 1.4f, 0f);
        Vector3 desired = TestOrigin + new Vector3(0.5f, 1.55f, -1.8f);

        GameObject enemyRoot = new GameObject("TestVillager");
        enemyRoot.transform.position = TestOrigin + new Vector3(0.25f, 0f, -0.9f);
        enemyRoot.AddComponent<VillagerController>();
        createdObjects.Add(enemyRoot);

        GameObject enemyChild = new GameObject("EnemyDefaultBodyCollider");
        enemyChild.layer = 0;
        enemyChild.transform.SetParent(enemyRoot.transform, false);
        enemyChild.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        BoxCollider enemyBodyCollider = enemyChild.AddComponent<BoxCollider>();
        enemyBodyCollider.size = new Vector3(1.2f, 1.8f, 1.2f);

        GameObject dynamicProp = CreateBox("DynamicProp", TestOrigin + new Vector3(0.3f, 1.45f, -1.1f), new Vector3(1.2f, 1.8f, 1.2f));
        Rigidbody propBody = dynamicProp.AddComponent<Rigidbody>();
        propBody.isKinematic = false;
        propBody.useGravity = false;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        GameObject enemyLayerObj = null;
        if (enemyLayer >= 0)
        {
            enemyLayerObj = CreateBox("EnemyLayerObstacle", TestOrigin + new Vector3(0.35f, 1.45f, -1.2f), new Vector3(1f, 1.8f, 1f), enemyLayer);
        }

        Physics.SyncTransforms();

        CameraCollisionFilter.ReadEnemyLayers(out int resolvedEnemyLayer, out int resolvedHitboxLayer);
        Assert.IsTrue(CameraCollisionFilter.ShouldIgnoreCollider(enemyBodyCollider, null, null, resolvedEnemyLayer, resolvedHitboxLayer));
        Assert.IsTrue(CameraCollisionFilter.ShouldIgnoreCollider(dynamicProp.GetComponent<Collider>(), null, null, resolvedEnemyLayer, resolvedHitboxLayer));
        if (enemyLayerObj != null)
        {
            Assert.IsTrue(CameraCollisionFilter.ShouldIgnoreCollider(enemyLayerObj.GetComponent<Collider>(), null, null, resolvedEnemyLayer, resolvedHitboxLayer));
        }

        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest request = CreateBaseRequest(focus, desired);
        request.resetState = true;

        Vector3 result = solver.Solve(in request, in settings);
        Assert.Less(Vector3.Distance(result, desired), 0.02f);
        Assert.Less(solver.CompressionRatio, 0.05f);
    }

    [Test]
    public void LowCeiling_DropsCameraHeightInsteadOfRaising()
    {
        Vector3 focus = TestOrigin + new Vector3(0f, 1.4f, 0f);
        Vector3 desired = TestOrigin + new Vector3(0.5f, 1.95f, -1.8f);

        // 底面位于 TestOrigin.y + 1.75m 的低矮天花板，覆盖相机上方。
        CreateBox("LowCeiling", TestOrigin + new Vector3(0.25f, 2.0f, -1.0f), new Vector3(4.0f, 0.5f, 4.0f));
        Physics.SyncTransforms();

        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest request = CreateBaseRequest(focus, desired);
        request.resetState = true;

        Vector3 result = solver.Solve(in request, in settings);
        request.resetState = false;
        for (int i = 0; i < 8; i++)
        {
            result = solver.Solve(in request, in settings);
        }

        float ceilingBottomY = TestOrigin.y + 1.75f;
        Assert.Less(result.y, desired.y - 0.1f, "低矮天花板下应优先下压机位高度");
        Assert.LessOrEqual(result.y + request.cameraRadius, ceilingBottomY + 0.01f, "下压后相机球体不应穿透顶棚底面");
        Assert.Less(solver.LastSnapshot.ceilingDrop, -0.1f);
    }

    [Test]
    public void SideWallObstruction_SlidesShoulderInwardBeforeCollapsingDistance()
    {
        Vector3 focus = TestOrigin + new Vector3(0f, 1.4f, 0f);
        Vector3 desired = TestOrigin + new Vector3(0.5f, 1.55f, -1.8f);

        // 右肩侧贴近一道沿 Z 轴延伸的实心墙体，中轴 X = 0 保持畅通。
        CreateBox("RightSideWall", TestOrigin + new Vector3(0.72f, 1.5f, -0.9f), new Vector3(0.8f, 3.0f, 3.2f));
        Physics.SyncTransforms();

        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest request = CreateBaseRequest(focus, desired);
        request.resetState = true;

        Vector3 result = solver.Solve(in request, in settings);
        request.resetState = false;
        for (int i = 0; i < 15; i++)
        {
            result = solver.Solve(in request, in settings);
        }

        float actualDistance = Vector3.Distance(focus, result);
        Assert.Less(solver.LastSnapshot.shoulderScale, 0.7f, "右肩贴墙时应优先向中轴内收肩位");
        Assert.Greater(actualDistance, 1.45f, "肩位内收后应保住主要观察视距，不骤然贴脸");
    }

    [Test]
    public void ExitEase_LimitsRecoverySpeedAndPreventsSingleFrameSnap()
    {
        Vector3 focus = TestOrigin + new Vector3(0f, 1.4f, 0f);
        Vector3 desired = TestOrigin + new Vector3(0.5f, 1.55f, -1.8f);
        float desiredDistance = Vector3.Distance(focus, desired);

        GameObject backWall = CreateBox("TemporaryBackWall", TestOrigin + new Vector3(0.25f, 1.5f, -0.85f), new Vector3(3.0f, 3.0f, 0.3f));
        Physics.SyncTransforms();

        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest request = CreateBaseRequest(focus, desired);
        request.resetState = true;

        Vector3 compressedPos = solver.Solve(in request, in settings);
        float compressedDistance = Vector3.Distance(focus, compressedPos);
        Assert.Less(compressedDistance, 1.0f);

        // 移除遮挡，模拟翻出窗洞或走出门框进入开阔区域。
        Object.DestroyImmediate(backWall);
        Physics.SyncTransforms();

        request.resetState = false;
        request.deltaTime = 0.016f;
        request.exitEaseWeight = 1.0f;

        Vector3 firstReleasePos = solver.Solve(in request, in settings);
        float firstStepGain = Vector3.Distance(focus, firstReleasePos) - compressedDistance;
        Assert.LessOrEqual(firstStepGain, settings.maxExitRecoverySpeed * request.deltaTime + 0.02f, "出口首帧恢复步长应受最大恢复速度约束，不能单帧弹射");

        Vector3 currentPos = firstReleasePos;
        float prevDistance = Vector3.Distance(focus, currentPos);
        for (int i = 1; i < 42; i++)
        {
            request.exitEaseWeight = Mathf.Clamp01(1f - i / 26f);
            currentPos = solver.Solve(in request, in settings);
            float stepDist = Vector3.Distance(focus, currentPos);
            if (request.exitEaseWeight > 0.8f)
            {
                float stepGain = stepDist - prevDistance;
                Assert.LessOrEqual(stepGain, settings.maxExitRecoverySpeed * request.deltaTime + 0.02f, "出口舒展初期每帧恢复速度应受控");
            }
            prevDistance = stepDist;
        }

        float finalDistance = Vector3.Distance(focus, currentPos);
        Assert.Greater(finalDistance, desiredDistance - 0.12f, "出口舒展期结束后应平滑恢复到期望视距");
    }

    [Test]
    public void PassageRelativeSmoothing_DoesNotTrapBehindWindowFrame()
    {
        // 在 Z = 0 处放置实心墙体，验证穿窗前后焦点相对平滑不会把机位卡在墙另一侧。
        CreateBox("SolidWindowWall", TestOrigin + new Vector3(0f, 1.5f, 0f), new Vector3(4.0f, 3.0f, 0.3f));
        Physics.SyncTransforms();

        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();

        Vector3 insideFocus = TestOrigin + new Vector3(0f, 1.4f, -0.8f);
        Vector3 insideDesired = TestOrigin + new Vector3(0.5f, 1.5f, -2.4f);
        CameraCollisionSolver.SolveRequest insideRequest = CreateBaseRequest(insideFocus, insideDesired);
        insideRequest.inPassage = true;
        insideRequest.resetState = true;
        solver.Solve(in insideRequest, in settings);

        // 下一帧角色完成穿窗来到墙体 +Z 侧，期望机位也在 +Z 侧开阔区。
        Vector3 outsideFocus = TestOrigin + new Vector3(0f, 1.4f, 2.2f);
        Vector3 outsideDesired = TestOrigin + new Vector3(0.5f, 1.5f, 0.6f);
        CameraCollisionSolver.SolveRequest outsideRequest = CreateBaseRequest(outsideFocus, outsideDesired);
        outsideRequest.inPassage = true;
        outsideRequest.resetState = false;
        outsideRequest.deltaTime = 0.016f;

        Vector3 outsideResult = solver.Solve(in outsideRequest, in settings);
        Assert.Greater(outsideResult.z, TestOrigin.z + 0.35f, "穿窗后相机应跟随焦点来到墙体外侧，不应卡在窗墙背面");
    }

    [Test]
    public void OverrideCollisionFocus_IgnoresStaleControllerPositionDuringExternalSampling()
    {
        Vector3 staleDoorPosition = TestOrigin + new Vector3(0f, 0f, -4.0f);
        Vector3 exitPosition = TestOrigin + new Vector3(0f, 0f, 2.0f);

        // 在旧门口与过场交还站位之间放置实心墙体，模拟过场期间未传送玩家刚体时的隔墙环境。
        CreateBox("HouseWallBetweenDoorAndExit", TestOrigin + new Vector3(0f, 1.5f, 0f), new Vector3(6.0f, 3.0f, 0.4f));
        Physics.SyncTransforms();

        GameObject staleTargetObj = new GameObject("StaleDoorTarget");
        staleTargetObj.transform.position = staleDoorPosition;
        createdObjects.Add(staleTargetObj);

        GameObject followPivotObj = new GameObject("ExitFollowPivot");
        followPivotObj.transform.position = exitPosition + Vector3.up * 1.32f;
        createdObjects.Add(followPivotObj);

        GameObject cameraHost = new GameObject("TestThirdPersonCamera");
        RE4RThirdPersonCamera cameraDriver = cameraHost.AddComponent<RE4RThirdPersonCamera>();
        createdObjects.Add(cameraHost);

        BindingFlags privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo targetField = typeof(RE4RThirdPersonCamera).GetField("target", privateFlags);
        FieldInfo pivotField = typeof(RE4RThirdPersonCamera).GetField("followPivot", privateFlags);
        MethodInfo setOverrideMethod = typeof(RE4RThirdPersonCamera).GetMethod("SetOverrideCollisionFocusRoot", privateFlags);
        MethodInfo readFocusMethod = typeof(RE4RThirdPersonCamera).GetMethod("ReadStableCollisionFocusPosition", privateFlags);

        Assert.IsNotNull(targetField);
        Assert.IsNotNull(pivotField);
        Assert.IsNotNull(setOverrideMethod);
        Assert.IsNotNull(readFocusMethod);

        targetField.SetValue(cameraDriver, staleTargetObj.transform);
        pivotField.SetValue(cameraDriver, followPivotObj.transform);

        // 1. 启用外部输入抑制时，应自动退回 followPivot 所在交还站位，不读取旧门口位置。
        cameraDriver.SetInputSuppressed(true);
        Vector3 suppressedFocus = (Vector3)readFocusMethod.Invoke(cameraDriver, new object[] { Vector3.up });
        Assert.Less(Vector3.Distance(suppressedFocus, followPivotObj.transform.position), 0.01f);

        // 2. 即使在非抑制状态下，显式设置覆盖原点也必须优先使用 exitPosition，且避障不应撞上旧门口之间的墙体。
        cameraDriver.SetInputSuppressed(false);
        setOverrideMethod.Invoke(cameraDriver, new object[] { true, exitPosition });
        Vector3 overriddenFocus = (Vector3)readFocusMethod.Invoke(cameraDriver, new object[] { Vector3.up });
        Assert.Greater(overriddenFocus.z, TestOrigin.z + 1.5f);

        Vector3 desiredShoulder = exitPosition + new Vector3(0.5f, 1.55f, -1.5f);
        CameraCollisionSolver solver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest request = CreateBaseRequest(overriddenFocus, desiredShoulder);
        request.resetState = true;

        Vector3 solved = solver.Solve(in request, in settings);
        Assert.Less(Vector3.Distance(solved, desiredShoulder), 0.05f, "覆盖观察原点后采样机位应保持在目标越肩位置，不受旧门口隔墙拉偏");

        // 3. 清除覆盖原点后，恢复读取实时锚点位置。
        setOverrideMethod.Invoke(cameraDriver, new object[] { false, Vector3.zero });
        Vector3 normalFocus = (Vector3)readFocusMethod.Invoke(cameraDriver, new object[] { Vector3.up });
        Assert.Less(normalFocus.z, TestOrigin.z - 3.5f);
    }

    [Test]
    public void HandgunLevelAim_IsHigherAndCloserToEarThanLegacyTorsoFraming()
    {
        float fov = 40f;
        float aspect = 16f / 9f;
        float tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanX = tanY * aspect;

        Vector3 shoulders = new Vector3(0f, 1.42f, 0f);
        Vector3 head = new Vector3(0f, 1.66f, 0f);
        Vector3 earLevel = Vector3.Lerp(shoulders, head, 0.52f);
        Vector3 torsoAxis = new Vector3(0f, earLevel.y, 0f);

        Vector3 legacyPos = shoulders - new Vector3(-0.56f * tanX, -0.38f * tanY, 1f) * 0.82f;
        Vector3 newPos = RE4RThirdPersonCamera.SolveOverShoulderAimPosition(
            earLevel,
            torsoAxis,
            Quaternion.identity,
            tanX,
            tanY,
            0.18f,
            0f,
            false,
            0f);

        Assert.Greater(newPos.y, legacyPos.y + 0.05f, "手枪平视 ADS 机位应比旧胸背基准更高、接近耳侧高度");
        Assert.Less(Mathf.Abs(newPos.z), Mathf.Abs(legacyPos.z) - 0.10f, "手枪平视 ADS 机位应更贴耳紧凑");

        Vector3 headLocal = head - newPos;
        float headNdcX = headLocal.x / (headLocal.z * tanX);
        float headNdcY = headLocal.y / (headLocal.z * tanY);
        Assert.Less(headNdcX, -0.45f);
        Assert.Greater(headNdcY, 0.05f);
        Assert.Less(headNdcY, 0.55f);
    }

    [Test]
    public void SteepDownPitchShotgunAim_ClearsRightElbowAndShoulderFromReticle()
    {
        float fov = 40f;
        float aspect = 16f / 9f;
        float tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanX = tanY * aspect;
        float lookPitch = 65f;
        Quaternion orientation = Quaternion.Euler(lookPitch, 0f, 0f);

        Vector3 torsoAxis = new Vector3(0f, 1.54f, 0f);
        Vector3 earLevel = new Vector3(-0.08f, 1.52f, 0.04f);
        Vector3 rightShoulder = new Vector3(0.14f, 1.40f, 0.08f);
        Vector3 rightElbow = new Vector3(0.26f, 1.25f, 0.22f);
        Vector3 head = new Vector3(-0.06f, 1.64f, 0.08f);

        Vector3 baseGunPos = RE4RThirdPersonCamera.SolveOverShoulderAimPosition(
            earLevel,
            torsoAxis,
            orientation,
            tanX,
            tanY,
            0.18f,
            lookPitch,
            true,
            0f);

        Vector3 clearance = RE4RThirdPersonCamera.ComputeReticleClearanceLocalOffset(
            baseGunPos,
            orientation,
            tanX,
            tanY,
            lookPitch,
            rightShoulder,
            true,
            rightElbow,
            true,
            head,
            true);

        Vector3 finalPos = baseGunPos + orientation * clearance;
        Quaternion toCamera = Quaternion.Inverse(orientation);

        AssertPointClearsReticle(rightElbow, 0.13f, finalPos, toCamera, tanX, tanY, "右肘");
        AssertPointClearsReticle(rightShoulder, 0.11f, finalPos, toCamera, tanX, tanY, "右肩");
    }

    [Test]
    public void AimMode_LocksShoulderSlideAgainstTowerSideWall()
    {
        Vector3 focus = TestOrigin + new Vector3(0f, 1.4f, 0f);
        Vector3 desired = TestOrigin + new Vector3(0.5f, 1.55f, -1.8f);

        CreateBox("TowerRightWall", TestOrigin + new Vector3(0.72f, 1.5f, -0.9f), new Vector3(0.8f, 3.0f, 3.2f));
        Physics.SyncTransforms();

        CameraCollisionSolver exploreSolver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        CameraCollisionSolver.SolveRequest exploreRequest = CreateBaseRequest(focus, desired);
        exploreRequest.aimShoulderLockWeight = 0f;
        exploreRequest.resetState = true;
        exploreSolver.Solve(in exploreRequest, in settings);
        exploreRequest.resetState = false;
        for (int i = 0; i < 12; i++)
        {
            exploreSolver.Solve(in exploreRequest, in settings);
        }

        Assert.Less(exploreSolver.LastSnapshot.shoulderScale, 0.7f, "非瞄准状态下遇侧墙应允许肩位滑移避障");

        CameraCollisionSolver aimSolver = new CameraCollisionSolver();
        CameraCollisionSolver.SolveRequest aimRequest = CreateBaseRequest(focus, desired);
        aimRequest.aimShoulderLockWeight = 1f;
        aimRequest.resetState = true;
        aimSolver.Solve(in aimRequest, in settings);
        aimRequest.resetState = false;
        for (int i = 0; i < 12; i++)
        {
            aimSolver.Solve(in aimRequest, in settings);
        }

        Assert.GreaterOrEqual(aimSolver.LastSnapshot.shoulderScale, 0.77f, "ADS 瞄准状态下应锁定肩位不向后脑勺中轴内收");
    }

    [Test]
    public void LevelAimBreathing_DoesNotSnapReticleClearanceAcrossThreshold()
    {
        float fov = 40f;
        float aspect = 16f / 9f;
        float tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanX = tanY * aspect;
        Quaternion orientation = Quaternion.identity;

        Vector3 earLevel = new Vector3(0f, 1.54f, 0f);
        Vector3 torsoAxis = new Vector3(0f, 1.54f, 0f);
        Vector3 baseGunPos = RE4RThirdPersonCamera.SolveOverShoulderAimPosition(
            earLevel,
            torsoAxis,
            orientation,
            tanX,
            tanY,
            0.18f,
            0f,
            false,
            0f);

        Vector3 rightShoulder = new Vector3(0.14f, 1.40f, 0.08f);
        Vector3 head = new Vector3(-0.06f, 1.64f, 0.08f);
        Vector3 prevOffset = Vector3.zero;
        bool hasPrev = false;

        // 模拟平视呼吸或微幅俯仰导致右肘从 ndcTop=-0.24 跨过 -0.20 到 -0.02 之间连续起伏。
        for (int step = 0; step <= 40; step++)
        {
            float elbowY = Mathf.Lerp(1.22f, 1.48f, step / 40f);
            Vector3 rightElbow = new Vector3(0.18f, elbowY, 0.22f);
            Vector3 offset = RE4RThirdPersonCamera.ComputeReticleClearanceLocalOffset(
                baseGunPos,
                orientation,
                tanX,
                tanY,
                0f,
                rightShoulder,
                true,
                rightElbow,
                true,
                head,
                true);

            if (hasPrev)
            {
                float jump = Vector3.Distance(offset, prevOffset);
                Assert.Less(jump, 0.02f, "平视微动跨过准星让位阈值时不得发生单步阶跃跳变: step=" + step);
            }

            prevOffset = offset;
            hasPrev = true;
        }
    }

    private static void AssertPointClearsReticle(
        Vector3 worldPoint,
        float radius,
        Vector3 cameraPos,
        Quaternion toCamera,
        float tanX,
        float tanY,
        string label)
    {
        Vector3 local = toCamera * (worldPoint - cameraPos);
        if (local.z <= 0.06f)
        {
            return;
        }

        float ndcRight = (local.x + radius) / (local.z * tanX);
        float ndcTop = (local.y + radius) / (local.z * tanY);
        bool clearsHorizontally = ndcRight <= -0.14f;
        bool clearsVertically = ndcTop <= -0.19f;
        Assert.IsTrue(clearsHorizontally || clearsVertically, label + " 仍侵入准星视野: ndcRight=" + ndcRight + ", ndcTop=" + ndcTop);
    }
}
