using UnityEngine;

// 物理步动画在两次渲染之间可能没有重新采样。恢复上一次仍留在骨骼上的偏移，避免重复累加。
internal sealed class EnvironmentPoseOffset
{
    private readonly Transform bone;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 appliedPosition;
    private Quaternion appliedRotation;
    private bool hasOffset;

    internal EnvironmentPoseOffset(Transform bone)
    {
        this.bone = bone;
    }

    internal void Capture()
    {
        if (bone == null)
        {
            return;
        }

        Restore();
        originalPosition = bone.localPosition;
        originalRotation = bone.localRotation;
    }

    internal void Commit()
    {
        if (bone == null)
        {
            return;
        }

        appliedPosition = bone.localPosition;
        appliedRotation = bone.localRotation;
        hasOffset = true;
    }

    internal void Restore()
    {
        if (!hasOffset || bone == null)
        {
            return;
        }

        if ((appliedPosition - bone.localPosition).sqrMagnitude < 0.00000001f)
        {
            bone.localPosition = originalPosition;
        }
        if (Quaternion.Angle(appliedRotation, bone.localRotation) < .01f)
        {
            bone.localRotation = originalRotation;
        }
        hasOffset = false;
    }
}
