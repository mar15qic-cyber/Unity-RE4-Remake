using UnityEngine;
using UnityEngine.Timeline;

[System.Serializable]
public sealed class VillageSiegeCue : Marker
{
    public enum Kind { Latch, Raise, Mount, Engine, EngineReady }

    [Tooltip("播放到标记时交给实际玩法组件提交的动作。")]
    public Kind kind;
}
