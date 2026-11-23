using UnityEngine;

public sealed class VillageSiegeCameraMotion : MonoBehaviour
{
    [Tooltip("源参数轨的横向视场角，单位为弧度。")]
    public float horizontalFieldOfView = .98f;
    [Tooltip("事件指定的近裁剪距离。")]
    public float nearPlane = .01f;
}
