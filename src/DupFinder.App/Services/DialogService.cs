using System.Runtime.Versioning;
using System.Windows;

namespace DupFinder.App.Services;

/// <summary>Ответ пользователя на вопрос из трёх вариантов.</summary>
public enum ThreeWayAnswer
{
    Yes,
    No,
    Cancel,
}

/// <summary>Диалоги: выбор папки, вопросы, сообщения об ошибках.</summary>
public interface IDialogService
{
    /// <summary>Просит выбрать папку. Возвращает null, если пользователь отказался.</summary>
    string? PickFolder(string title, string? initialPath = null);

    /// <summary>Показывает предупреждение.</summary>
    void Warn(string message);

    /// <summary>Показывает ошибку с предложением открыть журнал.</summary>
    void ShowError(string message, string logFolder);

    /// <summary>Задаёт вопрос «да/нет».</summary>
    bool Confirm(string message, string title);

    /// <summary>Задаёт вопрос «да/нет/отмена».</summary>
    ThreeWayAnswer Ask(string message, string title);

    /// <summary>Открывает окно с журналом работы.</summary>
    void ShowLog();
}

/// <inheritdoc />
[SupportedOSPlatform("windows")]
public sealed class DialogService : IDialogService
{
    private readonly IShellService _shell;

    public DialogService(IShellService shell) => _shell = shell;

    public string? PickFolder(string title, string? initialPath = null)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.SelectedPath = initialPath;
        }

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public void Warn(string message) =>
        MessageBox.Show(message, Resources.Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string logFolder)
    {
        var result = MessageBox.Show(
            $"{message}\n\n{Resources.Strings.ErrorOpenLogPrompt}",
            Resources.Strings.AppTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
        {
            _shell.Open(logFolder);
        }
    }

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowLog()
    {
        var window = new Views.LogWindow(new ViewModels.LogViewModel(_shell))
        {
            Owner = Application.Current?.MainWindow,
        };
        window.ShowDialog();
    }

    public ThreeWayAnswer Ask(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => ThreeWayAnswer.Yes,
            MessageBoxResult.No => ThreeWayAnswer.No,
            _ => ThreeWayAnswer.Cancel,
        };
}
