using CommunityToolkit.Mvvm.ComponentModel;
using DupFinder.App.Resources;
using DupFinder.Core.Model;

namespace DupFinder.App.ViewModels;

/// <summary>
/// Одна строка таблицы результатов. Строк бывает больше 200 000, поэтому
/// производные тексты считаются лениво в геттерах, а не хранятся полями (ТЗ §3).
/// </summary>
public sealed partial class DuplicateRowViewModel : ObservableObject
{
    private readonly DuplicateItem _item;
    private readonly Action<DuplicateRowViewModel, bool>? _markChanged;

    [ObservableProperty]
    private bool _isMarked;

    [ObservableProperty]
    private bool _isOriginal;

    public DuplicateRowViewModel(
        DuplicateItem item,
        int groupId,
        MatchKind kind,
        Action<DuplicateRowViewModel, bool>? markChanged = null)
    {
        _item = item;
        _markChanged = markChanged;
        GroupId = groupId;
        Kind = kind;
        _isOriginal = item.IsOriginal;
    }

    /// <summary>Номер группы; по нему строки одной группы идут подряд и красятся полосами.</summary>
    public int GroupId { get; }

    /// <summary>Как найдено совпадение.</summary>
    public MatchKind Kind { get; }

    /// <summary>Нечётные группы подсвечиваются, чтобы граница групп была видна.</summary>
    public bool IsGroupOdd => (GroupId & 1) == 1;

    /// <summary>Файл из папки-эталона: удалять нельзя.</summary>
    public bool IsProtected => _item.IsProtected;

    /// <summary>Точное совпадение байт в байт.</summary>
    public bool IsExact => Kind == MatchKind.ExactCopy;

    public string Path => _item.Path;

    public string Name => System.IO.Path.GetFileName(_item.Path);

    public string Directory => System.IO.Path.GetDirectoryName(_item.Path) ?? string.Empty;

    public string Extension => System.IO.Path.GetExtension(_item.Path).ToLowerInvariant();

    public long Length => _item.Length;

    public DateTime Modified => _item.LastWriteUtc.ToLocalTime();

    public string SizeText => FormatSize(_item.Length);

    public string PixelsText => _item.Width > 0 ? $"{_item.Width}×{_item.Height}" : string.Empty;

    public string KindText => _item.Kind switch
    {
        FileKind.Photo => Strings.KindPhoto,
        FileKind.Video => Strings.KindVideo,
        FileKind.Audio => Strings.KindAudio,
        FileKind.Document => Strings.KindDocument,
        FileKind.Archive => Strings.KindArchive,
        _ => Extension.TrimStart('.').ToUpperInvariant(),
    };

    /// <summary>Роль в группе: эталон / оригинал / копия.</summary>
    public string RoleText => IsProtected
        ? Strings.RoleProtected
        : IsOriginal ? Strings.RoleOriginal : Strings.RoleCopy;

    /// <summary>Чем именно файл похож на оригинал группы.</summary>
    public string MatchText
    {
        get
        {
            if (IsOriginal)
            {
                return Strings.MatchGroupOriginal;
            }

            return Kind switch
            {
                MatchKind.SameShot => Strings.MatchSameShot,
                MatchKind.Similar => $"{Strings.MatchSimilar} ({_item.Distance})",
                _ => Strings.MatchExact,
            };
        }
    }

    /// <summary>Переставляет роль без создания новой строки (пункт «сделать оригиналом»).</summary>
    public void SetOriginal(bool value) => IsOriginal = value;

    /// <summary>Человекочитаемый размер.</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):N2} ГБ",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):N1} МБ",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):N0} КБ",
        _ => $"{bytes} Б",
    };

    partial void OnIsMarkedChanged(bool value) => _markChanged?.Invoke(this, value);

    partial void OnIsOriginalChanged(bool value)
    {
        OnPropertyChanged(nameof(RoleText));
        OnPropertyChanged(nameof(MatchText));
    }
}
