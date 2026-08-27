using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeklaLookup.App.Services;

namespace TeklaLookup.App.Models;

/// <summary>
/// A single row in the decomposition pane: one resolved property/UDA/template attribute value.
/// </summary>
/// <remarks>
/// <see cref="Category"/> raises change notifications because it is the grouping key of the
/// properties view: pinning moves a row between groups by reassigning it, with no re-decomposition.
/// </remarks>
public sealed class PropertyEntry : INotifyPropertyChanged
{
    private string _category = string.Empty;
    private string _homeCategory = string.Empty;
    private bool _isPinned;

    public string Category
    {
        get => _category;
        set
        {
            if (!SetProperty(ref _category, value)) return;
            // Remember the first non-pinned category assigned, so every existing call site that
            // only sets Category still knows where to put the row back when it is unpinned.
            if (_homeCategory.Length == 0 && value != PropertyCategories.Pinned)
                _homeCategory = value;
        }
    }

    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ValueType { get; set; }

    /// <summary>Optional human-readable label, e.g. the display name Tekla declares for a UDA.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tooltip for the Name column: the declared label where there is one, otherwise the name
    /// itself so a column too narrow to show it in full still reveals it on hover.
    /// </summary>
    public string NameTooltip => string.IsNullOrEmpty(Description) ? Name : $"{Name} — {Description}";

    /// <summary>Live property value, used for drill-down navigation. Not bound to any column.</summary>
    public object? RawValue { get; set; }

    /// <summary>
    /// Group this row belongs to when it is not pinned. Kept so unpinning can put it back where
    /// it came from.
    /// </summary>
    public string HomeCategory
    {
        get => _homeCategory;
        set => _homeCategory = value;
    }

    /// <summary>
    /// Key the pin is stored under — the Tekla content type where there is one, so a pin made on a
    /// beam applies to every other part too. Empty when the row cannot be pinned.
    /// </summary>
    public string PinScope { get; set; } = string.Empty;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (!SetProperty(ref _isPinned, value)) return;
            Category = value ? PropertyCategories.Pinned : HomeCategory;
        }
    }

    /// <summary>
    /// Whether the star is offered at all. List elements are positional and error rows are
    /// transient, so neither is worth remembering across sessions.
    /// </summary>
    public bool CanPin =>
        PinScope.Length > 0 &&
        Name.Length > 0 &&
        !Name.StartsWith("<", StringComparison.Ordinal) &&
        HomeCategory != PropertyCategories.Items;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
