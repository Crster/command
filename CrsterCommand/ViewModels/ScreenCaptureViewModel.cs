using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using Avalonia.Controls;
using Avalonia.Threading;
using CrsterCommand.Windows;

namespace CrsterCommand.ViewModels;

public partial class ScreenCaptureViewModel : ViewModelBase
{
    private readonly ImageService _imageService = new();
    private Action? _onCaptureCompleted;

    [ObservableProperty]
    private Bitmap? _capturedImage;

    [ObservableProperty]
    private bool _isCapturing;

    public void SetOnCaptureCompleted(Action callback)
    {
        _onCaptureCompleted = callback;
    }

    [RelayCommand]
    public async Task StartCaptureAsync()
    {
        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;

        try 
        {
            await SetWindowStateAsync(WindowState.Minimized);
            await Task.Delay(600); // Small delay to let the window minimize before capture

            var systemBitmap = _imageService.CreateScreenCapture();

            var overlay = new CaptureOverlayWindow(_imageService.ToAvaloniaBitmap(systemBitmap));
            var tcs = new TaskCompletionSource<object?>();
            overlay.Closed += (_, _) => tcs.TrySetResult(null);
            overlay.Show();
            await tcs.Task;

            if (overlay.ResultBitmap != null)
            {
                CapturedImage = overlay.ResultBitmap;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screen Capture (Error): {ex.Message}");
        } 
        finally
        {
            await SetWindowStateAsync(WindowState.Normal);

            // Notify that capture is completed and navigate to the Capture view
            _onCaptureCompleted?.Invoke();
        }

        IsCapturing = false;
    }
}
