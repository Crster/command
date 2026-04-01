using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using System.IO;
using Avalonia.Platform;
using System.Drawing.Imaging;

namespace CrsterCommand.ViewModels;

public partial class ScreenCaptureViewModel : ViewModelBase
{
    private readonly ImageService _imageService = new();

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _capturedImage;

    [ObservableProperty]
    private bool _isCapturing;

    [RelayCommand]
    private async Task StartCaptureAsync()
    {
        IsCapturing = true;
        
        // 1. Hide main window instantly
        await SetWindowVisibilityAsync(false);
        await Task.Delay(400); // Delay for screen refresh
        
        try 
        {
            // 2. Capture entire screen
            var screen = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Screens.Primary;
            var width = screen?.Bounds.Width ?? 1920;
            var height = screen?.Bounds.Height ?? 1080;
            
            var systemBitmap = _imageService.CreateScreenCapture(width, height);
            
            // 3. Show main window back immediately after capture
            await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
            await SetWindowVisibilityAsync(true);
            await Task.Delay(100); // Brief pause to ensure UI is ready
            
            // 4. Convert and show overlay for editing
            using var ms = new MemoryStream();
            systemBitmap.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            
            var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
            
            var overlay = new Views.CaptureOverlayWindow(avaloniaBitmap);
            // Now we can use the re-shown MainWindow as the owner safely
            var result = await overlay.ShowDialog<Avalonia.Media.Imaging.Bitmap?>((Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow!);
            
            if (result != null)
            {
                CapturedImage = result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            // Fallback to ensure visibility
            await SetWindowVisibilityAsync(true);
            IsCapturing = false;
        }
    }
}
