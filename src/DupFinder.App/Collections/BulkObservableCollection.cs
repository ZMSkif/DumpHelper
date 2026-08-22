using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DupFinder.App.Collections;

/// <summary>
/// Коллекция, которая умеет принимать пачку элементов за одно уведомление.
/// Без этого 200 000 строк дают 200 000 событий и Dispatcher встаёт (ТЗ §3).
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppress;

    public BulkObservableCollection()
    {
    }

    public BulkObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    /// <summary>Добавляет пачку и уведомляет один раз.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var added = false;
        _suppress = true;
        try
        {
            foreach (var item in items)
            {
                Items.Add(item);
                added = true;
            }
        }
        finally
        {
            _suppress = false;
        }

        if (added)
        {
            RaiseReset();
        }
    }

    /// <summary>Заменяет содержимое целиком и уведомляет один раз.</summary>
    public void Reset(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppress = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppress = false;
        }

        RaiseReset();
    }

    /// <summary>Удаляет пачку элементов и уведомляет один раз.</summary>
    public void RemoveRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var doomed = new HashSet<T>(items);
        if (doomed.Count == 0)
        {
            return;
        }

        var kept = Items.Where(i => !doomed.Contains(i)).ToList();
        Reset(kept);
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppress)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppress)
        {
            base.OnPropertyChanged(e);
        }
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
