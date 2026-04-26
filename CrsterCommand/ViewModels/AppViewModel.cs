using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace CrsterCommand.ViewModels;

public partial class AppViewModel : ObservableObject
{
    private Window? _mainWindow;
    private bool _trayIconVisible;

    public bool TrayIconVisible
    {
        get => _trayIconVisible;
        set => SetProperty(ref _trayIconVisible, value);
    }

    public void AttachMainWindow(Window window)
    {
        _mainWindow = window;
    }

    [RelayCommand]
    private void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        TrayIconVisible = false;
        _mainWindow.Activate();
    }

    public void SetTrayVisible(bool visible) => TrayIconVisible = visible;

    [RelayCommand]
    private async Task Quit()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Close();
        }
    }
}
