using UnityEngine;
using UnityEngine.SceneManagement;

// 用倒地姿势的躯干作为承重刚体，保留四肢动画。屋檐只托住身体一端时，重力会产生翻落力矩。
internal sealed class CorpseBodyPhysics
{
    private static int characterLayers;
    private Animator skeleton;
    private Transform hips;
    private Transform chest;
    private Transform leftShin, rightShin, leftFoot, rightFoot, leftToe, rightToe;
    private Rigidbody torso;
    private Vector3 rootOffset;
    private Quaternion rootRotation;
    internal bool Active => torso != null;

    internal bool Falling
    {
        get
        {
            if (torso == null || torso.IsSleeping())
            {
                return false;
            }
            return torso.linearVelocity.y < -.35f || !HasSupport(torso.position);
        }
    }

    private static bool HasSupport(Vector3 point)
    {
        int mask = Physics.DefaultRaycastLayers & ~characterLayers;
        return Physics.Raycast(point + Vector3.up * .08f, Vector3.down, .42f, mask, QueryTriggerInteraction.Ignore);
    }

    private static bool TrySampleGroundY(Vector3 point, float maxDistance, out float groundY)
    {
        int mask = Physics.DefaultRaycastLayers & ~characterLayers;
        if (Physics.Raycast(point + Vector3.up * 0.18f, Vector3.down, out RaycastHit hit, maxDistance + 0.18f, mask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private static bool HasLevelGroundBelow(Vector3 bonePoint, float referenceGroundY, float maxDropBelowReference)
    {
        if (!TrySampleGroundY(bonePoint, 1.85f, out float hitY))
        {
            return false;
        }

        return referenceGroundY - hitY <= maxDropBelowReference;
    }

    internal bool TryBegin(Animator animator, Transform root, bool requireOverhang)
    {
        if (animator == null || root == null || Active)
        {
            return false;
        }
        if (skeleton != animator)
        {
            if (characterLayers == 0)
            {
                characterLayers = LayerMask.GetMask("player", "Enemy", "EnemyHitbox");
            }
            skeleton = animator;
            var bones = animator.GetComponentsInChildren<Transform>(true);
            hips = FindBone(animator, bones, HumanBodyBones.Hips, "Hip");
            chest = FindBone(animator, bones, HumanBodyBones.Chest, "Spine_2");
            leftShin = FindBone(animator, bones, HumanBodyBones.LeftLowerLeg, "L_Shin");
            rightShin = FindBone(animator, bones, HumanBodyBones.RightLowerLeg, "R_Shin");
            leftFoot = FindBone(animator, bones, HumanBodyBones.LeftFoot, "L_Foot");
            rightFoot = FindBone(animator, bones, HumanBodyBones.RightFoot, "R_Foot");
            leftToe = FindBone(animator, bones, HumanBodyBones.LeftToes, "L_Toe");
            rightToe = FindBone(animator, bones, HumanBodyBones.RightToes, "R_Toe");
        }
        if (hips == null || chest == null)
        {
            return false;
        }
        Vector3 axis = chest.position - hips.position;
        if (axis.sqrMagnitude < .01f)
        {
            return false;
        }

        float referenceGroundY = root.position.y;
        if (TrySampleGroundY(root.position, 1.2f, out float sampledRootGroundY))
        {
            referenceGroundY = sampledRootGroundY;
        }

        // 平地和普通缓坡上的尸体始终交由贴地拟合处理；只有真正悬空于屋檐或高台边缘时才生成翻落刚体，
        // 避免平地倒地半途或躺地后因刚体与动画旋转叠加把尸体斜插进地面。
        bool hipsSupported = HasLevelGroundBelow(hips.position, referenceGroundY, 0.65f);
        bool chestSupported = HasLevelGroundBelow(chest.position, referenceGroundY, 0.65f);
        if (hipsSupported && chestSupported)
        {
            return false;
        }

        if (requireOverhang && Mathf.Abs(axis.normalized.y) > .35f)
        {
            return false;
        }
        Vector3 center = (hips.position + chest.position) * .5f;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis);
        if (up.sqrMagnitude < .01f)
        {
            up = Vector3.ProjectOnPlane(root.forward, axis);
        }
        Quaternion rotation = Quaternion.LookRotation(axis, up);
        var proxy = new GameObject("遗体躯干碰撞");
        SceneManager.MoveGameObjectToScene(proxy, root.gameObject.scene);
        proxy.layer = LayerMask.NameToLayer("Ignore Raycast");
        proxy.transform.SetPositionAndRotation(center, rotation);
        var shape = proxy.AddComponent<BoxCollider>();
        shape.size = new Vector3(.37f, .23f, axis.magnitude + .35f);
        AddLimb(proxy.transform, leftShin, leftFoot, .075f);
        AddLimb(proxy.transform, rightShin, rightFoot, .075f);
        AddLimb(proxy.transform, leftFoot, leftToe, .065f);
        AddLimb(proxy.transform, rightFoot, rightToe, .065f);
        var ownShapes = root.GetComponentsInChildren<Collider>();
        foreach (var support in proxy.GetComponentsInChildren<Collider>())
        {
            support.excludeLayers = characterLayers | (1 << proxy.layer);
            foreach (var own in ownShapes)
            {
                Physics.IgnoreCollision(support, own);
            }
        }
        torso = proxy.AddComponent<Rigidbody>();
        torso.mass = 60f;
        torso.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        torso.interpolation = RigidbodyInterpolation.Interpolate;
        rootOffset = Quaternion.Inverse(rotation) * (root.position - center);
        rootRotation = Quaternion.Inverse(rotation) * root.rotation;
        return true;
    }

    private static Transform FindBone(Animator animator, Transform[] bones, HumanBodyBones id, string name)
    {
        if (animator.isHuman)
        {
            var mapped = animator.GetBoneTransform(id);
            if (mapped != null)
            {
                return mapped;
            }
        }
        foreach (var bone in bones)
        {
            if (bone.name == name)
            {
                return bone;
            }
        }
        return null;
    }

    private static void AddLimb(Transform proxy, Transform start, Transform end, float radius)
    {
        if (start == null)
        {
            return;
        }
        var limb = new GameObject(start.name);
        limb.layer = proxy.gameObject.layer;
        limb.transform.SetParent(proxy, false);
        limb.transform.position = start.position;
        if (end == null || (end.position - start.position).sqrMagnitude < .0001f)
        {
            limb.AddComponent<SphereCollider>().radius = radius;
            return;
        }
        Vector3 span = end.position - start.position;
        limb.transform.SetPositionAndRotation(start.position + span * .5f,
            Quaternion.FromToRotation(Vector3.up, span.normalized));
        var capsule = limb.AddComponent<CapsuleCollider>();
        capsule.radius = radius;
        capsule.height = span.magnitude + 2f * radius;
    }

    internal Pose ReadPose()
    {
        Transform pose = torso.transform;
        return new Pose(pose.TransformPoint(rootOffset), pose.rotation * rootRotation);
    }

    internal void Release()
    {
        if (torso != null)
        {
            torso.gameObject.SetActive(false);
            Object.Destroy(torso.gameObject);
            torso = null;
        }
    }
}
