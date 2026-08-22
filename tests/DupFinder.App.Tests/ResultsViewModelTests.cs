using DupFinder.App.ViewModels;
using DupFinder.Core.Actions;
using DupFinder.Core.Files;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

public class ResultsViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dupfinder-app-" + Guid.NewGuid().ToString("N"));

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellService _shell = new();
    private readonly FakeRecycleBin _bin = new();
    private readonly ResultsViewModel _vm;

    public ResultsViewModelTests()
    {
        Directory.CreateDirectory(_root);
        _vm = new ResultsViewModel(
            _dialogs,
            _shell,
            _bin,
            new DeletionPlanner(new FileSystemFileSource()),
            new OperationJournal(Path.Combine(_root, "operations.jsonl")));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка не должна ронять прогон.
        }
    }

    /// <summary>
    /// Создаёт настоящий файл: планировщик удаления проверяет, что файл на месте
    /// и не изменился после сканирования, поэтому выдуманные пути не годятся.
    /// </summary>
    private string Create(string relative, int size = 64)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[size]);
        return full;
    }

    private static DuplicateGroup Group(int id, params (string Path, bool Original, bool Protected)[] items) =>
        new(
            id,
            MatchKind.ExactCopy,
            items.Select(i =>
            {
                var info = new FileInfo(i.Path);
                return new DuplicateItem(new FileEntry(i.Path, info.Length, info.LastWriteTimeUtc))
                {
                    IsOriginal = i.Original,
                    IsProtected = i.Protected,
                };
            }).ToList());

    private DuplicateGroup PairGroup(int id = 1, int size = 64)
    {
        var original = Create($"g{id}/оригинал.jpg", size);
        var copy = Create($"g{id}/копия.jpg", size);
        return Group(id, (original, true, false), (copy, false, false));
    }

    [Fact]
    public void Группы_превращаются_в_строки()
    {
        _vm.AddGroups(new[] { PairGroup() });

        _vm.Rows.Should().HaveCount(2);
        _vm.HasRows.Should().BeTrue();
        _vm.Rows.Count(r => r.IsOriginal).Should().Be(1);
    }

    [Fact]
    public void Отметить_все_копии_не_трогает_оригиналы_и_эталоны()
    {
        var original = Create("g1/оригинал.jpg");
        var copy = Create("g1/копия.jpg");
        var reference = Create("эталон/копия2.jpg");
        _vm.AddGroups(new[] { Group(1, (original, true, false), (copy, false, false), (reference, false, true)) });

        _vm.MarkCopiesCommand.Execute(null);

        _vm.Rows.Single(r => r.Path == copy).IsMarked.Should().BeTrue();
        _vm.Rows.Single(r => r.Path == original).IsMarked.Should().BeFalse();
        _vm.Rows.Single(r => r.Path == reference).IsMarked.Should().BeFalse();
        _vm.MarkedCount.Should().Be(1);
    }

    [Fact]
    public void Снять_все_обнуляет_счётчик()
    {
        _vm.AddGroups(new[] { PairGroup() });
        _vm.MarkCopiesCommand.Execute(null);

        _vm.ClearMarksCommand.Execute(null);

        _vm.MarkedCount.Should().Be(0);
        _vm.HasMarked.Should().BeFalse();
    }

    [Fact]
    public void Инвертирование_не_отмечает_эталон()
    {
        var reference = Create("эталон/a.jpg");
        var copy = Create("g1/b.jpg");
        _vm.AddGroups(new[] { Group(1, (reference, true, true), (copy, false, false)) });

        _vm.InvertMarksCommand.Execute(null);

        _vm.Rows.Single(r => r.IsProtected).IsMarked.Should().BeFalse();
        _vm.Rows.Single(r => !r.IsProtected).IsMarked.Should().BeTrue();
    }

    [Fact]
    public void Сделать_оригиналом_переставляет_роль_внутри_группы()
    {
        _vm.AddGroups(new[] { PairGroup() });
        var copy = _vm.Rows.Single(r => !r.IsOriginal);
        _vm.SelectedRow = copy;

        _vm.MakeSelectedOriginalCommand.Execute(null);

        copy.IsOriginal.Should().BeTrue();
        _vm.Rows.Count(r => r.IsOriginal).Should().Be(1);
    }

    [Fact]
    public async Task Удаление_отправляет_в_корзину_только_незащищённые()
    {
        var reference = Create("эталон/a.jpg");
        var copy = Create("g1/b.jpg");
        var spare = Create("g1/c.jpg");
        _vm.AddGroups(new[] { Group(1, (reference, true, true), (copy, false, false), (spare, false, false)) });
        foreach (var row in _vm.Rows)
        {
            row.IsMarked = true;
        }

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _vm.Rows.Should().NotContain(r => r.Path == copy);
        _bin.Requested.Should().NotContain(reference, "файл эталона удалять нельзя");
        _dialogs.Plans.Should().ContainSingle();
        _dialogs.Plans[0].Refused.Should().Contain(d => d.Path == reference);
    }

    [Fact]
    public async Task Группа_не_остаётся_пустой()
    {
        _vm.AddGroups(new[] { PairGroup() });
        foreach (var row in _vm.Rows)
        {
            row.IsMarked = true;
        }

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _bin.Requested.Should().ContainSingle("один файл группы обязан остаться");
        _vm.Rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Изменившийся_файл_не_удаляется()
    {
        var group = PairGroup();
        _vm.AddGroups(new[] { group });
        var copy = _vm.Rows.Single(r => !r.IsOriginal);
        copy.IsMarked = true;

        // Кто-то дописал файл, пока окно было открыто.
        File.WriteAllBytes(copy.Path, new byte[4096]);

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _bin.Requested.Should().BeEmpty();
        _dialogs.Plans[0].Refused.Single().Refusal.Should().Be(DeletionRefusal.Changed);
    }

    [Fact]
    public async Task Отказ_в_диалоге_ничего_не_удаляет()
    {
        _dialogs.ConfirmAnswer = false;
        _vm.AddGroups(new[] { PairGroup() });
        _vm.Rows.Single(r => !r.IsOriginal).IsMarked = true;

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _bin.Requested.Should().BeEmpty();
        _vm.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ничего_не_отмечено_показывает_подсказку()
    {
        _vm.AddGroups(new[] { PairGroup() });

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _bin.Requested.Should().BeEmpty();
        _dialogs.Warnings.Should().ContainSingle();
        _dialogs.Plans.Should().BeEmpty("до плана дело не дошло");
    }

    [Fact]
    public async Task Неудачное_удаление_оставляет_строку_на_месте()
    {
        _vm.AddGroups(new[] { PairGroup() });
        var copy = _vm.Rows.Single(r => !r.IsOriginal);
        _bin.Failing.Add(copy.Path);
        copy.IsMarked = true;

        await _vm.DeleteMarkedCommand.ExecuteAsync(null);

        _vm.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Удаление_попадает_в_журнал_операций()
    {
        var journalPath = Path.Combine(_root, "журнал.jsonl");
        var vm = new ResultsViewModel(
            _dialogs,
            _shell,
            _bin,
            new DeletionPlanner(new FileSystemFileSource()),
            new OperationJournal(journalPath));
        vm.AddGroups(new[] { PairGroup(2) });
        vm.Rows.Single(r => !r.IsOriginal).IsMarked = true;

        await vm.DeleteMarkedCommand.ExecuteAsync(null);

        var records = new OperationJournal(journalPath).ReadRecent();
        records.Should().ContainSingle();
        records[0].Kind.Should().Be(FileOperationKind.Recycled);
        records[0].Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Копирование_пути_идёт_через_оболочку()
    {
        _vm.AddGroups(new[] { PairGroup() });
        _vm.SelectedRow = _vm.Rows[0];

        _vm.CopySelectedPathCommand.Execute(null);

        _shell.Clipboard.Should().Be(_vm.Rows[0].Path);
    }

    [Fact]
    public void Очистка_возвращает_таблицу_в_исходное_состояние()
    {
        _vm.AddGroups(new[] { PairGroup() });

        _vm.Clear();

        _vm.Rows.Should().BeEmpty();
        _vm.HasRows.Should().BeFalse();
        _vm.MarkedCount.Should().Be(0);
    }

    [Fact]
    public void Статистика_показывает_группы_и_объём_к_освобождению()
    {
        _vm.AddGroups(new[] { PairGroup(1, 1_048_576) });
        _vm.ApplySummary(new ScanSummary { FilesSeen = 10 });

        _vm.StatisticsText.Should().Contain("10").And.Contain("МБ");
    }
}
