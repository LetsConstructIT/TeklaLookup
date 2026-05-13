using System;

namespace TeklaLookup.App.Models;

/// <summary>
/// A single row in the event-monitor log. One Tekla event can produce multiple entries (e.g.
/// <c>ModelObjectChanged</c> with N <c>ChangeData</c>s yields N rows).
/// </summary>
public sealed class EventLogEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Source { get; set; } = string.Empty;   // "Model" / "Drawing"
    public string Kind   { get; set; } = string.Empty;   // "Add" / "Modify" / "Delete" / "Selection" / "Saved" / "Loaded" / "DrawingChanged"
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId   { get; set; } = string.Empty;
    public string Detail     { get; set; } = string.Empty;

    /// <summary>Persistent GUID of the model object, when this entry refers to one. Empty otherwise.</summary>
    public string Guid       { get; set; } = string.Empty;
}
