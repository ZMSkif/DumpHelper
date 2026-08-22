using System.Diagnostics;
using DupFinder.Core.Diagnostics;
using DupFinder.Core.Files;
using DupFinder.Core.Model;
using DupFinder.Core.Scanning;
using DupFinder.TestData;

namespace DupFinder.Cli;

/// <summary>
/// Консольный прогон движка без интерфейса (ТЗ §7 этап 2): позволяет мерить скорость
/// и проверять результаты на настоящих папках, не запуская WPF.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "scan" => await ScanAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "gen" => Generate(args.Skip(1).ToArray()),
                _ => Unknown(args[0]),
            };
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Отменено.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> ScanAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Укажите папку: dupfinder-cli scan <папка> [--bytewise] [--min-kb N] [--ref <папка>]");
            return 1;
        }

        var roots = args.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var bytewise = args.Contains("--bytewise");
        var minKb = ReadOption(args, "--min-kb") is { } raw && long.TryParse(raw, out var kb) ? kb : 0;
        var reference = ReadOption(args, "--ref");
        var quiet = args.Contains("--quiet");

        var options = ScanOptions.ForRoot(roots[0]) with
        {
            Roots = roots,
            MinBytes = minKb * 1024,
            ReferenceFolder = reference,
            ConfirmBytewise = bytewise,
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("Останавливаю…");
            cts.Cancel();
        };

        var log = new ConsoleScanLog(quiet);
        var scanner = DuplicateScanner.CreateDefault(new FileSystemFileSource(log), log);
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!quiet)
            {
                Console.WriteLine($"  [{p.Stage}] {p.Message} {p.Done}/{p.Total}");
            }
        });

        var clock = Stopwatch.StartNew();
        var groups = 0;
        var redundant = 0;
        var bytes = 0L;

        await foreach (var group in scanner.ScanAsync(options, progress, cts.Token).ConfigureAwait(false))
        {
            groups++;
            redundant += group.Items.Count - 1;
            bytes += group.RedundantBytes;
            if (!quiet)
            {
                Console.WriteLine($"Группа {group.Id} ({group.Kind}):");
                foreach (var item in group.Items)
                {
                    var role = item.IsProtected ? "эталон" : item.IsOriginal ? "оригинал" : "копия";
                    Console.WriteLine($"    {role,-9} {item.Length,12:N0}  {item.Path}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Групп: {groups}   Лишних копий: {redundant}   Освободить: {bytes / 1048576.0:N1} МБ   Время: {clock.Elapsed:mm\\:ss\\.fff}");
        return 0;
    }

    private static int Generate(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Укажите папку: dupfinder-cli gen <папка> [файлов] [копий]");
            return 1;
        }

        var unique = args.Length > 1 && int.TryParse(args[1], out var u) ? u : 5000;
        var copies = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 800;

        Console.WriteLine($"Создаю тестовый корпус в {args[0]}…");
        var manifest = TestCorpus.Generate(args[0], unique, copies);
        Console.Write(TestCorpus.Describe(manifest));
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Неизвестная команда: {command}");
        PrintUsage();
        return 1;
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("DupFinder CLI — прогон движка без интерфейса");
        Console.WriteLine();
        Console.WriteLine("  scan <папка…> [--min-kb N] [--ref <папка>] [--bytewise] [--quiet]");
        Console.WriteLine("  gen  <папка> [файлов] [копий]");
    }

    private sealed class ConsoleScanLog : IScanLog
    {
        private readonly bool _quiet;

        public ConsoleScanLog(bool quiet) => _quiet = quiet;

        public void Info(string message)
        {
            if (!_quiet)
            {
                Console.WriteLine($"    {message}");
            }
        }

        public void Warn(string message, Exception? error = null)
        {
            if (!_quiet)
            {
                Console.Error.WriteLine($"    ! {message}");
            }
        }

        public void Error(string message, Exception? error = null) =>
            Console.Error.WriteLine($"    !! {message} {error?.Message}");
    }
}
