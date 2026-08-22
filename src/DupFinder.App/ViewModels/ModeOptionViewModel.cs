using CommunityToolkit.Mvvm.ComponentModel;
using DupFinder.App.Resources;
using DupFinder.Core.Model;

namespace DupFinder.App.ViewModels;

/// <summary>
/// Режим поиска как карточка с объяснением простым языком (ТЗ §5).
/// Недоступные режимы не прячем, а показываем с пометкой «скоро» — иначе
/// непонятно, почему выбрать нельзя.
/// </summary>
public sealed partial class ModeOptionViewModel : ObservableObject
{
    private readonly Action<ModeOptionViewModel> _selected;

    [ObservableProperty]
    private bool _isSelected;

    public ModeOptionViewModel(
        ScanMode value,
        string title,
        string description,
        bool isAvailable,
        Action<ModeOptionViewModel> selected)
    {
        Value = value;
        Title = title;
        Description = description;
        IsAvailable = isAvailable;
        _selected = selected;
    }

    public ScanMode Value { get; }

    public string Title { get; }

    public string Description { get; }

    /// <summary>Есть ли движок для этого режима в текущей сборке.</summary>
    public bool IsAvailable { get; }

    /// <summary>Показывать ли пометку «скоро».</summary>
    public bool ShowsBadge => !IsAvailable;

    public string Badge => Strings.ModeComingSoon;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _selected(this);
        }
    }
}
