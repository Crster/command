using System;

namespace CrsterCommand.Models;

public class AiResponseFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = "application/octet-stream";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AiResponseData
{
    public string TextContent { get; set; } = "";
    public AiResponseFile? FileContent { get; set; }
}
