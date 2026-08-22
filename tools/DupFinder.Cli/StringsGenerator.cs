using System.Xml.Linq;

namespace DupFinder.Cli;

/// <summary>
/// Генерирует строго типизированный доступ к строкам интерфейса из Strings.resx.
/// Штатный ResXFileCodeGenerator работает только внутри Visual Studio, а собираемся
/// мы через `dotnet build`, поэтому генерируем сами — и на C#, чтобы в репозитории
/// не заводить второй язык ради одного скрипта.
/// </summary>
internal static class StringsGenerator
{
    private const string Header = """
        using System.Globalization;
        using System.Resources;

        namespace DupFinder.App.Resources;

        /// <summary>
        /// Доступ к строкам интерфейса. Файл сгенерирован из Strings.resx командой
        /// `dupfinder-cli gen-strings` — правьте .resx, а не этот файл.
        /// Локализация заведена с первого дня, как требует ТЗ §11.
        /// </summary>
        public static class Strings
        {
            private static readonly ResourceManager Manager =
                new("DupFinder.App.Resources.Strings", typeof(Strings).Assembly);

            /// <summary>Язык интерфейса. Смена подхватывается при следующем чтении строки.</summary>
            public static CultureInfo? Culture { get; set; }

            /// <summary>Строка по ключу; если её нет — сам ключ, чтобы окно не падало.</summary>
            public static string Get(string key) => Manager.GetString(key, Culture) ?? key;
        """;

    /// <summary>Перегенерирует Strings.cs. Возвращает код возврата для консоли.</summary>
    internal static int Run(string[] args)
    {
        var root = args.Length > 0 ? args[0] : FindRepositoryRoot();
        if (root is null)
        {
            Console.Error.WriteLine("Не нашёл корень репозитория (DupFinder.sln). Укажите его первым аргументом.");
            return 1;
        }

        var resx = Path.Combine(root, "src", "DupFinder.App", "Resources", "Strings.resx");
        var target = Path.Combine(root, "src", "DupFinder.App", "Resources", "Strings.cs");
        if (!File.Exists(resx))
        {
            Console.Error.WriteLine($"Не найден файл ресурсов: {resx}");
            return 1;
        }

        var keys = ReadKeys(resx);
        var duplicates = keys.GroupBy(k => k.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            Console.Error.WriteLine($"Повторяющиеся ключи: {string.Join(", ", duplicates)}");
            return 1;
        }

        var writer = new StringWriter { NewLine = "\n" };
        writer.Write(Header.ReplaceLineEndings("\n"));
        writer.WriteLine();
        foreach (var (name, value) in keys)
        {
            writer.WriteLine();
            writer.WriteLine($"    /// <summary>{EscapeDoc(value)}</summary>");
            writer.WriteLine($"    public static string {name} => Get(nameof({name}));");
        }

        writer.Write("}\n");
        File.WriteAllText(target, writer.ToString());

        Console.WriteLine($"{Path.GetRelativePath(root, target)}: {keys.Count} строк(и)");
        CheckTranslations(root, keys.Select(k => k.Name).ToHashSet(StringComparer.Ordinal));
        return 0;
    }

    /// <summary>Сообщает про ключи, которые есть в одном языке и потеряны в другом.</summary>
    private static void CheckTranslations(string root, HashSet<string> expected)
    {
        var english = Path.Combine(root, "src", "DupFinder.App", "Resources", "Strings.en.resx");
        if (!File.Exists(english))
        {
            return;
        }

        var actual = ReadKeys(english).Select(k => k.Name).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Except(actual).ToList();
        var extra = actual.Except(expected).ToList();

        if (missing.Count == 0 && extra.Count == 0)
        {
            Console.WriteLine("Strings.en.resx: полный перевод.");
            return;
        }

        if (missing.Count > 0)
        {
            Console.WriteLine($"Strings.en.resx: нет перевода для {string.Join(", ", missing)}");
        }

        if (extra.Count > 0)
        {
            Console.WriteLine($"Strings.en.resx: лишние ключи {string.Join(", ", extra)}");
        }
    }

    private static List<(string Name, string Value)> ReadKeys(string resx) => XDocument
        .Load(resx)
        .Root!
        .Elements("data")
        .Where(d => d.Attribute("name") is not null)
        .Select(d => (d.Attribute("name")!.Value, d.Element("value")?.Value ?? string.Empty))
        .ToList();

    private static string EscapeDoc(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .ReplaceLineEndings(" ")
        .Trim();

    /// <summary>Идёт вверх от текущей папки, пока не найдёт DupFinder.sln.</summary>
    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DupFinder.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
