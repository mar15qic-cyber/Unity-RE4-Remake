using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个IK姿势数据：记录左右手 target 的 localPosition/localRotation
/// </summary>
[Serializable]
public class IKPoseData
{
    /// <summary>中文含义：姿态名称。使用位置：作为 List&lt;IKPoseData&gt;.Add(IKPoseData) 的参数、作为 List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public string poseName = "New Pose";
    /// <summary>中文含义：右手局部位置。使用位置：IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public Vector3 rightHandLocalPos;
    /// <summary>中文含义：右手局部旋转。使用位置：IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public Vector3 rightHandLocalRot;
    /// <summary>中文含义：左手局部位置。使用位置：IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public Vector3 leftHandLocalPos;
    /// <summary>中文含义：左手局部旋转。使用位置：IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public Vector3 leftHandLocalRot;
}

/// <summary>
/// IK 姿势预设：ScriptableObject，存储左右手 target 的 localPosition/localRotation。
/// 运行时修改会持久化到磁盘，退出 Play 模式后数据保留。
/// </summary>
[CreateAssetMenu(menuName = "IK/IK Pose Preset", fileName = "IKPosePreset")]
public class IKPosePreset : ScriptableObject
{
    /// <summary>中文含义：姿态集合。使用位置：IKPosePreset.GetPose(string)、IKPosePreset.OnValidate()、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</summary>
    public List<IKPoseData> poses = new List<IKPoseData>();
    /// <summary>OnValidate 的业务处理入口；本方法直接依赖 IKPoseData.IKPoseData()、List&lt;IKPoseData&gt;.Add(IKPoseData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IKPoseData.IKPoseData()、List&lt;IKPoseData&gt;.Add(IKPoseData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnValidate()
    {
        if (poses.Count == 0)
        {
            poses.Add(new IKPoseData { poseName = "Idle" });
            poses.Add(new IKPoseData { poseName = "Aim" });
        }
    }

    /// <summary>GetPose 的业务处理入口；本方法直接依赖 List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="poseName">中文含义：姿态名称。使用位置：作为 List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;) 的参数。</param>
    public IKPoseData GetPose(string poseName)
    {
        return poses.Find( /* 中文含义：当前反向动力学姿态数据。 */p => p.poseName == poseName);
    }

    /// <summary>SavePose 的业务处理入口；本方法直接依赖 Debug.Log(object)、EditorUtility.SetDirty(Object)、IKPoseData.IKPoseData()、List&lt;IKPoseData&gt;.Add(IKPoseData)、List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、EditorUtility.SetDirty(Object)、IKPoseData.IKPoseData()、List&lt;IKPoseData&gt;.Add(IKPoseData)、List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="poseName">中文含义：姿态名称。使用位置：作为 Debug.Log(object) 的参数、作为 List&lt;IKPoseData&gt;.Find(Predicate&lt;IKPoseData&gt;) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</param>
    /// <param name="rPos">中文含义：右侧位置。使用位置：作为 Debug.Log(object) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</param>
    /// <param name="rRot">中文含义：右侧旋转。使用位置：作为 Debug.Log(object) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</param>
    /// <param name="lPos">中文含义：左侧位置。使用位置：作为 Debug.Log(object) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</param>
    /// <param name="lRot">中文含义：左侧旋转。使用位置：作为 Debug.Log(object) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。</param>
    public void SavePose(string poseName, Vector3 rPos, Vector3 rRot, Vector3 lPos, Vector3 lRot)
    {
        // pose（姿态）：保存 List<IKPoseData>.Find(Predicate<IKPoseData>) 的返回结果，供当前步骤使用。使用位置：作为 List<IKPoseData>.Add(IKPoseData) 的参数、IKPosePreset.SavePose(string, Vector3, Vector3, Vector3, Vector3)。
var pose = poses.Find( /* 中文含义：当前反向动力学姿态数据。 */p => p.poseName == poseName);
        if (pose == null)
        {
            pose = new IKPoseData
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
        Debug.Log($"IKPosePreset: 姿势 '{poseName}' 已保存 " + $"(右手 pos={rPos}, rot={rRot} | 左手 pos={lPos}, rot={lRot})");
    }
}
