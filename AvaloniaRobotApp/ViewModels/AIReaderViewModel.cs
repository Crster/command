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
    private readonly AIService _aiService;

    public AIReaderViewModel(StorageService storageService)
    {
        _aiService = new AIService(storageService);
    }

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
        AiResult = "Preparing capture...";
        
        // 1. Hide main window to avoid capturing it
        await SetWindowVisibilityAsync(false);
        await Task.Delay(400); // Short delay to let screen refresh
        
        try
        {
            // 2. Capture FULL screen first
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var screen = mainWindow?.Screens.Primary;
            var width = screen?.Bounds.Width ?? 1920;
            var height = screen?.Bounds.Height ?? 1080;
            var systemBitmap = _imageService.CreateScreenCapture(width, height);

            // 3. Re-show main window BEFORE showing the modal dialog to avoid "non-visible owner" error
            await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
            await SetWindowVisibilityAsync(true);
            await Task.Delay(100); // Brief pause for UI stability

            // 4. Convert to Avalonia Bitmap for the crop overlay
            using var ms = new MemoryStream();
            systemBitmap.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            var fullAvaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(ms);

            // 5. Show the interactive cropping overlay
            var overlay = new Views.AIReaderCropWindow(fullAvaloniaBitmap);
            var resultBitmap = await overlay.ShowDialog<Avalonia.Media.Imaging.Bitmap?>(mainWindow!);

            if (resultBitmap == null)
            {
                AiResult = "Capture cancelled.";
                return;
            }

            LastAnalysisImage = resultBitmap;

            // 6. Convert cropped part to bytes for AI
            using var cropStream = new MemoryStream();
            resultBitmap.Save(cropStream);
            var imageBytes = cropStream.ToArray();

            // 7. Send to AI
            AiResult = "AI is analyzing selection...";
            var result = await _aiService.ExplainImageAsync(imageBytes);
            
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(result);
                if (doc.RootElement.TryGetProperty("result", out var res))
                {
                    if (res.ValueKind == System.Text.Json.JsonValueKind.String)
                        AiResult = res.GetString() ?? "";
                    else
                        AiResult = System.Text.Json.JsonSerializer.Serialize(res, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    AiResult = result;
                }
            }
            catch
            {
                AiResult = result;
            }
        }
        catch (Exception ex)
        {
            AiResult = "Error: " + ex.Message;
        }
        finally
        {
            // Ensure visibility as safety fallback
            await SetWindowVisibilityAsync(true);
            IsProcessing = false;
        }
    }
}
