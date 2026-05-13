using System;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Views;

public partial class SearchByIdDialog : FluentWindow
{
    private static readonly char[] Separators = { '\r', '\n', ' ', '\t', ',', ';' };

    public SearchByIdDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus();
    }

    /// <summary>Tokens parsed from the input box after the dialog closes with DialogResult=true.</summary>
    public string[] Tokens { get; private set; } = Array.Empty<string>();

    private void OnFindClicked(object sender, RoutedEventArgs e)
    {
        Tokens = (InputBox.Text ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToArray();
        DialogResult = true;
        Close();
    }
}
