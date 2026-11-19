using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class VillagerKnifeContactTests
{
    private delegate bool Sweep(Vector3 start, Vector3 end, float sweepRadius, Vector3 bottom, Vector3 top,
        float capsuleRadius, out float fraction);

    private static readonly Sweep SweepCapsule = (Sweep)Delegate.CreateDelegate(typeof(Sweep),
        typeof(EnemyCombatModule).GetMethod("TrySweepKnifeCapsule", BindingFlags.Static | BindingFlags.NonPublic));

    [Test]
    public void CrossingBetweenSamplesUsesFirstContact()
    {
        bool hit = SweepCapsule(new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), .1f,
            Vector3.up * .35f, Vector3.up * 1.4f, .35f, out float fraction);

        Assert.IsTrue(hit);
        Assert.That(fraction, Is.EqualTo(.275f).Within(.001f));
    }

    [Test]
    public void CapsulePaddingCatchesOnlyNearEdge()
    {
        Vector3 bottom = Vector3.up * .35f;
        Vector3 top = Vector3.up * 1.4f;
        Vector3 start = new Vector3(-1f, 1f, .49f);
        Vector3 end = new Vector3(1f, 1f, .49f);
        Assert.IsFalse(SweepCapsule(start, end, .12f, bottom, top, .35f, out _));
        Assert.IsTrue(SweepCapsule(start, end, .15f, bottom, top, .35f, out _));
        Assert.IsFalse(SweepCapsule(start, end, .17f, bottom + Vector3.up * 3f, top + Vector3.up * 3f, .35f, out _));
    }

    [Test]
    public void BladeTipSweepsBetweenFramesBeforeBladeCenter()
    {
        Type type = typeof(EnemyCombatModule).Assembly.GetType("MeleeCapsule");
        Assert.IsNotNull(type);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ConstructorInfo constructor = type.GetConstructor(flags, null, new[] { typeof(Vector3), typeof(Vector3), typeof(float) }, null);
        var first = constructor.Invoke(new object[] { new Vector3(-1f, 0f, 0f), new Vector3(-1f, 0f, 1f), .04f });
        var last = constructor.Invoke(new object[] { new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f), .04f });
        var near = constructor.Invoke(new object[] { new Vector3(-.15f, 0f, 1f), new Vector3(-.15f, 0f, 1f), .16f });
        var far = constructor.Invoke(new object[] { new Vector3(.15f, 0f, 1f), new Vector3(.15f, 0f, 1f), .16f });
        MethodInfo sweep = type.GetMethod("TrySweep", flags);
        var nearResult = new[] { last, near, near, 0f, Vector3.zero, Vector3.zero, 0f };
        var farResult = new[] { last, far, far, 0f, Vector3.zero, Vector3.zero, 0f };

        Assert.IsTrue((bool)sweep.Invoke(first, nearResult));
        Assert.IsTrue((bool)sweep.Invoke(first, farResult));
        Assert.That((float)nearResult[3], Is.EqualTo(.325f).Within(.001f));
        Assert.That((float)nearResult[3], Is.LessThan((float)farResult[3]));
        Assert.That((float)nearResult[6], Is.EqualTo(1f).Within(.001f));
    }

    [TestCase("FemaleGanardEnemy")]
    [TestCase("FemaleGanard2Enemy")]
    public void AuthoredSwingsReachStationaryLeonAtCalibratedDistances(string prefabName)
    {
        string path = $"Assets/Prefab/Enemies/{prefabName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(prefab);
        var serialized = new SerializedObject(prefab.GetComponent<EnemyCombatModule>());
        SerializedProperty profiles = serialized.FindProperty("knifeAttackConfiguration");
        Assert.That(profiles.arraySize, Is.EqualTo(3));

        for (int index = 0; index < profiles.arraySize; index++)
        {
            var profile = (KnifeEnemyAttackProfile)profiles.GetArrayElementAtIndex(index).boxedValue;
            Assert.That(profile.maximumStartDistance, Is.InRange(.78f, 1.55f));
            Assert.That(profile.contactPadding, Is.InRange(0f, .05f));
            Assert.That(profile.planarRootTrajectory.Length, Is.GreaterThan(1));
            float midpoint = (.78f + profile.maximumStartDistance) * .5f;
            foreach (float distance in new[] { .78f, midpoint, profile.maximumStartDistance })
            {
                Assert.IsTrue(ReachesLeon(profile, distance, 0f), $"{profile.sourceClip.name} / {distance:F2} 米");
                Assert.IsFalse(ReachesLeon(profile, distance, 1.8f), $"{profile.sourceClip.name} / 横移");
            }
        }
    }

    [Test]
    public void ProductionWaveUsesTheSameClipsForVisibleSwingAndContact()
    {
        var scene = EditorSceneManager.OpenPreviewScene(SceneManagerEx.MainScenePath);
        try
        {
            ChapterOneMissionManager mission = null;
            KnifeCombatModule knife = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (mission == null)
                {
                    mission = root.GetComponentInChildren<ChapterOneMissionManager>(true);
                }

                if (knife == null)
                {
                    knife = root.GetComponentInChildren<KnifeCombatModule>(true);
                }
            }

            Assert.IsNotNull(mission);
            Assert.IsNotNull(knife);
            Assert.That(knife.configuration.tapWindow, Is.EqualTo(.30f).Within(.001f));
            Assert.That(knife.configuration.perfectWindow, Is.EqualTo(.11f).Within(.001f));
            Assert.That(mission.ganadoPrefabs, Is.Not.Empty);
            foreach (GameObject prefab in mission.ganadoPrefabs)
            {
                Assert.IsNotNull(prefab);
                RuntimeAnimatorController controller = prefab.GetComponent<Animator>().runtimeAnimatorController;
                var serialized = new SerializedObject(prefab.GetComponent<EnemyCombatModule>());
                RuntimeAnimatorController unarmedController = controller;
                if (prefab.TryGetComponent(out EnemyHeldWeapon weapon))
                {
                    unarmedController = (RuntimeAnimatorController)new SerializedObject(weapon)
                        .FindProperty("unarmedController").objectReferenceValue;
                    CheckProfiles(serialized.FindProperty("standingWeaponAttackProfiles"), controller, prefab.transform);
                }

                CheckProfiles(serialized.FindProperty("knifeAttackConfiguration"), unarmedController, prefab.transform);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckProfiles(SerializedProperty profiles, RuntimeAnimatorController controller, Transform root)
    {
        Assert.That(profiles.arraySize, Is.EqualTo(3), controller.name);
        for (int index = 0; index < profiles.arraySize; index++)
        {
            var profile = (KnifeEnemyAttackProfile)profiles.GetArrayElementAtIndex(index).boxedValue;
            AnimationClip clip = KnifeContactProfileBuilder.FindProductionAttackClip(controller, index);
            Assert.That(profile.sourceClip, Is.SameAs(clip));
            Assert.IsTrue(profile.usePoseContact, clip.name);
            Assert.That(profile.maximumStartDistance, Is.GreaterThan(.7f));
            Assert.That(profile.planarRootTrajectory.Length, Is.GreaterThan(1));
            Assert.That(profile.promptEarly, Is.GreaterThanOrEqualTo(.25f));
            Assert.That(profile.contactSegments.Length, Is.GreaterThan(0));
            foreach (KnifeEnemyAttackSegment segment in profile.contactSegments)
            {
                Assert.That(segment.end, Is.LessThanOrEqualTo(1.05f), clip.name);
                Assert.That(segment.localPoints.Length, Is.GreaterThan(2));
                Assert.That(segment.localEndPoints.Length, Is.EqualTo(segment.localPoints.Length));
                Transform contactBone = root.Find(segment.contactBonePath);
                Assert.IsNotNull(contactBone, clip.name);
                Vector3 worldAxis = contactBone.TransformVector(segment.contactLocalEnd - segment.contactLocalStart);
                Assert.That(worldAxis.magnitude, Is.GreaterThan(.01f), clip.name);
            }
        }
    }

    [Test]
    public void HudLateUpdateRunsAfterIncomingContacts()
    {
        int enemyOrder = ReadExecutionOrder(typeof(EnemyCombatModule));
        int contactOrder = ReadExecutionOrder(typeof(LeonAnimatorDriver));
        int hudOrder = ReadExecutionOrder(typeof(CombatHUD));

        Assert.IsNotNull(typeof(CombatHUD).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.That(contactOrder, Is.GreaterThan(enemyOrder));
        Assert.That(hudOrder, Is.GreaterThan(contactOrder));
    }

    private static int ReadExecutionOrder(Type type)
    {
        var order = type.GetCustomAttribute<DefaultExecutionOrder>();
        if (order == null)
        {
            return 0;
        }

        return order.order;
    }

    private static bool ReachesLeon(KnifeEnemyAttackProfile profile, float distance, float lateral)
    {
        Vector3 bottom = new Vector3(lateral, .30f, distance);
        Vector3 top = new Vector3(lateral, 1.35f, distance);
        foreach (KnifeEnemyAttackSegment segment in profile.contactSegments)
        {
            Vector3 previous = HandAt(profile, segment, 0, distance);
            for (int index = 1; index < segment.localPoints.Length; index++)
            {
                Vector3 current = HandAt(profile, segment, index, distance);
                if (SweepCapsule(previous, current, segment.radius + profile.contactPadding, bottom, top, .35f, out _))
                {
                    return true;
                }

                previous = current;
            }
        }

        return false;
    }

    private static Vector3 HandAt(KnifeEnemyAttackProfile profile, KnifeEnemyAttackSegment segment, int index, float distance)
    {
        float time = Mathf.Lerp(segment.begin, segment.end, (float)index / (segment.localPoints.Length - 1));
        float sample = time / KnifeEnemyAttackProfile.RootSampleInterval;
        int previous = Mathf.Min(Mathf.FloorToInt(sample), profile.planarRootTrajectory.Length - 2);
        Vector3 root = Vector3.Lerp(profile.planarRootTrajectory[previous], profile.planarRootTrajectory[previous + 1], sample - previous);
        root.z = Mathf.Min(root.z, Mathf.Max(0f, distance - .70f));
        return root + segment.localPoints[index];
    }
}
