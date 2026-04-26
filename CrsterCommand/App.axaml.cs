using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CrsterCommand.Services;
using CrsterCommand.ViewModels;
using CrsterCommand.Views;
using CrsterCommand.Windows;
using System.Diagnostics;

namespace CrsterCommand;

public partial class App : Application
{
    public ScreenCaptureHotkeyService? HotkeyService =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is MainWindow mainWindow
            ? mainWindow.HotkeyService
            : null;

    public DesktopRobotHotkeyService? DesktopRobotHotkeyService =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is MainWindow mainWindow
            ? mainWindow.DesktopRobotHotkeyService
            : null;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storageService = new StorageService();
            var appViewModel = new AppViewModel();
            var mainViewModel = new MainViewModel(storageService);

            static void LogStartupState(string stage, MainWindow window, AppViewModel appVm)
            {
                Debug.WriteLine(
                    $"[Startup] {stage} | IsStartHidden={Program.IsStartHidden} | " +
                    $"IsHidden={!window.IsVisible} | IsMinimized={window.WindowState == WindowState.Minimized} | " +
                    $"TrayIconVisible={appVm.TrayIconVisible}");
            }

            MainWindow mainWindow;

            if (Program.IsStartHidden)
            {
                appViewModel.SetTrayVisible(true);

                mainWindow = new MainWindow()
                {
                    DataContext = mainViewModel,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowState = WindowState.Minimized,
                    IsVisible = false,
                };

                LogStartupState("Created hidden-start window", mainWindow, appViewModel);
            }
            else
            {
                mainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };

                LogStartupState("Created normal-start window", mainWindow, appViewModel);
            }

            appViewModel.AttachMainWindow(mainWindow);

            DataContext = appViewModel;

            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            LogStartupState("MainWindow assigned to desktop lifetime", mainWindow, appViewModel);

            mainWindow.InitializeHotkeyServices(storageService, mainViewModel);

            if (Program.IsStartHidden)
            {
                await mainViewModel.MinimizeToTrayAsync();
                LogStartupState("Applied MinimizeToTrayAsync", mainWindow, appViewModel);
            }
            else
            {
                LogStartupState("Normal startup completed", mainWindow, appViewModel);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}