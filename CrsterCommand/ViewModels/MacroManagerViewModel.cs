using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Services;
using Avalonia;

namespace CrsterCommand.ViewModels;

public partial class MacroManagerViewModel : ViewModelBase
{
    private readonly MacroService _macroService = new();

    [ObservableProperty]
    private string _generatedPassword = "";

    [ObservableProperty]
    private string _macroStatus = "Select a macro to run";

    [RelayCommand]
    private void GeneratePassword()
    {
        GeneratedPassword = _macroService.GenerateRandomPassword();
        MacroStatus = "New password generated.";
    }

    [RelayCommand]
    private async Task ResetNetwork()
    {
        MacroStatus = "Resetting network... (This may take a moment)";
        var result = await _macroService.ResetNetworkAsync();
        MacroStatus = result;
    }

    [RelayCommand]
    private async Task CopyPassword()
    {
        if (string.IsNullOrEmpty(GeneratedPassword)) return;
        
        var clipboard = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(GeneratedPassword);
            MacroStatus = "Password copied to clipboard.";
        }
    }
}
