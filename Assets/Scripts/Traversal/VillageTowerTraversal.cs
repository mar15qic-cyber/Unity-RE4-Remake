using Pathfinding;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class VillageTowerTraversal : MonoBehaviour
{
    [Header("固定梯与平台")]
    public Transform ladderFoot;
    public Transform ladderHead;
    public Transform ladderExit;
    public BoxCollider platformArea;
    public Collider ladderCollider;
    [Tooltip("固定梯左右立柱的实体碰撞，与正在通行的角色暂时分离")]
    public Collider[] ladderFrameColliders = System.Array.Empty<Collider>();
    [Min(0.6f)] public float climberSpacing = 1.65f;
    [Tooltip("坠落者实际扫到下方攀爬者时，使对方脱手")]
    public bool enableClimberImpact = true;

    [Header("一次性坍塌")]
    public Collider[] collapsingSurfaces;
    [Tooltip("随楼板破碎失效的边梁碰撞，不参与踩板计时")]
    public Collider[] collapsingBeams;
    public Renderer intactFloor;
    public GameObject brokenFloor;
    public AnimationClip breakingAnimation;
    public AudioEventData breakingSound;
    [Min(0f)] public float stepInSeconds = 3f;
    [Min(0f)] public float leaveGraceSeconds = 0.1f;

    private VillageLadderSite site;
    private EnemyAIManager group;
    private float onFloorTime;
    private float offFloorTime;
    private PlayableGraph breakingGraph;

    internal bool HasCollapsed { get; private set; }

    internal void Install(VillageLadderConfiguration config, HouseThreeRegionNavigation navigation, EnemyAIManager enemies)
    {
        if (!isActiveAndEnabled || site != null)
        {
            return;
        }

        if (ladderFoot == null || ladderHead == null || ladderExit == null || platformArea == null ||
            ladderCollider == null || intactFloor == null || brokenFloor == null || breakingAnimation == null ||
            collapsingSurfaces == null || collapsingSurfaces.Length == 0 ||
            config.playerFixed == null || !config.playerFixed.IsComplete ||
            config.playerTowerFall == null || config.playerTowerLand == null)
        {
            Debug.LogError("塔楼通行、楼板或原版动作未完整绑定。", this);
            return;
        }

        group = enemies;
        var root = new GameObject("塔楼固定梯");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        root.transform.position = ladderExit.position;
        site = root.AddComponent<VillageLadderSite>();
        site.ConfigureFixed(this, config, navigation, enemies);
        root.SetActive(true);
    }

    internal bool ContainsPlatform(Vector3 world)
    {
        Vector3 local = platformArea.transform.InverseTransformPoint(world) - platformArea.center;
        Vector3 extents = platformArea.size * 0.5f;
        return Mathf.Abs(local.x) <= extents.x && Mathf.Abs(local.y) <= extents.y &&
            Mathf.Abs(local.z) <= extents.z;
    }

    internal bool IsLadderCollider(Collider value)
    {
        if (value == ladderCollider)
        {
            return true;
        }
        return System.Array.IndexOf(ladderFrameColliders, value) >= 0;
    }

    private void FixedUpdate()
    {
        if (site == null || HasCollapsed || group == null)
        {
            return;
        }

        var player = group.PlayerTarget;
        if (player == null || player.currentState == LeonState.Dead)
        {
            return;
        }

        if (!player.IsGrounded || player.currentState == LeonState.Traversing ||
            !ContainsPlatform(player.transform.position) || !StandsOnCollapsingSurface(player))
        {
            offFloorTime += Time.fixedDeltaTime;
            if (offFloorTime >= leaveGraceSeconds)
            {
                onFloorTime = 0f;
            }
            return;
        }

        offFloorTime = 0f;
        onFloorTime += Time.fixedDeltaTime;
        if (onFloorTime >= stepInSeconds)
        {
            Collapse(player);
        }
    }

    private void OnDisable()
    {
        if (breakingGraph.IsValid())
        {
            breakingGraph.Destroy();
        }
    }

    private bool StandsOnCollapsingSurface(LeonController player)
    {
        if (!player.TryGetComponent<CapsuleCollider>(out var capsule) || !capsule.enabled)
        {
            return false;
        }
        Bounds bodyBounds = capsule.bounds;
        Vector3 foot = new Vector3(bodyBounds.center.x, bodyBounds.min.y, bodyBounds.center.z);
        if (!Physics.Raycast(foot + Vector3.up * .15f, Vector3.down, out var support, .25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || support.normal.y < .55f)
        {
            return false;
        }
        foreach (var surface in collapsingSurfaces)
        {
            if (surface == null || !surface.enabled)
            {
                continue;
            }

            if (surface == support.collider)
            {
                return true;
            }
        }
        return false;
    }

    private void Collapse(LeonController player)
    {
        HasCollapsed = true;
        site.BeginTowerDrop(player);
        site.CloseFixedRoute();
        var bounds = new Bounds();
        for (int i = 0; i < collapsingSurfaces.Length; i++)
        {
            var surface = collapsingSurfaces[i];
            if (i == 0)
            {
                bounds = surface.bounds;
            }
            else
            {
                bounds.Encapsulate(surface.bounds);
            }
            surface.enabled = false;
        }
        if (collapsingBeams != null)
        {
            foreach (var beam in collapsingBeams)
            {
                if (beam != null)
                {
                    beam.enabled = false;
                }
            }
        }
        intactFloor.enabled = false;
        brokenFloor.SetActive(true);
        var animator = brokenFloor.GetComponent<Animator>();
        if (animator != null)
        {
            breakingGraph = PlayableGraph.Create("塔楼木板破碎");
            var clip = AnimationClipPlayable.Create(breakingGraph, breakingAnimation);
            var output = AnimationPlayableOutput.Create(breakingGraph, "原版木板动画", animator);
            output.SetSourcePlayable(clip);
            breakingGraph.Play();
        }
        VillageLadderSite.PlaySound(breakingSound, ladderExit.position);
        CloseFloorNavigation(bounds);
    }

    private static void CloseFloorNavigation(Bounds floorBounds)
    {
        var pathfinding = AstarPath.active;
        if (pathfinding == null)
        {
            return;
        }

        floorBounds.Expand(new Vector3(0.04f, 0.75f, 0.04f));
        pathfinding.AddWorkItem(new AstarWorkItem(context =>
        {
            pathfinding.data.GetNodes(node =>
            {
                if (floorBounds.Contains((Vector3)node.position))
                {
                    node.Walkable = false;
                }
            });
            context.DirtyBounds(floorBounds);
            PursuitNavigationTopology.Invalidate();
        }));
    }
}
