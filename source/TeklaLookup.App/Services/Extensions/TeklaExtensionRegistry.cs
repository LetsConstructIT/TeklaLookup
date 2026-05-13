using System.Collections.Generic;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>
/// Holds every <see cref="ITeklaTypeExtension"/> and yields the union of entries that apply to a
/// given target. Most-derived types should appear in their own extension class; entries from a
/// base-type extension (e.g. <see cref="ModelObjectExtensions"/>) are returned for derived types
/// as well.
/// </summary>
public sealed class TeklaExtensionRegistry
{
    private readonly IReadOnlyList<ITeklaTypeExtension> _extensions;

    public TeklaExtensionRegistry()
        : this(new ITeklaTypeExtension[]
        {
            new ModelExtensions(),
            new WorkPlaneHandlerExtensions(),
            new CatalogHandlerExtensions(),
            new ModelObjectExtensions(),
            new PartExtensions(),
            new ContourPlateExtensions(),
            new PolyBeamExtensions(),
            new AssemblyExtensions(),
            new BoltGroupExtensions(),
            new BaseWeldExtensions(),
            new PolygonWeldExtensions(),
            new ReinforcementExtensions(),
            new BaseRebarGroupExtensions(),
            new RebarGroupExtensions(),
            new SingleRebarExtensions(),
            new RebarSetExtensions(),
            new ComponentExtensions(),
            new ConnectionExtensions(),
            new DetailExtensions(),
            new SeamExtensions(),
            new ReferenceModelExtensions(),
            new ReferenceModelObjectExtensions(),
            new ProjectInfoExtensions(),
            new ProfileItemExtensions(),
            new ShapeItemExtensions(),
            new ComponentItemExtensions(),
            new DrawingObjectExtensions(),
            new ViewBaseExtensions(),
            new MarkExtensions(),
            new SymbolExtensions(),
            new TextExtensions(),
            new DimensionBaseExtensions(),
            new StraightDimensionSetExtensions(),
            new DrawingExtensions(),
            new AssemblyDrawingExtensions(),
            new SinglePartDrawingExtensions(),
            new CastUnitDrawingExtensions(),
        })
    {
    }

    public TeklaExtensionRegistry(IReadOnlyList<ITeklaTypeExtension> extensions)
    {
        _extensions = extensions;
    }

    public IEnumerable<PropertyEntry> Resolve(object target)
    {
        foreach (var extension in _extensions)
        {
            if (!extension.Applies(target)) continue;
            foreach (var entry in extension.Resolve(target))
                yield return entry;
        }
    }
}
