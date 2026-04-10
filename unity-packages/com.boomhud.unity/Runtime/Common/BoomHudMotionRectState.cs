using UnityEngine;

namespace BoomHud.Unity.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoomHudMotionRectState : MonoBehaviour
    {
        [SerializeField] private bool _initialized;
        [SerializeField] private Vector2 _anchoredPosition;
        [SerializeField] private Vector3 _localScale = Vector3.one;
        [SerializeField] private Vector3 _localEulerAngles;

        public Vector2 AnchoredPosition => _anchoredPosition;

        public Vector3 LocalScale => _localScale;

        public Vector3 LocalEulerAngles => _localEulerAngles;

        public static BoomHudMotionRectState Capture(Component target)
        {
            var state = target.GetComponent<BoomHudMotionRectState>();
            if (state == null)
            {
                state = target.gameObject.AddComponent<BoomHudMotionRectState>();
            }

            if (!state._initialized)
            {
                state._initialized = true;
                if (target.TryGetComponent<RectTransform>(out var rectTransform))
                {
                    state._anchoredPosition = rectTransform.anchoredPosition;
                }

                state._localScale = target.transform.localScale;
                state._localEulerAngles = target.transform.localEulerAngles;
            }

            return state;
        }
    }
}
