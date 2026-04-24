using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Models;
using CrsterCommand.Services;
using Google.GenAI;
using System.Threading;
using SharpHook;
using SharpHook.Data;

namespace CrsterCommand.ViewModels;

public class MacroManagerViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
    private readonly ImageService _imageService;
    private readonly AIService _aiService;

    public ObservableCollection<MacroAiAppItem> AiApps { get; } = new();
    public ObservableCollection<string> AiModelOptions { get; } = new();

    public IRelayCommand<MacroAiAppItem?> ToggleExpandCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> SendCommand { get; }
    public IAsyncRelayCommand<MacroAiAppItem?> CopyAnswerCommand { get; }
    public IRelayCommand<MacroAiAppItem?> DeleteAiAppCommand { get; }
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

        ToggleExpandCommand = new RelayCommand<MacroAiAppItem?>(ToggleExpand);
        SendCommand = new AsyncRelayCommand<MacroAiAppItem?>(SendAsync);
        CopyAnswerCommand = new AsyncRelayCommand<MacroAiAppItem?>(CopyAnswerAsync);
        DeleteAiAppCommand = new RelayCommand<MacroAiAppItem?>(DeleteAiApp);
        ToggleRobotCommand = new RelayCommand(() => ToggleRobot());

        LoadModelOptions();
        LoadAll();
        _ = FetchModelsAsync();
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
            AiApps.Add(new MacroAiAppItem(item, fallbackModel, SaveModelOverride));
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
        AiApps.Insert(0, new MacroAiAppItem(model, model.Model ?? "gemini-2.5-flash", SaveModelOverride));
    }

    private void ToggleExpand(MacroAiAppItem? item)
    {
        if (item is null)
            return;

        var isExpanding = !item.IsExpanded;
        if (isExpanding)
        {
            item.UserInput = "";
            item.AiAnswer = "";
        }

        item.IsExpanded = isExpanding;
    }

    private async Task SendAsync(MacroAiAppItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.UserInput) || item.IsBusy)
            return;

        item.IsBusy = true;
        var answer = await _aiService.RunMacroPromptAsync(item.SystemPrompt, item.UserInput, item.SelectedModel);
        item.AiAnswer = answer;
        item.IsBusy = false;

        var record = _storageService.GetAiMacroApps().FindById(item.Id);
        if (record is null)
            return;

        // Keep result/session ephemeral in UI only.
        record.LastUserInput = "";
        record.LastAiAnswer = "";
        record.Model = item.SelectedModel;
        record.LastModified = DateTime.Now;
        _storageService.GetAiMacroApps().Update(record);
    }

    private void SaveModelOverride(MacroAiAppItem item)
    {
        if (string.IsNullOrWhiteSpace(item.SelectedModel))
            return;

        var record = _storageService.GetAiMacroApps().FindById(item.Id);
        if (record is null)
            return;

        record.Model = item.SelectedModel;
        record.LastModified = DateTime.Now;
        _storageService.GetAiMacroApps().Update(record);
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
    private readonly Action<MacroAiAppItem> _onModelChanged;

    public Guid Id { get; }

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
        set
        {
            if (SetProperty(ref _selectedModel, value))
                _onModelChanged(this);
        }
    }

    public MacroAiAppItem(AiMacroApp model, string fallbackModel, Action<MacroAiAppItem> onModelChanged)
    {
        Id = model.Id;
        _systemPrompt = model.SystemPrompt;
        _userInput = "";
        _aiAnswer = "";
        _selectedModel = string.IsNullOrWhiteSpace(model.Model) ? fallbackModel : model.Model;
        _onModelChanged = onModelChanged;
    }
}
