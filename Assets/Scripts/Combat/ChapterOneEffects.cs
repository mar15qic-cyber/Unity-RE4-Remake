using System;
using UnityEngine;

public enum ChapterOneEffect
{
    None, PistolMuzzle, ShotgunMuzzle, RifleMuzzle,
    PistolCase, ShotgunCase, RifleCase,
    StoneImpact, WoodImpact, MetalImpact, DirtImpact, GlassImpact, WaterImpact,
    HandgunBlood, ShotgunBlood, RifleBlood, SlashBlood, StabBlood, ChainsawBlood,
    Parry, PerfectParry, CrateBreak, Assassination,
    HandgunImpactBurst, ShotgunImpactBurst, OtherImpact, KickImpact, PairLandingDust, KnifeWood, KnifeHardSurface
}

public sealed class ChapterOneEffects : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ChapterOneEffect kind;
        public GameObject prefab;
        public float lifetime;
        public int prewarm;
    }

    [SerializeField] private Entry[] effects = Array.Empty<Entry>();
    private static ChapterOneEffects loaded;
    private static bool loadAttempted;
    private static GameObjectPoolManager warmedPool;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        loaded = null;
        loadAttempted = false;
        warmedPool = null;
    }

    private static ChapterOneEffects GetBank()
    {
        if (!loadAttempted)
        {
            loaded = Resources.Load<ChapterOneEffects>("Combat/ChapterOneEffects");
            loadAttempted = true;
        }
        return loaded;
    }

    public static void Prewarm()
    {
        var pool = GameObjectPoolManager.Instance;
        var bank = GetBank();
        if (pool == null || bank == null || pool == warmedPool)
        {
            return;
        }
        foreach (var effect in bank.effects)
        {
            if (effect.prefab != null && effect.prewarm > 0)
            {
                pool.GetVisualPool(effect.prefab).Prewarm(effect.prewarm);
            }
        }
        warmedPool = pool;
    }

    public static bool TryPlay(ChapterOneEffect kind, Vector3 point, Vector3 direction)
    {
        Quaternion rotation = Quaternion.identity;
        if (direction.sqrMagnitude > 0.000001f)
        {
            rotation = Quaternion.LookRotation(direction);
        }
        return Spawn(kind, point, rotation) != null;
    }

    internal static void PlayMuzzle(ChapterOneEffect kind, Transform muzzle)
    {
        var effect = Spawn(kind, muzzle.position, muzzle.rotation);
        if (effect != null && effect.TryGetComponent<MuzzleFlashBillboard>(out var billboard))
        {
            billboard.Follow(muzzle);
        }
    }

    public static void PlayCase(ChapterOneEffect kind, Transform joint, Vector3 offsetMeters, Vector3 velocity, Vector3 spin)
    {
        if (joint == null)
        {
            return;
        }

        // 弹壳偏移以米计，原生枪体骨骼的导入缩放不参与换算。
        var position = joint.position + joint.TransformDirection(offsetMeters);
        var effect = Spawn(kind, position, joint.rotation);
        if (effect != null && effect.TryGetComponent<Rigidbody>(out var body))
        {
            body.linearVelocity = joint.TransformDirection(velocity);
            body.angularVelocity = joint.TransformDirection(spin * Mathf.Deg2Rad);
        }
    }

    private static GameObject Spawn(ChapterOneEffect kind, Vector3 point, Quaternion rotation)
    {
        var bank = GetBank();
        if (bank == null)
        {
            return null;
        }
        foreach (var entry in bank.effects)
        {
            if (entry.kind != kind || entry.prefab == null)
            {
                continue;
            }

            var pool = GameObjectPoolManager.Instance;
            if (pool == null)
            {
                var clone = Instantiate(entry.prefab, point, rotation);
                Destroy(clone, entry.lifetime);
                return clone;
            }

            var effect = pool.GetVisualPool(entry.prefab).Get(point, rotation, null);
            if (effect != null)
            {
                pool.ReturnDelayed(effect, entry.lifetime);
            }
            return effect;
        }
        return null;
    }
}
