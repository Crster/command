using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CrsterCommand.Models;
using CrsterCommand.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace CrsterCommand.Windows;

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

        // Use tunneling to intercept Ctrl+V before focused controls handle it
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
    }

    private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TabControl != null)
        {
            UpdateSaveButtonText(TabControl.SelectedIndex);
        }
    }

    private async void GenerateDescription_Click(object? sender, RoutedEventArgs e)
    {
        if (_aiService == null || IsProcessing) return;

        var btn = sender as Avalonia.Controls.Button;
        var tag = btn?.Tag?.ToString();

        BaseNoteItem? tempItem = null;
        Stream? tempStream = null;

        try
        {
            switch (tag)
            {
                case "Memory":
                    tempItem = new MemoryNote { Content = MemoryContent.Text ?? "" };
                    break;
                case "Todo":
                    tempItem = new TodoItem { Tasks = TodoList.ToList() };
                    break;
                case "Vault":
                    tempItem = new VaultItem { EncryptedContent = VaultContent.Text ?? "" };
                    break;
                case "File":
                    if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
                    {
                        tempItem = new FileItem { FileName = Path.GetFileName(_selectedFilePath), FilePath = _selectedFilePath, FileType = Path.GetExtension(_selectedFilePath) };
                        tempStream = File.OpenRead(_selectedFilePath);
                    }
                    else if (Result is FileItem existingFile && File.Exists(existingFile.FilePath))
                    {
                        tempItem = existingFile;
                        tempStream = File.OpenRead(existingFile.FilePath);
                    }
                    break;
            }

            if (tempItem == null) return;

            IsProcessing = true;
            var desc = await _aiService.GenerateNoteDescriptionAsync(tempItem, tempStream);

            switch (tag)
            {
                case "Memory":
                    MemoryDescription.Text = desc;
                    break;
                case "Todo":
                    TodoDescription.Text = desc;
                    break;
                case "Vault":
                    VaultDescription.Text = desc;
                    break;
                case "File":
                    FileDescription.Text = desc;
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI generate description error: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            tempStream?.Dispose();
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

        // Populate description fields for each tab when editing
        MemoryDescription.Text = item.Description;
        TodoDescription.Text = item.Description;
        VaultDescription.Text = item.Description;
        FileDescription.Text = item.Description;
        
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

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
        {
            // Only intercept if we are not in a text input
            if (FocusManager?.GetFocusedElement() is TextBox)
                return;

            if (await ProcessClipboardPaste())
            {
                e.Handled = true;
            }
        }
    }

    private async Task<bool> ProcessClipboardPaste()
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return false;

        // One call to get the data transfer object
        using var data = await clipboard.TryGetDataAsync();
        if (data == null) return false;

        // 1. Files
        var storageItems = await data.TryGetFilesAsync();
        if (storageItems != null && storageItems.Any())
        {
            var firstItem = storageItems.First();
            _selectedFilePath = firstItem.Path.LocalPath;
            SelectedFileName.Text = firstItem.Name;
            TabControl.SelectedIndex = 3; // File Tab
            return true;
        }

        // 2. Bitmap (Screenshots)
        var bitmap = await data.TryGetBitmapAsync();
        if (bitmap != null)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "CrsterCommand");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                string tempPath = Path.Combine(tempDir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bitmap.Save(tempPath);

                _selectedFilePath = tempPath;
                SelectedFileName.Text = Path.GetFileName(tempPath);
                TabControl.SelectedIndex = 3; // File Tab
                return true;
            }
            catch { }
        }

        // 3. Text
        var textContent = await data.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(textContent)) return false;

            // Todo List Pattern
            var lines = textContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                  .Select(l => l.Trim())
                                  .Where(l => !string.IsNullOrEmpty(l))
                                  .ToList();

            bool isTodo = lines.Count > 1 && lines.All(l => l.StartsWith("-") || l.StartsWith("*") || l.StartsWith(">") || l.StartsWith("[]"));
            if (!isTodo && (textContent.Contains("\r\n-") || textContent.Contains("\n-"))) isTodo = true;

            if (isTodo)
            {
                TabControl.SelectedIndex = 1; // Todo Tab
                foreach (var line in lines)
                {
                    string cleanLine = line.TrimStart('-', '*', '>', '[', ']', ' ').Trim();
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                    {
                        if (!TodoList.Any(t => t.Todo == cleanLine))
                            TodoList.Add(new TodoSubTask { Todo = cleanLine, IsDone = false });
                    }
                }
                return true;
            }

            // Vault Pattern
            if (textContent.Length < 256)
            {
                bool hasEmail = Regex.IsMatch(textContent, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
                bool hasPotentialSecret = Regex.IsMatch(textContent, @"[a-zA-Z0-9!@#$%^&*()_+]{8,}");

                if (hasEmail && hasPotentialSecret)
                {
                    TabControl.SelectedIndex = 2; // Vault Tab
                    VaultContent.Text = textContent;
                    return true;
                }
            }

            // Fallback: Memory
            if (TabControl.SelectedIndex != 0) TabControl.SelectedIndex = 0;
            MemoryContent.Text = (MemoryContent.Text ?? "") + textContent;
            MemoryContent.CaretIndex = MemoryContent.Text.Length;
            return true;
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
            // Apply user-provided descriptions from the UI first
            try
            {
                itemToProcess.LastModified = DateTime.Now;

                string? uiDescription = TabControl.SelectedIndex switch
                {
                    0 => MemoryDescription.Text,
                    1 => TodoDescription.Text,
                    2 => VaultDescription.Text,
                    3 => FileDescription.Text,
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(uiDescription))
                {
                    itemToProcess.Description = uiDescription.Trim();
                }

                // 1. Generate Description via AI (only when editing and no user-provided description exists)
                if (itemToProcess.Type != NoteType.Vault && _aiService != null && IsEditMode && string.IsNullOrWhiteSpace(itemToProcess.Description))
                {
                    IsProcessing = true;
                    try
                    {
                        itemToProcess.Description = await _aiService.GenerateNoteDescriptionAsync(itemToProcess, fileStreamToProcess);
                    }
                    finally
                    {
                        IsProcessing = false;
                    }
                }

                // 2. Generate Embedding
                if (_embeddingService != null)
                {
                    // Use improved text for embedding (Description + Content)
                    itemToProcess.Embedding = await _embedding_service_helper(itemToProcess, fileStreamToProcess);
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

    // Helper to safely get embeddings while ensuring any temporary processing state is handled
    private async Task<float[]?> _embedding_service_helper(BaseNoteItem item, Stream? fileStream)
    {
        try
        {
            IsProcessing = true;
            return await _embeddingService!.GetEmbeddingAsync(item.GetTextForEmbedding());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Embedding error: {ex.Message}");
            return null;
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
