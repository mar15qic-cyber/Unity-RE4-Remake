using UnityEngine;

/// <summary>
/// 脚步音效控制器：挂在 Leon 角色上，可由动画事件或移动状态自动触发 PlayFootstep()
/// 使用 FootstepSurfaceProfile 配置地表音效，通过射线检测判断地表类型
/// 不再硬编码 AudioClip 数组
/// </summary>
[DefaultExecutionOrder(10020)]
public class FootstepController : MonoBehaviour
{
    /// <summary>中文含义：脚步地表音效配置资产。使用位置：FootstepController.PlayFootstep()。</summary>
    [Header("地表配置")]
    [Tooltip("脚步地表音效配置资产")]
    public FootstepSurfaceProfile surfaceProfile;
    /// <summary>中文含义：向下射线检测距离。使用位置：作为 Physics.Raycast(Vector3, Vector3, out RaycastHit, float, int, QueryTriggerInteraction) 的参数。</summary>
    [Header("检测设置")]
    [Tooltip("向下射线检测距离")]
    public float rayDistance = 1.5f;
    /// <summary>中文含义：地表检测层级。使用位置：作为 Physics.Raycast(Vector3, Vector3, out RaycastHit, float, int, QueryTriggerInteraction) 的参数。</summary>
    [Tooltip("地表检测层级")]
    public LayerMask groundLayer = ~0;
    /// <summary>中文含义：脚步音量额外倍率。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("音量倍率")]
    [Tooltip("脚步音量额外倍率")]
    public float volumeMultiplier = 1f;
    /// <summary>中文含义：是否没有动画事件时，根据角色实际位移自动触发脚步声。没有动画事件时，根据角色实际位移自动触发脚步声。使用位置：FootstepController.Update()。</summary>
    [Header("自动触发")]
    [Tooltip("没有动画事件时，根据角色实际位移自动触发脚步声")]
    public bool automaticTrigger = true;
    /// <summary>中文含义：普通移动时两次脚步声的最短间隔。使用位置：FootstepController.Update()。</summary>
    [Min(0.05f)]
    [Tooltip("普通移动时两次脚步声的最短间隔")]
    public float walkStepInterval = 0.42f;
    /// <summary>中文含义：跑步或冲刺时两次脚步声的最短间隔。使用位置：FootstepController.Update()。</summary>
    [Min(0.05f)]
    [Tooltip("跑步或冲刺时两次脚步声的最短间隔")]
    public float runStepInterval = 0.28f;
    /// <summary>中文含义：动画事件与自动触发之间的去重间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    [Tooltip("动画事件与自动触发之间的去重间隔")]
    public float duplicateGuardInterval = 0.08f;
    /// <summary>中文含义：里昂玩家控制器。使用位置：FootstepController.PlayFootstep()、FootstepController.Start()、FootstepController.Update()。</summary>
    [Header("移动与植被")]
    [Tooltip("实际移动速度超过此值时使用跑步脚步")]
    public float runSpeedThreshold = 2.4f;
    [Tooltip("蹲行脚步间隔")]
    public float crouchStepInterval = 0.52f;
    [Tooltip("穿过已配置低植被时播放的摩擦声")]
    public AudioEventData grassRustleEvent;
    [Tooltip("所有草丛触发区共享的播放间隔")]
    public float grassRustleInterval = 0.85f;

    private LeonController controller;
    private Vector3 lastPosition;
    private float movementSpeed;
    private float lastMovementTime;
    private float stepTimer;
    private NativeActionAudio nativeAudio;
    private float lastStepTime;
    private float nextGrassTime;
    private LeonAnimatorDriver animatorDriver;
    private Transform leftFoot;
    private Transform rightFoot;
    private int knifeMovementMode;
    private AnimationClip knifeWalkClip;
    private float knifeWalkTime;

