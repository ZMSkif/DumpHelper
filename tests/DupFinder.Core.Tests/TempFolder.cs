namespace DupFinder.Core.Tests;

/// <summary>Временная папка, которая сама за собой убирает.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder(string? name = null)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dupfinder-tests",
            $"{name ?? "t"}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Создаёт файл с указанным содержимым, попутно создавая папки.</summary>
    public string Write(string relativePath, byte[] content, DateTime? lastWriteUtc = null)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        if (lastWriteUtc is not null)
        {
            File.SetLastWriteTimeUtc(full, lastWriteUtc.Value);
        }

        return full;
    }

    /// <summary>Создаёт текстовый файл.</summary>
    public string WriteText(string relativePath, string content, DateTime? lastWriteUtc = null) =>
        Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content), lastWriteUtc);

    /// <summary>Детерминированный «мусор» заданного размера.</summary>
    public static byte[] Bytes(int size, int seed)
    {
        var bytes = new byte[size];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Уборка мусора в тестах не должна ронять прогон.
        }
    }
}
