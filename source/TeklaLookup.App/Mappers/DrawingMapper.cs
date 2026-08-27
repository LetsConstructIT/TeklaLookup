using Tekla.Structures.Drawing;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Mappers;

internal static class DrawingMapper
{
    public static TeklaObjectSnapshot ToSnapshot(this DrawingObject drawingObject)
    {
        var snapshot = new TeklaObjectSnapshot
        {
            Guid         = string.Empty,
            IdentifierId = 0,
            Source       = drawingObject,
        };
        snapshot.RefreshFrom(drawingObject);
        return snapshot;
    }

    /// <summary>Re-derives the display columns from the live drawing object.</summary>
    public static void RefreshFrom(this TeklaObjectSnapshot snapshot, DrawingObject drawingObject)
    {
        snapshot.TypeName = drawingObject.GetType().Name;
        snapshot.Summary  = BuildDrawingObjectSummary(drawingObject);
    }

    private static string? BuildDrawingObjectSummary(DrawingObject drawingObject)
    {
        return drawingObject switch
        {
            Tekla.Structures.Drawing.Text text => Truncate(text.TextString, 80),
            Tekla.Structures.Drawing.Mark mark => mark.Attributes?.Content?.ToString(),
            _ => null,
        };
    }

    private static string? Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text!.Length <= max ? text : text.Substring(0, max - 1) + "…";
    }

    public static TeklaObjectSnapshot ToSnapshot(this Drawing drawing)
    {
        var snapshot = new TeklaObjectSnapshot
        {
            Guid         = string.Empty,
            IdentifierId = 0, // Drawing.Identifier isn't part of the public API surface
            Source       = drawing,
        };
        snapshot.RefreshFrom(drawing);
        return snapshot;
    }

    /// <summary>
    /// Re-derives the display columns from the live drawing. Call after a <c>Select()</c> so a
    /// rename or retitle made in Tekla reaches the grid row, not just the details pane.
    /// </summary>
    public static void RefreshFrom(this TeklaObjectSnapshot snapshot, Drawing drawing)
    {
        snapshot.TypeName = drawing.GetType().Name;
        snapshot.Summary  = BuildSummary(drawing);
    }

    private static string BuildSummary(Drawing drawing)
    {
        // Concrete drawing subclasses (Assembly/SinglePart/CastUnit) have a Mark + a numeric sheet
        // identifier; GADrawing has Name/Title1. Combine the noteworthy bits in one line.
        var bits = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(drawing.Name))   bits.Add(drawing.Name);
        if (!string.IsNullOrEmpty(drawing.Mark))   bits.Add($"mark {drawing.Mark}");
        if (!string.IsNullOrEmpty(drawing.Title1)) bits.Add(drawing.Title1);
        return string.Join(" / ", bits);
    }
}
