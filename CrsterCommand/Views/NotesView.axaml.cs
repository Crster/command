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
        if (DataContext is not NotesViewModel vm)
            return;

        var dialog = new AddNoteDialog(vm.StorageService, vm.AIService, vm.EmbeddingService);

        var mainWindow = await vm.GetMainWindowAsync();        
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
        if (item == null) return;

        if (DataContext is not NotesViewModel vm)
            return;

        var dialog = new AddNoteDialog(vm.StorageService, vm.AIService, vm.EmbeddingService);
        dialog.LoadItem(item);

        var mainWindow = await vm.GetMainWindowAsync();
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

    private void MainScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Length == 0 && e.OffsetDelta.Length == 0) return;
        
        var viewer = sender as ScrollViewer;
        if (viewer == null) return;

        // If we are within 100 pixels of the bottom, load more
        if (viewer.Offset.Y + viewer.Viewport.Height >= viewer.Extent.Height - 100)
        {
            var vm = DataContext as NotesViewModel;
            vm?.LoadMoreCommand.Execute(false);
        }
    }
}
