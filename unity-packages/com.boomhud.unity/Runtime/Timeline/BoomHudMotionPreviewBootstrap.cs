using System.Collections;
using BoomHud.Unity.UIToolkit;
using UnityEngine;
using UnityEngine.Playables;

namespace BoomHud.Unity.Timeline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoomHudUiToolkitMotionHost))]
    public sealed class BoomHudMotionPreviewBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayableDirector? _director;
        [SerializeField] private BoomHudUiToolkitMotionHost? _host;
        [SerializeField] private string _fallbackClipId = "intro";
        [SerializeField] private bool _preferTimelinePlayback = true;
        [SerializeField] private bool _disableDirectorWhenFallback = true;

        private Coroutine? _startupRoutine;

        private BoomHudUiToolkitMotionHost Host
            => _host != null ? _host : _host = GetComponent<BoomHudUiToolkitMotionHost>();

        private PlayableDirector? Director
            => _director != null ? _director : _director = GetComponent<PlayableDirector>();

        public void Configure(PlayableDirector director, BoomHudUiToolkitMotionHost host, string fallbackClipId)
        {
            _director = director;
            _host = host;
            _fallbackClipId = fallbackClipId;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_startupRoutine != null)
            {
                StopCoroutine(_startupRoutine);
            }

            _startupRoutine = StartCoroutine(EnsurePreviewPlayback());
        }

        private void OnDisable()
        {
            if (_startupRoutine == null)
            {
                return;
            }

            StopCoroutine(_startupRoutine);
            _startupRoutine = null;
        }

        private IEnumerator EnsurePreviewPlayback()
        {
            yield return null;
            yield return null;

            var host = Host;
            var director = Director;

            if (_preferTimelinePlayback && director != null && director.playableAsset != null)
            {
                host.Pause();
                director.time = 0d;
                director.Evaluate();
                director.Play();
                yield return null;

                if (director.state == PlayState.Playing)
                {
                    _startupRoutine = null;
                    yield break;
                }
            }

            if (director != null)
            {
                director.Stop();
                if (_disableDirectorWhenFallback)
                {
                    director.enabled = false;
                }
            }

            host.Play(_fallbackClipId, restart: true);
            _startupRoutine = null;
        }

        private void Reset()
        {
            _director = GetComponent<PlayableDirector>();
            _host = GetComponent<BoomHudUiToolkitMotionHost>();
        }
    }
}
