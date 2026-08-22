using System.Collections.Concurrent;
using DupFinder.Core.Diagnostics;
using DupFinder.Core.Model;

namespace DupFinder.Core.Scanning;

/// <summary>
/// Одна ступень отсева: посчитать хэш для файлов участвующих групп и разбить группы по нему.
/// Вынесено отдельно, потому что все ступени конвейера отличаются только функцией хэша.
/// </summary>
internal static class HashRegrouper
{
    /// <summary>
    /// Пересобирает группы. Группы, для которых <paramref name="participates"/> вернул false,
    /// проходят ступень без изменений. Файлы, которые не удалось прочитать, выбывают.
    /// </summary>
    internal static async Task<List<List<FileEntry>>> RegroupAsync<TKey>(
        IReadOnlyList<List<FileEntry>> groups,
        Func<List<FileEntry>, bool> participates,
        Func<FileEntry, CancellationToken, Task<TKey>> hash,
        int maxReaders,
        Action<int> onProgress,
        ISet<string> failed,
        IScanLog log,
        CancellationToken ct)
        where TKey : notnull
    {
        var participating = new List<List<FileEntry>>();
        var untouched = new List<List<FileEntry>>();
        foreach (var group in groups)
        {
            (participates(group) ? participating : untouched).Add(group);
        }

        var candidates = participating.SelectMany(g => g).ToArray();
        if (candidates.Length == 0)
        {
            return groups.ToList();
        }

        var hashes = new ConcurrentDictionary<string, TKey>(FileSystemPathComparer);
        var done = 0;

        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions { MaxDegreeOfParallelism = maxReaders, CancellationToken = ct },
            async (file, token) =>
            {
                try
                {
                    hashes[file.Path] = await hash(file, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    log.Warn($"Файл пропущен: {file.Path}", ex);
                    lock (failed)
                    {
                        failed.Add(file.Path);
                    }
                }

                onProgress(Interlocked.Increment(ref done));
            }).ConfigureAwait(false);

        var result = new List<List<FileEntry>>(untouched);
        foreach (var group in participating)
        {
            var buckets = new Dictionary<TKey, List<FileEntry>>();
            foreach (var file in group)
            {
                if (!hashes.TryGetValue(file.Path, out var key))
                {
                    continue;
                }

                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<FileEntry>();
                    buckets[key] = bucket;
                }

                bucket.Add(file);
            }

            foreach (var bucket in buckets.Values)
            {
                if (bucket.Count > 1)
                {
                    result.Add(bucket);
                }
            }
        }

        return result;
    }

    private static StringComparer FileSystemPathComparer => Files.FileSystemFileSource.PathComparer;
}
