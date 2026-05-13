using System;
using TeklaLookup.App.Services;

namespace TeklaLookup.App.Models;

/// <summary>
/// A single row in the decomposition pane: one resolved property/UDA/report value.
/// </summary>
public sealed class PropertyEntry
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ValueType { get; set; }

    /// <summary>Live property value, used for drill-down navigation. Not bound to any column.</summary>
    public object? RawValue { get; set; }

    /// <summary>True when <see cref="RawValue"/> can be drawn in the Tekla viewport.</summary>
    public bool IsHighlightable => Services.GeometryVisualizer.IsSupported(RawValue);

    /// <summary>True when <see cref="RawValue"/> is worth opening a new pane for.</summary>
    public bool IsDrillable
    {
        get
        {
            if (RawValue is null) return false;
            var type = RawValue.GetType();
            if (type.IsPrimitive) return false;
            if (type.IsEnum) return false;
            if (RawValue is string) return false;
            if (RawValue is decimal or DateTime or DateTimeOffset or TimeSpan or Guid) return false;
            return true;
        }
    }
}
