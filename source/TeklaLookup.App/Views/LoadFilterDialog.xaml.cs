using System;
using System.Windows;
using TeklaLookup.App.ViewModels;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Views;

public partial class LoadFilterDialog : FluentWindow
{
    public LoadFilterDialog()
    {
        InitializeComponent();
    }

    /// <summary>Selected types after the dialog closes with DialogResult=true.</summary>
    public Type[] SelectedTypes { get; private set; } = Array.Empty<Type>();

    private void OnLoadClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoadFilterDialogViewModel vm)
            SelectedTypes = vm.SelectedTeklaTypes;
        DialogResult = true;
        Close();
    }
}
