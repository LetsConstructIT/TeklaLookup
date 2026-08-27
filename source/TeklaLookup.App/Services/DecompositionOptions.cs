using TeklaLookup.App.Services.TemplateAttributes;

namespace TeklaLookup.App.Services;

/// <summary>
/// Knobs for a single <see cref="ObjectDecomposer.Decompose(object, DecompositionOptions)"/> pass.
/// </summary>
public sealed class DecompositionOptions
{
    /// <summary>
    /// Fresh defaults. Deliberately a new instance each time rather than a shared singleton — the
    /// object is mutable, and a shared one would let any caller change every other caller's pass.
    /// </summary>
    public static DecompositionOptions Default => new();

    /// <summary>
    /// How much of the environment's template attribute list to read. Defaults to
    /// <see cref="TemplateAttributeScope.Associated"/>: the object's own attributes plus its
    /// constituents (a bolt's nut and washer, a part's profile and material), which is the useful
    /// answer at a cost close to reading the object alone.
    /// </summary>
    public TemplateAttributeScope TemplateAttributes { get; set; } = TemplateAttributeScope.Associated;

    /// <summary>
    /// Ceiling on template attributes read in one pass. Guards the pathological case under
    /// <see cref="TemplateAttributeScope.Full"/>: a connection declares over 13,000 once related
    /// objects are in scope, fetched in a single blocking interop call that Cancel cannot interrupt.
    /// </summary>
    public int MaxTemplateAttributes { get; set; } = 2000;
}
