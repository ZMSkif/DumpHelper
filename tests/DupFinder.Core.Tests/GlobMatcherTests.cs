using DupFinder.Core.Files;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("*.tmp", "/data/a.tmp", true)]
    [InlineData("*.tmp", "/data/a.txt", false)]
    [InlineData("~$*", "/data/~$doc.docx", true)]
    [InlineData("thumbs.db", "/data/Thumbs.db", true)]
    [InlineData("file?.bin", "/data/file1.bin", true)]
    [InlineData("file?.bin", "/data/file12.bin", false)]
    public void Маска_по_имени_проверяет_только_имя(string mask, string path, bool expected) =>
        new GlobMatcher(new[] { mask }).IsMatch(path).Should().Be(expected);

    [Fact]
    public void Маска_с_разделителем_проверяет_весь_путь()
    {
        var matcher = new GlobMatcher(new[] { "*/node_modules/*" });

        matcher.IsMatch("/src/node_modules/x.js").Should().BeTrue();
        matcher.IsMatch("/src/lib/x.js").Should().BeFalse();
    }

    [Fact]
    public void Пустой_список_масок_не_исключает_ничего()
    {
        GlobMatcher.Empty.IsEmpty.Should().BeTrue();
        GlobMatcher.Empty.IsMatch("/data/a.tmp").Should().BeFalse();
    }

    [Fact]
    public void Пустые_строки_в_списке_игнорируются() =>
        new GlobMatcher(new[] { "", "   " }).IsEmpty.Should().BeTrue();

    [Fact]
    public void Спецсимволы_регулярок_экранируются()
    {
        var matcher = new GlobMatcher(new[] { "a+b.txt" });

        matcher.IsMatch("/x/a+b.txt").Should().BeTrue();
        matcher.IsMatch("/x/aab.txt").Should().BeFalse();
    }
}
