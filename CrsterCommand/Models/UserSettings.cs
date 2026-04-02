using System;

namespace CrsterCommand.Models;

public class UserSettings
{
    public string DbPath { get; set; } = "";
    public string? AiApiKey { get; set; }
    public string? AiModel { get; set; } = "gemini-2.0-flash";
    public string? VaultPassword { get; set; }
}
