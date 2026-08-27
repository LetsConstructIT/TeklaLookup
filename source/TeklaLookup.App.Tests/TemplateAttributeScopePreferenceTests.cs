using TeklaLookup.App.Services;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// What the remembered scope reads back as. Only <see cref="TemplateAttributeScopePreference.Load"/>
/// is exercised — Save writes to %LOCALAPPDATA% and is not something a test should touch.
/// </summary>
public sealed class TemplateAttributeScopePreferenceTests
{
    private static TemplateAttributeScope LoadWith(string? stored)
    {
        SettingsStore.Current.TemplateAttributeScope = stored;
        return TemplateAttributeScopePreference.Load();
    }

    [Theory]
    [InlineData("PinnedOnly", TemplateAttributeScope.PinnedOnly)]
    [InlineData("Associated", TemplateAttributeScope.Associated)]
    public void Cheap_tiers_round_trip(string stored, TemplateAttributeScope expected)
        => Assert.Equal(expected, LoadWith(stored));

    [Fact]
    public void Full_is_clamped_so_the_next_launch_does_not_look_like_a_hang()
        => Assert.Equal(TemplateAttributeScope.Associated, LoadWith("Full"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Everything")]  // written by a newer build, or hand-edited
    [InlineData("0")]           // the numeric form TryParse would otherwise accept
    public void Unset_or_unrecognised_falls_back_to_the_default_tier(string? stored)
        => Assert.Equal(TemplateAttributeScope.Associated, LoadWith(stored));
}
