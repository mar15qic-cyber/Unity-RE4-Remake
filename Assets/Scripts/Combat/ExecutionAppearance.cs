using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class ExecutionAppearance
{
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<SkinnedMeshRenderer> attached = new List<SkinnedMeshRenderer>();
    private readonly List<SkinnedMeshRenderer> separated = new List<SkinnedMeshRenderer>();
    private readonly List<Mesh> snapshots = new List<Mesh>();
    private readonly List<GameObject> fragments = new List<GameObject>();

    private PoseHalf firstRig;
    private PoseHalf secondRig;

    internal Transform FirstRoot => firstRig.root;
    internal Transform SecondRoot => secondRig.root;

    private sealed class PoseHalf
    {
        internal readonly Transform root;
        private readonly Transform actor;
        private readonly Dictionary<Transform, Transform> copies = new Dictionary<Transform, Transform>();

        internal PoseHalf(Transform source, string name)
        {
            actor = source;
            root = new GameObject(name).transform;
            root.SetParent(actor, false);
        }

        internal Transform[] Bind(Transform[] bones, int[] joints)
        {
            foreach (int index in joints)
            {
                var source = bones[index];
                if (!copies.TryGetValue(source, out var copy))
                {
                    copy = new GameObject(source.name).transform;
                    copy.SetParent(root, false);
                    copies.Add(source, copy);
                }
                bones[index] = copy;
            }
            return bones;
        }

        internal void Pose(Vector3 pivot, Quaternion turn, Vector3 shift)
        {
            root.SetPositionAndRotation(pivot + shift + turn * (actor.position - pivot), turn * actor.rotation);
            var inverseRotation = Quaternion.Inverse(actor.rotation);
            var scale = actor.lossyScale;
            foreach (var item in copies)
            {
                var source = item.Key;
                var copy = item.Value;
                copy.SetLocalPositionAndRotation(actor.InverseTransformPoint(source.position), inverseRotation * source.rotation);
                var size = source.lossyScale;
                copy.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            }
        }
    }

    internal bool Apply(Transform actor, ExecutionGeometry.Section[] sections, Material interior, bool independent = false)
    {
        if (independent)
        {
            firstRig = new PoseHalf(actor, "里昂处决左半身");
            secondRig = new PoseHalf(actor, "里昂处决右半身");
        }
        var sources = new Dictionary<Mesh,ExecutionGeometry.Section>();
        foreach (var section in sections)
        {
            sources[section.original] = section;
        }
        foreach (var original in actor.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!original.enabled || original.forceRenderingOff || original.sharedMesh == null ||
                !sources.TryGetValue(original.sharedMesh, out var section))
            {
                continue;
            }
            AddHalf(original, section.retained, section.cutMaterials, interior, attached, firstRig, section.retainedJoints);
            AddHalf(original, section.separated, section.cutMaterials, interior, separated, secondRig, section.separatedJoints);
            hidden.Add(original);
            original.forceRenderingOff = true;
        }
        if (independent)
        {
            MoveHalves(actor.position, Quaternion.identity, Vector3.zero, Quaternion.identity, Vector3.zero);
        }
        if (hidden.Count == 0)
        {
            Clear();
            return false;
        }
        return true;
    }

    private static void AddHalf(SkinnedMeshRenderer source, Mesh mesh, Material[] cutMaterials, Material interior, List<SkinnedMeshRenderer> side,
        PoseHalf pose, int[] joints)
    {
        if (mesh == null)
        {
            return;
        }
        var node = new GameObject(mesh.name);
        node.layer = source.gameObject.layer;
        var t = node.transform;
        t.SetParent(source.transform, false);
        var copy = node.AddComponent<SkinnedMeshRenderer>();
        copy.sharedMesh = mesh;
        var bones = source.bones;
        if (pose != null)
        {
            pose.Bind(bones, joints);
        }
        copy.bones = bones;
        copy.rootBone = source.rootBone;
        copy.quality = source.quality;
        copy.updateWhenOffscreen = true;
        var bounds = source.localBounds;
        bounds.Expand(bounds.size.magnitude * .6f);
        copy.localBounds = bounds;
        var materials = new List<Material>(source.sharedMaterials);
        if (mesh.subMeshCount > source.sharedMesh.subMeshCount)
        {
            if (cutMaterials != null && cutMaterials.Length == mesh.subMeshCount - source.sharedMesh.subMeshCount)
            {
                materials.AddRange(cutMaterials);
            }
            else
            {
                // 旧版分件资源只有一个断面材质槽。
                materials.Add(interior);
            }
        }
        copy.sharedMaterials = materials.ToArray();
        copy.shadowCastingMode = source.shadowCastingMode;
        copy.receiveShadows = source.receiveShadows;
        copy.probeAnchor = source.probeAnchor;
        side.Add(copy);
    }

    internal void MoveHalves(Vector3 pivot, Quaternion firstTurn, Vector3 firstShift, Quaternion secondTurn, Vector3 secondShift)
    {
        firstRig.Pose(pivot, firstTurn, firstShift);
        secondRig.Pose(pivot, secondTurn, secondShift);
    }

    internal void SetOpening(float retainedWeight, float separatedWeight)
    {
        SetWeight(attached, retainedWeight);
        SetWeight(separated, separatedWeight);
    }

    private static void SetWeight(List<SkinnedMeshRenderer> renderers, float weight)
    {
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled && renderer.sharedMesh.blendShapeCount > 0)
            {
                renderer.SetBlendShapeWeight(0, weight);
            }
        }
    }

    // 固定在当前蒙皮姿势，两半随后可由各自的根独立移动。
    internal Transform Detach(bool second, Vector3 pivot, Scene scene)
    {
        var list = attached;
        if (second)
        {
            list = separated;
        }
        var piece = new GameObject("处决分件");
        SceneManager.MoveGameObjectToScene(piece, scene);
        piece.transform.position = pivot;
        fragments.Add(piece);
        foreach (var skin in list)
        {
            if (skin == null)
            {
                continue;
            }
            var baked = new Mesh { name = skin.name };
            skin.BakeMesh(baked, false);
            snapshots.Add(baked);
            var child = new GameObject(skin.name);
            child.layer = skin.gameObject.layer;
            child.transform.SetParent(piece.transform, false);
            child.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            child.transform.localScale = skin.transform.lossyScale;
            child.AddComponent<MeshFilter>().sharedMesh = baked;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = skin.sharedMaterials;
            renderer.shadowCastingMode = skin.shadowCastingMode;
            renderer.receiveShadows = skin.receiveShadows;
            skin.enabled = false;
        }
        return piece.transform;
    }

    internal void Clear()
    {
        foreach (var source in hidden)
        {
            if (source != null)
            {
                source.forceRenderingOff = false;
            }
        }
        foreach (var skin in attached)
        {
            if (skin != null)
            {
                skin.gameObject.SetActive(false);
                Object.Destroy(skin.gameObject);
            }
        }
        foreach (var skin in separated)
        {
            if (skin != null)
            {
                skin.gameObject.SetActive(false);
                Object.Destroy(skin.gameObject);
            }
        }
        foreach (var fragment in fragments)
        {
            if (fragment != null)
            {
                fragment.SetActive(false);
                Object.Destroy(fragment);
            }
        }
        foreach (var mesh in snapshots)
        {
            Object.Destroy(mesh);
        }
        if (firstRig != null)
        {
            Object.Destroy(firstRig.root.gameObject);
            Object.Destroy(secondRig.root.gameObject);
            firstRig = null;
            secondRig = null;
        }
        hidden.Clear();
        attached.Clear();
        separated.Clear();
        fragments.Clear();
        snapshots.Clear();
    }
}
