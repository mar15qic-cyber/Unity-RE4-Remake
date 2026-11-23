using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ExecutionGeometryTests
{
    [Test]
    public void EveryPlayerSurfaceHasAWeightedReplacement()
    {
        var bank = Resources.Load<ExecutionGeometry>("Combat/ExecutionGeometry");
        Assert.That(bank, Is.Not.Null);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/角色/里昂/LeonR4R.fbx");
        var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        CollectionAssert.AreEquivalent(renderers.Select(renderer => renderer.sharedMesh),
            bank.leon.Select(section => section.original));
        foreach (var section in bank.leon)
        {
            if (section.retained != null)
            {
                Assert.That(section.retainedJoints, Is.Not.Empty);
            }
            if (section.separated != null)
            {
                Assert.That(section.separatedJoints, Is.Not.Empty);
            }
            foreach (var mesh in new[] { section.retained, section.separated })
            {
                if (mesh == null)
                {
                    continue;
                }
                Assert.That(mesh.bindposes.Length, Is.EqualTo(section.original.bindposes.Length));
                Assert.That(mesh.GetBlendShapeFrameCount(0), Is.EqualTo(4));
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            }
        }
        var jacket = bank.leon.Single(section => section.original.name.StartsWith("24_jacket_"));
        var hands = bank.leon.Single(section => section.original.name.StartsWith("24_body_"));
        foreach (var half in new[] { hands.retained, hands.separated })
        {
            var offsets = new Vector3[half.vertexCount];
            half.GetBlendShapeFrameVertices(0, 3, offsets, null, null);
            Assert.That(offsets.Max(offset => offset.magnitude), Is.LessThan(.034f),
                "双手应随左右半身刚性错开，不能通过指骨形态键拉伸。");
        }
        CollectionAssert.AreEqual(new[] { "JacketEdge", "JacketLining", "Interior" },
            jacket.cutMaterials.Select(material => material.name));
        var jacketRenderer = renderers.Single(renderer => renderer.sharedMesh == jacket.original);
        var toActor = source.transform.worldToLocalMatrix * jacketRenderer.transform.localToWorldMatrix;
        foreach (var half in new[] { jacket.retained, jacket.separated })
        {
            int firstSlot = jacket.original.subMeshCount;
            Assert.That(half.subMeshCount, Is.EqualTo(firstSlot + 3));
            Assert.That(half.GetIndexCount(firstSlot), Is.GreaterThan(0));
            Assert.That(half.GetIndexCount(firstSlot + 1), Is.GreaterThan(0));
            int[] tissue = half.GetTriangles(firstSlot + 2);
            var vertices = half.vertices;
            var lateral = tissue.Distinct().Select(i => toActor.MultiplyPoint3x4(vertices[i]).x).ToArray();
            Assert.That(lateral.Max() - lateral.Min(), Is.GreaterThan(.035f), "身体断面须有侧壁厚度。");
            var incidence = new System.Collections.Generic.Dictionary<(int, int), int>();
            for (int i = 0; i < tissue.Length; i += 3)
            {
                for (int j = 0; j < 3; j++)
                {
                    int a = tissue[i + j];
                    int b = tissue[i + (j + 1) % 3];
                    var edge = (Math.Min(a, b), Math.Max(a, b));
                    incidence.TryGetValue(edge, out int count);
                    incidence[edge] = count + 1;
                }
            }
            Assert.That(incidence.Values.All(count => count == 2), Is.True, "断面网格须闭合。");
        }
    }

    [Test]
    public void ShotgunUsesCommittedDamageAcrossPelletsAndRequiresDeath()
    {
        var type = typeof(WeaponBase).Assembly.GetType("ShotgunTerminalHit", true);
        var shot = Activator.CreateInstance(type);
        var record = type.GetMethod("Record", BindingFlags.Instance | BindingFlags.NonPublic);
        var select = type.GetMethod("TrySelect", BindingFlags.Instance | BindingFlags.NonPublic);
        var result = new object[] { null, null };
        record.Invoke(shot, new object[] { HitboxType.Head, 8f, Vector3.up, false });
        record.Invoke(shot, new object[] { HitboxType.LeftArm, 6f, Vector3.left, false });
        Assert.That(select.Invoke(shot, result), Is.False);
        record.Invoke(shot, new object[] { HitboxType.LeftArm, 4f, Vector3.left, true });
        Assert.That(select.Invoke(shot, result), Is.True);
        Assert.That(result[0], Is.EqualTo(HitboxType.LeftArm));
        record.Invoke(shot, new object[] { HitboxType.Chest, 12f, Vector3.zero, true });
        Assert.That(select.Invoke(shot, result), Is.False);
    }

    [TestCase("MainGanadoFemale")]
    [TestCase("MainGanadoArmed")]
    public void OfficialVillagerCutsMatchVisibleParts(string prefabName)
    {
        var bank = Resources.Load<ExecutionGeometry>("Combat/ExecutionGeometry");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RE4ChapterOneAnimationLoop/Generated/" + prefabName + ".prefab");
        var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(skin => skin.enabled).ToArray();
        foreach (var cut in new[] { bank.head, bank.leftArm, bank.rightArm })
        {
            Assert.That(cut.Select(section => section.original).Distinct().Count(), Is.EqualTo(cut.Length));
            var matched = cut.Where(section => skins.Any(skin => skin.sharedMesh == section.original)).ToArray();
            Assert.That(matched.Any(section => section.retained != null && section.separated != null &&
                section.retained.subMeshCount > section.original.subMeshCount &&
                section.separated.subMeshCount > section.original.subMeshCount), Is.True);
        }
        foreach (var hair in skins.Where(s => s.name.EndsWith("__Hair_Mat", StringComparison.Ordinal)))
        {
            var section = bank.head.Single(s => s.original == hair.sharedMesh);
            Assert.That(section.separated, Is.SameAs(hair.sharedMesh));
            Assert.That(section.retained, Is.Null);
        }
        foreach (var side in new[] { HitboxType.LeftArm, HitboxType.RightArm })
        {
            var cut = bank.leftArm;
            string sleeve = "__L_Arm_Mat";
            string other = "__R_Arm_Mat";
            if (side == HitboxType.RightArm)
            {
                cut = bank.rightArm;
                sleeve = "__R_Arm_Mat";
                other = "__L_Arm_Mat";
            }
            var matchingSleeve = skins.Single(s => s.name.EndsWith(sleeve, StringComparison.Ordinal));
            Assert.That(cut.Any(s => s.original == matchingSleeve.sharedMesh && s.separated != null), Is.True);
            var protectedMeshes = skins.Where(s => s.name.EndsWith(other, StringComparison.Ordinal) ||
                s.name.EndsWith("__Skirt_Mat", StringComparison.Ordinal)).Select(s => s.sharedMesh).ToArray();
            Assert.That(cut.Any(s => protectedMeshes.Contains(s.original)), Is.False);
        }
        var holder = prefab.GetComponent<EnemyHeldWeapon>();
        if (holder != null)
        {
            var held = (BoxCollider)new SerializedObject(holder).FindProperty("droppedCollider").objectReferenceValue;
            Assert.That(held.transform.parent.name, Is.EqualTo("R_Wep"));
            var weaponMeshes = held.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.sharedMesh).ToArray();
            Assert.That(weaponMeshes, Is.Not.Empty);
            Assert.That(bank.head.Concat(bank.leftArm).Concat(bank.rightArm).Any(s => weaponMeshes.Contains(s.original)), Is.False);
        }
    }

    [Test]
    public void EveryVillageCutHasASeparatedPartAndAClosedStump()
    {
        var bank = Resources.Load<ExecutionGeometry>("Combat/ExecutionGeometry");
        foreach (var sections in new[] { bank.head, bank.leftArm, bank.rightArm })
        {
            Assert.That(sections.Any(section => section.separated != null), Is.True);
            Assert.That(sections.Any(section => section.retained != null &&
                section.retained.subMeshCount > section.original.subMeshCount), Is.True);
        }
        Assert.That(bank.bloodMark.GetComponent<Collider>(), Is.Null);
        Assert.That(bank.interior.mainTexture, Is.Not.Null);
    }
}
