namespace DupFinder.App.ViewModels;

/// <summary>
/// Пункт выпадающего списка: значение из движка плюс подпись и пояснение
/// на языке пользователя. Нужен, чтобы в XAML не было ни enum-ов, ни магических индексов.
/// </summary>
public sealed record ChoiceItem<T>(T Value, string Label, string? Hint = null)
{
    public override string ToString() => Label;

    /// <summary>Доступен ли пункт. Недоступные показываем, но не даём выбрать.</summary>
    public bool IsEnabled { get; init; } = true;
}
