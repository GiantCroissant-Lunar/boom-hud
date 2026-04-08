using BoomHud.Unity.Runtime;
using UnityEngine;

namespace BoomHud.Unity.UGUI
{
    [DisallowMultipleComponent]
    public abstract class BoomHudUguiHost : BoomHudViewHost
    {
        [SerializeField] private Canvas? _canvas;
        [SerializeField] private RectTransform? _root;

        protected Canvas Canvas
            => _canvas != null ? _canvas : _canvas = GetComponentInParent<Canvas>() ?? throw new MissingComponentException("BoomHudUguiHost requires a Canvas in the parent hierarchy or an explicit Canvas reference.");

        protected RectTransform Root
            => _root != null ? _root : _root = transform as RectTransform ?? throw new MissingComponentException("BoomHudUguiHost requires a RectTransform root.");

        protected override void EnsureInitialized()
        {
            _ = Canvas;
            _ = Root;
        }

        protected override void Bind()
        {
            BindView(Canvas, Root);
        }

        protected abstract void BindView(Canvas canvas, RectTransform root);
    }
}