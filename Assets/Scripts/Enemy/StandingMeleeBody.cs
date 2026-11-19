using System.Collections.Generic;
using UnityEngine;

internal sealed class StandingMeleeBody
{
    private readonly struct Part
    {
        internal readonly Transform start;
        internal readonly Transform end;
        internal readonly Collider collider;
        internal readonly float radius;
        internal readonly HitboxType type;

        internal Part(Transform start, Transform end, float radius, HitboxType type)
        {
            this.start = start;
            this.end = end;
            this.radius = radius;
            this.type = type;
            collider = null;
        }

        internal Part(Collider collider, HitboxType type)
        {
            this.collider = collider;
            this.type = type;
            start = null;
            end = null;
            radius = 0f;
        }

        internal MeleeCapsule Read()
        {
            if (start != null && end != null)
            {
                Vector3 point = start.position;
                Vector3 tip = end.position;
                if (type == HitboxType.Head)
                {
                    point += (point - tip).normalized * .055f;
                    tip = point;
                }
                else if (type == HitboxType.Pelvis)
                {
                    point -= (tip - point).normalized * .12f;
                }

                return new MeleeCapsule(point, tip, radius);
            }

            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return default;
            }

            Transform anchor = collider.transform;
            Vector3 scale = anchor.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (collider is SphereCollider sphere)
            {
                Vector3 center = anchor.TransformPoint(sphere.center);
                return new MeleeCapsule(center, center, sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));
            }

            if (collider is CapsuleCollider capsule)
            {
                Vector3 center = anchor.TransformPoint(capsule.center);
                int direction = capsule.direction;
                float worldRadius = capsule.radius * Mathf.Max(scale[(direction + 1) % 3], scale[(direction + 2) % 3]);
                Vector3 axis = Vector3.zero;
                axis[direction] = Mathf.Max(0f, capsule.height * scale[direction] * .5f - worldRadius);
                axis = anchor.rotation * axis;
                return new MeleeCapsule(center - axis, center + axis, worldRadius);
            }

            if (collider is BoxCollider box)
            {
                Vector3 extents = Vector3.Scale(box.size * .5f, scale);
                int direction = 0;
                if (extents.y > extents[direction])
                {
                    direction = 1;
                }

                if (extents.z > extents[direction])
                {
                    direction = 2;
                }

                float worldRadius = Mathf.Min(extents[(direction + 1) % 3], extents[(direction + 2) % 3]);
                Vector3 axis = Vector3.zero;
                axis[direction] = Mathf.Max(0f, extents[direction] - worldRadius);
                axis = anchor.rotation * axis;
                Vector3 center = anchor.TransformPoint(box.center);
                return new MeleeCapsule(center - axis, center + axis, worldRadius);
            }

            return default;
        }
    }

    private readonly Part[] parts;
    private readonly MeleeCapsule[] previous;
    private readonly MeleeCapsule[] current;
    private int frame = -2;
    private Vector3 rootPosition;

    private StandingMeleeBody(Part[] parts)
    {
        this.parts = parts;
        previous = new MeleeCapsule[parts.Length];
        current = new MeleeCapsule[parts.Length];
    }

    internal static StandingMeleeBody ForLeon(LeonController leon)
    {
        Animator animator = null;
        if (leon.AnimDriver != null)
        {
            animator = leon.AnimDriver.Animator;
        }

        if (animator == null)
        {
            animator = leon.GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            return new StandingMeleeBody(System.Array.Empty<Part>());
        }

        Transform hips = FindBone(animator.transform, "root hips");
        Transform middle = FindBone(animator.transform, "spine middle");
        Transform neck = FindBone(animator.transform, "head neck lower");
        Transform head = FindBone(animator.transform, "head");
        if (animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            middle = animator.GetBoneTransform(HumanBodyBones.Spine);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        return new StandingMeleeBody(new[]
        {
            new Part(hips, middle, .23f, HitboxType.Pelvis),
            new Part(middle, neck, .24f, HitboxType.Chest),
            new Part(head, neck, .145f, HitboxType.Head)
        });
    }

    internal static StandingMeleeBody ForVillager(VillagerController owner)
    {
        var parts = new List<Part>();
        foreach (EnemyHitbox hitbox in owner.GetComponentsInChildren<EnemyHitbox>(true))
        {
            if (hitbox.hitboxType != HitboxType.Chest && hitbox.hitboxType != HitboxType.Head && hitbox.hitboxType != HitboxType.Pelvis)
            {
                continue;
            }

            foreach (Collider collider in hitbox.GetComponents<Collider>())
            {
                parts.Add(new Part(collider, hitbox.hitboxType));
            }
        }

        return new StandingMeleeBody(parts.ToArray());
    }

    internal static Transform FindBone(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindBone(root.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    internal void Capture(Vector3 root, bool refresh = false)
    {
        int currentFrame = Time.frameCount;
        if (frame == currentFrame && !refresh)
        {
            return;
        }

        bool continuous = frame == currentFrame - 1 && (root - rootPosition).sqrMagnitude < 1f;
        for (int index = 0; index < parts.Length; index++)
        {
            MeleeCapsule latest = parts[index].Read();
            if (frame != currentFrame)
            {
                previous[index] = latest;
                if (continuous && current[index].radius > 0f)
                {
                    previous[index] = current[index];
                }
            }

            current[index] = latest;
        }

        frame = currentFrame;
        rootPosition = root;
    }

    internal bool Sweep(MeleeCapsule first, MeleeCapsule last, float bodyFrom, float bodyTo,
        ref float earliest, out Vector3 point, out Vector3 normal, out float axis, out HitboxType part)
    {
        point = default;
        normal = default;
        axis = 0f;
        part = default;
        bool hit = false;
        for (int index = 0; index < parts.Length; index++)
        {
            if (current[index].radius <= 0f)
            {
                continue;
            }

            MeleeCapsule targetFirst = MeleeCapsule.Interpolate(previous[index], current[index], bodyFrom);
            MeleeCapsule targetLast = MeleeCapsule.Interpolate(previous[index], current[index], bodyTo);
            if (first.TrySweep(last, targetFirst, targetLast, out float time, out Vector3 surface, out Vector3 outward, out float weaponAxis)
                && time < earliest)
            {
                hit = true;
                earliest = time;
                point = surface;
                normal = outward;
                axis = weaponAxis;
                part = parts[index].type;
            }
        }

        return hit;
    }
}
