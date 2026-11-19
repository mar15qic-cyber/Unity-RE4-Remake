using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyAnimationAdapter，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人动画适配器")]
public sealed partial class EnemyAnimationAdapter : MonoBehaviour
{
    /// <summary>中文含义：宿主。使用位置：EnemyAnimationAdapter.ActionCoordinator.get、EnemyAnimationAdapter.actualHorizontalMovementSpeed.get、EnemyAnimationAdapter.actualMovementSpeedResponse.get 等共 35 处。</summary>
    [Header("角色组装引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("宿主")]
    [UnityEngine.InspectorName("宿主")]
    public VillagerController host;
    [SerializeField] private AnimationClip[] legacyClipSources = Array.Empty<AnimationClip>();
    [SerializeField] private AnimationClip[] legacyClipRebindings = Array.Empty<AnimationClip>();

    internal AnimationClip ResolveClipForSkeleton(Animator target, AnimationClip source)
    {
        if (source == null || target == null || target.isHuman || !source.humanMotion)
        {
            return source;
        }

        int index = Array.IndexOf(legacyClipSources, source);
        if (index < 0 || index >= legacyClipRebindings.Length)
        {
            return null;
        }
        return legacyClipRebindings[index];
    }
    /// <summary>中文含义：战斗模块。</summary>
    private Transform bellHead;
    private Quaternion bellHeadBase;
    private Quaternion bellHeadApplied;
    private bool bellHeadAppliedLastFrame;
    private float bellHeadPitch;

    internal void ApplyBellHeadPose(bool looking, Vector3 destination)
    {
        if (bellHead == null)
        {
            if (!looking || anim == null)
            {
                return;
            }
            bellHead = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Head);
            if (bellHead == null)
            {
                return;
            }
        }
        if (bellHeadAppliedLastFrame && Quaternion.Angle(bellHead.rotation, bellHeadApplied) < .01f)
        {
            bellHead.rotation = bellHeadBase;
        }
        bellHeadAppliedLastFrame = false;

        if (!looking)
        {
            bellHeadPitch = 0f;
            return;
        }

        Vector3 direction = destination - bellHead.position;
        float pitch = Mathf.Atan2(direction.y, Vector3.ProjectOnPlane(direction, Vector3.up).magnitude) * Mathf.Rad2Deg;
        bellHeadPitch = Mathf.MoveTowards(bellHeadPitch, Mathf.Clamp(pitch, 0f, 32f), 90f * Time.deltaTime);
        bellHeadBase = bellHead.rotation;
        bellHeadApplied = Quaternion.AngleAxis(-bellHeadPitch, transform.right) * bellHeadBase;
        bellHead.rotation = bellHeadApplied;
        bellHeadAppliedLastFrame = true;
    }

    private EnemyCombatModule CombatModule => host.combatModule;
    /// <summary>中文含义：感知模块。</summary>
    private EnemyPerceptionModule PerceptionModule => host.perceptionModule;
    /// <summary>中文含义：动作协调器。</summary>
    private EnemyActionCoordinator ActionCoordinator => host.actionCoordinator;
    /// <summary>中文含义：动画显示器。</summary>
    private EnemyAnimationAdapter AnimationPresenter => host.animationPresenter;

    /// <summary>中文含义：上一次语音时间。使用位置：EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)、EnemyAnimationAdapter.ReadLastVoiceTime.get。</summary>
    private float lastVoiceTime = float.NegativeInfinity;
    /// <summary>中文含义：读取上一次语音时间。使用位置：VillagerController.lastVoiceTime.get。</summary>
    internal float ReadLastVoiceTime => lastVoiceTime;

    /// <summary>中文含义：是否绕过语音冷却下一次调用。使用位置：EnemyAnimationAdapter.AllowLowerOnceVoiceBypassCooldown()、EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)、EnemyAnimationAdapter.ReadBypassVoiceCooldownNextCall.get。</summary>
    private bool bypassVoiceCooldownNextCall;
    /// <summary>中文含义：是否读取绕过语音冷却下一次调用。使用位置：VillagerController.bypassVoiceCooldownNextCall.get。</summary>
    internal bool ReadBypassVoiceCooldownNextCall => bypassVoiceCooldownNextCall;

    /// <summary>中文含义：当前语音句柄。使用位置：作为 AudioManager.Stop(AudioPlayHandle) 的参数、EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)、EnemyAnimationAdapter.ReadCurrentVoiceHandle.get。</summary>
    private AudioPlayHandle currentVoiceHandle;
    private float currentVoiceEndTime;
    private AudioEventData.VoicePriority currentVoicePriority;
    /// <summary>中文含义：读取当前语音句柄。使用位置：VillagerController.currentVoiceHandle.get。</summary>
    internal AudioPlayHandle ReadCurrentVoiceHandle => currentVoiceHandle;

    /// <summary>中文含义：是否具有移动行为播放速率。使用位置：EnemyAnimationAdapter.InitializeAnimationParameter()、EnemyAnimationAdapter.ReadHasLocomotionPlaybackRate.get、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private bool hasLocomotionPlaybackRate;
    /// <summary>中文含义：读取是否具有移动行为播放速率。使用位置：VillagerController.hasLocomotionPlaybackRate.get。</summary>
    internal bool ReadHasLocomotionPlaybackRate => hasLocomotionPlaybackRate;

    /// <summary>中文含义：移动行为播放速率。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、EnemyAnimationAdapter.ReadLocomotionPlaybackRate.get 等共 4 处。</summary>
    private float locomotionPlaybackRate = 1f;
    /// <summary>中文含义：行走动画片段。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetCurrentAnimatorClipInfo(int, List&lt;AnimatorClipInfo&gt;) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private readonly List<AnimatorClipInfo> walkClip = new List<AnimatorClipInfo>(8);
    /// <summary>中文含义：读取移动行为播放速率。使用位置：VillagerController.locomotionPlaybackRate.get。</summary>
    internal float ReadLocomotionPlaybackRate => locomotionPlaybackRate;

    /// <summary>中文含义：是否生命比例参数可用。使用位置：EnemyAnimationAdapter.InitializeAnimationParameter()、EnemyAnimationAdapter.ReadHealthRatioParameterAvailable.get、EnemyAnimationAdapter.UpdateHealthAnimationParameter()。</summary>
    private bool healthRatioParameterAvailable;
    /// <summary>中文含义：是否读取生命比例参数可用。使用位置：VillagerController.healthRatioParameterAvailable.get。</summary>
    internal bool ReadHealthRatioParameterAvailable => healthRatioParameterAvailable;

    /// <summary>中文含义：是否硬直触发器参数可用。使用位置：EnemyAnimationAdapter.ApplyDeathAnimatorParameters()、EnemyAnimationAdapter.InitializeAnimationParameter()、EnemyAnimationAdapter.ReadStaggerTriggerParameterAvailable.get。</summary>
    private bool staggerTriggerParameterAvailable;
    /// <summary>中文含义：是否读取硬直触发器参数可用。使用位置：EnemyCombatModule.staggerTriggerParameterAvailable.get、VillagerController.staggerTriggerParameterAvailable.get。</summary>
    internal bool ReadStaggerTriggerParameterAvailable => staggerTriggerParameterAvailable;

    /// <summary>中文含义：请求的站立移动参数。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数、作为 VillagerController.CalculateStandingAdvanceSpeed(float) 的参数、EnemyAnimationAdapter.ReadRequestedStandingMoveParameter.get 等共 5 处。</summary>
    private float requestedStandingMoveParameter;
    /// <summary>中文含义：读取请求的站立移动参数。使用位置：VillagerController.requestedStandingMoveParameter.get。</summary>
    internal float ReadRequestedStandingMoveParameter => requestedStandingMoveParameter;

    /// <summary>中文含义：上一次站立指令指定的速度。使用位置：EnemyAnimationAdapter.ReadLastStandingCommandedSpeed.get、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.UpdateAnimator() 等共 4 处。</summary>
    private float lastStandingCommandedSpeed;
    /// <summary>中文含义：读取上一次站立指令指定的速度。使用位置：VillagerController.lastStandingCommandedSpeed.get。</summary>
    internal float ReadLastStandingCommandedSpeed => lastStandingCommandedSpeed;

    /// <summary>中文含义：是否此前具有站立移动意图。使用位置：EnemyAnimationAdapter.ReadHadStandingMoveIntent.get、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.UpdateAnimator()。</summary>
    private bool hadStandingMoveIntent;
    /// <summary>中文含义：是否读取此前具有站立移动意图。使用位置：VillagerController.hadStandingMoveIntent.get。</summary>
    internal bool ReadHadStandingMoveIntent => hadStandingMoveIntent;

    /// <summary>中文含义：站立移动行为是否可见。使用位置：EnemyAnimationAdapter.ReadStandingLocomotionVisible.get、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.UpdateAnimator() 等共 4 处。</summary>
    private bool standingLocomotionVisible;
    /// <summary>中文含义：读取站立移动行为是否可见。使用位置：VillagerController.standingLocomotionVisible.get。</summary>
    internal bool ReadStandingLocomotionVisible => standingLocomotionVisible;

    /// <summary>中文含义：移动行为阻挡持续时间。使用位置：EnemyAnimationAdapter.ReadLocomotionBlockedDuration.get、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.UpdateAnimator() 等共 4 处。</summary>
    private float locomotionBlockedDuration;
    /// <summary>中文含义：读取移动行为阻挡持续时间。使用位置：VillagerController.locomotionBlockedDuration.get。</summary>
    internal float ReadLocomotionBlockedDuration => locomotionBlockedDuration;

    /// <summary>中文含义：移动行为移动中持续时间。使用位置：EnemyAnimationAdapter.ReadLocomotionMovingDuration.get、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.UpdateAnimator() 等共 4 处。</summary>
    private float locomotionMovingDuration;
    /// <summary>中文含义：读取移动行为移动中持续时间。使用位置：VillagerController.locomotionMovingDuration.get。</summary>
    internal float ReadLocomotionMovingDuration => locomotionMovingDuration;

    /// <summary>中文含义：站立移动意图开始时间。使用位置：EnemyAnimationAdapter.ReadStandingMoveIntentStartedTime.get、EnemyAnimationAdapter.UpdateAnimator()、EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。</summary>
    private float standingMoveIntentStartedTime = float.NegativeInfinity;
    /// <summary>中文含义：读取站立移动意图开始时间。使用位置：VillagerController.standingMoveIntentStartedTime.get。</summary>
    internal float ReadStandingMoveIntentStartedTime => standingMoveIntentStartedTime;
    /// <summary>中文含义：当前移动模式。使用位置：EnemyAnimationAdapter.UpdateAnimator()、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private EnemyMovementMode CurrentMovementMode => host.ArchitectureReadCurrentMovementMode;
    /// <summary>中文含义：当前状态。使用位置：EnemyAnimationAdapter.UpdateAnimator()。</summary>
    private VillagerState CurrentState => host.ArchitectureReadCurrentState;
    /// <summary>中文含义：攻击的哈希编号。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static int HashATK => VillagerController.ArchitectureReadHashATK;
    /// <summary>中文含义：爬行待机路径的哈希编号。使用位置：作为 Animator.HasState(int, int) 的参数、EnemyAnimationAdapter.PlayCrawlIdleImmediately()。</summary>
    private static int HashCrawlIdlePath => VillagerController.ArchitectureReadHashCrawlIdlePath;
    /// <summary>中文含义：爬行待机状态的哈希编号。使用位置：作为 Animator.HasState(int, int) 的参数、EnemyAnimationAdapter.PlayCrawlIdleImmediately()。</summary>
    private static int HashCrawlIdleState => VillagerController.ArchitectureReadHashCrawlIdleState;
    /// <summary>中文含义：爬行子级状态路径的哈希编号。使用位置：作为 Animator.HasState(int, int) 的参数、EnemyAnimationAdapter.PlayCrawlIdleImmediately()。</summary>
    private static int HashCrawlSubStatePath => VillagerController.ArchitectureReadHashCrawlSubStatePath;
    /// <summary>中文含义：死亡的哈希编号。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static int HashDIE => VillagerController.ArchitectureReadHashDIE;
    /// <summary>中文含义：命中的哈希编号。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static int HashHIT => VillagerController.ArchitectureReadHashHIT;
    /// <summary>中文含义：生命比例的哈希编号。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static int HashHealthRatio => VillagerController.ArchitectureReadHashHealthRatio;
    /// <summary>中文含义：命中触发器的哈希编号。使用位置：作为 Animator.ResetTrigger(int) 的参数。</summary>
    private static int HashHitTrigger => VillagerController.ArchitectureReadHashHitTrigger;
    /// <summary>中文含义：移动行为播放速率的哈希编号。使用位置：作为 Animator.SetFloat(int, float) 的参数、EnemyAnimationAdapter.InitializeAnimationParameter()。</summary>
    private static int HashLocomotionPlaybackRate => VillagerController.ArchitectureReadHashLocomotionPlaybackRate;
    /// <summary>中文含义：移动速度的哈希编号。使用位置：作为 Animator.GetFloat(int) 的参数、作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static int HashMoveSpeed => VillagerController.ArchitectureReadHashMoveSpeed;
    /// <summary>中文含义：移动横向的哈希编号。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static int HashMoveX => VillagerController.ArchitectureReadHashMoveX;
    /// <summary>中文含义：移动纵向的哈希编号。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static int HashMoveY => VillagerController.ArchitectureReadHashMoveY;
    /// <summary>中文含义：硬直触发器的哈希编号。使用位置：作为 Animator.ResetTrigger(int) 的参数。</summary>
    private static int HashStaggerTrigger => VillagerController.ArchitectureReadHashStaggerTrigger;
    /// <summary>中文含义：行走的哈希编号。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static int HashWALK => VillagerController.ArchitectureReadHashWALK;
    /// <summary>中文含义：实际水平移动速度。使用位置：EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。</summary>
    private float actualHorizontalMovementSpeed => host.ArchitectureReadActualHorizontalMovementSpeed;
    /// <summary>中文含义：实际移动速度响应。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private float actualMovementSpeedResponse => host.actualMovementSpeedResponse;
    /// <summary>中文含义：寻路代理。使用位置：EnemyAnimationAdapter.UpdateAnimator()。</summary>
    private PathAgent agent => host.ArchitectureReadAgent;
    /// <summary>中文含义：动画控制器。使用位置：EnemyAnimationAdapter.ApplyDeathAnimatorParameters()、EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.InitializeAnimationParameter() 等共 18 处。</summary>
    private Animator anim => host.ArchitectureReadAnim;
    /// <summary>中文含义：当前敌人上下文。使用位置：EnemyAnimationAdapter.UpdateAnimator()、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private EnemyContext ctx => host.ArchitectureReadCtx;
    /// <summary>中文含义：当前生命值。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    private float currentHealth => host.currentHealth;
    /// <summary>中文含义：保持站立移动行为期间是否受阻。使用位置：EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。</summary>
    private bool keepStandingLocomotionWhileBlocked => host.keepStandingLocomotionWhileBlocked;
    /// <summary>中文含义：上一次稳定爬行移动方向。使用位置：EnemyAnimationAdapter.UpdateAnimator()。</summary>
    private Vector3 lastStableCrawlMoveDirection => host.ArchitectureReadLastStableCrawlMoveDirection;
    /// <summary>中文含义：上一次稳定站立移动方向。使用位置：EnemyAnimationAdapter.UpdateAnimator()。</summary>
    private Vector3 lastStableStandingMoveDirection => host.ArchitectureReadLastStableStandingMoveDirection;
    /// <summary>中文含义：移动行为重新开始速度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    private float locomotionRestartSpeed => host.locomotionRestartSpeed;
    /// <summary>中文含义：移动行为起始确认持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    private float locomotionStartConfirmationDuration => host.locomotionStartConfirmationDuration;
    /// <summary>中文含义：移动行为停止确认持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    private float locomotionStopConfirmationDuration => host.locomotionStopConfirmationDuration;
    /// <summary>中文含义：移动行为停止速度。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private float locomotionStopSpeed => host.locomotionStopSpeed;
    /// <summary>中文含义：最大生命值。使用位置：作为 Mathf.Clamp01(float) 的参数、EnemyAnimationAdapter.UpdateHealthAnimationParameter()。</summary>
    private float maxHealth => host.maxHealth;
    /// <summary>中文含义：移动动画起始宽限持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    private float movementAnimationStartGraceDuration => host.movementAnimationStartGraceDuration;
    /// <summary>中文含义：移动混合速度。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。</summary>
    private float movementBlendSpeed => host.movementBlendSpeed;
    /// <summary>中文含义：移动方向混合时间。使用位置：作为 Animator.SetFloat(string, float, float, float) 的参数、EnemyAnimationAdapter.SetMovementDirection(float, float)。</summary>
    private float movementDirectionBlendTime => host.movementDirectionBlendTime;
    /// <summary>中文含义：是否使用站立动画速度用于路径。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</summary>
    private bool useStandingAnimationSpeedForPath => host.useStandingAnimationSpeedForPath;
    /// <summary>中文含义：语音最小间隔。使用位置：EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。</summary>
    private float voiceMinInterval => host.voiceMinInterval;
    /// <summary>中文含义：语音配置档。使用位置：EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。</summary>
    private ZombieAudioProfile voiceProfile => host.voiceProfile;

    /// <summary>GetTargetMoveSpeed 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeGetTargetMoveSpeed()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()；下游（含内部回调体） VillagerController.ArchitectureInvokeGetTargetMoveSpeed()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float GetTargetMoveSpeed()
    {
        return host.ArchitectureInvokeGetTargetMoveSpeed();
    }

    /// <summary>HasCrawlMovementDirection 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeHasCrawlMovementDirection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()；下游（含内部回调体） VillagerController.ArchitectureInvokeHasCrawlMovementDirection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasCrawlMovementDirection()
    {
        return host.ArchitectureInvokeHasCrawlMovementDirection();
    }

    /// <summary>ResolveStandingPathDirectionForCurrentFrame 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()；下游（含内部回调体） VillagerController.ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveStandingPathDirectionForCurrentFrame()
    {
        return host.ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame();
    }

    /// <summary>UpdateAnimator 的业务处理入口；本方法直接依赖 Animator.GetFloat(string)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、EnemyAnimationAdapter.GetTargetMoveSpeed()、EnemyAnimationAdapter.HasCrawlMovementDirection()、EnemyAnimationAdapter.ResolveStandingPathDirectionForCurrentFrame()、EnemyAnimationAdapter.SetMovementDirection(float, float) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateAnimator()；下游（含内部回调体） Animator.GetFloat(string)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、EnemyAnimationAdapter.GetTargetMoveSpeed()、EnemyAnimationAdapter.HasCrawlMovementDirection()、EnemyAnimationAdapter.ResolveStandingPathDirectionForCurrentFrame()、EnemyAnimationAdapter.SetMovementDirection(float, float) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateAnimator()
    {
        if (anim == null)
        {
            return;
        }

        if (CurrentState == VillagerState.Staggered || CurrentState == VillagerState.Dead)
        {
            requestedStandingMoveParameter = 0f;
            lastStandingCommandedSpeed = 0f;
            hadStandingMoveIntent = false;
            standingLocomotionVisible = false;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
            anim.SetFloat(HashMoveSpeed, 0f);
            anim.SetFloat(HashMoveX, 0f);
            anim.SetFloat(HashMoveY, 0f);
            anim.SetBool(HashWALK, false);
            return;
        }

        if (CurrentState == VillagerState.Crawl)
        {
            requestedStandingMoveParameter = 0f;
            lastStandingCommandedSpeed = 0f;
            hadStandingMoveIntent = false;
            standingLocomotionVisible = false;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
            if (agent == null || !agent.enabled)
            {
                return;
            }

            // crawlMoveSpeed（爬行移动速度）：使用位置：作为 Animator.SetFloat(int, float) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
float crawlMoveSpeed;
            if (HasCrawlMovementDirection())
            {
                crawlMoveSpeed = ctx.CrawlSpeed;
            }
            else
            {
                crawlMoveSpeed = 0f;
            }

            anim.SetFloat(HashMoveSpeed, crawlMoveSpeed);
            agent.speed = ctx.CrawlSpeed;
            // crawlAnimDirection（爬行动画方向）：使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
Vector3 crawlAnimDirection = lastStableCrawlMoveDirection;
            if (crawlAnimDirection.sqrMagnitude <= 0.0001f && agent != null && agent.enabled && !agent.isStopped && agent.desiredDirection.sqrMagnitude > 0.0001f)
            {
                crawlAnimDirection = agent.desiredDirection;
            }

            if (crawlAnimDirection.sqrMagnitude > 0.0001f)
            {
                // localDir（局部方向）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 EnemyAnimationAdapter.SetMovementDirection(float, float) 的参数。
Vector3 localDir = transform.InverseTransformDirection(crawlAnimDirection.normalized);
                SetMovementDirection(localDir.x, localDir.z);
            }

            return;
        }

        if (agent == null || !agent.enabled)
        {
            return;
        }

        // moveDirection（移动方向）：使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
Vector3 moveDirection;
        if (CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion)
        {
            moveDirection = ResolveStandingPathDirectionForCurrentFrame();
        }
        else
        {
            moveDirection = lastStableStandingMoveDirection;
        }

        // hasMoveIntent（是否具有移动意图）：使用位置：作为 EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
bool hasMoveIntent = CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && !agent.isStopped && moveDirection.sqrMagnitude > 0.0001f;
        // lv（层级）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 EnemyAnimationAdapter.SetMovementDirection(float, float) 的参数。
Vector3 lv = transform.InverseTransformDirection(moveDirection);
        try
        {
            // requestedTargetMoveSpeed（请求的目标移动速度）：使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
float requestedTargetMoveSpeed;
            if (hasMoveIntent)
            {
                requestedTargetMoveSpeed = ctx.ClampStandingMoveParameter(GetTargetMoveSpeed());
            }
            else
            {
                requestedTargetMoveSpeed = 0f;
            }

            requestedStandingMoveParameter = Mathf.MoveTowards(requestedStandingMoveParameter, requestedTargetMoveSpeed, movementBlendSpeed * Time.deltaTime);
            // commandedSpeed（指令指定的速度）：保存 VillagerController.CalculateStandingAdvanceSpeed(float) 的返回结果，供当前步骤使用。使用位置：EnemyAnimationAdapter.UpdateAnimator()。
float commandedSpeed = host.CalculateStandingAdvanceSpeed(requestedStandingMoveParameter);
            if (hasMoveIntent && !hadStandingMoveIntent)
            {
                standingMoveIntentStartedTime = Time.time;
                standingLocomotionVisible = true;
                locomotionBlockedDuration = 0f;
                locomotionMovingDuration = 0f;
            }

            hadStandingMoveIntent = hasMoveIntent;
            if (hasNativeGaitPace)
            {
                standingLocomotionVisible = hasMoveIntent;
            }
            else
            {
                UpdateStandingLocomotionVisibility(hasMoveIntent);
            }
            // actualTargetMoveSpeed（实际目标移动速度）：使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数、EnemyAnimationAdapter.UpdateAnimator()。
float actualTargetMoveSpeed;
            if (standingLocomotionVisible)
            {
                actualTargetMoveSpeed = requestedStandingMoveParameter;
            }
            else
            {
                actualTargetMoveSpeed = 0f;
            }

            // currentMoveSpeed（当前移动速度）：保存 Animator.GetFloat(string) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。
float currentMoveSpeed = anim.GetFloat("MoveSpeed");
            // newMoveSpeed（新移动速度）：保存 Mathf.MoveTowards(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Animator.SetBool(int, bool) 的参数、作为 Animator.SetFloat(int, float) 的参数。
float newMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, actualTargetMoveSpeed, movementBlendSpeed * Time.deltaTime);
            anim.SetFloat(HashMoveSpeed, newMoveSpeed);
            agent.speed = commandedSpeed;
            lastStandingCommandedSpeed = commandedSpeed;
            anim.SetBool(HashWALK, newMoveSpeed > 0.02f);
            SetMovementDirection(lv.x, lv.z);
        }
        catch
        {
        }
    }

