using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CrsterCommand.ViewModels;
using CrsterCommand.Views;
using CrsterCommand.Windows;

namespace CrsterCommand;

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
            var appViewModel = new AppViewModel();
            DataContext = appViewModel;

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };

            appViewModel.AttachMainWindow(desktop.MainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }
}