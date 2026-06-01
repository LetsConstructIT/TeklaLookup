using System.IO;
using System.Text;

namespace TeklaLookup.App.Services;

/// <summary>
/// Shared "save a JSON dump to disk" helper used by the main and inspector view-models. Pops a
/// save-file dialog and writes UTF-8 (no BOM) text. Returns the chosen path, or <c>null</c> when
/// the user cancels. IO failures propagate so callers can surface them in their status line.
/// </summary>
public static class JsonDumpFile
{
    public static string? Save(string json, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save object dump as JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = Sanitize(suggestedName),
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return null;

        File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
        return dialog.FileName;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
