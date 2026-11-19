using UnityEngine;

public sealed class NativePlayerActionAudioProfile : ScriptableObject
{
    [Tooltip("配对动作片段及里昂一侧的声音标记")]
    public NativeActionAudio.Track[] pairTracks;
    public AudioEventData dirtLanding;
    public AudioEventData grassLanding;
    public AudioEventData stoneLanding;
    public AudioEventData woodLanding;
}
