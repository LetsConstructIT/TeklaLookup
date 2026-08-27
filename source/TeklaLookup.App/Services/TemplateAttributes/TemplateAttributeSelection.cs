using System;
using System.Collections.Generic;

namespace TeklaLookup.App.Services.TemplateAttributes;

/// <summary>
/// Decides whether one declared attribute is read for a given scope. Split out of
/// <see cref="TemplateAttributeReader"/> so the rule can be exercised without a Tekla connection —
/// it is pure, and it is the part most likely to be got wrong.
/// </summary>
internal static class TemplateAttributeSelection
{
    /// <summary>
    /// <c>USERDEFINED.*</c> duplicates what <c>GetAllUserProperties</c> already reports under
    /// "User Properties". Prefixed forms such as <c>ASSEMBLY.MAINPART.USERDEFINED.X</c> belong to
    /// a different object and are kept.
    /// </summary>
    public const string UserDefinedGroup = "USERDEFINED";

    public static bool IsSelected(
        TemplateAttributeDefinition definition,
        TemplateAttributeScope scope,
        ICollection<string>? pinned)
    {
        // A pin outranks the scope — including the switched-off tier. That is what makes it a
        // favourite rather than a bookmark that only works in some views.
        if (pinned is not null && pinned.Contains(definition.Name)) return true;
        if (scope == TemplateAttributeScope.PinnedOnly) return false;

        if (string.Equals(definition.Group, UserDefinedGroup, StringComparison.OrdinalIgnoreCase))
            return false;

        if (definition.IsDirect) return true;

        return scope == TemplateAttributeScope.Full || definition.IsAssociated;
    }
}
