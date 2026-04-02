using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CrsterCommand.Models;
using CrsterCommand.Services;

namespace CrsterCommand.Views;

public partial class AddNoteDialog : Window, INotifyPropertyChanged
{
    public BaseNoteItem? Result { get; private set; }
    private string? _selectedFilePath;
    private readonly StorageService? _storageService;
    private readonly AIService? _aiService;
    private readonly EmbeddingService? _embeddingService;

    public ObservableCollection<TodoSubTask> TodoList { get; } = new();

    private bool _hasVaultPassword;
    public bool HasVaultPassword
    {
        get => _hasVaultPassword;
        set => SetField(ref _hasVaultPassword, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetField(ref _isProcessing, value);
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetField(ref _isEditMode, value);
    }

    private string _headerText = "Create New Note";
    public string HeaderText
    {
        get => _headerText;
        set => SetField(ref _headerText, value);
    }

    private string _headerSubtitle = "Select a type and capture your thoughts.";
    public string HeaderSubtitle
    {
        get => _headerSubtitle;
        set => SetField(ref _headerSubtitle, value);
    }

    private string _saveButtonText = "Save Note";
    public string SaveButtonText
    {
        get => _saveButtonText;
        set => SetField(ref _saveButtonText, value);
    }

    public AddNoteDialog() : this(null, null, null) { }

    public AddNoteDialog(StorageService? storageService, AIService? aiService, EmbeddingService? embeddingService)
    {
        _storageService = storageService;
        _aiService = aiService;
        _embeddingService = embeddingService;
        InitializeComponent();
        
        HasVaultPassword = !string.IsNullOrEmpty(_storageService?.GetVaultPassword());
        UpdateSaveButtonText(0);
        DataContext = this;
    }

    private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TabControl != null)
        {
            UpdateSaveButtonText(TabControl.SelectedIndex);
        }
    }

    private void UpdateSaveButtonText(int index)
    {
        string action = IsEditMode ? "Update" : "Save";
        SaveButtonText = index switch
        {
            0 => $"{action} Memory",
            1 => $"{action} Todo",
            2 => $"{action} Vault",
            3 => $"{action} File",
            _ => $"{action} Note"
        };
    }

    public void LoadItem(BaseNoteItem item)
    {
        IsEditMode = true;
        Result = item;
        HeaderText = "Edit Item";
        HeaderSubtitle = "Update your note or memory details.";

        if (item is MemoryNote memory)
        {
            TabControl.SelectedIndex = 0;
            MemoryContent.Text = memory.Content;
        }
        else if (item is TodoItem todo)
        {
            TabControl.SelectedIndex = 1;
            TodoList.Clear();
            foreach (var task in todo.Tasks) TodoList.Add(task);
        }
        else if (item is VaultItem vault)
        {
            TabControl.SelectedIndex = 2;
            var password = _storageService?.GetVaultPassword();
            if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(vault.EncryptedContent))
            {
                try {
                    VaultContent.Text = SecurityService.Decrypt(vault.EncryptedContent, password);
                } catch {
                    VaultContent.Text = "[Decryption Failed]";
                }
            }
        }
        else if (item is FileItem file)
        {
            TabControl.SelectedIndex = 3;
            _selectedFilePath = file.FilePath;
            SelectedFileName.Text = file.FileName;
        }
        
        VaultDescription.Text = item.Description;
        
        UpdateSaveButtonText(TabControl.SelectedIndex);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetPassword_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SetupVaultPassword.Text))
        {
            _storageService?.SetVaultPassword(SetupVaultPassword.Text);
            HasVaultPassword = true;
        }
    }

    private void ChangePasswordInSettings_Click(object? sender, RoutedEventArgs e)
    {
        // This could navigate or just show a message, but for simplicity we keep it hidden or use it to reset.
    }

    private void NewTodo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(NewTodoInput.Text))
        {
            TodoList.Add(new TodoSubTask { Todo = NewTodoInput.Text, IsDone = false });
            NewTodoInput.Text = "";
        }
    }

    private async void SelectFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Add",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var file = files[0];
            _selectedFilePath = file.Path.LocalPath;
            SelectedFileName.Text = file.Name;
        }
    }

    private async void OpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (Result is FileItem file && !string.IsNullOrEmpty(file.FileId))
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "CrsterCommand");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                
                string tempPath = Path.Combine(tempDir, file.FileName);
                using (var stream = File.Create(tempPath))
                {
                    _storageService?.DownloadFile(file.FileId, stream);
                }
                
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private async void SaveFileAs_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null || !(Result is FileItem file) || string.IsNullOrEmpty(file.FileId)) return;

        var pickedFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Copy As",
            DefaultExtension = file.FileType,
            SuggestedFileName = file.FileName
        });

        if (pickedFile != null)
        {
            try
            {
                using (var stream = await pickedFile.OpenWriteAsync())
                {
                    _storageService?.DownloadFile(file.FileId, stream);
                }
            }
            catch { }
        }
    }

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (IsProcessing) return;
        
        int selectedIndex = TabControl.SelectedIndex;
        BaseNoteItem? itemToProcess = null;
        Stream? fileStreamToProcess = null;

        switch (selectedIndex)
        {
            case 0: // Memory
                if (!string.IsNullOrWhiteSpace(MemoryContent.Text))
                {
                    var memory = Result as MemoryNote ?? new MemoryNote();
                    memory.Content = MemoryContent.Text;
                    itemToProcess = memory;
                }
                break;

            case 1: // Todo
                if (TodoList.Any())
                {
                    var todo = Result as TodoItem ?? new TodoItem();
                    todo.Tasks = TodoList.ToList();
                    itemToProcess = todo;
                }
                break;

            case 2: // Vault
                var password = _storageService?.GetVaultPassword();
                if (!string.IsNullOrWhiteSpace(VaultContent.Text) && !string.IsNullOrEmpty(password))
                {
                    string encrypted = SecurityService.Encrypt(VaultContent.Text, password);
                    var vault = Result as VaultItem ?? new VaultItem { Label = "Encrypted Vault Item" };
                    vault.EncryptedContent = encrypted;
                    vault.Description = string.IsNullOrWhiteSpace(VaultDescription.Text) ? "Private information" : VaultDescription.Text;
                    itemToProcess = vault;
                }
                break;

            case 3: // File
                if (!string.IsNullOrWhiteSpace(_selectedFilePath))
                {
                    string? currentFileId = (Result as FileItem)?.FileId;
                    bool isNewSelection = true;
                    
                    if (Result is FileItem oldFile && oldFile.FilePath == _selectedFilePath && !string.IsNullOrEmpty(oldFile.FileId))
                        isNewSelection = false;

                    if (isNewSelection && File.Exists(_selectedFilePath))
                    {
                        if (!string.IsNullOrEmpty(currentFileId)) _storageService?.DeleteFile(currentFileId);
                        currentFileId = Guid.NewGuid().ToString();
                        using (var stream = File.OpenRead(_selectedFilePath))
                        {
                            _storageService?.UploadFile(currentFileId, Path.GetFileName(_selectedFilePath), stream);
                        }
                    }

                    var file = Result as FileItem ?? new FileItem();
                    file.FileName = Path.GetFileName(_selectedFilePath);
                    file.FilePath = _selectedFilePath;
                    file.FileId = currentFileId ?? "";
                    file.FileType = Path.GetExtension(_selectedFilePath);
                    file.FileSize = new FileInfo(_selectedFilePath).Length;
                    
                    itemToProcess = file;
                    if (File.Exists(_selectedFilePath))
                    {
                        fileStreamToProcess = File.OpenRead(_selectedFilePath);
                    }
                }
                break;
        }

        if (itemToProcess != null)
        {
            IsProcessing = true;
            try
            {
                itemToProcess.LastModified = DateTime.Now;
                
                // 1. Generate Description via AI (except Vault which is already set)
                if (itemToProcess.Type != NoteType.Vault && _aiService != null)
                {
                    itemToProcess.Description = await _aiService.GenerateNoteDescriptionAsync(itemToProcess, fileStreamToProcess);
                }

                // 2. Generate Embedding
                if (_embeddingService != null)
                {
                    // Use improved text for embedding (Description + Content)
                    itemToProcess.Embedding = await _embeddingService.GetEmbeddingAsync(itemToProcess.GetTextForEmbedding());
                }

                Result = itemToProcess;
                Close(Result);
            }
            catch (Exception ex)
            {
                // Handle or show error
                Debug.WriteLine($"Error processing note: {ex.Message}");
                IsProcessing = false;
            }
            finally
            {
                fileStreamToProcess?.Dispose();
            }
        }
    }
}
