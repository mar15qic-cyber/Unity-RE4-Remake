using UnityEngine;

internal struct ShotgunTerminalHit
{
    private float head;
    private float left;
    private float right;
    private float other;
    private Vector3 headPoint;
    private Vector3 leftPoint;
    private Vector3 rightPoint;
    internal bool killed;

    internal void Record(HitboxType part, float damage, Vector3 point, bool dead)
    {
        if (part == HitboxType.Head)
        {
            head += damage;
            headPoint = point;
        }
        else if (part == HitboxType.LeftArm)
        {
            left += damage;
            leftPoint = point;
        }
        else if (part == HitboxType.RightArm)
        {
            right += damage;
            rightPoint = point;
        }
        else
        {
            other += damage;
        }
        killed |= dead;
    }

    internal bool TrySelect(out HitboxType part, out Vector3 point)
    {
        part = HitboxType.Head;
        point = headPoint;
        if (!killed)
        {
            return false;
        }
        float best = head;
        if (left > best)
        {
            best = left;
            part = HitboxType.LeftArm;
            point = leftPoint;
        }
        if (right > best)
        {
            best = right;
            part = HitboxType.RightArm;
            point = rightPoint;
        }
        return best > other;
    }
}

public sealed class VillagerDismemberment : MonoBehaviour
{
    private readonly ExecutionAppearance appearance = new ExecutionAppearance();
    private Transform stump;
    private Transform fragment;
    private Vector3 velocity;
    private Vector3 direction;
    private Vector3 spin;
    private float age;
    private float nextBurst;
    private bool landed;

    internal static void Present(VillagerController target, ShotgunTerminalHit shot, Vector3 shooter)
    {
        if (target == null || !target.IsDead || !shot.TrySelect(out var part, out var hit))
        {
            return;
        }
        Present(target, part, hit, shooter, ChapterOneEffect.ShotgunBlood);
    }

    internal static void PresentPistolCritical(VillagerController target, HitboxType part, Vector3 hit, Vector3 shooter)
    {
        // 手枪命中反馈已随伤害结算播放，分离时只补充断面和后续表现。
        Present(target, part, hit, shooter, ChapterOneEffect.None);
    }

    private static void Present(VillagerController target, HitboxType part, Vector3 hit, Vector3 shooter,
        ChapterOneEffect separationBurst)
    {
        if (target == null || !target.IsDead)
        {
            return;
        }
        var bank = ExecutionGeometry.Load();
        if (bank == null)
        {
            return;
        }
        var sections = bank.head;
        string joint = "Neck_1";
        if (part == HitboxType.LeftArm)
        {
            sections = bank.leftArm;
            joint = "L_UpperArm";
        }
        else if (part == HitboxType.RightArm)
        {
            sections = bank.rightArm;
            joint = "R_UpperArm";
        }
        if (sections == null || sections.Length == 0)
        {
            return;
        }
        var effect = target.GetComponent<VillagerDismemberment>();
        if (effect != null && effect.fragment != null)
        {
            return;
        }
        if (effect == null)
        {
            effect = target.gameObject.AddComponent<VillagerDismemberment>();
        }
        if (!effect.appearance.Apply(target.transform, sections, bank.interior))
        {
            return;
        }
        foreach (var bone in target.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == joint)
            {
                effect.stump = bone;
                break;
            }
        }
        Vector3 position = hit;
        if (effect.stump != null)
        {
            position = effect.stump.position;
        }
        effect.fragment = effect.appearance.Detach(true, position, target.gameObject.scene);
        effect.direction = (hit - shooter).normalized;
        effect.velocity = effect.direction * 2.7f + Vector3.up * 1.5f;
        effect.spin = Vector3.Cross(effect.direction, Vector3.up) * 240f;
        effect.age = 0f;
        effect.nextBurst = .16f;
        effect.landed = false;
        if (separationBurst != ChapterOneEffect.None)
        {
            ChapterOneEffects.TryPlay(separationBurst, position, effect.direction);
        }
        ExecutionBlood.Mark(position, effect.direction, 2f, .56f);
        ExecutionBlood.Mark(position, Vector3.down, 3f, .39f);
    }

    private void Update()
    {
        if (fragment == null || Time.deltaTime <= 0f)
        {
            return;
        }
        age += Time.deltaTime;
        if (stump != null && age >= nextBurst && nextBurst < .55f)
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.StabBlood, stump.position, direction + Vector3.down * age);
            nextBurst += .27f;
        }
        if (landed)
        {
            return;
        }
        velocity += Physics.gravity * Time.deltaTime;
        Vector3 delta = velocity * Time.deltaTime;
        if (ExecutionBlood.FindSurface(fragment.position, delta.normalized, delta.magnitude + .1f, out var hit))
        {
            fragment.position = hit.point + hit.normal * .1f;
            landed = true;
            ExecutionBlood.Mark(fragment.position, -hit.normal, .3f, .24f);
            return;
        }
        fragment.position += delta;
        fragment.Rotate(spin * Time.deltaTime, Space.World);
    }

    private void OnDisable()
    {
        appearance.Clear();
        fragment = null;
        stump = null;
    }
}
