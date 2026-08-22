using DupFinder.Core.Abstractions;
using DupFinder.Core.Files;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class FileSystemFileSourceTests
{
    private static async Task<List<FileEntry>> CollectAsync(FileEnumerationRequest request)
    {
        var result = new List<FileEntry>();
        await foreach (var entry in new FileSystemFileSource().EnumerateAsync(request, CancellationToken.None))
        {
            result.Add(entry);
        }

        return result;
    }

    [Fact]
    public async Task Обход_находит_файлы_во_всех_подпапках()
    {
        using var temp = new TempFolder();
        temp.WriteText("a.txt", "1");
        temp.WriteText("sub/b.txt", "2");
        temp.WriteText("sub/deep/c.txt", "3");

        var files = await CollectAsync(new FileEnumerationRequest(new[] { temp.Path }, Recurse: true));

        files.Select(f => f.Name).Should().BeEquivalentTo("a.txt", "b.txt", "c.txt");
    }

    [Fact]
    public async Task Без_подпапок_берётся_только_верхний_уровень()
    {
        using var temp = new TempFolder();
        temp.WriteText("a.txt", "1");
        temp.WriteText("sub/b.txt", "2");

        var files = await CollectAsync(new FileEnumerationRequest(new[] { temp.Path }, Recurse: false));

        files.Select(f => f.Name).Should().BeEquivalentTo("a.txt");
    }

    [Fact]
    public async Task Размер_и_дата_изменения_заполняются()
    {
        using var temp = new TempFolder();
        var moment = new DateTime(2022, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        temp.Write("a.bin", new byte[123], moment);

        var files = await CollectAsync(new FileEnumerationRequest(new[] { temp.Path }, Recurse: true));

        files.Should().ContainSingle();
        files[0].Length.Should().Be(123);
        files[0].LastWriteUtc.Should().BeCloseTo(moment, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Маски_исключений_отсеивают_файлы()
    {
        using var temp = new TempFolder();
        temp.WriteText("keep.txt", "1");
        temp.WriteText("drop.tmp", "2");

        var files = await CollectAsync(new FileEnumerationRequest(new[] { temp.Path }, true)
        {
            ExcludeMasks = new[] { "*.tmp" },
        });

        files.Select(f => f.Name).Should().BeEquivalentTo("keep.txt");
    }

    [Fact]
    public async Task Исключённая_папка_не_обходится()
    {
        using var temp = new TempFolder();
        temp.WriteText("keep.txt", "1");
        temp.WriteText("skipme/drop.txt", "2");

        var files = await CollectAsync(new FileEnumerationRequest(new[] { temp.Path }, true)
        {
            ExcludeFolders = new[] { Path.Combine(temp.Path, "skipme") },
        });

        files.Select(f => f.Name).Should().BeEquivalentTo("keep.txt");
    }

    [Fact]
    public async Task Вложенные_корни_не_дают_дублей()
    {
        using var temp = new TempFolder();
        temp.WriteText("sub/a.txt", "1");

        var files = await CollectAsync(
            new FileEnumerationRequest(new[] { temp.Path, Path.Combine(temp.Path, "sub") }, Recurse: true));

        files.Should().ContainSingle();
    }

    [Fact]
    public async Task Отмена_прекращает_обход()
    {
        using var temp = new TempFolder();
        for (var i = 0; i < 50; i++)
        {
            temp.WriteText($"f{i}.txt", i.ToString());
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            await foreach (var _ in new FileSystemFileSource()
                               .EnumerateAsync(new FileEnumerationRequest(new[] { temp.Path }, true), cts.Token))
            {
                // Ждём исключение, а не результат.
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Несуществующая_папка_не_роняет_обход()
    {
        var missing = Path.Combine(Path.GetTempPath(), "dupfinder-missing-" + Guid.NewGuid().ToString("N"));

        var act = async () => await CollectAsync(new FileEnumerationRequest(new[] { missing }, true));

        act.Should().NotThrowAsync();
    }

    [Fact]
    public void FileExists_отвечает_правильно()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "1");
        var source = new FileSystemFileSource();

        source.FileExists(path).Should().BeTrue();
        source.FileExists(path + ".missing").Should().BeFalse();
    }

    [Fact]
    public void OpenRead_читает_содержимое()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "привет");

        using var stream = new FileSystemFileSource().OpenRead(path, FileReadHint.Sequential);
        using var reader = new StreamReader(stream);

        reader.ReadToEnd().Should().Be("привет");
    }

    [Theory]
    [InlineData("/a/b/c", "/a/b", true)]
    [InlineData("/a/b", "/a/b", true)]
    [InlineData("/a/bc", "/a/b", false)]
    [InlineData("/x/y", "/a/b", false)]
    public void IsUnder_различает_вложенность_и_совпадение_префикса(string path, string parent, bool expected) =>
        FileSystemFileSource.IsUnder(
            path.Replace('/', Path.DirectorySeparatorChar),
            parent.Replace('/', Path.DirectorySeparatorChar)).Should().Be(expected);
}
