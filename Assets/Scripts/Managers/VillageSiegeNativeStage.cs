using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class VillageSiegeNativeStage : MonoBehaviour
{
    [Tooltip("由源相机轨驱动的机位与镜头参数。")]
    public VillageSiegeCameraMotion cameraMotion;
    [Tooltip("源角色与道具动画的绑定对象。")]
    public Animator[] actors;
    [Tooltip("源音效与对白各自的播放源。")]
    public AudioSource[] audioSources;
    [Tooltip("里昂的源动画根骨，用于交还玩家站位。")]
    public Transform playerRoot;
    [Tooltip("电锯敌人的源动画根骨，用于实际敌人位置同步。")]
    public Transform threatRoot;
    [Tooltip("源动作以骨盆承载世界位置时，换算到脚底的高度。")]
    public float threatHeightOffset;
    [Tooltip("过场时间轴使用的电锯演员与武器模型组。")]
    public GameObject[] threatObjects;

    internal void Bind(PlayableDirector director, TimelineAsset timeline)
    {
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is AnimationTrack || track is ActivationTrack)
            {
                foreach (var actor in actors)
                {
                    if (actor != null && actor.name == track.name)
                    {
                        if (track is AnimationTrack)
                        {
                            director.SetGenericBinding(track, actor);
                        }
                        else
                        {
                            director.SetGenericBinding(track, actor.gameObject);
                        }
                        break;
                    }
                }
            }
            else if (track is AudioTrack)
            {
                foreach (var source in audioSources)
                {
                    if (source.name == track.name)
                    {
                        director.SetGenericBinding(track, source);
                        break;
                    }
                }
            }
        }
    }

    internal void UpdateVolume()
    {
        float volume = 1f;
        if (AudioManager.HasInstance)
        {
            volume = AudioManager.Instance.MasterVolume * AudioManager.Instance.GetCategoryVolume(AudioCategory.SFX);
        }
        foreach (var source in audioSources)
        {
            source.volume = volume;
        }
    }

    internal void PrepareThreatActors()
    {
        foreach (var target in threatObjects)
        {
            if (target != null)
            {
                target.SetActive(true);
            }
        }
    }
}
