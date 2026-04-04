using System;

namespace CrsterCommand.Models;

public class AiMacroApp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public string SystemPrompt { get; set; } = "";
    public string LastUserInput { get; set; } = "";
    public string LastAiAnswer { get; set; } = "";
    public string? Model { get; set; }
}
