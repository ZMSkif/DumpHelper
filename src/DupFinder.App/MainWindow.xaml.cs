using System.Windows;
using DupFinder.App.ViewModels;

namespace DupFinder.App;

/// <summary>Главное окно. Кода здесь ровно столько, сколько нельзя сделать привязками.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel? Model => DataContext as MainViewModel;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Папки можно просто перетащить в окно (ТЗ §5).</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (Model is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var added = false;
        foreach (var path in (string[])e.Data.GetData(DataFormats.FileDrop))
        {
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (folder is not null)
            {
                Model.Scan.AddRoot(folder);
                added = true;
            }
        }

        if (added)
        {
            Model.SelectedTab = 0;
        }

        e.Handled = true;
    }
}
