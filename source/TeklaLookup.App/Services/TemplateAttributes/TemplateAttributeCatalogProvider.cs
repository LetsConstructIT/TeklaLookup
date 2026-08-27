using System;
using System.Collections.Generic;
using System.IO;
using Tekla.Structures;
using Tekla.Structures.Model;

namespace TeklaLookup.App.Services.TemplateAttributes;

/// <summary>
/// Finds the environment's <c>contentattributes*.lst</c> files and caches the parsed
/// <see cref="TemplateAttributeCatalog"/>.
/// </summary>
/// <remarks>
/// Search order mirrors Tekla's own: model folder, then <c>XS_PROJECT</c>, <c>XS_FIRM</c> and
/// <c>XS_SYSTEM</c>. Each of those roots is checked directly and under <c>template\settings\</c>,
/// which is where stock environments actually keep the files. First file of a given NAME wins, per
/// filename rather than per folder — a project that overrides only
/// <c>contentattributes_userdefined.lst</c> still inherits the system <c>_global</c> one.
/// <para>
/// The cache is keyed on the open model's path, so opening a model in a different environment
/// picks up that environment's attributes without anyone having to invalidate anything.
/// </para>
/// </remarks>
public static class TemplateAttributeCatalogProvider
{
    private static readonly string[] SearchOptions = { "XS_PROJECT", "XS_FIRM", "XS_SYSTEM" };

    /// <summary>
    /// Where the Template Editor settings folder sits relative to an installation root. Two forms
    /// because <c>XS_DIR</c> points at the installation while the API assembly resolves inside
    /// <c>bin</c>, and either may be what we are walking from.
    /// </summary>
    private static readonly string[] TplEdRelativePaths =
    {
        Path.Combine("bin", "applications", "Tekla", "Tools", "TplEd", "settings"),
        Path.Combine("applications", "Tekla", "Tools", "TplEd", "settings"),
    };
    private static readonly object Gate = new();

    private static TemplateAttributeCatalog? _catalog;
    private static string? _catalogModelPath;

