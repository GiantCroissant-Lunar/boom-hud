using BoomHud.Unity.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace BoomHud.Unity.Samples.UGUI
{
    public sealed class BoomHudSampleUguiPresenter : BoomHudUguiHost
    {
        [SerializeField] private BoomHudSampleUguiViewModel? _viewModel;
        [SerializeField] private Text? _statusLabel;

        protected override void BindView(Canvas canvas, RectTransform root)
        {
            if (_statusLabel == null)
            {
                _statusLabel = root.GetComponentInChildren<Text>(includeInactive: true);
            }

            if (_statusLabel == null)
            {
                Debug.LogWarning("Could not find a UnityEngine.UI.Text under the sample root.");
                return;
            }

            if (_viewModel == null)
            {
                Debug.LogWarning("Assign a BoomHudSampleUguiViewModel to drive the sample presenter.");
                return;
            }

            _statusLabel.text = _viewModel.StatusText;
            _statusLabel.color = _viewModel.StatusColor;
        }
    }
}
