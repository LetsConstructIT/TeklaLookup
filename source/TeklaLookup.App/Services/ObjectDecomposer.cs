using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services.Extensions;
using TSD = Tekla.Structures.Drawing;

namespace TeklaLookup.App.Services;

/// <summary>
/// Produces a flat, displayable list of properties for any CLR object. For <see cref="ModelObject"/>
/// targets it also pulls UDAs and a handful of common report properties; drawing-side objects
/// (drawings, views, marks, dimensions…) contribute their UDAs through the drawing API instead.
/// </summary>
public sealed class ObjectDecomposer
{
    private readonly TeklaExtensionRegistry _extensions;

    public ObjectDecomposer() : this(new TeklaExtensionRegistry()) { }

    public ObjectDecomposer(TeklaExtensionRegistry extensions)
    {
        _extensions = extensions;
    }

    private static readonly string[] CommonReportProperties =
    {
        "NAME", "PROFILE", "MATERIAL", "CLASS", "PHASE",
        "LENGTH", "AREA", "WEIGHT", "VOLUME",
        "PART_POS", "ASSEMBLY_POS",
    };

    public IReadOnlyList<PropertyEntry> Decompose(object target)
    {
        var entries = new List<PropertyEntry>();

        switch (target)
        {
            case IDictionary dictionary:
                AddDictionaryItems(dictionary, entries);
                break;
            case IEnumerable enumerable when target is not string:
                AddEnumerableItems(enumerable, entries);
                break;
            default:
                AddReflectedProperties(target, entries);
                AddExtensions(target, entries);
                if (target is ModelObject modelObject)
                {
                    AddUserProperties(modelObject, entries);
                    AddReportProperties(modelObject, entries);
                }
                else if (target is TSD.DatabaseObject drawingObject)
                {
                    // Separate hierarchy from ModelObject, with its own UDA API and no report
                    // properties at all — hence the dedicated branch rather than a shared one.
                    AddDrawingUserProperties(drawingObject, entries);
                }
                break;
        }

        return entries;
    }

