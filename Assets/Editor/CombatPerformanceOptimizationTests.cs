using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Pathfinding;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","战斗性能优化测试")]

public sealed class CombatPerformanceOptimizationTests
{
    private const BindingFlags member = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> temporaryObject = new();
    private GameObject Create(string name)
    { var objectValue = new GameObject(name); temporaryObject.Add(objectValue); return objectValue; }
    private static Type CacheType => typeof(PathAgent).Assembly.GetType("NavigationCollisionCache");
    private static object InvokeStatic(string method, params object[] parameter) =>
        CacheType.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, parameter);
    private static void Set(object objectValue, string field, object value) => objectValue.GetType().GetField(field, member).SetValue(objectValue, value);
    private static object Read(object objectValue, string field) => objectValue.GetType().GetField(field, member).GetValue(objectValue);
    private static object InvokeValue(object objectValue, string method, params object[] parameter) => objectValue.GetType().GetMethod(method, member).Invoke(objectValue, parameter);
    [TearDown] public void Cleanup()
    {
        foreach (var objectValue in temporaryObject) if (objectValue != null) UnityEngine.Object.DestroyImmediate(objectValue);
        temporaryObject.Clear(); InvokeStatic("ClearValue");
    }

    [Test] public void ObstacleCategoryWarmupBackNotProduceManagedAllocate()
    {
        var collision = Create("测试普通地面").AddComponent<BoxCollider>();
        var category = (Func<Collider, bool>)Delegate.CreateDelegate(typeof(Func<Collider, bool>), CacheType.GetMethod("IsObstacle", BindingFlags.Static | BindingFlags.NonPublic));
        for (int count = 0; count < 32; count++) category(collision);
        long front = GC.GetAllocatedBytesForCurrentThread();
        bool result = false;
        for (int count = 0; count < 1000; count++) result |= category(collision);
        long allocate = GC.GetAllocatedBytesForCurrentThread() - front;
        Assert.IsFalse(result); Assert.AreEqual(0, allocate);
    }

    [Test] public void RenameExplicitInvalidateBackUpdateObstacleCategory()
    {
        var collision = Create("测试地面").AddComponent<BoxCollider>();
        Assert.IsFalse((bool)InvokeStatic("IsObstacle", collision));
        collision.name = "测试灌木"; InvokeStatic("Invalidate", collision);
        Assert.IsTrue((bool)InvokeStatic("IsObstacle", collision));
        collision.name = "测试地面"; InvokeStatic("InvalidateHierarchy", collision.transform);
        Assert.IsFalse((bool)InvokeStatic("IsObstacle", collision));
    }

    [Test] public void NavigationModifierOfEnableStateNotBeingStaticCacheFreeze()
    {
        var objectValue = Create("测试地面"); var collision = objectValue.AddComponent<BoxCollider>();
        var modifier = objectValue.AddComponent<RecastNavmeshModifier>();
        modifier.mode = RecastNavmeshModifier.Mode.UnwalkableSurface;
        Assert.IsTrue((bool)InvokeStatic("IsObstacle", collision));
        modifier.enabled = false;
        Assert.IsFalse((bool)InvokeStatic("IsObstacle", collision));
    }

    [Test] public void CloseRecordTimeDuplicateRecordNotProduceManagedAllocate()
    {
        for (int count = 0; count < 8; count++) EnemyCombatTimeline.Record(null, "测试", "固定说明");
        long front = GC.GetAllocatedBytesForCurrentThread();
        for (int count = 0; count < 1000; count++)
            if (EnemyCombatTimeline.IsRecord(null)) EnemyCombatTimeline.Record(null, "测试", "固定说明");
        long allocate = GC.GetAllocatedBytesForCurrentThread() - front;
        Assert.AreEqual(0, allocate);
    }

    [Test] public void EnumDescribeWarmupBackNotBoxing()
    {
        EnemyCombatTimeline.Describe(VillagerState.Chase);
        long front = GC.GetAllocatedBytesForCurrentThread();
        string result = null;
        for (int count = 0; count < 1000; count++) result = EnemyCombatTimeline.Describe(VillagerState.Chase);
        long allocate = GC.GetAllocatedBytesForCurrentThread() - front;
        Assert.AreEqual("追击", result); Assert.AreEqual(0, allocate);
    }

    private PathAgent CreateAgent() => Create("测试寻路代理").AddComponent<PathAgent>();
    private static object Context(PathAgent agent, int version)
    {
        var type = typeof(PathAgent).GetNestedType("RequestContext", BindingFlags.NonPublic);
        var value = Activator.CreateInstance(type, member, null, new object[] { agent }, null);
        Set(value, "version", version); Set(value, "startPoint", Vector3.zero); Set(value, "endPoint", new Vector3(2, 0, 0));
        return value;
    }
    private static ABPath Result()
    {
        // 只验证返回数据的交接，不启动寻路，也不占用插件的路径池。
        var path = new ABPath();
        path.vectorPath = new List<Vector3> { Vector3.zero, new Vector3(2, 0, 0) };
        return path;
    }
    [Test] public void PathResultCopyToSelfHasBufferAndDelayToUpdateHandoff()
    {
        var agent = CreateAgent(); Set(agent, "hasDestination", true); Set(agent, "requestVersion", 4);
        var path = Result(); InvokeValue(agent, "OnPathComplete", path, Context(agent, 4));
        Assert.IsFalse(agent.hasPath);
        path.vectorPath[1] = new Vector3(99, 0, 0);
        Assert.AreEqual(new Vector3(2, 0, 0), ((List<Vector3>)Read(agent, "pendingPath"))[1]);
        InvokeValue(agent, "ApplyPendingPath");
        Assert.IsTrue(agent.hasPath); Assert.AreEqual(0, ((List<Vector3>)Read(agent, "pendingPath")).Count);
    }
    [Test] public void ExpiredCallbackCannotOverrideNewTarget()
    {
        var agent = CreateAgent(); Set(agent, "hasDestination", true); Set(agent, "requestVersion", 5);
        InvokeValue(agent, "OnPathComplete", Result(), Context(agent, 4));
        Assert.AreEqual(0, ((List<Vector3>)Read(agent, "pendingPath")).Count);
    }
    [Test] public void StopAndDeactivateAllRejectLatePath()
    {
        var agent = CreateAgent(); Set(agent, "hasDestination", true); Set(agent, "requestVersion", 4);
        agent.isStopped = true;
        InvokeValue(agent, "OnPathComplete", Result(), Context(agent, agent.PathRequestVersion));
        Assert.AreEqual(0, ((List<Vector3>)Read(agent, "pendingPath")).Count);
        agent.isStopped = false; agent.enabled = false;
        InvokeValue(agent, "OnPathComplete", Result(), Context(agent, agent.PathRequestVersion));
        Assert.AreEqual(0, ((List<Vector3>)Read(agent, "pendingPath")).Count);
    }
    [Test] public void ClearPathPreserveBufferCapacityAndDuplicateStopIdempotent()
    {
        var agent = CreateAgent(); var current = (List<Vector3>)Read(agent, "path"); var pendingReceive = (List<Vector3>)Read(agent, "pendingPath");
        current.Add(Vector3.one); pendingReceive.Add(Vector3.one); agent.CancelPath();
        Assert.AreSame(current, Read(agent, "path")); Assert.AreSame(pendingReceive, Read(agent, "pendingPath"));
        Assert.GreaterOrEqual(current.Capacity, 64); Assert.AreEqual(-1, agent.CurrentWaypointIndex);
        agent.isStopped = true; int version = agent.PathRequestVersion; agent.isStopped = true;
        Assert.AreEqual(version, agent.PathRequestVersion);
    }

    [Test] public void FullCapacityTimeStateEventEvictCycleSnapshotAndCycleRecordMerge()
    {
        var sessionType = typeof(EnemyCombatTimeline).GetNestedType("WriteSession", BindingFlags.NonPublic);
        var itemType = typeof(EnemyCombatTimeline).GetNestedType("TimelineEntry", BindingFlags.NonPublic);
        string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "战斗队列测试_" + Guid.NewGuid() + ".txt");
        var session = Activator.CreateInstance(sessionType, member, null, new object[] { file }, null);
        var lockValue = Read(session, "queueLock");
        try
        {
            System.Threading.Monitor.Enter(lockValue);
            try
            {
                for (int sequence = 0; sequence < 4097; sequence++)
                {
                    var item = Activator.CreateInstance(itemType);
                    Set(item, "sequence", sequence); Set(item, "enemy", 1);
                    Set(item, "isSnapshot", sequence < 2);
                    InvokeValue(session, "Enqueue", item);
                }
                Assert.AreEqual(1, Read(session, "missedSnapshot"));
                var eventValue = Activator.CreateInstance(itemType); Set(eventValue, "sequence", 4097);
                InvokeValue(session, "Enqueue", eventValue);
                Assert.AreEqual(2, Read(session, "missedSnapshot"));
                Assert.AreEqual(0, Read(session, "missedEvent"));
                InvokeValue(session, "Enqueue", eventValue);
                Assert.AreEqual(1, Read(session, "missedEvent"));
            }
            finally { System.Threading.Monitor.Exit(lockValue); }
        }
        finally { InvokeValue(session, "Stop", true); if (System.IO.File.Exists(file)) System.IO.File.Delete(file); }
    }

    [Test] public void WriteFailureEndThreadAndNotContinueReceiveRecord()
    {
        var type = typeof(EnemyCombatTimeline).GetNestedType("WriteSession", BindingFlags.NonPublic);
        string file = System.IO.Path.GetTempFileName();
        try
        {
            var session = Activator.CreateInstance(type, member, null, new object[] { System.IO.Path.Combine(file, "禁止.txt") }, null);
            InvokeValue(session, "Stop", true);
            Assert.IsNotNull(Read(session, "error"));
            Assert.IsFalse(((System.Threading.Thread)Read(session, "thread")).IsAlive);
        }
        finally { System.IO.File.Delete(file); }
    }
    [Test]
    public void TimelineTextFieldsAppendValueWithoutDuplicatingExistingRecord()
    {
        Type type = typeof(EnemyCombatTimeline).Assembly.GetType("CombatDiagnosticFields");
        var builder = new System.Text.StringBuilder("记录：");
        var factory = type.GetMethod("Text", BindingFlags.Public | BindingFlags.Static);
        var append = type.GetMethod("AppendValue", BindingFlags.NonPublic | BindingFlags.Instance);
        object first = factory.Invoke(null, new object[] { "状态", "追击" });
        object second = factory.Invoke(null, new object[] { "原因", "窗口排队" });
        append.Invoke(first, new object[] { builder });
        append.Invoke(second, new object[] { builder });
        Assert.AreEqual("记录：状态=追击，原因=窗口排队，", builder.ToString());
    }
}
