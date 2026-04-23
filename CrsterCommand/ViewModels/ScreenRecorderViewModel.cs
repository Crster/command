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

    [ObservableProperty]
    private string? _audioDeviceName = "Microphone Array (Realtek(R) Audio)";

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
            var fileName = $"Screen_{DateTime.Now:yyyyMM_dd_HHmmss}.mp4";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), fileName);

            try
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Minimized);
                await Task.Delay(500);

                // Show overlay with loading state first
                await ShowOverlayAsync();

                // Now detect audio and start recording
                await DetectAndStartRecordingAsync(path);
            }
            catch (Exception ex)
            {
                await SetWindowStateAsync(Avalonia.Controls.WindowState.Normal);
                var message = ex.Message.Replace(Environment.NewLine, " ").Trim();
                StatusMessage = $"FFmpeg error: {message}";
                if (_overlayWindow != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => _overlayWindow.Close());
                }
            }
        }
        else
        {
            try
            {
                await _recorderService.StopRecordingAsync();
                await HideOverlayAsync();
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

    private async Task DetectAndStartRecordingAsync(string path)
    {
        // Update overlay with audio detection message
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.ShowLoading("Detecting audio...");
        });

        // Try to detect audio device (non-blocking, can take a moment)
        var detectedDevice = await ScreenRecorderService.TryFindDefaultAudioDeviceAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(detectedDevice))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AudioDeviceName = detectedDevice;
            });
        }

        // Update overlay with starting message
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.ShowLoading("Starting recording...");
        });

        // Start recording
        await _recorderService.StartRecordingAsync(path, AudioDeviceName).ConfigureAwait(false);

        // Hide loading and show recording controls
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.HideLoading();
            _overlayWindow?.StartTimer();
        });

        IsRecording = true;
        StatusMessage = $"Recording to: {Path.GetFileName(path)}";
    }

    private async Task ShowOverlayAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow = new RecordingOverlayWindow(() =>
            {
                _ = ToggleRecording();
            });
            _overlayWindow.Show();
            _overlayWindow.Activate();
            _overlayWindow.ShowLoading("Starting...");
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
