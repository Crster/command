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

    public async override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storageService = new StorageService();
            var appViewModel = new AppViewModel();
            var mainViewModel = new MainViewModel(storageService);

            MainWindow mainWindow;

            if (Program.IsStartHidden)
            {
                mainWindow = new MainWindow()
                {
                    DataContext = mainViewModel,
                    ShowActivated = false,
                };
            }
            else
            {
                mainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };
            }

            appViewModel.AttachMainWindow(mainWindow);

            DataContext = appViewModel;

            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            _hotkeyService = new ScreenCaptureHotkeyService(storageService, mainViewModel.ScreenCaptureViewModel);
            _hotkeyService.Start();

            _desktopRobotHotkeyService = new DesktopRobotHotkeyService(storageService, mainViewModel.MacroManagerViewModel);
            _desktopRobotHotkeyService.Start();

            if (Program.IsStartHidden)
            {
                await mainViewModel.MinimizeToTrayAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}