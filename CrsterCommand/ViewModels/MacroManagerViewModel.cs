using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Models;
using CrsterCommand.Services;
using Google.GenAI;
using System.Threading;
using Desktop.Robot.Extensions;

namespace CrsterCommand.ViewModels;

public class MacroManagerViewModel : ViewModelBase
{
    private readonly StorageService _storageService;
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
    public bool IsRobotRunning
    {
        get => _isRobotRunning;
        set => SetProperty(ref _isRobotRunning, value);
    }

    public MacroManagerViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _aiService = new AIService(storageService);

        ToggleExpandCommand = new RelayCommand<MacroAiAppItem?>(ToggleExpand);
        SendCommand = new AsyncRelayCommand<MacroAiAppItem?>(SendAsync);
        CopyAnswerCommand = new AsyncRelayCommand<MacroAiAppItem?>(CopyAnswerAsync);
        DeleteAiAppCommand = new RelayCommand<MacroAiAppItem?>(DeleteAiApp);
        ToggleRobotCommand = new RelayCommand(ToggleRobot);

        LoadModelOptions();
        LoadAll();
        _ = FetchModelsAsync();
    }

    private void ToggleRobot()
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
            _ = Task.Run(() => RunDesktopRobotAsync(_robotCts.Token));
            IsRobotRunning = true;
        }
    }

    private async Task RunDesktopRobotAsync(CancellationToken token)
    {
        var rnd = new Random();
        await SetWindowVisibilityAsync(false);
        try
        {
            await Task.Delay(3000, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SetWindowVisibilityAsync(true);
            IsRobotRunning = false;
            return;
        }
        double screenWidth = 1920, screenHeight = 1080;
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                screenWidth = desktop.MainWindow.Bounds.Width;
                screenHeight = desktop.MainWindow.Bounds.Height;
            }
        }
        catch { }
        var diagonal = Math.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);
        var threshold = 0.2 * diagonal;
        var centerX = (int)(screenWidth / 2);
        var centerY = (int)(screenHeight / 2);
        var maxOffsetX = screenWidth * 0.25;
        var maxOffsetY = screenHeight * 0.25;
        var robot = new Desktop.Robot.Robot();
        var previousPos = robot.GetMousePosition();
        try
        {
            robot.Click();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var actual = robot.GetMousePosition();
                    var lastRecorded = previousPos;
                    var dist = Math.Sqrt(Math.Pow(actual.X - lastRecorded.X, 2) + Math.Pow(actual.Y - lastRecorded.Y, 2));
                    if (dist > threshold)
                    {
                        break;
                    }
                    int dx = rnd.Next(-220, 221);
                    int dy = rnd.Next(-160, 161);
                    var targetX = previousPos.X + dx;
                    var targetY = previousPos.Y + dy;
                    targetX = (int)Math.Max(centerX - maxOffsetX, Math.Min(centerX + maxOffsetX, targetX));
                    targetY = (int)Math.Max(centerY - maxOffsetY, Math.Min(centerY + maxOffsetY, targetY));
                    targetX = Math.Max(0, Math.Min((int)screenWidth - 1, targetX));
                    targetY = Math.Max(0, Math.Min((int)screenHeight - 1, targetY));
                    int steps = rnd.Next(16, 32);
                    double stepX = (targetX - previousPos.X) / (double)steps;
                    double stepY = (targetY - previousPos.Y) / (double)steps;
                    for (int i = 1; i <= steps; i++)
                    {
                        double rawX = previousPos.X + stepX * i;
                        double rawY = previousPos.Y + stepY * i;
                        rawX = Math.Max(centerX - maxOffsetX, Math.Min(centerX + maxOffsetX, rawX));
                        rawY = Math.Max(centerY - maxOffsetY, Math.Min(centerY + maxOffsetY, rawY));
                        int glideX = Math.Max(0, Math.Min((int)screenWidth - 1, (int)rawX));
                        int glideY = Math.Max(0, Math.Min((int)screenHeight - 1, (int)rawY));
                        robot.MouseMove(glideX, glideY);
                        await Task.Delay(rnd.Next(4, 14), token).ConfigureAwait(false);
                    }
                    if (rnd.NextDouble() < 0.5)
                    {
                        int scrollAmount;
                        if (rnd.NextDouble() < 0.75)
                        {
                            scrollAmount = rnd.Next(-32, -10);
                        }
                        else
                        {
                            scrollAmount = rnd.Next(8, 10);
                        }
                        robot.MouseScroll(scrollAmount);
                    }
                    previousPos = robot.GetMousePosition();
                    await Task.Delay(rnd.Next(500, 2000), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await SetWindowVisibilityAsync(true);
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

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(item.AiAnswer);
        }
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
