namespace DupFinder.Core.Model;

/// <summary>Группа совпавших файлов. Ровно один элемент помечен <see cref="DuplicateItem.IsOriginal"/>.</summary>
public sealed record DuplicateGroup(int Id, MatchKind Kind, IReadOnlyList<DuplicateItem> Items)
{
    /// <summary>Оригинал группы.</summary>
    public DuplicateItem Original => Items.First(i => i.IsOriginal);

    /// <summary>Лишние копии.</summary>
    public IEnumerable<DuplicateItem> Copies => Items.Where(i => !i.IsOriginal);

    /// <summary>Сколько байт освободится, если удалить все копии этой группы.</summary>
    public long RedundantBytes => Copies.Sum(i => i.Length);
}

/// <summary>Файл внутри группы.</summary>
public sealed record DuplicateItem(FileEntry File)
{
    /// <summary>Полный путь.</summary>
    public string Path => File.Path;

    /// <summary>Размер в байтах.</summary>
    public long Length => File.Length;

    /// <summary>Дата изменения (UTC).</summary>
    public DateTime LastWriteUtc => File.LastWriteUtc;

    /// <summary>Оригинал группы — удалению не подлежит по умолчанию.</summary>
    public bool IsOriginal { get; init; }

    /// <summary>Файл лежит в папке-эталоне: удалять нельзя никогда (ТЗ §4.4).</summary>
    public bool IsProtected { get; init; }

    /// <summary>Расстояние Хэмминга до оригинала для режима «похожие»; 0 для точных совпадений.</summary>
    public int Distance { get; init; }

    /// <summary>Ширина в пикселях, если известна.</summary>
    public int Width { get; init; }

    /// <summary>Высота в пикселях, если известна.</summary>
    public int Height { get; init; }

    /// <summary>Дата съёмки из EXIF, если есть.</summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>Модель камеры из EXIF, если есть.</summary>
    public string? Camera { get; init; }

    /// <summary>Путь к парному файлу (Live Photo / RAW+JPEG), если найден (ТЗ §4.5).</summary>
    public string? PairPath { get; init; }

    /// <summary>Категория файла.</summary>
    public FileKind Kind { get; init; } = FileKind.Other;
}
