using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services;
using TSAssembly = Tekla.Structures.Model.Assembly;

namespace TeklaLookup.App.Mappers;

internal static class ModelObjectMapper
{
    public static TeklaObjectSnapshot ToSnapshot(this ModelObject modelObject, TeklaObjectsCollector collector)
    {
        return new TeklaObjectSnapshot
        {
            TypeName = BuildTypeName(modelObject),
            Guid = collector.GetGuid(modelObject),
            IdentifierId = modelObject.Identifier.ID,
            Summary = BuildSummary(modelObject),
            Source = modelObject,
        };
    }

    /// <summary>
    /// Decorates the CLR type name with a Tekla-specific subtype where one exists
    /// (e.g. <c>Beam · COLUMN</c>, <c>Assembly · CAST_UNIT</c>) so the user can tell apart objects
    /// that share the same .NET class.
    /// </summary>
    private static string BuildTypeName(ModelObject modelObject)
    {
        var clrName = modelObject.GetType().Name;
        var subtype = modelObject switch
        {
            Beam beam              => beam.Type.ToString(),
            PolyBeam polyBeam      => polyBeam.Type.ToString(),
            ContourPlate plate     => plate.Type.ToString(),
            TSAssembly assembly    => TryGetAssemblyType(assembly),
            _ => null,
        };
        return string.IsNullOrEmpty(subtype) ? clrName : $"{clrName} · {subtype}";
    }

    private static string? TryGetAssemblyType(TSAssembly assembly)
    {
        try { return assembly.GetAssemblyType().ToString(); }
        catch { return null; }
    }

    private static string? BuildSummary(ModelObject modelObject)
    {
        return modelObject switch
        {
            Part part            => SummarizePart(part),
            TSAssembly assembly  => SummarizeAssembly(assembly),
            BoltGroup bolts      => SummarizeBoltGroup(bolts),
            BaseWeld weld        => $"size={weld.SizeAbove}",
            Reinforcement rebar  => SummarizeReinforcement(rebar),
            BaseComponent comp   => SummarizeComponent(comp),
            _ => null,
        };
    }

    private static string SummarizePart(Part part)
    {
        var bits = new List<string>();
        if (!string.IsNullOrEmpty(part.Name)) bits.Add(part.Name);
        var profile = part.Profile?.ProfileString;
        if (!string.IsNullOrEmpty(profile)) bits.Add(profile!);
        var material = part.Material?.MaterialString;
        if (!string.IsNullOrEmpty(material)) bits.Add(material!);
        if (!string.IsNullOrEmpty(part.Class)) bits.Add($"class {part.Class}");
        return string.Join(" / ", bits);
    }

    private static string SummarizeAssembly(TSAssembly assembly)
    {
        var main = assembly.GetMainPart() as Part;
        var secondaryCount = 0;
        try
        {
            secondaryCount = assembly.GetSecondaries().Cast<object>().Count();
        }
        catch
        {
            // ignore — we still want to show whatever we have
        }

        var mainBit = main is null
            ? "no main part"
            : $"main: {main.GetType().Name} {main.Profile.ProfileString}".Trim();
        return secondaryCount > 0
            ? $"{mainBit} + {secondaryCount} secondary"
            : mainBit;
    }

    private static string SummarizeBoltGroup(BoltGroup bolts)
    {
        var count = bolts.BoltPositions?.Count ?? 0;
        var main = bolts.PartToBeBolted is Part p ? p.Name : null;
        return main is null ? $"{count} bolts" : $"{count} bolts on {main}";
    }

    private static string SummarizeReinforcement(Reinforcement rebar)
    {
        var bits = new List<string>();
        if (!string.IsNullOrEmpty(rebar.Name)) bits.Add(rebar.Name);
        if (!string.IsNullOrEmpty(rebar.Grade)) bits.Add(rebar.Grade);
        bits.Add($"class {rebar.Class}");
        return string.Join(" / ", bits);
    }

    private static string SummarizeComponent(BaseComponent component)
    {
        var name = component.Name ?? string.Empty;
        return component.Number > 0 ? $"{component.Number}/{name}".TrimEnd('/') : name;
    }
}
