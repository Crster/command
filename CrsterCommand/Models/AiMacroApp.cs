using System;
using System.Collections.Generic;

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
    // Previously supported multiple attachments; now only a single attachment is stored per macro
    public FileAttachment? Attachment { get; set; }
}

public class FileAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
