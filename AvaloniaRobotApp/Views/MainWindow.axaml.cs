using Avalonia.Controls;
using AvaloniaRobotApp.ViewModels;

namespace AvaloniaRobotApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
