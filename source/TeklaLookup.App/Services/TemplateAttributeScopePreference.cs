using System;
using TeklaLookup.App.Services.TemplateAttributes;

namespace TeklaLookup.App.Services;

/// <summary>
/// The template-attribute scope the user last picked, remembered in
/// <see cref="UserSettings.TemplateAttributeScope"/>.
/// </summary>
/// <remarks>
/// Stored as the enum name rather than its number so a reordered enum cannot silently repoint an
/// existing settings file at a different tier.
/// </remarks>
public static class TemplateAttributeScopePreference
{
    /// <summary>
    /// The scope a freshly opened window starts in. Unset or unrecognised reads as
    /// <see cref="TemplateAttributeScope.Associated"/>, matching
    /// <see cref="DecompositionOptions.TemplateAttributes"/>'s own default.
    /// </summary>
    /// <remarks>
    /// <see cref="TemplateAttributeScope.Full"/> is deliberately clamped back to Associated on
    /// load: it is slow enough that restoring it silently would make the next session's first
    /// object look like the app hanging. The two cheap tiers are the ones worth remembering —
    /// picking PinnedOnly is a speed decision, and having to re-pick it every launch defeats it.
    /// </remarks>
    public static TemplateAttributeScope Load()
    {
        var stored = SettingsStore.Current.TemplateAttributeScope;
        if (string.IsNullOrEmpty(stored)) return TemplateAttributeScope.Associated;

        // Round-tripping the name is what rejects everything that is not one: TryParse also accepts
        // the numeric form ("0" would come back as PinnedOnly) and any undefined number, and a
        // newer build's tier name parses to nothing this build knows.
        if (!Enum.TryParse<TemplateAttributeScope>(stored, out var scope)
            || !string.Equals(scope.ToString(), stored, StringComparison.Ordinal))
            return TemplateAttributeScope.Associated;

        return scope == TemplateAttributeScope.Full ? TemplateAttributeScope.Associated : scope;
    }

    /// <summary>Records the pick. Written eagerly, like the other toolbar preferences.</summary>
    public static void Save(TemplateAttributeScope scope)
    {
        SettingsStore.Current.TemplateAttributeScope = scope.ToString();
        SettingsStore.Save();
    }
}
