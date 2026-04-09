using System;
using BoomHud.Unity.UIToolkit;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BoomHud.Unity.Timeline
{
    [TrackClipType(typeof(BoomHudMotionPlayableAsset))]
    [TrackBindingType(typeof(BoomHudUiToolkitMotionHost))]
    [TrackColor(0.2f, 0.68f, 0.96f)]
    public sealed class BoomHudMotionTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<BoomHudMotionTrackMixerBehaviour>.Create(graph, inputCount);
        }
    }

    internal sealed class BoomHudMotionTrackMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var host = playerData as BoomHudUiToolkitMotionHost;
            if (host == null)
            {
                return;
            }

            var inputCount = playable.GetInputCount();
            var bestWeight = 0f;
            BoomHudMotionClipBehaviour? activeClip = null;
            var activeTime = 0d;

            for (var index = 0; index < inputCount; index++)
            {
                var weight = playable.GetInputWeight(index);
                if (weight <= 0f)
                {
                    continue;
                }

                var inputPlayable = (ScriptPlayable<BoomHudMotionClipBehaviour>)playable.GetInput(index);
                var clipBehaviour = inputPlayable.GetBehaviour();
                if (clipBehaviour == null || string.IsNullOrWhiteSpace(clipBehaviour.ClipId))
                {
                    continue;
                }

                if (weight <= bestWeight)
                {
                    continue;
                }

                bestWeight = weight;
                activeClip = clipBehaviour;
                activeTime = inputPlayable.GetTime();
            }

            if (activeClip == null)
            {
                return;
            }

            var timeSeconds = (float)Math.Max(0d, activeTime + activeClip.TimeOffsetSeconds);
            host.Evaluate(activeClip.ClipId, timeSeconds);
        }
    }
}
