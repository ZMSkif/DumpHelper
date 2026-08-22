using DupFinder.Core.Model;
using DupFinder.Core.Scanning;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class OriginalSelectorTests
{
    private static FileEntry File(string path, long length = 100, int daysOld = 0) =>
        new(Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar)),
            length,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-daysOld));

    [Fact]
    public void Правило_старейший_ставит_первым_самый_старый()
    {
        var files = new[] { File("/data/new.jpg", daysOld: 1), File("/data/old.jpg", daysOld: 10) };

        var ordered = OriginalSelector.Order(files, OriginalRule.Oldest, null);

        ordered[0].Path.Should().EndWith("old.jpg");
        ordered[0].IsOriginal.Should().BeTrue();
    }

    [Fact]
    public void Правило_новейший_ставит_первым_самый_новый()
    {
        var files = new[] { File("/data/old.jpg", daysOld: 10), File("/data/new.jpg", daysOld: 1) };

        var ordered = OriginalSelector.Order(files, OriginalRule.Newest, null);

        ordered[0].Path.Should().EndWith("new.jpg");
    }

    [Fact]
    public void Правило_кратчайший_путь_учитывает_длину_пути()
    {
        var files = new[] { File("/data/very/deep/nested/a.jpg"), File("/data/a.jpg") };

        var ordered = OriginalSelector.Order(files, OriginalRule.ShortestPath, null);

        ordered[0].Path.Should().Be(Path.GetFullPath("/data/a.jpg".Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Правило_наибольший_файл_ставит_первым_самый_крупный()
    {
        var files = new[] { File("/data/small.jpg", 100), File("/data/big.jpg", 900) };

        var ordered = OriginalSelector.Order(files, OriginalRule.LargestFile, null);

        ordered[0].Path.Should().EndWith("big.jpg");
    }

    [Fact]
    public void Правило_наибольшее_разрешение_использует_пиксели()
    {
        var small = File("/data/small.jpg");
        var big = File("/data/big.jpg");

        var ordered = OriginalSelector.Order(
            new[] { small, big },
            OriginalRule.HighestResolution,
            null,
            f => f.Path.EndsWith("big.jpg", StringComparison.Ordinal) ? new PixelSize(4000, 3000) : new PixelSize(640, 480));

        ordered[0].Path.Should().EndWith("big.jpg");
        ordered[0].Width.Should().Be(4000);
        ordered[0].Height.Should().Be(3000);
    }

    [Fact]
    public void Правило_исходный_формат_предпочитает_HEIC_и_RAW()
    {
        var files = new[] { File("/data/a.jpg"), File("/data/a.heic"), File("/data/a.dng") };

        var ordered = OriginalSelector.Order(files, OriginalRule.SourceFormat, null);

        ordered.Select(i => Path.GetExtension(i.Path)).Should().ContainInOrder(".dng", ".heic", ".jpg");
    }

    [Fact]
    public void Файл_из_папки_эталона_всегда_оригинал_и_защищён()
    {
        var files = new[]
        {
            File("/data/copies/new.jpg", daysOld: 100),
            File("/data/etalon/x.jpg", daysOld: 1),
        };

        var ordered = OriginalSelector.Order(files, OriginalRule.Oldest, "/data/etalon");

        ordered[0].Path.Should().EndWith("x.jpg");
        ordered[0].IsOriginal.Should().BeTrue();
        ordered[0].IsProtected.Should().BeTrue();
        ordered[1].IsProtected.Should().BeFalse();
    }

    [Fact]
    public void Все_файлы_эталона_защищены_даже_если_оригинал_один()
    {
        var files = new[] { File("/data/etalon/a.jpg"), File("/data/etalon/b.jpg"), File("/data/other/c.jpg") };

        var ordered = OriginalSelector.Order(files, OriginalRule.Oldest, "/data/etalon");

        ordered.Where(i => i.IsProtected).Should().HaveCount(2);
        ordered.Count(i => i.IsOriginal).Should().Be(1);
    }

    [Fact]
    public void Группа_никогда_не_остаётся_без_оригинала()
    {
        foreach (var rule in Enum.GetValues<OriginalRule>())
        {
            var ordered = OriginalSelector.Order(
                new[] { File("/data/a.jpg"), File("/data/b.jpg"), File("/data/c.jpg") },
                rule,
                null);

            ordered.Count(i => i.IsOriginal).Should().Be(1, $"правило {rule}");
        }
    }

    [Fact]
    public void Порядок_стабилен_при_равных_ключах()
    {
        var files = new[] { File("/data/b.jpg"), File("/data/a.jpg") };

        var first = OriginalSelector.Order(files, OriginalRule.Oldest, null).Select(i => i.Path);
        var second = OriginalSelector.Order(files.Reverse().ToArray(), OriginalRule.Oldest, null).Select(i => i.Path);

        first.Should().Equal(second);
    }

    [Fact]
    public void Пустой_список_даёт_пустой_результат() =>
        OriginalSelector.Order(Array.Empty<FileEntry>(), OriginalRule.Oldest, null).Should().BeEmpty();

    [Fact]
    public void Пустая_папка_эталона_считается_отсутствующей()
    {
        OriginalSelector.NormalizeReference(null).Should().BeNull();
        OriginalSelector.NormalizeReference("   ").Should().BeNull();
        OriginalSelector.NormalizeReference("/data/etalon/").Should().NotBeNull();
    }
}
