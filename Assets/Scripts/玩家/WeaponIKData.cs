using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个 IK 姿态数据：记录左右手 target 的本地位置和旋转
/// </summary>
[Serializable]
public class IKHandPose
{
    /// <summary>姿态名称（Idle / Aim / Reload 等）。</summary>
    [Tooltip("姿态名称（Idle / Aim / Reload 等）")]
    public string poseName = "Idle";
    /// <summary>右手局部位置。</summary>
    public Vector3 rightHandLocalPos;
    /// <summary>右手局部旋转。</summary>
    public Vector3 rightHandLocalRot;
    /// <summary>左手局部位置。</summary>
    public Vector3 leftHandLocalPos;
    /// <summary>左手局部旋转。</summary>
    public Vector3 leftHandLocalRot;
}

/// <summary>
/// 单个 IK 模式的权重配置
/// </summary>
[Serializable]
public class IKModeWeights
{
    /// <summary>IK 模式。</summary>
    [Tooltip("IK 模式")]
    public IKMode mode;
    /// <summary>手臂权重。</summary>
    [Range(0f, 1f)]
    public float armWeight = 0f;
    /// <summary>脊柱权重。</summary>
    [Range(0f, 1f)]
    public float spineWeight = 0f;
    /// <summary>头部权重。</summary>
    [Range(0f, 1f)]
    public float headWeight = 0f;
}

/// <summary>
/// 武器 IK 数据（ScriptableObject），每个武器一个。
/// 包含手部姿态列表和各 IK 模式的权重表。
/// </summary>
[CreateAssetMenu(menuName = "IK/Weapon IK Data", fileName = "WeaponIKData")]
public class WeaponIKData : ScriptableObject
{
    /// <summary>中文含义：武器类型标识。</summary>
    [Header("武器信息")]
    [Tooltip("武器类型标识")]
    public WeaponType weaponType;
    /// <summary>该武器的所有 IK 姿态。</summary>
    [Header("手部姿态")]
    [Tooltip("该武器的所有 IK 姿态")]
    public List<IKHandPose> poses = new List<IKHandPose>();
    /// <summary>每个 IK 模式对应的手臂/脊椎/头部目标权重。</summary>
    [Header("IK 模式权重表")]
    [Tooltip("每个 IK 模式对应的手臂/脊椎/头部目标权重")]
    public List<IKModeWeights> modeWeights = new List<IKModeWeights>();
    /// <summary>中文含义：手臂过渡速度。</summary>
    [Header("过渡速度")]
    [Range(0f, 30f)]
    public float armTransitionSpeed = 12f;
    /// <summary>中文含义：脊柱过渡速度。</summary>
    [Range(0f, 30f)]
    public float spineTransitionSpeed = 6f;
    /// <summary>中文含义：头部过渡速度。</summary>
    [Range(0f, 30f)]
    public float headTransitionSpeed = 10f;
    /// <summary>OnValidate 的业务处理入口；本方法直接依赖 IKHandPose.IKHandPose()、IKModeWeights.IKModeWeights()、List&lt;IKHandPose&gt;.Add(IKHandPose)、List&lt;IKModeWeights&gt;.Add(IKModeWeights)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IKHandPose.IKHandPose()、IKModeWeights.IKModeWeights()、List&lt;IKHandPose&gt;.Add(IKHandPose)、List&lt;IKModeWeights&gt;.Add(IKModeWeights)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnValidate()
    {
        if (poses.Count == 0)
        {
            poses.Add(new IKHandPose { poseName = "Idle" });
            poses.Add(new IKHandPose { poseName = "Aim" });
            poses.Add(new IKHandPose { poseName = "Reload" });
        }

        if (modeWeights.Count == 0)
        {
            modeWeights.Add(new IKModeWeights { mode = IKMode.None, armWeight = 0f, spineWeight = 0f, headWeight = 0f });
            modeWeights.Add(new IKModeWeights { mode = IKMode.Aim, armWeight = 1f, spineWeight = 0.5f, headWeight = 0.6f });
            modeWeights.Add(new IKModeWeights { mode = IKMode.Transition, armWeight = 0.4f, spineWeight = 0.5f, headWeight = 0.6f });
            modeWeights.Add(new IKModeWeights { mode = IKMode.Hit, armWeight = 0f, spineWeight = 0f, headWeight = 0f });
            modeWeights.Add(new IKModeWeights { mode = IKMode.FreeLook, armWeight = 0f, spineWeight = 0f, headWeight = 0f });
        }
    }

    /// <summary>GetPose 的业务处理入口；本方法直接依赖 List&lt;IKHandPose&gt;.Find(Predicate&lt;IKHandPose&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponIKData.SavePose(string, Vector3, Vector3, Vector3, Vector3)；下游（含内部回调体） List&lt;IKHandPose&gt;.Find(Predicate&lt;IKHandPose&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="poseName">姿态名称。</param>
    public IKHandPose GetPose(string poseName)
    {
        return poses.Find( /* 中文含义：当前反向动力学手部姿态。 */p => p.poseName == poseName);
    }

    /// <summary>GetWeights 的业务处理入口；本方法直接依赖 List&lt;IKModeWeights&gt;.Find(Predicate&lt;IKModeWeights&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;IKModeWeights&gt;.Find(Predicate&lt;IKModeWeights&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">当前反向动力学模式。</param>
    public IKModeWeights GetWeights(IKMode mode)
    {
        return modeWeights.Find( /* 中文含义：当前反向动力学模式权重集合。 */w => w.mode == mode);
    }

    /// <summary>SavePose 的业务处理入口；本方法直接依赖 EditorUtility.SetDirty(Object)、IKHandPose.IKHandPose()、List&lt;IKHandPose&gt;.Add(IKHandPose)、WeaponIKData.GetPose(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorUtility.SetDirty(Object)、IKHandPose.IKHandPose()、List&lt;IKHandPose&gt;.Add(IKHandPose)、WeaponIKData.GetPose(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="poseName">姿态名称。</param>
    /// <param name="rPos">右侧位置。</param>
    /// <param name="rRot">右侧旋转。</param>
    /// <param name="lPos">左侧位置。</param>
    /// <param name="lRot">左侧旋转。</param>
    public void SavePose(string poseName, Vector3 rPos, Vector3 rRot, Vector3 lPos, Vector3 lRot)
    {
        // 姿态。
var pose = GetPose(poseName);
        if (pose == null)
        {
            pose = new IKHandPose
            {
                poseName = poseName
            };
            poses.Add(pose);
        }

        pose.rightHandLocalPos = rPos;
        pose.rightHandLocalRot = rRot;
        pose.leftHandLocalPos = lPos;
        pose.leftHandLocalRot = lRot;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
