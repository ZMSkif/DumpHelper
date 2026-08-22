using DupFinder.Core.Files;
using DupFinder.Core.Hashing;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class FileHasherTests
{
    private static FileEntry Entry(string path) =>
        new(path, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path));

    private static FileHasher Hasher() => new(new FileSystemFileSource());

    [Fact]
    public async Task Частичный_хэш_совпадает_у_файлов_с_одинаковым_началом()
    {
        using var temp = new TempFolder();
        var head = TempFolder.Bytes(10_000, 1);
        var tailA = head.ToArray();
        var tailB = head.ToArray();
        tailB[^1] ^= 0xFF;
        var a = Entry(temp.Write("a.bin", tailA));
        var b = Entry(temp.Write("b.bin", tailB));
        var hasher = Hasher();

        var hashA = await hasher.PartialAsync(a, CancellationToken.None);
        var hashB = await hasher.PartialAsync(b, CancellationToken.None);

        hashA.Should().Be(hashB, "первые 4 КБ у файлов совпадают");
    }

    [Fact]
    public async Task Хэш_середины_и_хвоста_ловит_разный_хвост()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(2_000_000, 2);
        var changed = content.ToArray();
        changed[^1] ^= 0xFF;
        var a = Entry(temp.Write("a.bin", content));
        var b = Entry(temp.Write("b.bin", changed));
        var hasher = Hasher();

        var hashA = await hasher.MidTailAsync(a, CancellationToken.None);
        var hashB = await hasher.MidTailAsync(b, CancellationToken.None);

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public async Task Полный_хэш_одинаков_у_идентичных_файлов_и_различается_у_разных()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(50_000, 3);
        var other = TempFolder.Bytes(50_000, 4);
        var hasher = Hasher();

        var first = await hasher.FullAsync(Entry(temp.Write("a.bin", content)), CancellationToken.None);
        var same = await hasher.FullAsync(Entry(temp.Write("b.bin", content)), CancellationToken.None);
        var different = await hasher.FullAsync(Entry(temp.Write("c.bin", other)), CancellationToken.None);

        first.Should().Be(same);
        first.Should().NotBe(different);
        first.Should().HaveLength(32, "XxHash128 — это 16 байт в hex");
    }

    [Fact]
    public async Task Sha256_совпадает_с_эталонным_значением()
    {
        using var temp = new TempFolder();
        var path = temp.Write("a.bin", System.Text.Encoding.ASCII.GetBytes("abc"));
        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes("abc")));

        var actual = await Hasher().Sha256Async(Entry(path), CancellationToken.None);

        actual.Should().Be(expected);
    }

    [Fact]
    public async Task Побайтовое_сравнение_различает_содержимое()
    {
        using var temp = new TempFolder();
        var content = TempFolder.Bytes(3_000_000, 5);
        var changed = content.ToArray();
        changed[1_500_000] ^= 0xFF;
        var hasher = Hasher();

        var same = await hasher.AreEqualAsync(
            Entry(temp.Write("a.bin", content)),
            Entry(temp.Write("b.bin", content)),
            CancellationToken.None);
        var different = await hasher.AreEqualAsync(
            Entry(temp.Write("c.bin", content)),
            Entry(temp.Write("d.bin", changed)),
            CancellationToken.None);

        same.Should().BeTrue();
        different.Should().BeFalse();
    }

    [Fact]
    public async Task Файлы_разного_размера_не_читаются_целиком()
    {
        using var temp = new TempFolder();
        var hasher = Hasher();
        var a = Entry(temp.Write("a.bin", TempFolder.Bytes(1000, 6)));
        var b = Entry(temp.Write("b.bin", TempFolder.Bytes(2000, 6)));

        var equal = await hasher.AreEqualAsync(a, b, CancellationToken.None);

        equal.Should().BeFalse();
        hasher.BytesRead.Should().Be(0);
    }

    [Fact]
    public async Task Счётчик_прочитанных_байт_растёт()
    {
        using var temp = new TempFolder();
        var hasher = Hasher();
        var entry = Entry(temp.Write("a.bin", TempFolder.Bytes(100_000, 7)));

        await hasher.FullAsync(entry, CancellationToken.None);

        hasher.BytesRead.Should().Be(100_000);
    }
}
