using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;

namespace CrsterCommand.ViewModels;

public partial class ScreenRecorderViewModel : ViewModelBase
{
    private readonly ScreenRecorderService _recorderService = new();

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _statusMessage = "Ready to record.";

    [RelayCommand]
    private async Task ToggleRecording()
    {
        if (!IsRecording)
        {
            var fileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), fileName);
            
            try
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Minimized);
                await Task.Delay(500); // Let animation finish
                
                await _recorderService.StartRecordingAsync(path);
                IsRecording = true;
                StatusMessage = $"Recording to: {fileName}";
            }
            catch (Exception ex)
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
                StatusMessage = $"Error: {ex.Message}. Make sure FFmpeg is installed.";
            }
        }
        else
        {
            try
            {
                await _recorderService.StopRecordingAsync();
                StatusMessage = "Recording saved to Videos folder.";
            }
            finally
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
                IsRecording = false;
            }
        }
    }
}
