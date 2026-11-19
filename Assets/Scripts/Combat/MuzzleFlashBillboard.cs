using UnityEngine;

[DefaultExecutionOrder(1100)]
public sealed class MuzzleFlashBillboard : MonoBehaviour
{
    [SerializeField] private MeshRenderer flash;
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private int smokeCount = 3;
    [SerializeField] private float duration = 0.045f;
    [SerializeField] private float forwardOffset = 0.018f;

    private Transform muzzle;
    private int firstFrame;
    private float hideAt;

    private void OnEnable()
    {
        firstFrame = Time.frameCount;
        hideAt = Time.time + duration;
        flash.enabled = true;
        Camera.onPreCull += AlignForCamera;
    }

    internal void Follow(Transform source)
    {
        muzzle = source;
        Align();
        if (smoke != null)
        {
            smoke.Emit(smokeCount);
        }
    }

    private void LateUpdate()
    {
        if (!flash.enabled)
        {
            return;
        }
        // 以实际渲染帧保留火光，避免单次长帧跨过整个可见时段。
        if (Time.frameCount > firstFrame && Time.time >= hideAt)
        {
            flash.enabled = false;
            StopFollowing();
            return;
        }
        Align();
    }

    private void AlignForCamera(Camera camera)
    {
        if (flash.enabled)
        {
            Align();
        }
    }

    private void Align()
    {
        if (muzzle != null)
        {
            flash.transform.position = muzzle.position + muzzle.forward * forwardOffset;
        }
    }

    private void OnDisable()
    {
        StopFollowing();
    }

    private void StopFollowing()
    {
        Camera.onPreCull -= AlignForCamera;
        muzzle = null;
    }
}
