using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "战斗/原版村民接触与复起配置")]
public sealed class NativeEnemyCombatProfile : ScriptableObject
{
    [Header("第一章歪脖复起")]
    [Tooltip("符合条件的普通村民受到致命伤时进入濒死的项目概率，以百分比填写。")]
    [Range(0f, 100f)] public float revivalProbability = 20f;
    public float revivalWaitBase = 2.5f;
    public float revivalWaitRandom = 2f;
    [Tooltip("从濒死到复起沿用的生命值，采用项目伤害单位。")]
    [Min(0f)] public float revivalContinuationHealth = 40f;
    [Tooltip("在符合条件的普通村民上启用歪脖复起。")]
    [FormerlySerializedAs("revivalDataVerified")]
    public bool enableRevival;

    [Header("歪脖复起原版声音")]
    public AudioEventData mutationPulse;
    public AudioEventData riseShort;
    public AudioEventData riseLong;
    public AudioEventData riseMiddle;

    [Header("项目命中尺度")]
    [Tooltip("手枪命中头部时用于累积体术失衡的倍率。")]
    [Range(0f, 1f)] public float pistolHeadStaggerMultiplier = .5f;
    [Tooltip("同一次攀梯中，手枪命中头部达到此次数会使存活村民脱手。")]
    [Min(1)] public int pistolLadderHeadHitsToFall = 3;
    [Tooltip("一发散弹在同一敌人上的实际总伤害达到此值时击倒；不是原作伤害数值。")]
    public float shotgunKnockdownDamage = 28f;
    [Tooltip("重击至少命中此数量的散弹弹丸，避免远距离单颗擦伤击倒。")]
    public int shotgunKnockdownPellets = 3;
}
