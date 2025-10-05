using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EDHelp.ViewModels;

namespace EDHelp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        // Only allow files
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.ToList();
            if (files?.Count > 0)
            {
                var file = files[0];
                if (file.Path.LocalPath.EndsWith(".txt"))
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        await vm.ImportDeckFromFile(file.Path.LocalPath);
                    }
                }
            }
        }
    }
}