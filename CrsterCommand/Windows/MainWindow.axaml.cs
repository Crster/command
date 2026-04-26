using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CrsterCommand.Services;
using CrsterCommand.ViewModels;
using System.Diagnostics;

namespace CrsterCommand.Windows;

public partial class MainWindow : Window
{
    private ScreenCaptureHotkeyService? _hotkeyService;
    private DesktopRobotHotkeyService? _desktopRobotHotkeyService;
    private bool _isCleanupCompleted;

    public ScreenCaptureHotkeyService? HotkeyService => _hotkeyService;
    public DesktopRobotHotkeyService? DesktopRobotHotkeyService => _desktopRobotHotkeyService;

    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += MainWindow_PropertyChanged;
        Closed += MainWindow_Closed;

        // Add tunneling event handler to capture Ctrl+V before it reaches child elements
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public void InitializeHotkeyServices(StorageService storageService, MainViewModel mainViewModel)
    {
        if (_hotkeyService != null || _desktopRobotHotkeyService != null)
        {
            return;
        }

        _hotkeyService = new ScreenCaptureHotkeyService(storageService, mainViewModel.ScreenCaptureViewModel);
        _hotkeyService.Start();

        _desktopRobotHotkeyService = new DesktopRobotHotkeyService(storageService, mainViewModel.MacroManagerViewModel);
        _desktopRobotHotkeyService.Start();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Intercept Ctrl+V during the tunnel phase
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Debug.WriteLine("MainWindow - Ctrl + V captured via tunneling");
            // Event will continue to bubble to child controls
        }
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            if (WindowState == WindowState.Minimized)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(true);

                if (IsVisible)
                {
                    Hide();
                }
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(false);
            }
        }
    }

    private void MainWindow_Closed(object? sender, System.EventArgs e)
    {
        CleanupResources();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        CleanupResources();
        base.OnClosed(e);
    }

    private void CleanupResources()
    {
        if (_isCleanupCompleted)
        {
            return;
        }

        _isCleanupCompleted = true;

        // Unsubscribe window events first
        PropertyChanged -= MainWindow_PropertyChanged;
        Closed -= MainWindow_Closed;
        RemoveHandler(InputElement.KeyDownEvent, OnPreviewKeyDown);

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
    }
}
