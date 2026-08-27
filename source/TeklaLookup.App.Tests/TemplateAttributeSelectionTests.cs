using System;
using System.Collections.Generic;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// What each scope reads, and the rule that a pin outranks all of them.
/// </summary>
public sealed class TemplateAttributeSelectionTests
{
    private static TemplateAttributeDefinition Direct(string name = "WEIGHT")
        => new(name, TemplateValueType.Float, string.Empty, null);

    private static TemplateAttributeDefinition Associated(string name = "NUT.WEIGHT", string group = "NUT")
        => new(name, TemplateValueType.Float, group, null, isRelatedObject: false);

    private static TemplateAttributeDefinition Related(string name = "MAIN_PART.WEIGHT", string group = "MAIN_PART")
        => new(name, TemplateValueType.Float, group, null, isRelatedObject: true);

    private static HashSet<string> Pins(params string[] names)
        => new(names, StringComparer.Ordinal);

    [Theory]
    [InlineData(TemplateAttributeScope.PinnedOnly, false)]
    [InlineData(TemplateAttributeScope.Associated, true)]
    [InlineData(TemplateAttributeScope.Full, true)]
    public void Direct_attributes_are_read_in_every_scope_except_pinned_only(
        TemplateAttributeScope scope, bool expected)
        => Assert.Equal(expected, TemplateAttributeSelection.IsSelected(Direct(), scope, null));

    [Theory]
    [InlineData(TemplateAttributeScope.PinnedOnly, false)]
    [InlineData(TemplateAttributeScope.Associated, true)]
    [InlineData(TemplateAttributeScope.Full, true)]
    public void Constituent_attributes_arrive_with_the_associated_scope(
        TemplateAttributeScope scope, bool expected)
        => Assert.Equal(expected, TemplateAttributeSelection.IsSelected(Associated(), scope, null));

    [Theory]
    [InlineData(TemplateAttributeScope.PinnedOnly, false)]
    [InlineData(TemplateAttributeScope.Associated, false)]
    [InlineData(TemplateAttributeScope.Full, true)]
    public void Related_object_attributes_are_full_scope_only(
        TemplateAttributeScope scope, bool expected)
        => Assert.Equal(expected, TemplateAttributeSelection.IsSelected(Related(), scope, null));

    [Theory]
    [InlineData(TemplateAttributeScope.PinnedOnly)]
    [InlineData(TemplateAttributeScope.Associated)]
    [InlineData(TemplateAttributeScope.Full)]
    public void A_pin_outranks_every_scope(TemplateAttributeScope scope)
    {
        // MAIN_PART is the most expensive tier there is, which makes it the sharpest case: if a
        // favourite survives here it survives anywhere.
        var pinned = Related();

        Assert.True(TemplateAttributeSelection.IsSelected(pinned, scope, Pins(pinned.Name)));
    }

    [Fact]
    public void Pinned_only_reads_nothing_but_the_pins()
    {
        var pins = Pins("MAIN_PART.WEIGHT");
        const TemplateAttributeScope scope = TemplateAttributeScope.PinnedOnly;

        Assert.True(TemplateAttributeSelection.IsSelected(Related(), scope, pins));
        Assert.False(TemplateAttributeSelection.IsSelected(Direct(), scope, pins));
        Assert.False(TemplateAttributeSelection.IsSelected(Associated(), scope, pins));
    }

    [Fact]
    public void Pin_matching_is_case_sensitive()
    {
        // A part exposes both a CLR property "Profile" and a template attribute "PROFILE". Under a
        // case-insensitive comparison, starring one would silently star the other.
        var attribute = Direct("PROFILE");

        Assert.True(TemplateAttributeSelection.IsSelected(
            attribute, TemplateAttributeScope.PinnedOnly, Pins("PROFILE")));
        Assert.False(TemplateAttributeSelection.IsSelected(
            attribute, TemplateAttributeScope.PinnedOnly, Pins("Profile")));
    }

    [Theory]
    [InlineData(TemplateAttributeScope.Associated)]
    [InlineData(TemplateAttributeScope.Full)]
    public void Direct_user_defined_attributes_are_left_to_the_UDA_reader(TemplateAttributeScope scope)
    {
        // GetAllUserProperties already reports these under "User Properties", with values.
        var uda = new TemplateAttributeDefinition(
            "USERDEFINED.COMMENT", TemplateValueType.Character, "USERDEFINED", null);

        Assert.False(TemplateAttributeSelection.IsSelected(uda, scope, null));
    }

    [Fact]
    public void A_pinned_user_defined_attribute_is_still_read()
    {
        var uda = new TemplateAttributeDefinition(
            "USERDEFINED.COMMENT", TemplateValueType.Character, "USERDEFINED", null);

        Assert.True(TemplateAttributeSelection.IsSelected(
            uda, TemplateAttributeScope.Associated, Pins("USERDEFINED.COMMENT")));
    }

    [Fact]
    public void Another_objects_user_defined_attributes_are_not_skipped()
    {
        // ASSEMBLY.MAINPART.USERDEFINED.X belongs to a different object, so the local UDA reader
        // never reports it — only the leading segment decides.
        var foreign = new TemplateAttributeDefinition(
            "ASSEMBLY.MAINPART.USERDEFINED.X", TemplateValueType.Character, "ASSEMBLY", null,
            isRelatedObject: true);

        Assert.True(TemplateAttributeSelection.IsSelected(foreign, TemplateAttributeScope.Full, null));
    }
}
