using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum VillageSiegeShot
{
    Door,
    Yard,
    Ladder,
    Stair,
    FarmGate,
    FarmReveal,
    FarmRoute,
    FarmReturn,
    DoorLatch,
    HouseReaction,
    LadderSeat,
    LadderMount,
    FarmTool
}

[TrackClipType(typeof(VillageSiegeShotClip))]
[TrackBindingType(typeof(VillageSiegeSequence))]
public sealed class VillageSiegeShotTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<ShotMixer>.Create(graph, inputCount);
    }

    private sealed class ShotMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (playerData is not VillageSiegeSequence sequence)
            {
                return;
            }

            for (int index = 0; index < playable.GetInputCount(); index++)
            {
                if (playable.GetInputWeight(index) <= 0f)
                {
                    continue;
                }
                var clip = (ScriptPlayable<VillageSiegeShotClip.ShotBehaviour>)playable.GetInput(index);
                sequence.QueueShot(clip.GetBehaviour().kind);
                return;
            }
        }
    }
}