    /// <summary>UpdateStandingLocomotionVisibility 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()、VillagerController.UpdateStandingLocomotionVisibility(bool)；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hasMoveIntent">中文含义：是否具有移动意图。使用位置：EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。</param>
    internal void UpdateStandingLocomotionVisibility(bool hasMoveIntent)
    {
        if (!hasMoveIntent)
        {
            standingLocomotionVisible = false;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
            return;
        }

        // 追击方向仍然有效时，碰撞只表示当前帧不能前进，不能再反向
        // 关闭走路动画。旧逻辑会先因碰撞停掉动画，再因动画没有根位移
        // 停掉代码推进，表现为敌人一抽一抽或原地搜寻。
        if (keepStandingLocomotionWhileBlocked)
        {
            standingLocomotionVisible = true;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
            return;
        }

        // insideStartGrace（是否内部起始宽限）：使用位置：EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。
bool insideStartGrace = Time.time - standingMoveIntentStartedTime <= Mathf.Max(0f, movementAnimationStartGraceDuration);
        if (insideStartGrace || lastStandingCommandedSpeed <= 0.01f)
        {
            standingLocomotionVisible = true;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
            return;
        }

        if (standingLocomotionVisible)
        {
            if (actualHorizontalMovementSpeed > Mathf.Max(0f, locomotionStopSpeed))
            {
                locomotionBlockedDuration = 0f;
                return;
            }

            locomotionBlockedDuration += Time.deltaTime;
            if (locomotionBlockedDuration >= Mathf.Max(0.05f, locomotionStopConfirmationDuration))
            {
                standingLocomotionVisible = false;
                locomotionBlockedDuration = 0f;
                locomotionMovingDuration = 0f;
            }

            return;
        }

        // restartSpeed（重新开始速度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。
float restartSpeed = Mathf.Max(locomotionStopSpeed + 0.01f, locomotionRestartSpeed);
        if (actualHorizontalMovementSpeed < restartSpeed)
        {
            locomotionMovingDuration = 0f;
            return;
        }

        locomotionMovingDuration += Time.deltaTime;
        if (locomotionMovingDuration >= Mathf.Max(0.05f, locomotionStartConfirmationDuration))
        {
            standingLocomotionVisible = true;
            locomotionBlockedDuration = 0f;
            locomotionMovingDuration = 0f;
        }
    }

