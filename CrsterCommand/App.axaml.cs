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

namespace CrsterCommand;

public partial class App : Application
{
    private ScreenCaptureHotkeyService? _hotkeyService;
    private DesktopRobotHotkeyService? _desktopRobotHotkeyService;

    public ScreenCaptureHotkeyService? HotkeyService => _hotkeyService;
    public DesktopRobotHotkeyService? DesktopRobotHotkeyService => _desktopRobotHotkeyService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storageService = new StorageService();
            var appViewModel = new AppViewModel();
            DataContext = appViewModel;

            var mainViewModel = new MainViewModel(storageService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            appViewModel.AttachMainWindow(mainWindow);

            // If started with --startup-hidden flag, keep app in tray without showing window
            if (Program.IsStartHidden)
            {
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
                appViewModel.SetTrayVisible(true);
                mainWindow.ShowInTaskbar = false;
                mainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
            }
            else
            {
                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            _hotkeyService = new ScreenCaptureHotkeyService(storageService, mainViewModel.ScreenCaptureViewModel);
            _hotkeyService.Start();

            _desktopRobotHotkeyService = new DesktopRobotHotkeyService(storageService, mainViewModel.MacroManagerViewModel);
            _desktopRobotHotkeyService.Start();

            // Handle application exit to properly dispose resources before shutdown
            desktop.ShutdownRequested += (_, e) =>
            {
                try
                {
                    _hotkeyService?.Dispose();
                    _hotkeyService = null;
                }
                catch
                {
                }

                try
                {
                    _desktopRobotHotkeyService?.Dispose();
                    _desktopRobotHotkeyService = null;
                }
                catch
                {
                }

                try
                {
                    GlobalHookManager.ResetInstance();
                }
                catch
                {
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}