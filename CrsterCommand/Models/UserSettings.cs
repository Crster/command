using System;
using System.Runtime.InteropServices;

namespace CrsterCommand.Models;

public class UserSettings
{
     public string DbPath { get; set; } = "";
     public string? AiApiKey { get; set; }
     public string? AiModel { get; set; } = "gemini-2.5-flash";
     public string? VaultPassword { get; set; }
     public string ScreenCaptureShortcut { get; set; } = GetDefaultScreenCaptureShortcut();
     public string DesktopRobotShortcut { get; set; } = GetDefaultDesktopRobotShortcut();
     public bool StartOnStartup { get; set; } = false;
     public bool StartHidden { get; set; } = true;

     private static string GetDefaultScreenCaptureShortcut()
     {
         return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Alt+F12" : "PrintScreen";
     }

     private static string GetDefaultDesktopRobotShortcut()
     {
         return "Ctrl+Alt+Shift+F12";
     }
 }
