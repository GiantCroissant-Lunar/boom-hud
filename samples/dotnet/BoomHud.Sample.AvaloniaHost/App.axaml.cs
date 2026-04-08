using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BoomHud.Sample.Generated;

namespace BoomHud.Sample.AvaloniaHost;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var view = new AppMainFrameView();

            desktop.MainWindow = new Window
            {
                Title = "BoomHud Avalonia Sample",
                Width = 1440,
                Height = 1024,
                Content = view
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
