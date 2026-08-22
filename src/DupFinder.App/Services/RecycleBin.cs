using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DupFinder.App.Services;

/// <summary>Результат удаления пачки файлов.</summary>
public sealed record DeleteResult(int Deleted, IReadOnlyList<string> Failed);

/// <summary>Удаление файлов в Корзину.</summary>
public interface IRecycleBin
{
    /// <summary>Отправляет файлы в Корзину. Возвращает, сколько удалось.</summary>
    DeleteResult Delete(IReadOnlyList<string> paths);
}

/// <summary>
/// Корзина через оболочку Windows (SHFileOperation) — способ из прототипа.
/// Сначала пробуем удалить всё одним вызовом: так операция в Корзине одна,
/// и это в разы быстрее. Если не вышло — идём по одному, чтобы понять, какие файлы виноваты.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShellRecycleBin : IRecycleBin
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>Сколько путей отдавать оболочке за один вызов.</summary>
    private const int BatchSize = 500;

    public DeleteResult Delete(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var deleted = 0;
        var failed = new List<string>();

        foreach (var batch in paths.Where(File.Exists).Chunk(BatchSize))
        {
            if (Run(string.Join('\0', batch) + "\0\0"))
            {
                deleted += batch.Length;
                continue;
            }

            foreach (var path in batch)
            {
                if (Run(path + "\0\0"))
                {
                    deleted++;
                }
                else
                {
                    failed.Add(path);
                }
            }
        }

        return new DeleteResult(deleted, failed);
    }

    private static bool Run(string from)
    {
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = from,
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        return SHFileOperation(ref operation) == 0 && !operation.fAnyOperationsAborted;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT operation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }
}
