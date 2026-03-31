using System;

namespace CrsterCommand.Models;

public class UserSettings
{
    public string DbPath { get; set; } = "";
    public string? AiApiKey { get; set; }
    public string? AiServiceProvider { get; set; } = "Gemini";
    public string? AiModel { get; set; } = "gemini-2.5-flash-lite";
    public string? AiEndPoint { get; set; }
}
