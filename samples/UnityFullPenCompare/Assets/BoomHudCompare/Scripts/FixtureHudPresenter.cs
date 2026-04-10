using System;
using System.Linq;
using BoomHud.Unity.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FixtureHudPresenter : BoomHudUiToolkitHost
    {
        private const float CaptureWidth = 1920f;
        private const float CaptureHeight = 1080f;
        private const string DefaultResourceBasePath = "BoomHudGenerated/PartyStatusStripView";
        private const string DefaultGeneratedRootName = "PartyStatusStrip";
        private const string DefaultGeneratedViewTypeName = "Generated.Hud.PartyStatusStripView";

        [SerializeField] private string _resourceBasePath = DefaultResourceBasePath;
        [SerializeField] private string _generatedRootName = DefaultGeneratedRootName;
        [SerializeField] private string _generatedViewTypeName = DefaultGeneratedViewTypeName;

        private StyleSheet? _appliedStyleSheet;
        private object? _view;

        public void Configure(string resourceBasePath, string generatedRootName, string generatedViewTypeName)
        {
            _resourceBasePath = resourceBasePath;
            _generatedRootName = generatedRootName;
            _generatedViewTypeName = generatedViewTypeName;
            _view = null;
            Rebind();
        }

        protected override void BindView(VisualElement root)
        {
            if (string.IsNullOrWhiteSpace(_resourceBasePath) ||
                string.IsNullOrWhiteSpace(_generatedRootName) ||
                string.IsNullOrWhiteSpace(_generatedViewTypeName))
            {
                Debug.LogError("FixtureHudPresenter is missing fixture configuration.");
                return;
            }

            var visualTree = Resources.Load<VisualTreeAsset>(_resourceBasePath);
            if (visualTree == null)
            {
                Debug.LogError($"Could not load VisualTreeAsset from Resources/{_resourceBasePath}.uxml");
                return;
            }

            var styleSheet = Resources.Load<StyleSheet>(_resourceBasePath);

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.width = CaptureWidth;
            root.style.height = CaptureHeight;
            root.style.minWidth = CaptureWidth;
            root.style.minHeight = CaptureHeight;
            root.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            root.style.alignItems = Align.FlexStart;
            root.style.justifyContent = Justify.FlexStart;
            root.style.paddingLeft = 0f;
            root.style.paddingTop = 0f;
            root.style.paddingRight = 0f;
            root.style.paddingBottom = 0f;

            if (_appliedStyleSheet != null && _appliedStyleSheet != styleSheet)
            {
                root.styleSheets.Remove(_appliedStyleSheet);
            }

            visualTree.CloneTree(root);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            _appliedStyleSheet = styleSheet;

            var generatedRoot = root.Q<VisualElement>(_generatedRootName);
            if (generatedRoot == null)
            {
                Debug.LogError($"Could not find generated root '{_generatedRootName}' after cloning the tree.");
                return;
            }

            generatedRoot.style.left = 0f;
            generatedRoot.style.top = 0f;
            generatedRoot.style.alignSelf = Align.FlexStart;
            _view = CreateGeneratedViewInstance(generatedRoot);
            root.MarkDirtyRepaint();
        }

        protected override void Unbind()
        {
            _view = null;
        }

        private object CreateGeneratedViewInstance(VisualElement generatedRoot)
        {
            var generatedViewType = ResolveType(_generatedViewTypeName);
            var constructor = generatedViewType.GetConstructor(new[] { typeof(VisualElement) });
            if (constructor == null)
            {
                throw new MissingMethodException(generatedViewType.FullName, ".ctor(VisualElement)");
            }

            return constructor.Invoke(new object[] { generatedRoot });
        }

        private static Type ResolveType(string typeName)
        {
            var resolvedType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                .FirstOrDefault(type => type != null);

            return resolvedType ?? throw new InvalidOperationException($"Could not resolve generated view type '{typeName}'.");
        }
    }
}
