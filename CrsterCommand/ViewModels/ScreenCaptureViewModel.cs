using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using Avalonia.Controls;

namespace CrsterCommand.ViewModels;

public partial class ScreenCaptureViewModel : ViewModelBase
{
    private readonly ImageService _imageService = new();

    [ObservableProperty]
    private Bitmap? _capturedImage;

    [ObservableProperty]
    private bool _isCapturing;

    [RelayCommand]
    private async Task StartCaptureAsync()
    {
        IsCapturing = true;

        try 
        {
            var mainWindow = await GetMainWindowAsync();
            if (mainWindow == null)
            {
                IsCapturing = false;
                return;
            }

            await SetWindowStateAsync(WindowState.Minimized);
            await Task.Delay(400); // Delay for screen refresh

            var systemBitmap = _imageService.CreateScreenCapture();
            
            var overlay = new Views.CaptureOverlayWindow(_imageService.ToAvaloniaBitmap(systemBitmap));
            var result = await overlay.ShowDialog<Bitmap>(mainWindow);
            
            if (result != null)
            {
                CapturedImage = result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screen Capture (Error): {ex.Message}");
        }
        finally
        {
            await SetWindowStateAsync(WindowState.Normal);
            IsCapturing = false;
        }
    }
}
