using DupFinder.Core.Actions;
using DupFinder.Core.Files;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class DeletionPlannerTests
{
    private static DeletionCandidate Candidate(string path, int group, bool original = false, bool isProtected = false)
    {
        var info = new FileInfo(path);
        return new DeletionCandidate(
            new FileEntry(path, info.Length, info.LastWriteTimeUtc),
            group,
            original,
            isProtected);
    }

    private static DeletionPlanner Planner() => new(new FileSystemFileSource());

    [Fact]
    public void Обычные_копии_разрешены_к_удалению()
    {
        using var temp = new TempFolder();
        var a = temp.WriteText("a.txt", "одинаково");
        var b = temp.WriteText("b.txt", "одинаково");

        var plan = Planner().Prepare(
            new[] { Candidate(b, 1) },
            new Dictionary<int, int> { [1] = 2 });

        plan.Allowed.Should().ContainSingle();
        plan.Refused.Should().BeEmpty();
        plan.HasWork.Should().BeTrue();
        _ = a;
    }

    [Fact]
    public void Защищённый_файл_не_удаляется()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("эталон.txt", "x");

        var plan = Planner().Prepare(
            new[] { Candidate(path, 1, isProtected: true) },
            new Dictionary<int, int> { [1] = 2 });

        plan.Allowed.Should().BeEmpty();
        plan.Refused.Single().Refusal.Should().Be(DeletionRefusal.Protected);
    }

    [Fact]
    public void Группа_не_может_опустеть_целиком()
    {
        using var temp = new TempFolder();
        var a = temp.WriteText("a.txt", "одинаково");
        var b = temp.WriteText("b.txt", "одинаково");
        var c = temp.WriteText("c.txt", "одинаково");

        var plan = Planner().Prepare(
            new[] { Candidate(a, 1, original: true), Candidate(b, 1), Candidate(c, 1) },
            new Dictionary<int, int> { [1] = 3 });

        plan.Allowed.Should().HaveCount(2, "один файл обязан остаться");
        plan.Refused.Single().Refusal.Should().Be(DeletionRefusal.WouldEmptyGroup);
    }

    [Fact]
    public void Уцелеть_должен_именно_оригинал()
    {
        using var temp = new TempFolder();
        var original = temp.WriteText("оригинал.txt", "одинаково");
        var copy = temp.WriteText("копия.txt", "одинаково");

        var plan = Planner().Prepare(
            new[] { Candidate(original, 1, original: true), Candidate(copy, 1) },
            new Dictionary<int, int> { [1] = 2 });

        plan.Allowed.Single().Path.Should().Be(copy);
        plan.Refused.Single().Path.Should().Be(original);
    }

    [Fact]
    public void Изменившийся_после_скана_файл_не_удаляется()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "было");
        var stale = Candidate(path, 1);

        // Кто-то переписал файл, пока окно было открыто.
        File.WriteAllText(path, "стало другим и длиннее");

        var plan = Planner().Prepare(new[] { stale }, new Dictionary<int, int> { [1] = 2 });

        plan.Allowed.Should().BeEmpty();
        plan.Refused.Single().Refusal.Should().Be(DeletionRefusal.Changed);
    }

    [Fact]
    public void Исчезнувший_файл_отмечается_отдельно()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "x");
        var candidate = Candidate(path, 1);
        File.Delete(path);

        var plan = Planner().Prepare(new[] { candidate }, new Dictionary<int, int> { [1] = 2 });

        plan.Refused.Single().Refusal.Should().Be(DeletionRefusal.Missing);
    }

    [Fact]
    public void Проверку_неизменности_можно_отключить()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "было");
        var stale = Candidate(path, 1);
        File.WriteAllText(path, "стало другим");

        var plan = Planner().Prepare(new[] { stale }, new Dictionary<int, int> { [1] = 2 }, verifyUnchanged: false);

        plan.Allowed.Should().ContainSingle();
    }

    [Fact]
    public void Порядок_в_плане_совпадает_с_порядком_отметок()
    {
        using var temp = new TempFolder();
        var a = temp.WriteText("a.txt", "одинаково");
        var b = temp.WriteText("b.txt", "одинаково");
        var c = temp.WriteText("c.txt", "одинаково");
        var input = new[] { Candidate(a, 1, original: true), Candidate(b, 1), Candidate(c, 1) };

        var plan = Planner().Prepare(input, new Dictionary<int, int> { [1] = 3 });

        plan.Decisions.Select(d => d.Path).Should().Equal(a, b, c);
    }

    [Fact]
    public void План_считает_освобождаемый_объём()
    {
        using var temp = new TempFolder();
        var a = temp.Write("a.bin", new byte[1000]);
        var b = temp.Write("b.bin", new byte[1000]);

        var plan = Planner().Prepare(
            new[] { Candidate(a, 1), Candidate(b, 1) },
            new Dictionary<int, int> { [1] = 3 });

        plan.BytesFreed.Should().Be(2000);
    }

    [Fact]
    public void Причины_отказа_считаются_по_видам()
    {
        using var temp = new TempFolder();
        var kept = temp.WriteText("a.txt", "одинаково");
        var guarded = temp.WriteText("b.txt", "одинаково");
        var gone = temp.WriteText("c.txt", "одинаково");
        var goneCandidate = Candidate(gone, 1);
        File.Delete(gone);

        var plan = Planner().Prepare(
            new[] { Candidate(kept, 1, original: true), Candidate(guarded, 1, isProtected: true), goneCandidate },
            new Dictionary<int, int> { [1] = 3 });

        plan.RefusalCounts.Should().ContainKey(DeletionRefusal.Protected);
        plan.RefusalCounts.Should().ContainKey(DeletionRefusal.Missing);
        // Оригинал удалить можно: в группе три файла, защищённый останется.
        plan.Allowed.Single().Path.Should().Be(kept);
    }

    [Fact]
    public void Пустой_список_даёт_пустой_план()
    {
        var plan = Planner().Prepare(Array.Empty<DeletionCandidate>(), new Dictionary<int, int>());

        plan.Decisions.Should().BeEmpty();
        plan.HasWork.Should().BeFalse();
        plan.BytesFreed.Should().Be(0);
    }

    [Fact]
    public void Неизвестная_группа_не_даёт_удалить_ничего()
    {
        using var temp = new TempFolder();
        var path = temp.WriteText("a.txt", "x");

        var plan = Planner().Prepare(new[] { Candidate(path, 42) }, new Dictionary<int, int>());

        plan.Refused.Single().Refusal.Should().Be(DeletionRefusal.WouldEmptyGroup);
    }
}
