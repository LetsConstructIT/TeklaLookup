using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TeklaLookup.App.ViewModels;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Views;

public partial class EventMonitorWindow : FluentWindow
{
    public EventMonitorWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not EventMonitorViewModel vm) return;
        vm.Entries.CollectionChanged += OnEntriesChanged;
        vm.Start();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (DataContext is EventMonitorViewModel vm)
        {
            vm.Entries.CollectionChanged -= OnEntriesChanged;
            vm.Dispose();
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (EntriesGrid.Items.Count == 0) return;
        EntriesGrid.ScrollIntoView(EntriesGrid.Items[EntriesGrid.Items.Count - 1]);
    }

    /// <summary>
    /// WPF's default DataGrid doesn't select on right-click. Without this, opening the context
    /// menu on an unselected row would still pass the *previously* selected rows to the command.
    /// We promote the right-clicked row into the selection if it isn't already.
    /// </summary>
    private void OnEntriesPreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var row = FindAncestor<DataGridRow>(source);
        if (row is null) return;

        if (!row.IsSelected)
        {
            EntriesGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null && current is not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
