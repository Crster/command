using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;

namespace CrsterCommand.Windows;

public partial class AddAiAppDialog : Window
{
    public (string SystemPrompt, string Model)? Result { get; private set; }
    public ObservableCollection<string> AiModelOptions { get; set; }

    public AddAiAppDialog() : this(new ObservableCollection<string>())
    {
    }

    public AddAiAppDialog(ObservableCollection<string> modelOptions)
    {
        InitializeComponent();

        // Bind directly to the ViewModel's collection instead of copying it
        // This way, when models are fetched asynchronously, they appear in real-time
        AiModelOptions = modelOptions ?? new ObservableCollection<string>();

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
