using System;

namespace BoomHud.Sample.AvaloniaHost;

/// <summary>
/// Simple sample ViewModel for the generated StatusBarView.
/// This is where you would expose bindable HUD state.
/// </summary>
public sealed class StatusBarViewModel
{
    public string Health => "100/100";
    public string Mana => "50/100";
    public string Location => "Town Square";
    public string Time => DateTime.Now.ToString("HH:mm");
    public string Fps => "60";
}
