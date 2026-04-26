using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CrsterCommand.Services;

public class StartupService
{
    private const string AppName = "CrsterCommand";
    private const string HiddenFlag = "--startup-hidden";

    public static string GetExecutablePath()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    public static string GetExecutableFullPath()
    {
        var path = GetExecutablePath();
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // Running as a DLL (development), find the executable
            path = Path.Combine(Path.GetDirectoryName(path)!, "CrsterCommand.exe");
        }
        return path;
    }

    public static bool IsStartupEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsWindowsStartupEnabled();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsLinuxStartupEnabled();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsMacStartupEnabled();
        }

        return false;
    }

    public static bool EnableStartup()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return EnableWindowsStartup();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return EnableLinuxStartup();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return EnableMacStartup();
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error enabling startup: {ex.Message}");
            return false;
        }
    }

    public static bool DisableStartup()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DisableWindowsStartup();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return DisableLinuxStartup();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return DisableMacStartup();
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error disabling startup: {ex.Message}");
            return false;
        }
    }

    #region Windows Implementation

    private static bool IsWindowsStartupEnabled()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                return key?.GetValue(AppName) != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool EnableWindowsStartup()
    {
        try
        {
            var exePath = GetExecutableFullPath();
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    key.SetValue(AppName, $"\"{exePath}\" {HiddenFlag}");
                    Console.WriteLine($"[StartupService] Windows startup entry created: {AppName}");
                    return true;
                }
                else
                {
                    Console.WriteLine("[StartupService] Failed to open registry key for writing. Check permissions for HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                }
            }

            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied accessing Windows registry: {ex.Message}. " +
                            "Try running the app with administrator privileges.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error enabling Windows startup: {ex.Message}");
            return false;
        }
    }

    private static bool DisableWindowsStartup()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    key.DeleteValue(AppName, false);
                    Console.WriteLine($"[StartupService] Windows startup entry deleted: {AppName}");
                    return true;
                }
                else
                {
                    Console.WriteLine("[StartupService] Failed to open registry key for writing. Check permissions for HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                }
            }

            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied accessing Windows registry: {ex.Message}. " +
                            "Try running the app with administrator privileges.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error disabling Windows startup: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Linux Implementation

    private static bool IsLinuxStartupEnabled()
    {
        try
        {
            var desktopFile = GetLinuxDesktopFilePath();
            return File.Exists(desktopFile);
        }
        catch
        {
            return false;
        }
    }

    private static bool EnableLinuxStartup()
    {
        try
        {
            var autoStartDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "autostart");

            if (!Directory.Exists(autoStartDir))
            {
                Directory.CreateDirectory(autoStartDir);
            }

            var desktopFile = GetLinuxDesktopFilePath();
            var exePath = GetExecutableFullPath();

            var content = $"""
                [Desktop Entry]
                Type=Application
                Exec={exePath} {HiddenFlag}
                Hidden=false
                NoDisplay=false
                X-GNOME-Autostart-enabled=true
                Name=CrsterCommand
                Comment=CrsterCommand Startup
                """;

            File.WriteAllText(desktopFile, content);
            Console.WriteLine($"[StartupService] Linux startup file created: {desktopFile}");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied creating startup file: {ex.Message}. " +
                            "Check that you have write permissions to ~/.config/autostart/");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error enabling Linux startup: {ex.Message}");
            return false;
        }
    }

    private static bool DisableLinuxStartup()
    {
        try
        {
            var desktopFile = GetLinuxDesktopFilePath();
            if (File.Exists(desktopFile))
            {
                File.Delete(desktopFile);
                Console.WriteLine($"[StartupService] Linux startup file deleted: {desktopFile}");
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied deleting startup file: {ex.Message}. " +
                            "Check that you have write permissions to ~/.config/autostart/");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error disabling Linux startup: {ex.Message}");
            return false;
        }
    }

    private static string GetLinuxDesktopFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "autostart",
            "crstercommand.desktop");
    }

    #endregion

    #region macOS Implementation

    private static bool IsMacStartupEnabled()
    {
        try
        {
            var plistFile = GetMacPlistPath();
            return File.Exists(plistFile);
        }
        catch
        {
            return false;
        }
    }

    private static bool EnableMacStartup()
    {
        try
        {
            var launchAgentsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "LaunchAgents");

            if (!Directory.Exists(launchAgentsDir))
            {
                Directory.CreateDirectory(launchAgentsDir);
            }

            var plistFile = GetMacPlistPath();
            var exePath = GetExecutableFullPath();

            var plistContent = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>com.crster.crstercommand</string>
                    <key>Program</key>
                    <string>{exePath}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exePath}</string>
                        <string>{HiddenFlag}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>StandardOutPath</key>
                    <string>/dev/null</string>
                    <key>StandardErrorPath</key>
                    <string>/dev/null</string>
                </dict>
                </plist>
                """;

            File.WriteAllText(plistFile, plistContent);

            // Change permissions to 600 (rw-------)
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/chmod",
                        Arguments = $"600 \"{plistFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                proc.WaitForExit();
            }
            catch { }

            Console.WriteLine($"[StartupService] macOS startup plist created: {plistFile}");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied creating plist file: {ex.Message}. " +
                            "Check that you have write permissions to ~/Library/LaunchAgents/");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error enabling macOS startup: {ex.Message}");
            return false;
        }
    }

    private static bool DisableMacStartup()
    {
        try
        {
            var plistFile = GetMacPlistPath();
            if (File.Exists(plistFile))
            {
                File.Delete(plistFile);
                Console.WriteLine($"[StartupService] macOS startup plist deleted: {plistFile}");
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[StartupService] Permission denied deleting plist file: {ex.Message}. " +
                            "Check that you have write permissions to ~/Library/LaunchAgents/");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Error disabling macOS startup: {ex.Message}");
            return false;
        }
    }

    private static string GetMacPlistPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "LaunchAgents",
            "com.crster.crstercommand.plist");
    }

    #endregion
}
