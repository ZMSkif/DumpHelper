using System.Runtime.Versioning;

namespace DupFinder.Core.Files;

/// <summary>
/// Папки, из которых нельзя ничего удалять. Дубликаты внутри Windows и
/// установленных программ — обычное дело (общие библиотеки, кэши установщика),
/// и удаление такого «дубликата» ломает систему или программу.
/// Файлы отсюда показываются, но помечаются защищёнными наравне с папкой-эталоном.
/// </summary>
public static class SystemFolders
{
    private static readonly string[] Roots = Build();

    /// <summary>Список защищённых корней. Пуст, если система их не сообщила.</summary>
    public static IReadOnlyList<string> Protected => Roots;

    /// <summary>Лежит ли файл в системной папке.</summary>
    public static bool IsProtected(string path)
    {
        if (Roots.Length == 0 || string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var root in Roots)
        {
            if (FileSystemFileSource.IsUnder(path, root))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Build()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        return BuildWindows();
    }

    [SupportedOSPlatform("windows")]
    private static string[] BuildWindows()
    {
        var folders = new[]
        {
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.SystemX86,
        };

        var result = new List<string>();
        foreach (var folder in folders)
        {
            string path;
            try
            {
                path = Environment.GetFolderPath(folder);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length > 0 && !result.Contains(trimmed, FileSystemFileSource.PathComparer))
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }
}
