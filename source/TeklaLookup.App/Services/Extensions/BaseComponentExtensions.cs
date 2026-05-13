using System.Collections.Generic;
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
