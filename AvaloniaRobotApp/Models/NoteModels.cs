using System;
using System.Collections.Generic;

namespace AvaloniaRobotApp.Models;

public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Task { get; set; } = "";
    public bool IsDone { get; set; }
}

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = "";
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
}

public class MemoryNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime LastModified { get; set; } = DateTime.Now;
    public float[]? Embedding { get; set; }
}

public class VaultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string EncryptedContent { get; set; } = "";
}

public class AppData
{
    public List<TodoItem> Todos { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<MemoryNote> MemoryBank { get; set; } = new();
    public List<VaultItem> Vault { get; set; } = new();
}
