using System.Collections;
using System.Collections.Generic;
using System.Collections.Generic;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Solid;
using TSColor = Tekla.Structures.Model.UI.Color;
using TSDrawer = Tekla.Structures.Model.UI.GraphicsDrawer;
using TSPolyLine = Tekla.Structures.Geometry3d.PolyLine;
using TSGraphicLine = Tekla.Structures.Model.UI.GraphicPolyLine;

namespace TeklaLookup.App.Services;

/// <summary>
/// Renders temporary overlays in the active Tekla model view for geometry-bearing property values
/// (points, vectors, lines, segments, point lists, solids). Stores the polyline IDs the
/// <see cref="TSDrawer"/> hands back so they can be wiped together via <see cref="Clear"/>.
/// </summary>
public sealed class GeometryVisualizer
{
    private static readonly TSColor HighlightColor = new(1.0, 0.2, 0.2); // default red
    private static readonly TSColor AxisRed   = new(1.0, 0.0, 0.0);
    private static readonly TSColor AxisGreen = new(0.0, 0.8, 0.0);
    private static readonly TSColor AxisBlue  = new(0.0, 0.3, 1.0);

    private readonly TSDrawer _drawer = new();
    private readonly List<int> _ids = new();

    /// <summary>True when the value is something this class knows how to draw.</summary>
    public static bool IsSupported(object? value) => value switch
    {
        null => false,
        Point or Vector => true,
        Line or LineSegment => true,
        TSPolyLine => true,
        RebarGeometry => true,
        Solid => true,
        Contour => true,
        Polygon => true,
        Plane => true,
        TransformationPlane => true,
        CoordinateSystem => true,
        IList list when ContainsPoints(list) => true,
        IEnumerable seq => ContainsDrawable(seq),
        _ => false,
    };

    public bool TryHighlight(object? value)
    {
        switch (value)
        {
            case null: return false;
            case Vector v: return DrawCross(new Point(v.X, v.Y, v.Z));
            case Point p:  return DrawCross(p);
            case LineSegment seg: return DrawSegment(seg.Point1, seg.Point2);
            case Line line: return DrawSegment(line.Origin, AdvanceAlong(line.Origin, line.Direction, 1000));
            case TSPolyLine polyline: return DrawPointSequence(polyline.Points);
            case RebarGeometry rebar: return rebar.Shape != null && DrawPointSequence(rebar.Shape.Points);
            case Solid solid: return DrawSolidEdges(solid);
            case Contour contour: return DrawClosedPointList(contour.ContourPoints);
            case Polygon polygon: return DrawClosedPointList(polygon.Points);
            case Plane plane: return DrawPlane(plane);
            case TransformationPlane tp: return DrawTransformationPlane(tp);
            case CoordinateSystem cs: return DrawCoordinateSystem(cs);
            case IList list when ContainsPoints(list): return DrawPointSequence(list);
            case IEnumerable seq: return DrawEach(seq);
            default: return false;
        }
    }

    public void Clear()
    {
        if (_ids.Count == 0) return;
        _drawer.RemoveTemporaryGraphicsObjects(_ids);
        _ids.Clear();
    }

    private bool DrawCross(Point center)
    {
        const double size = 100; // mm
        return DrawPolyline(new[]
            {
                new Point(center.X - size, center.Y, center.Z),
                new Point(center.X + size, center.Y, center.Z),
            })
            & DrawPolyline(new[]
            {
                new Point(center.X, center.Y - size, center.Z),
                new Point(center.X, center.Y + size, center.Z),
            })
            & DrawPolyline(new[]
            {
                new Point(center.X, center.Y, center.Z - size),
                new Point(center.X, center.Y, center.Z + size),
            });
    }

    private bool DrawSegment(Point a, Point b) => DrawPolyline(new[] { a, b });

    private bool DrawSolidEdges(Solid solid)
    {
        var anyDrawn = false;
        var faces = solid.GetFaceEnumerator();
        while (faces.MoveNext())
        {
            if (faces.Current is not Face face) continue;
            var loops = face.GetLoopEnumerator();
            while (loops.MoveNext())
            {
                if (loops.Current is not Loop loop) continue;
                var points = new List<Point>();
                var vertices = loop.GetVertexEnumerator();
                while (vertices.MoveNext())
                {
                    if (vertices.Current is Point p) points.Add(p);
                }
                if (points.Count >= 2)
                {
                    // close the loop
                    points.Add(points[0]);
                    anyDrawn |= DrawPolyline(points);
                }
            }
        }
        return anyDrawn;
    }

    private bool DrawTransformationPlane(TransformationPlane plane)
    {
        // Draw the work-plane as an X/Y/Z tripod by transforming unit-axis tips from local to global.
        // Tekla convention: red=X, green=Y, blue=Z.
        const double axisLength = 1000;
        var m = plane.TransformationMatrixToGlobal;
        var origin = m.Transform(new Point(0, 0, 0));
        var xTip   = m.Transform(new Point(axisLength, 0, 0));
        var yTip   = m.Transform(new Point(0, axisLength, 0));
        var zTip   = m.Transform(new Point(0, 0, axisLength));

        var any = DrawPolyline(new[] { origin, xTip }, AxisRed);
        any |= DrawPolyline(new[] { origin, yTip }, AxisGreen);
        any |= DrawPolyline(new[] { origin, zTip }, AxisBlue);
        return any;
    }

