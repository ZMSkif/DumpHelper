using DupFinder.App.ViewModels;
using DupFinder.Core.Model;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

public class ScanViewModelTests
{
    private sealed class Folder : IDisposable
    {
        public Folder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dupfinder-app-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Уборка не должна ронять прогон.
            }
        }
    }

    [Fact]
    public void Без_папок_настройки_не_собираются()
    {
        var dialogs = new FakeDialogService();
        var vm = new ScanViewModel(dialogs);

        vm.BuildOptions().Should().BeNull();
        dialogs.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void Папка_добавляется_один_раз()
    {
        using var folder = new Folder();
        var vm = new ScanViewModel(new FakeDialogService());

        vm.AddRoot(folder.Path);
        vm.AddRoot(folder.Path);

        vm.Roots.Should().ContainSingle();
        vm.HasRoots.Should().BeTrue();
    }

    [Fact]
    public void Несуществующая_папка_не_добавляется()
    {
        var vm = new ScanViewModel(new FakeDialogService());

        vm.AddRoot(Path.Combine(Path.GetTempPath(), "нет-такой-папки-" + Guid.NewGuid().ToString("N")));

        vm.Roots.Should().BeEmpty();
    }

    [Fact]
    public void Настройки_переносят_значения_из_формы()
    {
        using var folder = new Folder();
        var vm = new ScanViewModel(new FakeDialogService());
        vm.AddRoot(folder.Path);
        vm.MinSizeKb = "4";
        vm.MaxSizeMb = "10";
        vm.ExcludeMasks = "*.tmp; ~$*";
        vm.Recurse = false;
        vm.IncludeHidden = true;
        vm.ConfirmBytewise = true;
        vm.SelectedKind = vm.Kinds.Single(k => k.Value == FileKindFilter.Photo);
        vm.SelectedOriginalRule = vm.OriginalRules.Single(r => r.Value == OriginalRule.Newest);
        vm.SelectedDiskKind = vm.DiskKinds.Single(d => d.Value == DiskKind.Hdd);

        var options = vm.BuildOptions();

        options.Should().NotBeNull();
        options!.MinBytes.Should().Be(4 * 1024);
        options.MaxBytes.Should().Be(10L * 1024 * 1024);
        options.ExcludeMasks.Should().Equal("*.tmp", "~$*");
        options.Recurse.Should().BeFalse();
        options.IncludeHidden.Should().BeTrue();
        options.ConfirmBytewise.Should().BeTrue();
        options.Kinds.Should().Be(FileKindFilter.Photo);
        options.OriginalRule.Should().Be(OriginalRule.Newest);
        options.DiskKind.Should().Be(DiskKind.Hdd);
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Нечисловой_размер_считается_нулём()
    {
        using var folder = new Folder();
        var vm = new ScanViewModel(new FakeDialogService());
        vm.AddRoot(folder.Path);
        vm.MinSizeKb = "не число";

        vm.BuildOptions()!.MinBytes.Should().Be(0);
    }

    [Fact]
    public void Несуществующая_папка_эталон_блокирует_запуск()
    {
        using var folder = new Folder();
        var dialogs = new FakeDialogService();
        var vm = new ScanViewModel(dialogs);
        vm.AddRoot(folder.Path);
        vm.ReferenceFolder = Path.Combine(Path.GetTempPath(), "нет-эталона-" + Guid.NewGuid().ToString("N"));

        vm.BuildOptions().Should().BeNull();
        dialogs.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void По_умолчанию_выбраны_точные_копии()
    {
        var vm = new ScanViewModel(new FakeDialogService());

        vm.SelectedMode.Should().Be(ScanMode.Exact);
        vm.Modes.Single(m => m.Value == ScanMode.Exact).IsSelected.Should().BeTrue();
        vm.ModeHint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Режимы_без_движка_помечены_недоступными()
    {
        var vm = new ScanViewModel(new FakeDialogService());

        vm.Modes.Single(m => m.Value == ScanMode.Exact).IsAvailable.Should().BeTrue();
        vm.Modes.Single(m => m.Value == ScanMode.SameShot).IsAvailable.Should().BeFalse();
        vm.Modes.Single(m => m.Value == ScanMode.Similar).IsAvailable.Should().BeFalse();
        vm.Modes.Where(m => !m.IsAvailable).Should().OnlyContain(m => m.ShowsBadge);
    }

    [Fact]
    public void Выбор_карточки_снимает_остальные_и_меняет_режим()
    {
        var vm = new ScanViewModel(new FakeDialogService());
        var similar = vm.Modes.Single(m => m.Value == ScanMode.Similar);

        similar.IsSelected = true;

        vm.SelectedMode.Should().Be(ScanMode.Similar);
        vm.Modes.Count(m => m.IsSelected).Should().Be(1);
        vm.ModeHint.Should().Be(similar.Description);
    }

    [Fact]
    public void Полоса_прогресса_до_запуска_ничего_не_показывает()
    {
        var vm = new ScanViewModel(new FakeDialogService());

        vm.IsBusy.Should().BeFalse();
        vm.IsIndeterminate.Should().BeFalse("до первого запуска анимации быть не должно");
        vm.ProgressValue.Should().Be(0);
    }

    [Fact]
    public void Прогресс_наполняет_строку_состояния()
    {
        var vm = new ScanViewModel(new FakeDialogService());
        vm.BeginScan();

        vm.ApplyProgress(new ScanProgress(ScanStage.FullHash, 50, 100, "", 1_000_000));

        vm.IsIndeterminate.Should().BeFalse();
        vm.ProgressValue.Should().Be(50);
        vm.DetailText.Should().NotBeEmpty();
    }

    [Fact]
    public void Завершение_снимает_признак_занятости()
    {
        var vm = new ScanViewModel(new FakeDialogService());
        vm.BeginScan();
        vm.IsBusy.Should().BeTrue();

        vm.EndScan("готово");

        vm.IsBusy.Should().BeFalse();
        vm.StatusText.Should().Be("готово");
    }
}