    /// <summary>
    /// The catalog for the connected model, loaded on first use. Never null and never throws —
    /// with no Tekla connection this is <see cref="TemplateAttributeCatalog.Empty"/> and callers
    /// simply show no template attributes.
    /// </summary>
    public static TemplateAttributeCatalog Current
    {
        get
        {
            // No model, no environment to read: bail out before touching the file system, and
            // without caching, so the catalog appears as soon as a model is opened.
            var modelPath = TryGetModelPath();
            if (modelPath is null) return TemplateAttributeCatalog.Empty;

            lock (Gate)
            {
                if (_catalog is not null &&
                    string.Equals(_catalogModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    return _catalog;
                }

                _catalog = LoadSafely(modelPath);
                _catalogModelPath = modelPath;
                return _catalog;
            }
        }
    }

    private static TemplateAttributeCatalog LoadSafely(string modelPath)
    {
        try
        {
            var files = DiscoverFiles(modelPath, out var searched);
            return TemplateAttributeCatalog.Load(files, searched);
        }
        catch (Exception)
        {
            // Attribute discovery is an enhancement, never a reason to fail decomposition.
            return TemplateAttributeCatalog.Empty;
        }
    }

    /// <param name="searched">
    /// Every directory actually probed. Reported back so a "found nothing" outcome can say where
    /// it looked instead of leaving the user to guess.
    /// </param>
    private static IReadOnlyList<string> DiscoverFiles(string modelPath, out IReadOnlyList<string> searched)
    {
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        var probed = new List<string>();
        searched = probed;

        foreach (var directory in CandidateDirectories(modelPath))
        {
            if (!visited.Add(directory)) continue;
            probed.Add(directory);

            string[] files;
            try
            {
                if (!Directory.Exists(directory)) continue;
                // Leading wildcard on purpose: environments prefix their own files
                // (FI_contentattributes_userdefined.lst), and those are real attribute sources.
                files = Directory.GetFiles(directory, "*contentattributes*.lst");
            }
            catch (Exception)
            {
                continue; // unreadable or malformed path — skip it, keep searching
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (claimed.Add(Path.GetFileName(file)))
                    found.Add(file);
            }
        }

        return found;
    }

    private static IEnumerable<string> CandidateDirectories(string modelPath)
    {
        foreach (var root in SearchRoots(modelPath))
        {
            string? settings;
            try
            {
                settings = Path.Combine(root, "template", "settings");
            }
            catch (ArgumentException)
            {
                continue; // invalid characters in an advanced option entry
            }

            yield return root;
            yield return settings;
        }
    }

    private static IEnumerable<string> SearchRoots(string modelPath)
    {
        yield return modelPath;
        yield return Path.Combine(modelPath, "attributes");

        foreach (var option in SearchOptions)
        {
            foreach (var path in TryGetOptionPaths(option))
                yield return path;
        }

        // Last, so anything the environment defines wins. This is where the stock definitions
        // actually live — an environment like Finland ships only a container file that INCLUDEs
        // contentattributes_global.lst from here, so without this root it resolves to nothing.
        foreach (var path in TemplateEditorSettingsDirectories())
            yield return path;
    }

    /// <summary>
    /// The Template Editor's own settings folder inside the Tekla installation
    /// (<c>bin\applications\Tekla\Tools\TplEd\settings</c>), which holds the default attribute set.
    /// </summary>
    /// <remarks>
    /// Located from <c>XS_DIR</c> where it is set, and otherwise from the folder the Tekla API
    /// assembly was actually loaded from — that is inside the running installation by definition,
    /// which makes it a reliable fallback when the advanced option is unavailable.
    /// </remarks>
    private static IEnumerable<string> TemplateEditorSettingsDirectories()
    {
        foreach (var root in InstallationRoots())
        {
            foreach (var relative in TplEdRelativePaths)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(root, relative);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> InstallationRoots()
    {
        var installDirectory = TryGetStringOption("XS_DIR");
        if (!string.IsNullOrWhiteSpace(installDirectory))
            yield return installDirectory!;

        string? assemblyDirectory = null;
        try
        {
            var location = typeof(Model).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
                assemblyDirectory = Path.GetDirectoryName(location);
        }
        catch (Exception)
        {
            // Loaded from the GAC or otherwise without a file path — nothing to derive.
        }

        // Walk up from the assembly: it sits under <install>\bin, so <install> is a level or two up.
        for (var i = 0; i < 3 && assemblyDirectory is not null; i++)
        {
            yield return assemblyDirectory;
            assemblyDirectory = Path.GetDirectoryName(assemblyDirectory);
        }
    }

    private static string? TryGetStringOption(string option)
    {
        try
        {
            var value = string.Empty;
            return TeklaStructuresSettings.GetAdvancedOption(option, ref value) ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryGetModelPath()
    {
        try
        {
            var model = new Model();
            if (!model.GetConnectionStatus()) return null;
            var path = model.GetInfo().ModelPath;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> TryGetOptionPaths(string option)
    {
        try
        {
            // Splits the semicolon-separated advanced option and drops blanks for us.
            //
            // The return value is deliberately IGNORED. It means "read successfully AND every
            // entry was a valid path" — one bad entry in a long XS_SYSTEM list makes it false
            // while `paths` still holds all the good ones. Gating on it discards a whole
            // environment because of a single stale folder, which is exactly what stock
            // environments contain.
            TeklaStructuresSettings.GetAdvancedOptionPaths(
                option, out var paths, (advancedOption, invalidString, exceptionMessage) => { });

            if (paths is not null) return paths;
        }
        catch (Exception)
        {
            // Older or oddly-configured Tekla — treat as "option not set".
        }

        return new string[0];
    }
}
