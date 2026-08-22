using System.Text;
using System.Text.RegularExpressions;

namespace DupFinder.Core.Files;

/// <summary>
/// Маски вида <c>*.tmp</c>, <c>~$*</c>, <c>thumbs.db</c>.
/// Маска с разделителем пути проверяется по всему пути, без — только по имени файла.
/// </summary>
public sealed class GlobMatcher
{
    private readonly Regex[] _nameRules;
    private readonly Regex[] _pathRules;

    public GlobMatcher(IEnumerable<string> masks)
    {
        var names = new List<Regex>();
        var paths = new List<Regex>();
        foreach (var mask in masks)
        {
            if (string.IsNullOrWhiteSpace(mask))
            {
                continue;
            }

            var trimmed = mask.Trim();
            var regex = new Regex(ToPattern(trimmed), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (trimmed.Contains('/') || trimmed.Contains('\\'))
            {
                paths.Add(regex);
            }
            else
            {
                names.Add(regex);
            }
        }

        _nameRules = names.ToArray();
        _pathRules = paths.ToArray();
    }

    /// <summary>Матчер, который никогда ничего не исключает.</summary>
    public static GlobMatcher Empty { get; } = new(Array.Empty<string>());

    /// <summary>Есть ли вообще правила.</summary>
    public bool IsEmpty => _nameRules.Length == 0 && _pathRules.Length == 0;

    /// <summary>Подпадает ли файл под одну из масок.</summary>
    public bool IsMatch(string fullPath)
    {
        if (IsEmpty)
        {
            return false;
        }

        if (_nameRules.Length > 0)
        {
            var name = Path.GetFileName(fullPath);
            foreach (var rule in _nameRules)
            {
                if (rule.IsMatch(name))
                {
                    return true;
                }
            }
        }

        foreach (var rule in _pathRules)
        {
            if (rule.IsMatch(fullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToPattern(string mask)
    {
        var sb = new StringBuilder("^");
        foreach (var ch in mask)
        {
            switch (ch)
            {
                case '*':
                    sb.Append(".*");
                    break;
                case '?':
                    sb.Append('.');
                    break;
                case '/':
                case '\\':
                    sb.Append("[/\\\\]");
                    break;
                default:
                    sb.Append(Regex.Escape(ch.ToString()));
                    break;
            }
        }

        return sb.Append('$').ToString();
    }
}
