using Tekla.Structures.Drawing;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Mappers;

internal static class DrawingMapper
{
    public static TeklaObjectSnapshot ToSnapshot(this DrawingObject drawingObject)
    {
        return new TeklaObjectSnapshot
        {
            TypeName     = drawingObject.GetType().Name,
            Guid         = string.Empty,
            IdentifierId = 0,
            Summary      = BuildDrawingObjectSummary(drawingObject),
            Source       = drawingObject,
        };
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
        return new TeklaObjectSnapshot
        {
            TypeName     = drawing.GetType().Name,
            Guid         = string.Empty,
            IdentifierId = 0, // Drawing.Identifier isn't part of the public API surface
            Summary      = BuildSummary(drawing),
            Source       = drawing,
        };
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
