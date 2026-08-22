namespace DupFinder.Core.Model;

/// <summary>Режим поиска (ТЗ §4.1–4.3).</summary>
public enum ScanMode
{
    /// <summary>Точные копии: совпадение байт в байт.</summary>
    Exact,

    /// <summary>Та же съёмка: EXIF-ключ (дата съёмки + камера + размеры).</summary>
    SameShot,

    /// <summary>Визуально похожие: dHash + расстояние Хэмминга.</summary>
    Similar,
}

/// <summary>Как именно совпали файлы внутри группы.</summary>
public enum MatchKind
{
    ExactCopy,
    SameShot,
    Similar,
}

/// <summary>Правило выбора оригинала в группе (ТЗ §4.4).</summary>
public enum OriginalRule
{
    Oldest,
    Newest,
    ShortestPath,
    HighestResolution,
    LargestFile,
    SourceFormat,
}

/// <summary>Категория файла по расширению.</summary>
public enum FileKind
{
    Other = 0,
    Photo,
    Video,
    Audio,
    Document,
    Archive,
}

/// <summary>Фильтр по категориям. <see cref="All"/> — не фильтровать.</summary>
[Flags]
public enum FileKindFilter
{
    All = 0,
    Photo = 1 << 0,
    Video = 1 << 1,
    Audio = 1 << 2,
    Document = 1 << 3,
    Archive = 1 << 4,
    Other = 1 << 5,
}

/// <summary>Стадия конвейера — для прогресса.</summary>
public enum ScanStage
{
    Starting,
    Enumerating,
    GroupingBySize,
    PartialHash,
    MidTailHash,
    FullHash,
    Confirming,
    Fingerprinting,
    Matching,
    Building,
    Completed,
    Cancelled,
}

/// <summary>Тип носителя — определяет число потоков чтения (ТЗ §3).</summary>
public enum DiskKind
{
    Auto,
    Ssd,
    Hdd,
    Network,
}

/// <summary>Способ окончательного подтверждения точной копии (ТЗ §4.1 п.6).</summary>
public enum ExactConfirmation
{
    Sha256,
    Bytewise,
}
