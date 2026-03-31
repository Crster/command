using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaRobotApp.Services;
using System.Drawing.Imaging;

namespace AvaloniaRobotApp.ViewModels;

public partial class AIReaderViewModel : ViewModelBase
{
    private readonly ImageService _imageService = new();
    private readonly AIService _aiService = new();

    [ObservableProperty]
    private string _aiResult = "Capture the screen to see AI analysis.";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private Bitmap? _lastAnalysisImage;

    [RelayCommand]
    private async Task AnalyzeScreenAsync()
    {
        IsProcessing = true;
        AiResult = "Capturing screen...";
        
        // Hide main window to avoid capturing it
        await SetWindowVisibilityAsync(false);
        await Task.Delay(300); // Short delay to let screen refresh
        
        try
        {
            // 1. Capture screen
            var screen = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Screens.Primary;
            var width = screen?.Bounds.Width ?? 1920;
            var height = screen?.Bounds.Height ?? 1080;
            
            var systemBitmap = _imageService.CreateScreenCapture(width, height);
            
            // 2. Convert to bytes for AI
            using var ms = new MemoryStream();
            systemBitmap.Save(ms, ImageFormat.Png);
            var imageBytes = ms.ToArray();

            // 3. Update UI preview
            ms.Seek(0, SeekOrigin.Begin);
            LastAnalysisImage = new Bitmap(ms);

            // 4. Send to AI
            AiResult = "AI is analyzing...";
            var result = await _aiService.ExplainImageAsync(imageBytes);
            AiResult = result;
        }
        catch (Exception ex)
        {
            AiResult = "Error: " + ex.Message;
        }
        finally
        {
            await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
            await SetWindowVisibilityAsync(true);
            IsProcessing = false;
        }
    }
}