    private void AddExtensions(object target, List<PropertyEntry> entries)
    {
        foreach (var entry in _extensions.Resolve(target).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            entries.Add(entry);
    }

    private static void AddDictionaryItems(IDictionary dictionary, List<PropertyEntry> entries)
    {
        foreach (DictionaryEntry kvp in dictionary)
        {
            entries.Add(new PropertyEntry
            {
                Category = "Items",
                Name = kvp.Key?.ToString() ?? "<null>",
                Value = FormatValue(kvp.Value),
                ValueType = kvp.Value?.GetType().Name,
                RawValue = kvp.Value,
            });
        }
    }

    private static void AddEnumerableItems(IEnumerable enumerable, List<PropertyEntry> entries)
    {
        var index = 0;
        foreach (var item in enumerable)
        {
            entries.Add(new PropertyEntry
            {
                Category = "Items",
                Name = $"[{index}]",
                Value = FormatValue(item),
                ValueType = item?.GetType().Name,
                RawValue = item,
            });
            index++;
        }
    }

    private static void AddReflectedProperties(object target, List<PropertyEntry> entries)
    {
        var type = target.GetType();
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => new ReflectedMember(p.Name, p.PropertyType, () => p.GetValue(target)));

        // Many Tekla geometry types (Point/Vector/CoordinateSystem) expose X/Y/Z as public *fields*
        // rather than properties, so a properties-only pass shows them as empty. Include public
        // instance fields too, but skip compiler-generated backing fields and any field whose name
        // is already covered by a property of the same name.
        var propertyNames = new HashSet<string>(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name));
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.Name.StartsWith("<", StringComparison.Ordinal))
            .Where(f => !propertyNames.Contains(f.Name))
            .Select(f => new ReflectedMember(f.Name, f.FieldType, () => f.GetValue(target)));

        foreach (var member in properties.Concat(fields).OrderBy(m => m.Name))
        {
            object? raw = null;
            string? value;
            string typeName = member.DeclaredType.Name;
            try
            {
                raw = member.Get();
                if (raw != null)
                    typeName = raw.GetType().Name;
                raw = ValueFormatting.MaterializeIfEnumerator(raw);
                value = FormatValue(raw);
            }
            catch (TargetInvocationException tie)
            {
                value = $"<error: {tie.InnerException?.Message ?? tie.Message}>";
            }
            catch (Exception ex)
            {
                value = $"<error: {ex.Message}>";
            }

            entries.Add(new PropertyEntry
            {
                Category = "Properties",
                Name = member.Name,
                Value = value,
                ValueType = typeName,
                RawValue = raw,
            });
        }
    }

    private readonly struct ReflectedMember
    {
        public ReflectedMember(string name, Type declaredType, Func<object?> get)
        {
            Name = name;
            DeclaredType = declaredType;
            Get = get;
        }

        public string Name { get; }
        public Type DeclaredType { get; }
        public Func<object?> Get { get; }
    }

    private static void AddUserProperties(ModelObject modelObject, List<PropertyEntry> entries)
    {
        var hashtable = new Hashtable();
        try
        {
            modelObject.GetAllUserProperties(ref hashtable);
        }
        catch (Exception ex)
        {
            entries.Add(new PropertyEntry
            {
                Category = "User Properties",
                Name = "<error>",
                Value = ex.Message,
            });
            return;
        }

        foreach (DictionaryEntry kvp in hashtable.Cast<DictionaryEntry>()
            .OrderBy(e => e.Key?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new PropertyEntry
            {
                Category = "User Properties",
                Name = kvp.Key?.ToString() ?? string.Empty,
                Value = FormatValue(kvp.Value),
                ValueType = kvp.Value?.GetType().Name,
                RawValue = kvp.Value,
            });
        }
    }

    private static void AddReportProperties(ModelObject modelObject, List<PropertyEntry> entries)
    {
        foreach (var name in CommonReportProperties.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (TryReadReportString(modelObject, name, out var s))
            {
                entries.Add(new PropertyEntry { Category = "Report", Name = name, Value = s, ValueType = nameof(String), RawValue = s });
                continue;
            }
            if (TryReadReportDouble(modelObject, name, out var d))
            {
                entries.Add(new PropertyEntry { Category = "Report", Name = name, Value = d.ToString("R"), ValueType = nameof(Double), RawValue = d });
                continue;
            }
            if (TryReadReportInt(modelObject, name, out var i))
            {
                entries.Add(new PropertyEntry { Category = "Report", Name = name, Value = i.ToString(), ValueType = nameof(Int32), RawValue = i });
            }
        }
    }

    private static bool TryReadReportString(ModelObject mo, string name, out string value)
    {
        value = string.Empty;
        try { return mo.GetReportProperty(name, ref value); }
        catch { return false; }
    }

    private static bool TryReadReportDouble(ModelObject mo, string name, out double value)
    {
        value = 0;
        try { return mo.GetReportProperty(name, ref value); }
        catch { return false; }
    }

    private static bool TryReadReportInt(ModelObject mo, string name, out int value)
    {
        value = 0;
        try { return mo.GetReportProperty(name, ref value); }
        catch { return false; }
    }

    /// <summary>
    /// Drawing-side UDAs. <see cref="TSD.DatabaseObject"/> — the base of <c>Drawing</c>, views,
    /// marks, dimensions and drawing parts — has no <c>GetAllUserProperties</c>; it exposes three
    /// typed batch getters instead, which we merge back into one alphabetical block.
    /// </summary>
    private static void AddDrawingUserProperties(TSD.DatabaseObject target, List<PropertyEntry> entries)
    {
        var collected = new List<PropertyEntry>();
        var failures = new List<string>();

        CollectDrawingUserProperties<string>(collected, failures, "string",
            () => { target.GetStringUserProperties(out var values); return values; });
        CollectDrawingUserProperties<int>(collected, failures, "integer",
            () => { target.GetIntegerUserProperties(out var values); return values; });
        CollectDrawingUserProperties<double>(collected, failures, "double",
            () => { target.GetDoubleUserProperties(out var values); return values; });

        foreach (var entry in collected.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            entries.Add(entry);

        foreach (var failure in failures)
        {
            entries.Add(new PropertyEntry
            {
                Category = "User Properties",
                Name = "<error>",
                Value = failure,
            });
        }
    }

    private static void CollectDrawingUserProperties<T>(
        List<PropertyEntry> collected,
        List<string> failures,
        string label,
        Func<Dictionary<string, T>?> read)
    {
        Dictionary<string, T>? values;
        try
        {
            values = read();
        }
        catch (Exception ex)
        {
            // One type failing (e.g. the object isn't in a drawing yet) shouldn't hide the others.
            failures.Add($"{label} UDAs: {ex.Message}");
            return;
        }

        if (values is null) return;

        foreach (var kvp in values)
        {
            collected.Add(new PropertyEntry
            {
                Category = "User Properties",
                Name = kvp.Key,
                Value = FormatValue(kvp.Value),
                ValueType = typeof(T).Name,
                RawValue = kvp.Value,
            });
        }
    }

    private static string? FormatValue(object? raw) => ValueFormatting.Format(raw);
}