    /// <summary>SetMovementDirection 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、Animator.SetFloat(string, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()、VillagerController.SetMovementDirection(float, float)；下游（含内部回调体） Animator.SetFloat(int, float)、Animator.SetFloat(string, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="moveX">中文含义：移动横向。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Animator.SetFloat(string, float, float, float) 的参数。</param>
    /// <param name="moveY">中文含义：移动纵向。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Animator.SetFloat(string, float, float, float) 的参数。</param>
    internal void SetMovementDirection(float moveX, float moveY)
    {
        if (movementDirectionBlendTime <= 0f)
        {
            anim.SetFloat(HashMoveX, moveX);
            anim.SetFloat(HashMoveY, moveY);
            return;
        }

        anim.SetFloat("MoveX", moveX, movementDirectionBlendTime, Time.deltaTime);
        anim.SetFloat("MoveY", moveY, movementDirectionBlendTime, Time.deltaTime);
    }

    /// <summary>UpdateHealthAnimationParameter 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.UpdateHealthAnimationParameter()、VillagerController.UpdateHealthAnimationParameter()；下游（含内部回调体） Animator.SetFloat(int, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateHealthAnimationParameter()
    {
        if (anim == null || !healthRatioParameterAvailable)
        {
            return;
        }

        // healthRatio（生命比例）：使用位置：作为 Animator.SetFloat(int, float) 的参数、EnemyAnimationAdapter.UpdateHealthAnimationParameter()。
float healthRatio;
        if (maxHealth > 0f)
        {
            healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
        }
        else
        {
            healthRatio = 0f;
        }

        anim.SetFloat(HashHealthRatio, healthRatio);
    }

