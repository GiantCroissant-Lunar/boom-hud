using BoomHud.Sample.Generated;

namespace BoomHud.Sample.TerminalGuiHost;

/// <summary>
/// Sample ViewModel for the generated HudRootView.
/// In a real game this would be backed by live HUD state.
/// </summary>
public sealed class HudRootViewModel : IHudRootViewModel
{
    public object? Health => "100/100";
    public object? Mana => "50/100";
    public object? Location => "Town Square";
    public object? Time => "12:34";
    public object? Fps => "60";

    public object? Party1Name => "Alice";
    public object? Party1Health => "120/150";

    public object? Party2Name => "Bob";
    public object? Party2Health => "80/120";

    public object? Party3Name => "Cara";
    public object? Party3Health => "200/200";

    public object? ChatLine1 => "[System] Welcome to the dungeon.";
    public object? ChatLine2 => "[Alice] Let's go left.";
    public object? ChatLine3 => "[Bob] Pulling in 3...2...1...";
}
