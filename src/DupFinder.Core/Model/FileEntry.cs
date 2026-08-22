namespace DupFinder.Core.Model;

/// <summary>
/// Лёгкое описание файла. Специально не <see cref="System.IO.FileInfo"/>:
/// на 100 000 файлов разница в памяти существенная (ТЗ §3).
/// </summary>
public sealed record FileEntry(string Path, long Length, DateTime LastWriteUtc)
{
    /// <summary>Имя файла с расширением.</summary>
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Расширение в нижнем регистре, с точкой. Пустая строка, если расширения нет.</summary>
    public string Extension => System.IO.Path.GetExtension(Path).ToLowerInvariant();

    /// <summary>Папка, в которой лежит файл.</summary>
    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}
