using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CrsterCommand.Models;
using CrsterCommand.ViewModels;

namespace CrsterCommand.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
    }

    public async void OpenAddDialogCommand()
    {
        var vm = DataContext as NotesViewModel;
        if (vm == null) return;

        var dialog = new AddNoteDialog(vm.StorageService, vm.AIService, vm.EmbeddingService);
        var lifetime = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var mainWindow = lifetime?.MainWindow;
        
        if (mainWindow == null) return;

        var result = await dialog.ShowDialog<BaseNoteItem>(mainWindow);
        if (result == null) return;

        if (result is TodoItem todo)
            await vm.AddTodoCommand.ExecuteAsync(todo);
        else if (result is MemoryNote memory)
            await vm.AddMemoryCommand.ExecuteAsync(memory);
        else if (result is VaultItem vault)
            await vm.AddVaultCommand.ExecuteAsync(vault);
        else if (result is FileItem file)
            await vm.AddFileCommand.ExecuteAsync(file);
    }

    public async void OpenEditDialogCommand(BaseNoteItem item)
    {
        var vm = DataContext as NotesViewModel;
        if (vm == null || item == null) return;

        var dialog = new AddNoteDialog(vm.StorageService, vm.AIService, vm.EmbeddingService);
        dialog.LoadItem(item);
        
        var lifetime = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var mainWindow = lifetime?.MainWindow;
        
        if (mainWindow == null) return;

        var result = await dialog.ShowDialog<BaseNoteItem>(mainWindow);
        if (result != null)
        {
            await vm.UpdateItemCommand.ExecuteAsync(result);
        }
    }

    private void NotesList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var vm = DataContext as NotesViewModel;
        if (vm != null && vm.SelectedNote != null)
        {
            OpenEditDialogCommand(vm.SelectedNote);
        }
    }
}
