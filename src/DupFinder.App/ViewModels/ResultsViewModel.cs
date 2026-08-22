using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupFinder.App.Collections;
using DupFinder.App.Resources;
using DupFinder.App.Services;
using DupFinder.Core.Actions;
using DupFinder.Core.Model;

namespace DupFinder.App.ViewModels;

/// <summary>
/// Вкладка «Результаты»: таблица, фильтры, отметка и удаление.
/// Полный список строк живёт в обычном массиве, а в коллекцию для таблицы
/// попадает уже отфильтрованный набор — фильтрация идёт в фоне (ТЗ §3).
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly IRecycleBin _recycleBin;
    private readonly DeletionPlanner _planner;
    private readonly OperationJournal _journal;

    private readonly List<DuplicateRowViewModel> _allRows = new();
    private readonly Dictionary<int, List<DuplicateRowViewModel>> _byGroup = new();

    private CancellationTokenSource? _filterCts;
    private ScanSummary? _summary;
    private int _markedCount;
    private long _markedBytes;
    private int _copiesCount;
    private long _reclaimBytes;
    private bool _bulkMarking;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private ChoiceItem<RoleFilter> _selectedRoleFilter;

    [ObservableProperty]
    private DuplicateRowViewModel? _selectedRow;

    [ObservableProperty]
    private string _statisticsText = Strings.SummaryPlaceholder;

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private bool _isDeleting;

    public ResultsViewModel(
        IDialogService dialogs,
        IShellService shell,
        IRecycleBin recycleBin,
        DeletionPlanner planner,
        OperationJournal journal)
    {
        _dialogs = dialogs;
        _shell = shell;
        _recycleBin = recycleBin;
        _planner = planner;
        _journal = journal;

        RoleFilters = new[]
        {
            new ChoiceItem<RoleFilter>(RoleFilter.Any, Strings.FilterRoleAll),
            new ChoiceItem<RoleFilter>(RoleFilter.CopiesOnly, Strings.FilterRoleCopies),
            new ChoiceItem<RoleFilter>(RoleFilter.OriginalsOnly, Strings.FilterRoleOriginals),
        };
        _selectedRoleFilter = RoleFilters[0];
    }

    /// <summary>Что показывать: всё, только копии или только оригиналы.</summary>
    public enum RoleFilter
    {
        Any,
        CopiesOnly,
        OriginalsOnly,
    }

    /// <summary>Строки, которые видит таблица.</summary>
    public BulkObservableCollection<DuplicateRowViewModel> Rows { get; } = new();

    public IReadOnlyList<ChoiceItem<RoleFilter>> RoleFilters { get; }

    /// <summary>Сколько строк отмечено галочкой.</summary>
    public int MarkedCount => _markedCount;

    /// <summary>Есть ли что удалять.</summary>
    public bool HasMarked => _markedCount > 0;

    /// <summary>Готовит таблицу к новому поиску.</summary>
    public void Clear()
    {
        _allRows.Clear();
        _byGroup.Clear();
        _markedCount = 0;
        _markedBytes = 0;
        _copiesCount = 0;
        _reclaimBytes = 0;
        _summary = null;
        Rows.Reset(Array.Empty<DuplicateRowViewModel>());
        HasRows = false;
        StatisticsText = Strings.SummaryPlaceholder;
        NotifyMarkedChanged();
    }

    /// <summary>Добавляет пачку найденных групп. Вызывается из UI-потока.</summary>
    public void AddGroups(IReadOnlyList<DuplicateGroup> groups)
    {
        var fresh = new List<DuplicateRowViewModel>();
        foreach (var group in groups)
        {
            var rows = new List<DuplicateRowViewModel>(group.Items.Count);
            foreach (var item in group.Items)
            {
                var row = new DuplicateRowViewModel(item, group.Id, group.Kind, OnRowMarkChanged);
                rows.Add(row);
                fresh.Add(row);
            }

            _byGroup[group.Id] = rows;
            _copiesCount += rows.Count - 1;
            _reclaimBytes += group.RedundantBytes;
        }

        _allRows.AddRange(fresh);

        var visible = fresh.Where(Matches).ToList();
        if (visible.Count > 0)
        {
            Rows.AddRange(visible);
        }

        HasRows = _allRows.Count > 0;
        UpdateStatistics();
    }

    /// <summary>Записывает итоги сканирования в строку статистики.</summary>
    public void ApplySummary(ScanSummary? summary)
    {
        _summary = summary;
        UpdateStatistics();
    }

    [RelayCommand]
    private void MarkCopies() => MarkAll(row => !row.IsOriginal && !row.IsProtected);

    [RelayCommand]
    private void InvertMarks() => MarkAll(row => !row.IsMarked && !row.IsProtected);

    [RelayCommand]
    private void ClearMarks() => MarkAll(_ => false);

    /// <summary>
    /// Проставляет отметки всем строкам за один проход. Пересчёт статистики
    /// на время прохода выключается: иначе на 200 000 строк получаем
    /// 200 000 переформатирований строки и подвисший интерфейс (ТЗ §3).
    /// </summary>
    private void MarkAll(Func<DuplicateRowViewModel, bool> decide)
    {
        _bulkMarking = true;
        try
        {
            foreach (var row in _allRows)
            {
                row.IsMarked = decide(row);
            }
        }
        finally
        {
            _bulkMarking = false;
        }

        NotifyMarkedChanged();
        UpdateStatistics();
    }

    /// <summary>Показывает журнал того, что уже сделано с файлами.</summary>
    [RelayCommand]
    private void ShowJournal() => _dialogs.ShowOperationJournal(_journal);

    [RelayCommand]
    private void RevealSelected()
    {
        if (SelectedRow is not null)
        {
            _shell.RevealInExplorer(SelectedRow.Path);
        }
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedRow is not null)
        {
            _shell.Open(SelectedRow.Path);
        }
    }

    [RelayCommand]
    private void CopySelectedPath()
    {
        if (SelectedRow is not null)
        {
            _shell.CopyToClipboard(SelectedRow.Path);
        }
    }

    /// <summary>Делает выбранную строку оригиналом своей группы (ТЗ §5, контекстное меню).</summary>
    [RelayCommand]
    private void MakeSelectedOriginal()
    {
        if (SelectedRow is null || !_byGroup.TryGetValue(SelectedRow.GroupId, out var group))
        {
            return;
        }

        var previous = group.FirstOrDefault(r => r.IsOriginal);
        foreach (var row in group)
        {
            row.SetOriginal(ReferenceEquals(row, SelectedRow));
        }

        // Число копий не изменилось, но «освободить» считается по размерам копий.
        _reclaimBytes += (previous?.Length ?? 0) - SelectedRow.Length;
        SelectedRow.IsMarked = false;
        UpdateStatistics();
    }

    /// <summary>Удаляет отмеченные файлы в Корзину (ТЗ §4.6).</summary>
    [RelayCommand]
    private async Task DeleteMarkedAsync()
    {
        var marked = _allRows.Where(r => r.IsMarked).ToList();
        if (marked.Count == 0)
        {
            _dialogs.Warn(Strings.NothingMarked);
            return;
        }

        // Последняя проверка живёт в движке: файл мог измениться или исчезнуть,
        // пока окно было открыто, а группа — оказаться отмеченной целиком.
        var candidates = marked
            .Select(r => new DeletionCandidate(
                new FileEntry(r.Path, r.Length, r.Modified.ToUniversalTime()),
                r.GroupId,
                r.IsOriginal,
                r.IsProtected))
            .ToList();

        var sizes = _byGroup.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
        var plan = await Task.Run(() => _planner.Prepare(candidates, sizes)).ConfigureAwait(true);

        if (!_dialogs.ConfirmDeletion(plan))
        {
            return;
        }

        IsDeleting = true;
        try
        {
            var paths = plan.Allowed.Select(d => d.Path).ToList();
            var result = await Task.Run(() => _recycleBin.Delete(paths)).ConfigureAwait(true);

            var failed = result.Failed.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var now = DateTimeOffset.Now;
            await Task.Run(() => _journal.Append(plan.Allowed.Select(d => new FileOperation(
                now,
                FileOperationKind.Recycled,
                d.Path,
                d.Candidate.File.Length,
                !failed.Contains(d.Path))))).ConfigureAwait(true);

            var byPath = _allRows.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
            var removed = plan.Allowed
                .Where(d => !failed.Contains(d.Path))
                .Select(d => byPath.GetValueOrDefault(d.Path))
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();
            RemoveRows(removed);

            var status = string.Format(Strings.DeleteResultFormat, result.Deleted);
            if (result.Failed.Count > 0)
            {
                status += string.Format(
                    Strings.DeleteFailedFormat,
                    string.Join(", ", result.Failed.Take(5).Select(Path.GetFileName)));
            }

            StatisticsText = status;
        }
        finally
        {
            IsDeleting = false;
        }
    }

    partial void OnFilterTextChanged(string value) => ScheduleFilter();

    partial void OnSelectedRoleFilterChanged(ChoiceItem<RoleFilter> value) => ScheduleFilter();

    private void RemoveRows(IReadOnlyList<DuplicateRowViewModel> removed)
    {
        var doomed = removed.ToHashSet();
        _bulkMarking = true;
        foreach (var row in removed)
        {
            row.IsMarked = false;
            if (_byGroup.TryGetValue(row.GroupId, out var group))
            {
                group.Remove(row);
                if (group.Count == 0)
                {
                    _byGroup.Remove(row.GroupId);
                }
            }
        }

        _bulkMarking = false;
        NotifyMarkedChanged();

        _allRows.RemoveAll(doomed.Contains);
        Rows.RemoveRange(removed);
        HasRows = _allRows.Count > 0;

        _copiesCount = 0;
        _reclaimBytes = 0;
        foreach (var group in _byGroup.Values)
        {
            _copiesCount += Math.Max(0, group.Count - 1);
            _reclaimBytes += group.Where(r => !r.IsOriginal).Sum(r => r.Length);
        }

        UpdateStatistics();
    }

    private void OnRowMarkChanged(DuplicateRowViewModel row, bool marked)
    {
        _markedCount += marked ? 1 : -1;
        _markedBytes += marked ? row.Length : -row.Length;
        if (_bulkMarking)
        {
            return;
        }

        NotifyMarkedChanged();
        UpdateStatistics();
    }

    private void NotifyMarkedChanged()
    {
        OnPropertyChanged(nameof(MarkedCount));
        OnPropertyChanged(nameof(HasMarked));
    }

    /// <summary>
    /// Пересобирает видимый набор в фоне: при 200 000 строках предикат
    /// CollectionView на UI-потоке заметно подтормаживает.
    /// </summary>
    private async void ScheduleFilter()
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        var cts = new CancellationTokenSource();
        _filterCts = cts;

        try
        {
            // Небольшая пауза: пока человек печатает, пересчитывать нет смысла.
            await Task.Delay(150, cts.Token).ConfigureAwait(true);
            var snapshot = _allRows.ToArray();
            var filtered = await Task.Run(() => snapshot.Where(Matches).ToList(), cts.Token).ConfigureAwait(true);
            if (!cts.IsCancellationRequested)
            {
                Rows.Reset(filtered);
            }
        }
        catch (OperationCanceledException)
        {
            // Пришёл более свежий запрос фильтра.
        }
    }

    private bool Matches(DuplicateRowViewModel row)
    {
        if (SelectedRoleFilter.Value == RoleFilter.CopiesOnly && row.IsOriginal)
        {
            return false;
        }

        if (SelectedRoleFilter.Value == RoleFilter.OriginalsOnly && !row.IsOriginal)
        {
            return false;
        }

        var query = FilterText;
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Directory.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatistics()
    {
        if (_allRows.Count == 0 && _summary is null)
        {
            StatisticsText = Strings.SummaryPlaceholder;
            return;
        }

        var scanned = _summary?.FilesSeen ?? 0;
        var inGroups = _allRows.Count;
        var groups = _byGroup.Count;
        StatisticsText = string.Format(
            Strings.SummaryFormat,
            scanned,
            Math.Max(0, scanned - inGroups),
            inGroups,
            groups,
            _copiesCount,
            DuplicateRowViewModel.FormatSize(_reclaimBytes),
            _markedCount,
            DuplicateRowViewModel.FormatSize(_markedBytes));
    }
}
