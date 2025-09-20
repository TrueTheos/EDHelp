using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EDHelp.ViewModels;

namespace EDHelp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupDragDrop();
    }

    private void SetupDragDrop()
    {
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DragEnterEvent, DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, DragLeave);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void DragEnter(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsDragOver = true;
            ImportView.Classes.Add("dragover");
        }
    }

    private void DragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsDragOver = false;
            ImportView.Classes.Remove("dragover");
        }
    }

    private async void Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsDragOver = false;
            ImportView.Classes.Remove("dragover");

            if (e.Data.GetFiles() is { } files)
            {
                var file = files.FirstOrDefault();
                if (file?.Path.LocalPath is { } filePath &&
                    Path.GetExtension(filePath).ToLower() == ".txt")
                {
                    await vm.ImportDeckCommand.ExecuteAsync(filePath);
                }
            }
        }
    }

    private async void BrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Decklist File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text Files")
                {
                    Patterns = new[] { "*.txt" }
                }
            }
        });

        if (files.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.ImportDeckCommand.ExecuteAsync(files[0].Path.LocalPath);
        }
    }
}