using DupFinder.Core.Abstractions;
using DupFinder.Core.Model;

namespace DupFinder.App.Services;

/// <summary>
/// Гоняет движок в фоне и отдаёт результаты пачками.
/// UI-поток здесь не участвует вообще: он только получает готовые списки (ТЗ §3).
/// </summary>
public sealed class ScanRunner
{
    /// <summary>Сколько строк накапливать, прежде чем отдать пачку наверх.</summary>
    public const int BatchRows = 500;

    private readonly IDuplicateScanner _scanner;

    public ScanRunner(IDuplicateScanner scanner) => _scanner = scanner;

    /// <summary>
    /// Запускает поиск. <paramref name="onBatch"/> вызывается из фонового потока —
    /// вызывающий сам решает, как попасть в Dispatcher.
    /// </summary>
    public Task<ScanSummary?> RunAsync(
        ScanOptions options,
        IProgress<ScanProgress> progress,
        Func<IReadOnlyList<DuplicateGroup>, Task> onBatch,
        CancellationToken ct) =>
        Task.Run(
            async () =>
            {
                ScanSummary? summary = null;
                var relay = new RelayProgress(progress, p => summary = p.Summary ?? summary);

                var batch = new List<DuplicateGroup>();
                var rows = 0;

                await foreach (var group in _scanner.ScanAsync(options, relay, ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    batch.Add(group);
                    rows += group.Items.Count;
                    if (rows >= BatchRows)
                    {
                        await onBatch(batch).ConfigureAwait(false);
                        batch = new List<DuplicateGroup>();
                        rows = 0;
                    }
                }

                if (batch.Count > 0)
                {
                    await onBatch(batch).ConfigureAwait(false);
                }

                return summary;
            },
            ct);

    /// <summary>Подсматривает итоги, не мешая основному получателю прогресса.</summary>
    private sealed class RelayProgress : IProgress<ScanProgress>
    {
        private readonly IProgress<ScanProgress> _inner;
        private readonly Action<ScanProgress> _peek;

        public RelayProgress(IProgress<ScanProgress> inner, Action<ScanProgress> peek)
        {
            _inner = inner;
            _peek = peek;
        }

        public void Report(ScanProgress value)
        {
            _peek(value);
            _inner.Report(value);
        }
    }
}
