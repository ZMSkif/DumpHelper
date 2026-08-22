using System.Buffers;
using System.IO.Hashing;
using System.Security.Cryptography;
using DupFinder.Core.Abstractions;
using DupFinder.Core.Model;

namespace DupFinder.Core.Hashing;

/// <summary>
/// Ступени отсева из ТЗ §4.1: сначала дешёвые частичные хэши, потом полный,
/// и только затем дорогое подтверждение.
/// </summary>
public sealed class FileHasher
{
    /// <summary>Размер куска для частичных хэшей.</summary>
    public const int ChunkSize = 4096;

    /// <summary>Файлы крупнее этого порога получают дополнительную ступень «середина + хвост».</summary>
    public const long MidTailThreshold = 1L << 20;

    private const int StreamBufferSize = 1 << 20;

    private readonly IFileSource _source;
    private long _bytesRead;

    public FileHasher(IFileSource source) => _source = source;

    /// <summary>Сколько байт прочитано с диска за всё время работы.</summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>Первые 4 КБ, XxHash3 (ТЗ §4.1 п.3).</summary>
    public async Task<ulong> PartialAsync(FileEntry file, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            await using var stream = _source.OpenRead(file.Path, FileReadHint.Ranged);
            var read = await ReadBlockAsync(stream, buffer.AsMemory(0, ChunkSize), ct).ConfigureAwait(false);
            return XxHash3.HashToUInt64(buffer.AsSpan(0, read));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>4 КБ из середины + последние 4 КБ, XxHash3 (ТЗ §4.1 п.4).</summary>
    public async Task<ulong> MidTailAsync(FileEntry file, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize * 2);
        try
        {
            await using var stream = _source.OpenRead(file.Path, FileReadHint.Ranged);
            var length = file.Length;

            var midOffset = Math.Max(0, (length / 2) - (ChunkSize / 2));
            stream.Seek(midOffset, SeekOrigin.Begin);
            var mid = await ReadBlockAsync(stream, buffer.AsMemory(0, ChunkSize), ct).ConfigureAwait(false);

            var tailOffset = Math.Max(0, length - ChunkSize);
            stream.Seek(tailOffset, SeekOrigin.Begin);
            var tail = await ReadBlockAsync(stream, buffer.AsMemory(mid, ChunkSize), ct).ConfigureAwait(false);

            return XxHash3.HashToUInt64(buffer.AsSpan(0, mid + tail));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Полный XxHash128 всего файла (ТЗ §4.1 п.5).</summary>
    public async Task<string> FullAsync(FileEntry file, CancellationToken ct)
    {
        var hash = new XxHash128();
        var digest = new byte[16];
        await StreamThroughAsync(file, (buffer, count) => hash.Append(buffer.AsSpan(0, count)), ct).ConfigureAwait(false);
        hash.GetHashAndReset(digest);
        return Convert.ToHexString(digest);
    }

    /// <summary>SHA-256 всего файла — финальное подтверждение (ТЗ §4.1 п.6).</summary>
    public async Task<string> Sha256Async(FileEntry file, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await StreamThroughAsync(file, (buffer, count) => sha.AppendData(buffer, 0, count), ct).ConfigureAwait(false);
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Побайтовое сравнение двух файлов — альтернатива SHA-256.</summary>
    public async Task<bool> AreEqualAsync(FileEntry left, FileEntry right, CancellationToken ct)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        if (string.Equals(left.Path, right.Path, Files.FileSystemFileSource.PathComparison))
        {
            return true;
        }

        var bufferA = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        var bufferB = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            await using var streamA = _source.OpenRead(left.Path, FileReadHint.Sequential);
            await using var streamB = _source.OpenRead(right.Path, FileReadHint.Sequential);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var readA = await ReadBlockAsync(streamA, bufferA.AsMemory(0, StreamBufferSize), ct).ConfigureAwait(false);
                var readB = await ReadBlockAsync(streamB, bufferB.AsMemory(0, StreamBufferSize), ct).ConfigureAwait(false);

                if (readA != readB)
                {
                    return false;
                }

                if (readA == 0)
                {
                    return true;
                }

                Interlocked.Add(ref _bytesRead, readA + readB);

                if (!bufferA.AsSpan(0, readA).SequenceEqual(bufferB.AsSpan(0, readB)))
                {
                    return false;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bufferA);
            ArrayPool<byte>.Shared.Return(bufferB);
        }
    }

    private async Task StreamThroughAsync(FileEntry file, Action<byte[], int> consume, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            await using var stream = _source.OpenRead(file.Path, FileReadHint.Sequential);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer.AsMemory(0, StreamBufferSize), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                Interlocked.Add(ref _bytesRead, read);
                consume(buffer, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask<int> ReadBlockAsync(Stream stream, Memory<byte> destination, CancellationToken ct)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await stream.ReadAsync(destination[total..], ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        Interlocked.Add(ref _bytesRead, total);
        return total;
    }
}
