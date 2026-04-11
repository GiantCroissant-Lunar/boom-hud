using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoomHud.Compare
{
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public abstract class BoomHudUGuiHost : MonoBehaviour
    {
        [SerializeField] private RectTransform? _generatedRoot;
#if UNITY_EDITOR
        private bool _editorRebindQueued;
#endif

        protected RectTransform GeneratedRoot => _generatedRoot ??= EnsureGeneratedRoot();

        protected virtual string RootObjectName => "BoomHudUGuiRoot";

        protected virtual void OnEnable()
        {
            RequestRebind();
        }

        protected virtual void OnDisable()
        {
            Unbind();
        }

        protected virtual void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                RequestRebind();
            }
        }

        private void RequestRebind()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueEditorRebind();
                return;
            }
#endif
            Rebind();
        }

        public void Rebind()
        {
            EnsureCanvasSetup();

            var root = EnsureGeneratedRoot();
            ClearRoot(root);
            ConfigureRoot(root);
            BindView(root);
            QueueEditorRefresh();
        }

        protected abstract void BindView(RectTransform root);

        protected virtual void Unbind()
        {
        }

        protected virtual void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        protected virtual void ConfigureCanvas(Canvas canvas, Camera mainCamera)
        {
            if (mainCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCamera;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }

        protected static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return rectTransform;
        }

        protected static Image CreateImage(string name, Transform parent, Color color)
        {
            var rectTransform = CreateRect(name, parent);
            rectTransform.gameObject.AddComponent<CanvasRenderer>();
            var image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        protected static Text CreateText(string name, Transform parent, string text, int fontSize, Color color, TextAnchor alignment = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal)
        {
            var rectTransform = CreateRect(name, parent);
            rectTransform.gameObject.AddComponent<CanvasRenderer>();
            var label = rectTransform.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        protected static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        protected static void FitPreviewToSurface(RectTransform preview, RectTransform surface, float padding = 8f)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(preview);

            var preferredWidth = Mathf.Max(preview.rect.width, LayoutUtility.GetPreferredWidth(preview));
            var preferredHeight = Mathf.Max(preview.rect.height, LayoutUtility.GetPreferredHeight(preview));
            var safeWidth = Mathf.Max(0f, surface.rect.width - (padding * 2f));
            var safeHeight = Mathf.Max(0f, surface.rect.height - (padding * 2f));

            var scaleX = preferredWidth > 0f ? safeWidth / preferredWidth : 1f;
            var scaleY = preferredHeight > 0f ? safeHeight / preferredHeight : 1f;
            var scale = Mathf.Min(1f, scaleX, scaleY);

            preview.anchorMin = new Vector2(0.5f, 0.5f);
            preview.anchorMax = new Vector2(0.5f, 0.5f);
            preview.pivot = new Vector2(0.5f, 0.5f);
            preview.anchoredPosition = Vector2.zero;
            preview.localScale = new Vector3(scale, scale, 1f);
        }

        private void EnsureCanvasSetup()
        {
            var canvas = GetComponent<Canvas>();
            var mainCamera = Camera.main;
            ConfigureCanvas(canvas, mainCamera);
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 0;

            var scaler = GetComponent<CanvasScaler>();
            ConfigureCanvasScaler(scaler);
        }

        private RectTransform EnsureGeneratedRoot()
        {
            if (_generatedRoot != null)
            {
                return _generatedRoot;
            }

            var existing = transform.Find(RootObjectName);
            if (existing != null && existing.TryGetComponent<RectTransform>(out var existingRect))
            {
                _generatedRoot = existingRect;
                return existingRect;
            }

            _generatedRoot = CreateRect(RootObjectName, transform);
            return _generatedRoot;
        }

        private void ConfigureRoot(RectTransform root)
        {
            StretchToParent(root);
            root.anchoredPosition = Vector2.zero;
        }

        private static void ClearRoot(RectTransform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index);
                if (Application.isPlaying)
                {
                    Object.Destroy(child.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void QueueEditorRefresh()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Canvas.ForceUpdateCanvases();
                EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRebind()
        {
            if (_editorRebindQueued)
            {
                return;
            }

            _editorRebindQueued = true;
            EditorApplication.delayCall += RunDeferredEditorRebind;
        }

        private void RunDeferredEditorRebind()
        {
            EditorApplication.delayCall -= RunDeferredEditorRebind;
            _editorRebindQueued = false;

            if (this == null || !isActiveAndEnabled || Application.isPlaying)
            {
                return;
            }

            Rebind();
        }
#endif
    }
}
