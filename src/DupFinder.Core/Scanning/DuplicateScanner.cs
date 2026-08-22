using DupFinder.Core.Abstractions;
using DupFinder.Core.Diagnostics;
using DupFinder.Core.Model;

namespace DupFinder.Core.Scanning;

/// <summary>
/// Точка входа для интерфейса: выбирает движок по режиму.
/// Режимы «та же съёмка» и «похожие» регистрируются из DupFinder.Imaging (этап 4),
/// поэтому Core не зависит от WIC.
/// </summary>
public sealed class DuplicateScanner : IDuplicateScanner
{
    private readonly IReadOnlyDictionary<ScanMode, IDuplicateScanner> _scanners;

    public DuplicateScanner(IReadOnlyDictionary<ScanMode, IDuplicateScanner> scanners) => _scanners = scanners;

    /// <summary>Движок, умеющий только точные копии.</summary>
    public static DuplicateScanner CreateDefault(IFileSource source, IScanLog? log = null) => new(
        new Dictionary<ScanMode, IDuplicateScanner>
        {
            [ScanMode.Exact] = new ExactDuplicateScanner(source, log),
        });

    /// <summary>Доступен ли режим в текущей сборке.</summary>
    public bool Supports(ScanMode mode) => _scanners.ContainsKey(mode);

    public IAsyncEnumerable<DuplicateGroup> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        if (!_scanners.TryGetValue(options.Mode, out var scanner))
        {
            throw new NotSupportedException($"Режим поиска «{Describe(options.Mode)}» пока недоступен.");
        }

        return scanner.ScanAsync(options, progress, ct);
    }

    private static string Describe(ScanMode mode) => mode switch
    {
        ScanMode.Exact => "точные копии",
        ScanMode.SameShot => "та же съёмка",
        ScanMode.Similar => "визуально похожие",
        _ => mode.ToString(),
    };
}