    // 正式采样骨架的落脚标定，帧号按对应片段的 60 Hz 源采样记录。
    private static readonly int[] ForwardLeft = { 4, 80, 158 };
    private static readonly int[] ForwardRight = { 42, 117, 196 };
    private static readonly int[] BackwardLeft = { 22, 100, 178 };
    private static readonly int[] BackwardRight = { 61, 139, 218 };
    private static readonly int[] StrafeLeftLeft = { 7, 85, 159 };
    private static readonly int[] StrafeLeftRight = { 44, 119, 197 };
    private static readonly int[] StrafeRightLeft = { 23, 102, 182 };
    private static readonly int[] StrafeRightRight = { 59, 139, 221 };
    private bool knifeLeftLifted;
    private bool knifeRightLifted;

    private void Start()
    {
        controller = GetComponent<LeonController>();
        animatorDriver = GetComponent<LeonAnimatorDriver>();
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            leftFoot = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftFoot);
            rightFoot = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightFoot);
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        movementSpeed = 0f;
        lastMovementTime = float.NegativeInfinity;
        stepTimer = 0f;
        lastStepTime = float.NegativeInfinity;
        nextGrassTime = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        Vector3 delta = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
        lastPosition = transform.position;
        if (delta.sqrMagnitude > 0.000001f && delta.sqrMagnitude < 0.81f && Time.deltaTime > 0f)
        {
            float sampleTime = Time.deltaTime;
            if (!float.IsNegativeInfinity(lastMovementTime))
            {
                sampleTime = Mathf.Max(sampleTime, Time.time - lastMovementTime);
            }

            movementSpeed = delta.magnitude / sampleTime;
            lastMovementTime = Time.time;
        }
        else if (Time.time - lastMovementTime > 0.06f || delta.sqrMagnitude >= 0.81f)
        {
            movementSpeed = 0f;
        }

        if (!CanPlayStep())
        {
            stepTimer = 0f;
            return;
        }

        if (animatorDriver != null && animatorDriver.OwnsKnifeLocomotionFootsteps)
        {
            stepTimer = 0f;
            return;
        }
        knifeMovementMode = 0;

        if (nativeAudio == null)
        {
            TryGetComponent(out nativeAudio);
        }
        if (nativeAudio != null && nativeAudio.OwnsFootsteps)
        {
            stepTimer = 0f;
            return;
        }

        if (automaticTrigger)
        {
            stepTimer += Time.deltaTime;
            float interval = walkStepInterval;
            if (controller.IsCrouching)
            {
                interval = crouchStepInterval;
            }
            else if (controller.IsEffectiveSprinting)
            {
                interval = runStepInterval;
            }
            else if (movementSpeed >= runSpeedThreshold)
            {
                interval = Mathf.Lerp(walkStepInterval, runStepInterval, 0.6f);
            }

            if (stepTimer >= Mathf.Max(0.05f, interval))
            {
                PlayFootstep();
            }
        }
    }

    public void PlayFootstep()
    {
        AudioManager audio = AudioManager.Instance;
        if (!CanPlayStep() || audio == null || surfaceProfile == null
            || Time.time - lastStepTime < Mathf.Max(0f, duplicateGuardInterval))
        {
            return;
        }

        stepTimer = 0f;
        lastStepTime = Time.time;
        if (!FootstepSurface.TryFindGround(transform, rayDistance, groundLayer, out RaycastHit ground))
        {
            return;
        }

        SurfaceType surface = FootstepSurface.Resolve(ground);
        AudioEventData sound;
        if (controller.IsCrouching)
        {
            sound = surfaceProfile.GetCrouchEvent(surface);
        }
        else if (controller.IsEffectiveSprinting || movementSpeed >= runSpeedThreshold)
        {
            sound = surfaceProfile.GetRunEvent(surface);
        }
        else
        {
            sound = surfaceProfile.GetWalkEvent(surface);
        }

        audio.Play3D(sound, ground.point, Mathf.Max(0f, volumeMultiplier));
        PlayNativeGrass(audio, ground.point);
    }

    internal void SampleKnifeFootsteps(int movementMode, AnimationClip clip, float time, float leftHeight, bool advanced)
    {
        if (movementMode == 0 || clip == null || controller == null || !controller.IsGrounded)
        {
            knifeMovementMode = 0;
            return;
        }
        if (!advanced)
        {
            return;
        }

        if (knifeMovementMode != movementMode || knifeWalkClip != clip || time < knifeWalkTime)
        {
            knifeMovementMode = movementMode;
            knifeWalkClip = clip;
            knifeWalkTime = time;
            knifeLeftLifted = false;
            knifeRightLifted = false;
        }

        if (TryReadKnifeStepFrames(clip.name, out int[] left, out int[] right))
        {
            if (CrossedFootfall(left, clip.length, knifeWalkTime, time))
            {
                PlayKnifeFootstep(leftFoot);
            }
            if (CrossedFootfall(right, clip.length, knifeWalkTime, time))
            {
                PlayKnifeFootstep(rightFoot);
            }
        }
        else
        {
            SampleKnifeFoot(leftFoot, leftHeight, ref knifeLeftLifted);
            SampleKnifeFoot(rightFoot, -leftHeight, ref knifeRightLifted);
        }

        knifeWalkTime = time;
    }

    private static bool TryReadKnifeStepFrames(string clipName, out int[] left, out int[] right)
    {
        switch (clipName)
        {
            case "Knife_0403":
                left = ForwardLeft;
                right = ForwardRight;
                return true;
            case "Knife_0443":
                left = BackwardLeft;
                right = BackwardRight;
                return true;
            case "Knife_0413":
                left = StrafeLeftLeft;
                right = StrafeLeftRight;
                return true;
            case "Knife_0453":
                left = StrafeRightLeft;
                right = StrafeRightRight;
                return true;
            default:
                left = null;
                right = null;
                return false;
        }
    }

    private static bool CrossedFootfall(int[] frames, float duration, float before, float after)
    {
        if (duration <= 0f || before >= after)
        {
            return false;
        }

        foreach (int frame in frames)
        {
            float contactTime = frame / 60f;
            int previousCycle = Mathf.FloorToInt((before - contactTime) / duration);
            int currentCycle = Mathf.FloorToInt((after - contactTime) / duration);
            if (currentCycle > previousCycle)
            {
                return true;
            }
        }
        return false;
    }

    private void SampleKnifeFoot(Transform foot, float heightAboveOtherFoot, ref bool lifted)
    {
        if (heightAboveOtherFoot >= .045f)
        {
            lifted = true;
            return;
        }
        if (!lifted || heightAboveOtherFoot > .018f)
        {
            return;
        }

        lifted = false;
        PlayKnifeFootstep(foot);
    }

    private void PlayKnifeFootstep(Transform foot)
    {
        AudioManager audio = AudioManager.Instance;
        if (foot == null || audio == null || surfaceProfile == null
            || !FootstepSurface.TryFindGround(transform, foot.position, .48f, groundLayer, out var contact))
        {
            return;
        }

        var sound = surfaceProfile.GetWalkEvent(FootstepSurface.Resolve(contact));
        if (sound != null)
        {
            audio.Play3D(sound, contact.point, Mathf.Max(0f, volumeMultiplier));
            RecordNativeFootstep();
            PlayNativeGrass(audio, contact.point);
        }
    }

    internal void RecordNativeFootstep()
    {
        lastStepTime = Time.time;
        stepTimer = 0f;
    }

    internal void PlayNativeGrass(AudioManager audio, Vector3 contact)
    {
        if (grassRustleEvent != null && Time.time >= nextGrassTime && GrassRustleZone.Contains(contact + Vector3.up * 0.25f))
        {
            audio.Play3D(grassRustleEvent, contact);
            nextGrassTime = Time.time + Mathf.Max(0.1f, grassRustleInterval);
        }
    }

    private bool CanPlayStep()
    {
        return controller != null && controller.IsGrounded && controller.MoveInput.sqrMagnitude > 0.01f && movementSpeed > 0.06f
            && controller.currentState != LeonState.Dead && controller.currentState != LeonState.Staggered
            && controller.currentState != LeonState.QTE && controller.currentState != LeonState.Traversing;
    }
}
