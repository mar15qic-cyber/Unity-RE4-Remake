using UnityEngine;

public sealed class NativePlayerActionAudioProfile : ScriptableObject
{
    [Tooltip("配对动作片段及里昂一侧的声音标记")]
    public NativeActionAudio.Track[] pairTracks;
    [Tooltip("正式控制器直接播放的原版动作声")]
    public NativeActionAudio.Track[] motionTracks;
    [Tooltip("由配对时钟驱动，定位到对方武器的原版机构声")]
    public NativeActionAudio.Track[] weaponPairTracks;
    public AudioEventData dirtLanding;
    public AudioEventData grassLanding;
    public AudioEventData stoneLanding;
    public AudioEventData woodLanding;
}
