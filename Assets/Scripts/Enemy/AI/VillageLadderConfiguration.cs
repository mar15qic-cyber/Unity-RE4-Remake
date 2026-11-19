using System;
using UnityEngine;

[PreferBinarySerialization]
public sealed class VillageLadderConfiguration : ScriptableObject
{
    internal const string ResourceName = "VillageLadderConfiguration";

    [Serializable]
    public sealed class Site
    {
        [Tooltip("在正式村庄中显示的梯口名称")]
        public string displayName;
        [Tooltip("梯脚支点、靠墙支点和角色出梯后的支撑点，使用场景坐标")]
        public Vector3 foot;
        public Vector3 head;
        public Vector3 exit;
        [Tooltip("在梯脚外侧接近和转身的距离")]
        [Min(0.4f)] public float approachDistance = 0.53f;
        [Tooltip("用于直入窗的正式窗口对象名称。平台梯口留空")]
        public string windowName;
        [Tooltip("由活动梯替代的场景装饰梯路径")]
        public string replacedPropPath;
        [Tooltip("正式场景开始时梯子已经架好，可直接攀爬。")]
        public bool initiallyRaised;
    }

    [Serializable]
    public sealed class SkeletonClip
    {
        public Avatar avatar;
        public AnimationClip source;
        public AnimationClip baked;
    }

    [Header("正式村庄梯口")]
    public Site[] sites;
    [Header("敌人全身动画")]
    public AnimationClip raise;
    public AnimationClip mount;
    public AnimationClip climb;
    public AnimationClip platformExit;
    public AnimationClip windowExit;
    public AnimationClip fall;
    public AnimationClip land;
    [Tooltip("正式村民 Generic 骨架的重定向片段")]
    public SkeletonClip[] skeletonClips;
    [Header("里昂架梯与推梯动画")]
    public AnimationClip playerRaise;
    public AnimationClip playerRaiseAlternate;
    [Range(0.1f, 0.9f)] public float playerRaiseGrip = 0.23f;
    [Range(0.1f, 1f)] public float playerRaiseSeat = 0.84f;
    public AnimationClip push;
    [Range(0.1f, 0.9f)] public float pushContact = 0.46f;
    public AnimationClip platformPush;
    [Range(0.1f, 0.9f)] public float platformPushContact = 0.186f;
    [Header("木梯原版模型与接触音效")]
    public Mesh ladderMesh;
    public Material ladderMaterial;
    public AudioEventData gripSound;
    public AudioEventData impactSound;
    public AudioEventData leftRungSound;
    public AudioEventData rightRungSound;
    public AudioEventData kickSound;
    public AudioEventData floorSound;

    [Serializable]
    public sealed class PlayerClips
    {
        public AnimationClip mount;
        public AnimationClip waitLeft;
        public AnimationClip waitRight;
        public AnimationClip climbLeft;
        public AnimationClip climbRight;
        public AnimationClip descendLeft;
        public AnimationClip descendRight;
        public AnimationClip dismountBottom;
        public AnimationClip exitLeft;
        public AnimationClip exitRight;

        internal bool IsComplete => mount != null && waitLeft != null && waitRight != null &&
            climbLeft != null && climbRight != null && descendLeft != null && descendRight != null &&
            dismountBottom != null && exitLeft != null && exitRight != null;
    }

    [Header("里昂原版攀梯动作")]
    public PlayerClips playerFixed;
    public PlayerClips playerLeaning;
    public AnimationClip playerWindowExit;
    public AnimationClip playerJumpDown;
    public AnimationClip playerJumpLand;
    public AnimationClip playerTowerFall;
    public AnimationClip playerTowerLand;
    [Header("村民原版固定梯动作")]
    public AnimationClip fixedEnemyMount;
    public AnimationClip fixedEnemyClimb;
    public AnimationClip fixedEnemyClimbRight;
    public AnimationClip fixedEnemyExit;
    public AnimationClip fixedEnemyFall;
    public AnimationClip fixedEnemyLand;
    public AnimationClip leaningEnemyClimbRight;

    internal AnimationClip Resolve(Animator animator, AnimationClip source)
    {
        if (animator == null || animator.isHuman || skeletonClips == null)
        {
            return source;
        }

        foreach (var clip in skeletonClips)
        {
            if (clip != null && clip.source == source && clip.avatar == animator.avatar && clip.baked != null)
            {
                return clip.baked;
            }
        }

        return source;
    }
}
