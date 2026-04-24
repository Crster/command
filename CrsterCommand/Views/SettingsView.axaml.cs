using Avalonia.Controls;
using Avalonia.Input;
using System;
using CrsterCommand.ViewModels;
using Avalonia;

namespace CrsterCommand.Views;

public partial class SettingsView : UserControl
{
    private bool _editingShortcut;
    private bool _editingDesktopRobotShortcut;
    private string? _lastScreenCaptureShortcut;
    private string? _lastDesktopRobotShortcut;

    public SettingsView()
    {
        InitializeComponent();
        this.Loaded += SettingsView_Loaded;
        this.Unloaded += SettingsView_Unloaded;
    }

    private void SettingsView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Console.WriteLine("[SettingsView] Loaded - pausing hotkey services");
        var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
        app?.HotkeyService?.Pause();
        app?.DesktopRobotHotkeyService?.Pause();
    }

    private void SettingsView_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Console.WriteLine("[SettingsView] Unloaded - resuming hotkey services");
        if (_editingShortcut || _editingDesktopRobotShortcut)
        {
            EndEditing();
        }

        var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
        app?.HotkeyService?.Resume();
        app?.DesktopRobotHotkeyService?.Resume();
    }

    private void ScreenCaptureShortcutBox_GotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_editingShortcut)
        {
            _editingShortcut = true;
            // Save the current shortcut in case user presses Esc
            if (DataContext is SettingsViewModel viewModel)
            {
                _lastScreenCaptureShortcut = viewModel.ScreenCaptureShortcut;
            }
            Console.WriteLine("[SettingsView] Screen Capture Shortcut Box Got Focus - starting capture mode");
            StartShortcutCapture();
        }
    }

    private void ScreenCaptureShortcutBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_editingShortcut)
        {
            EndEditing();
        }
    }

    private void StartShortcutCapture()
    {
        try
        {
            var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
            if (app?.HotkeyService != null)
            {
                app.HotkeyService.StartCaptureMode(
                    OnShortcutCaptured,
                    onEscapePressed: RestoreScreenCaptureShortcut,
                    onBackspacePressed: RestoreScreenCaptureDefault
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting shortcut capture: {ex.Message}");
            EndEditing();
        }
    }

    private void OnShortcutCaptured(string shortcut)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(shortcut))
                return;

            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.ScreenCaptureShortcut = shortcut;
            }

            // Don't refocus - let the LostFocus event handle cleanup naturally
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnShortcutCaptured: {ex.Message}");
        }
    }

    private void StopShortcutCapture()
    {
        try
        {
            var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
            if (app?.HotkeyService != null)
            {
                app.HotkeyService.StopCaptureMode();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping shortcut capture: {ex.Message}");
        }
    }

    private async void EndEditing()
    {
        try
        {
            Console.WriteLine("[SettingsView] EndEditing - stopping capture mode");
            _editingShortcut = false;
            _editingDesktopRobotShortcut = false;
            StopShortcutCapture();
            StopDesktopRobotShortcutCapture();

            await System.Threading.Tasks.Task.Delay(150);

            // Don't resume services here - they should stay paused while in Settings view
            // They will be resumed when the Settings view is unloaded (user leaves Settings)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in EndEditing: {ex.Message}");
        }
    }

    private void DesktopRobotShortcutBox_GotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Console.WriteLine("[SettingsView] Desktop Robot Shortcut Box Got Focus");
        if (!_editingDesktopRobotShortcut)
        {
            _editingDesktopRobotShortcut = true;
            // Save the current shortcut in case user presses Esc
            if (DataContext is SettingsViewModel viewModel)
            {
                _lastDesktopRobotShortcut = viewModel.DesktopRobotShortcut;
            }
            Console.WriteLine("[SettingsView] Starting Desktop Robot Shortcut editing");
            StartDesktopRobotShortcutCapture();
        }
    }

    private void DesktopRobotShortcutBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_editingDesktopRobotShortcut)
        {
            EndEditing();
        }
    }

    private void StartDesktopRobotShortcutCapture()
    {
        try
        {
            Console.WriteLine("Starting Desktop Robot Shortcut Capture");
            var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
            if (app?.DesktopRobotHotkeyService != null)
            {
                Console.WriteLine("DesktopRobotHotkeyService found, starting capture mode");
                app.DesktopRobotHotkeyService.StartCaptureMode(
                    OnDesktopRobotShortcutCaptured,
                    onEscapePressed: RestoreDesktopRobotShortcut,
                    onBackspacePressed: RestoreDesktopRobotDefault
                );
                Console.WriteLine("Capture mode started");
            }
            else
            {
                Console.WriteLine("DesktopRobotHotkeyService is null!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting desktop robot shortcut capture: {ex.Message}");
            EndEditing();
        }
    }

    private void OnDesktopRobotShortcutCaptured(string shortcut)
    {
        try
        {
            Console.WriteLine($"Desktop Robot Shortcut Captured: {shortcut}");

            if (string.IsNullOrWhiteSpace(shortcut))
            {
                Console.WriteLine("Shortcut is empty, ignoring");
                return;
            }

            if (DataContext is SettingsViewModel viewModel)
            {
                Console.WriteLine($"Setting Desktop Robot Shortcut to: {shortcut}");
                viewModel.DesktopRobotShortcut = shortcut;
                Console.WriteLine($"Desktop Robot Shortcut now: {viewModel.DesktopRobotShortcut}");
            }
            else
            {
                Console.WriteLine("DataContext is not SettingsViewModel");
            }

            // Don't refocus - let the LostFocus event handle cleanup naturally
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnDesktopRobotShortcutCaptured: {ex.Message}");
        }
    }

    private void StopDesktopRobotShortcutCapture()
    {
        try
        {
            var app = global::Avalonia.Application.Current as global::CrsterCommand.App;
            if (app?.DesktopRobotHotkeyService != null)
            {
                app.DesktopRobotHotkeyService.StopCaptureMode();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping desktop robot shortcut capture: {ex.Message}");
        }
    }

    private void RestoreScreenCaptureShortcut()
    {
        Console.WriteLine($"Restoring Screen Capture shortcut to: {_lastScreenCaptureShortcut}");
        if (DataContext is SettingsViewModel viewModel && !string.IsNullOrWhiteSpace(_lastScreenCaptureShortcut))
        {
            viewModel.ScreenCaptureShortcut = _lastScreenCaptureShortcut;
        }
    }

    private void RestoreScreenCaptureDefault()
    {
        Console.WriteLine("Restoring Screen Capture to default");
        if (DataContext is SettingsViewModel viewModel)
        {
            var defaultShortcut = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX) ? "Alt+F12" : "PrintScreen";
            viewModel.ScreenCaptureShortcut = defaultShortcut;
        }
    }

    private void RestoreDesktopRobotShortcut()
    {
        Console.WriteLine($"Restoring Desktop Robot shortcut to: {_lastDesktopRobotShortcut}");
        if (DataContext is SettingsViewModel viewModel && !string.IsNullOrWhiteSpace(_lastDesktopRobotShortcut))
        {
            viewModel.DesktopRobotShortcut = _lastDesktopRobotShortcut;
        }
    }

    private void RestoreDesktopRobotDefault()
    {
        Console.WriteLine("Restoring Desktop Robot to default");
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.DesktopRobotShortcut = "Ctrl+Alt+Shift+F12";
        }
    }
}
