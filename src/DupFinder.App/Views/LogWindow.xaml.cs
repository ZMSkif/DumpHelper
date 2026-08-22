using System.Windows;
using DupFinder.App.ViewModels;

namespace DupFinder.App.Views;

/// <summary>Окно просмотра журнала.</summary>
public partial class LogWindow : Window
{
    public LogWindow(LogViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => await model.RefreshAsync();
    }
}
