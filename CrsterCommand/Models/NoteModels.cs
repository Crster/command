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
    public float[]? Embedding { get; set; }
    public abstract NoteType Type { get; }
    public abstract string Summary { get; }
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
    public override string Summary => $"{Tasks.Count} items ({Tasks.Count(t => t.IsDone)} done)";
}

public class Reminder : BaseNoteItem
{
    public string Message { get; set; } = "";
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
    public override NoteType Type => NoteType.Todo;
    public override string Summary => Message;
}

public class MemoryNote : BaseNoteItem
{
    public string Content { get; set; } = "";
    public override NoteType Type => NoteType.Memory;
    public override string Summary => Content.Length > 50 ? Content.Substring(0, 50) + "..." : Content;
}

public class VaultItem : BaseNoteItem
{
    public string Label { get; set; } = "";
    public string EncryptedContent { get; set; } = "";
    public override NoteType Type => NoteType.Vault;
    public override string Summary => Label;
}

public class FileItem : BaseNoteItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileId { get; set; } = "";
    public string FileType { get; set; } = "";
    public string Description { get; set; } = "";
    public override NoteType Type => NoteType.File;
    public override string Summary => string.IsNullOrEmpty(Description) ? FileName : Description;
}

public class AppData
{
    public List<TodoItem> Todos { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<MemoryNote> MemoryBank { get; set; } = new();
    public List<VaultItem> Vault { get; set; } = new();
}
