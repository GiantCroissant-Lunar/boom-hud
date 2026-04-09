using UnityEngine;
using UnityEngine.UIElements;
using BoomHud.Unity.UIToolkit;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MotionStagePresenter : MonoBehaviour
    {
        [SerializeField] private string _generatedRootName = "ComponentCharPortrait";

        private UIDocument? _document;

        private void OnEnable()
        {
            ApplyStage();
        }

        private void Start()
        {
            ApplyStage();
        }

        private void ApplyStage()
        {
            _document ??= GetComponent<UIDocument>();
            GetComponent<BoomHudUiToolkitMotionHost>()?.Rebind();

            if (_document == null)
            {
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.paddingLeft = 0f;
            root.style.paddingTop = 0f;
            root.style.paddingRight = 0f;
            root.style.paddingBottom = 0f;
            root.style.backgroundColor = Color.clear;

            var generatedRoot = string.IsNullOrWhiteSpace(_generatedRootName)
                ? root.ElementAt(0)
                : root.Q<VisualElement>(_generatedRootName);
            if (generatedRoot == null)
            {
                return;
            }

            generatedRoot.style.flexShrink = 0f;
            generatedRoot.style.marginLeft = 0f;
            generatedRoot.style.marginTop = 0f;
            generatedRoot.style.marginRight = 0f;
            generatedRoot.style.marginBottom = 0f;
        }
    }
}