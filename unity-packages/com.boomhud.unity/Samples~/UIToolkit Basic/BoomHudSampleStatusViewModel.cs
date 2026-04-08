using UnityEngine;

namespace BoomHud.Unity.Samples.UIToolkit
{
    public sealed class BoomHudSampleStatusViewModel : MonoBehaviour
    {
        [SerializeField] private string _statusText = "Systems nominal";
        [SerializeField] private Color _statusColor = new(0.25f, 0.9f, 0.45f, 1f);

        public string StatusText => _statusText;

        public Color StatusColor => _statusColor;

        public void SetStatus(string text, Color color)
        {
            _statusText = text;
            _statusColor = color;
        }
    }
}
