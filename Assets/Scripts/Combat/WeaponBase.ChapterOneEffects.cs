using UnityEngine;

public partial class WeaponBase
{
    private Transform nativeCaseJoint;
    private bool resolvedNativeCaseJoint;
    private bool shotgunCasePending;
    private int pendingMuzzleFlashes;

    private void PlayChapterOneShotEffects()
    {
        if (muzzlePoint != null)
        {
            pendingMuzzleFlashes++;
            if (controller == null || controller.currentWeapon != data.weaponType
                || controller.AnimRig == null || !controller.AnimRig.isActiveAndEnabled)
            {
                FlushChapterOneMuzzleFlashes();
            }
        }

        switch (data.weaponType)
        {
            case WeaponType.Pistol:
                ChapterOneEffects.PlayCase(ChapterOneEffect.PistolCase, GetNativeCaseJoint(),
                    new Vector3(0f, 0.03f, 0f), new Vector3(0.4932974f, 0.5422038f, -0.6891291f) * 3f,
                    new Vector3(50f, -100f, 0f));
                break;
            case WeaponType.Shotgun:
                shotgunCasePending = true;
                break;
            case WeaponType.Rifle:
                ChapterOneEffects.PlayCase(ChapterOneEffect.RifleCase, GetNativeCaseJoint(),
                    new Vector3(0f, 0f, -0.03f), new Vector3(0.7005645f, 0.5088375f, -0.5087862f) * 2f,
                    Vector3.zero);
                break;
        }
    }

    internal void FlushChapterOneMuzzleFlashes()
    {
        int count = pendingMuzzleFlashes;
        pendingMuzzleFlashes = 0;
        if (count == 0 || muzzlePoint == null)
        {
            return;
        }

        ChapterOneEffect kind;
        switch (data.weaponType)
        {
            case WeaponType.Pistol:
                kind = ChapterOneEffect.PistolMuzzle;
                break;
            case WeaponType.Shotgun:
                kind = ChapterOneEffect.ShotgunMuzzle;
                break;
            case WeaponType.Rifle:
                kind = ChapterOneEffect.RifleMuzzle;
                break;
            default:
                return;
        }

        for (int index = 0; index < count; index++)
        {
            ChapterOneEffects.PlayMuzzle(kind, muzzlePoint);
        }
    }

    internal void PlayChapterOnePumpCase()
    {
        if (!shotgunCasePending)
        {
            return;
        }
        shotgunCasePending = false;
        ChapterOneEffects.PlayCase(ChapterOneEffect.ShotgunCase, GetNativeCaseJoint(),
            new Vector3(0.01f, -0.03f, 0.03f), new Vector3(0.8375815f, 0.2004322f, 0.229864f) * 2.5f,
            new Vector3(0f, 50f, 0f));
    }

    internal void CancelChapterOnePumpCase()
    {
        shotgunCasePending = false;
    }

    private Transform GetNativeCaseJoint()
    {
        if (!resolvedNativeCaseJoint && VisualModel != null)
        {
            foreach (var joint in VisualModel.GetComponentsInChildren<Transform>(true))
            {
                if (joint.name == "vfx_muzzle2")
                {
                    nativeCaseJoint = joint;
                    break;
                }
            }
            resolvedNativeCaseJoint = true;
        }
        return nativeCaseJoint;
    }

    private void PlayChapterOneSurfaceImpact(RaycastHit hit)
    {
        if (data.weaponType == WeaponType.Knife)
        {
            return;
        }
        SurfaceType surface = FootstepSurface.Resolve(hit);
        if (surface == SurfaceType.Water)
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.WaterImpact, hit.point, hit.normal);
            return;
        }
        PlayChapterOneImpactBurst(hit);
        var collider = hit.collider;
        var material = collider.sharedMaterial;
        if (material != null && TryPlayHardSurface(material.name, hit))
        {
            return;
        }
        if (TryPlayHardSurface(collider.name, hit)
            || collider.TryGetComponent<Renderer>(out var renderer) && renderer.sharedMaterial != null
            && TryPlayHardSurface(renderer.sharedMaterial.name, hit))
        {
            return;
        }

        ChapterOneEffect effect = ChapterOneEffect.OtherImpact;
        switch (surface)
        {
            case SurfaceType.Concrete:
                effect = ChapterOneEffect.StoneImpact;
                break;
            case SurfaceType.Wood:
                effect = ChapterOneEffect.WoodImpact;
                break;
            case SurfaceType.Grass:
            case SurfaceType.Mud:
            case SurfaceType.Sand:
            case SurfaceType.Snow:
                effect = ChapterOneEffect.DirtImpact;
                break;
        }
        ChapterOneEffects.TryPlay(effect, hit.point + hit.normal * 0.004f, hit.normal);
    }

    private void PlayChapterOneImpactBurst(RaycastHit hit)
    {
        var effect = ChapterOneEffect.HandgunImpactBurst;
        if (data.weaponType == WeaponType.Shotgun)
        {
            effect = ChapterOneEffect.ShotgunImpactBurst;
        }
        ChapterOneEffects.TryPlay(effect, hit.point, hit.normal);
    }

    private static bool TryPlayHardSurface(string name, RaycastHit hit)
    {
        ChapterOneEffect effect;
        if (name.Contains("metal", System.StringComparison.OrdinalIgnoreCase) || name.Contains("iron", System.StringComparison.OrdinalIgnoreCase) || name.Contains("铁"))
        {
            effect = ChapterOneEffect.MetalImpact;
        }
        else if (name.Contains("glass", System.StringComparison.OrdinalIgnoreCase) || name.Contains("玻璃"))
        {
            effect = ChapterOneEffect.GlassImpact;
        }
        else
        {
            return false;
        }

        ChapterOneEffects.TryPlay(effect, hit.point + hit.normal * 0.004f, hit.normal);
        return true;
    }
}
