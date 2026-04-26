using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CrsterCommand.ViewModels;
using System.Diagnostics;

namespace CrsterCommand.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += MainWindow_PropertyChanged;

        // Add tunneling event handler to capture Ctrl+V before it reaches child elements
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Intercept Ctrl+V during the tunnel phase
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Debug.WriteLine("MainWindow - Ctrl + V captured via tunneling");
            // Event will continue to bubble to child controls
        }
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            if (WindowState == WindowState.Minimized)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(true);

                if (IsVisible)
                {
                    Hide();
                }
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                (Application.Current?.DataContext as AppViewModel)?.SetTrayVisible(false);
            }
        }
    }
}
