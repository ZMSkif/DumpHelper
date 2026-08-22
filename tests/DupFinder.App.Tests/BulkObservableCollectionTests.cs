using System.Collections.Specialized;
using DupFinder.App.Collections;
using FluentAssertions;
using Xunit;

namespace DupFinder.App.Tests;

public class BulkObservableCollectionTests
{
    [Fact]
    public void Пачка_добавляется_за_одно_уведомление()
    {
        var collection = new BulkObservableCollection<int>();
        var events = 0;
        collection.CollectionChanged += (_, _) => events++;

        collection.AddRange(Enumerable.Range(0, 1000));

        collection.Should().HaveCount(1000);
        events.Should().Be(1, "1000 отдельных событий положили бы Dispatcher");
    }

    [Fact]
    public void Пустая_пачка_не_шумит()
    {
        var collection = new BulkObservableCollection<int>();
        var events = 0;
        collection.CollectionChanged += (_, _) => events++;

        collection.AddRange(Array.Empty<int>());

        events.Should().Be(0);
    }

    [Fact]
    public void Замена_содержимого_даёт_ровно_один_Reset()
    {
        var collection = new BulkObservableCollection<int>(new[] { 1, 2, 3 });
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.Reset(new[] { 7, 8 });

        collection.Should().Equal(7, 8);
        actions.Should().Equal(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void Удаление_пачки_оставляет_остальные_элементы()
    {
        var collection = new BulkObservableCollection<int>(new[] { 1, 2, 3, 4, 5 });

        collection.RemoveRange(new[] { 2, 4 });

        collection.Should().Equal(1, 3, 5);
    }

    [Fact]
    public void Удаление_пустого_набора_ничего_не_меняет()
    {
        var collection = new BulkObservableCollection<int>(new[] { 1, 2 });
        var events = 0;
        collection.CollectionChanged += (_, _) => events++;

        collection.RemoveRange(Array.Empty<int>());

        collection.Should().Equal(1, 2);
        events.Should().Be(0);
    }
}
