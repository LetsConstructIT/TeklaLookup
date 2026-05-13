using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class BoltGroupExtensions : TeklaTypeExtension<BoltGroup>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(BoltGroup target)
    {
        yield return Entry("BoltPositions",    () => target.BoltPositions,    "ArrayList<Point>");
        yield return Entry("PartToBeBolted",   () => target.PartToBeBolted,   "Part");
        yield return Entry("PartToBoltTo",     () => target.PartToBoltTo,     "Part");
        yield return Entry("OtherPartsToBolt", () => target.OtherPartsToBolt, "ArrayList<Part>");
    }
}
