using DupFinder.Core.Files;
using DupFinder.Core.Model;

namespace DupFinder.Core.Scanning;

/// <summary>Размеры картинки в пикселях, если они известны.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public long Area => (long)Width * Height;

    public static PixelSize Unknown => default;
}

/// <summary>
/// Выбор оригинала (ТЗ §4.4). Файл из папки-эталона всегда впереди и всегда защищён;
/// группа никогда не остаётся без оригинала — им становится первый элемент.
/// </summary>
public static class OriginalSelector
{
    /// <summary>Упорядочивает файлы группы и помечает оригинал и защищённые файлы.</summary>
    public static IReadOnlyList<DuplicateItem> Order(
        IReadOnlyList<FileEntry> files,
        OriginalRule rule,
        string? referenceFolder,
        Func<FileEntry, PixelSize>? pixels = null)
    {
        if (files.Count == 0)
        {
            return Array.Empty<DuplicateItem>();
        }

        var reference = NormalizeReference(referenceFolder);
        var measure = pixels ?? (_ => PixelSize.Unknown);

        var ordered = files
            .Select(f => (File: f, Protected: IsInReference(f, reference)))
            .OrderBy(x => x.Protected ? 0 : 1)
            .ThenBy(x => x.File, Comparer(rule, measure))
            .ToList();

        var result = new List<DuplicateItem>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            result.Add(new DuplicateItem(ordered[i].File)
            {
                IsOriginal = i == 0,
                IsProtected = ordered[i].Protected,
                Kind = FileKinds.FromPath(ordered[i].File.Path),
                Width = measure(ordered[i].File).Width,
                Height = measure(ordered[i].File).Height,
            });
        }

        return result;
    }

    /// <summary>Лежит ли файл в папке-эталоне.</summary>
    public static bool IsInReference(FileEntry file, string? normalizedReference) =>
        normalizedReference is not null && FileSystemFileSource.IsUnder(file.Path, normalizedReference);

    /// <summary>Приводит путь папки-эталона к виду, пригодному для сравнения по префиксу.</summary>
    public static string? NormalizeReference(string? referenceFolder)
    {
        if (string.IsNullOrWhiteSpace(referenceFolder))
        {
            return null;
        }

        var full = Path.GetFullPath(referenceFolder);
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? full : trimmed;
    }

    private static IComparer<FileEntry> Comparer(OriginalRule rule, Func<FileEntry, PixelSize> pixels) => rule switch
    {
        OriginalRule.Newest => Build(f => -f.LastWriteUtc.Ticks),
        OriginalRule.ShortestPath => Build(f => f.Path.Length),
        OriginalRule.HighestResolution => Build(f => -pixels(f).Area, f => -f.Length),
        OriginalRule.LargestFile => Build(f => -f.Length),
        OriginalRule.SourceFormat => Build(f => FileKinds.RankOfFormat(f.Path), f => -f.Length),
        _ => Build(f => f.LastWriteUtc.Ticks),
    };

    private static IComparer<FileEntry> Build(Func<FileEntry, long> primary, Func<FileEntry, long>? secondary = null) =>
        System.Collections.Generic.Comparer<FileEntry>.Create((a, b) =>
        {
            var byPrimary = primary(a).CompareTo(primary(b));
            if (byPrimary != 0)
            {
                return byPrimary;
            }

            if (secondary is not null)
            {
                var bySecondary = secondary(a).CompareTo(secondary(b));
                if (bySecondary != 0)
                {
                    return bySecondary;
                }
            }

            // Стабильность: одинаковые по правилу файлы всегда упорядочены одинаково.
            return string.CompareOrdinal(a.Path, b.Path);
        });
}
