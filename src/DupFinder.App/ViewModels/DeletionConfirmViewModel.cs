using DupFinder.App.Resources;
using DupFinder.Core.Actions;

namespace DupFinder.App.ViewModels;

/// <summary>Строка в списке подтверждения.</summary>
public sealed record DeletionRowViewModel(string Name, string Directory, string Size, string Note);

/// <summary>
/// Показывает, что именно произойдёт при удалении: список файлов и —
/// главное — что программа удалять отказалась и почему. Текстового
/// предупреждения недостаточно: человек должен видеть конкретные файлы.
/// </summary>
public sealed class DeletionConfirmViewModel
{
    public DeletionConfirmViewModel(DeletionPlan plan)
    {
        Plan = plan;

        Allowed = plan.Allowed
            .Select(d => new DeletionRowViewModel(
                System.IO.Path.GetFileName(d.Path),
                System.IO.Path.GetDirectoryName(d.Path) ?? string.Empty,
                DuplicateRowViewModel.FormatSize(d.Candidate.File.Length),
                string.Empty))
            .ToList();

        Refused = plan.Refused
            .Select(d => new DeletionRowViewModel(
                System.IO.Path.GetFileName(d.Path),
                System.IO.Path.GetDirectoryName(d.Path) ?? string.Empty,
                DuplicateRowViewModel.FormatSize(d.Candidate.File.Length),
                Describe(d.Refusal)))
            .ToList();

        Header = plan.HasWork
            ? string.Format(
                Strings.DeleteConfirmHeaderFormat,
                plan.Allowed.Count,
                DuplicateRowViewModel.FormatSize(plan.BytesFreed))
            : Strings.DeleteConfirmNothing;

        SkippedHeader = string.Format(Strings.DeleteConfirmSkippedFormat, plan.Refused.Count);
    }

    public DeletionPlan Plan { get; }

    public IReadOnlyList<DeletionRowViewModel> Allowed { get; }

    public IReadOnlyList<DeletionRowViewModel> Refused { get; }

    public string Header { get; }

    public string SkippedHeader { get; }

    public bool HasRefused => Refused.Count > 0;

    public bool CanProceed => Plan.HasWork;

    /// <summary>Причина отказа человеческим языком.</summary>
    public static string Describe(DeletionRefusal refusal) => refusal switch
    {
        DeletionRefusal.Protected => Strings.RefusalProtected,
        DeletionRefusal.WouldEmptyGroup => Strings.RefusalWholeGroup,
        DeletionRefusal.Changed => Strings.RefusalChanged,
        DeletionRefusal.Missing => Strings.RefusalMissing,
        _ => string.Empty,
    };
}