    private bool DrawCoordinateSystem(CoordinateSystem cs)
    {
        // Same X/Y/Z tripod as TransformationPlane, but the basis is given by AxisX/AxisY directly.
        // Z is derived as X × Y so handedness matches Tekla's convention.
        const double axisLength = 1000;
        var origin = cs.Origin;
        var ux = cs.AxisX.GetNormal();
        var uy = cs.AxisY.GetNormal();
        var uz = ux.Cross(uy).GetNormal();

        var xTip = Advance(origin, ux, axisLength);
        var yTip = Advance(origin, uy, axisLength);
        var zTip = Advance(origin, uz, axisLength);

        var any = DrawPolyline(new[] { origin, xTip }, AxisRed);
        any |= DrawPolyline(new[] { origin, yTip }, AxisGreen);
        any |= DrawPolyline(new[] { origin, zTip }, AxisBlue);
        return any;
    }

    private static Point Advance(Point origin, Vector unit, double distance)
        => new(origin.X + unit.X * distance,
               origin.Y + unit.Y * distance,
               origin.Z + unit.Z * distance);

    private bool DrawPlane(Plane plane)
    {
        // Render the plane as a 1000 mm parallelogram centered at the origin, spanned by AxisX
        // and AxisY. A short normal stub helps disambiguate orientation.
        const double half = 500;
        const double normalLength = 300;

        var origin = plane.Origin;
        var ux = plane.AxisX.GetNormal();
        var uy = plane.AxisY.GetNormal();

        var p1 = Offset(origin, ux, -half, uy, -half);
        var p2 = Offset(origin, ux,  half, uy, -half);
        var p3 = Offset(origin, ux,  half, uy,  half);
        var p4 = Offset(origin, ux, -half, uy,  half);

        var quadDrawn = DrawPolyline(new[] { p1, p2, p3, p4, p1 });

        var normal = ux.Cross(uy).GetNormal();
        var tip = new Point(origin.X + normal.X * normalLength,
                            origin.Y + normal.Y * normalLength,
                            origin.Z + normal.Z * normalLength);
        var normalDrawn = DrawPolyline(new[] { origin, tip });

        return quadDrawn || normalDrawn;
    }

    private static Point Offset(Point origin, Vector ux, double dx, Vector uy, double dy)
        => new(origin.X + ux.X * dx + uy.X * dy,
               origin.Y + ux.Y * dx + uy.Y * dy,
               origin.Z + ux.Z * dx + uy.Z * dy);

    private bool DrawClosedPointList(IList? source)
    {
        if (source is null) return false;
        var points = new List<Point>();
        foreach (var item in source)
            if (item is Point p) points.Add(p);
        if (points.Count < 2) return false;
        points.Add(points[0]); // close the loop
        return DrawPolyline(points);
    }

    private bool DrawPointSequence(IList list)
    {
        var points = new List<Point>(list.Count);
        foreach (var item in list)
            if (item is Point p) points.Add(p);
        return points.Count >= 2 && DrawPolyline(points);
    }

    private bool DrawPolyline(IEnumerable<Point> points, TSColor? color = null)
    {
        var list = new List<Point>(points);
        if (list.Count < 2) return false;

        var polyline = new TSPolyLine(list);
        var graphic = new TSGraphicLine(polyline, color ?? HighlightColor, 2, TSGraphicLine.LineType.Solid);
        var id = _drawer.DrawPolyLine(graphic);
        // Tekla's DrawPolyLine returns an int whose meaning isn't documented — empirically it
        // can be 0 or negative on a successful draw. Stash whatever it returns so Clear() can
        // attempt removal, and treat the draw itself as fire-and-forget.
        _ids.Add(id);
        return true;
    }

    private static Point AdvanceAlong(Point origin, Vector direction, double distance)
    {
        var unit = direction.GetNormal();
        return new Point(origin.X + unit.X * distance,
                         origin.Y + unit.Y * distance,
                         origin.Z + unit.Z * distance);
    }

    private bool DrawEach(IEnumerable seq)
    {
        var any = false;
        foreach (var item in seq)
        {
            if (item is null) continue;
            if (item is Point) continue; // handled by the point-sequence path
            any |= TryHighlight(item);
        }
        return any;
    }

    private static bool ContainsDrawable(IEnumerable seq)
    {
        foreach (var item in seq)
        {
            if (item is null) continue;
            if (item is Point) return false; // point lists are handled by the polyline path
            if (IsSupported(item)) return true;
            return false; // bail on first non-drawable mismatch
        }
        return false;
    }

    private static bool ContainsPoints(IList list)
    {
        foreach (var item in list)
        {
            if (item is Point) return true;
            // bail after first non-null mismatch to avoid eating long enumerations
            if (item is not null) return false;
        }
        return false;
    }
}
