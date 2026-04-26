using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CrsterCommand.ViewModels;
using CrsterCommand.Models;
using Avalonia.Markup.Xaml;
using CrsterCommand.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace CrsterCommand.Views
{
    public partial class MacroManagerView : UserControl
    {
        public MacroManagerView()
        {
            InitializeComponent();

            // Add handler for TUNNEL phase KeyDown events (intercepts BEFORE child controls)
            // This way we catch Ctrl+V before the TextBox consumes it
            AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

            // Handle unloading to clean up attachments
            this.Unloaded += MacroManagerView_Unloaded;
        }

        private void MacroManagerView_Unloaded(object? sender, RoutedEventArgs e)
        {
            // Cleanup when leaving the page
            if (DataContext is MacroManagerViewModel vm)
            {
                vm.ClearAllSessions();
                vm.CleanupAttachments();
            }
        }

        private async void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            Debug.WriteLine($"OnPreviewKeyDown (Tunnel) - Key: {e.Key}, Modifiers: {e.KeyModifiers}, Sender: {sender?.GetType().Name}");

            // Handle Enter key - send prompt (cross-platform: Ctrl+Enter on Windows/Linux, Cmd+Enter on Mac adds newline, plain Enter sends)
            if (e.Key == Key.Return)
            {
                // Check if we have Ctrl (Windows/Linux) or Meta (Mac) pressed - if so, add newline
                bool isModifierPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

                if (isModifierPressed)
                {
                    // Ctrl+Enter or Cmd+Enter: Allow newline (don't handle, let TextBox do it)
                    Debug.WriteLine("Ctrl/Cmd+Enter detected - allowing newline");
                    return;
                }
                else
                {
                    // Plain Enter: Send the prompt
                    Debug.WriteLine("Enter detected - sending prompt");
                    TextBox? textBox = FindFocusedTextBox();
                    if (textBox != null)
                    {
                        var item = FindParentItem(textBox);
                        if (item != null && DataContext is MacroManagerViewModel vm)
                        {
                            e.Handled = true;
                            await vm.SendCommand.ExecuteAsync(item);
                            return;
                        }
                    }
                }
            }

            // Check for Ctrl+V or Cmd+V during the TUNNEL phase (before child controls handle it)
            if ((e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control)) ||
                (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                Debug.WriteLine("OnPreviewKeyDown (Tunnel) - Ctrl + V detected!");

                // In tunnel phase, sender will be the UserControl or a parent
                // We need to find the focused TextBox
                TextBox? textBox = FindFocusedTextBox();
                if (textBox == null)
                {
                    Debug.WriteLine("No focused TextBox found");
                    return;
                }

                Debug.WriteLine($"Found focused TextBox, looking for parent MacroAiAppItem");

                // Find the parent item by looking through DataContext hierarchy
                var item = FindParentItem(textBox);
                if (item == null)
                {
                    Debug.WriteLine("No MacroAiAppItem parent found");
                    return;
                }

                if (DataContext is not MacroManagerViewModel vm)
                {
                    Debug.WriteLine("DataContext is not MacroManagerViewModel");
                    return;
                }

                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null)
                {
                    Debug.WriteLine("Clipboard not available");
                    return;
                }

                try
                {
                    Debug.WriteLine("Getting clipboard data...");
                    // Get the clipboard data
                    using var data = await clipboard.TryGetDataAsync();
                    if (data == null)
                    {
                        Debug.WriteLine("Clipboard data is null");
                        return;
                    }

                    // 1. Check for files
                    var storageItems = await data.TryGetFilesAsync();
                    if (storageItems != null && storageItems.Any())
                    {
                        var fileList = storageItems.OfType<IStorageFile>().ToList();
                        if (fileList.Count > 0)
                        {
                            Debug.WriteLine($"Found {fileList.Count} files in clipboard");
                            e.Handled = true;
                            await vm.AttachFilesFromPasteAsync(item, fileList);
                            return;
                        }
                    }

                    // 2. Check for bitmap (screenshots)
                    var bitmap = await data.TryGetBitmapAsync();
                    if (bitmap != null)
                    {
                        Debug.WriteLine("Found bitmap in clipboard");
                        e.Handled = true;
                        await vm.AttachImageFromPasteAsync(item, bitmap);
                        return;
                    }

                    Debug.WriteLine("No files or bitmaps found in clipboard");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception in clipboard operations: {ex.Message}");
                    // If clipboard operations fail, allow normal paste
                }
            }
        }

        private TextBox? FindFocusedTextBox()
        {
            // Get the focused element from the top level
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.FocusManager?.GetFocusedElement() is TextBox textBox)
            {
                return textBox;
            }
            return null;
        }

        private MacroAiAppItem? FindParentItem(Visual visual)
        {
            // Traverse up the visual tree to find a parent with a MacroAiAppItem DataContext
            var current = visual.Parent;
            while (current != null)
            {
                if (current is StyledElement styledElement && styledElement.DataContext is MacroAiAppItem item)
                    return item;

                current = (current as Visual)?.Parent;
            }
            return null;
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
