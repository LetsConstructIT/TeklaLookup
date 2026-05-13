using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ContourPlateExtensions : TeklaTypeExtension<ContourPlate>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ContourPlate target)
    {
        yield return Entry("ContourPolycurve", () => target.GetContourPolycurve(), "Polycurve");
    }
}

public sealed class PolyBeamExtensions : TeklaTypeExtension<PolyBeam>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(PolyBeam target)
    {
        yield return Entry("CenterLinePolycurve",       () => target.GetCenterLinePolycurve(),       "Polycurve");
        yield return Entry("PolybeamCoordinateSystems", () => target.GetPolybeamCoordinateSystems(), "List<CoordinateSystem>");
    }
}
