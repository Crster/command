using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Models;
using CrsterCommand.Services;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace CrsterCommand.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly EmbeddingService _embeddingService = new();

    public ObservableCollection<TodoItem> Todos { get; } = new();
    public ObservableCollection<Reminder> Reminders { get; } = new();
    public ObservableCollection<MemoryNote> MemoryNotes { get; } = new();
    public ObservableCollection<string> AttachedFiles { get; } = new();

    [ObservableProperty]
    private string _newTodoTask = "";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private MemoryNote? _selectedNote;

    public NotesViewModel(StorageService storageService)
    {
        _storageService = storageService;
        LoadAll();
    }

    private void LoadAll()
    {
        Todos.Clear();
        foreach (var item in _storageService.GetTodos().FindAll()) Todos.Add(item);
        
        Reminders.Clear();
        foreach (var item in _storageService.GetReminders().FindAll()) Reminders.Add(item);
        
        MemoryNotes.Clear();
        foreach (var item in _storageService.GetMemoryNotes().FindAll()) MemoryNotes.Add(item);

        AttachedFiles.Clear();
        foreach (var file in _storageService.GetFileStorage().FindAll()) AttachedFiles.Add(file.Id);
    }

    [RelayCommand]
    private async Task SaveNote()
    {
        if (SelectedNote == null) return;

        // Generate embedding if content changed (simplified check: always generate for now)
        if (!string.IsNullOrEmpty(SelectedNote.Content))
        {
            SelectedNote.Embedding = await _embeddingService.GetEmbeddingAsync(SelectedNote.Content);
        }
        
        SelectedNote.LastModified = DateTime.Now;
        _storageService.GetMemoryNotes().Update(SelectedNote);
    }

    [RelayCommand]
    private void AddNote()
    {
        var note = new MemoryNote { Title = "New Note", Content = "" };
        _storageService.GetMemoryNotes().Insert(note);
        MemoryNotes.Add(note);
        SelectedNote = note;
    }

    [RelayCommand]
    private async Task SemanticSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            LoadAll();
            return;
        }

        var queryVector = await _embeddingService.GetEmbeddingAsync(SearchQuery);
        var allNotes = _storageService.GetMemoryNotes().FindAll().ToList();

        var scoredNotes = allNotes
            .Select(n => new { Note = n, Score = n.Embedding != null ? _embeddingService.CalculateSimilarity(queryVector, n.Embedding) : 0 })
            .OrderByDescending(x => x.Score)
            .Where(x => x.Score > 0.3) // Similarity threshold
            .Select(x => x.Note)
            .ToList();

        MemoryNotes.Clear();
        foreach (var note in scoredNotes) MemoryNotes.Add(note);
    }

    [RelayCommand]
    private void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTask)) return;
        
        var todo = new TodoItem { Task = NewTodoTask };
        _storageService.GetTodos().Insert(todo);
        Todos.Add(todo);
        NewTodoTask = "";
    }

    [RelayCommand]
    private void DeleteTodo(TodoItem item)
    {
        _storageService.GetTodos().Delete(item.Id);
        Todos.Remove(item);
    }

    [RelayCommand]
    private async Task UploadFile()
    {
        var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Upload",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var file = files[0];
            using var stream = await file.OpenReadAsync();
            var fileId = file.Name;
            _storageService.GetFileStorage().Upload(fileId, fileId, stream);
            AttachedFiles.Add(fileId);
        }
    }

    [RelayCommand]
    private async Task AttachClipboard()
    {
        var clipboard = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
        if (clipboard == null) return;

        var text = await ((Avalonia.Input.IAsyncDataTransfer)clipboard).TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            var fileId = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
            _storageService.GetFileStorage().Upload(fileId, fileId, ms);
            AttachedFiles.Add(fileId);
        }
    }
}
