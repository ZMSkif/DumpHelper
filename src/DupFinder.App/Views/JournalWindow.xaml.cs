using System.Windows;
using DupFinder.App.ViewModels;

namespace DupFinder.App.Views;

/// <summary>Окно журнала операций над файлами.</summary>
public partial class JournalWindow : Window
{
    public JournalWindow(JournalViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => await model.RefreshAsync();
    }
}
