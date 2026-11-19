using UnityEngine;

/// <summary>单个镜头阶段的构图值，由 CameraProfile 提供，第三人称驱动读取偏移、视野角和阻尼，不在此值类型中推进镜头。</summary>
[System.Serializable]
public struct CameraPose
{
    /// <summary>中文含义：偏移量。该阶段跟随目标附近的镜头构图偏移。</summary>
    [Header("构图")]
    public Vector3 offset;
    /// <summary>中文含义：视野角度。该阶段目标视野角。</summary>
    public float fieldOfView;
    /// <summary>中文含义：阻尼。该阶段各方向的跟随阻尼配置。</summary>
    [Header("阻尼")]
    public Vector3 damping;
}

/// <summary>武器镜头的共享配置资产。RE4CameraController 优先从武器数据取得，再回退到场景配置，RE4RThirdPersonCamera 应用姿态、过渡和反馈参数；本资产不保存本帧镜头状态。</summary>
[CreateAssetMenu(fileName = "CameraProfile", menuName = "RE4/摄像机配置")]
public sealed class CameraProfile : ScriptableObject
{
    /// <summary>中文含义：武器类型。该配置对应的武器种类，供绑定及配置使用关系核对。</summary>
    [Header("武器")]
    public WeaponType weaponType;
    /// <summary>中文含义：是否允许瞄准。该武器配置是否允许瞄准表现，实际输入与动作许可仍由已有宿主审批。</summary>
    public bool allowAim = true;
    [Tooltip("源姿态与原版实测校准结果。")]
    public NativeShoulderCameraSettings nativeShoulder;
    /// <summary>中文含义：探索姿态。探索阶段构图，驱动用于正常跟随。</summary>
    [Header("探索")]
    public CameraPose explorePose;
    /// <summary>中文含义：瞄准姿态。瞄准阶段构图，驱动用于越肩瞄准过渡。</summary>
    [Header("瞄准")]
    public CameraPose aimPose;
    /// <summary>中文含义：冲刺姿态。冲刺阶段构图，驱动用于冲刺表现。</summary>
    [Header("奔跑")]
    public CameraPose sprintPose;
    /// <summary>中文含义：混合速度。阶段构图过渡速度，保留当前资产调参。</summary>
    [Header("过渡")]
    [Min(0.01f)]
    public float blendSpeed = 8f;
    /// <summary>中文含义：后坐力缩放。基础武器后坐幅度倍率，摄像机入口在提交开火反馈前读取。</summary>
    [Header("反馈")]
    [Min(0f)]
    public float recoilScale = 1f;
    /// <summary>中文含义：开火震动倍率。本武器的连续开火震动倍率，传给手感驱动。</summary>
    [Header("连续开火反馈，角度或回正为零时沿用相机默认值")]
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("开火震动倍率")]
    [UnityEngine.InspectorName("开火震动倍率")]
    public float fireShakeMultiplier = 1f;
    /// <summary>中文含义：最大累计抬枪角度。累计抬枪角上限，为零时沿用驱动默认值。</summary>
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("最大累计抬枪角度")]
    [UnityEngine.InspectorName("最大累计抬枪角度")]
    public float maximumAccumulatedRaiseGunAngle;
    /// <summary>中文含义：后坐回正时间。后坐回正时间，为零时沿用驱动默认值。</summary>
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("后坐回正时间")]
    [UnityEngine.InspectorName("后坐回正时间")]
    public float recoilRecenteringTime;
}
