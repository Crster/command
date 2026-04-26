using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace CrsterCommand.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected async Task ShowDebugAsync(string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok);
        await box.ShowAsync();
    }

    protected async Task SetWindowStateAsync(WindowState state)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = state;
                    if (state == WindowState.Normal)
                    {
                        if (!desktop.MainWindow.IsVisible)
                            desktop.MainWindow.Show();
                        desktop.MainWindow.Activate();
                    }
                }
            }
        });
    }

    protected async Task SetWindowVisibilityAsync(bool visible)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.IsVisible = visible;
                }
            }
        });
    }

    internal async Task MinimizeToTrayAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.WindowState = WindowState.Minimized;
                    desktop.MainWindow.ShowInTaskbar = false;
                    desktop.MainWindow.IsVisible = false;

                    // Set tray icon visible through AppViewModel
                    if (Application.Current is App app && app.DataContext is AppViewModel appViewModel)
                    {
                        appViewModel.SetTrayVisible(true);
                    }
                }
            }
        });
    }

    protected async Task RestoreFromTrayAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.IsVisible = true;
                    desktop.MainWindow.ShowInTaskbar = true;
                    desktop.MainWindow.WindowState = WindowState.Normal;
                    desktop.MainWindow.Activate();

                    // Hide tray icon through AppViewModel
                    if (Application.Current is App app && app.DataContext is AppViewModel appViewModel)
                    {
                        appViewModel.SetTrayVisible(false);
                    }
                }
            }
        });
    }

    public async Task<Window?> GetMainWindowAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        });
    }
}
