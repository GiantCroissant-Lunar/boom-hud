using System.ComponentModel;
using Generated.Hud;

namespace BoomHud.Compare
{
    public sealed class EmptyExploreHudViewModel : IExploreHudViewModel
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}