namespace TeklaLookup.App.Services;

/// <summary>
/// Group headings for the decomposition pane. These strings are the grouping key of the
/// properties <c>CollectionView</c> and the switch key of
/// <see cref="PropertyEntryToIconConverter"/>, so they live in one place.
/// </summary>
public static class PropertyCategories
{
    /// <summary>Attributes the user starred; always rendered first, whatever they came from.</summary>
    public const string Pinned = "Pinned";

    public const string Properties = "Properties";
    public const string Extensions = "Extensions";
    public const string UserProperties = "User Properties";
    public const string Items = "Items";

    /// <summary>
    /// Tekla's report/template attributes. Related-object groups extend this with a separator and
    /// the group name, e.g. <c>Template Attributes · NUT</c>.
    /// </summary>
    public const string TemplateAttributes = "Template Attributes";
}
