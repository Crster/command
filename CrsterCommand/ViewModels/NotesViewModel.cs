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
    public readonly AIService AIService;
    public readonly EmbeddingService EmbeddingService;
    
    private List<BaseNoteItem> _filteredList = new();
    private const int PageSize = 10;
    private int _loadedCount = 0;
    private System.Threading.CancellationTokenSource? _searchCts;

    public ObservableCollection<BaseNoteItem> AllNotes { get; } = new();
    
    [ObservableProperty]
    private string _searchQuery = "";

    partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token);
                if (!token.IsCancellationRequested)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(SemanticSearch);
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    [ObservableProperty]
    private BaseNoteItem? _selectedNote;

    public NotesViewModel(StorageService storageService)
    {
        StorageService = storageService;
        AIService = new AIService(storageService);
        EmbeddingService = new EmbeddingService();
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

        _filteredList = combined.OrderByDescending(i => i.LastModified).ToList();
        LoadMore(true);
    }

    [RelayCommand]
    public void LoadMore(bool reset = false)
    {
        if (reset)
        {
            AllNotes.Clear();
            _loadedCount = 0;
        }

        var nextItems = _filteredList.Skip(_loadedCount).Take(PageSize).ToList();
        foreach (var item in nextItems)
        {
            AllNotes.Add(item);
        }
        _loadedCount += nextItems.Count;
    }

    [RelayCommand]
    private async Task SemanticSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            LoadAll();
            return;
        }

        var queryVector = await EmbeddingService.GetEmbeddingAsync(SearchQuery);
        
        var todos = StorageService.GetTodos().FindAll();
        var notes = StorageService.GetMemoryNotes().FindAll();
        var vault = StorageService.GetVaultItems().FindAll();
        var files = StorageService.GetFileItems().FindAll();
 
        var allItems = new List<BaseNoteItem>();
        allItems.AddRange(todos);
        allItems.AddRange(notes);
        allItems.AddRange(vault);
        allItems.AddRange(files);
 
        _filteredList = allItems
            .Select(n => new { Item = n, Score = n.Embedding != null ? EmbeddingService.CalculateSimilarity(queryVector, n.Embedding) : 0 })
            .OrderByDescending(x => x.Score)
            .Where(x => x.Score > 0.1) // Slightly lower threshold for better recall
            .Select(x => x.Item)
            .ToList();

        LoadMore(true);
    }

    [RelayCommand]
    private async Task AddTodo(TodoItem todo)
    {
        StorageService.GetTodos().Insert(todo);
        AllNotes.Insert(0, todo);
    }

    [RelayCommand]
    private async Task AddMemory(MemoryNote note)
    {
        StorageService.GetMemoryNotes().Insert(note);
        AllNotes.Insert(0, note);
    }

    [RelayCommand]
    private async Task AddVault(VaultItem vault)
    {
        StorageService.GetVaultItems().Insert(vault);
        AllNotes.Insert(0, vault);
    }

    [RelayCommand]
    private async Task AddFile(FileItem file)
    {
        StorageService.GetFileItems().Insert(file);
        AllNotes.Insert(0, file);
    }

    [RelayCommand]
    public async Task UpdateItem(BaseNoteItem item)
    {
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
