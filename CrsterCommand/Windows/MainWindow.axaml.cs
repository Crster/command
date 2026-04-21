using Avalonia;
using Avalonia.Controls;
using CrsterCommand.ViewModels;

namespace CrsterCommand.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += MainWindow_PropertyChanged;
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            if (WindowState == WindowState.Minimized && IsVisible)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(true);
                Hide();
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(false);
            }
        }
    }
}
