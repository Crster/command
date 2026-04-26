using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Models;
using CrsterCommand.Services;
using Google.GenAI;
using System.Threading;
using SharpHook;
using SharpHook.Data;
using System.IO;

namespace CrsterCommand.ViewModels;

public class MacroManagerViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly ImageService _imageService;
    private readonly AIService _aiService;
    private readonly FileAttachmentService _fileAttachmentService;

    public ObservableCollection<MacroAiAppItem> AiApps { get; } = new();
    public ObservableCollection<string> AiModelOptions { get; } = new();

    public IRelayCommand<MacroAiAppItem?> ToggleExpandCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> SendCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> CopyAnswerCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> DownloadResponseFileCommand { get; }
    public IRelayCommand<MacroAiAppItem?> DeleteAiAppCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> UploadFileCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> RemoveFileCommand { get; }
    public IRelayCommand ToggleRobotCommand { get; }

    private CancellationTokenSource? _robotCts;
    private bool _isRobotRunning;
    private bool _robotStartedFromHotkey;
    public bool IsRobotRunning
    {
        get => _isRobotRunning;
        set => SetProperty(ref _isRobotRunning, value);
    }

    public MacroManagerViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _aiService = new AIService(storageService);
        _imageService = new ImageService();
        _fileAttachmentService = new FileAttachmentService();

        ToggleExpandCommand = new RelayCommand<MacroAiAppItem?>(ToggleExpand);
        SendCommand = new AsyncRelayCommand<MacroAiAppItem?>(SendAsync);
        CopyAnswerCommand = new AsyncRelayCommand<MacroAiAppItem?>(CopyAnswerAsync);
        DownloadResponseFileCommand = new AsyncRelayCommand<MacroAiAppItem?>(DownloadResponseFileAsync);
        DeleteAiAppCommand = new RelayCommand<MacroAiAppItem?>(DeleteAiApp);
        UploadFileCommand = new AsyncRelayCommand<MacroAiAppItem?>(UploadFileAsync);
        RemoveFileCommand = new AsyncRelayCommand<MacroAiAppItem?>(RemoveAttachmentAsync);
        ToggleRobotCommand = new RelayCommand(() => ToggleRobot());

        LoadModelOptions();
        LoadAll();
        // Fetch models immediately on a background thread without awaiting
        _ = Task.Run(() => FetchModelsAsync());
    }

    // Use base ViewModelBase.GetMainWindowAsync to obtain the main window.

    private void ToggleRobot(bool fromHotkey = false)
    {
        if (IsRobotRunning)
        {
            // Stop
            _robotCts?.Cancel();
            IsRobotRunning = false;
            _robotCts = null;
        }
        else
        {
            // Start
            _robotCts = new CancellationTokenSource();
            _robotStartedFromHotkey = fromHotkey;
            _ = Task.Run(() => RunDesktopRobotAsync(_robotCts.Token, fromHotkey));
            IsRobotRunning = true;
        }
    }

    public void RunDesktopRobot(bool fromHotkey = false)
    {
        if (!IsRobotRunning)
        {
            ToggleRobot(fromHotkey);
        }
    }

    private async Task RunDesktopRobotAsync(CancellationToken token, bool fromHotkey = false)
    {
        var rnd = new Random();
        await MinimizeToTrayAsync();
        try
        {
            await Task.Delay(3000, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!fromHotkey)
            {
                await RestoreFromTrayAsync();
            }
            IsRobotRunning = false;
            return;
        }
        
        var screen = this._imageService.GetFullscreenSize();

        var centerX = (int)(screen.Width / 2);
        var centerY = (int)(screen.Height / 2);
        var maxOffsetX = screen.Width * 0.15;
        var maxOffsetY = screen.Height * 0.20;
        var simulator = new EventSimulator();
        var previousPos = _imageService.GetMousePosition();

        try
        {
            simulator.SimulateMousePress((short)previousPos.X, (short)previousPos.Y, SharpHook.Data.MouseButton.Button1);
            simulator.SimulateMouseRelease((short)previousPos.X, (short)previousPos.Y, SharpHook.Data.MouseButton.Button1);

            // Move mouse to center of boundary
            simulator.SimulateMouseMovement((short)centerX, (short)centerY);
            previousPos = _imageService.GetMousePosition();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var actual = _imageService.GetMousePosition();
                    // Check if mouse is outside the allowed boundary box
                    if (actual.X < centerX - maxOffsetX || actual.X > centerX + maxOffsetX ||
                        actual.Y < centerY - maxOffsetY || actual.Y > centerY + maxOffsetY)
                    {
                        break;
                    }
                    int dx = rnd.Next(-(int)maxOffsetX, (int)maxOffsetX + 1);
                    int dy = rnd.Next(-(int)maxOffsetY, (int)maxOffsetY + 1);
                    int targetX = (int)Math.Max(0, Math.Min((int)screen.Width - 1, centerX + dx));
                    int targetY = (int)Math.Max(0, Math.Min((int)screen.Height - 1, centerY + dy));
                    simulator.SimulateMouseMovement((short)targetX, (short)targetY);

                    Task.Delay(rnd.Next(500, 1500), token).Wait(token);

                    if (rnd.NextDouble() < 0.5)
                    {
                        int scrollAmount = GetPlatformScrollAmount(rnd);
                        simulator.SimulateMouseWheel((short)scrollAmount, MouseWheelScrollDirection.Vertical, MouseWheelScrollType.UnitScroll);
                    }
                    previousPos = _imageService.GetMousePosition();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                }
            }

            // Move mouse back to center of boundary when exiting
            simulator.SimulateMouseMovement((short)centerX, (short)centerY);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!fromHotkey)
            {
                await RestoreFromTrayAsync();
            }
            await Dispatcher.UIThread.InvokeAsync(() => IsRobotRunning = false);
        }
    }



    private void LoadModelOptions()
    {
        AiModelOptions.Clear();

        var current = _storageService.GetAiModel() ?? "gemini-2.5-flash";
        AiModelOptions.Add(current);
        if (!AiModelOptions.Contains("gemini-2.5-flash"))
            AiModelOptions.Add("gemini-2.5-flash");
    }

    private async Task FetchModelsAsync()
    {
        var apiKey = _storageService.GetAiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        try
        {
            var client = new Client(apiKey: apiKey);
            var pager = await client.Models.ListAsync();
            var models = new System.Collections.Generic.List<string>();

            await foreach (var model in pager)
            {
                if (!string.IsNullOrWhiteSpace(model.Name))
                    models.Add(model.Name.StartsWith("models/") ? model.Name[7..] : model.Name);
            }

            models = models.Distinct().OrderBy(x => x).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AiModelOptions.Clear();
                foreach (var model in models)
                    AiModelOptions.Add(model);

                if (AiModelOptions.Count == 0)
                    AiModelOptions.Add(_storageService.GetAiModel() ?? "gemini-2.5-flash");
            });
        }
        catch
        {
            // Keep existing fallback options.
        }
    }

    private void LoadAll()
    {
        AiApps.Clear();

        var fallbackModel = _storageService.GetAiModel() ?? "gemini-2.5-flash";
        var items = _storageService
            .GetAiMacroApps()
            .FindAll()
            .OrderByDescending(x => x.LastModified);

        foreach (var item in items)
            AiApps.Add(new MacroAiAppItem(item, fallbackModel, _ => { }));
    }

    public void AddSystemPrompt(string systemPrompt, string? selectedModel = null)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return;

        var modelToUse = selectedModel ?? _storageService.GetAiModel() ?? "gemini-2.5-flash";

        var model = new AiMacroApp
        {
            SystemPrompt = systemPrompt.Trim(),
            LastModified = DateTime.Now,
            Model = modelToUse
        };

        _storageService.GetAiMacroApps().Insert(model);
        AiApps.Insert(0, new MacroAiAppItem(model, model.Model ?? "gemini-2.5-flash", _ => { }));
    }

    private void ToggleExpand(MacroAiAppItem? item)
    {
        if (item is null)
            return;

        var isExpanding = !item.IsExpanded;
        if (isExpanding)
        {
            // Clear session when expanding to a new macro
            ClearMacroSession(item);
        }
        else
        {
            // Clear session when collapsing
            ClearMacroSession(item);
        }

        item.IsExpanded = isExpanding;
    }

    private void ClearMacroSession(MacroAiAppItem item)
    {
        item.UserInput = "";
        item.AiAnswer = "";
        item.ResponseFile = null;
        item.ChatHistory.Clear();
    }

    private async Task SendAsync(MacroAiAppItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.UserInput) || item.IsBusy)
            return;

        item.IsBusy = true;
        item.AiAnswer = "";
        item.ResponseFile = null;

        // Store user message in history
        var userMessage = item.UserInput;
        item.ChatHistory.Add(("user", userMessage));

        // Only allow a single attachment per send: use the single Attachment if present
        var attachmentsList = item.Attachment is not null
            ? new System.Collections.Generic.List<FileAttachment> { item.Attachment }
            : new System.Collections.Generic.List<FileAttachment>();
        var responseData = await _aiService.RunMacroPromptAsync(item.SystemPrompt, item.ChatHistory, item.SelectedModel, attachmentsList);
        item.AiAnswer = responseData.TextContent;
        item.ResponseFile = responseData.FileContent;

        // Store AI response in history (only store actual content, not file download messages)
        if (!string.IsNullOrWhiteSpace(responseData.TextContent) && !responseData.TextContent.StartsWith("📥"))
        {
            item.ChatHistory.Add(("assistant", responseData.TextContent));
        }

        item.UserInput = "";
        item.IsBusy = false;

        var record = _storageService.GetAiMacroApps().FindById(item.Id);
        if (record is null)
            return;

        // Keep result/session ephemeral in UI only.
        record.LastUserInput = "";
        record.LastAiAnswer = "";
        record.Model = item.SelectedModel;
        record.LastModified = DateTime.Now;

        // Clear attached file after sending (only association cleared; file not deleted)
        if (item.Attachment is not null)
            item.Attachment = null;

        if (record.Attachment is not null)
            record.Attachment = null;

        _storageService.GetAiMacroApps().Update(record);
    }

    private async Task UploadFileAsync(MacroAiAppItem? item)
    {
        if (item is null)
            return;

        try
        {
            var mainWindow = await GetMainWindowAsync();
            if (mainWindow == null)
                return;

            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select file to upload (Images, PDF, Audio)",
                AllowMultiple = false
            });

            if (files.Count == 0)
                return;
            var filePath = files[0].Path.LocalPath;
            var attachment = await _fileAttachmentService.AddFileAsync(filePath);

            if (attachment is not null)
            {
                // Only a single attachment is supported now
                item.Attachment = attachment;

                var record = _storageService.GetAiMacroApps().FindById(item.Id);
                if (record is not null)
                {
                    record.Attachment = attachment;
                    record.LastModified = DateTime.Now;
                    _storageService.GetAiMacroApps().Update(record);
                }
            }
        }
        catch
        {
            // Show error to user if needed
        }
    }

    // Remove single attachment from an item
    private async Task RemoveAttachmentAsync(MacroAiAppItem? item)
    {
        if (item is null || item.Attachment is null)
            return;

        var attachment = item.Attachment;
        item.Attachment = null;
        await _fileAttachmentService.RemoveFileAsync(attachment);

        var record = _storageService.GetAiMacroApps().FindById(item.Id);
        if (record is not null)
        {
            record.Attachment = null;
            record.LastModified = DateTime.Now;
            _storageService.GetAiMacroApps().Update(record);
        }
    }

    public async Task AttachFilesFromPasteAsync(MacroAiAppItem item, List<IStorageFile> files)
    {
        if (item is null || files.Count == 0)
            return;

        try
        {
            // Only accept the first file as the single attachment
            var file = files[0];
            var filePath = file.Path.LocalPath;

            var attachment = await _fileAttachmentService.AddFileAsync(filePath);

            if (attachment is not null)
            {
                item.Attachment = attachment;

                var record = _storageService.GetAiMacroApps().FindById(item.Id);
                if (record is not null)
                {
                    record.Attachment = attachment;
                    record.LastModified = DateTime.Now;
                    _storageService.GetAiMacroApps().Update(record);
                }
            }
        }
        catch
        {
            // Silently handle error; files may not be valid
        }
    }

    public async Task AttachImageFromPasteAsync(MacroAiAppItem item, IImage bitmap)
    {
        if (item is null || bitmap is null)
            return;

        try
        {
            // Save bitmap to temporary file
            string tempDir = Path.Combine(Path.GetTempPath(), "CrsterCommand");
            if (!Directory.Exists(tempDir)) 
                Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, $"pasted_image_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            if (bitmap is Bitmap bitmapImpl)
            {
                bitmapImpl.Save(tempPath);

                var attachment = await _fileAttachmentService.AddFileAsync(tempPath);
                if (attachment is not null)
                {
                    item.Attachment = attachment;

                    var record = _storageService.GetAiMacroApps().FindById(item.Id);
                    if (record is not null)
                    {
                        record.Attachment = attachment;
                        record.LastModified = DateTime.Now;
                        _storageService.GetAiMacroApps().Update(record);
                    }
                }
            }
        }
        catch
        {
            // Silently handle error
        }
    }

    private void DeleteAiApp(MacroAiAppItem? item)
    {
        if (item is null)
            return;

        _storageService.GetAiMacroApps().Delete(item.Id);
        AiApps.Remove(item);
    }

    private async Task CopyAnswerAsync(MacroAiAppItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.AiAnswer))
            return;

        var mainWindow = await GetMainWindowAsync();
        if (mainWindow == null)
            return;

        var clipboard = TopLevel.GetTopLevel(mainWindow)?.Clipboard;
        if (clipboard == null)
            return;

        // Try direct API (if available), otherwise fall back to reflection-based invocation
        var setTextMethod = clipboard.GetType().GetMethod("SetTextAsync", new[] { typeof(string) });
        if (setTextMethod != null)
        {
            var task = (System.Threading.Tasks.Task)setTextMethod.Invoke(clipboard, new object[] { item.AiAnswer })!;
            await task;
            return;
        }

        // Try non-async SetText
        var setTextSync = clipboard.GetType().GetMethod("SetText", new[] { typeof(string) });
        if (setTextSync != null)
        {
            setTextSync.Invoke(clipboard, new object[] { item.AiAnswer });
            return;
        }

        // As a last resort, attempt to invoke System.Windows.Forms.Clipboard via reflection on Windows
        try
        {
            if (System.OperatingSystem.IsWindows())
            {
                var clipboardType = System.Type.GetType("System.Windows.Forms.Clipboard, System.Windows.Forms");
                var setText = clipboardType?.GetMethod("SetText", new[] { typeof(string) });
                setText?.Invoke(null, new object[] { item.AiAnswer });
            }
        }
        catch
        {
            // ignore failures; clipboard operation is best-effort
        }
    }

    private async Task DownloadResponseFileAsync(MacroAiAppItem? item)
    {
        if (item is null || item.ResponseFile is null)
            return;

        try
        {
            var mainWindow = await GetMainWindowAsync();
            if (mainWindow == null)
                return;

            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Response File",
                SuggestedFileName = item.ResponseFile.FileName
            });

            if (file != null)
            {
                var filePath = file.Path.LocalPath;
                await System.IO.File.WriteAllBytesAsync(filePath, item.ResponseFile.Data);
            }
        }
        catch
        {
            // Silently handle error
        }
    }

    public void ClearAllSessions()
    {
        foreach (var item in AiApps)
        {
            ClearMacroSession(item);
        }
    }

    public async void CleanupAttachments()
    {
        foreach (var item in AiApps)
        {
            if (item.Attachment is not null)
            {
                // Remove the file from disk
                await _fileAttachmentService.RemoveFileAsync(item.Attachment);
                item.Attachment = null;

                // Clear the attachment from the database record as well
                var record = _storageService.GetAiMacroApps().FindById(item.Id);
                if (record is not null)
                {
                    record.Attachment = null;
                    record.LastModified = DateTime.Now;
                    _storageService.GetAiMacroApps().Update(record);
                }
            }
        }
    }

    private int GetPlatformScrollAmount(Random rnd)
    {
        // Platform-specific scroll amounts based on SharpHook documentation:
        // Windows: multiples of 120 represent one default wheel step
        // macOS: values between -10 and 10 recommended for pixel scrolling
        // Linux: multiples of 100 can be used
        int scrollAmount;

        if (OperatingSystem.IsWindows())
        {
            // Windows: 240 = 2 wheel steps (120 per step)
            scrollAmount = rnd.NextDouble() < 0.5 ? 240 : -240;
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: use smaller values between -10 and 10
            scrollAmount = rnd.NextDouble() < 0.5 ? 8 : -8;
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux: multiples of 100
            scrollAmount = rnd.NextDouble() < 0.5 ? 200 : -200;
        }
        else
        {
            // Fallback for unknown platforms
            scrollAmount = rnd.NextDouble() < 0.5 ? 120 : -120;
        }

        return scrollAmount;
    }
}

