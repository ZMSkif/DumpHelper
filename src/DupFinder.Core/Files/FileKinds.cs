using DupFinder.Core.Model;

namespace DupFinder.Core.Files;

/// <summary>Расширение файла → категория. Список взят из прототипа и дополнен RAW-форматами.</summary>
public static class FileKinds
{
    private static readonly string[] PhotoExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".ico",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf", ".srw", ".pef",
    };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp",
    };

    private static readonly string[] AudioExtensions =
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma",
    };

    private static readonly string[] DocumentExtensions =
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".rtf", ".csv", ".odt",
    };

    private static readonly string[] ArchiveExtensions =
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".iso",
    };

    /// <summary>Форматы, которые считаются «исходными» и выигрывают у JPG (ТЗ §4.4).</summary>
    private static readonly Dictionary<string, int> FormatRank = new(StringComparer.OrdinalIgnoreCase)
    {
        [".dng"] = 0, [".cr2"] = 0, [".cr3"] = 0, [".nef"] = 0, [".arw"] = 0,
        [".orf"] = 0, [".rw2"] = 0, [".raf"] = 0, [".srw"] = 0, [".pef"] = 0,
        [".heic"] = 1, [".heif"] = 1,
        [".tif"] = 2, [".tiff"] = 2,
        [".png"] = 3,
        [".webp"] = 4,
        [".jpg"] = 5, [".jpeg"] = 5,
    };

    private static readonly Dictionary<string, FileKind> Map = Build();

    private static Dictionary<string, FileKind> Build()
    {
        var map = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in PhotoExtensions)
        {
            map[ext] = FileKind.Photo;
        }

        foreach (var ext in VideoExtensions)
        {
            map[ext] = FileKind.Video;
        }

        foreach (var ext in AudioExtensions)
        {
            map[ext] = FileKind.Audio;
        }

        foreach (var ext in DocumentExtensions)
        {
            map[ext] = FileKind.Document;
        }

        foreach (var ext in ArchiveExtensions)
        {
            map[ext] = FileKind.Archive;
        }

        return map;
    }

    /// <summary>Категория по пути или расширению.</summary>
    public static FileKind FromPath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Length > 0 && Map.TryGetValue(ext, out var kind) ? kind : FileKind.Other;
    }

    /// <summary>Проходит ли файл фильтр по категориям.</summary>
    public static bool Matches(FileKindFilter filter, string path)
    {
        if (filter == FileKindFilter.All)
        {
            return true;
        }

        return (filter & ToFlag(FromPath(path))) != 0;
    }

    /// <summary>Ранг формата: чем меньше, тем «исходнее». Неизвестные форматы — в конец.</summary>
    public static int RankOfFormat(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Length > 0 && FormatRank.TryGetValue(ext, out var rank) ? rank : 9;
    }

    /// <summary>Расширения, которые считаются фотографиями.</summary>
    public static IReadOnlyCollection<string> Photo => PhotoExtensions;

    private static FileKindFilter ToFlag(FileKind kind) => kind switch
    {
        FileKind.Photo => FileKindFilter.Photo,
        FileKind.Video => FileKindFilter.Video,
        FileKind.Audio => FileKindFilter.Audio,
        FileKind.Document => FileKindFilter.Document,
        FileKind.Archive => FileKindFilter.Archive,
        _ => FileKindFilter.Other,
    };
}
