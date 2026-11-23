using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class ExecutionGeometryBuilder
{
    private static void AppendVillagerCuts(string prefabName, List<Mesh> generated,
        ref ExecutionGeometry.Section[] head, ref ExecutionGeometry.Section[] left, ref ExecutionGeometry.Section[] right)
    {
        string path = "Assets/RE4ChapterOneAnimationLoop/Generated/" + prefabName + ".prefab";
        var actor = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (actor == null)
        {
            throw new InvalidOperationException("缺少正式村民模板 " + path);
        }
        head = MergeCut(head, Vector3.up, "Neck_1");
        left = MergeCut(left, Vector3.left, "L_UpperArm");
        right = MergeCut(right, Vector3.right, "R_UpperArm");

        ExecutionGeometry.Section[] MergeCut(ExecutionGeometry.Section[] existing, Vector3 direction, string joint)
        {
            var pieces = BuildSections(actor, direction, Vector3.zero, false, prefabName + "_" + joint,
                generated, cutJoint: joint);
            if (pieces.Length == 0 || !pieces.Any(section => section.retained != null && section.separated != null))
            {
                throw new InvalidOperationException(actor.name + " 未生成完整分件 " + joint);
            }
            return existing.Concat(pieces).ToArray();
        }
    }

    private static bool TryGetVillagerCut(SkinnedMeshRenderer skin, Matrix4x4 toActor, string jointName,
        Vector3 direction, out Vector3 origin, out bool detachWhole)
    {
        origin = Vector3.zero;
        detachWhole = false;
        // 武器有自己的蒙皮渲染器，网格和掉落均归持械组件。
        for (Transform node = skin.transform; node != null; node = node.parent)
        {
            if (node.name == "R_Wep" || node.name == "L_Wep")
            {
                return false;
            }
        }
        Transform[] bones = skin.bones;
        int joint = Array.FindIndex(bones, bone => bone != null && bone.name == jointName);
        if (joint < 0)
        {
            return false;
        }
        bool head = jointName == "Neck_1";
        if (head && (skin.name.EndsWith("__Hair_Mat", StringComparison.Ordinal) ||
            skin.name.Contains("__Beard") || skin.name.Contains("__Eyebrow") || skin.name.Contains("__Eyelash")))
        {
            detachWhole = true;
            return true;
        }
        var branch = bones[joint];
        if (head && branch.parent != null && branch.parent.name == "Neck_0")
        {
            branch = branch.parent;
        }
        // 裙摆、围裙和另一侧肢体也可能越过肩部切平面，只处理实际由该部位驱动的网格。
        var influenced = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            influenced[i] = bones[i] != null && bones[i].IsChildOf(branch);
        }
        foreach (var weight in skin.sharedMesh.GetAllBoneWeights())
        {
            if (weight.weight <= 0f || !influenced[weight.boneIndex])
            {
                continue;
            }
            // 顶点处于绑定姿势；持械模板在编辑器里已经摆好了动作。
            var jointInMesh = skin.sharedMesh.bindposes[joint].inverse.MultiplyPoint3x4(Vector3.zero);
            float offset = .055f;
            if (head)
            {
                offset = .018f;
            }
            origin = toActor.MultiplyPoint3x4(jointInMesh) + direction * offset;
            return true;
        }
        return false;
    }
}
