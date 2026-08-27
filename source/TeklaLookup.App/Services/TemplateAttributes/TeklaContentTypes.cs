using System;
using System.Collections.Generic;
using Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;
using TeklaTask = Tekla.Structures.Model.Task;

namespace TeklaLookup.App.Services.TemplateAttributes;

/// <summary>
/// Maps a CLR object onto the content type name Tekla uses in <c>contentattributes*.lst</c>
/// (<c>PART</c>, <c>BOLT</c>, …), and onto the scope key under which the user's pins are stored.
/// </summary>
public static class TeklaContentTypes
{
    /// <summary>
    /// Most-derived first — the first assignable entry wins, so <c>SingleRebar</c> resolves to
    /// <c>SINGLE_REBAR</c> rather than falling through to its <c>Reinforcement</c> base.
    /// </summary>
    private static readonly KeyValuePair<Type, string>[] Map =
    {
        new(typeof(SingleRebar), "SINGLE_REBAR"),
        new(typeof(RebarStrand), "STRAND"),
        new(typeof(RebarMesh), "MESH"),
        new(typeof(RebarSet), "REBAR"),
        new(typeof(RebarGroup), "REBAR"),
        new(typeof(BaseRebarGroup), "REBAR"),
        new(typeof(Reinforcement), "REBAR"),

        new(typeof(BoltGroup), "BOLT"),
        new(typeof(BaseWeld), "WELD"),
        new(typeof(BaseComponent), "CONNECTION"),

        new(typeof(PourObject), "POUR_OBJECT"),
        new(typeof(PourBreak), "POUR_BREAK"),

        new(typeof(Assembly), "ASSEMBLY"),
        new(typeof(Part), "PART"),

        new(typeof(ReferenceModelObject), "REFERENCE_OBJECT"),
        new(typeof(ReferenceModel), "REFERENCE_MODEL"),
        new(typeof(ProjectInfo), "PROJECT"),
        new(typeof(TeklaTask), "TASK"),

        new(typeof(TSD.Drawing), "DRAWING"),
    };

    /// <summary>
    /// The Tekla content type for <paramref name="target"/>, or null when the object has no
    /// template attributes (a geometry struct, a catalog item, a plain CLR value).
    /// </summary>
    public static string? For(object? target)
    {
        if (target is null) return null;

        var type = target.GetType();
        foreach (var pair in Map)
        {
            if (pair.Key.IsAssignableFrom(type))
                return pair.Value;
        }

        return null;
    }

    /// <summary>
    /// Key that pins are stored under. Prefers the Tekla content type so a pin made on a beam also
    /// applies to a column or a plate — they are all <c>PART</c> and share one attribute set — and
    /// falls back to the CLR type name so anything else can still be pinned in its own right.
    /// </summary>
    public static string ScopeFor(object? target)
    {
        if (target is null) return string.Empty;
        return For(target) ?? target.GetType().Name;
    }
}
