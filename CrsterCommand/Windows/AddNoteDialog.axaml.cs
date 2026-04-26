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

    private bool _isMemoryTabVisible = true;
    public bool IsMemoryTabVisible { get => _isMemoryTabVisible; set => SetField(ref _isMemoryTabVisible, value); }

    private bool _isTodoTabVisible = true;
    public bool IsTodoTabVisible { get => _isTodoTabVisible; set => SetField(ref _isTodoTabVisible, value); }

    private bool _isVaultTabVisible = true;
    public bool IsVaultTabVisible { get => _isVaultTabVisible; set => SetField(ref _isVaultTabVisible, value); }

    private bool _isFileTabVisible = true;
    public bool IsFileTabVisible { get => _isFileTabVisible; set => SetField(ref _isFileTabVisible, value); }

    private bool _isGeneratingMemory;
    public bool IsGeneratingMemory { get => _isGeneratingMemory; set => SetField(ref _isGeneratingMemory, value); }

    private bool _isGeneratingTodo;
    public bool IsGeneratingTodo { get => _isGeneratingTodo; set => SetField(ref _isGeneratingTodo, value); }

    private bool _isGeneratingVault;
    public bool IsGeneratingVault { get => _isGeneratingVault; set => SetField(ref _isGeneratingVault, value); }

    private bool _isGeneratingFile;
    public bool IsGeneratingFile { get => _isGeneratingFile; set => SetField(ref _isGeneratingFile, value); }

    private bool _isOptimizing;
    public bool IsOptimizing { get => _isOptimizing; set => SetField(ref _isOptimizing, value); }

    private bool _isSearchVisible;
    public bool IsSearchVisible { get => _isSearchVisible; set => SetField(ref _isSearchVisible, value); }

    private string _searchQuery = "";
    public string SearchQuery { get => _searchQuery; set => SetField(ref _searchQuery, value); }

    private string _searchMatchInfo = "";
    public string SearchMatchInfo { get => _searchMatchInfo; set => SetField(ref _searchMatchInfo, value); }

    private List<int> _searchMatches = new();
    private int _searchMatchIndex = -1;

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
        if (_aiService == null) return;

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
                    // Remove password patterns before submitting to AI
                    string sanitizedContent = PasswordSanitizationService.RemovePasswordPatterns(VaultContent.Text ?? "");
                    tempItem = new VaultItem { EncryptedContent = sanitizedContent };
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

            // Validate content before submitting to AI to save processing time
            if (!IsValidContentForAI(tempItem))
            {
                return;
            }

            SetGenerating(tag, true);
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
            SetGenerating(tag, false);
            tempStream?.Dispose();
        }
    }

    private void SetGenerating(string? tag, bool value)
    {
        switch (tag)
        {
            case "Memory": IsGeneratingMemory = value; break;
            case "Todo":   IsGeneratingTodo   = value; break;
            case "Vault":  IsGeneratingVault  = value; break;
            case "File":   IsGeneratingFile   = value; break;
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
            IsMemoryTabVisible = true; IsTodoTabVisible = false; IsVaultTabVisible = false; IsFileTabVisible = false;
            TabControl.SelectedIndex = 0;
            MemoryContent.Text = memory.Content;
        }
        else if (item is TodoItem todo)
        {
            IsMemoryTabVisible = false; IsTodoTabVisible = true; IsVaultTabVisible = false; IsFileTabVisible = false;
            TabControl.SelectedIndex = 1;
            TodoList.Clear();
            foreach (var task in todo.Tasks) TodoList.Add(task);
        }
        else if (item is VaultItem vault)
        {
            IsMemoryTabVisible = false; IsTodoTabVisible = false; IsVaultTabVisible = true; IsFileTabVisible = false;
            TabControl.SelectedIndex = 2;
            var password = _storageService?.GetVaultPassword();
            if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(vault.EncryptedContent))
            {
                try {
                    var decryptedContent = SecurityService.Decrypt(vault.EncryptedContent, password);
                    VaultContent.Text = decryptedContent;
                    // Store decrypted content for display purposes
                    vault.DecryptedContent = decryptedContent;
                } catch {
                    VaultContent.Text = "[Decryption Failed]";
                }
            }
        }
        else if (item is FileItem file)
        {
            IsMemoryTabVisible = false; IsTodoTabVisible = false; IsVaultTabVisible = false; IsFileTabVisible = true;
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
        e.Handled = true;
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
        if (PlatformImpl is null) { e.Handled = true; return; }

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
        else if (e.Key == Key.F2)
        {
            _ = OptimizeSelectedTextAsync(NewTodoInput);
            e.Handled = true;
        }
    }

    private void MemoryContent_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            var caret = MemoryContent.CaretIndex;
            MemoryContent.Text = (MemoryContent.Text ?? "").Insert(caret, "    ");
            MemoryContent.CaretIndex = caret + 4;
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            var now = DateTime.Now.ToString("g");
            var caret = MemoryContent.CaretIndex;
            MemoryContent.Text = (MemoryContent.Text ?? "").Insert(caret, now);
            MemoryContent.CaretIndex = caret + now.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            _ = PasteFromClipboardAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _ = OptimizeSelectedTextAsync(MemoryContent);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            if (!IsSearchVisible)
            {
                OpenSearch();
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                NavigateSearch(-1);
            }
            else
            {
                NavigateSearch(1);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsSearchVisible)
        {
            CloseSearch();
            e.Handled = true;
        }
    }

    private void OpenSearch()
    {
        IsSearchVisible = true;
        SearchQuery = "";
        _searchMatches.Clear();
        _searchMatchIndex = -1;
        SearchMatchInfo = "";
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SearchBox?.Focus());
    }

    private void CloseSearch()
    {
        IsSearchVisible = false;
        _searchMatches.Clear();
        _searchMatchIndex = -1;
        SearchMatchInfo = "";
        MemoryContent.Focus();
    }

    private void RunSearch(string query)
    {
        _searchMatches.Clear();
        _searchMatchIndex = -1;

        var text = MemoryContent.Text ?? "";
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
        {
            SearchMatchInfo = "";
            return;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        int idx = 0;
        while ((idx = text.IndexOf(query, idx, comparison)) >= 0)
        {
            _searchMatches.Add(idx);
            idx += query.Length;
        }

        if (_searchMatches.Count > 0)
        {
            _searchMatchIndex = 0;
            HighlightMatch();
        }
        else
        {
            SearchMatchInfo = "No results";
        }
    }

    private void NavigateSearch(int direction)
    {
        if (_searchMatches.Count == 0) return;
        _searchMatchIndex = (_searchMatchIndex + direction + _searchMatches.Count) % _searchMatches.Count;
        HighlightMatch();
    }

    private void HighlightMatch()
    {
        if (_searchMatchIndex < 0 || _searchMatchIndex >= _searchMatches.Count) return;
        var start = _searchMatches[_searchMatchIndex];
        var len = SearchQuery.Length;
        SearchMatchInfo = $"{_searchMatchIndex + 1}/{_searchMatches.Count}";
        MemoryContent.SelectionStart = start;
        MemoryContent.SelectionEnd = start + len;
        MemoryContent.CaretIndex = start;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RunSearch(SearchBox?.Text ?? "");
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3 || e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                NavigateSearch(-1);
            else
                NavigateSearch(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearch();
            e.Handled = true;
        }
    }

    private void SearchNext_Click(object? sender, RoutedEventArgs e) => NavigateSearch(1);
    private void SearchPrev_Click(object? sender, RoutedEventArgs e) => NavigateSearch(-1);

    private async Task PasteFromClipboardAsync()
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        using var data = await clipboard.TryGetDataAsync();
        if (data == null) return;

        var caret = MemoryContent.CaretIndex;
        string textToInsert = "";

        // 1. Check for files first
        var storageItems = await data.TryGetFilesAsync();
        if (storageItems != null && storageItems.Any())
        {
            var firstItem = storageItems.First();
            textToInsert = firstItem.Path.LocalPath;
        }
        // 2. Check for images
        else
        {
            var bitmap = await data.TryGetBitmapAsync();
            if (bitmap != null)
            {
                textToInsert = $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}";
            }
            else
            {
                // 3. Check for text
                var textContent = await data.TryGetTextAsync();
                if (!string.IsNullOrWhiteSpace(textContent))
                {
                    // Convert to one-liner: replace escape characters and newlines
                    textToInsert = ConvertToOneLiner(textContent);
                }
            }
        }

        if (!string.IsNullOrEmpty(textToInsert))
        {
            MemoryContent.Text = (MemoryContent.Text ?? "").Insert(caret, textToInsert);
            MemoryContent.CaretIndex = caret + textToInsert.Length;
        }
    }

    private string ConvertToOneLiner(string text)
    {
        // Handle command-line continuation characters (\ and ^)
        // These are used in bash, PowerShell, batch scripts, etc.
        // Remove line continuation backslash followed by newline
        text = Regex.Replace(text, @"\\\s*(\r\n|\r|\n)\s*", " ");
        // Remove line continuation caret (^) used in Windows batch
        text = Regex.Replace(text, @"\^\s*(\r\n|\r|\n)\s*", " ");

        // Replace escaped versions first (from JSON/web content)
        text = text.Replace("\\r\\n", " ");
        text = text.Replace("\\n", " ");
        text = text.Replace("\\r", "");
        text = text.Replace("\\t", " ");
        text = text.Replace("\\f", " ");
        text = text.Replace("\\b", " ");
        text = text.Replace("\\\"", "\"");
        text = text.Replace("\\\\", "\\");
        text = text.Replace("\\/", "/");

        // Replace actual control characters with spaces
        text = text.Replace("\r\n", " ");
        text = text.Replace("\n", " ");
        text = text.Replace("\r", "");
        text = text.Replace("\t", " ");
        text = text.Replace("\f", " ");
        text = text.Replace("\b", " ");

        // Replace common HTML entities
        text = text.Replace("&nbsp;", " ");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&apos;", "'");

        // Clean up multiple consecutive spaces
        while (text.Contains("  "))
        {
            text = text.Replace("  ", " ");
        }

        return text.Trim();
    }


    private async Task OptimizeSelectedTextAsync(TextBox tb)
    {
        if (_aiService == null || IsOptimizing) return;

        var selected = tb.SelectedText;
        if (string.IsNullOrWhiteSpace(selected)) return;

        var selStart = tb.SelectionStart;
        var selEnd = tb.SelectionEnd;
        var start = Math.Min(selStart, selEnd);
        var length = Math.Abs(selEnd - selStart);

        try
        {
            IsOptimizing = true;
            var optimized = await _aiService.OptimizeTextAsync(selected);
            var full = tb.Text ?? "";
            tb.Text = full.Remove(start, length).Insert(start, optimized);
            tb.SelectionStart = start;
            tb.SelectionEnd = start + optimized.Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Optimize text error: {ex.Message}");
        }
        finally
        {
            IsOptimizing = false;
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
        string? vaultPlainTextForEmbedding = null; // Store original vault content for embedding

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
                    // Store original plain text for embedding BEFORE encryption
                    vaultPlainTextForEmbedding = VaultContent.Text;

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
                    // For vault items, use the original plain text content for better semantic search
                    if (itemToProcess.Type == NoteType.Vault && !string.IsNullOrWhiteSpace(vaultPlainTextForEmbedding))
                    {
                        var vault = itemToProcess as VaultItem;
                        if (vault != null)
                        {
                            // Temporarily set the plain text for embedding generation
                            string originalEncrypted = vault.EncryptedContent;
                            vault.EncryptedContent = vaultPlainTextForEmbedding;
                            itemToProcess.Embedding = await _embedding_service_helper(itemToProcess, fileStreamToProcess);
                            // Restore encrypted content
                            vault.EncryptedContent = originalEncrypted;
                        }
                    }
                    else
                    {
                        // Use improved text for embedding (Description + Content)
                        itemToProcess.Embedding = await _embedding_service_helper(itemToProcess, fileStreamToProcess);
                    }
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

    // Validate that content is not empty/null/whitespace before submitting to AI
    private bool IsValidContentForAI(BaseNoteItem? item)
    {
        if (item == null) return false;

        return item switch
        {
            MemoryNote memory => !string.IsNullOrWhiteSpace(memory.Content),
            TodoItem todo => todo.Tasks != null && todo.Tasks.Count > 0,
            VaultItem vault => !string.IsNullOrWhiteSpace(vault.EncryptedContent),
            FileItem file => !string.IsNullOrWhiteSpace(file.FileName) && !string.IsNullOrWhiteSpace(file.FilePath),
            _ => false
        };
    }
}


