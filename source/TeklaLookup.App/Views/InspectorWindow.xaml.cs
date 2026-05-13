using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeklaLookup.App.Models;
using TeklaLookup.App.ViewModels;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Views;

public partial class InspectorWindow : FluentWindow
{
    // Stagger each new inspector so a stack of comparison panes doesn't perfectly overlap.
    private static int _offsetCount;

    public InspectorWindow()
    {
        InitializeComponent();
        var step = (_offsetCount++ % 8) * 28;
        var owner = System.Windows.Application.Current?.MainWindow;
        if (owner is not null)
        {
            Left = owner.Left + 60 + step;
            Top = owner.Top + 60 + step;
        }
    }

    private void OnPropertiesGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not InspectorViewModel vm) return;
        if (e.OriginalSource is not DependencyObject source) return;
        var row = FindAncestor<DataGridRow>(source);
        if (row?.Item is not PropertyEntry entry) return;
        if (vm.DrillIntoCommand.CanExecute(entry))
            vm.DrillIntoCommand.Execute(entry);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null && current is not T)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
