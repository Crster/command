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
