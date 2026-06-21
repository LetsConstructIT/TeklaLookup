using System.Collections.Generic;
using System.Reflection;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ReinforcementExtensions : TeklaTypeExtension<Reinforcement>
{
    // Reinforcement.GetAssembly was added in Tekla 2022; resolved at runtime so the 2021-floor
    // build still surfaces it on a newer Tekla (omitted on 2021). See OptionalTeklaApi.
    private static readonly MethodInfo? GetAssemblyMethod = OptionalTeklaApi.Method(typeof(Reinforcement), "GetAssembly");

    protected override IEnumerable<PropertyEntry> ResolveCore(Reinforcement target)
    {
        yield return Entry("Solid",          () => target.GetSolid(),                  "Solid");
        if (GetAssemblyMethod is not null)
            yield return Entry("Assembly",   () => GetAssemblyMethod.Invoke(target, null), "Assembly");
        yield return Entry("FatherPour",     () => target.GetFatherPour(),             "PourObject");
        yield return Entry("FatherPourUnit", () => target.GetFatherPourUnit(),         "PourUnit");
        yield return Entry("NumberOfRebars", () => target.GetNumberOfRebars(),         "int");
        yield return Entry("GeometryValid",  () => target.IsGeometryValid(),           "bool");
        yield return Entry("RebarGeometries", () => target.GetRebarGeometries(true),   "ArrayList<RebarGeometry>");
    }
}

public sealed class BaseRebarGroupExtensions : TeklaTypeExtension<BaseRebarGroup>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(BaseRebarGroup target)
    {
        yield return Entry("StartPoint", () => target.StartPoint, "Point");
        yield return Entry("EndPoint",   () => target.EndPoint,   "Point");
    }
}

public sealed class RebarGroupExtensions : TeklaTypeExtension<RebarGroup>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(RebarGroup target)
    {
        yield return Entry("Polygons", () => target.Polygons, "ArrayList<Polygon>");
    }
}

public sealed class SingleRebarExtensions : TeklaTypeExtension<SingleRebar>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(SingleRebar target)
    {
        yield return Entry("Polygon",  () => target.Polygon,        "Polygon");
        yield return Entry("RebarSet", () => target.GetRebarSet(),  "RebarSet");
    }
}

public sealed class RebarSetExtensions : TeklaTypeExtension<RebarSet>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(RebarSet target)
    {
        yield return Entry("Reinforcements",   () => target.GetReinforcements(),   "ModelObjectEnumerator");
        yield return Entry("RebarModifiers",   () => target.GetRebarModifiers(),   "ModelObjectEnumerator");
        yield return Entry("RebarSetAdditions", () => target.GetRebarSetAdditions(), "ModelObjectEnumerator");
        yield return Entry("RebarLegSurfaces", () => target.GetRebarLegSurfaces(), "ModelObjectEnumerator");
        yield return Entry("LegFaces",         () => target.LegFaces,              "List<RebarLegFace>");
        yield return Entry("Guidelines",       () => target.Guidelines,            "List<RebarGuideline>");
    }
}
