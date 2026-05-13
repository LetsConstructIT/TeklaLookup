using System.Collections.Generic;
using Tekla.Structures.Catalogs;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>
/// Surfaces each catalog enumerator (profiles, materials, bolts, rebar, components, drawings,
/// shapes, meshes) as a drillable virtual property on the catalog handler.
/// </summary>
public sealed class CatalogHandlerExtensions : TeklaTypeExtension<CatalogHandler>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(CatalogHandler target)
    {
        yield return Entry("Profiles (library)", () => target.GetLibraryProfileItems(), "ProfileItemEnumerator");
        yield return Entry("Materials",          () => target.GetMaterialItems(),       "MaterialItemEnumerator");
        yield return Entry("Bolts",              () => target.GetBoltItems(),           "BoltItemEnumerator");
        yield return Entry("Rebars",             () => target.GetRebarItems(),          "RebarItemEnumerator");
        yield return Entry("Components",         () => target.GetComponentItems(),      "ComponentItemEnumerator");
        yield return Entry("Drawings",           () => target.GetDrawingItems(),        "DrawingItemEnumerator");
        yield return Entry("Shapes",             () => target.GetShapeItems(),          "ShapeItemEnumerator");
        yield return Entry("Meshes",             () => target.GetMeshItems(),           "MeshItemEnumerator");
    }
}
