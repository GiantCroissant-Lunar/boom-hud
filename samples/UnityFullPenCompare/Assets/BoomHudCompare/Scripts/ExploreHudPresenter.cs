using BoomHud.Unity.UIToolkit;
using Generated.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class ExploreHudPresenter : BoomHudUiToolkitHost
    {
        private const string VisualTreeResourcePath = "BoomHudGenerated/ExploreHudView";
        private const string StyleSheetResourcePath = "BoomHudGenerated/ExploreHudView";
        private const string GeneratedRootName = "ExploreHUD";

        [SerializeField] private EmptyExploreHudViewModel? _viewModel;

        private ExploreHudView? _view;
        private VisualTreeAsset? _loadedVisualTree;
        private StyleSheet? _loadedStyleSheet;

        protected override void BindView(VisualElement root)
        {
            _loadedVisualTree ??= Resources.Load<VisualTreeAsset>(VisualTreeResourcePath);
            _loadedStyleSheet ??= Resources.Load<StyleSheet>(StyleSheetResourcePath);

            if (_loadedVisualTree == null)
            {
                Debug.LogError($"Could not load VisualTreeAsset from Resources/{VisualTreeResourcePath}.uxml");
                return;
            }

            root.Clear();
            _loadedVisualTree.CloneTree(root);

            if (_loadedStyleSheet != null && !root.styleSheets.Contains(_loadedStyleSheet))
            {
                root.styleSheets.Add(_loadedStyleSheet);
            }

            var generatedRoot = root.Q<VisualElement>(GeneratedRootName);
            if (generatedRoot == null)
            {
                Debug.LogError($"Could not find generated root '{GeneratedRootName}' after cloning the tree.");
                return;
            }

            _view ??= new ExploreHudView(generatedRoot);
            _view.ViewModel = _viewModel;
        }

        protected override void Unbind()
        {
            if (_view != null)
            {
                _view.ViewModel = null;
            }
        }
    }
}