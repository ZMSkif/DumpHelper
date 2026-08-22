using System.Diagnostics;
using System.Runtime.Versioning;

namespace DupFinder.App.Services;

/// <summary>Открывает Проводник и файлы во внешних программах.</summary>
public interface IShellService
{
    /// <summary>Показывает файл в Проводнике (с выделением).</summary>
    void RevealInExplorer(string path);

    /// <summary>Открывает файл или папку в программе по умолчанию.</summary>
    void Open(string path);

    /// <summary>Кладёт текст в буфер обмена.</summary>
    void CopyToClipboard(string text);
}

/// <inheritdoc />
[SupportedOSPlatform("windows")]
public sealed class ShellService : IShellService
{
    public void RevealInExplorer(string path) =>
        Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    public void Open(string path) =>
        Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Буфер обмена бывает занят другим приложением — это не повод падать.
        }
    }

    private static void Start(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // Пользователь мог удалить файл прямо перед нажатием — это не повод падать.
        }
    }
}
