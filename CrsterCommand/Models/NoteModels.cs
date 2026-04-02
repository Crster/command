using System;
using System.Collections.Generic;

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

public class TodoItem : BaseNoteItem
{
    public string Task { get; set; } = "";
    public bool IsDone { get; set; }
    public override NoteType Type => NoteType.Todo;
    public override string Summary => Task;
}

public class Reminder : BaseNoteItem
{
    public string Message { get; set; } = "";
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
    public override NoteType Type => NoteType.Todo; // Mapping reminders to Todo for now
    public override string Summary => Message;
}

public class MemoryNote : BaseNoteItem
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public override NoteType Type => NoteType.Memory;
    public override string Summary => string.IsNullOrEmpty(Title) ? (Content.Length > 50 ? Content.Substring(0, 50) + "..." : Content) : Title;
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
    public string FileType { get; set; } = "";
    public override NoteType Type => NoteType.File;
    public override string Summary => FileName;
}

public class AppData
{
    public List<TodoItem> Todos { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<MemoryNote> MemoryBank { get; set; } = new();
    public List<VaultItem> Vault { get; set; } = new();
}
