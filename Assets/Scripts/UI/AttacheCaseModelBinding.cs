using UnityEngine;

public sealed class AttacheCaseModelBinding : MonoBehaviour
{
    [Header("原版箱盖骨骼")]
    public Transform lid;
    public Quaternion closedLidRotation = Quaternion.identity;
    public Vector3 localOpenAxis = Vector3.right;
    public float openAngle = 145f;

    public void ApplyOpenPose(float amount)
    {
        if (lid != null)
        {
            lid.localRotation = closedLidRotation * Quaternion.AngleAxis(openAngle * amount, localOpenAxis);
        }
    }
}
