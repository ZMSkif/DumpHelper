namespace DupFinder.Core.Model;

/// <summary>Снимок прогресса. Отдаётся не чаще 10 раз в секунду (ТЗ §3).</summary>
public sealed record ScanProgress(ScanStage Stage, int Done, int Total, string Message, long BytesRead)
{
    /// <summary>Заполняется только на завершающей стадии.</summary>
    public ScanSummary? Summary { get; init; }

    /// <summary>Доля выполнения 0…1; null, если общее число неизвестно.</summary>
    public double? Fraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : null;
}

/// <summary>Итоги сканирования — для строки статистики в интерфейсе (ТЗ §5).</summary>
public sealed record ScanSummary
{
    /// <summary>Сколько файлов встретилось при обходе.</summary>
    public int FilesSeen { get; init; }

    /// <summary>Сколько файлов прошло фильтры и участвовало в поиске.</summary>
    public int FilesConsidered { get; init; }

    /// <summary>Пустые файлы: отдельная категория, дубликатами не считаются (ТЗ §4.1 п.2).</summary>
    public int EmptyFiles { get; init; }

    /// <summary>Файлы, которые не удалось прочитать.</summary>
    public int FailedFiles { get; init; }

    /// <summary>Найдено групп.</summary>
    public int Groups { get; init; }

    /// <summary>Файлов внутри групп.</summary>
    public int ItemsInGroups { get; init; }

    /// <summary>Лишних копий (файлы групп без оригиналов).</summary>
    public int RedundantItems { get; init; }

    /// <summary>Сколько байт освободится, если удалить все лишние копии.</summary>
    public long RedundantBytes { get; init; }

    /// <summary>Сколько байт прочитано с диска.</summary>
    public long BytesRead { get; init; }

    /// <summary>Время работы.</summary>
    public TimeSpan Elapsed { get; init; }
}
