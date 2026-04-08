using BoomHud.Unity.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace BoomHud.Unity.Samples.UIToolkit
{
    public sealed class BoomHudSampleStatusPresenter : BoomHudUiToolkitHost
    {
        [SerializeField] private BoomHudSampleStatusViewModel? _viewModel;
        [SerializeField] private string _labelName = "StatusLabel";

        private Label? _label;

        protected override void BindView(VisualElement root)
        {
            _label ??= root.Q<Label>(_labelName);
            if (_label == null)
            {
                Debug.LogWarning($"Could not find a Label named '{_labelName}' in the attached UIDocument.");
                return;
            }

            if (_viewModel == null)
            {
                Debug.LogWarning("Assign a BoomHudSampleStatusViewModel to drive the sample presenter.");
                return;
            }

            _label.text = _viewModel.StatusText;
            _label.style.color = _viewModel.StatusColor;
        }
    }
}
