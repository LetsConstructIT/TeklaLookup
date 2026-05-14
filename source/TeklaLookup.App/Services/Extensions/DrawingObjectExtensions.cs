using System.Collections.Generic;
using System.Reflection;
using Tekla.Structures.Drawing;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>
/// Base curated view for any <see cref="DrawingObject"/> — surfaces the parent drawing, the view
/// it lives in, related model objects, and an axis-aligned bounding box where available.
/// </summary>
public sealed class DrawingObjectExtensions : TeklaTypeExtension<DrawingObject>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(DrawingObject target)
    {
        yield return Entry("Drawing",        () => target.GetDrawing(),         "Drawing");
        yield return Entry("View",           () => target.GetView(),            "ViewBase");
        yield return Entry("RelatedObjects", () => target.GetRelatedObjects(),  "DrawingObjectEnumerator");
    }
}

public sealed class ViewBaseExtensions : TeklaTypeExtension<ViewBase>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ViewBase target)
    {
        yield return Entry("OriginalDrawing",       () => target.GetOriginalDrawing(),       "Drawing");
        yield return Entry("AllObjects",            () => target.GetAllObjects(),            "DrawingObjectEnumerator");
        yield return Entry("Objects",               () => target.GetObjects(),               "DrawingObjectEnumerator");
        // GetModelObjects(Identifier) requires a specific identifier — not surfaceable as a flat
        // curated entry. Users can drill into individual drawing objects to reach their model
        // counterparts via DrawingObjectExtensions.RelatedObjects.
        yield return Entry("AxisAlignedBoundingBox", () => target.GetAxisAlignedBoundingBox(), "AABB");
    }
}

public sealed class MarkBaseExtensions : TeklaTypeExtension<MarkBase>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(MarkBase target)
    {
        yield return Entry("AxisAlignedBoundingBox",   () => target.GetAxisAlignedBoundingBox(),   "RectangleBoundingBox");
        yield return Entry("ObjectAlignedBoundingBox", () => target.GetObjectAlignedBoundingBox(), "RectangleBoundingBox");
        yield return Entry("Objects",                  () => target.GetObjects(),                  "DrawingObjectEnumerator");
    }
}

public sealed class WeldMarkExtensions : TeklaTypeExtension<WeldMark>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(WeldMark target)
    {
        yield return Entry("AxisAlignedBoundingBox",   () => target.GetAxisAlignedBoundingBox(),   "RectangleBoundingBox");
        yield return Entry("ObjectAlignedBoundingBox", () => target.GetObjectAlignedBoundingBox(), "RectangleBoundingBox");
        yield return Entry("Objects",                  () => target.GetObjects(),                  "DrawingObjectEnumerator");
    }
}

public sealed class SymbolExtensions : TeklaTypeExtension<Symbol>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Symbol target)
    {
        yield return Entry("AxisAlignedBoundingBox",   () => target.GetAxisAlignedBoundingBox(),   "AABB");
        yield return Entry("ObjectAlignedBoundingBox", () => target.GetObjectAlignedBoundingBox(), "OBB");
        yield return Entry("Objects",                  () => target.GetObjects(),                  "DrawingObjectEnumerator");
    }
}

public sealed class TextExtensions : TeklaTypeExtension<Text>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Text target)
    {
        yield return Entry("AxisAlignedBoundingBox",   () => target.GetAxisAlignedBoundingBox(),   "AABB");
        yield return Entry("ObjectAlignedBoundingBox", () => target.GetObjectAlignedBoundingBox(), "OBB");
        yield return Entry("Objects",                  () => target.GetObjects(),                  "DrawingObjectEnumerator");
    }
}

public sealed class DimensionBaseExtensions : TeklaTypeExtension<DimensionBase>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(DimensionBase target)
    {
        yield return Entry("DimensionSet", () => target.GetDimensionSet(), "DimensionSetBase");
    }
}

/// <summary>
/// Surfaces the <c>DimensionPoints</c> list and <c>UpDirection</c> vector on a
/// <see cref="StraightDimensionSet"/>. Tekla declares both getters as <c>internal</c>, so the
/// default public-reflection pass skips them — we reach in by name with non-public binding flags.
/// </summary>
public sealed class StraightDimensionSetExtensions : TeklaTypeExtension<StraightDimensionSet>
{
    private const BindingFlags NonPublicInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly PropertyInfo? DimensionPointsProperty =
        typeof(StraightDimensionSet).GetProperty("DimensionPoints", NonPublicInstance);

    private static readonly PropertyInfo? UpDirectionProperty =
        typeof(StraightDimensionSet).GetProperty("UpDirection", NonPublicInstance);

    protected override IEnumerable<PropertyEntry> ResolveCore(StraightDimensionSet target)
    {
        yield return Entry("DimensionPoints",
            () => DimensionPointsProperty?.GetValue(target),
            "PointList");
        yield return Entry("UpDirection",
            () => UpDirectionProperty?.GetValue(target),
            "Vector");
    }
}
