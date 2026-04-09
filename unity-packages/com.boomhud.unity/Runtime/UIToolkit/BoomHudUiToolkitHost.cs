using System.Collections;
using System.Collections.Generic;
using BoomHud.Unity.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoomHud.Unity.UIToolkit
{
    [DisallowMultipleComponent]
    public abstract class BoomHudUiToolkitHost : BoomHudViewHost
    {
        [SerializeField] private UIDocument? _document;
        [SerializeField] private VisualTreeAsset? _visualTree;
        [SerializeField] private List<StyleSheet> _styleSheets = new();
        private bool _rebindQueued;

        protected UIDocument Document
            => _document != null ? _document : _document = GetComponent<UIDocument>() ?? throw new MissingComponentException("BoomHudUiToolkitHost requires a UIDocument reference.");

        protected VisualElement Root => TryGetRoot(out var root)
            ? root
            : throw new MissingReferenceException("UIDocument rootVisualElement is not ready yet.");

        protected override void EnsureInitialized()
        {
            if (!TryGetRoot(out var root))
            {
                QueueRebind();
                return;
            }

            if (_visualTree != null && root.childCount == 0)
            {
                root.Clear();
                _visualTree.CloneTree(root);
            }

            foreach (var styleSheet in _styleSheets)
            {
                if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                {
                    root.styleSheets.Add(styleSheet);
                }
            }
        }

        protected override void Bind()
        {
            if (!TryGetRoot(out var root))
            {
                QueueRebind();
                return;
            }

            BindView(root);
        }

        protected abstract void BindView(VisualElement root);

        private bool TryGetRoot(out VisualElement root)
        {
            root = Document.rootVisualElement;
            return root != null && root.panel != null;
        }

        private void QueueRebind()
        {
            if (_rebindQueued)
            {
                return;
            }

            _rebindQueued = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += PerformDelayedRebind;
                return;
            }
#endif

            StartCoroutine(RebindNextFrame());
        }

        private IEnumerator RebindNextFrame()
        {
            yield return null;
            PerformDelayedRebind();
        }

        private void PerformDelayedRebind()
        {
            _rebindQueued = false;

            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            Rebind();
        }

        protected virtual void Reset()
        {
            _document = GetComponent<UIDocument>();
        }
    }
}
