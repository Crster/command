using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CrsterCommand.Services;

public class ScreenRecorderService
{
    private Process? _ffmpegProcess;
    private string? _currentOutputFile;

    public bool IsRecording => _ffmpegProcess != null && !_ffmpegProcess.HasExited;

    public static string? ResolveFfmpegPath()
    {
        // Use platform-appropriate executable name and PATH separator
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        // Search PATH entries first (respecting platform path separator)
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(dir.Trim());
                var full = Path.Combine(expanded, exeName);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        // Check some common installation locations per platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var candidates = new[] {
                "/usr/local/bin/ffmpeg",
                "/opt/homebrew/bin/ffmpeg",
                "/usr/bin/ffmpeg",
                "/usr/local/sbin/ffmpeg"
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var candidates = new[] {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/snap/bin/ffmpeg",
                "/usr/local/sbin/ffmpeg"
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check common Program Files locations
            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var candidates = new[] {
                    Path.Combine(programFiles, "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(programFilesX86, "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(localAppData, "Programs", "ffmpeg", "bin", "ffmpeg.exe")
                };
                foreach (var c in candidates) if (File.Exists(c)) return c;
            }
            catch { }

            // Fallback: try additional PATHs from registry (only on Windows)
            try
            {
                var userPath = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Environment", "PATH", null) as string ?? string.Empty;
                var machinePath = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "PATH", null) as string ?? string.Empty;

                var allPaths = string.Join(";", pathEnv, machinePath, userPath);
                foreach (var dir in allPaths.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var expanded = Environment.ExpandEnvironmentVariables(dir.Trim());
                        var full = Path.Combine(expanded, "ffmpeg.exe");
                        if (File.Exists(full)) return full;
                    }
                    catch { }
                }
            }
            catch { }
        }

        return null;
    }

    public async Task StartRecordingAsync(string outputPath)
    {
        if (IsRecording) return;

        var ffmpegPath = ResolveFfmpegPath()
            ?? throw new FileNotFoundException("FFmpeg not found. Please install FFmpeg and add it to your system PATH.");

        _currentOutputFile = outputPath;
        var arguments = GetPlatformArguments(outputPath);

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
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
