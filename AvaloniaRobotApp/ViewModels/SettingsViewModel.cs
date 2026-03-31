using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaRobotApp.Services;
using Avalonia;
using System.Threading.Tasks;

namespace AvaloniaRobotApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    [ObservableProperty]
    private string _dbPath;

    [ObservableProperty]
    private string? _aiApiKey;

    public SettingsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _dbPath = _storageService.GetCurrentDbPath();
        _aiApiKey = _storageService.GetAiApiKey();
    }

    partial void OnAiApiKeyChanged(string? value)
    {
        _storageService.SetAiApiKey(value);
    }

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
