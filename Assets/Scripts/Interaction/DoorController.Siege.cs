using UnityEngine;

public partial class DoorController
{
    [SerializeField, Tooltip("上闩门最终损坏时播放的木材声。")]
    private AudioEventData siegeBreakAudio;
    [SerializeField, Tooltip("锁闩到位时显示的门闩模型。")]
    private GameObject siegeBolt;
    [SerializeField, Tooltip("锯穿门叶时释放的木屑。")]
    private ParticleSystem siegeSplinters;
    [SerializeField, Tooltip("可破坏门的模型根，用于对齐原始破坏分件。")]
    private Transform siegeLeaf;
    [SerializeField, Tooltip("门叶破碎后飞散的原始分件。")]
    private GameObject siegeBreakPieces;
    [System.Serializable]
    private struct SiegeBoard
    {
        [Tooltip("第几次锯击使这块门板脱落。")]
        public int cut;
        public Renderer[] surfaces;
        public Rigidbody fragment;
    }

    [SerializeField, Tooltip("组成完整门叶的原版木板，以及每块木板对应的飞散分件。")]
    private SiegeBoard[] siegeBoards;

    private readonly Collider[] siegeOverlaps = new Collider[24];
    private bool siegeLatched;
    private bool siegeBroken;
    private bool siegeLatchPending;
    private int siegeCuts;
    internal bool IsSiegeLatched => siegeLatched || siegeLatchPending;

    internal bool IsSiegeDoorBetween(Vector3 from, Vector3 to)
    {
        if (!IsSiegeLatched || !PursuitNavigationTopology.TryGetDoorRegions(this, out int positive, out int negative) ||
            !PursuitNavigationTopology.TryGetRegion(from, out int source) ||
            !PursuitNavigationTopology.TryGetRegion(to, out int destination))
        {
            return false;
        }

        return source == positive && destination == negative || source == negative && destination == positive;
    }

    internal void RequestSiegeLatch()
    {
        if (!siegeBroken && !siegeLatched)
        {
            siegeLatchPending = true;
        }
    }

    internal bool TryLatchForSiege()
    {
        if (!siegeLatchPending || siegeLatched || siegeBroken || animator == null ||
            hingePivot == null || !(physicalCollider is BoxCollider leaf))
        {
            return false;
        }

        Quaternion toClosed = initialHingeWorldRotation * Quaternion.Inverse(hingePivot.rotation);
        Vector3 offset = leaf.transform.TransformPoint(leaf.center) - hingePivot.position;
        Vector3 center = hingePivot.position + toClosed * offset;
        Vector3 halfExtents = Vector3.Scale(leaf.size * 0.5f, AbsVector(leaf.transform.lossyScale));
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, siegeOverlaps, toClosed * leaf.transform.rotation,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            Collider contact = siegeOverlaps[index];
            if (contact.GetComponentInParent<LeonController>() != null || contact.GetComponentInParent<VillagerController>() != null)
            {
                return false;
            }
        }

        siegeLatchPending = false;
        SetOpen(false);
        animator.Play("Base Layer.Closed", 0, 0f);
        animator.Update(0f);
        isAnimating = false;
        doorOpenProgress = 0f;
        hingePivot.rotation = initialHingeWorldRotation;
        SetPhysicalColliderBlocking();
        siegeLatched = true;
        if (siegeBolt != null)
        {
            siegeBolt.SetActive(true);
        }

        NavigationCollisionCache.MarkerPoseChange();
        return true;
    }

    internal bool TrySawSiegeDoor(Vector3 blade, float reach)
    {
        if (!siegeLatched || siegeBroken || physicalCollider == null ||
            (physicalCollider.ClosestPoint(blade) - blade).sqrMagnitude > reach * reach)
        {
            return false;
        }

        siegeCuts++;
        bool hasBoards = siegeBoards != null && siegeBoards.Length > 0;
        if (hasBoards)
        {
            foreach (SiegeBoard board in siegeBoards)
            {
                if (board.cut != siegeCuts)
                {
                    continue;
                }
                foreach (Renderer surface in board.surfaces)
                {
                    surface.enabled = false;
                }
                Transform piece = board.fragment.transform;
                Vector3 position = siegeLeaf.TransformPoint(piece.localPosition);
                var fragment = Instantiate(board.fragment, position, siegeLeaf.rotation * piece.localRotation);
                fragment.transform.localScale = Vector3.Scale(siegeLeaf.lossyScale, piece.localScale);
                ThrowSiegeFragment(fragment, blade);
                Destroy(fragment.gameObject, 4f);
            }
        }
        if (siegeSplinters != null)
        {
            siegeSplinters.transform.position = physicalCollider.ClosestPoint(blade);
            siegeSplinters.Play();
        }
        if (siegeCuts < 3)
        {
            return true;
        }

        siegeLatched = false;
        siegeBroken = true;
        isAnimating = false;
        physicalCollider.enabled = false;
        if (siegeBolt != null)
        {
            siegeBolt.SetActive(false);
        }

        foreach (Renderer leaf in hingePivot.GetComponentsInChildren<Renderer>())
        {
            leaf.enabled = false;
        }

        if (!hasBoards)
        {
            SpawnSiegeBreakPieces(blade);
        }
        if (siegeBreakAudio != null && AudioManager.HasInstance)
        {
            AudioManager.Instance.Play3D(siegeBreakAudio, blade);
        }
        NavigationCollisionCache.MarkerPoseChange();
        return true;
    }

    private void SpawnSiegeBreakPieces(Vector3 blade)
    {
        if (siegeLeaf == null || siegeBreakPieces == null)
        {
            return;
        }

        GameObject fragments = Instantiate(siegeBreakPieces, siegeLeaf.position, siegeLeaf.rotation);
        fragments.transform.localScale = siegeLeaf.lossyScale;
        foreach (Rigidbody fragment in fragments.GetComponentsInChildren<Rigidbody>())
        {
            ThrowSiegeFragment(fragment, blade);
        }

        Destroy(fragments, 4f);
    }

    private void ThrowSiegeFragment(Rigidbody fragment, Vector3 blade)
    {
        Vector3 away = Vector3.ProjectOnPlane(PassageCenter - blade, Vector3.up).normalized;
        Vector3 spread = (fragment.worldCenterOfMass - blade).normalized;
        fragment.AddForce(away * 1.3f + spread * .7f + Vector3.up * Random.Range(.4f, 1.2f), ForceMode.VelocityChange);
        fragment.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
    }
}
