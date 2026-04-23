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
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var candidates = new[]
            {
                "/usr/local/bin/ffmpeg",
                "/opt/homebrew/bin/ffmpeg",
                "/usr/bin/ffmpeg",
                "/usr/local/sbin/ffmpeg"
            };

            foreach (var c in candidates)
                if (File.Exists(c)) return c;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var candidates = new[]
            {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/snap/bin/ffmpeg",
                "/usr/local/sbin/ffmpeg"
            };

            foreach (var c in candidates)
                if (File.Exists(c)) return c;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var candidates = new[]
                {
                    Path.Combine(programFiles, "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(programFilesX86, "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(localAppData, "Programs", "ffmpeg", "bin", "ffmpeg.exe")
                };

                foreach (var c in candidates)
                    if (File.Exists(c)) return c;
            }
            catch { }

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

    public async Task StartRecordingAsync(string outputPath, string? audioDevice = null)
    {
        if (IsRecording) return;

        var ffmpegPath = ResolveFfmpegPath()
            ?? throw new FileNotFoundException("FFmpeg not found. Please install FFmpeg and add it to your system PATH.");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        _currentOutputFile = outputPath;
        var arguments = GetPlatformArguments(outputPath, audioDevice);

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true
            }
        };

        try
        {
            _ffmpegProcess.Start();
        }
        catch (Exception ex)
        {
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
            throw new InvalidOperationException($"Failed to start FFmpeg: {ex.Message}", ex);
        }
    }

    public async Task StopRecordingAsync()
    {
        if (_ffmpegProcess == null || _ffmpegProcess.HasExited) return;

        try
        {
            // Send 'q' to FFmpeg to stop recording gracefully
            await _ffmpegProcess.StandardInput.WriteAsync("q").ConfigureAwait(false);
            await _ffmpegProcess.StandardInput.FlushAsync().ConfigureAwait(false);
            _ffmpegProcess.StandardInput.Close();

            // Wait for graceful exit (up to 5 seconds)
            var exited = await Task.Run(() => _ffmpegProcess.WaitForExit(5000)).ConfigureAwait(false);

            if (!exited && !_ffmpegProcess.HasExited)
            {
                // Force kill if still running
                _ffmpegProcess.Kill(entireProcessTree: true);
                await _ffmpegProcess.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }
    }

    /// <summary>
    /// Attempts to find the default audio device name asynchronously (non-blocking).
    /// Returns null if not found or on non-Windows platforms.
    /// </summary>
    public static async Task<string?> TryFindDefaultAudioDeviceAsync(string? ffmpegPath = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            ffmpegPath ??= ResolveFfmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath))
                return null;

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-list_devices true -f dshow -i dummy",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            var lines = stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var audioSection = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();

                if (line.Contains("DirectShow audio devices", StringComparison.OrdinalIgnoreCase))
                {
                    audioSection = true;
                    continue;
                }

                if (audioSection)
                {
                    var firstQuote = line.IndexOf('"');
                    var lastQuote = line.LastIndexOf('"');
                    if (firstQuote >= 0 && lastQuote > firstQuote)
                    {
                        var name = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        if (!string.IsNullOrWhiteSpace(name))
                            return name;
                    }

                    if (line.Contains("DirectShow video devices", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }
        }
        catch
        {
            // Silently fail - mic detection is optional
        }

        return null;
    }

    private string GetPlatformArguments(string outputPath, string? audioDevice = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: gdigrab for screen capture
            if (!string.IsNullOrWhiteSpace(audioDevice))
            {
                return $"-y -f gdigrab -framerate 30 -i desktop -f dshow -i audio=\"{audioDevice}\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac -b:a 128k \"{outputPath}\"";
            }
            return $"-y -f gdigrab -framerate 30 -i desktop -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p \"{outputPath}\"";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: avfoundation for screen capture (1:0 = screen + default audio)
            return $"-y -f avfoundation -framerate 30 -i \"1:0\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac \"{outputPath}\"";
        }

        // Linux: x11grab for screen, pulse for audio
        if (!string.IsNullOrWhiteSpace(audioDevice))
        {
            return $"-y -f x11grab -framerate 30 -i :0.0 -f pulse -i {audioDevice} -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac -b:a 128k \"{outputPath}\"";
        }

        return $"-y -f x11grab -framerate 30 -i :0.0 -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p \"{outputPath}\"";
    }
}
