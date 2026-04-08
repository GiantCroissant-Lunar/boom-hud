using BoomHud.Sample.Generated;

namespace BoomHud.Sample.TerminalGuiHost;

/// <summary>
/// Simple sample ViewModel for the generated StatusBarView.
/// In real apps, this would be backed by your game state/services.
/// </summary>
public sealed class StatusBarViewModel : IStatusBarViewModel
{
    public object? Health => "100/100";

    public object? Mana => "50/100";

    public object? Location => "Town Square";

    public object? Time => "12:34";

    public object? Fps => "60";
}
