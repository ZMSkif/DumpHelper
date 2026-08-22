using System.Reflection;
using DupFinder.App.Services;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

/// <summary>
/// Проверяет привязку сборки к git. Тесты собираются из того же репозитория,
/// поэтому штамп должен быть заполнен — если он пуст, значит цель StampGitInfo
/// в Directory.Build.targets перестала срабатывать, и это надо заметить.
/// </summary>
public class BuildInfoTests
{
    [Fact]
    public void Версия_отделена_от_коммита()
    {
        BuildInfo.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        BuildInfo.Version.Should().NotContain("+");
    }

    [Fact]
    public void Информационная_версия_содержит_коммит()
    {
        var informational = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        informational.Should().NotBeNullOrEmpty();
        informational.Should().StartWith(BuildInfo.Version);
    }

    [Fact]
    public void Коммит_и_ветка_вшиты_при_сборке()
    {
        BuildInfo.Commit.Should().MatchRegex("^[0-9a-f]{40}$", "ожидаем полный SHA");
        BuildInfo.Branch.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Короткий_коммит_ровно_двенадцать_знаков()
    {
        BuildInfo.ShortCommit.Should().HaveLength(12);
        BuildInfo.Commit.Should().StartWith(BuildInfo.ShortCommit);
    }

    [Fact]
    public void Подпись_для_окна_содержит_версию_и_коммит()
    {
        BuildInfo.ShortLabel.Should().Contain(BuildInfo.Version);
        BuildInfo.ShortLabel.Should().Contain(BuildInfo.ShortCommit);
    }

    [Fact]
    public void Строка_для_журнала_содержит_полный_коммит_и_ветку()
    {
        BuildInfo.FullLabel.Should().Contain(BuildInfo.Commit);
        BuildInfo.FullLabel.Should().Contain(BuildInfo.Branch);
    }

    [Fact]
    public void Признак_несохранённых_правок_согласован_с_описанием()
    {
        BuildInfo.IsDirty.Should().Be(BuildInfo.Describe.EndsWith("-dirty", StringComparison.Ordinal));
        BuildInfo.Describe.Should().NotContain("-dirty-dirty", "суффикс не должен задваиваться");
    }
}
