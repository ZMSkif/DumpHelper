using DupFinder.Core.Actions;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class OperationJournalTests
{
    private static FileOperation Op(string path, bool ok = true, FileOperationKind kind = FileOperationKind.Recycled) =>
        new(DateTimeOffset.UtcNow, kind, path, 1234, ok);

    [Fact]
    public void Записи_читаются_обратно()
    {
        using var temp = new TempFolder();
        var journal = new OperationJournal(Path.Combine(temp.Path, "operations.jsonl"));

        journal.Append(new[] { Op(@"C:\фото\a.jpg"), Op(@"C:\фото\b.jpg") });
        var read = journal.ReadRecent();

        read.Should().HaveCount(2);
        read.Select(o => o.Path).Should().Contain(@"C:\фото\a.jpg");
    }

    [Fact]
    public void Новые_записи_идут_первыми()
    {
        using var temp = new TempFolder();
        var journal = new OperationJournal(Path.Combine(temp.Path, "operations.jsonl"));

        journal.Append(new[] { Op("первый") });
        journal.Append(new[] { Op("второй") });

        journal.ReadRecent()[0].Path.Should().Be("второй");
    }

    [Fact]
    public void Поля_операции_сохраняются_целиком()
    {
        using var temp = new TempFolder();
        var journal = new OperationJournal(Path.Combine(temp.Path, "operations.jsonl"));
        var moment = new DateTimeOffset(2026, 8, 22, 10, 30, 0, TimeSpan.Zero);

        journal.Append(new[]
        {
            new FileOperation(moment, FileOperationKind.Moved, @"D:\a.jpg", 4096, false)
            {
                Destination = @"E:\архив\a.jpg",
                Error = "нет доступа",
            },
        });

        var read = journal.ReadRecent().Single();
        read.At.Should().Be(moment);
        read.Kind.Should().Be(FileOperationKind.Moved);
        read.Path.Should().Be(@"D:\a.jpg");
        read.Destination.Should().Be(@"E:\архив\a.jpg");
        read.Length.Should().Be(4096);
        read.Succeeded.Should().BeFalse();
        read.Error.Should().Be("нет доступа");
    }

    [Fact]
    public void Читается_не_больше_запрошенного()
    {
        using var temp = new TempFolder();
        var journal = new OperationJournal(Path.Combine(temp.Path, "operations.jsonl"));

        journal.Append(Enumerable.Range(0, 50).Select(i => Op($"файл{i}")));

        journal.ReadRecent(10).Should().HaveCount(10);
    }

    [Fact]
    public void Битая_строка_не_ломает_чтение()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "operations.jsonl");
        var journal = new OperationJournal(path);
        journal.Append(new[] { Op("хороший") });
        File.AppendAllText(path, "{это не json\n");
        journal.Append(new[] { Op("тоже хороший") });

        var read = journal.ReadRecent();

        read.Should().HaveCount(2);
        read.Select(o => o.Path).Should().BeEquivalentTo("хороший", "тоже хороший");
    }

    [Fact]
    public void Пустой_журнал_читается_без_ошибок()
    {
        using var temp = new TempFolder();

        new OperationJournal(Path.Combine(temp.Path, "нет-такого.jsonl"))
            .ReadRecent().Should().BeEmpty();
    }

    [Fact]
    public void Пустая_пачка_не_создаёт_файл()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "operations.jsonl");

        new OperationJournal(path).Append(Array.Empty<FileOperation>());

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Папка_журнала_создаётся_сама()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "вложенная", "папка", "operations.jsonl");

        new OperationJournal(path).Append(new[] { Op("a") });

        File.Exists(path).Should().BeTrue();
    }
}
