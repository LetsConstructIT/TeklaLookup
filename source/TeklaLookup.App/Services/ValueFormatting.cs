using System.Collections;
using System.Collections.Generic;

namespace TeklaLookup.App.Services;

/// <summary>
/// Shared one-line-summary formatter used by both the reflection decomposer and by extension
/// providers. Delegates Tekla-specific types to <see cref="TeklaValueFormatter"/>.
/// </summary>
internal static class ValueFormatting
{
    public static string? Format(object? raw)
    {
        if (raw is null) return null;
        if (raw is string s) return s;

        var tekla = TeklaValueFormatter.TryFormat(raw);
        if (tekla != null) return tekla;

        if (raw is IEnumerable enumerable)
            return FormatEnumerable(enumerable);
        if (raw is IEnumerator enumerator)
            return FormatEnumerator(enumerator);

        return raw.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var preview = new List<string?>();
        var total = 0;
        foreach (var item in enumerable)
        {
            total++;
            if (preview.Count < 8)
                preview.Add(TeklaValueFormatter.TryFormat(item) ?? item?.ToString());
        }
        return BuildEnumerablePreview(preview, total);
    }

    private static string FormatEnumerator(IEnumerator enumerator)
    {
        var preview = new List<string?>();
        var total = 0;
        while (enumerator.MoveNext())
        {
            total++;
            if (preview.Count < 8)
                preview.Add(TeklaValueFormatter.TryFormat(enumerator.Current) ?? enumerator.Current?.ToString());
        }
        return BuildEnumerablePreview(preview, total);
    }

    private static string BuildEnumerablePreview(List<string?> preview, int total)
    {
        var tail = total > preview.Count ? ", …" : "";
        return $"({total} items): [{string.Join(", ", preview)}{tail}]";
    }

    /// <summary>
    /// Drains single-pass enumerables/enumerators into a materialized list. Leaves
    /// <see cref="string"/>, <see cref="IDictionary"/> and already-materialized
    /// <see cref="ICollection"/> instances (List, ArrayList, etc.) untouched.
    /// </summary>
    /// <remarks>
    /// Tekla's <c>ModelObjectEnumerator</c> implements <see cref="IEnumerator"/> but NOT
    /// <see cref="IEnumerable"/>, so an IEnumerable-only check would skip it.
    /// </remarks>
    public static object? MaterializeIfEnumerator(object? raw)
    {
        if (raw is null) return null;
        if (raw is string) return raw;
        if (raw is IDictionary) return raw;
        if (raw is ICollection) return raw;

        if (raw is IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
                list.Add(item);
            return list;
        }

        if (raw is IEnumerator enumerator)
        {
            var list = new List<object?>();
            while (enumerator.MoveNext())
                list.Add(enumerator.Current);
            return list;
        }

        return raw;
    }
}
