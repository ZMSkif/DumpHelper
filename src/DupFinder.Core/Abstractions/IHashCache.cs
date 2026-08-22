using DupFinder.Core.Model;

namespace DupFinder.Core.Abstractions;

/// <summary>Что мы уже знаем про файл. Всё, кроме ключа, может отсутствовать.</summary>
public sealed record FileFingerprint
{
    public ulong? PartialHash { get; init; }

    public ulong? MidTailHash { get; init; }

    public string? FullHash { get; init; }

    public string? Sha256 { get; init; }

    public ulong? DHash { get; init; }

    public ulong? PHash { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public DateTime? DateTaken { get; init; }

    public string? Camera { get; init; }
}

/// <summary>
/// Кэш отпечатков: ключ — путь + размер + время изменения (ТЗ §4.1 п.7).
/// Реализация на SQLite появится на этапе 5; сейчас движок работает с <see cref="NullHashCache"/>.
/// </summary>
public interface IHashCache
{
    /// <summary>Возвращает отпечаток, если он посчитан для этой же версии файла.</summary>
    ValueTask<FileFingerprint?> TryGetAsync(FileEntry file, CancellationToken ct);

    /// <summary>Сохраняет (или дополняет) отпечаток файла.</summary>
    ValueTask SetAsync(FileEntry file, FileFingerprint fingerprint, CancellationToken ct);

    /// <summary>Сбрасывает накопленное на носитель.</summary>
    ValueTask FlushAsync(CancellationToken ct);
}

/// <summary>Кэш-заглушка: ничего не помнит.</summary>
public sealed class NullHashCache : IHashCache
{
    public static readonly NullHashCache Instance = new();

    public ValueTask<FileFingerprint?> TryGetAsync(FileEntry file, CancellationToken ct) =>
        new((FileFingerprint?)null);

    public ValueTask SetAsync(FileEntry file, FileFingerprint fingerprint, CancellationToken ct) => default;

    public ValueTask FlushAsync(CancellationToken ct) => default;
}
