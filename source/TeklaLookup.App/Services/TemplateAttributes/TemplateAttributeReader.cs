using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.TemplateAttributes;

/// <summary>
/// Reads template (report) attributes off a <see cref="ModelObject"/>, driven by the environment's
/// declared attribute list rather than a hardcoded handful of names.
/// </summary>
/// <remarks>
/// Values are fetched with one <c>GetAllReportProperties</c> call per datatype bucket, which is
/// what makes a list this size affordable: the catalog already knows whether a name is a string,
/// a double or an integer, so nothing has to be probed by trial and error.
/// </remarks>
public sealed class TemplateAttributeReader
{
    /// <summary>
    /// Direct attributes of the object itself, as opposed to a related object's
    /// (<c>NUT.WEIGHT</c>, <c>ASSEMBLY.MAINPART.PROFILE</c>).
    /// </summary>
    public const string DirectCategory = PropertyCategories.TemplateAttributes;

    private readonly TemplateAttributeCatalog? _catalog;

    public TemplateAttributeReader() { }

    /// <summary>Pins the reader to a specific catalog; otherwise the open model's is used.</summary>
    public TemplateAttributeReader(TemplateAttributeCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// Resolved per call rather than at construction: the decomposer is built at window start-up,
    /// which is routinely before a model is connected and therefore before any catalog exists.
    /// </summary>
    private TemplateAttributeCatalog Catalog => _catalog ?? TemplateAttributeCatalogProvider.Current;

    /// <summary>
    /// Resolves the attributes visible for <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The object to read from.</param>
    /// <param name="scope">How much of the declared attribute list to read.</param>
    /// <param name="pinned">
    /// Names the user has pinned for this content type. These are read whatever group they belong
    /// to and whatever the scope, and are reported even when the object has no value for them — a
    /// pin that silently vanished would look like a bug.
    /// </param>
    /// <param name="maxAttributes">
    /// Ceiling on how many attributes one pass will read. Direct and pinned attributes are never
    /// dropped; only related-object ones are cut, and the caller is told when that happened.
    /// </param>
    public IReadOnlyList<PropertyEntry> Read(
        ModelObject target,
        TemplateAttributeScope scope,
        IReadOnlyCollection<string>? pinned = null,
        int maxAttributes = int.MaxValue)
    {
        var pinnedSet = pinned is null || pinned.Count == 0
            ? null
            : new HashSet<string>(pinned, StringComparer.Ordinal);

        // Switched off with nothing pinned: there is genuinely nothing to do, so return before
        // touching the catalog or Tekla at all.
        if (scope == TemplateAttributeScope.PinnedOnly && pinnedSet is null)
            return new PropertyEntry[0];

        var contentType = TeklaContentTypes.For(target);
        if (contentType is null) return new PropertyEntry[0];

        var catalog = Catalog;

        // The object HAS template attributes, we just couldn't find the files that name them.
        // Say so, and say where we looked: silently showing nothing looks identical to "this
        // object has none", and a bare failure gives the user nothing to act on. Only worth saying
        // when the user actually asked for the list — otherwise it is a nag about a switched-off
        // feature, and any pins simply have nothing to resolve against.
        if (catalog.IsEmpty)
        {
            if (scope == TemplateAttributeScope.PinnedOnly) return new PropertyEntry[0];

            return new[]
            {
                new PropertyEntry
                {
                    Category = DirectCategory,
                    Name = "<no catalog>",
                    Value = DescribeMissingCatalog(catalog),
                },
            };
        }

        var definitions = catalog.ForContentType(contentType);
        if (definitions.Count == 0) return new PropertyEntry[0];

        var selected = new List<TemplateAttributeDefinition>(definitions.Count);
        var truncated = false;
        foreach (var definition in definitions)
        {
            if (!TemplateAttributeSelection.IsSelected(definition, scope, pinnedSet)) continue;

            // A connection declares over 13,000 attributes once related objects are in scope, and
            // they are read in one blocking interop call that cannot be cancelled part-way. Cap it
            // — but never at the expense of a direct attribute or one the user explicitly pinned.
            var protectedFromCap = definition.IsDirect ||
                                   (pinnedSet is not null && pinnedSet.Contains(definition.Name));
            if (!protectedFromCap && selected.Count >= maxAttributes)
            {
                truncated = true;
                continue;
            }

            selected.Add(definition);
        }

        if (selected.Count == 0) return new PropertyEntry[0];

        var entries = new List<PropertyEntry>(selected.Count);
        if (!TryReadValues(target, selected, out var values, out var error))
        {
            entries.Add(new PropertyEntry
            {
                Category = DirectCategory,
                Name = "<error>",
                Value = error,
            });
            return entries;
        }

        foreach (var definition in selected)
        {
            var hasValue = values.ContainsKey(definition.Name);
            var isPinned = pinnedSet is not null && pinnedSet.Contains(definition.Name);

            // An attribute the object simply doesn't carry is noise — unless it is pinned, in
            // which case its absence is the answer the user asked for.
            if (!hasValue && !isPinned) continue;

            var raw = hasValue ? values[definition.Name] : null;
            entries.Add(new PropertyEntry
            {
                Category = CategoryFor(definition),
                Name = definition.Name,
                Value = hasValue ? FormatValue(raw) : null,
                ValueType = ValueTypeName(definition.ValueType),
                RawValue = raw,
                Description = definition.Label,
            });
        }

        if (truncated)
        {
            entries.Add(new PropertyEntry
            {
                Category = DirectCategory,
                Name = "<truncated>",
                Value = $"Stopped after {maxAttributes} attributes. Pin the related-object ones you "
                      + "need — pinned attributes are always read.",
            });
        }

        return entries;
    }

    /// <summary>
    /// Explains an empty catalog in terms the user can act on: whether anything was searched at
    /// all (no model connection vs. no files), and a sample of the folders actually probed.
    /// </summary>
    private static string DescribeMissingCatalog(TemplateAttributeCatalog catalog)
    {
        if (catalog.LoadError is not null)
            return $"Could not read the attribute definitions: {catalog.LoadError}";

        if (catalog.SearchedDirectories.Count == 0)
        {
            return "No model folder resolved, so the environment was never searched. "
                 + "Is a model open in Tekla?";
        }

        // Files found but nothing parsed out of them is a different failure from finding nothing,
        // and conflating the two sends you looking in the wrong place.
        if (catalog.SourceFiles.Count > 0)
        {
            var read = string.Join("; ", catalog.SourceFiles.Take(3).Select(Path.GetFileName));
            return $"Read {catalog.SourceFiles.Count} attribute file(s) but none declared any "
                 + $"attributes (e.g. {read}). They may be container files whose [INCLUDE] targets "
                 + "are missing from this installation.";
        }

        var sample = string.Join("; ", catalog.SearchedDirectories.Take(3));
        return $"No contentattributes*.lst found in {catalog.SearchedDirectories.Count} folder(s) "
             + $"under the model, XS_PROJECT, XS_FIRM, XS_SYSTEM or the Template Editor settings. "
             + $"Searched e.g. {sample}";
    }

    private static string CategoryFor(TemplateAttributeDefinition definition)
        => definition.IsDirect
            ? DirectCategory
            : $"{DirectCategory} · {definition.Group}";

    private static bool TryReadValues(
        ModelObject target,
        IReadOnlyList<TemplateAttributeDefinition> definitions,
        out Hashtable values,
        out string error)
    {
        var strings = new ArrayList();
        var doubles = new ArrayList();
        var integers = new ArrayList();

        foreach (var definition in definitions)
        {
            switch (definition.ValueType)
            {
                case TemplateValueType.Float: doubles.Add(definition.Name); break;
                case TemplateValueType.Integer: integers.Add(definition.Name); break;
                default: strings.Add(definition.Name); break;
            }
        }

        values = new Hashtable();
        error = string.Empty;

        try
        {
            target.GetAllReportProperties(strings, doubles, integers, ref values);
            values ??= new Hashtable();
            return true;
        }
        catch (Exception ex)
        {
            values = new Hashtable();
            error = ex.Message;
            return false;
        }
    }

    private static string ValueTypeName(TemplateValueType valueType) => valueType switch
    {
        TemplateValueType.Float => nameof(Double),
        TemplateValueType.Integer => nameof(Int32),
        _ => nameof(String),
    };

    private static string? FormatValue(object? raw) => raw switch
    {
        null => null,
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => ValueFormatting.Format(raw),
    };
}
