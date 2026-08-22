namespace DupFinder.App.Services;

/// <summary>Где приложение хранит свои файлы (ТЗ §2, §6).</summary>
public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\DupFinder</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DupFinder");

    /// <summary>Папка журналов.</summary>
    public static string Logs => Path.Combine(Root, "logs");

    /// <summary>Файл кэша отпечатков (появится на этапе 5).</summary>
    public static string CacheDatabase => Path.Combine(Root, "cache.db");

    /// <summary>Настройки интерфейса.</summary>
    public static string Settings => Path.Combine(Root, "settings.json");

    /// <summary>Журнал того, что программа сделала с файлами.</summary>
    public static string OperationJournal => Path.Combine(Root, "operations.jsonl");

    /// <summary>Профили сканирования.</summary>
    public static string Profiles => Path.Combine(Root, "profiles");

    /// <summary>Создаёт папки, если их ещё нет.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
    }
}
