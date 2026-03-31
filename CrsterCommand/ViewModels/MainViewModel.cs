using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using CrsterCommand.Services;
using FluentIcons.Common;

namespace CrsterCommand.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly StorageService _storageService = new();

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private NavigationItem? _selectedListItem;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public MainViewModel()
    {
        NavigationItems.Add(new NavigationItem("Dashboard", Symbol.Home, new DashboardViewModel()));
        NavigationItems.Add(new NavigationItem("Capture", Symbol.Camera, new ScreenCaptureViewModel()));
        NavigationItems.Add(new NavigationItem("Recorder", Symbol.Video, new ScreenRecorderViewModel()));
        NavigationItems.Add(new NavigationItem("Notes", Symbol.Notebook, new NotesViewModel(_storageService)));
        NavigationItems.Add(new NavigationItem("AI Reader", Symbol.Brain, new AIReaderViewModel(_storageService)));
        NavigationItems.Add(new NavigationItem("Macros", Symbol.Flash, new MacroManagerViewModel()));
        NavigationItems.Add(new NavigationItem("Settings", Symbol.Settings, new SettingsViewModel(_storageService)));

        SelectedListItem = NavigationItems[0];
        CurrentPage = SelectedListItem.ViewModel;
    }

    partial void OnSelectedListItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            CurrentPage = value.ViewModel;
        }
    }
}

public record NavigationItem(string Name, Symbol Icon, ViewModelBase ViewModel);
