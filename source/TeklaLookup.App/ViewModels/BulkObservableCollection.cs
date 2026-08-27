using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TeklaLookup.App.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> whose whole content can be swapped in one
/// notification.
/// </summary>
/// <remarks>
/// The properties grid is grouped, so WPF rebuilds its groups — and the DataGrid its rows — on
/// every single change notification. Filling it row by row therefore costs O(n) regroups; a
/// single reset costs one, which is what makes re-rendering after a pin toggle feel instant.
/// </remarks>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces every item with <paramref name="items"/>, raising a single reset.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();

        // Copy first: the source may be (or be derived from) this collection's own items.
        var replacement = new List<T>(items);

        Items.Clear();
        foreach (var item in replacement)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
