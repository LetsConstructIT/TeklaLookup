using System;
using System.Collections.Generic;
using System.Linq;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// Which groups count as constituents of the object (cheap, read in the Associated tier) and which
/// traverse to another model object (expensive, Full only).
/// </summary>
/// <remarks>
/// This is the split that decides whether the properties pane responds instantly or hangs: a
/// traversal group re-exposes another content type's entire attribute list, and every name in it
/// makes Tekla resolve and evaluate a different object.
/// </remarks>
public sealed class TemplateAttributeTierTests
{
    private static readonly string[] NoDirectories = Array.Empty<string>();

    private static TemplateAttributeCatalog Build(LstFixture fixture, params string[] bindings)
    {
        // One shared datatype keeps the fixtures about grouping rather than about parsing.
        var definitions = bindings
            .Select(b => b.Substring(b.IndexOf('=') + 1).Trim())
            .Select(n => n.Split('.').Last())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => $"{n} CHARACTER LEFT TRUE 20");

        var file = fixture.Write("contentattributes.lst",
            LstFixture.File_(definitions: definitions, bindings: bindings));
        return TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
    }

    [Theory]
    [InlineData("NUT")]
    [InlineData("WASHER")]
    [InlineData("HOLE")]
    [InlineData("PROFILE")]
    [InlineData("MATERIAL")]
    [InlineData("PHASE")]
    [InlineData("HISTORY")]
    public void Constituent_groups_are_associated(string group)
    {
        using var fixture = new LstFixture();
        var catalog = Build(fixture, $"BOLT = {group}.VALUE_A", $"BOLT = {group}.VALUE_B");

        var attribute = catalog.ForContentType("BOLT").First(a => a.Group == group);

        Assert.True(attribute.IsAssociated);
        Assert.False(attribute.IsRelatedObject);
    }

    [Theory]
    [InlineData("MAIN_PART")]
    [InlineData("MAINPART")]
    [InlineData("SECONDARY_1")]
    [InlineData("ASSEMBLY")]
    [InlineData("CAST_UNIT")]
    [InlineData("DRAWING")]
    [InlineData("PROJECT")]
    [InlineData("POUR_UNIT")]
    public void Object_traversal_groups_are_related(string group)
    {
        using var fixture = new LstFixture();
        var catalog = Build(fixture, $"BOLT = {group}.VALUE_A", $"BOLT = {group}.VALUE_B");

        var attribute = catalog.ForContentType("BOLT").First(a => a.Group == group);

        Assert.True(attribute.IsRelatedObject);
        Assert.False(attribute.IsAssociated);
    }

    [Fact]
    public void Size_alone_would_misclassify_both_ways()
    {
        // The regression that motivated naming constituents instead of measuring them. On a bolt
        // ASSEMBLY is small yet a traversal; on a part PROFILE is larger yet a constituent. Any
        // rule that only counts attributes gets one of these wrong.
        using var fixture = new LstFixture();
        var bindings = new List<string>();
        for (var i = 0; i < 10; i++) bindings.Add($"BOLT = ASSEMBLY.SMALL_{i}");
        for (var i = 0; i < 40; i++) bindings.Add($"PART = PROFILE.BIGGER_{i}");

        var catalog = Build(fixture, bindings.ToArray());

        Assert.True(catalog.ForContentType("BOLT").First(a => a.Group == "ASSEMBLY").IsRelatedObject);
        Assert.True(catalog.ForContentType("PART").First(a => a.Group == "PROFILE").IsAssociated);
    }

    [Fact]
    public void An_unknown_group_defaults_to_related()
    {
        // Fail-safe direction: a group this build has never heard of costs nothing in Associated,
        // it just waits in Full. A custom environment can never make the default view slow.
        using var fixture = new LstFixture();
        var catalog = Build(fixture, "PART = SOME_VENDOR_EXTENSION.VALUE_A");

        Assert.True(catalog.ForContentType("PART").Single().IsRelatedObject);
    }

    [Fact]
    public void A_constituent_group_stuffed_with_attributes_is_treated_as_expensive()
    {
        // Size keeps a vote as a backstop, for an environment that has redefined a known group
        // into something far larger than the stock one.
        using var fixture = new LstFixture();
        var bindings = Enumerable.Range(0, TemplateAttributeCatalog.RelatedObjectGroupSize + 1)
            .Select(i => $"BOLT = NUT.VALUE_{i}")
            .ToArray();

        var catalog = Build(fixture, bindings);

        Assert.True(catalog.ForContentType("BOLT").First(a => a.Group == "NUT").IsRelatedObject);
    }

    [Fact]
    public void Direct_attributes_are_neither_grouped_nor_related()
    {
        using var fixture = new LstFixture();
        var catalog = Build(fixture, "BOLT = WEIGHT");

        var attribute = catalog.ForContentType("BOLT").Single();

        Assert.True(attribute.IsDirect);
        Assert.Equal(string.Empty, attribute.Group);
        Assert.False(attribute.IsRelatedObject);
    }
}
