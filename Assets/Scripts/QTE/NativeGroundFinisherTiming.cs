using System;

/// <summary>来自 chc0_grapple / chc0_jack_pl 原始 DirectDamage、ReleaseJack 与 CameraSwitch 事件轨。</summary>
internal readonly struct NativeGroundFinisherTiming
{
    internal readonly int contactFrame;
    internal readonly int releaseFrame;
    internal readonly int cameraSwitchFrame;
    internal readonly int cameraBlendFrames;
    internal readonly bool smoothCameraBlend;

    private NativeGroundFinisherTiming(int contact, int release, int cameraSwitch, int blend, bool smooth)
    {
        contactFrame = contact;
        releaseFrame = release;
        cameraSwitchFrame = cameraSwitch;
        cameraBlendFrames = blend;
        smoothCameraBlend = smooth;
    }

    internal static bool TryGet(string motion, out NativeGroundFinisherTiming timing)
    {
        timing = default;
        if (string.IsNullOrEmpty(motion))
        {
            return false;
        }
        if (motion.EndsWith("Ground1000", StringComparison.Ordinal))
        {
            timing = new NativeGroundFinisherTiming(38, 145, 115, 20, false);
            return true;
        }
        if (motion.EndsWith("Ground1010", StringComparison.Ordinal))
        {
            timing = new NativeGroundFinisherTiming(35, 179, 115, 20, false);
            return true;
        }
        if (motion.EndsWith("Ground1035", StringComparison.Ordinal))
        {
            timing = new NativeGroundFinisherTiming(36, 160, 120, 44, true);
            return true;
        }
        return false;
    }
}
