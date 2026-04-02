using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CrsterCommand.Models;

namespace CrsterCommand.Views;

public partial class AddNoteDialog : Window
{
    public BaseNoteItem? Result { get; private set; }
    private string? _selectedFilePath;

    public AddNoteDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SelectFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Add",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var file = files[0];
            _selectedFilePath = file.Path.LocalPath;
            SelectedFileName.Text = file.Name;
        }
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        var tabControl = this.FindControl<TabControl>("TabControl");
        // Simplified way to get active tab content since TabControl doesn't natively expose SelectedIndex as an int easily in code-behind without casting
        // Or I can just check the active tab by finding which input is visible or has data.
        
        // Let's use simple logic based on what's filled
        if (!string.IsNullOrWhiteSpace(TodoTask.Text))
        {
            Result = new TodoItem { Task = TodoTask.Text, LastModified = DateTime.Now };
        }
        else if (!string.IsNullOrWhiteSpace(MemoryTitle.Text) || !string.IsNullOrWhiteSpace(MemoryContent.Text))
        {
            Result = new MemoryNote { Title = MemoryTitle.Text ?? "", Content = MemoryContent.Text ?? "", LastModified = DateTime.Now };
        }
        else if (!string.IsNullOrWhiteSpace(VaultLabel.Text) || !string.IsNullOrWhiteSpace(VaultContent.Text))
        {
            Result = new VaultItem { Label = VaultLabel.Text ?? "", EncryptedContent = VaultContent.Text ?? "", LastModified = DateTime.Now };
        }
        else if (!string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            Result = new FileItem { FileName = Path.GetFileName(_selectedFilePath), FilePath = _selectedFilePath, LastModified = DateTime.Now };
        }

        if (Result != null)
        {
            Close(Result);
        }
    }
}
