using DupFinder.App.ViewModels;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

public class DuplicateRowViewModelTests
{
    private static DuplicateRowViewModel Row(
        string path = @"C:\фото\IMG_0001.JPG",
        long length = 2048,
        bool original = false,
        bool isProtected = false,
        int groupId = 1,
        MatchKind kind = MatchKind.ExactCopy,
        Action<DuplicateRowViewModel, bool>? onMark = null) =>
        new(
            new DuplicateItem(new FileEntry(path, length, DateTime.UtcNow))
            {
                IsOriginal = original,
                IsProtected = isProtected,
                Kind = FileKind.Photo,
            },
            groupId,
            kind,
            onMark);

    [Fact]
    public void Разбор_пути_даёт_имя_папку_и_расширение()
    {
        var row = Row();

        row.Name.Should().Be("IMG_0001.JPG");
        row.Extension.Should().Be(".jpg");
        row.Directory.Should().EndWith("фото");
    }

    [Theory]
    [InlineData(512, "512 Б")]
    [InlineData(2048, "2 КБ")]
    public void Мелкие_размеры_показываются_понятно(long bytes, string expected) =>
        DuplicateRowViewModel.FormatSize(bytes).Should().Be(expected);

    [Fact]
    public void Крупные_размеры_переводятся_в_МБ_и_ГБ()
    {
        DuplicateRowViewModel.FormatSize(5L * 1024 * 1024).Should().Contain("МБ");
        DuplicateRowViewModel.FormatSize(3L * 1024 * 1024 * 1024).Should().Contain("ГБ");
    }

    [Fact]
    public void Роль_меняется_вместе_с_признаком_оригинала()
    {
        var row = Row();
        var copyRole = row.RoleText;

        row.SetOriginal(true);

        row.RoleText.Should().NotBe(copyRole);
        row.IsOriginal.Should().BeTrue();
    }

    [Fact]
    public void Эталон_показывается_особой_ролью() =>
        Row(isProtected: true).RoleText.Should().Contain("Эталон");

    [Fact]
    public void Чётность_группы_определяет_подсветку()
    {
        Row(groupId: 1).IsGroupOdd.Should().BeTrue();
        Row(groupId: 2).IsGroupOdd.Should().BeFalse();
    }

    [Fact]
    public void Точность_совпадения_видна_из_строки()
    {
        Row(kind: MatchKind.ExactCopy).IsExact.Should().BeTrue();
        Row(kind: MatchKind.Similar).IsExact.Should().BeFalse();
    }

    [Fact]
    public void Отметка_сообщает_наверх()
    {
        var calls = new List<bool>();
        var row = Row(onMark: (_, marked) => calls.Add(marked));

        row.IsMarked = true;
        row.IsMarked = false;

        calls.Should().Equal(true, false);
    }
}
