using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupFinder.App.Resources;
using DupFinder.App.Services;
using DupFinder.Core.Actions;

namespace DupFinder.App.ViewModels;

/// <summary>Одна строка журнала операций.</summary>
public sealed record JournalRowViewModel(string When, string Action, string Name, string Directory, string Size);

/// <summary>
/// Что программа сделала с файлами. Отвечает на вопрос, который человек
/// задаёт через неделю: «а что вы вообще удалили с моего диска».
/// </summary>
public sealed partial class JournalViewModel : ObservableObject
{
    private readonly OperationJournal _journal;
    private readonly IShellService _shell;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty = true;

    public JournalViewModel(OperationJournal journal, IShellService shell)
    {
        _journal = journal;
        _shell = shell;
    }

    /// <summary>Записи, новые сверху.</summary>
    public Collections.BulkObservableCollection<JournalRowViewModel> Rows { get; } = new();

    /// <summary>Текст для пустого состояния.</summary>
    public string EmptyText => Strings.JournalEmpty;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var records = await Task.Run(() => _journal.ReadRecent()).ConfigureAwait(true);
            Rows.Reset(records.Select(Map));
            IsEmpty = Rows.Count == 0;
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
        _shell.Open(AppPaths.Root);
    }

    private static JournalRowViewModel Map(FileOperation operation)
    {
        var action = operation.Kind switch
        {
            FileOperationKind.Moved => Strings.KindMoved,
            FileOperationKind.Linked => Strings.KindLinked,
            _ => Strings.KindRecycled,
        };

        if (!operation.Succeeded)
        {
            action += $" — {Strings.JournalFailed}";
        }

        return new JournalRowViewModel(
            operation.At.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
            action,
            System.IO.Path.GetFileName(operation.Path),
            System.IO.Path.GetDirectoryName(operation.Path) ?? string.Empty,
            DuplicateRowViewModel.FormatSize(operation.Length));
    }
}
