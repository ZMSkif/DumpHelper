namespace DupFinder.Core.Diagnostics;

/// <summary>
/// Минимальный журнал для движка. Своя абстракция, чтобы Core не тянул
/// ни Serilog, ни WPF; интерфейс подключает настоящий журнал (ТЗ §2, §8).
/// </summary>
public interface IScanLog
{
    void Info(string message);

    void Warn(string message, Exception? error = null);

    void Error(string message, Exception? error = null);
}

/// <summary>Журнал, который ничего не пишет.</summary>
public sealed class NullScanLog : IScanLog
{
    public static readonly NullScanLog Instance = new();

    public void Info(string message)
    {
    }

    public void Warn(string message, Exception? error = null)
    {
    }

    public void Error(string message, Exception? error = null)
    {
    }
}
