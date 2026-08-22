using DupFinder.App.Services;

namespace DupFinder.App.Tests;

/// <summary>Диалоги-заглушки: тесты не должны показывать окна.</summary>
public sealed class FakeDialogService : IDialogService
{
    public List<string> Warnings { get; } = new();

    public List<string> Questions { get; } = new();

    /// <summary>Что вернуть на «да/нет».</summary>
    public bool ConfirmAnswer { get; set; } = true;

    public string? FolderToPick { get; set; }

    public string? PickFolder(string title, string? initialPath = null) => FolderToPick;

    public void Warn(string message) => Warnings.Add(message);

    public void ShowError(string message, string logFolder) => Warnings.Add(message);

    public bool Confirm(string message, string title)
    {
        Questions.Add(message);
        return ConfirmAnswer;
    }

    public int LogShown { get; private set; }

    /// <summary>Планы, которые показывали на подтверждение.</summary>
    public List<DupFinder.Core.Actions.DeletionPlan> Plans { get; } = new();

    public bool ConfirmDeletion(DupFinder.Core.Actions.DeletionPlan plan)
    {
        Plans.Add(plan);
        return ConfirmAnswer;
    }

    public void ShowLog() => LogShown++;

    public int JournalShown { get; private set; }

    public void ShowOperationJournal(DupFinder.Core.Actions.OperationJournal journal) => JournalShown++;

    public ThreeWayAnswer Ask(string message, string title)
    {
        Questions.Add(message);
        return ConfirmAnswer ? ThreeWayAnswer.Yes : ThreeWayAnswer.No;
    }
}

/// <summary>Оболочка-заглушка.</summary>
public sealed class FakeShellService : IShellService
{
    public List<string> Revealed { get; } = new();

    public List<string> Opened { get; } = new();

    public string? Clipboard { get; private set; }

    public void RevealInExplorer(string path) => Revealed.Add(path);

    public void Open(string path) => Opened.Add(path);

    public void CopyToClipboard(string text) => Clipboard = text;
}

/// <summary>Корзина-заглушка: ничего не удаляет, но помнит, что просили.</summary>
public sealed class FakeRecycleBin : IRecycleBin
{
    public List<string> Requested { get; } = new();

    /// <summary>Пути, которые «не удаётся» удалить.</summary>
    public HashSet<string> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DeleteResult Delete(IReadOnlyList<string> paths)
    {
        Requested.AddRange(paths);
        var failed = paths.Where(Failing.Contains).ToList();
        return new DeleteResult(paths.Count - failed.Count, failed);
    }
}
