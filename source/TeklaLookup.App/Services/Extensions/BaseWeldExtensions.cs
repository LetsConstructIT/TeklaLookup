using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class BaseWeldExtensions : TeklaTypeExtension<BaseWeld>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(BaseWeld target)
    {
        yield return Entry("MainObject",       () => target.MainObject,         "ModelObject");
        yield return Entry("SecondaryObject",  () => target.SecondaryObject,    "ModelObject");
        yield return Entry("Solid",            () => target.GetSolid(),         "Solid");
        yield return Entry("WeldGeometries",   () => target.GetWeldGeometries(), "IEnumerable<WeldGeometry>");
    }
}

public sealed class PolygonWeldExtensions : TeklaTypeExtension<PolygonWeld>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(PolygonWeld target)
    {
        yield return Entry("Polygon", () => target.Polygon, "Polygon");
    }
}
