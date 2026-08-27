namespace TeklaLookup.App.Services.TemplateAttributes;

/// <summary>
/// How much of the environment's template attribute list to read for one object.
/// </summary>
/// <remarks>
/// The tiers exist because cost is wildly uneven. A group that names another full model object
/// (<c>MAIN_PART</c>, <c>CAST_UNIT</c>, <c>ASSEMBLY</c>) re-exposes that object's entire attribute
/// set — hundreds of names, each needing Tekla to resolve and evaluate a different object. A group
/// that names a constituent (<c>NUT</c>, <c>WASHER</c>, <c>HOLE</c>, <c>PROFILE</c>) contributes a
/// couple of dozen. Reading the second kind is comparable to reading the object itself; reading
/// the first is what makes the pane hang.
/// <para>
/// Pinned attributes are read in every tier, including <see cref="PinnedOnly"/>. There are only
/// ever a handful, so they never move the cost, and a favourite that disappeared because of a
/// view setting would not be a favourite.
/// </para>
/// </remarks>
public enum TemplateAttributeScope
{
    /// <summary>
    /// Only the attributes the user pinned. A pin is a standing instruction to always show
    /// something, so it outranks the scope: turning template attributes off hides the list, never
    /// your favourites. With nothing pinned this reads nothing at all and costs nothing.
    /// </summary>
    PinnedOnly,

    /// <summary>
    /// The object's own attributes plus its constituent groups — a bolt's nut, washer and hole; a
    /// part's profile, material and phase. The default: it is the mode that answers "what does
    /// this thing have", and stays in the same cost bracket as reading the object alone.
    /// </summary>
    Associated,

    /// <summary>
    /// Everything, including groups that traverse to other model objects (assembly, cast unit,
    /// drawings, connection secondaries). Complete, and slow enough to be an explicit choice.
    /// </summary>
    Full,
}
