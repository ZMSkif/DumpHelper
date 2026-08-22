using System.Runtime.CompilerServices;
using DupFinder.Core.Abstractions;
using DupFinder.Core.Diagnostics;
using DupFinder.Core.Model;

namespace DupFinder.Core.Files;

/// <summary>
/// Обычная файловая система. Обход итеративный (без рекурсии по стеку вызовов),
/// точки повторного разбора пропускаются — иначе симлинк на родителя даёт бесконечный цикл (ТЗ §4.1).
/// </summary>
public sealed class FileSystemFileSource : IFileSource
{
    private const int SequentialBufferSize = 1 << 20; // 1 МБ, как требует ТЗ §3
    private readonly IScanLog _log;

    public FileSystemFileSource(IScanLog? log = null) => _log = log ?? NullScanLog.Instance;

    public async IAsyncEnumerable<FileEntry> EnumerateAsync(
        FileEnumerationRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var masks = new GlobMatcher(request.ExcludeMasks);
        var excluded = NormalizeFolders(request.ExcludeFolders);
        var visited = new HashSet<string>(PathComparer);
        var pending = new Stack<string>();

        foreach (var root in NormalizeRoots(request.Roots))
        {
            pending.Push(root);
        }

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();

            if (!visited.Add(dir) || IsExcluded(dir, excluded))
            {
                continue;
            }

            List<DirectoryWalkItem> items;
            try
            {
                items = ReadDirectory(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn($"Папка пропущена: {dir}", ex);
                continue;
            }

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if (!request.IncludeHidden && (item.Attributes & FileAttributes.Hidden) != 0)
                {
                    continue;
                }

                if (!request.IncludeSystem && (item.Attributes & FileAttributes.System) != 0)
                {
                    continue;
                }

                if (item.IsDirectory)
                {
                    if (request.Recurse)
                    {
                        pending.Push(item.Path);
                    }

                    continue;
                }

                if (masks.IsMatch(item.Path))
                {
                    continue;
                }

                yield return new FileEntry(item.Path, item.Length, item.LastWriteUtc);
            }

            // Обход диска — не мгновенная операция; даём вызывающему шанс обработать отмену.
            await Task.Yield();
        }
    }

    public Stream OpenRead(string path, FileReadHint hint)
    {
        var options = hint == FileReadHint.Sequential
            ? FileOptions.SequentialScan | FileOptions.Asynchronous
            : FileOptions.Asynchronous;
        var buffer = hint == FileReadHint.Sequential ? SequentialBufferSize : 4096;
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, buffer, options);
    }

    public bool FileExists(string path) => File.Exists(path);

    public FileEntry? Describe(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileEntry(info.FullName, info.Length, info.LastWriteTimeUtc) : null;
    }

    internal static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Убирает дубли и вложенные корни: иначе один файл попадёт в список дважды.</summary>
    internal static IReadOnlyList<string> NormalizeRoots(IReadOnlyList<string> roots)
    {
        var full = roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(TrimPath)
            .Distinct(PathComparer)
            .OrderBy(r => r.Length)
            .ToList();

        var result = new List<string>();
        foreach (var candidate in full)
        {
            if (!result.Any(kept => IsUnder(candidate, kept)))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeFolders(IReadOnlyList<string> folders) => folders
        .Where(f => !string.IsNullOrWhiteSpace(f))
        .Select(TrimPath)
        .ToList();

    private static bool IsExcluded(string dir, IReadOnlyList<string> excluded)
    {
        foreach (var candidate in excluded)
        {
            if (IsUnder(dir, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Лежит ли <paramref name="path"/> внутри <paramref name="parent"/> (или совпадает с ним).</summary>
    internal static bool IsUnder(string path, string parent)
    {
        if (path.Equals(parent, PathComparison))
        {
            return true;
        }

        return path.StartsWith(parent, PathComparison)
            && path.Length > parent.Length
            && (path[parent.Length] == Path.DirectorySeparatorChar || path[parent.Length] == Path.AltDirectorySeparatorChar);
    }

    private static string TrimPath(string path)
    {
        var full = Path.GetFullPath(path);
        // У корня диска (C:\) разделитель — часть пути, его убирать нельзя.
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? full : trimmed;
    }

    private static List<DirectoryWalkItem> ReadDirectory(string dir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = 0,
        };

        var enumerable = new System.IO.Enumeration.FileSystemEnumerable<DirectoryWalkItem>(
            dir,
            static (ref System.IO.Enumeration.FileSystemEntry entry) => entry.IsDirectory
                ? new DirectoryWalkItem(entry.ToFullPath(), true, 0, default, entry.Attributes)
                : new DirectoryWalkItem(entry.ToFullPath(), false, entry.Length, entry.LastWriteTimeUtc.UtcDateTime, entry.Attributes),
            options);

        return enumerable.ToList();
    }

    private readonly record struct DirectoryWalkItem(
        string Path,
        bool IsDirectory,
        long Length,
        DateTime LastWriteUtc,
        FileAttributes Attributes);
}