    /// <summary>PlayCrawlIdleImmediately 的业务处理入口；本方法直接依赖 Animator.HasState(int, int)、Animator.Play(int, int, float)、Animator.Update(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.PlayCrawlIdleImmediately()、VillagerController.PlayCrawlIdleImmediately()；下游（含内部回调体） Animator.HasState(int, int)、Animator.Play(int, int, float)、Animator.Update(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void PlayCrawlIdleImmediately()
    {
        if (anim == null)
        {
            return;
        }

        // stateHash（状态哈希编号）：使用位置：作为 Animator.Play(int, int, float) 的参数、EnemyAnimationAdapter.PlayCrawlIdleImmediately()。
int stateHash = 0;
        if (anim.HasState(0, HashCrawlIdlePath))
        {
            stateHash = HashCrawlIdlePath;
        }
        else if (anim.HasState(0, HashCrawlSubStatePath))
        {
            stateHash = HashCrawlSubStatePath;
        }
        else if (anim.HasState(0, HashCrawlIdleState))
        {
            stateHash = HashCrawlIdleState;
        }

        if (stateHash == 0)
        {
            return;
        }

        anim.Play(stateHash, 0, 0f);
        anim.Update(0f);
    }

    /// <summary>ApplyDeathAnimatorParameters 的业务处理入口；本方法直接依赖 Animator.ResetTrigger(int)、Animator.SetBool(int, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApplyDeathAnimatorParameters()、VillagerController.ApplyDeathAnimatorParameters()；下游（含内部回调体） Animator.ResetTrigger(int)、Animator.SetBool(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ApplyDeathAnimatorParameters()
    {
        if (anim == null)
        {
            return;
        }

        anim.SetBool(HashHIT, false);
        anim.SetBool(HashATK, false);
        anim.SetBool(HashDIE, true);
        anim.ResetTrigger(HashHitTrigger);
        if (staggerTriggerParameterAvailable)
        {
            anim.ResetTrigger(HashStaggerTrigger);
        }
    }

