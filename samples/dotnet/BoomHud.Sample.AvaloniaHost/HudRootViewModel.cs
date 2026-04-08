namespace BoomHud.Sample.AvaloniaHost;

/// <summary>
/// Sample ViewModel for the generated HudRootView.
/// Exposes properties used by bindings in HudRootView.axaml.
/// </summary>
public sealed class HudRootViewModel
{
    public string Health => "100/100";
    public string Mana => "50/100";
    public string Location => "Town Square";
    public string Time => "12:34";
    public string Fps => "60";

    public string Party1Name => "Alice";
    public string Party1Health => "120/150";

    public string Party2Name => "Bob";
    public string Party2Health => "80/120";

    public string Party3Name => "Cara";
    public string Party3Health => "200/200";

    public string ChatLine1 => "[System] Welcome to the dungeon.";
    public string ChatLine2 => "[Alice] Let's go left.";
    public string ChatLine3 => "[Bob] Pulling in 3...2...1...";
}
