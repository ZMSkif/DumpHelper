using DupFinder.Core.Model;

namespace DupFinder.Core.Hashing;

/// <summary>
/// Сколько файлов читать одновременно. На HDD случайный доступ убивает скорость,
/// поэтому там 1–2 потока, на SSD — по числу ядер (ТЗ §3).
/// </summary>
public static class ParallelismPlanner
{
    /// <summary>Число параллельных читателей для указанных настроек и набора путей.</summary>
    public static int Plan(ScanOptions options, IReadOnlyList<string> roots)
    {
        if (options.MaxReaders > 0)
        {
            return options.MaxReaders;
        }

        var kind = options.DiskKind == DiskKind.Auto ? Detect(roots) : options.DiskKind;
        return ForKind(kind);
    }

    /// <summary>Число читателей для известного типа носителя.</summary>
    public static int ForKind(DiskKind kind) => kind switch
    {
        DiskKind.Hdd => 2,
        DiskKind.Network => 2,
        DiskKind.Ssd => Math.Max(2, Environment.ProcessorCount),
        _ => Math.Max(2, Environment.ProcessorCount),
    };

    /// <summary>
    /// Грубая эвристика по <see cref="DriveInfo"/>: сетевые и сменные носители —
    /// заведомо медленные. Отличить SSD от HDD без WMI нельзя, поэтому для локальных
    /// дисков считаем SSD, а пользователь может переопределить это в настройках.
    /// </summary>
    public static DiskKind Detect(IReadOnlyList<string> roots)
    {
        var worst = DiskKind.Ssd;
        foreach (var root in roots)
        {
            var kind = DetectSingle(root);
            if (kind == DiskKind.Network)
            {
                return DiskKind.Network;
            }

            if (kind == DiskKind.Hdd)
            {
                worst = DiskKind.Hdd;
            }
        }

        return worst;
    }

    private static DiskKind DetectSingle(string root)
    {
        try
        {
            if (root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return DiskKind.Network;
            }

            var full = Path.GetFullPath(root);
            var drive = new DriveInfo(Path.GetPathRoot(full) ?? full);
            return drive.DriveType switch
            {
                DriveType.Network => DiskKind.Network,
                DriveType.Removable or DriveType.CDRom => DiskKind.Hdd,
                _ => DiskKind.Ssd,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return DiskKind.Ssd;
        }
    }
}
