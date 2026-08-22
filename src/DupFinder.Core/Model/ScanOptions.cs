namespace DupFinder.Core.Model;

/// <summary>
/// Параметры сканирования. Позиционная часть — как в ТЗ §4.7; остальное добавлено
/// свойствами со значениями по умолчанию, чтобы не ломать заявленную сигнатуру.
/// </summary>
public sealed record ScanOptions(
    IReadOnlyList<string> Roots,
    bool Recurse,
    ScanMode Mode,
    FileKindFilter Kinds,
    long MinBytes,
    IReadOnlyList<string> ExcludeMasks,
    string? ReferenceFolder,
    OriginalRule OriginalRule,
    int SimilarityThreshold,
    bool ConfirmBytewise)
{
    /// <summary>Удобный конструктор «всё по умолчанию» для одной папки.</summary>
    public static ScanOptions ForRoot(string root, ScanMode mode = ScanMode.Exact) => new(
        Roots: new[] { root },
        Recurse: true,
        Mode: mode,
        Kinds: FileKindFilter.All,
        MinBytes: 0,
        ExcludeMasks: Array.Empty<string>(),
        ReferenceFolder: null,
        OriginalRule: OriginalRule.Oldest,
        SimilarityThreshold: 7,
        ConfirmBytewise: false);

    /// <summary>Верхняя граница размера файла; 0 или меньше — без ограничения.</summary>
    public long MaxBytes { get; init; }

    /// <summary>Папки, которые не обходить (по префиксу пути).</summary>
    public IReadOnlyList<string> ExcludeFolders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Защищать файлы в системных папках: Windows, Program Files, ProgramData.
    /// Дубликаты там встречаются постоянно, но удалять их нельзя. По умолчанию включено.
    /// </summary>
    public bool ProtectSystemFolders { get; init; } = true;

    /// <summary>Включать скрытые файлы и папки.</summary>
    public bool IncludeHidden { get; init; }

    /// <summary>Включать системные файлы и папки.</summary>
    public bool IncludeSystem { get; init; }

    /// <summary>Тип носителя: определяет число параллельных читателей.</summary>
    public DiskKind DiskKind { get; init; } = DiskKind.Auto;

    /// <summary>Явное число потоков чтения; 0 — вычислить из <see cref="DiskKind"/>.</summary>
    public int MaxReaders { get; init; }

    /// <summary>Способ подтверждения точной копии. Выводится из <see cref="ConfirmBytewise"/>.</summary>
    public ExactConfirmation Confirmation => ConfirmBytewise ? ExactConfirmation.Bytewise : ExactConfirmation.Sha256;

    /// <summary>Проверяет параметры и бросает <see cref="ArgumentException"/> с понятным текстом.</summary>
    public void Validate()
    {
        if (Roots is null || Roots.Count == 0)
        {
            throw new ArgumentException("Не указано ни одной папки для проверки.", nameof(Roots));
        }

        if (MinBytes < 0)
        {
            throw new ArgumentException("Минимальный размер не может быть отрицательным.", nameof(MinBytes));
        }

        if (MaxBytes > 0 && MaxBytes < MinBytes)
        {
            throw new ArgumentException("Максимальный размер меньше минимального.", nameof(MaxBytes));
        }

        if (SimilarityThreshold is < 0 or > 64)
        {
            throw new ArgumentException("Порог сходства должен быть в диапазоне 0…64.", nameof(SimilarityThreshold));
        }
    }
}
