using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using Avalonia;
using System.Threading.Tasks;

namespace CrsterCommand.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    [ObservableProperty]
    private string _dbPath;

    [ObservableProperty]
    private string? _aiApiKey;

    public string[] AiServiceProviders { get; } = ["Gemini", "Ollama", "OpenAI", "HuggingFace"];

    [ObservableProperty]
    private string? _aiServiceProvider;

    [ObservableProperty]
    private string? _aiModel;

    [ObservableProperty]
    private string? _aiEndPoint;

    public SettingsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _dbPath = _storageService.GetCurrentDbPath();
        _aiApiKey = _storageService.GetAiApiKey();
        _aiServiceProvider = _storageService.GetAiServiceProvider();
        _aiModel = _storageService.GetAiModel();
        _aiEndPoint = _storageService.GetAiEndPoint();
    }

    partial void OnAiApiKeyChanged(string? value) => _storageService.SetAiApiKey(value);
    partial void OnAiServiceProviderChanged(string? value) => _storageService.SetAiServiceProvider(value);
    partial void OnAiModelChanged(string? value) => _storageService.SetAiModel(value);
    partial void OnAiEndPointChanged(string? value) => _storageService.SetAiEndPoint(value);

    [RelayCommand]
    private async Task BrowsePath()
    {
        var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;
        var folder = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Database Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            var newDir = folder[0].Path.LocalPath;
            DbPath = System.IO.Path.Combine(newDir, "toolkit.db");
            _storageService.ChangeDatabasePath(DbPath);
        }
    }
}
