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

    public MacroManagerViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _aiService = new AIService(storageService);

        ToggleExpandCommand = new RelayCommand<MacroAiAppItem?>(ToggleExpand);
        SendCommand = new AsyncRelayCommand<MacroAiAppItem?>(SendAsync);
        CopyAnswerCommand = new AsyncRelayCommand<MacroAiAppItem?>(CopyAnswerAsync);
        DeleteAiAppCommand = new RelayCommand<MacroAiAppItem?>(DeleteAiApp);

        LoadModelOptions();
        LoadAll();
        _ = FetchModelsAsync();
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
