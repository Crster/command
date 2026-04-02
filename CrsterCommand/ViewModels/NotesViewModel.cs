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
    public readonly StorageService StorageService;
    private readonly EmbeddingService _embeddingService = new();

    public ObservableCollection<BaseNoteItem> AllNotes { get; } = new();
    
    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private BaseNoteItem? _selectedNote;

    public NotesViewModel(StorageService storageService)
    {
        StorageService = storageService;
        LoadAll();
    }

    private void LoadAll()
    {
        AllNotes.Clear();
        
        var todos = StorageService.GetTodos().FindAll();
        var notes = StorageService.GetMemoryNotes().FindAll();
        var vault = StorageService.GetVaultItems().FindAll();
        var files = StorageService.GetFileItems().FindAll();

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
        
        var todos = StorageService.GetTodos().FindAll();
        var notes = StorageService.GetMemoryNotes().FindAll();
        var vault = StorageService.GetVaultItems().FindAll();
        var files = StorageService.GetFileItems().FindAll();

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
    private async Task AddTodo(TodoItem todo)
    {
        todo.LastModified = DateTime.Now;
        todo.Embedding = await _embeddingService.GetEmbeddingAsync(todo.Summary);
        StorageService.GetTodos().Insert(todo);
        AllNotes.Insert(0, todo);
    }

    [RelayCommand]
    private async Task AddMemory(MemoryNote note)
    {
        note.LastModified = DateTime.Now;
        note.Embedding = await _embeddingService.GetEmbeddingAsync($"{note.Content}");
        StorageService.GetMemoryNotes().Insert(note);
        AllNotes.Insert(0, note);
    }

    [RelayCommand]
    private async Task AddVault(VaultItem vault)
    {
        vault.LastModified = DateTime.Now;
        vault.Embedding = await _embeddingService.GetEmbeddingAsync(vault.Label);
        StorageService.GetVaultItems().Insert(vault);
        AllNotes.Insert(0, vault);
    }

    [RelayCommand]
    private async Task AddFile(FileItem file)
    {
        file.LastModified = DateTime.Now;
        file.Embedding = await _embeddingService.GetEmbeddingAsync(file.FileName);
        StorageService.GetFileItems().Insert(file);
        AllNotes.Insert(0, file);
    }

    [RelayCommand]
    public async Task UpdateItem(BaseNoteItem item)
    {
        if (item is MemoryNote memory)
            item.Embedding = await _embeddingService.GetEmbeddingAsync(memory.Content);
        else if (item is TodoItem todo)
            item.Embedding = await _embeddingService.GetEmbeddingAsync(todo.Summary);
        else if (item is VaultItem vault)
            item.Embedding = await _embeddingService.GetEmbeddingAsync(vault.Label);
        else if (item is FileItem file)
             item.Embedding = await _embeddingService.GetEmbeddingAsync(file.FileName);

        switch (item.Type)
        {
            case NoteType.Todo: StorageService.GetTodos().Update((TodoItem)item); break;
            case NoteType.Memory: StorageService.GetMemoryNotes().Update((MemoryNote)item); break;
            case NoteType.Vault: StorageService.GetVaultItems().Update((VaultItem)item); break;
            case NoteType.File: StorageService.GetFileItems().Update((FileItem)item); break;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
            LoadAll();
        else
            await SemanticSearch();
    }

    [RelayCommand]
    private void DeleteItem(BaseNoteItem item)
    {
        switch (item.Type)
        {
            case NoteType.Todo: StorageService.GetTodos().Delete(item.Id); break;
            case NoteType.Memory: StorageService.GetMemoryNotes().Delete(item.Id); break;
            case NoteType.Vault: StorageService.GetVaultItems().Delete(item.Id); break;
            case NoteType.File: 
                var fileItem = (FileItem)item;
                if (!string.IsNullOrEmpty(fileItem.FileId)) StorageService.DeleteFile(fileItem.FileId);
                StorageService.GetFileItems().Delete(item.Id); 
                break;
        }
        AllNotes.Remove(item);
    }
}
