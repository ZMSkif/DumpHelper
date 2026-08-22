using System.Diagnostics;
using DupFinder.Core.Files;
using DupFinder.Core.Model;
using DupFinder.Core.Scanning;
using DupFinder.TestData;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class ExactDuplicateScannerTests
{
    private static ExactDuplicateScanner Scanner() => new(new FileSystemFileSource());

    private static async Task<List<DuplicateGroup>> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var groups = new List<DuplicateGroup>();
        await foreach (var group in Scanner().ScanAsync(options, progress, ct))
        {
            groups.Add(group);
        }

        return groups;
    }

    [Fact]
    public async Task Находит_копию_с_другим_именем_и_датой()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(50_000, 11);
        temp.Write("a.jpg", content, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        temp.Write("вложенная/совсем другое имя.jpeg", content, new DateTime(2024, 6, 6, 0, 0, 0, DateTimeKind.Utc));

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        groups.Should().ContainSingle();
        groups[0].Items.Should().HaveCount(2);
        groups[0].Kind.Should().Be(MatchKind.ExactCopy);
    }

    [Fact]
    public async Task Одинаковый_размер_и_разное_содержимое_не_считаются_копиями()
    {
        using var temp = new TempFolder();
        temp.Write("a.bin", TempFolder.Bytes(40_000, 21));
        temp.Write("b.bin", TempFolder.Bytes(40_000, 22));

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Одинаковое_начало_и_разный_хвост_не_считаются_копиями()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(2_000_000, 31);
        var changed = content.ToArray();
        changed[^3] ^= 0xFF;
        temp.Write("a.bin", content);
        temp.Write("b.bin", changed);

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Пустые_файлы_не_образуют_групп()
    {
        using var temp = new TempFolder();
        temp.Write("a.dat", Array.Empty<byte>());
        temp.Write("b.dat", Array.Empty<byte>());
        temp.Write("c.dat", Array.Empty<byte>());
        ScanSummary? summary = null;

        var groups = await ScanAsync(
            ScanOptions.ForRoot(temp.Path),
            new Progress<ScanProgress>(p => summary ??= p.Summary));

        groups.Should().BeEmpty();
        await WaitForAsync(() => summary is not null);
        summary!.EmptyFiles.Should().Be(3);
    }

    [Fact]
    public async Task Минимальный_размер_отсекает_мелочь()
    {
        using var temp = new TempFolder();
        var small = TempFolder.Bytes(500, 41);
        temp.Write("a.bin", small);
        temp.Write("b.bin", small);

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path) with { MinBytes = 1024 });

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Максимальный_размер_отсекает_крупные_файлы()
    {
        using var temp = new TempFolder();
        var big = TempFolder.Bytes(200_000, 42);
        temp.Write("a.bin", big);
        temp.Write("b.bin", big);

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path) with { MaxBytes = 100_000 });

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Фильтр_по_типу_учитывает_только_нужные_файлы()
    {
        using var temp = new TempFolder();
        var photo = TempFolder.Bytes(30_000, 51);
        var document = TempFolder.Bytes(30_001, 52);
        temp.Write("a.jpg", photo);
        temp.Write("b.jpg", photo);
        temp.Write("a.pdf", document);
        temp.Write("b.pdf", document);

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path) with { Kinds = FileKindFilter.Photo });

        groups.Should().ContainSingle();
        groups[0].Items.Should().OnlyContain(i => i.Path.EndsWith(".jpg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Файл_из_папки_эталона_становится_оригиналом_и_защищён()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(20_000, 61);
        temp.Write("копии/a.jpg", content, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        temp.Write("эталон/b.jpg", content, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path) with
        {
            ReferenceFolder = Path.Combine(temp.Path, "эталон"),
            OriginalRule = OriginalRule.Oldest,
        });

        groups.Should().ContainSingle();
        var original = groups[0].Original;
        original.Path.Should().Contain("эталон");
        original.IsProtected.Should().BeTrue();
    }

    [Fact]
    public async Task Побайтовое_подтверждение_даёт_тот_же_результат_что_и_SHA256()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(1_500_000, 71);
        var changed = content.ToArray();
        changed[^1] ^= 0xFF;
        temp.Write("a.bin", content);
        temp.Write("b.bin", content);
        temp.Write("c.bin", changed);

        var sha = await ScanAsync(ScanOptions.ForRoot(temp.Path));
        var bytewise = await ScanAsync(ScanOptions.ForRoot(temp.Path) with { ConfirmBytewise = true });

        sha.Should().ContainSingle();
        bytewise.Should().ContainSingle();
        bytewise[0].Items.Select(i => i.Path).Should().BeEquivalentTo(sha[0].Items.Select(i => i.Path));
    }

    [Fact]
    public async Task Группа_из_трёх_копий_имеет_один_оригинал_и_две_копии()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(80_000, 81);
        temp.Write("a.bin", content, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        temp.Write("b.bin", content, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        temp.Write("c.bin", content, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        groups.Should().ContainSingle();
        groups[0].Items.Count(i => i.IsOriginal).Should().Be(1);
        groups[0].Copies.Should().HaveCount(2);
        groups[0].RedundantBytes.Should().Be(160_000);
    }

    [Fact]
    public async Task Прогресс_доходит_до_завершения_и_приносит_итоги()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(60_000, 91);
        temp.Write("a.bin", content);
        temp.Write("b.bin", content);
        temp.Write("c.bin", TempFolder.Bytes(60_001, 92));
        var reports = new List<ScanProgress>();

        await ScanAsync(ScanOptions.ForRoot(temp.Path), new SyncProgress(reports));

        reports.Should().NotBeEmpty();
        var final = reports.Last(r => r.Summary is not null);
        final.Stage.Should().Be(ScanStage.Completed);
        final.Summary!.FilesSeen.Should().Be(3);
        final.Summary.FilesConsidered.Should().Be(3);
        final.Summary.Groups.Should().Be(1);
        final.Summary.RedundantItems.Should().Be(1);
        final.Summary.RedundantBytes.Should().Be(60_000);
        final.Summary.BytesRead.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Отмена_срабатывает_быстрее_300_мс()
    {
        using var temp = new TempFolder();
        for (var i = 0; i < 400; i++)
        {
            // Пары одинаковых крупных файлов: движку будет что читать.
            var content = TempFolder.Bytes(300_000, i);
            temp.Write($"pair{i}/a.bin", content);
            temp.Write($"pair{i}/b.bin", content);
        }

        using var cts = new CancellationTokenSource();
        var clock = new Stopwatch();

        var act = async () =>
        {
            await foreach (var _ in Scanner().ScanAsync(ScanOptions.ForRoot(temp.Path), null, cts.Token))
            {
                if (!clock.IsRunning)
                {
                    clock.Start();
                    cts.Cancel();
                }
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        clock.ElapsedMilliseconds.Should().BeLessThan(300);
    }

    [Fact]
    public async Task Приёмка_на_сгенерированном_корпусе_находит_все_копии_и_не_придумывает_лишних()
    {
        using var temp = new TempFolder();
        var manifest = TestCorpus.Generate(temp.Path, uniqueFiles: 400, copies: 60);

        var groups = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        // Каждая посаженная группа найдена целиком.
        var found = groups
            .Select(g => g.Items.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var expected in manifest.ExactGroups)
        {
            found.Should().Contain(
                set => expected.All(set.Contains),
                $"группа из {expected.Count} файлов должна быть найдена целиком: {expected[0]}");
        }

        // Ложных срабатываний нет: ни одна ловушка и ни один пустой файл не попали в группы.
        var inGroups = groups.SelectMany(g => g.Items).Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        inGroups.Overlaps(manifest.Traps).Should().BeFalse("файлы-ловушки не являются копиями");
        inGroups.Overlaps(manifest.EmptyFiles).Should().BeFalse("пустые файлы — отдельная категория");

        groups.Sum(g => g.Items.Count - 1).Should().Be(manifest.RedundantCopies);
        groups.Should().OnlyContain(g => g.Items.Count(i => i.IsOriginal) == 1);
    }

    [Fact]
    public async Task Повторный_прогон_даёт_тот_же_результат()
    {
        using var temp = new TempFolder();
        TestCorpus.Generate(temp.Path, uniqueFiles: 120, copies: 25);

        var first = await ScanAsync(ScanOptions.ForRoot(temp.Path));
        var second = await ScanAsync(ScanOptions.ForRoot(temp.Path));

        first.Select(g => g.Original.Path).Should().BeEquivalentTo(second.Select(g => g.Original.Path));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class SyncProgress : IProgress<ScanProgress>
    {
        private readonly List<ScanProgress> _sink;

        public SyncProgress(List<ScanProgress> sink) => _sink = sink;

        public void Report(ScanProgress value)
        {
            lock (_sink)
            {
                _sink.Add(value);
            }
        }
    }
}

public class DuplicateScannerTests
{
    [Fact]
    public void Неподдержанный_режим_сообщает_понятной_ошибкой()
    {
        var scanner = DuplicateScanner.CreateDefault(new FileSystemFileSource());

        scanner.Supports(ScanMode.Exact).Should().BeTrue();
        scanner.Supports(ScanMode.Similar).Should().BeFalse();
        scanner.Invoking(s => s.ScanAsync(ScanOptions.ForRoot("/data", ScanMode.Similar), null, default))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*визуально похожие*");
    }
}
