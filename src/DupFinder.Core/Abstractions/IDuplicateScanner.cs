using DupFinder.Core.Model;

namespace DupFinder.Core.Abstractions;

/// <summary>
/// Движок поиска дубликатов. Группы отдаются по мере готовности,
/// чтобы интерфейс показывал их не дожидаясь конца (ТЗ §4.7).
/// </summary>
public interface IDuplicateScanner
{
    IAsyncEnumerable<DuplicateGroup> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken ct);
}
