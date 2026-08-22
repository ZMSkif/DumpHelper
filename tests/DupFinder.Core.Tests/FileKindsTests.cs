using DupFinder.Core.Files;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class FileKindsTests
{
    [Theory]
    [InlineData("a.JPG", FileKind.Photo)]
    [InlineData("a.heic", FileKind.Photo)]
    [InlineData("a.dng", FileKind.Photo)]
    [InlineData("a.mp4", FileKind.Video)]
    [InlineData("a.flac", FileKind.Audio)]
    [InlineData("a.pdf", FileKind.Document)]
    [InlineData("a.7z", FileKind.Archive)]
    [InlineData("a.qqq", FileKind.Other)]
    [InlineData("noextension", FileKind.Other)]
    public void Категория_определяется_по_расширению(string name, FileKind expected) =>
        FileKinds.FromPath(name).Should().Be(expected);

    [Fact]
    public void Фильтр_All_пропускает_всё() =>
        FileKinds.Matches(FileKindFilter.All, "a.qqq").Should().BeTrue();

    [Fact]
    public void Фильтр_пропускает_только_выбранные_категории()
    {
        FileKinds.Matches(FileKindFilter.Photo, "a.jpg").Should().BeTrue();
        FileKinds.Matches(FileKindFilter.Photo, "a.mp4").Should().BeFalse();
        FileKinds.Matches(FileKindFilter.Photo | FileKindFilter.Video, "a.mp4").Should().BeTrue();
    }

    [Fact]
    public void Исходные_форматы_ранжируются_выше_JPG()
    {
        FileKinds.RankOfFormat("a.dng").Should().BeLessThan(FileKinds.RankOfFormat("a.heic"));
        FileKinds.RankOfFormat("a.heic").Should().BeLessThan(FileKinds.RankOfFormat("a.png"));
        FileKinds.RankOfFormat("a.png").Should().BeLessThan(FileKinds.RankOfFormat("a.jpg"));
        FileKinds.RankOfFormat("a.jpg").Should().BeLessThan(FileKinds.RankOfFormat("a.qqq"));
    }
}
