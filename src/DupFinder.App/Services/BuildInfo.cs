using System.Reflection;

namespace DupFinder.App.Services;

/// <summary>
/// Из чего собрано это приложение. Данные вшиваются при сборке из git
/// (см. Directory.Build.targets), поэтому по запущенному exe всегда видно,
/// какому состоянию репозитория он соответствует.
/// </summary>
public static class BuildInfo
{
    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    /// <summary>Версия вида «0.3.0» — без служебного хвоста с коммитом.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Полный SHA коммита; пустая строка, если собирали не из репозитория.</summary>
    public static string Commit { get; } = ReadMetadata("GitCommit");

    /// <summary>Ветка, из которой собирали.</summary>
    public static string Branch { get; } = ReadMetadata("GitBranch");

    /// <summary>Ближайший тег или короткий SHA; «-dirty» означает несохранённые правки.</summary>
    public static string Describe { get; } = ReadMetadata("GitDescribe");

    /// <summary>Коммит в коротком виде — столько, сколько влезает в интерфейс.</summary>
    public static string ShortCommit => Commit.Length >= 12 ? Commit[..12] : Commit;

    /// <summary>Собрано ли из репозитория с несохранёнными правками.</summary>
    public static bool IsDirty => Describe.EndsWith("-dirty", StringComparison.Ordinal);

    /// <summary>Короткая подпись для окна: «0.3.0 · fcb5c2718479».</summary>
    public static string ShortLabel => Commit.Length == 0
        ? Version
        : $"{Version} · {ShortCommit}{(IsDirty ? " (с правками)" : string.Empty)}";

    /// <summary>Развёрнутая строка для журнала.</summary>
    public static string FullLabel => Commit.Length == 0
        ? $"версия {Version}"
        : $"версия {Version}, коммит {Commit}, ветка {Branch}{(IsDirty ? ", есть несохранённые правки" : string.Empty)}";

    private static string ReadVersion()
    {
        var informational = Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // «0.3.0+fcb5c2718479» → «0.3.0»: коммит показываем отдельно.
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? informational[..plus] : informational;
    }

    private static string ReadMetadata(string key) => Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal))?
        .Value ?? string.Empty;
}
