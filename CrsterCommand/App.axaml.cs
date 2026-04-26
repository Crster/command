using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CrsterCommand.Services;
using CrsterCommand.ViewModels;
using CrsterCommand.Views;
using CrsterCommand.Windows;
using System;

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

            desktop.MainWindow = mainWindow;

            appViewModel.AttachMainWindow(mainWindow);

            // If started with --startup-hidden flag, hide the window
            if (Program.IsStartHidden)
            {
                mainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Hide();
                Console.WriteLine("[App] Starting hidden due to startup flag");
            }

            _hotkeyService = new ScreenCaptureHotkeyService(storageService, mainViewModel.ScreenCaptureViewModel);
            _hotkeyService.Start();

            _desktopRobotHotkeyService = new DesktopRobotHotkeyService(storageService, mainViewModel.MacroManagerViewModel);
            _desktopRobotHotkeyService.Start();

            // Handle application exit to properly dispose resources before shutdown
            desktop.ShutdownRequested += (_, e) =>
            {
                Console.WriteLine("[App] ShutdownRequested - disposing services");

                try
                {
                    _hotkeyService?.Dispose();
                    _hotkeyService = null;
                    Console.WriteLine("[App] HotkeyService disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] Error disposing HotkeyService: {ex.Message}");
                }

                try
                {
                    _desktopRobotHotkeyService?.Dispose();
                    _desktopRobotHotkeyService = null;
                    Console.WriteLine("[App] DesktopRobotHotkeyService disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] Error disposing DesktopRobotHotkeyService: {ex.Message}");
                }

                try
                {
                    GlobalHookManager.ResetInstance();
                    Console.WriteLine("[App] GlobalHookManager disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] Error disposing GlobalHookManager: {ex.Message}");
                }

                Console.WriteLine("[App] All services disposed successfully");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}