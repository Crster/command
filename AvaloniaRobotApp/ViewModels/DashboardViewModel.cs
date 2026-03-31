using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Robot;
using Desktop.Robot.Extensions;

namespace AvaloniaRobotApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IRobot _robot = new Robot();
    private IManagedNotificationManager? _notificationManager;

    [ObservableProperty]
    private int _mouseX = 100;

    [ObservableProperty]
    private int _mouseY = 100;

    [ObservableProperty]
    private string _textToType = "Hello from Avalonia Robot!";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public void SetNotificationManager(IManagedNotificationManager manager)
    {
        _notificationManager = manager;
    }

    [RelayCommand]
    private async Task MoveMouseAsync()
    {
        StatusMessage = $"Moving mouse to ({MouseX}, {MouseY})...";
        _robot.MouseMove(MouseX, MouseY);
        ShowNotification("Mouse Moved", $"Cursor moved to {MouseX}, {MouseY}");
        await Task.Delay(500);
        StatusMessage = "Ready";
    }

    [RelayCommand]
    private async Task ClickLeftAsync()
    {
        StatusMessage = "Performing left click...";
        _robot.Click();
        ShowNotification("Click performed", "Left click executed.");
        await Task.Delay(500);
        StatusMessage = "Ready";
    }

    [RelayCommand]
    private async Task TypeTextAsync()
    {
        if (string.IsNullOrWhiteSpace(TextToType)) return;
        
        StatusMessage = $"Typing: {TextToType}...";
        // Give user time to focus on target field if needed
        await Task.Delay(2000); 
        _robot.Type(TextToType);
        ShowNotification("Text Typed", "The robot has finished typing.");
        StatusMessage = "Ready";
    }

    private void ShowNotification(string title, string message)
    {
        _notificationManager?.Show(new Avalonia.Controls.Notifications.Notification(title, message, NotificationType.Information));
    }
}
