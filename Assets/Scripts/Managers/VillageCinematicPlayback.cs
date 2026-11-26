using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// 村庄三段过场都使用从零开始的独立整段音轨。先准备绑定，再按同一音频采样时钟播放。
internal sealed class VillageCinematicPlayback
{
    private readonly PlayableDirector director;
    private readonly List<AudioSource> sources = new();
    private readonly List<AudioClip> clips = new();
    private bool continuousTracks = true;
    private int clockIndex;
    private double startDsp;
    private readonly double duration;
    private readonly List<Renderer> concealed = new();
    private bool prepared;
    private double previousTime;

    internal VillageCinematicPlayback(PlayableDirector director, TimelineAsset timeline, GameObject stage)
    {
        this.director = director;
        director.playableAsset = timeline;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is not AudioTrack || track.mutedInHierarchy)
            {
                continue;
            }
            var source = director.GetGenericBinding(track) as AudioSource;
            foreach (var entry in track.GetClips())
            {
                if (entry.asset is not AudioPlayableAsset asset || asset.clip == null)
                {
                    continuousTracks = false;
                    continue;
                }
                asset.clip.LoadAudioData();
                if (source == null || entry.start != 0d ||
                    entry.clipIn != 0d || entry.timeScale != 1d || sources.Contains(source))
                {
                    continuousTracks = false;
                    continue;
                }
                if (clips.Count == 0 || asset.clip.length > clips[clockIndex].length)
                {
                    clockIndex = clips.Count;
                }
                sources.Add(source);
                clips.Add(asset.clip);
            }
        }
        duration = timeline.duration;
        continuousTracks &= clips.Count > 0;
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.forceRenderingOff)
            {
                renderer.forceRenderingOff = true;
                concealed.Add(renderer);
            }
        }
        stage.SetActive(true);
        if (continuousTracks)
        {
            Prepare();
        }
        else
        {
            director.RebuildGraph();
        }
    }

    internal void Begin()
    {
        foreach (var renderer in concealed)
        {
            renderer.forceRenderingOff = false;
        }
        previousTime = 0d;
        director.time = 0d;
        if (!continuousTracks)
        {
            director.timeUpdateMode = DirectorUpdateMode.DSPClock;
            director.Play();
            return;
        }
        if (!prepared)
        {
            Prepare();
        }
        director.Resume();
        AudioSettings.GetDSPBufferSize(out int samples, out int buffers);
        double lead = Math.Max(.05d, (double)samples * buffers / AudioSettings.outputSampleRate);
        startDsp = AudioSettings.dspTime + lead;
        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            source.clip = clips[i];
            source.PlayScheduled(startDsp);
        }
    }

    private void Prepare()
    {
        director.Play();
        var graph = director.playableGraph;
        for (int i = graph.GetOutputCount() - 1; i >= 0; i--)
        {
            var output = graph.GetOutput(i);
            if (output.IsOutputValid() && output.GetPlayableOutputType() == typeof(AudioPlayableOutput))
            {
                graph.DestroyOutput(output);
            }
        }
        director.Evaluate();
        director.Pause();
        prepared = true;
    }

    internal void Sample()
    {
        if (!continuousTracks)
        {
            return;
        }
        double audioTime = Math.Max(0d, AudioSettings.dspTime - startDsp);
        var clock = sources[clockIndex];
        if (audioTime < clips[clockIndex].length && clock.isPlaying)
        {
            audioTime = (double)clock.timeSamples / clips[clockIndex].frequency;
        }

        double time;
        if (previousTime <= 0d && audioTime <= 0d)
        {
            time = 0d;
        }
        else
        {
            double continuousTime = previousTime + Math.Min(0.05d, (double)Time.unscaledDeltaTime);
            double drift = audioTime - continuousTime;
            if (Math.Abs(drift) > 0.035d)
            {
                continuousTime += drift * 0.35d;
            }
            time = Math.Min(duration, Math.Max(previousTime, continuousTime));
        }

        if (time > previousTime)
        {
            director.time = time;
            director.Evaluate();
            previousTime = time;
        }
    }

    internal void Stop()
    {
        if (continuousTracks)
        {
            foreach (var source in sources)
            {
                source.Stop();
            }
        }
        director.Stop();
        prepared = false;
    }
}
