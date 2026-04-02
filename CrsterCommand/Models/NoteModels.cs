using System;
using System.Collections.Generic;
using System.Linq;

namespace CrsterCommand.Models;

public enum NoteType
{
    Todo,
    Memory,
    Vault,
    File
}

public abstract class BaseNoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public string Description { get; set; } = "";
    public float[]? Embedding { get; set; }
    public abstract NoteType Type { get; }
    public virtual string Summary => "";
    public virtual string DisplayDescription => Description;
    public virtual string GetTextForEmbedding() => Description;
    public abstract string DetailInfo { get; }
}

public class TodoSubTask
{
    public string Todo { get; set; } = "";
    public bool IsDone { get; set; }
}

public class TodoItem : BaseNoteItem
{
    public List<TodoSubTask> Tasks { get; set; } = new();
    public override NoteType Type => NoteType.Todo;
    public override string Summary => Tasks.FirstOrDefault(t => !t.IsDone)?.Todo ?? (Tasks.Any() ? "All tasks completed!" : "Empty Todo List");
    public override string GetTextForEmbedding() => $"{Description} {string.Join(" ", Tasks.Select(t => t.Todo))}".Trim();
    public override string DetailInfo => $"{Tasks.Count(t => t.IsDone)}/{Tasks.Count} done";
}

public class Reminder : BaseNoteItem
{
    public string Message { get; set; } = "";
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
    public override NoteType Type => NoteType.Todo;
    public override string Summary => Message;
    public override string DetailInfo => DueDate.ToString("MMM dd");
}

public class MemoryNote : BaseNoteItem
{
    public string Content { get; set; } = "";
    public override NoteType Type => NoteType.Memory;
    public override string Summary => Content.Length > 50 ? Content.Substring(0, 50) + "..." : Content;
    public override string GetTextForEmbedding() => $"{Description} {Content}".Trim();
    public override string DetailInfo => $"{Content.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length} words";
}

public class VaultItem : BaseNoteItem
{
    public string Label { get; set; } = "";
    public string EncryptedContent { get; set; } = "";
    public override NoteType Type => NoteType.Vault;
    public override string Summary => string.IsNullOrWhiteSpace(Description) ? Label : Description;
    public override string DisplayDescription => "Encrypted Vault Item";
    public override string GetTextForEmbedding() => $"{Description} {Label}".Trim();
    public override string DetailInfo => $"{EncryptedContent.Length} chars"; // Encrypted length is suitable for private info
}

public class FileItem : BaseNoteItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileId { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public override NoteType Type => NoteType.File;
    public override string Summary => FileName;
    public override string GetTextForEmbedding() => $"{Description} {FileName}".Trim();
    public override string DetailInfo => FormatFileSize(FileSize);

    private string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        double size = bytes;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:N1}{units[unitIndex]}";
    }
}

public class AppData
{
    public List<TodoItem> Todos { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<MemoryNote> MemoryBank { get; set; } = new();
    public List<VaultItem> Vault { get; set; } = new();
}