    /// <summary>PlayVoice 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、AudioManager.Stop(AudioPlayHandle)、object.ReferenceEquals(object, object)、ZombieAudioProfile.GetEvent(ZombieVoiceType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.PlayVoice(ZombieVoiceType)、EnemyCombatModule.PlayVoice(ZombieVoiceType)、EnemyPerceptionModule.PlayVoice(ZombieVoiceType)、VillagerController.PlayVoice(ZombieVoiceType)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、AudioManager.Stop(AudioPlayHandle)、object.ReferenceEquals(object, object)、ZombieAudioProfile.GetEvent(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">中文含义：语音类型。使用位置：作为 ZombieAudioProfile.GetEvent(ZombieVoiceType) 的参数、EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。</param>
    internal void PlayVoice(ZombieVoiceType voiceType)
    {
        if (voiceProfile == null)
        {
            return;
        }

        // eventData（事件配置）：保存 ZombieAudioProfile.GetEvent(ZombieVoiceType) 的返回结果，供当前步骤使用。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。
AudioEventData eventData = voiceProfile.GetEvent(voiceType);
        if (eventData == null)
        {
            return;
        }

        bool isCritical = voiceType == ZombieVoiceType.Death;
        bool bypassCooldownOnce = bypassVoiceCooldownNextCall;
        bypassVoiceCooldownNextCall = false;
        bool voicePlaying = currentVoiceHandle.IsValid && Time.time < currentVoiceEndTime;
        bool overridesPriority = voicePlaying && eventData.voicePriority > currentVoicePriority;
        if (!isCritical && !bypassCooldownOnce && !overridesPriority)
        {
            if (Time.time - lastVoiceTime < voiceMinInterval || (voicePlaying && eventData.voicePriority < currentVoicePriority))
            {
                return;
            }
        }

        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        AudioPlayHandle handle = audio.Play3D(eventData, transform.position);
        if (handle.IsValid)
        {
            if (currentVoiceHandle.IsValid)
            {
                audio.Stop(currentVoiceHandle);
            }

            currentVoiceHandle = handle;
            currentVoicePriority = eventData.voicePriority;
            currentVoiceEndTime = Time.time + handle.source.clip.length / Mathf.Max(0.01f, Mathf.Abs(handle.source.pitch));
            lastVoiceTime = Time.time;
        }
    }

