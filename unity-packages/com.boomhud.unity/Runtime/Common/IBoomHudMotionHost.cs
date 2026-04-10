using System;

namespace BoomHud.Unity.Runtime
{
    public interface IBoomHudMotionHost
    {
        event Action<string, float> MotionApplied;

        string? CurrentClip { get; }

        float CurrentTimeSeconds { get; }

        bool IsPlaying { get; }

        bool Loop { get; set; }

        float PlaybackSpeed { get; set; }

        void Play();

        void Play(string clipId, bool restart = true);

        void Pause();

        void Stop();

        void Evaluate(string clipId, float timeSeconds);

        void ScrubToTime(float timeSeconds);

        void ScrubToNormalizedTime(float normalizedTime);
    }
}
