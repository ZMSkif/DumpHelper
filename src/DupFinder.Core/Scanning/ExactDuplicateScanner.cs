using System.Diagnostics;
using System.Runtime.CompilerServices;
using DupFinder.Core.Abstractions;
using DupFinder.Core.Diagnostics;
using DupFinder.Core.Files;
using DupFinder.Core.Hashing;
using DupFinder.Core.Model;

namespace DupFinder.Core.Scanning;

/// <summary>
/// Конвейер «точные копии» (ТЗ §4.1): размер → 4 КБ → середина+хвост → полный XxHash128 →
/// подтверждение SHA-256 или побайтно. Каждая ступень дешевле следующей и убирает
/// подавляющую часть кандидатов.
/// </summary>
public sealed class ExactDuplicateScanner : IDuplicateScanner
{
    /// <summary>Сколько групп подтверждать за один заход, прежде чем отдать их наверх.</summary>
    private const int ConfirmChunkGroups = 32;

    private readonly IFileSource _source;
    private readonly IScanLog _log;

    public ExactDuplicateScanner(IFileSource source, IScanLog? log = null)
    {
        _source = source;
        _log = log ?? NullScanLog.Instance;
    }

    public async IAsyncEnumerable<DuplicateGroup> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        [EnumeratorCancellation] CancellationToken ct)
    {
        options.Validate();

        var clock = Stopwatch.StartNew();
        var throttle = new ProgressThrottle(progress);
        var hasher = new FileHasher(_source);
        var readers = ParallelismPlanner.Plan(options, options.Roots);
        var failed = new HashSet<string>(FileSystemFileSource.PathComparer);

        var collected = await CollectAsync(options, throttle, ct).ConfigureAwait(false);
        var groups = await SiftAsync(collected.Groups, hasher, throttle, readers, failed, ct).ConfigureAwait(false);

        var reference = OriginalSelector.NormalizeReference(options.ReferenceFolder);
        var groupId = 0;
        var itemsInGroups = 0;
        var redundantItems = 0;
        var redundantBytes = 0L;
        var confirmedTotal = groups.Sum(g => g.Count);
        var confirmedDone = 0;

        foreach (var chunk in Chunk(groups, ConfirmChunkGroups))
        {
            var confirmed = await ConfirmAsync(chunk, options, hasher, readers, failed, ct).ConfigureAwait(false);
            confirmedDone += chunk.Sum(g => g.Count);
            throttle.Report(
                ScanStage.Confirming,
                confirmedDone,
                confirmedTotal,
                "Подтверждаю совпадения",
                hasher.BytesRead);

            foreach (var group in confirmed)
            {
                var items = OriginalSelector.Order(group, options.OriginalRule, reference);
                itemsInGroups += items.Count;
                redundantItems += items.Count - 1;
                redundantBytes += items.Where(i => !i.IsOriginal).Sum(i => i.Length);
                yield return new DuplicateGroup(++groupId, MatchKind.ExactCopy, items);
            }
        }

        throttle.Report(new ScanProgress(
            ScanStage.Completed,
            confirmedTotal,
            confirmedTotal,
            "Готово",
            hasher.BytesRead)
        {
            Summary = new ScanSummary
            {
                FilesSeen = collected.FilesSeen,
                FilesConsidered = collected.FilesConsidered,
                EmptyFiles = collected.EmptyFiles,
                FailedFiles = failed.Count,
                Groups = groupId,
                ItemsInGroups = itemsInGroups,
                RedundantItems = redundantItems,
                RedundantBytes = redundantBytes,
                BytesRead = hasher.BytesRead,
                Elapsed = clock.Elapsed,
            },
        });
    }

    /// <summary>Обход папок и группировка по размеру (ТЗ §4.1 пп.1–2).</summary>
    private async Task<CollectResult> CollectAsync(ScanOptions options, ProgressThrottle throttle, CancellationToken ct)
    {
        var request = new FileEnumerationRequest(options.Roots, options.Recurse)
        {
            ExcludeFolders = options.ExcludeFolders,
            ExcludeMasks = options.ExcludeMasks,
            IncludeHidden = options.IncludeHidden,
            IncludeSystem = options.IncludeSystem,
        };

        var bySize = new Dictionary<long, List<FileEntry>>();
        var seen = 0;
        var considered = 0;
        var empty = 0;

        throttle.Report(ScanStage.Enumerating, 0, 0, "Собираю список файлов", 0);

        await foreach (var entry in _source.EnumerateAsync(request, ct).ConfigureAwait(false))
        {
            seen++;

            // Пустые файлы — отдельная категория, дубликатами не считаются (ТЗ §4.1 п.2).
            if (entry.Length == 0)
            {
                empty++;
            }
            else if (PassesFilters(entry, options))
            {
                considered++;
                if (!bySize.TryGetValue(entry.Length, out var bucket))
                {
                    bucket = new List<FileEntry>();
                    bySize[entry.Length] = bucket;
                }

                bucket.Add(entry);
            }

            if ((seen & 0x1FF) == 0)
            {
                throttle.Report(ScanStage.Enumerating, seen, 0, $"Собираю список файлов: {seen}", 0);
            }
        }

        var groups = bySize.Values.Where(g => g.Count > 1).ToList();
        throttle.Report(
            ScanStage.GroupingBySize,
            considered,
            considered,
            $"Кандидатов после отсева по размеру: {groups.Sum(g => g.Count)}",
            0);

        return new CollectResult(groups, seen, considered, empty);
    }

    /// <summary>Ступени частичных и полного хэшей (ТЗ §4.1 пп.3–5).</summary>
    private async Task<List<List<FileEntry>>> SiftAsync(
        List<List<FileEntry>> groups,
        FileHasher hasher,
        ProgressThrottle throttle,
        int readers,
        ISet<string> failed,
        CancellationToken ct)
    {
        var partialTotal = CountFiles(groups);
        groups = await HashRegrouper.RegroupAsync(
            groups,
            static _ => true,
            hasher.PartialAsync,
            readers,
            done => throttle.Report(ScanStage.PartialHash, done, partialTotal, "Быстрая проверка начала файлов", hasher.BytesRead),
            failed,
            _log,
            ct).ConfigureAwait(false);

        var midTailTotal = CountFiles(groups);
        groups = await HashRegrouper.RegroupAsync(
            groups,
            static g => g[0].Length > FileHasher.MidTailThreshold,
            hasher.MidTailAsync,
            readers,
            done => throttle.Report(ScanStage.MidTailHash, done, midTailTotal, "Проверка середины и конца файлов", hasher.BytesRead),
            failed,
            _log,
            ct).ConfigureAwait(false);

        var fullTotal = CountFiles(groups);
        groups = await HashRegrouper.RegroupAsync(
            groups,
            static _ => true,
            hasher.FullAsync,
            readers,
            done => throttle.Report(ScanStage.FullHash, done, fullTotal, "Полная сверка содержимого", hasher.BytesRead),
            failed,
            _log,
            ct).ConfigureAwait(false);

        return groups;
    }

    /// <summary>Финальное подтверждение: SHA-256 или побайтовое сравнение (ТЗ §4.1 п.6).</summary>
    private async Task<List<List<FileEntry>>> ConfirmAsync(
        IReadOnlyList<List<FileEntry>> chunk,
        ScanOptions options,
        FileHasher hasher,
        int readers,
        ISet<string> failed,
        CancellationToken ct)
    {
        if (options.Confirmation == ExactConfirmation.Sha256)
        {
            return await HashRegrouper.RegroupAsync(
                chunk,
                static _ => true,
                hasher.Sha256Async,
                readers,
                static _ => { },
                failed,
                _log,
                ct).ConfigureAwait(false);
        }

        var result = new List<List<FileEntry>>();
        foreach (var group in chunk)
        {
            result.AddRange(await SplitByContentAsync(group, hasher, failed, ct).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>Разбивает группу на подгруппы побайтовым сравнением с представителем.</summary>
    private async Task<List<List<FileEntry>>> SplitByContentAsync(
        List<FileEntry> group,
        FileHasher hasher,
        ISet<string> failed,
        CancellationToken ct)
    {
        var result = new List<List<FileEntry>>();
        var remaining = new List<FileEntry>(group);

        while (remaining.Count > 1)
        {
            var head = remaining[0];
            var same = new List<FileEntry> { head };
            var rest = new List<FileEntry>();

            for (var i = 1; i < remaining.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await hasher.AreEqualAsync(head, remaining[i], ct).ConfigureAwait(false))
                    {
                        same.Add(remaining[i]);
                    }
                    else
                    {
                        rest.Add(remaining[i]);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.Warn($"Файл пропущен при сравнении: {remaining[i].Path}", ex);
                    failed.Add(remaining[i].Path);
                }
            }

            if (same.Count > 1)
            {
                result.Add(same);
            }

            remaining = rest;
        }

        return result;
    }

    private static bool PassesFilters(FileEntry entry, ScanOptions options)
    {
        if (entry.Length < options.MinBytes)
        {
            return false;
        }

        if (options.MaxBytes > 0 && entry.Length > options.MaxBytes)
        {
            return false;
        }

        return FileKinds.Matches(options.Kinds, entry.Path);
    }

    private static int CountFiles(IReadOnlyList<List<FileEntry>> groups)
    {
        var total = 0;
        foreach (var group in groups)
        {
            total += group.Count;
        }

        return total;
    }

    private static IEnumerable<IReadOnlyList<List<FileEntry>>> Chunk(List<List<FileEntry>> groups, int size)
    {
        for (var i = 0; i < groups.Count; i += size)
        {
            yield return groups.GetRange(i, Math.Min(size, groups.Count - i));
        }
    }

    private sealed record CollectResult(List<List<FileEntry>> Groups, int FilesSeen, int FilesConsidered, int EmptyFiles);
}
