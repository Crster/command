using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;

namespace CrsterCommand.Views;

public partial class AddAiAppDialog : Window
{
    public (string SystemPrompt, string Model)? Result { get; private set; }
    public ObservableCollection<string> AiModelOptions { get; } = new();

    public AddAiAppDialog(ObservableCollection<string> modelOptions)
    {
        InitializeComponent();

        foreach (var model in modelOptions)
            AiModelOptions.Add(model);

        if (AiModelOptions.Count == 0)
            AiModelOptions.Add("gemini-2.5-flash");

        DataContext = this;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        var prompt = SystemPromptInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        var selectedModel = ModelSelector.SelectedItem as string ?? AiModelOptions[0];
        Result = (prompt, selectedModel);
        Close(Result);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
