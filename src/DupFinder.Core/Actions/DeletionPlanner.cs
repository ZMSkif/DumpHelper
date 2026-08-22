using DupFinder.Core.Abstractions;
using DupFinder.Core.Files;
using DupFinder.Core.Model;

namespace DupFinder.Core.Actions;

/// <summary>Почему файл не будет удалён.</summary>
public enum DeletionRefusal
{
    /// <summary>Удаление разрешено.</summary>
    None = 0,

    /// <summary>Файл в папке-эталоне или в системной папке.</summary>
    Protected,

    /// <summary>Отмечена вся группа целиком: один файл обязан остаться.</summary>
    WouldEmptyGroup,

    /// <summary>Файл изменился после сканирования — данные о нём устарели.</summary>
    Changed,

    /// <summary>Файла больше нет.</summary>
    Missing,
}

/// <summary>Файл, отмеченный к удалению.</summary>
public sealed record DeletionCandidate(FileEntry File, int GroupId, bool IsOriginal, bool IsProtected);

/// <summary>Решение по одному файлу.</summary>
public sealed record DeletionDecision(DeletionCandidate Candidate, DeletionRefusal Refusal)
{
    public bool IsAllowed => Refusal == DeletionRefusal.None;

    public string Path => Candidate.File.Path;
}

/// <summary>Что именно произойдёт, если нажать «Удалить».</summary>
public sealed record DeletionPlan(IReadOnlyList<DeletionDecision> Decisions)
{
    /// <summary>Файлы, которые будут удалены.</summary>
    public IReadOnlyList<DeletionDecision> Allowed { get; } =
        Decisions.Where(d => d.IsAllowed).ToList();

    /// <summary>Файлы, которые будут пропущены, и причина.</summary>
    public IReadOnlyList<DeletionDecision> Refused { get; } =
        Decisions.Where(d => !d.IsAllowed).ToList();

    /// <summary>Сколько байт освободится.</summary>
    public long BytesFreed => Allowed.Sum(d => d.Candidate.File.Length);

    /// <summary>Есть ли вообще что удалять.</summary>
    public bool HasWork => Allowed.Count > 0;

    /// <summary>Сколько файлов отклонено по каждой причине.</summary>
    public IReadOnlyDictionary<DeletionRefusal, int> RefusalCounts { get; } = Decisions
        .Where(d => !d.IsAllowed)
        .GroupBy(d => d.Refusal)
        .ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>
/// Последняя проверка перед удалением. Между сканированием и нажатием кнопки
/// проходит время: файл могли изменить, переместить или удалить, а список
/// отметок — собрать так, что группа опустеет целиком. Всё это ловится здесь,
/// в движке, а не в интерфейсе — чтобы проверка была одна и её можно было испытать.
/// </summary>
public sealed class DeletionPlanner
{
    private readonly IFileSource _source;

    public DeletionPlanner(IFileSource source) => _source = source;

    /// <summary>
    /// Строит план удаления.
    /// </summary>
    /// <param name="candidates">Отмеченные пользователем файлы.</param>
    /// <param name="groupSizes">Сколько всего файлов в каждой группе — чтобы понять, не опустеет ли она.</param>
    /// <param name="verifyUnchanged">Сверять размер и время изменения с теми, что были при сканировании.</param>
    public DeletionPlan Prepare(
        IReadOnlyList<DeletionCandidate> candidates,
        IReadOnlyDictionary<int, int> groupSizes,
        bool verifyUnchanged = true)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(groupSizes);

        var decisions = new List<DeletionDecision>(candidates.Count);

        // Сколько файлов группы уже разрешено удалить: нужно, чтобы остановиться,
        // когда в группе остаётся последний.
        var removedPerGroup = new Dictionary<int, int>();

        // Оригиналы удаляем в последнюю очередь: если группу отметили целиком,
        // уцелеть должен именно оригинал.
        foreach (var candidate in candidates.OrderBy(c => c.IsOriginal ? 1 : 0))
        {
            decisions.Add(new DeletionDecision(candidate, Judge(candidate, groupSizes, removedPerGroup, verifyUnchanged)));
        }

        // Возвращаем в исходном порядке, чтобы интерфейс показал список привычно.
        var byPath = decisions.ToDictionary(d => d.Path, FileSystemFileSource.PathComparer);
        var ordered = candidates
            .Select(c => byPath[c.File.Path])
            .ToList();

        return new DeletionPlan(ordered);
    }

    private DeletionRefusal Judge(
        DeletionCandidate candidate,
        IReadOnlyDictionary<int, int> groupSizes,
        Dictionary<int, int> removedPerGroup,
        bool verifyUnchanged)
    {
        if (candidate.IsProtected || SystemFolders.IsProtected(candidate.File.Path))
        {
            return DeletionRefusal.Protected;
        }

        if (!_source.FileExists(candidate.File.Path))
        {
            return DeletionRefusal.Missing;
        }

        if (verifyUnchanged && HasChanged(candidate.File))
        {
            return DeletionRefusal.Changed;
        }

        // Группа не должна опустеть: хотя бы один файл обязан остаться.
        // Если размер группы неизвестен, считаем её состоящей из одного файла:
        // не зная, что останется, удалять нельзя.
        var total = groupSizes.TryGetValue(candidate.GroupId, out var size) ? size : 1;
        var alreadyRemoved = removedPerGroup.GetValueOrDefault(candidate.GroupId);
        if (total - alreadyRemoved <= 1)
        {
            return DeletionRefusal.WouldEmptyGroup;
        }

        removedPerGroup[candidate.GroupId] = alreadyRemoved + 1;
        return DeletionRefusal.None;
    }

    /// <summary>Изменился ли файл с момента сканирования.</summary>
    private bool HasChanged(FileEntry known)
    {
        try
        {
            var current = _source.Describe(known.Path);
            if (current is null)
            {
                return true;
            }

            if (current.Length != known.Length)
            {
                return true;
            }

            // Файловые системы хранят время с разной точностью — секунды достаточно.
            return Math.Abs((current.LastWriteUtc - known.LastWriteUtc).TotalSeconds) > 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не смогли проверить — значит удалять нельзя.
            return true;
        }
    }
}
