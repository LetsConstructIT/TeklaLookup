using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeklaLookup.App.Models;

/// <summary>
/// Lightweight, UI-bindable view of a Tekla model object. Captures only the fields we need to
/// display in the list — the full object stays available via <see cref="Guid"/> / identifier for
/// later decomposition.
/// </summary>
public sealed class TeklaObjectSnapshot : INotifyPropertyChanged
{
    private string _typeName = string.Empty;
    private string? _summary;

    /// <summary>
    /// Raised for the two derived display columns. Both are computed from <see cref="Source"/> at
    /// load time and re-computed on refresh, so the grid has to hear about the change.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public string TypeName
    {
        get => _typeName;
        set => SetProperty(ref _typeName, value);
    }

    public string Guid { get; set; } = string.Empty;
    public int IdentifierId { get; set; }

    /// <summary>
    /// Single-line description tailored to the concrete Tekla type — e.g. for a Beam it lists
    /// profile/material/class, for an Assembly the main-part summary, for a BoltGroup the bolt
    /// size and count. Used by the search filter as well.
    /// </summary>
    public string? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    /// <summary>
    /// Live Tekla object (either a <c>Tekla.Structures.Model.ModelObject</c> or a
    /// <c>Tekla.Structures.Drawing.Drawing</c>), kept for the decomposition pane. Not bound to any
    /// column. Consumers check the runtime type to choose the right "show in Tekla" path.
    /// </summary>
    public object? Source { get; set; }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
