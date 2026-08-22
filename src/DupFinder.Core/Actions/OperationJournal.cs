using System.Globalization;
using System.Text;
using DupFinder.Core.Diagnostics;

namespace DupFinder.Core.Actions;

/// <summary>Что сделали с файлом.</summary>
public enum FileOperationKind
{
    Recycled,
    Moved,
    Linked,
}

/// <summary>Одна запись журнала операций.</summary>
public sealed record FileOperation(
    DateTimeOffset At,
    FileOperationKind Kind,
    string Path,
    long Length,
    bool Succeeded)
{
    /// <summary>Куда переместили; пусто для удаления.</summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>Почему не получилось.</summary>
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Журнал того, что программа сделала с файлами (ТЗ §4.6).
/// Отдельно от журнала работы: там диагностика для разработчика, здесь —
/// ответ на вопрос «что вы удалили с моего диска», который человек может
/// задать через неделю. Формат — по строке JSON на операцию: дописывается
/// без перечитывания и переживает аварийное завершение.
/// </summary>
public sealed class OperationJournal
{
    private readonly string _path;
    private readonly IScanLog _log;
    private readonly object _gate = new();

    public OperationJournal(string path, IScanLog? log = null)
    {
        _path = path;
        _log = log ?? NullScanLog.Instance;
    }

    /// <summary>Файл журнала.</summary>
    public string Path => _path;

    /// <summary>Дописывает пачку операций одним заходом.</summary>
    public void Append(IEnumerable<FileOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var lines = operations.Select(Serialize).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                var directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllLines(_path, lines, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не записали журнал — операция всё равно состоялась, ронять нечего.
            _log.Warn($"Не удалось записать журнал операций: {_path}", ex);
        }
    }

    /// <summary>Читает последние записи, новые сверху.</summary>
    public IReadOnlyList<FileOperation> ReadRecent(int limit = 500)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Array.Empty<FileOperation>();
            }

            var tail = new Queue<string>(limit);
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                if (tail.Count == limit)
                {
                    tail.Dequeue();
                }

                tail.Enqueue(line);
            }

            return tail
                .Select(Deserialize)
                .Where(o => o is not null)
                .Select(o => o!)
                .Reverse()
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Не удалось прочитать журнал операций: {_path}", ex);
            return Array.Empty<FileOperation>();
        }
    }

    private static string Serialize(FileOperation operation) =>
        System.Text.Json.JsonSerializer.Serialize(new JournalLine(
            operation.At.ToString("O", CultureInfo.InvariantCulture),
            operation.Kind.ToString(),
            operation.Path,
            operation.Destination,
            operation.Length,
            operation.Succeeded,
            operation.Error));

    private static FileOperation? Deserialize(string line)
    {
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<JournalLine>(line);
            if (parsed is null || !Enum.TryParse<FileOperationKind>(parsed.Kind, out var kind))
            {
                return null;
            }

            return new FileOperation(
                DateTimeOffset.Parse(parsed.At, CultureInfo.InvariantCulture),
                kind,
                parsed.Path,
                parsed.Length,
                parsed.Succeeded)
            {
                Destination = parsed.Destination ?? string.Empty,
                Error = parsed.Error ?? string.Empty,
            };
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
        {
            // Битую строку просто пропускаем: остальной журнал от этого не страдает.
            return null;
        }
    }

    private sealed record JournalLine(
        string At,
        string Kind,
        string Path,
        string? Destination,
        long Length,
        bool Succeeded,
        string? Error);
}
