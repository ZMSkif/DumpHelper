using System.Security.Cryptography;
using System.Text;

namespace DupFinder.TestData;

/// <summary>Что именно нагенерировали — на этом строятся проверки приёмки (ТЗ §9).</summary>
public sealed record CorpusManifest
{
    /// <summary>Все созданные файлы.</summary>
    public required IReadOnlyList<string> AllFiles { get; init; }

    /// <summary>Группы точных копий: каждый набор — файлы с одинаковым содержимым.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> ExactGroups { get; init; }

    /// <summary>Файлы-ловушки, которые не должны попасть ни в одну группу.</summary>
    public required IReadOnlyList<string> Traps { get; init; }

    /// <summary>Файлы нулевой длины.</summary>
    public required IReadOnlyList<string> EmptyFiles { get; init; }

    /// <summary>Сколько лишних копий содержит корпус.</summary>
    public int RedundantCopies => ExactGroups.Sum(g => g.Count - 1);
}

/// <summary>
/// Генератор тестовых данных из ТЗ §9. Детерминированный: одно и то же зерно даёт
/// один и тот же корпус, поэтому тесты воспроизводимы.
/// </summary>
public static class TestCorpus
{
    /// <summary>
    /// Создаёт корпус: обычные файлы, посаженные точные копии и файлы-ловушки
    /// (одинаковый размер и разное содержимое, одинаковое начало и разный хвост, 0 байт).
    /// </summary>
    public static CorpusManifest Generate(string root, int uniqueFiles = 500, int copies = 80, int seed = 20240815)
    {
        Directory.CreateDirectory(root);
        var random = new Random(seed);
        var all = new List<string>();
        var groups = new List<IReadOnlyList<string>>();
        var traps = new List<string>();
        var empty = new List<string>();

        var folders = new[] { "Загрузки", "Телефон/DCIM", "Телефон/DCIM/Camera", "Архив 2023", "Архив 2024/январь" };
        foreach (var folder in folders)
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }

        string PickFolder() => Path.Combine(root, folders[random.Next(folders.Length)]);

        // Обычные файлы разных размеров, в том числе крупнее 1 МБ — чтобы работала ступень «середина+хвост».
        var originals = new List<string>();
        for (var i = 0; i < uniqueFiles; i++)
        {
            var size = random.Next(10) switch
            {
                0 => random.Next(1, 512),
                1 => random.Next(512, 4096),
                <= 7 => random.Next(4096, 200_000),
                _ => random.Next(1_100_000, 2_500_000),
            };

            var path = Path.Combine(PickFolder(), $"file_{i:D5}{PickExtension(random)}");
            File.WriteAllBytes(path, RandomBytes(random, size));
            SetTime(path, random);
            originals.Add(path);
            all.Add(path);
        }

        // Посаженные точные копии: другое имя, другая дата, иногда другое расширение.
        for (var i = 0; i < copies; i++)
        {
            var source = originals[random.Next(originals.Count)];
            var group = new List<string> { source };
            var duplicatesInGroup = random.Next(1, 3);
            for (var d = 0; d < duplicatesInGroup; d++)
            {
                var copy = Path.Combine(
                    PickFolder(),
                    $"{Path.GetFileNameWithoutExtension(source)}_копия{i}_{d}{Path.GetExtension(source)}");
                if (File.Exists(copy))
                {
                    continue;
                }

                File.Copy(source, copy);
                SetTime(copy, random);
                group.Add(copy);
                all.Add(copy);
            }

            if (group.Count > 1)
            {
                Merge(groups, group);
            }
        }

        // Ловушка 1: одинаковый размер, разное содержимое.
        for (var i = 0; i < 20; i++)
        {
            var path = Path.Combine(root, "Ловушки", $"same_size_{i:D2}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = RandomBytes(random, 64_000);
            bytes[i] = (byte)(bytes[i] ^ 0xFF);
            bytes[^1] = (byte)i;
            File.WriteAllBytes(path, bytes);
            traps.Add(path);
            all.Add(path);
        }

        // Ловушка 2: одинаковые первые 4 КБ (и первый мегабайт), разный хвост.
        var head = RandomBytes(random, 1_500_000);
        for (var i = 0; i < 10; i++)
        {
            var path = Path.Combine(root, "Ловушки", $"same_head_{i:D2}.bin");
            var bytes = (byte[])head.Clone();
            bytes[^1] = (byte)i;
            bytes[^2] = (byte)(i * 7);
            File.WriteAllBytes(path, bytes);
            traps.Add(path);
            all.Add(path);
        }

        // Ловушка 3: пустые файлы — отдельная категория, дубликатами не считаются.
        for (var i = 0; i < 5; i++)
        {
            var path = Path.Combine(root, "Ловушки", $"empty_{i:D2}.dat");
            File.WriteAllBytes(path, Array.Empty<byte>());
            empty.Add(path);
            all.Add(path);
        }

        return new CorpusManifest
        {
            AllFiles = all,
            ExactGroups = groups,
            Traps = traps,
            EmptyFiles = empty,
        };
    }

    /// <summary>Контрольная сумма файла — для независимой проверки результатов сканирования.</summary>
    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    /// <summary>Человекочитаемое описание корпуса.</summary>
    public static string Describe(CorpusManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Файлов всего:        {manifest.AllFiles.Count}");
        sb.AppendLine($"Групп копий:         {manifest.ExactGroups.Count}");
        sb.AppendLine($"Лишних копий:        {manifest.RedundantCopies}");
        sb.AppendLine($"Файлов-ловушек:      {manifest.Traps.Count}");
        sb.AppendLine($"Пустых файлов:       {manifest.EmptyFiles.Count}");
        return sb.ToString();
    }

    /// <summary>Сливает пересекающиеся наборы копий в одну группу.</summary>
    private static void Merge(List<IReadOnlyList<string>> groups, List<string> candidate)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            if (groups[i].Intersect(candidate, StringComparer.OrdinalIgnoreCase).Any())
            {
                groups[i] = groups[i].Union(candidate, StringComparer.OrdinalIgnoreCase).ToList();
                return;
            }
        }

        groups.Add(candidate);
    }

    private static byte[] RandomBytes(Random random, int size)
    {
        var bytes = new byte[size];
        random.NextBytes(bytes);
        return bytes;
    }

    private static string PickExtension(Random random) =>
        new[] { ".jpg", ".png", ".mp4", ".pdf", ".txt", ".zip", ".bin" }[random.Next(7)];

    private static void SetTime(string path, Random random)
    {
        var time = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(random.Next(0, 2_000_000));
        File.SetLastWriteTimeUtc(path, time);
    }
}
