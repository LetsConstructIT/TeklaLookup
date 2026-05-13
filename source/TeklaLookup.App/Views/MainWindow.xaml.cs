using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services;
using TeklaLookup.App.ViewModels;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyPersistedPlacement();
        SourceInitialized += (_, _) => ApplyPersistedPlacement();
        Closing += OnClosing;
    }

    /// <summary>
    /// Restores window left/top/size/maximized from <see cref="SettingsStore"/> if the saved values
    /// still land on a visible monitor. Falls back to the XAML defaults when settings are missing
    /// or the window would open off-screen (e.g. an unplugged secondary monitor).
    /// </summary>
    private void ApplyPersistedPlacement()
    {
        var s = SettingsStore.Current;
        if (s.WindowWidth is double w && w > 200) Width = w;
        if (s.WindowHeight is double h && h > 200) Height = h;
        if (s.WindowLeft is double l && s.WindowTop is double t && IsOnAnyScreen(l, t))
        {
            Left = l;
            Top = t;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        if (s.IsMaximized)
            WindowState = WindowState.Maximized;
    }

    private static bool IsOnAnyScreen(double left, double top)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var bounds = screen.Bounds;
            if (left >= bounds.Left - 50 && left <= bounds.Right - 100 &&
                top  >= bounds.Top  - 10 && top  <= bounds.Bottom - 100)
                return true;
        }
        return false;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = SettingsStore.Current;
        s.IsMaximized = WindowState == WindowState.Maximized;
        // RestoreBounds gives us the normal-state rect even when the window is maximized,
        // so we don't lose the user's preferred un-maximized size.
        var rect = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        s.WindowLeft   = rect.Left;
        s.WindowTop    = rect.Top;
        s.WindowWidth  = rect.Width;
        s.WindowHeight = rect.Height;
        SettingsStore.Save();
    }

    /// <summary>
    /// Same trick as in the event monitor: WPF doesn't auto-select on right-click, so opening the
    /// context menu on an unselected row would pass stale selection to the command.
    /// </summary>
    private void OnObjectsPreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.DependencyObject source) return;
        var row = FindAncestor<DataGridRow>(source);
        if (row is null || row.IsSelected) return;
        ObjectsGrid.SelectedItems.Clear();
        row.IsSelected = true;
    }

    private void OnPropertiesGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if (e.OriginalSource is not DependencyObject source) return;

        var row = FindAncestor<DataGridRow>(source);
        if (row?.Item is not PropertyEntry entry) return;

        if (viewModel.DrillIntoCommand.CanExecute(entry))
            viewModel.DrillIntoCommand.Execute(entry);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null && current is not T)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
