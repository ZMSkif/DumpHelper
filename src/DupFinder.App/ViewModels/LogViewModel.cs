using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupFinder.App.Resources;
using DupFinder.App.Services;

namespace DupFinder.App.ViewModels;

/// <summary>
/// Журнал прямо в окне. Раньше кнопка открывала папку в Проводнике — это
/// требовало от человека самому искать нужный файл. Теперь содержимое видно
/// сразу, а до файла можно дойти отсюда, если действительно нужно.
/// </summary>
public sealed partial class LogViewModel : ObservableObject
{
    /// <summary>Сколько последних строк показывать: журнал может быть большим.</summary>
    private const int TailLines = 2000;

    private readonly IShellService _shell;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _fileLabel = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public LogViewModel(IShellService shell) => _shell = shell;

    /// <summary>Путь к показанному файлу; пустая строка, если журнала ещё нет.</summary>
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>Читает последние строки самого свежего файла журнала.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var (path, text) = await Task.Run(Load).ConfigureAwait(true);
            FilePath = path;
            Text = text;
            FileLabel = path.Length > 0 ? path : Strings.LogEmpty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        AppPaths.EnsureCreated();
        _shell.Open(AppPaths.Logs);
    }

    [RelayCommand]
    private void CopyAll()
    {
        if (Text.Length > 0)
        {
            _shell.CopyToClipboard(Text);
        }
    }

    private static (string Path, string Text) Load()
    {
        try
        {
            if (!Directory.Exists(AppPaths.Logs))
            {
                return (string.Empty, Strings.LogEmpty);
            }

            var newest = new DirectoryInfo(AppPaths.Logs)
                .GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest is null)
            {
                return (string.Empty, Strings.LogEmpty);
            }

            // Файл пишется прямо сейчас, поэтому открываем с полным разделением доступа.
            using var stream = new FileStream(
                newest.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            var tail = new Queue<string>(TailLines);
            while (reader.ReadLine() is { } line)
            {
                if (tail.Count == TailLines)
                {
                    tail.Dequeue();
                }

                tail.Enqueue(line);
            }

            var text = string.Join(Environment.NewLine, tail);
            return (newest.FullName, text.Length > 0 ? text : Strings.LogEmpty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (string.Empty, string.Format(Strings.LogReadFailedFormat, ex.Message));
        }
    }
}
