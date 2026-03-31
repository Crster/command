using System;

namespace AvaloniaRobotApp.Models;

public class UserSettings
{
    public string DbPath { get; set; } = "";
    public string? AiApiKey { get; set; }
}
