using DupFinder.App.Services;
using DupFinder.App.ViewModels;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

public class ResultsViewModelTests
{
    private static DuplicateGroup Group(int id, params (string Path, long Length, bool Original, bool Protected)[] items) =>
        new(
            id,
            MatchKind.ExactCopy,
            items.Select(i => new DuplicateItem(new FileEntry(i.Path, i.Length, DateTime.UtcNow))
            {
                IsOriginal = i.Original,
                IsProtected = i.Protected,
            }).ToList());

    private static (ResultsViewModel Vm, FakeDialogService Dialogs, FakeShellService Shell, FakeRecycleBin Bin) Create()
    {
        var dialogs = new FakeDialogService();
        var shell = new FakeShellService();
        var bin = new FakeRecycleBin();
        return (new ResultsViewModel(dialogs, shell, bin), dialogs, shell, bin);
    }

    [Fact]
    public void Группы_превращаются_в_строки()
    {
        var (vm, _, _, _) = Create();

        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });

        vm.Rows.Should().HaveCount(2);
        vm.HasRows.Should().BeTrue();
        vm.Rows[0].IsOriginal.Should().BeTrue();
    }

    [Fact]
    public void Отметить_все_копии_не_трогает_оригиналы_и_эталоны()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[]
        {
            Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false), (@"C:\ref\c.jpg", 100, false, true)),
        });

        vm.MarkCopiesCommand.Execute(null);

        vm.Rows.Single(r => r.Name == "b.jpg").IsMarked.Should().BeTrue();
        vm.Rows.Single(r => r.Name == "a.jpg").IsMarked.Should().BeFalse();
        vm.Rows.Single(r => r.Name == "c.jpg").IsMarked.Should().BeFalse();
        vm.MarkedCount.Should().Be(1);
    }

    [Fact]
    public void Снять_все_обнуляет_счётчик()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });
        vm.MarkCopiesCommand.Execute(null);

        vm.ClearMarksCommand.Execute(null);

        vm.MarkedCount.Should().Be(0);
        vm.HasMarked.Should().BeFalse();
    }

    [Fact]
    public void Инвертирование_не_отмечает_эталон()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\ref\a.jpg", 100, true, true), (@"C:\b.jpg", 100, false, false)) });

        vm.InvertMarksCommand.Execute(null);

        vm.Rows.Single(r => r.IsProtected).IsMarked.Should().BeFalse();
        vm.Rows.Single(r => !r.IsProtected).IsMarked.Should().BeTrue();
    }

    [Fact]
    public void Сделать_оригиналом_переставляет_роль_внутри_группы()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });
        vm.SelectedRow = vm.Rows.Single(r => r.Name == "b.jpg");

        vm.MakeSelectedOriginalCommand.Execute(null);

        vm.Rows.Single(r => r.Name == "b.jpg").IsOriginal.Should().BeTrue();
        vm.Rows.Single(r => r.Name == "a.jpg").IsOriginal.Should().BeFalse();
        vm.Rows.Count(r => r.IsOriginal).Should().Be(1);
    }

    [Fact]
    public async Task Удаление_отправляет_в_корзину_только_незащищённые()
    {
        var (vm, dialogs, _, bin) = Create();
        vm.AddGroups(new[]
        {
            Group(1, (@"C:\ref\a.jpg", 100, true, true), (@"C:\b.jpg", 100, false, false)),
        });
        foreach (var row in vm.Rows)
        {
            row.IsMarked = true;
        }

        await vm.DeleteMarkedCommand.ExecuteAsync(null);

        bin.Requested.Should().Equal(@"C:\b.jpg");
        dialogs.Questions.Should().ContainSingle();
        dialogs.Questions[0].Should().Contain("эталон");
        vm.Rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Отказ_в_диалоге_ничего_не_удаляет()
    {
        var (vm, dialogs, _, bin) = Create();
        dialogs.ConfirmAnswer = false;
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });
        vm.Rows[1].IsMarked = true;

        await vm.DeleteMarkedCommand.ExecuteAsync(null);

        bin.Requested.Should().BeEmpty();
        vm.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ничего_не_отмечено_показывает_подсказку()
    {
        var (vm, dialogs, _, bin) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });

        await vm.DeleteMarkedCommand.ExecuteAsync(null);

        bin.Requested.Should().BeEmpty();
        dialogs.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task Неудачное_удаление_оставляет_строку_на_месте()
    {
        var (vm, _, _, bin) = Create();
        bin.Failing.Add(@"C:\b.jpg");
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });
        vm.Rows[1].IsMarked = true;

        await vm.DeleteMarkedCommand.ExecuteAsync(null);

        vm.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Копирование_пути_идёт_через_оболочку()
    {
        var (vm, _, shell, _) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });
        vm.SelectedRow = vm.Rows[0];

        vm.CopySelectedPathCommand.Execute(null);

        shell.Clipboard.Should().Be(@"C:\a.jpg");
    }

    [Fact]
    public void Очистка_возвращает_таблицу_в_исходное_состояние()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[] { Group(1, (@"C:\a.jpg", 100, true, false), (@"C:\b.jpg", 100, false, false)) });

        vm.Clear();

        vm.Rows.Should().BeEmpty();
        vm.HasRows.Should().BeFalse();
        vm.MarkedCount.Should().Be(0);
    }

    [Fact]
    public void Статистика_показывает_группы_и_объём_к_освобождению()
    {
        var (vm, _, _, _) = Create();
        vm.AddGroups(new[]
        {
            Group(1, (@"C:\a.jpg", 1_048_576, true, false), (@"C:\b.jpg", 1_048_576, false, false)),
        });
        vm.ApplySummary(new ScanSummary { FilesSeen = 10 });

        vm.StatisticsText.Should().Contain("10").And.Contain("МБ");
    }
}
