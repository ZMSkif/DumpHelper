using DupFinder.Core.Diagnostics;
using Serilog;

namespace DupFinder.App.Services;

/// <summary>Мост между журналом движка и Serilog: Core не должен знать про Serilog (ТЗ §8).</summary>
public sealed class SerilogScanLog : IScanLog
{
    private readonly ILogger _logger;

    public SerilogScanLog(ILogger logger) => _logger = logger;

    public void Info(string message) => _logger.Information("{Message}", message);

    public void Warn(string message, Exception? error = null) => _logger.Warning(error, "{Message}", message);

    public void Error(string message, Exception? error = null) => _logger.Error(error, "{Message}", message);
}
