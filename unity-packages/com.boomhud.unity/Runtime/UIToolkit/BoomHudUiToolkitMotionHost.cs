using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Unity.UIToolkit
{
    [DisallowMultipleComponent]
    public abstract class BoomHudUiToolkitMotionHost : BoomHudUiToolkitHost
    {
        [SerializeField] private string _initialClip = "intro";
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField] private bool _loop = true;
        [SerializeField] private float _playbackSpeed = 1f;
        [SerializeField] private bool _useUnscaledTime = true;

        private string? _currentClip;
        private float _currentTimeSeconds;
        private bool _isPlaying;

        public string? CurrentClip => _currentClip;

        public float CurrentTimeSeconds => _currentTimeSeconds;

        public bool IsPlaying => _isPlaying;

        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Mathf.Max(0f, value);
        }

        protected sealed override void BindView(VisualElement root)
        {
            BindMotionView(root);

            if (string.IsNullOrWhiteSpace(_currentClip))
            {
                _currentClip = string.IsNullOrWhiteSpace(_initialClip) ? null : _initialClip;
                _currentTimeSeconds = 0f;
            }

            ApplyCurrentPose();

            if (_playOnEnable && !string.IsNullOrWhiteSpace(_currentClip))
            {
                _isPlaying = true;
            }
        }

        protected virtual void Update()
        {
            if (!_isPlaying || string.IsNullOrWhiteSpace(_currentClip))
            {
                return;
            }

            var clipDuration = GetClipDurationSeconds(_currentClip);
            if (clipDuration <= 0f)
            {
                _isPlaying = false;
                return;
            }

            var deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            _currentTimeSeconds += deltaTime * _playbackSpeed;
            if (_currentTimeSeconds >= clipDuration)
            {
                if (_loop)
                {
                    _currentTimeSeconds %= clipDuration;
                }
                else
                {
                    _currentTimeSeconds = clipDuration;
                    _isPlaying = false;
                }
            }

            ApplyCurrentPose();
        }

        public void Play()
        {
            if (string.IsNullOrWhiteSpace(_currentClip))
            {
                _currentClip = string.IsNullOrWhiteSpace(_initialClip) ? null : _initialClip;
            }

            if (string.IsNullOrWhiteSpace(_currentClip))
            {
                return;
            }

            _isPlaying = true;
            ApplyCurrentPose();
        }

        public void Play(string clipId, bool restart = true)
        {
            _currentClip = clipId;
            if (restart)
            {
                _currentTimeSeconds = 0f;
            }

            _isPlaying = true;
            ApplyCurrentPose();
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void Stop()
        {
            _isPlaying = false;
            _currentTimeSeconds = 0f;
            ApplyCurrentPose();
        }

        public void ScrubToTime(float timeSeconds)
        {
            _currentTimeSeconds = Mathf.Max(0f, timeSeconds);
            ApplyCurrentPose();
        }

        public void ScrubToNormalizedTime(float normalizedTime)
        {
            if (string.IsNullOrWhiteSpace(_currentClip))
            {
                return;
            }

            var clipDuration = GetClipDurationSeconds(_currentClip);
            if (clipDuration <= 0f)
            {
                _currentTimeSeconds = 0f;
            }
            else
            {
                _currentTimeSeconds = Mathf.Clamp01(normalizedTime) * clipDuration;
            }

            ApplyCurrentPose();
        }

        protected override void Unbind()
        {
            _isPlaying = false;
            base.Unbind();
        }

        protected abstract void BindMotionView(VisualElement root);

        protected abstract bool TryApplyMotionAtTime(string clipId, float timeSeconds);

        protected abstract float GetClipDurationSeconds(string clipId);

        private void ApplyCurrentPose()
        {
            if (string.IsNullOrWhiteSpace(_currentClip))
            {
                return;
            }

            var clipDuration = GetClipDurationSeconds(_currentClip);
            if (clipDuration > 0f)
            {
                _currentTimeSeconds = Mathf.Clamp(_currentTimeSeconds, 0f, clipDuration);
            }
            else
            {
                _currentTimeSeconds = 0f;
            }

            TryApplyMotionAtTime(_currentClip, _currentTimeSeconds);
        }
    }
}
