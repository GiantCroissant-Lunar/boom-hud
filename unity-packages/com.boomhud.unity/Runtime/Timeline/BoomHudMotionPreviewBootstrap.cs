using System.Collections;
using BoomHud.Unity.UIToolkit;
using UnityEngine;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoomHud.Unity.Timeline
{
    [ExecuteAlways]
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
    #if UNITY_EDITOR
        private bool _editorRefreshQueued;
    #endif

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
#if UNITY_EDITOR
                QueueEditorRefresh();
#endif
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
                goto EditorCleanup;
            }

            StopCoroutine(_startupRoutine);
            _startupRoutine = null;

EditorCleanup:
#if UNITY_EDITOR
            _editorRefreshQueued = false;
#endif
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            QueueEditorRefresh();
        }

        private void QueueEditorRefresh()
        {
            if (_editorRefreshQueued)
            {
                return;
            }

            _editorRefreshQueued = true;
            EditorApplication.delayCall += PerformEditorRefresh;
        }

        private void PerformEditorRefresh()
        {
            _editorRefreshQueued = false;

            if (this == null || !isActiveAndEnabled || Application.isPlaying)
            {
                return;
            }

            var host = Host;
            host.Rebind();

            var director = Director;
            if (director == null)
            {
                return;
            }

            if (director.playableAsset != null)
            {
                director.RebuildGraph();
                director.Evaluate();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_fallbackClipId))
            {
                host.Evaluate(_fallbackClipId, 0f);
            }
        }
#endif
    }
}
