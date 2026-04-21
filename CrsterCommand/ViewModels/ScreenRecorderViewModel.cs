using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using CrsterCommand.Views;
using CrsterCommand.Windows;

namespace CrsterCommand.ViewModels;

public partial class ScreenRecorderViewModel : ViewModelBase
{
    private readonly ScreenRecorderService _recorderService = new();
    private RecordingOverlayWindow? _overlayWindow;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _statusMessage = "Checking FFmpeg...";

    [ObservableProperty]
    private bool _isFfmpegAvailable;

    [ObservableProperty]
    private string? _savedFolderPath;

    public bool HasSavedFile => SavedFolderPath != null;

    public ScreenRecorderViewModel()
    {
        CheckFfmpeg();
    }

    private void CheckFfmpeg()
    {
        var path = ScreenRecorderService.ResolveFfmpegPath();
        IsFfmpegAvailable = path != null;
        StatusMessage = IsFfmpegAvailable
            ? "Ready to record."
            : "FFmpeg is not installed. Please install FFmpeg and add it to your system PATH.";
    }

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
                await ShowOverlayAsync();
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
                await HideOverlayAsync();
                await _recorderService.StopRecordingAsync();
                StatusMessage = "Recording saved to Videos folder.";
                SavedFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                OnPropertyChanged(nameof(HasSavedFile));
            }
            finally
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
                IsRecording = false;
            }
        }
    }

    private async Task ShowOverlayAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow = new RecordingOverlayWindow(async () =>
            {
                // Stop recording from the overlay button
                await ToggleRecording();
            });
            _overlayWindow.Show();
            _overlayWindow.StartTimer();
        });
    }

    private async Task HideOverlayAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.StopTimer();
                _overlayWindow.Close();
                _overlayWindow = null;
            }
        });
    }

    [RelayCommand]
    private void OpenSavedFolder()
    {
        if (SavedFolderPath == null) return;
        Process.Start(new ProcessStartInfo(SavedFolderPath) { UseShellExecute = true });
    }
}
