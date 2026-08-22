using DupFinder.Core.Hashing;
using DupFinder.Core.Model;
using DupFinder.Core.Scanning;
using FluentAssertions;
using Xunit;

namespace DupFinder.Core.Tests;

public class ProgressThrottleTests
{
    [Fact]
    public void Сообщения_прореживаются()
    {
        var received = new List<ScanProgress>();
        var throttle = new ProgressThrottle(new SyncProgress(received), TimeSpan.FromSeconds(30));

        for (var i = 0; i < 100; i++)
        {
            throttle.Report(ScanStage.PartialHash, i, 100, "шаг", 0);
        }

        received.Should().HaveCount(1, "интервал ещё не истёк");
    }

    [Fact]
    public void Завершающая_стадия_проходит_всегда()
    {
        var received = new List<ScanProgress>();
        var throttle = new ProgressThrottle(new SyncProgress(received), TimeSpan.FromSeconds(30));

        throttle.Report(ScanStage.PartialHash, 1, 100, "шаг", 0);
        throttle.Report(ScanStage.PartialHash, 2, 100, "шаг", 0);
        throttle.Report(ScanStage.Completed, 100, 100, "готово", 0);

        received.Should().HaveCount(2);
        received[^1].Stage.Should().Be(ScanStage.Completed);
    }

    [Fact]
    public void Без_получателя_ничего_не_ломается()
    {
        var act = () => new ProgressThrottle(null).Report(ScanStage.Completed, 1, 1, "x", 0);

        act.Should().NotThrow();
    }

    [Fact]
    public void Доля_выполнения_считается_и_ограничивается()
    {
        new ScanProgress(ScanStage.FullHash, 50, 100, "", 0).Fraction.Should().Be(0.5);
        new ScanProgress(ScanStage.FullHash, 150, 100, "", 0).Fraction.Should().Be(1);
        new ScanProgress(ScanStage.FullHash, 5, 0, "", 0).Fraction.Should().BeNull();
    }

    private sealed class SyncProgress : IProgress<ScanProgress>
    {
        private readonly List<ScanProgress> _sink;

        public SyncProgress(List<ScanProgress> sink) => _sink = sink;

        public void Report(ScanProgress value) => _sink.Add(value);
    }
}

public class ParallelismPlannerTests
{
    [Fact]
    public void HDD_и_сеть_получают_мало_читателей()
    {
        ParallelismPlanner.ForKind(DiskKind.Hdd).Should().Be(2);
        ParallelismPlanner.ForKind(DiskKind.Network).Should().Be(2);
    }

    [Fact]
    public void SSD_получает_читателей_по_числу_ядер() =>
        ParallelismPlanner.ForKind(DiskKind.Ssd).Should().Be(Math.Max(2, Environment.ProcessorCount));

    [Fact]
    public void Явное_число_потоков_имеет_приоритет()
    {
        var options = ScanOptions.ForRoot("/data") with { MaxReaders = 3, DiskKind = DiskKind.Ssd };

        ParallelismPlanner.Plan(options, new[] { "/data" }).Should().Be(3);
    }

    [Fact]
    public void Сетевой_путь_определяется_как_медленный() =>
        ParallelismPlanner.Detect(new[] { @"\\server\share\photos" }).Should().Be(DiskKind.Network);
}

public class ScanOptionsTests
{
    [Fact]
    public void Без_папок_настройки_не_проходят_проверку()
    {
        var options = ScanOptions.ForRoot("/data") with { Roots = Array.Empty<string>() };

        options.Invoking(o => o.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Отрицательный_минимальный_размер_не_проходит()
    {
        var options = ScanOptions.ForRoot("/data") with { MinBytes = -1 };

        options.Invoking(o => o.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Максимум_меньше_минимума_не_проходит()
    {
        var options = ScanOptions.ForRoot("/data") with { MinBytes = 100, MaxBytes = 10 };

        options.Invoking(o => o.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Порог_сходства_вне_диапазона_не_проходит()
    {
        var options = ScanOptions.ForRoot("/data") with { SimilarityThreshold = 99 };

        options.Invoking(o => o.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Способ_подтверждения_выводится_из_флага()
    {
        ScanOptions.ForRoot("/data").Confirmation.Should().Be(ExactConfirmation.Sha256);
        (ScanOptions.ForRoot("/data") with { ConfirmBytewise = true }).Confirmation
            .Should().Be(ExactConfirmation.Bytewise);
    }

    [Fact]
    public void Настройки_по_умолчанию_проходят_проверку() =>
        ScanOptions.ForRoot("/data").Invoking(o => o.Validate()).Should().NotThrow();
}
