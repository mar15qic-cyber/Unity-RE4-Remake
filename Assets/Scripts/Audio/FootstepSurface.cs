using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootstepSurface : MonoBehaviour
{
    [Tooltip("整个碰撞体的默认地表")]
    public SurfaceType surfaceType = SurfaceType.Mud;
    [Tooltip("按渲染网格的子网格编号配置地表")]
    public SurfaceType[] submeshSurfaces = Array.Empty<SurfaceType>();


    [Tooltip("从原版渲染地表投影到碰撞物体局部 XZ 的材质索引图。")]
    [SerializeField] private Texture2D terrainIndex;
    [SerializeField] private Rect terrainIndexBounds;
    [SerializeField] private SurfaceType[] terrainIndexSurfaces = Array.Empty<SurfaceType>();

    private Mesh cachedMesh;
    private int[] triangleEnds;
    private static readonly RaycastHit[] groundHits = new RaycastHit[16];

    public static bool TryFindGround(Transform actor, float distance, LayerMask layers, out RaycastHit ground)
    {
        return TryFindGround(actor, actor.position, distance, layers, out ground);
    }

    public static bool TryFindGround(Transform actor, Vector3 position, float distance, LayerMask layers, out RaycastHit ground)
    {
        Vector3 origin = position + Vector3.up * 0.2f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, layers, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = groundHits;
        if (count == groundHits.Length)
        {
            hits = Physics.RaycastAll(origin, Vector3.down, distance, layers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        ground = default;
        float bestDistance = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = hits[index];
            if (hit.distance >= bestDistance || hit.normal.y < 0.45f || hit.transform.IsChildOf(actor))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<LeonController>() != null
                || hit.collider.GetComponentInParent<VillagerController>() != null)
            {
                continue;
            }

            ground = hit;
            bestDistance = hit.distance;
        }

        return ground.collider != null;
    }

    public static SurfaceType Resolve(RaycastHit ground)
    {
        Collider collider = ground.collider;
        FootstepSurface authored = collider.GetComponent<FootstepSurface>();
        if (authored != null)
        {
            return authored.ResolveSubmesh(ground);
        }

        return ResolveMaterialOrName(collider);
    }

    internal static SurfaceType ResolveCollider(Collider collider)
    {
        if (collider.TryGetComponent<FootstepSurface>(out var authored))
        {
            return authored.surfaceType;
        }
        return ResolveMaterialOrName(collider);
    }

    private static SurfaceType ResolveMaterialOrName(Collider collider)
    {
        if (collider.sharedMaterial != null)
        {
            SurfaceType materialType = ResolveName(collider.sharedMaterial.name);
            if (materialType != SurfaceType.Default)
            {
                return materialType;
            }
        }

        return ResolveName(collider.name);
    }

    private SurfaceType ResolveSubmesh(RaycastHit ground)
    {
        if (terrainIndex != null && terrainIndex.isReadable && terrainIndexSurfaces.Length > 0)
        {
            Vector3 point = transform.InverseTransformPoint(ground.point);
            if (terrainIndexBounds.Contains(new Vector2(point.x, point.z)))
            {
                float u = (point.x - terrainIndexBounds.xMin) / terrainIndexBounds.width;
                float v = (point.z - terrainIndexBounds.yMin) / terrainIndexBounds.height;
                int x = Mathf.Clamp(Mathf.FloorToInt(u * terrainIndex.width), 0, terrainIndex.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(v * terrainIndex.height), 0, terrainIndex.height - 1);
                int layer = Mathf.RoundToInt(terrainIndex.GetPixel(x, y).r * 255f) / 16;
                if (layer < terrainIndexSurfaces.Length)
                {
                    return terrainIndexSurfaces[layer];
                }
            }
        }

        if (ground.triangleIndex < 0 || submeshSurfaces.Length == 0 || !(ground.collider is MeshCollider meshCollider))
        {
            return surfaceType;
        }

        Mesh mesh = meshCollider.sharedMesh;
        if (mesh == null)
        {
            return surfaceType;
        }

        if (mesh != cachedMesh || triangleEnds == null)
        {
            cachedMesh = mesh;
            triangleEnds = new int[mesh.subMeshCount];
            int count = 0;
            for (int index = 0; index < triangleEnds.Length; index++)
            {
                count += (int)mesh.GetIndexCount(index) / 3;
                triangleEnds[index] = count;
            }
        }

        int triangle = ground.triangleIndex;
        int submesh = Array.BinarySearch(triangleEnds, triangle + 1);
        if (submesh < 0)
        {
            submesh = ~submesh;
        }
        else
        {
            while (submesh > 0 && triangleEnds[submesh - 1] == triangleEnds[submesh])
            {
                submesh--;
            }
        }

        if (submesh < submeshSurfaces.Length && submesh < triangleEnds.Length)
        {
            return submeshSurfaces[submesh];
        }

        return surfaceType;
    }

    private static SurfaceType ResolveName(string value)
    {
        string name = value.ToLowerInvariant();
        if (name.Contains("concrete") || name.Contains("stone") || name.Contains("水泥") || name.Contains("混凝土") || name.Contains("石"))
        {
            return SurfaceType.Concrete;
        }

        if (name.Contains("wood") || name.Contains("plank") || name.Contains("木"))
        {
            return SurfaceType.Wood;
        }

        if (name.Contains("grass") || name.Contains("terrain") || name.Contains("草"))
        {
            return SurfaceType.Grass;
        }

        if (name.Contains("mud") || name.Contains("dirt") || name.Contains("泥"))
        {
            return SurfaceType.Mud;
        }

        if (name.Contains("sand") || name.Contains("沙"))
        {
            return SurfaceType.Sand;
        }

        if (name.Contains("snow") || name.Contains("雪"))
        {
            return SurfaceType.Snow;
        }

        if (name.Contains("water") || name.Contains("水"))
        {
            return SurfaceType.Water;
        }

        if (name.Contains("ground") || name.Contains("floor"))
        {
            return SurfaceType.Concrete;
        }

        return SurfaceType.Default;
    }
}
