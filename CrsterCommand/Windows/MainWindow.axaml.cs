using Avalonia.Controls;
using CrsterCommand.ViewModels;

namespace CrsterCommand.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
