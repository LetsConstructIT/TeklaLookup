namespace TeklaLookup.App.Models;

/// <summary>
/// Lightweight, UI-bindable view of a Tekla model object. Captures only the fields we need to
/// display in the list — the full object stays available via <see cref="Guid"/> / identifier for
/// later decomposition.
/// </summary>
public sealed class TeklaObjectSnapshot
{
    public string TypeName { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public int IdentifierId { get; set; }

    /// <summary>
    /// Single-line description tailored to the concrete Tekla type — e.g. for a Beam it lists
    /// profile/material/class, for an Assembly the main-part summary, for a BoltGroup the bolt
    /// size and count. Used by the search filter as well.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Live Tekla object (either a <c>Tekla.Structures.Model.ModelObject</c> or a
    /// <c>Tekla.Structures.Drawing.Drawing</c>), kept for the decomposition pane. Not bound to any
    /// column. Consumers check the runtime type to choose the right "show in Tekla" path.
    /// </summary>
    public object? Source { get; set; }
}
