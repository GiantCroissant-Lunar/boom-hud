using UnityEngine;

namespace BoomHud.Unity.Samples.UGUI
{
    public sealed class BoomHudSampleUguiViewModel : MonoBehaviour
    {
        [SerializeField] private string _statusText = "Inventory online";
        [SerializeField] private Color _statusColor = new(1f, 0.75f, 0.2f, 1f);

        public string StatusText => _statusText;

        public Color StatusColor => _statusColor;
    }
}
