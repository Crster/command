using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using Avalonia;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Diagnostics;
using Google.GenAI;

namespace CrsterCommand.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    [ObservableProperty]
    private string _dbPath;

    [ObservableProperty]
    private string? _aiApiKey;

    [ObservableProperty]
    private string? _aiModel;

    [ObservableProperty]
    private string? _vaultPassword;

    private string _screenCaptureShortcut = "PrintScreen";

    public string ScreenCaptureShortcut
    {
        get => _screenCaptureShortcut;
        set
        {
            if (value == "Conflict")
            {
                SetProperty(ref _screenCaptureShortcut, value);
                return;
            }

            if (CheckShortcutConflict(value, _desktopRobotShortcut))
            {
                _screenCaptureShortcut = "Conflict";
                OnPropertyChanged(nameof(ScreenCaptureShortcut));
                return;
            }

            if (SetProperty(ref _screenCaptureShortcut, value))
            {
                _storageService.SetScreenCaptureShortcut(value);
            }
        }
    }

    private string _desktopRobotShortcut = "Ctrl+Alt+Shift+F12";

    public string DesktopRobotShortcut
    {
        get => _desktopRobotShortcut;
        set
        {
            if (value == "Conflict")
            {
                SetProperty(ref _desktopRobotShortcut, value);
                return;
            }

            if (CheckShortcutConflict(value, _screenCaptureShortcut))
            {
                _desktopRobotShortcut = "Conflict";
                OnPropertyChanged(nameof(DesktopRobotShortcut));
                return;
            }

            if (SetProperty(ref _desktopRobotShortcut, value))
            {
                _storageService.SetDesktopRobotShortcut(value);
            }
        }
    }

    private bool CheckShortcutConflict(string shortcut1, string shortcut2)
    {
        if (string.IsNullOrEmpty(shortcut1) || string.IsNullOrEmpty(shortcut2))
            return false;
        if (shortcut1 == "Conflict" || shortcut2 == "Conflict")
            return false;

        return string.Equals(shortcut1, shortcut2, StringComparison.OrdinalIgnoreCase);
    }

    [ObservableProperty]
    private ObservableCollection<string> _aiModelOptions = new();

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private bool _isFfmpegInstalled;

    [ObservableProperty]
    private string _ffmpegVersion = string.Empty;

    [ObservableProperty]
    private bool _isCheckingFfmpeg;

    public bool HasApiKey => !string.IsNullOrEmpty(AiApiKey);

    public SettingsViewModel(StorageService storageService)
    {
        _storageService = storageService;
        _dbPath = _storageService.GetCurrentDbPath();
        _aiApiKey = _storageService.GetAiApiKey();
        _aiModel = _storageService.GetAiModel();
        _vaultPassword = _storageService.GetVaultPassword();
        _screenCaptureShortcut = _storageService.GetScreenCaptureShortcut();
        _desktopRobotShortcut = _storageService.GetDesktopRobotShortcut();

        if (!string.IsNullOrEmpty(_aiApiKey))
        {
            _ = Task.Run(async () => await FetchModelsAsync());
        }

        _ = Task.Run(async () => await CheckFfmpegAsync());
    }

    partial void OnVaultPasswordChanged(string? value)
    {
        _storageService.SetVaultPassword(value);
    }

    partial void OnAiApiKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(HasApiKey));
        _storageService.SetAiApiKey(value);
        if (!string.IsNullOrEmpty(value))
        {
            _ = Task.Run(async () => await FetchModelsAsync());
        }
        else
        {
            AiModelOptions.Clear();
        }
    }

    partial void OnAiModelChanged(string? value)
    {
        if (value != null) _storageService.SetAiModel(value);
    }

    [RelayCommand]
    public async Task FetchModelsAsync()
    {
        if (string.IsNullOrEmpty(AiApiKey)) return;

        IsLoadingModels = true;
        try
        {
            var sdkClient = new Client(apiKey: AiApiKey);
            var pager = await sdkClient.Models.ListAsync();

            var models = new System.Collections.Generic.List<string>();
            await foreach (var m in pager)
            {
                if (m.Name != null)
                    models.Add(m.Name.StartsWith("models/") ? m.Name.Substring(7) : m.Name);
            }
            models.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));

            // Use Dispatcher if necessary, but since this is bound to UI it might need to be on main thread.
            // Avalonia's observable properties usually handle this if updated from non-UI thread if they are bound properly?
            // Actually, better to use Avalonia.Threading.Dispatcher
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AiModelOptions.Clear();
                foreach (var model in models)
                {
                    AiModelOptions.Add(model);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch models: {ex.Message}");
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    [RelayCommand]
    public async Task CheckFfmpegAsync()
    {
        IsCheckingFfmpeg = true;
        try
        {
            var ffmpegPath = await Task.Run(() => ScreenRecorderService.ResolveFfmpegPath());

            if (ffmpegPath == null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsFfmpegInstalled = false;
                    FfmpegVersion = string.Empty;
                });
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();

            // ffmpeg -version writes to stderr
            var stderr = await process.StandardError.ReadLineAsync();
            var stdout = await process.StandardOutput.ReadLineAsync();
            await process.WaitForExitAsync();

            var version = (!string.IsNullOrEmpty(stderr) ? stderr : stdout) ?? string.Empty;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsFfmpegInstalled = true;
                FfmpegVersion = version;
            });
        }
        catch
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsFfmpegInstalled = false;
                FfmpegVersion = string.Empty;
            });
        }
        finally
        {
            IsCheckingFfmpeg = false;
        }
    }

    [RelayCommand]
    private void OpenFfmpegDownload()
    {
        Process.Start(new ProcessStartInfo("https://ffmpeg.org/download.html") { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task BrowsePath()
    {
        var mainWindow = await GetMainWindowAsync();
        if (mainWindow == null) return;

        var storageProvider = mainWindow.StorageProvider;
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
