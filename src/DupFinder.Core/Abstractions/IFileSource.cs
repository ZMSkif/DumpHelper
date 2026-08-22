using DupFinder.Core.Model;

namespace DupFinder.Core.Abstractions;

/// <summary>Как именно будут читать поток — влияет на размер буфера и подсказку ОС.</summary>
public enum FileReadHint
{
    /// <summary>Файл читается целиком подряд: большой буфер, SequentialScan.</summary>
    Sequential,

    /// <summary>Читаются короткие куски в разных местах: буфер не нужен.</summary>
    Ranged,
}

/// <summary>Что и где обходить.</summary>
public sealed record FileEnumerationRequest(IReadOnlyList<string> Roots, bool Recurse)
{
    /// <summary>Папки, в которые не заходить (сравнение по префиксу пути).</summary>
    public IReadOnlyList<string> ExcludeFolders { get; init; } = Array.Empty<string>();

    /// <summary>Маски имён файлов, которые пропускать (например <c>*.tmp</c>).</summary>
    public IReadOnlyList<string> ExcludeMasks { get; init; } = Array.Empty<string>();

    /// <summary>Включать скрытые.</summary>
    public bool IncludeHidden { get; init; }

    /// <summary>Включать системные.</summary>
    public bool IncludeSystem { get; init; }
}

/// <summary>
/// Источник файлов. Абстракция нужна, чтобы движок не знал, что файлы локальные
/// (задел на будущее, ТЗ §11), и чтобы тесты работали без диска.
/// </summary>
public interface IFileSource
{
    /// <summary>Обходит корни, отдавая файлы по мере нахождения.</summary>
    IAsyncEnumerable<FileEntry> EnumerateAsync(FileEnumerationRequest request, CancellationToken ct);

    /// <summary>Открывает файл на чтение.</summary>
    Stream OpenRead(string path, FileReadHint hint);

    /// <summary>Есть ли такой файл.</summary>
    bool FileExists(string path);
}
