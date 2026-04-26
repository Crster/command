using Avalonia;
using System;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace CrsterCommand;

sealed class Program
{
    private static bool _startHidden = true;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for startup-hidden flag
        //if (args.Length > 0 && args[0] == "--startup-hidden")
        //{
        //    _startHidden = true;
        //}

        UpdateManager.Initialize();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static bool IsStartHidden => _startHidden;

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
