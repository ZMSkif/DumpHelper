using System.Diagnostics;
using DupFinder.Core.Model;

namespace DupFinder.Core.Scanning;

/// <summary>
/// Пропускает не больше 10 сообщений в секунду, чтобы не заваливать Dispatcher (ТЗ §3).
/// Завершающие стадии проходят всегда.
/// </summary>
public sealed class ProgressThrottle
{
    private readonly IProgress<ScanProgress>? _inner;
    private readonly long _minTicks;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastTicks;
    private bool _reportedOnce;

    public ProgressThrottle(IProgress<ScanProgress>? inner, TimeSpan? interval = null)
    {
        _inner = inner;
        _minTicks = (long)((interval ?? TimeSpan.FromMilliseconds(100)).TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>Отправляет снимок, если пришло время.</summary>
    public void Report(ScanProgress progress)
    {
        if (_inner is null)
        {
            return;
        }

        var isFinal = progress.Stage is ScanStage.Completed or ScanStage.Cancelled;
        var now = _clock.ElapsedTicks;
        if (!isFinal && _reportedOnce && now - _lastTicks < _minTicks)
        {
            return;
        }

        _lastTicks = now;
        _reportedOnce = true;
        _inner.Report(progress);
    }

    /// <summary>Короткая форма.</summary>
    public void Report(ScanStage stage, int done, int total, string message, long bytesRead) =>
        Report(new ScanProgress(stage, done, total, message, bytesRead));
}
