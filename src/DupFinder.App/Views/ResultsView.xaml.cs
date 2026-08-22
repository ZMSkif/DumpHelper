using System.Windows.Controls;
using System.Windows.Input;
using DupFinder.App.ViewModels;

namespace DupFinder.App.Views;

/// <summary>Вкладка «Результаты».</summary>
public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new FocusFilterCommand(this), Key.F, ModifierKeys.Control));
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    /// <summary>
    /// Пробел отмечает выделенные строки, Delete отправляет отмеченные в Корзину (ТЗ §5).
    /// </summary>
    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
            {
                var rows = ResultsGrid.SelectedItems.OfType<DuplicateRowViewModel>().Where(r => !r.IsProtected).ToList();
                if (rows.Count == 0)
                {
                    return;
                }

                var target = !rows[0].IsMarked;
                foreach (var row in rows)
                {
                    row.IsMarked = target;
                }

                e.Handled = true;
                break;
            }

            case Key.Delete when Model.Results.DeleteMarkedCommand.CanExecute(null):
                Model.Results.DeleteMarkedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Ctrl+F ставит курсор в поле фильтра.</summary>
    private sealed class FocusFilterCommand : ICommand
    {
        private readonly ResultsView _view;

        public FocusFilterCommand(ResultsView view) => _view = view;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _view.FilterBox.Focus();
            _view.FilterBox.SelectAll();
        }
    }
}
