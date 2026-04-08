using Terminal.Gui;
using BoomHud.Sample.Generated;

namespace BoomHud.Sample.TerminalGuiHost;

internal static class Program
{
    private static void Main()
    {
        Application.Init();

        var window = new Window
        {
            Title = "BoomHud TerminalGui Sample",
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Add generated HUD view and attach a sample ViewModel
        var hud = new AppMainFrameView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        window.Add(hud);

        // var prototype = new PrototypeDashboard();
        // window.Add(prototype);

        Application.Run(window);
        Application.Shutdown();
    }
}
