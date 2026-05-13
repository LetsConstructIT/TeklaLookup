using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class PartExtensions : TeklaTypeExtension<Part>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Part target)
    {
        yield return Entry("Solid",            () => target.GetSolid(),                   "Solid");
        yield return Entry("CoordinateSystem", () => target.GetCoordinateSystem(),        "CoordinateSystem");
        yield return Entry("CenterLine",       () => target.GetCenterLine(false),         "ArrayList<Point>");
        yield return Entry("ReferenceLine",    () => target.GetReferenceLine(false),      "ArrayList<Point>");
        yield return Entry("Assembly",         () => target.GetAssembly(),                "Assembly");
        yield return Entry("Bolts",            () => target.GetBolts(),                   "ModelObjectEnumerator");
        yield return Entry("Welds",            () => target.GetWelds(),                   "ModelObjectEnumerator");
        yield return Entry("Reinforcements",   () => target.GetReinforcements(),          "ModelObjectEnumerator");
    }
}
