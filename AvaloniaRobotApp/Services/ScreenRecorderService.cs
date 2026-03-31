using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AvaloniaRobotApp.Services;

public class ScreenRecorderService
{
    private Process? _ffmpegProcess;
    private string? _currentOutputFile;

    public bool IsRecording => _ffmpegProcess != null && !_ffmpegProcess.HasExited;

    public async Task StartRecordingAsync(string outputPath)
    {
        if (IsRecording) return;

        _currentOutputFile = outputPath;
        var arguments = GetPlatformArguments(outputPath);

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true // Used to send 'q' to stop
            }
        };

        await Task.Run(() => _ffmpegProcess.Start());
    }

    public async Task StopRecordingAsync()
    {
        if (_ffmpegProcess == null || _ffmpegProcess.HasExited) return;

        // Send 'q' to FFmpeg to stop recording gracefully
        await _ffmpegProcess.StandardInput.WriteAsync("q");
        await _ffmpegProcess.WaitForExitAsync();
        
        _ffmpegProcess.Dispose();
        _ffmpegProcess = null;
    }

    private string GetPlatformArguments(string outputPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: gdigrab for screen, dshow for audio (using default mic if possible)
            // Note: On Windows, detecting the exact mic name is complex via CLI, 
            // but we can try 'audio="virtual-audio-capturer"' or similar if installed, 
            // otherwise we might need user input. For now, capturing screen only 
            // as primary, audio added via -f dshow -i audio="Microphone..." if known.
            return $"-f gdigrab -framerate 30 -i desktop -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: avfoundation
            return $"-f avfoundation -i \"1:0\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        }
        else
        {
            // Linux: x11grab and pulse
            return $"-f x11grab -framerate 30 -i :0.0 -f pulse -i default -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
        }
    }
}
