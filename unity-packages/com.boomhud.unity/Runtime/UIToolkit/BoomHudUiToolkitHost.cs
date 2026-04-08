using System.Collections.Generic;
using BoomHud.Unity.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Unity.UIToolkit
{
    [DisallowMultipleComponent]
    public abstract class BoomHudUiToolkitHost : BoomHudViewHost
    {
        [SerializeField] private UIDocument? _document;
        [SerializeField] private VisualTreeAsset? _visualTree;
        [SerializeField] private List<StyleSheet> _styleSheets = new();

        protected UIDocument Document
            => _document != null ? _document : _document = GetComponent<UIDocument>() ?? throw new MissingComponentException("BoomHudUiToolkitHost requires a UIDocument reference.");

        protected VisualElement Root => Document.rootVisualElement;

        protected override void EnsureInitialized()
        {
            var root = Root;

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
            BindView(Root);
        }

        protected abstract void BindView(VisualElement root);

        protected virtual void Reset()
        {
            _document = GetComponent<UIDocument>();
        }
    }
}