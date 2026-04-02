using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
        var dialog = new AddNoteDialog();
        var lifetime = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var mainWindow = lifetime?.MainWindow;
        
        if (mainWindow == null) return;

        var result = await dialog.ShowDialog<BaseNoteItem>(mainWindow);
        if (result == null) return;

        var vm = DataContext as NotesViewModel;
        if (vm == null) return;

        if (result is TodoItem todo)
            await vm.AddTodoCommand.ExecuteAsync(todo.Task);
        else if (result is MemoryNote memory)
            await vm.AddMemoryCommand.ExecuteAsync(memory);
        else if (result is VaultItem vault)
            await vm.AddVaultCommand.ExecuteAsync(vault);
        else if (result is FileItem file)
            await vm.AddFileCommand.ExecuteAsync(file);
    }
}
