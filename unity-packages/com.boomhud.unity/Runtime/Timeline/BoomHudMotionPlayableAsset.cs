using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BoomHud.Unity.Timeline
{
    [Serializable]
    public sealed class BoomHudMotionPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private string _clipId = "intro";
        [SerializeField] private double _timeOffsetSeconds;

        public string ClipId
        {
            get => _clipId;
            set => _clipId = value;
        }

        public double TimeOffsetSeconds
        {
            get => _timeOffsetSeconds;
            set => _timeOffsetSeconds = value;
        }

        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<BoomHudMotionClipBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.ClipId = _clipId;
            behaviour.TimeOffsetSeconds = _timeOffsetSeconds;
            return playable;
        }
    }
}