public class MacroAiAppItem : ObservableObject
{
    private string _systemPrompt;
    private string _userInput;
    private string _aiAnswer;
    private bool _isExpanded;
    private bool _isBusy;
    private string? _selectedModel;
    private AiResponseFile? _responseFile;
    private readonly Action<MacroAiAppItem> _onModelChanged;

    // Chat history for this macro session
    public List<(string role, string content)> ChatHistory { get; } = new();

    public Guid Id { get; }
    // Single attachment for UI to match model change
    private FileAttachment? _attachment;
    public FileAttachment? Attachment
    {
        get => _attachment;
        set => SetProperty(ref _attachment, value);
    }

    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    public string AiAnswer
    {
        get => _aiAnswer;
        set => SetProperty(ref _aiAnswer, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string? SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public AiResponseFile? ResponseFile
    {
        get => _responseFile;
        set => SetProperty(ref _responseFile, value);
    }

    public MacroAiAppItem(AiMacroApp model, string fallbackModel, Action<MacroAiAppItem> onModelChanged)
    {
        Id = model.Id;
        _systemPrompt = model.SystemPrompt;
        _userInput = "";
        _aiAnswer = "";
        _selectedModel = string.IsNullOrWhiteSpace(model.Model) ? fallbackModel : model.Model;
        _onModelChanged = onModelChanged;

        // Load single attachment from model
        Attachment = model.Attachment;
    }
}
