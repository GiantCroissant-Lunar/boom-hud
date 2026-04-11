using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    public sealed class FixtureUGuiPresenter : BoomHudUGuiHost
    {
        private const string DefaultGeneratedViewTypeName = "Generated.Hud.UGui.PartyStatusStripView";
        private const string DefaultTargetObjectName = "PartyStatusStripRoot";

        [SerializeField] private string _generatedViewTypeName = DefaultGeneratedViewTypeName;
        [SerializeField] private string _targetObjectName = DefaultTargetObjectName;

        private object? _view;

        protected override string RootObjectName => "BoomHudUGuiFixtureRoot";

        public void Configure(string generatedViewTypeName, string targetObjectName)
        {
            _generatedViewTypeName = generatedViewTypeName;
            _targetObjectName = targetObjectName;
            _view = null;
            Rebind();
        }

        protected override void BindView(RectTransform root)
        {
            if (string.IsNullOrWhiteSpace(_generatedViewTypeName) || string.IsNullOrWhiteSpace(_targetObjectName))
            {
                Debug.LogError("FixtureUGuiPresenter is missing fixture configuration.");
                return;
            }

            var background = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 1f);

            _view = CreateGeneratedViewInstance(root);
            RenameGeneratedRoot(_view);
        }

        protected override void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
        }

        protected override void Unbind()
        {
            _view = null;
        }

        private object CreateGeneratedViewInstance(RectTransform root)
        {
            var generatedViewType = ResolveType(_generatedViewTypeName);

            try
            {
                return Activator.CreateInstance(generatedViewType, new object?[] { root, null })
                    ?? throw new InvalidOperationException($"Could not create generated view '{generatedViewType.FullName}'.");
            }
            catch (MissingMethodException)
            {
                return Activator.CreateInstance(generatedViewType, new object?[] { root })
                    ?? throw new InvalidOperationException($"Could not create generated view '{generatedViewType.FullName}'.");
            }
        }

        private void RenameGeneratedRoot(object generatedView)
        {
            var rootProperty = generatedView.GetType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public);
            if (rootProperty?.GetValue(generatedView) is not RectTransform generatedRoot)
            {
                throw new InvalidOperationException($"Generated view '{generatedView.GetType().FullName}' does not expose a RectTransform Root.");
            }

            generatedRoot.name = _targetObjectName;
            generatedRoot.anchorMin = new Vector2(0.5f, 0.5f);
            generatedRoot.anchorMax = new Vector2(0.5f, 0.5f);
            generatedRoot.pivot = new Vector2(0.5f, 0.5f);
            generatedRoot.anchoredPosition = Vector2.zero;
            generatedRoot.localScale = Vector3.one;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(generatedRoot);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(generatedRoot);
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
