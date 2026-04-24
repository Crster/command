using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using CrsterCommand.Services;
using FluentIcons.Common;

namespace CrsterCommand.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    public ScreenCaptureViewModel ScreenCaptureViewModel { get; }
    public MacroManagerViewModel MacroManagerViewModel { get; }

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private NavigationItem? _selectedListItem;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public MainViewModel()
        : this(new StorageService())
    {
    }

    public MainViewModel(StorageService storageService)
    {
        _storageService = storageService;
        ScreenCaptureViewModel = new ScreenCaptureViewModel();
        MacroManagerViewModel = new MacroManagerViewModel(_storageService);

        NavigationItems.Add(new NavigationItem("Notes", Symbol.Notebook, new NotesViewModel(_storageService)));
        NavigationItems.Add(new NavigationItem("Capture", Symbol.Camera, ScreenCaptureViewModel));
        NavigationItems.Add(new NavigationItem("Recorder", Symbol.Video, new ScreenRecorderViewModel()));
        NavigationItems.Add(new NavigationItem("Macros", Symbol.Flash, MacroManagerViewModel));
        NavigationItems.Add(new NavigationItem("Settings", Symbol.Settings, new SettingsViewModel(_storageService)));

        // Register callback to navigate to Capture view when screen capture completes
        ScreenCaptureViewModel.SetOnCaptureCompleted(() =>
        {
            var captureItem = NavigationItems[1]; // Capture is at index 1
            SelectedListItem = captureItem;
        });

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
