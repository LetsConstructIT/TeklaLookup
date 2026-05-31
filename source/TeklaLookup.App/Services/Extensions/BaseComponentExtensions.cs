using System.Collections;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ComponentExtensions : TeklaTypeExtension<Component>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Component target)
    {
        yield return Entry("Assembly",       () => target.GetAssembly(),       "Assembly");
        yield return Entry("Components",     () => target.GetComponents(),     "ModelObjectEnumerator");
        yield return Entry("Booleans",       () => target.GetBooleans(),       "ModelObjectEnumerator");
        yield return Entry("ComponentInput", () => target.GetComponentInput(), "ComponentInput");
    }
}

/// <summary>
/// A <see cref="ComponentInput"/> enumerates <see cref="InputItem"/>s, but each item's actual
/// payload only comes out of <see cref="InputItem.GetData"/> — reflection alone leaves the row
/// empty. This surfaces the input type and the resolved data (a single object, several objects, a
/// polygon, or one/two points).
/// </summary>
public sealed class InputItemExtensions : TeklaTypeExtension<InputItem>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(InputItem target)
    {
        yield return Entry("InputType", () => target.GetInputType(), "InputTypeEnum");
        yield return Entry("Data",      () => ResolveData(target),   "ArrayList");
    }

    /// <summary>
    /// <see cref="InputItem.GetData"/> returns an <see cref="ArrayList"/> whose element type depends
    /// on the input type: <see cref="Identifier"/>s for object inputs (1 / 2 / N objects) or
    /// <see cref="Point"/>s for point and polygon inputs. We resolve the identifiers to live model
    /// objects so the row is drillable and highlightable; points pass through untouched.
    /// </summary>
    private static object? ResolveData(InputItem item)
    {
        var data = item.GetData();
        if (data is not ArrayList list || list.Count == 0)
            return data;

        if (list[0] is not Identifier)
            return list; // points / polygon — already meaningful as-is

        var model = new Model();
        var resolved = new ArrayList(list.Count);
        foreach (var element in list)
            resolved.Add(element is Identifier id ? model.SelectModelObject(id) : element);
        return resolved;
    }
}

public sealed class ConnectionExtensions : TeklaTypeExtension<Connection>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Connection target)
    {
        yield return Entry("PrimaryObject",    () => target.GetPrimaryObject(),    "ModelObject");
        yield return Entry("SecondaryObjects", () => target.GetSecondaryObjects(), "ArrayList<ModelObject>");
    }
}

public sealed class DetailExtensions : TeklaTypeExtension<Detail>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Detail target)
    {
        yield return Entry("PrimaryObject",  () => target.GetPrimaryObject(),  "ModelObject");
        yield return Entry("ReferencePoint", () => target.GetReferencePoint(), "Point");
    }
}

public sealed class SeamExtensions : TeklaTypeExtension<Seam>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Seam target)
    {
        yield return Entry("PrimaryObject",    () => target.GetPrimaryObject(),    "ModelObject");
        yield return Entry("SecondaryObjects", () => target.GetSecondaryObjects(), "ArrayList<ModelObject>");
        yield return Entry("InputPolygon",     () => target.GetInputPolygon(),     "Polygon");
        yield return Entry("StartAndEndPositions", () =>
        {
            var start = new Point();
            var end = new Point();
            target.GetStartAndEndPositions(ref start, ref end);
            return new[] { start, end };
        }, "Point[]");
    }
}
