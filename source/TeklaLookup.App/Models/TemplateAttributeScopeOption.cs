using System.Collections.Generic;
using TeklaLookup.App.Services.TemplateAttributes;

namespace TeklaLookup.App.Models;

/// <summary>
/// One entry in the template-attribute scope picker: the enum value plus how it is labelled and
/// explained in the toolbar.
/// </summary>
public sealed class TemplateAttributeScopeOption
{
    private TemplateAttributeScopeOption(TemplateAttributeScope scope, string label, string description)
    {
        Scope = scope;
        Label = label;
        Description = description;
    }

    public TemplateAttributeScope Scope { get; }
    public string Label { get; }
    public string Description { get; }

    /// <summary>Ordered cheapest-first, which is also least-to-most complete.</summary>
    public static IReadOnlyList<TemplateAttributeScopeOption> All { get; } = new[]
    {
        new TemplateAttributeScopeOption(
            TemplateAttributeScope.PinnedOnly,
            "Pinned only",
            "Template attributes off, except the ones you starred — those stay at the top. "
            + "Fastest."),

        new TemplateAttributeScopeOption(
            TemplateAttributeScope.Associated,
            "Associated",
            "The object's own attributes plus its constituents — a bolt's nut, washer and hole; "
            + "a part's profile, material and phase."),

        new TemplateAttributeScopeOption(
            TemplateAttributeScope.Full,
            "Full (with related)",
            "Also traverses to other model objects — assembly, cast unit, drawings, connection "
            + "secondaries. Complete, and much slower."),
    };
}
