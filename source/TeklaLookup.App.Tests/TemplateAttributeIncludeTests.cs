using System;
using System.IO;
using System.Linq;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// <c>[INCLUDE]</c> handling. Several stock environments (Finland, ConstrusoftEuropean) ship
/// <c>contentattributes.lst</c> as a pure container — a handful of includes and an EMPTY
/// <c>[BINDINGS]</c> section — so a parser that ignores the directive reads a catalog of nothing
/// and the whole feature silently reports "no attributes".
/// </summary>
public sealed class TemplateAttributeIncludeTests
{
    private static readonly string[] NoDirectories = Array.Empty<string>();

    private static string Definitions => LstFixture.File_(
        definitions: new[] { "NAME CHARACTER LEFT TRUE 20", "WEIGHT FLOAT RIGHT TRUE 8 0 Weight kg" },
        bindings: new[] { "PART = NAME", "PART = WEIGHT" });

    [Fact]
    public void A_container_with_an_empty_bindings_section_still_yields_attributes()
    {
        using var fixture = new LstFixture();
        fixture.Write("settings/contentattributes_global.lst", Definitions);
        var container = fixture.Write("settings/contentattributes.lst",
            LstFixture.File_(includes: new[] { "contentattributes_global.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container }, NoDirectories);

        Assert.False(catalog.IsEmpty);
        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
    }

    [Fact]
    public void Resolves_an_include_written_relative_to_the_parent_directory()
    {
        // Environments write ".\settings\contentattributes_global.lst" from a file that already
        // sits IN a settings folder, so the path is relative to its parent, not to itself.
        using var fixture = new LstFixture();
        fixture.Write("template/settings/contentattributes_global.lst", Definitions);
        var container = fixture.Write("template/settings/contentattributes.lst",
            LstFixture.File_(includes: new[] { @".\settings\contentattributes_global.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container }, NoDirectories);

        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
    }

    [Fact]
    public void Falls_back_to_the_search_directories()
    {
        // The real case: the master list lives in the installation's Template Editor settings
        // folder, nowhere near the environment that includes it.
        using var fixture = new LstFixture();
        var installation = fixture.Dir("installation/TplEd/settings");
        File.WriteAllText(Path.Combine(installation, "contentattributes_global.lst"), Definitions);

        var container = fixture.Write("environment/contentattributes.lst",
            LstFixture.File_(includes: new[] { @".\settings\contentattributes_global.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container }, new[] { installation });

        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
    }

    [Fact]
    public void A_missing_include_is_tolerated()
    {
        // Environments reference optional integration files (ArchiCAD, MagiCAD, Revit) that are
        // not always installed. That must not cost the includes that did resolve.
        using var fixture = new LstFixture();
        fixture.Write("contentattributes_global.lst", Definitions);
        var container = fixture.Write("contentattributes.lst", LstFixture.File_(
            includes: new[] { "contentattributes_global.lst", "contentattributes_ArchiCAD.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container }, NoDirectories);

        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
        Assert.Null(catalog.LoadError);
    }

    [Fact]
    public void A_container_whose_includes_all_fail_stays_empty_without_throwing()
    {
        using var fixture = new LstFixture();
        var container = fixture.Write("contentattributes.lst",
            LstFixture.File_(includes: new[] { "nope_does_not_exist.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container }, NoDirectories);

        Assert.True(catalog.IsEmpty);
        Assert.Single(catalog.SourceFiles);
    }

    [Fact]
    public void Circular_includes_terminate()
    {
        using var fixture = new LstFixture();
        fixture.Write("a.lst", LstFixture.File_(
            includes: new[] { "b.lst" },
            definitions: new[] { "NAME CHARACTER LEFT TRUE 20" },
            bindings: new[] { "PART = NAME" }));
        fixture.Write("b.lst", LstFixture.File_(includes: new[] { "a.lst" }));

        var catalog = TemplateAttributeCatalog.Load(
            new[] { Path.Combine(fixture.Dir(), "a.lst") }, NoDirectories);

        Assert.Equal(new[] { "NAME" }, catalog.ForContentType("PART").Select(a => a.Name));
    }

    [Fact]
    public void Includes_are_followed_transitively()
    {
        using var fixture = new LstFixture();
        fixture.Write("third.lst", Definitions);
        fixture.Write("second.lst", LstFixture.File_(includes: new[] { "third.lst" }));
        var first = fixture.Write("first.lst", LstFixture.File_(includes: new[] { "second.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { first }, NoDirectories);

        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
    }

    [Fact]
    public void A_file_reached_twice_is_applied_once()
    {
        // Discovery finds a file AND another file includes it. Reporting it twice would make the
        // diagnostics lie about what was read.
        using var fixture = new LstFixture();
        var shared = fixture.Write("shared.lst", Definitions);
        var container = fixture.Write("contentattributes.lst",
            LstFixture.File_(includes: new[] { "shared.lst" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { container, shared }, NoDirectories);

        Assert.Equal(2, catalog.SourceFiles.Count);
        Assert.Equal(new[] { "NAME", "WEIGHT" }, catalog.ForContentType("PART").Select(a => a.Name));
    }
}
