using System.Globalization;
using Tekla.Structures;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using TSDistance = Tekla.Structures.Datatype.Distance;
using TSAngle = Tekla.Structures.Datatype.Angle;

namespace TeklaLookup.App.Services;

/// <summary>
/// One-line summaries for Tekla types whose default <see cref="object.ToString"/> is unhelpful
/// (typically the CLR type name). Returns null when the type isn't recognised, signalling the
/// caller to fall back to its own formatting.
/// </summary>
internal static class TeklaValueFormatter
{
    private const string DoubleFormat = "0.###";

    public static string? TryFormat(object? value)
    {
        return value switch
        {
            null => null,

            // Tekla.Structures
            Identifier id => $"ID={id.ID}, GUID={id.GUID}",

            // Tekla.Structures.Geometry3d — Vector inherits Point, so it must be matched first
            Vector v => $"({F(v.X)}, {F(v.Y)}, {F(v.Z)})",
            Point p => $"({F(p.X)}, {F(p.Y)}, {F(p.Z)})",
            CoordinateSystem cs =>
                $"Origin=({F(cs.Origin.X)}, {F(cs.Origin.Y)}, {F(cs.Origin.Z)}); " +
                $"AxisX=({F(cs.AxisX.X)}, {F(cs.AxisX.Y)}, {F(cs.AxisX.Z)}); " +
                $"AxisY=({F(cs.AxisY.X)}, {F(cs.AxisY.Y)}, {F(cs.AxisY.Z)})",
            Line line =>
                $"Origin=({F(line.Origin.X)}, {F(line.Origin.Y)}, {F(line.Origin.Z)}); " +
                $"Direction=({F(line.Direction.X)}, {F(line.Direction.Y)}, {F(line.Direction.Z)})",
            LineSegment seg =>
                $"Start=({F(seg.Point1.X)}, {F(seg.Point1.Y)}, {F(seg.Point1.Z)}); " +
                $"End=({F(seg.Point2.X)}, {F(seg.Point2.Y)}, {F(seg.Point2.Z)})",
            GeometricPlane plane =>
                $"Origin=({F(plane.Origin.X)}, {F(plane.Origin.Y)}, {F(plane.Origin.Z)}); " +
                $"Normal=({F(plane.Normal.X)}, {F(plane.Normal.Y)}, {F(plane.Normal.Z)})",

            // Tekla.Structures.Datatype — Distance/Angle wrap a double + unit
            TSDistance d => d.ToString(),
            TSAngle a => a.ToString(),

            // Tekla.Structures.Model value types
            Profile prof => string.IsNullOrEmpty(prof.ProfileString) ? "<empty>" : prof.ProfileString,
            Material mat => string.IsNullOrEmpty(mat.MaterialString) ? "<empty>" : mat.MaterialString,

            // Plain doubles: round to avoid noisy mantissas
            double dbl => dbl.ToString(DoubleFormat, CultureInfo.InvariantCulture),

            _ => null,
        };
    }

    private static string F(double value) => value.ToString(DoubleFormat, CultureInfo.InvariantCulture);
}
