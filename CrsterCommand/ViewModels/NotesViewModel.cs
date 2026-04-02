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

    public ObservableCollection<BaseNoteItem> AllNotes { get; } = new();
    
    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private BaseNoteItem? _selectedNote;

    public NotesViewModel(StorageService storageService)
    {
        _storageService = storageService;
        LoadAll();
    }

    private void LoadAll()
    {
        AllNotes.Clear();
        
        var todos = _storageService.GetTodos().FindAll();
        var notes = _storageService.GetMemoryNotes().FindAll();
        var vault = _storageService.GetVaultItems().FindAll();
        var files = _storageService.GetFileItems().FindAll();

        var combined = new List<BaseNoteItem>();
        combined.AddRange(todos);
        combined.AddRange(notes);
        combined.AddRange(vault);
        combined.AddRange(files);

        // Sort by LastModified descending
        foreach (var item in combined.OrderByDescending(i => i.LastModified))
        {
            AllNotes.Add(item);
        }
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
        
        var todos = _storageService.GetTodos().FindAll();
        var notes = _storageService.GetMemoryNotes().FindAll();
        var vault = _storageService.GetVaultItems().FindAll();
        var files = _storageService.GetFileItems().FindAll();

        var allItems = new List<BaseNoteItem>();
        allItems.AddRange(todos);
        allItems.AddRange(notes);
        allItems.AddRange(vault);
        allItems.AddRange(files);

        var scoredItems = allItems
            .Select(n => new { Item = n, Score = n.Embedding != null ? _embeddingService.CalculateSimilarity(queryVector, n.Embedding) : 0 })
            .OrderByDescending(x => x.Score)
            .Where(x => x.Score > 0.2) // Lower threshold for broader search
            .Select(x => x.Item)
            .ToList();

        AllNotes.Clear();
        foreach (var item in scoredItems) AllNotes.Add(item);
    }

    [RelayCommand]
    private async Task AddTodo(string task)
    {
        var todo = new TodoItem { Task = task, LastModified = DateTime.Now };
        todo.Embedding = await _embeddingService.GetEmbeddingAsync(todo.Summary);
        _storageService.GetTodos().Insert(todo);
        AllNotes.Insert(0, todo);
    }

    [RelayCommand]
    private async Task AddMemory(MemoryNote note)
    {
        note.LastModified = DateTime.Now;
        note.Embedding = await _embeddingService.GetEmbeddingAsync($"{note.Title} {note.Content}");
        _storageService.GetMemoryNotes().Insert(note);
        AllNotes.Insert(0, note);
    }

    [RelayCommand]
    private async Task AddVault(VaultItem vault)
    {
        vault.LastModified = DateTime.Now;
        vault.Embedding = await _embeddingService.GetEmbeddingAsync(vault.Label);
        _storageService.GetVaultItems().Insert(vault);
        AllNotes.Insert(0, vault);
    }

    [RelayCommand]
    private async Task AddFile(FileItem file)
    {
        file.LastModified = DateTime.Now;
        file.Embedding = await _embeddingService.GetEmbeddingAsync(file.FileName);
        _storageService.GetFileItems().Insert(file);
        AllNotes.Insert(0, file);
    }

    [RelayCommand]
    private void DeleteItem(BaseNoteItem item)
    {
        switch (item.Type)
        {
            case NoteType.Todo: _storageService.GetTodos().Delete(item.Id); break;
            case NoteType.Memory: _storageService.GetMemoryNotes().Delete(item.Id); break;
            case NoteType.Vault: _storageService.GetVaultItems().Delete(item.Id); break;
            case NoteType.File: _storageService.GetFileItems().Delete(item.Id); break;
        }
        AllNotes.Remove(item);
    }
}
