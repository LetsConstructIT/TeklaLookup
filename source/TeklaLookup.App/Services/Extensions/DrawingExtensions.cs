using System.Collections.Generic;
using Tekla.Structures.Drawing;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>Curated members for the base <see cref="Drawing"/> class.</summary>
public sealed class DrawingExtensions : TeklaTypeExtension<Drawing>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(Drawing target)
    {
        yield return Entry("Sheet",           () => target.GetSheet(),                                                     "ContainerView");
        yield return Entry("PlotFileName",    () => target.GetPlotFileName(false),                                         "string");
        yield return Entry("PlotFileNameExt", () => target.GetPlotFileNameExt(IncludeRevisionMarkEnum.ByFormatString),     "string");
    }
}

public sealed class AssemblyDrawingExtensions : TeklaTypeExtension<AssemblyDrawing>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(AssemblyDrawing target)
    {
        yield return Entry("AssemblyIdentifier", () => target.AssemblyIdentifier, "Identifier");
        yield return Entry("SheetNumber",        () => target.SheetNumber,        "int");
    }
}

public sealed class SinglePartDrawingExtensions : TeklaTypeExtension<SinglePartDrawing>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(SinglePartDrawing target)
    {
        yield return Entry("PartIdentifier", () => target.PartIdentifier, "Identifier");
        yield return Entry("SheetNumber",    () => target.SheetNumber,    "int");
    }
}

public sealed class CastUnitDrawingExtensions : TeklaTypeExtension<CastUnitDrawing>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(CastUnitDrawing target)
    {
        yield return Entry("CastUnitIdentifier", () => target.CastUnitIdentifier, "Identifier");
        yield return Entry("SheetNumber",        () => target.SheetNumber,        "int");
        yield return Entry("CastUnitById",       () => target.CastUnitById,       "bool");
    }
}
