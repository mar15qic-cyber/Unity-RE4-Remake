using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameArchitecture;
using NUnit.Framework;
using Pathfinding;
using UnityEditor;
using UnityEngine;

public sealed class EnemyTacticalCombatTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Vector3 Arena = new Vector3(2000f, 100f, 2000f);
    private readonly List<GameObject> objects = new();
    private EnemyAIManager manager;

    [SetUp]
    public void SetUp()
    {
        manager = CreateObject("敌群调度验证").AddComponent<EnemyAIManager>();
        typeof(EnemyAIManager).GetProperty(nameof(EnemyAIManager.IsCombatUnlocked), InstanceFields).SetValue(manager, true);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(objects[index]);
        }

        objects.Clear();
    }

    [Test]
    public void OccupiedAttackSlotsDoNotStopOrbitAtArrival()
    {
        AstarPath previous = AstarPath.active;
        var astarObject = CreateObject("外围寻路验证");
        var astar = astarObject.AddComponent<AstarPath>();
        try
        {
            AstarPath.active = astar;
            GridGraph graph = astar.data.AddGraph<GridGraph>();
            graph.center = Arena;
            graph.SetDimensions(41, 41, 0.25f);
            graph.collision.heightCheck = false;
            graph.collision.collisionCheck = false;
            astar.Scan(graph);

            var ground = CreateObject("外围走位地面");
            ground.transform.position = Arena - Vector3.up * 0.25f;
            var floor = ground.AddComponent<BoxCollider>();
            floor.size = new Vector3(12f, 0.5f, 12f);
            Physics.SyncTransforms();

            VillagerController enemy = CreateEnemy(Arena + Vector3.forward * 2.5f);
            manager.RegisterEnemy(enemy);
            Invoke("ReleaseEngagementOnHit", enemy);
            Assert.That(Invoke("CanEngage", enemy), Is.True, "测试村民已放行接战");
            Vector3 probe = enemy.transform.position + Vector3.right;
            Assert.That(enemy.TryResolveReachableNavigationPoint(probe, out var reachable), Is.True, "测试场地有可达节点");
            Assert.That(enemy.CanMoveDirectlyToNavigationPoint(reachable), Is.True, "测试场地有连续可走地面");
            var attackers = (HashSet<VillagerController>)ReadField("attackingEnemies");
            attackers.Add(CreateEnemy(Arena + Vector3.forward));
            attackers.Add(CreateEnemy(Arena + Vector3.back));
            Assert.That(Invoke("CanOpenAttackSlot", enemy), Is.False);

            Vector3 first = manager.GetCombatDestination(enemy, Arena);
            Assert.That(Vector3.Distance(first, enemy.transform.position), Is.GreaterThan(0.35f));
            enemy.transform.position = first;
            Physics.SyncTransforms();
            IDictionary tactics = (IDictionary)ReadField("tactics");
            object state = tactics[enemy];
            state.GetType().GetField("nextRouteCheck", InstanceFields).SetValue(state, float.NegativeInfinity);

            Vector3 next = manager.GetCombatDestination(enemy, Arena);
            Assert.That(Vector3.Distance(next, first), Is.GreaterThan(0.35f));
            Assert.That(attackers.Count, Is.EqualTo(2));
        }
        finally
        {
            objects.Remove(astarObject);
            Object.DestroyImmediate(astarObject);
            AstarPath.active = previous;
        }
    }

    [Test]
    public void ExpiredQTEWaitLeavesOneOrdinaryAttackInterval()
    {
        VillagerController enemy = CreateEnemy(Arena);
        WriteField("pendingQTEEnemy", enemy);
        WriteField("pendingQTEExpiresAt", Time.time - 1f);

        Assert.That(Invoke("HasPendingQTERequest"), Is.False);
        Assert.That(ReadField("pendingQTEEnemy"), Is.Null);
        Assert.That((float)ReadField("nextQTEPriorityAllowedTime"), Is.GreaterThanOrEqualTo(Time.time + 0.79f));
    }

    [Test]
    public void ExpiredActionCannotReleaseCurrentAttack()
    {
        VillagerController enemy = CreateEnemy(Arena);
        manager.RegisterEnemy(enemy);
        var attacks = (HashSet<VillagerController>)ReadField("attackingEnemies");
        attacks.Add(enemy);
        var identity = new ObjectIdentity(enemy.GetInstanceID(), 1);
        var old = new ActionHandle(identity, 1);
        var current = new ActionHandle(identity, 2);
        Invoke("ConfirmAttack", enemy, current);
        Assert.That((float)ReadField("nextAttackAllowedTime"), Is.GreaterThanOrEqualTo(Time.time + 0.79f));

        Invoke("ReleaseAttack", enemy, old);
        Assert.That(attacks.Contains(enemy), Is.True);
        Invoke("ReleaseAttack", enemy, current);
        Assert.That(attacks.Contains(enemy), Is.False);
    }

    [Test]
    public void OrbitSegmentCannotCutThroughPlayerAttackRadius()
    {
        MethodInfo method = typeof(EnemyAIManager).GetMethod("IsSafeTacticalSegment", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method.Invoke(null, new object[] { Vector3.left * 2f, Vector3.right * 2f, Vector3.zero, 1f }), Is.False);
        Assert.That(method.Invoke(null, new object[] { Vector3.right * 0.5f, Vector3.right * 2f, Vector3.zero, 1f }), Is.True);
        Assert.That(method.Invoke(null, new object[] { Vector3.right * 0.5f, Vector3.left * 2f, Vector3.zero, 1f }), Is.False);
    }

    [Test]
    public void InterceptionUsesWalkableDetourAndRejectsRouteThroughPlayer()
    {
        MethodInfo method = typeof(EnemyAIManager).GetMethod("TryMeasureInterceptionRoute", BindingFlags.Static | BindingFlags.NonPublic);
        Vector3 left = Vector3.left * 3f;
        Vector3 right = Vector3.right * 3f;
        var throughPlayer = new object[] { new[] { left, right }, Vector3.zero, 0f };
        Assert.That(method.Invoke(null, throughPlayer), Is.False);

        var detour = new object[]
        {
            new[] { left, left + Vector3.forward * 2f, right + Vector3.forward * 2f, right },
            Vector3.zero,
            0f
        };
        Assert.That(method.Invoke(null, detour), Is.True);
        Assert.That((float)detour[2], Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void FailedInterceptionReleasesOnlyItsOwnApproach()
    {
        VillagerController left = CreateEnemy(Arena + Vector3.left * 4f);
        VillagerController right = CreateEnemy(Arena + Vector3.right * 4f);
        var assignments = (VillagerController[])ReadField("interceptionEnemies");
        assignments[0] = left;
        assignments[1] = right;

        manager.InvalidatePursuitDestination(left);

        Assert.That(assignments[0], Is.Null);
        Assert.That(assignments[1], Is.SameAs(right));
        var retry = (Dictionary<VillagerController, float>)ReadField("interceptionRetryAt");
        Assert.That(retry[left], Is.GreaterThan(Time.time));
    }

    private VillagerController CreateEnemy(Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RE4ChapterOneAnimationLoop/Generated/MainGanadoArmed.prefab");
        GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
        objects.Add(instance);
        var enemy = instance.GetComponent<VillagerController>();
        typeof(CharacterHealthModule).GetMethod("Initialize", InstanceFields).Invoke(enemy.healthModule, new object[] { enemy.maxHealth });
        typeof(VillagerController).GetField("movementCollider", InstanceFields)
            .SetValue(enemy, instance.GetComponent<CapsuleCollider>());
        return enemy;
    }

    private GameObject CreateObject(string objectName)
    {
        var instance = new GameObject(objectName);
        objects.Add(instance);
        return instance;
    }

    private object Invoke(string method, params object[] arguments)
    {
        return typeof(EnemyAIManager).GetMethod(method, InstanceFields).Invoke(manager, arguments);
    }

    private object ReadField(string name)
    {
        return typeof(EnemyAIManager).GetField(name, InstanceFields).GetValue(manager);
    }

    private void WriteField(string name, object value)
    {
        typeof(EnemyAIManager).GetField(name, InstanceFields).SetValue(manager, value);
    }
}
