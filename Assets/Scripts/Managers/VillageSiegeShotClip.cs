using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public sealed class VillageSiegeShotClip : PlayableAsset
{
    [Tooltip("镜头构图使用的正式场景站位。")]
    public VillageSiegeShot kind;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ShotBehaviour>.Create(graph);
        playable.GetBehaviour().kind = kind;
        return playable;
    }

    public sealed class ShotBehaviour : PlayableBehaviour
    {
        public VillageSiegeShot kind;
    }
}