    internal void ReleaseVoice()
    {
        if (currentVoiceHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(currentVoiceHandle);
        }

        currentVoiceHandle = default;
        currentVoiceEndTime = 0f;
        lastVoiceTime = float.NegativeInfinity;
        bypassVoiceCooldownNextCall = false;
    }

    /// <summary>UpdateLocomotionPlaybackRate 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorClipInfo(int, List&lt;AnimatorClipInfo&gt;)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.SetFloat(int, float)、AnimatorStateInfo.IsName(string)、EnemyContext.MoveSpeedToAgentSpeed(float)、Mathf.Clamp(float, float, float) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateLocomotionPlaybackRate(Vector3)；下游（含内部回调体） Animator.GetCurrentAnimatorClipInfo(int, List&lt;AnimatorClipInfo&gt;)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.SetFloat(int, float)、AnimatorStateInfo.IsName(string)、EnemyContext.MoveSpeedToAgentSpeed(float)、Mathf.Clamp(float, float, float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementStartPosition">中文含义：移动起始位置。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。</param>
    internal void UpdateLocomotionPlaybackRate(Vector3 movementStartPosition)
    {
        if (!hasLocomotionPlaybackRate || anim == null)
        {
            return;
        }

        if (host.UsesNativeStandingStride)
        {
            locomotionPlaybackRate = 1f;
            anim.SetFloat(HashLocomotionPlaybackRate, locomotionPlaybackRate);
            return;
        }

        // moveParameter（移动参数）：保存 Animator.GetFloat(int) 的返回结果，供当前步骤使用。使用位置：作为 EnemyContext.MoveSpeedToAgentSpeed(float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float moveParameter = anim.GetFloat(HashMoveSpeed);
        if (CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion || moveParameter <= 0.02f)
        {
            locomotionPlaybackRate = 1f;
        }
        else
        {
            // 使用本帧经过碰撞、避让和转向限速后的真实位移同步脚步。
            // 参数只绑定移动状态，攻击、翻窗和配对动作保持原有播放节奏。
            // displacement（位移）：使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
Vector3 displacement = transform.position - movementStartPosition;
            displacement.y = 0f;
            // actualSpeed（实际速度）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float actualSpeed = displacement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            // strideSpeed（步幅速度）：保存 EnemyContext.MoveSpeedToAgentSpeed(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float strideSpeed = ctx.MoveSpeedToAgentSpeed(moveParameter);
            // 代码推进的村民完全受阻时暂停脚步，避免最低倍率继续原地踏动。
            // 动画根运动推进仍保留起步倍率，防止动画与位移互相等待。
            // multiplierMaximum（倍率最大）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float multiplierMaximum = 1.15f;
            if (host.ArchitectureReadChainsawBridge && anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion"))
            {
                // 电锯慢走的原始步幅比村民短，按当前混合片段的水平根速度换算实际步频。
                anim.GetCurrentAnimatorClipInfo(0, walkClip);
                // clipSpeed（动画片段速度）：使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float clipSpeed = 0f;
                // clipValue（动画片段值）：每轮从行走动画片段取得一个元素，仅代表当前这一项。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
foreach (var clipValue in walkClip)
                {
                    // speed（速度）：使用位置：作为 Mathf.Sqrt(float) 的参数。
var speed = clipValue.clip.averageSpeed;
                    clipSpeed += clipValue.weight * Mathf.Sqrt(speed.x * speed.x + speed.z * speed.z);
                }

                if (clipSpeed > .1f)
                {
                    strideSpeed = clipSpeed;
                }

                multiplierMaximum = 2.5f;
            }

            // minimumRate（最小速率）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float minimumRate;
            if (useStandingAnimationSpeedForPath)
            {
                minimumRate = 0.1f;
            }
            else
            {
                minimumRate = 0f;
            }

            // targetRate（目标速率）：保存 Mathf.Clamp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float targetRate = Mathf.Clamp(actualSpeed / Mathf.Max(0.01f, strideSpeed), minimumRate, multiplierMaximum);
            if (!useStandingAnimationSpeedForPath && actualSpeed <= locomotionStopSpeed)
            {
                locomotionPlaybackRate = 0f;
            }
            else
            {
                // leftOperand10（计算前缓存的左侧实际移动速度响应）：从实际移动速度响应读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float leftOperand10 = actualMovementSpeedResponse;
                // selectedValue9（分支选出的2f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。
float selectedValue9;
                if (targetRate < locomotionPlaybackRate)
                {
                    selectedValue9 = 2f;
                }
                else
                {
                    selectedValue9 = 1f;
                }

                // response（响应）：使用位置：作为 Mathf.Exp(float) 的参数。
float response = leftOperand10 * (selectedValue9);
                locomotionPlaybackRate = Mathf.Lerp(locomotionPlaybackRate, targetRate, 1f - Mathf.Exp(-response * Time.deltaTime));
            }
        }

        anim.SetFloat(HashLocomotionPlaybackRate, locomotionPlaybackRate);
    }

    /// <summary>AllowLowerOnceVoiceBypassCooldown 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void AllowLowerOnceVoiceBypassCooldown()
    {
        bypassVoiceCooldownNextCall = true;
    }

    /// <summary>ResetMovementPresentationCache 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.StopAndClearMovement()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ResetMovementPresentationCache()
    {
        requestedStandingMoveParameter = 0f;
        lastStandingCommandedSpeed = 0f;
        hadStandingMoveIntent = false;
        standingLocomotionVisible = false;
        locomotionBlockedDuration = 0f;
        locomotionMovingDuration = 0f;
    }

    /// <summary>InitializeAnimationParameter 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void InitializeAnimationParameter()
    {
        if (anim == null)
        {
            return;
        }

        anim.applyRootMotion = host.useRootMotion;
        // parameter（参数）：每轮从动画控制器的参数集合取得一个元素，仅代表当前这一项。使用位置：EnemyAnimationAdapter.InitializeAnimationParameter()。
foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.nameHash == HashNativeGaitPace && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasNativeGaitPace = true;
            }

            if (parameter.nameHash == HashLocomotionPlaybackRate && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasLocomotionPlaybackRate = true;
            }

            if (parameter.name == "HealthRatio" && parameter.type == AnimatorControllerParameterType.Float)
            {
                healthRatioParameterAvailable = true;
            }

            if (parameter.name == "StaggerTrigger" && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                staggerTriggerParameterAvailable = true;
            }
        }
    }

    // 所有敌人动画写入统一经过此处，调用方只提交表现参数。
    /// <summary>SetBoolean 的业务处理入口；本方法直接依赖 Animator.SetBool(int, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()、EnemyActionCoordinator.BeginStationaryPatrol(float)、EnemyActionCoordinator.EndStationaryPatrol()、EnemyCombatModule.ApplyKnifeParryStagger(bool) 等共 26 处；下游（含内部回调体） Animator.SetBool(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.SetBool(int, bool) 的参数。</param>
    /// <param name="value">中文含义：是否值。使用位置：作为 Animator.SetBool(int, bool) 的参数。</param>
    internal void SetBoolean(int parameter, bool value)
    {
        if (anim != null)
        {
            anim.SetBool(parameter, value);
        }
    }

    /// <summary>SetIntegerValue 的业务处理入口；本方法直接依赖 Animator.SetInteger(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginStationaryPatrol(float)、EnemyActionCoordinator.EndStationaryPatrol()、EnemyActionCoordinator.ReleaseStationaryPatrolTurn()、EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginHit() 等共 13 处；下游（含内部回调体） Animator.SetInteger(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.SetInteger(int, int) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：作为 Animator.SetInteger(int, int) 的参数。</param>
    internal void SetIntegerValue(int parameter, int value)
    {
        if (anim != null)
        {
            anim.SetInteger(parameter, value);
        }
    }

    /// <summary>SetFloatValue 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginStationaryPatrol(float)、EnemyActionCoordinator.EndStationaryPatrol()、EnemyCombatModule.BeginCrawlAttack()、VillagerController.MaintainAssassinationShowcaseIdle()、VillagerController.StopAndClearMovement()；下游（含内部回调体） Animator.SetFloat(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.SetFloat(int, float) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：作为 Animator.SetFloat(int, float) 的参数。</param>
    internal void SetFloatValue(int parameter, float value)
    {
        if (anim != null)
        {
            anim.SetFloat(parameter, value);
        }
    }

    /// <summary>SetFloatValue 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.SetFloat(int, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.SetFloat(int, float, float, float) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：作为 Animator.SetFloat(int, float, float, float) 的参数。</param>
    /// <param name="smoothing">中文含义：平滑。使用位置：作为 Animator.SetFloat(int, float, float, float) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Animator.SetFloat(int, float, float, float) 的参数。</param>
    internal void SetFloatValue(int parameter, float value, float smoothing, float duration)
    {
        if (anim != null)
        {
            anim.SetFloat(parameter, value, smoothing, duration);
        }
    }

    /// <summary>TriggerPresentation 的业务处理入口；本方法直接依赖 Animator.SetTrigger(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginChainsawStart()、ChainsawManAnimationBridge.BeginIntroTaunt(float)、EnemyCombatModule.BeginHit()；下游（含内部回调体） Animator.SetTrigger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.SetTrigger(int) 的参数。</param>
    internal void TriggerPresentation(int parameter)
    {
        if (anim != null)
        {
            anim.SetTrigger(parameter);
        }
    }

    /// <summary>ClearTrigger 的业务处理入口；本方法直接依赖 Animator.ResetTrigger(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginIntroTaunt(float)、ChainsawManAnimationBridge.CancelChainsawStart()、ChainsawManAnimationBridge.CancelIntroTaunt()、ChainsawManAnimationBridge.ResetRuntimeState()、EnemyCombatModule.ClearActionParameters() 等共 6 处；下游（含内部回调体） Animator.ResetTrigger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">中文含义：参数。使用位置：作为 Animator.ResetTrigger(int) 的参数。</param>
    internal void ClearTrigger(int parameter)
    {
        if (anim != null)
        {
            anim.ResetTrigger(parameter);
        }
    }

    /// <summary>SetLayerWeightValue 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.BeginAction(int, EnemyQTEAction)、VillagerQTEParticipant.BlendQTEEntryLayer(int)、VillagerQTEParticipant.EndAction(int)、VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.LateUpdate() 等共 9 处；下游（含内部回调体） Animator.SetLayerWeight(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="layer">中文含义：图层。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数。</param>
    /// <param name="weight">中文含义：权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数。</param>
    internal void SetLayerWeightValue(int layer, float weight)
    {
        if (anim != null)
        {
            anim.SetLayerWeight(layer, weight);
        }
    }

    /// <summary>PlayState 的业务处理入口；本方法直接依赖 Animator.Play(int, int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.BeginAction(int, EnemyQTEAction)；下游（含内部回调体） Animator.Play(int, int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：状态。使用位置：作为 Animator.Play(int, int, float) 的参数。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 Animator.Play(int, int, float) 的参数。</param>
    /// <param name="progress">中文含义：进度。使用位置：作为 Animator.Play(int, int, float) 的参数。</param>
    internal void PlayState(int state, int layer, float progress)
    {
        if (anim != null)
        {
            anim.Play(state, layer, progress);
        }
    }

    /// <summary>SetPlaySpeed 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.HandleActionCompleted(Animator, EnemyQTEAction, int)、VillagerQTEParticipant.Release(int)、VillagerQTEParticipant.ReleaseBodyTechniqueReservation()、VillagerQTEParticipant.TryReserveBodyTechnique(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="speedValue">中文含义：速度值。使用位置：EnemyAnimationAdapter.SetPlaySpeed(float)。</param>
    internal void SetPlaySpeed(float speedValue)
    {
        if (anim != null)
        {
            anim.speed = speedValue;
        }
    }

    /// <summary>SetRootMotion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.Release(int)、VillagerQTEParticipant.TryAcquire(int, QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enable">中文含义：是否启用。使用位置：EnemyAnimationAdapter.SetRootMotion(bool)。</param>
    internal void SetRootMotion(bool enable)
    {
        if (anim != null)
        {
            anim.applyRootMotion = enable;
        }
    }

    /// <summary>SetEnable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SetLODActive(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enable">中文含义：是否启用。使用位置：EnemyAnimationAdapter.SetEnable(bool)。</param>
    internal void SetEnable(bool enable)
    {
        if (anim != null)
        {
            anim.enabled = enable;
        }
    }
}
