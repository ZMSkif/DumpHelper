using System.Windows;
using DupFinder.App.ViewModels;

namespace DupFinder.App.Views;

/// <summary>Окно подтверждения удаления со списком файлов.</summary>
public partial class DeleteConfirmWindow : Window
{
    public DeleteConfirmWindow(DeletionConfirmViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void OnProceed(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
