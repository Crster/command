using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CrsterCommand.ViewModels;
using Avalonia.Markup.Xaml;
using CrsterCommand.Windows;

namespace CrsterCommand.Views
{
    public partial class MacroManagerView : UserControl
    {
        public MacroManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async void OpenAddDialogCommand()
        {
            if (DataContext is not MacroManagerViewModel vm)
                return;

            var mainWindow = await vm.GetMainWindowAsync();
            if (mainWindow == null)
                return;

            var dialog = new AddAiAppDialog(vm.AiModelOptions);
            var result = await dialog.ShowDialog<(string SystemPrompt, string Model)?>(mainWindow);

            if (result.HasValue && !string.IsNullOrWhiteSpace(result.Value.SystemPrompt))
                vm.AddSystemPrompt(result.Value.SystemPrompt, result.Value.Model);
        }
    }
}
