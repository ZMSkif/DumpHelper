using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupFinder.App.Resources;
using DupFinder.App.Services;
using DupFinder.Core.Model;

namespace DupFinder.App.ViewModels;

/// <summary>Вкладка «Сканирование»: что искать, где искать и как показывать ход работы.</summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly Stopwatch _clock = new();
    private long _lastBytes;
    private TimeSpan _lastBytesAt;

    [ObservableProperty]
    private string? _selectedRoot;

    [ObservableProperty]
    private string _referenceFolder = string.Empty;

    [ObservableProperty]
    private string _excludeMasks = string.Empty;

    [ObservableProperty]
    private string _minSizeKb = "1";

    [ObservableProperty]
    private string _maxSizeMb = "0";

    [ObservableProperty]
    private bool _recurse = true;

    [ObservableProperty]
    private bool _includeHidden;

    [ObservableProperty]
    private bool _includeSystem;

    [ObservableProperty]
    private bool _confirmBytewise;

    [ObservableProperty]
    private bool _protectSystemFolders = true;

    [ObservableProperty]
    private ScanMode _selectedMode = ScanMode.Exact;

    [ObservableProperty]
    private ChoiceItem<FileKindFilter> _selectedKind;

    [ObservableProperty]
    private ChoiceItem<OriginalRule> _selectedOriginalRule;

    [ObservableProperty]
    private ChoiceItem<DiskKind> _selectedDiskKind;

    [ObservableProperty]
    private bool _isBusy;

    // По умолчанию false: до первого запуска полоса прогресса не должна ничем шевелиться.
    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = Strings.Ready;

    [ObservableProperty]
    private string _detailText = string.Empty;

    public ScanViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;

        Modes = new[]
        {
            new ModeOptionViewModel(ScanMode.Exact, Strings.ModeExact, Strings.ModeExactHint, true, SelectMode),
            new ModeOptionViewModel(ScanMode.SameShot, Strings.ModeSameShot, Strings.ModeSameShotHint, false, SelectMode),
            new ModeOptionViewModel(ScanMode.Similar, Strings.ModeSimilar, Strings.ModeSimilarHint, false, SelectMode),
        };

        Kinds = new[]
        {
            new ChoiceItem<FileKindFilter>(FileKindFilter.All, Strings.KindAll),
            new ChoiceItem<FileKindFilter>(FileKindFilter.Photo, Strings.KindPhoto),
            new ChoiceItem<FileKindFilter>(FileKindFilter.Video, Strings.KindVideo),
            new ChoiceItem<FileKindFilter>(FileKindFilter.Audio, Strings.KindAudio),
            new ChoiceItem<FileKindFilter>(FileKindFilter.Document, Strings.KindDocument),
            new ChoiceItem<FileKindFilter>(FileKindFilter.Archive, Strings.KindArchive),
        };

        OriginalRules = new[]
        {
            new ChoiceItem<OriginalRule>(OriginalRule.Oldest, Strings.OriginalOldest),
            new ChoiceItem<OriginalRule>(OriginalRule.Newest, Strings.OriginalNewest),
            new ChoiceItem<OriginalRule>(OriginalRule.ShortestPath, Strings.OriginalShortest),
            new ChoiceItem<OriginalRule>(OriginalRule.HighestResolution, Strings.OriginalResolution),
            new ChoiceItem<OriginalRule>(OriginalRule.LargestFile, Strings.OriginalLargest),
            new ChoiceItem<OriginalRule>(OriginalRule.SourceFormat, Strings.OriginalFormat),
        };

        DiskKinds = new[]
        {
            new ChoiceItem<DiskKind>(DiskKind.Auto, Strings.DiskAuto),
            new ChoiceItem<DiskKind>(DiskKind.Ssd, Strings.DiskSsd),
            new ChoiceItem<DiskKind>(DiskKind.Hdd, Strings.DiskHdd),
            new ChoiceItem<DiskKind>(DiskKind.Network, Strings.DiskNetwork),
        };

        Modes[0].IsSelected = true;
        _selectedKind = Kinds[0];
        _selectedOriginalRule = OriginalRules[0];
        _selectedDiskKind = DiskKinds[0];
    }

    /// <summary>Папки, которые будут проверяться.</summary>
    public ObservableCollection<string> Roots { get; } = new();

    public IReadOnlyList<ModeOptionViewModel> Modes { get; }

    public IReadOnlyList<ChoiceItem<FileKindFilter>> Kinds { get; }

    public IReadOnlyList<ChoiceItem<OriginalRule>> OriginalRules { get; }

    public IReadOnlyList<ChoiceItem<DiskKind>> DiskKinds { get; }

    /// <summary>Пояснение к выбранному режиму простым языком.</summary>
    public string ModeHint => Modes.FirstOrDefault(m => m.Value == SelectedMode)?.Description ?? string.Empty;

    /// <summary>Есть ли хоть одна папка — от этого зависит доступность кнопки «Начать».</summary>
    public bool HasRoots => Roots.Count > 0;

    /// <summary>Добавляет папку, если её ещё нет в списке.</summary>
    public void AddRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        var full = Path.GetFullPath(path);
        if (!Roots.Any(r => string.Equals(r, full, StringComparison.OrdinalIgnoreCase)))
        {
            Roots.Add(full);
            OnPropertyChanged(nameof(HasRoots));
        }
    }

    /// <summary>Собирает настройки для движка. Возвращает null, если что-то не заполнено.</summary>
    public ScanOptions? BuildOptions()
    {
        if (Roots.Count == 0)
        {
            _dialogs.Warn(Strings.NoFoldersSelected);
            return null;
        }

        foreach (var root in Roots)
        {
            if (!Directory.Exists(root))
            {
                _dialogs.Warn(string.Format(Strings.FolderNotFound, root));
                return null;
            }
        }

        var reference = ReferenceFolder.Trim();
        if (reference.Length > 0 && !Directory.Exists(reference))
        {
            _dialogs.Warn(string.Format(Strings.ReferenceNotFound, reference));
            return null;
        }

        var masks = ExcludeMasks
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return new ScanOptions(
            Roots: Roots.ToArray(),
            Recurse: Recurse,
            Mode: SelectedMode,
            Kinds: SelectedKind.Value,
            MinBytes: ParseNumber(MinSizeKb) * 1024,
            ExcludeMasks: masks,
            ReferenceFolder: reference.Length > 0 ? reference : null,
            OriginalRule: SelectedOriginalRule.Value,
            SimilarityThreshold: 7,
            ConfirmBytewise: ConfirmBytewise)
        {
            MaxBytes = ParseNumber(MaxSizeMb) * 1024 * 1024,
            IncludeHidden = IncludeHidden,
            IncludeSystem = IncludeSystem,
            DiskKind = SelectedDiskKind.Value,
            ProtectSystemFolders = ProtectSystemFolders,
        };
    }

    /// <summary>Переводит интерфейс в состояние «идёт поиск».</summary>
    public void BeginScan()
    {
        IsBusy = true;
        IsIndeterminate = true;
        ProgressValue = 0;
        DetailText = string.Empty;
        StatusText = Strings.StageEnumerating;
        _lastBytes = 0;
        _lastBytesAt = TimeSpan.Zero;
        _clock.Restart();
    }

    /// <summary>Возвращает интерфейс в спокойное состояние.</summary>
    public void EndScan(string status)
    {
        _clock.Stop();
        IsBusy = false;
        IsIndeterminate = false;
        ProgressValue = 0;
        StatusText = status;
        DetailText = string.Empty;
    }

    /// <summary>Сколько времени идёт поиск.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    /// <summary>Обновляет строку прогресса. Вызывается только из UI-потока.</summary>
    public void ApplyProgress(ScanProgress progress)
    {
        StatusText = StageText(progress.Stage);

        if (progress.Fraction is { } fraction)
        {
            IsIndeterminate = false;
            ProgressValue = fraction * 100;
        }
        else
        {
            IsIndeterminate = true;
        }

        var parts = new List<string>();
        if (progress.Total > 0)
        {
            parts.Add(string.Format(Strings.ProgressFormat, StageText(progress.Stage), progress.Done, progress.Total));
        }
        else if (progress.Done > 0)
        {
            parts.Add($"{StageText(progress.Stage)}: {progress.Done}");
        }

        if (Speed(progress.BytesRead) is { } speed)
        {
            parts.Add(string.Format(Strings.SpeedFormat, DuplicateRowViewModel.FormatSize(speed)));
        }

        if (Remaining(progress) is { } remaining)
        {
            parts.Add(string.Format(Strings.RemainingFormat, Format(remaining)));
        }
        else
        {
            parts.Add(string.Format(Strings.ElapsedFormat, Format(_clock.Elapsed)));
        }

        DetailText = string.Join("   •   ", parts);
    }

    [RelayCommand]
    private void AddFolder()
    {
        var picked = _dialogs.PickFolder(Strings.AddFolder, Roots.LastOrDefault());
        if (picked is not null)
        {
            AddRoot(picked);
        }
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedRoot is not null && Roots.Remove(SelectedRoot))
        {
            SelectedRoot = Roots.LastOrDefault();
            OnPropertyChanged(nameof(HasRoots));
        }
    }

    [RelayCommand]
    private void PickReference()
    {
        var picked = _dialogs.PickFolder(Strings.SectionReference, ReferenceFolder);
        if (picked is not null)
        {
            ReferenceFolder = picked;
        }
    }

    [RelayCommand]
    private void ClearReference() => ReferenceFolder = string.Empty;

    partial void OnSelectedModeChanged(ScanMode value) => OnPropertyChanged(nameof(ModeHint));

    /// <summary>Карточка сообщает о выборе; остальные снимаем.</summary>
    private void SelectMode(ModeOptionViewModel option)
    {
        foreach (var mode in Modes)
        {
            if (!ReferenceEquals(mode, option))
            {
                mode.IsSelected = false;
            }
        }

        SelectedMode = option.Value;
    }

    private static long ParseNumber(string text) =>
        long.TryParse(text.Trim(), out var value) && value > 0 ? value : 0;

    private static string StageText(ScanStage stage) => stage switch
    {
        ScanStage.Enumerating => Strings.StageEnumerating,
        ScanStage.GroupingBySize => Strings.StageGrouping,
        ScanStage.PartialHash => Strings.StagePartial,
        ScanStage.MidTailHash => Strings.StageMidTail,
        ScanStage.FullHash => Strings.StageFull,
        ScanStage.Confirming => Strings.StageConfirming,
        ScanStage.Completed => Strings.StageDone,
        ScanStage.Cancelled => Strings.StageCancelled,
        _ => Strings.StageEnumerating,
    };

    /// <summary>Скорость чтения за последний отрезок; null, пока мерить нечего.</summary>
    private long? Speed(long bytesRead)
    {
        var now = _clock.Elapsed;
        var seconds = (now - _lastBytesAt).TotalSeconds;
        if (seconds < 0.5 || bytesRead <= _lastBytes)
        {
            return null;
        }

        var speed = (long)((bytesRead - _lastBytes) / seconds);
        _lastBytes = bytesRead;
        _lastBytesAt = now;
        return speed;
    }

    private TimeSpan? Remaining(ScanProgress progress)
    {
        if (progress.Fraction is not { } fraction || fraction <= 0.02)
        {
            return null;
        }

        var elapsed = _clock.Elapsed;
        return TimeSpan.FromSeconds(elapsed.TotalSeconds * ((1 - fraction) / fraction));
    }

    private static string Format(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
