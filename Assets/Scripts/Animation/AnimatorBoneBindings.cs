using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class AnimatorBoneBindings : MonoBehaviour
{
    [SerializeField] private Transform[] bones = Array.Empty<Transform>();

    public static Transform Resolve(Animator animator, HumanBodyBones bone)
    {
        int index = (int)bone;
        if (animator == null || index < 0 || index >= (int)HumanBodyBones.LastBone)
        {
            return null;
        }
        if (animator.TryGetComponent(out AnimatorBoneBindings bindings) && index < bindings.bones.Length)
        {
            Transform joint = bindings.bones[index];
            if (joint != null)
            {
                return joint;
            }
        }
        if (animator.isHuman)
        {
            return animator.GetBoneTransform(bone);
        }
        return null;
    }

#if UNITY_EDITOR
    public int CaptureHumanoid()
    {
        var animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            throw new InvalidOperationException("请在更换 Avatar 前采集现有骨骼引用。");
        }
        var captured = new Transform[(int)HumanBodyBones.LastBone];
        int count = 0;
        for (int index = 0; index < captured.Length; index++)
        {
            captured[index] = animator.GetBoneTransform((HumanBodyBones)index);
            if (captured[index] != null)
            {
                count++;
            }
        }
        bones = captured;
        UnityEditor.EditorUtility.SetDirty(this);
        return count;
    }
#endif
}
