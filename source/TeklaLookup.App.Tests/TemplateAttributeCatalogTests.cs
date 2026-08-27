using System;
using System.Linq;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// Parsing of <c>contentattributes*.lst</c>: the definitions section, the bindings section, and
/// the several ways Tekla writes an attribute name that is not the name you can query.
/// </summary>
public sealed class TemplateAttributeCatalogTests
{
    private static readonly string[] NoDirectories = Array.Empty<string>();

    [Fact]
    public void Reads_definitions_and_bindings()
    {
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[]
            {
                "NAME      CHARACTER LEFT  TRUE 20",
                "WEIGHT    FLOAT     RIGHT TRUE  8 0 Weight kg",
                "PHASE     INTEGER   LEFT  TRUE  5",
            },
            bindings: new[] { "PART = NAME", "PART = WEIGHT", "PART = PHASE" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var part = catalog.ForContentType("PART");

        Assert.Equal(3, part.Count);
        Assert.Equal(TemplateValueType.Character, part.Single(a => a.Name == "NAME").ValueType);
        Assert.Equal(TemplateValueType.Float, part.Single(a => a.Name == "WEIGHT").ValueType);
        Assert.Equal(TemplateValueType.Integer, part.Single(a => a.Name == "PHASE").ValueType);
    }

    [Fact]
    public void Bindings_are_scoped_to_their_content_type()
    {
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[] { "NAME CHARACTER LEFT TRUE 20", "CLASS_ATTR CHARACTER LEFT TRUE 6" },
            bindings: new[] { "PART = NAME", "PART = CLASS_ATTR", "BOLT = NAME" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);

        Assert.Equal(new[] { "NAME", "CLASS_ATTR" }, catalog.ForContentType("PART").Select(a => a.Name));
        Assert.Equal(new[] { "NAME" }, catalog.ForContentType("BOLT").Select(a => a.Name));
        Assert.Empty(catalog.ForContentType("WELD"));
        Assert.Empty(catalog.ForContentType(null));
    }

    [Fact]
    public void Dotted_names_take_their_datatype_from_the_last_segment()
    {
        // Datatypes are only ever declared for bare names; bindings are dotted. Getting this wrong
        // puts a name in the wrong GetAllReportProperties bucket, where it silently returns nothing.
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[] { "WEIGHT FLOAT RIGHT TRUE 8 0 Weight kg" },
            bindings: new[] { "BOLT = NUT.WEIGHT" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("BOLT"));

        Assert.Equal("NUT.WEIGHT", attribute.Name);
        Assert.Equal(TemplateValueType.Float, attribute.ValueType);
        Assert.Equal("NUT", attribute.Group);
        Assert.False(attribute.IsDirect);
    }

    [Fact]
    public void Drops_bindings_that_no_file_gives_a_datatype()
    {
        // Real environments do this: UDA datatypes live in objects.inp, not in any .lst. Such a
        // name cannot be bucketed, so it is unqueryable rather than merely unknown.
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[] { "NAME CHARACTER LEFT TRUE 20" },
            bindings: new[] { "PART = NAME", "PART = SOMETHING_UNDECLARED" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);

        Assert.Equal(new[] { "NAME" }, catalog.ForContentType("PART").Select(a => a.Name));
        Assert.Equal(1, catalog.UntypedCount);
    }

    [Fact]
    public void Strips_bracketed_virtual_nodes_including_the_spaces_inside_them()
    {
        // "USERDEFINED.[Base plate].SPACE_B" is ONE token containing a space. Splitting the
        // right-hand side on whitespace truncates the name to "USERDEFINED.[Base".
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[] { "SPACE_B CHARACTER LEFT TRUE 10" },
            bindings: new[] { "PART = USERDEFINED.[Base plate].SPACE_B      \"Space B\"" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("PART"));

        Assert.Equal("USERDEFINED.SPACE_B", attribute.Name);
        Assert.Equal("Space B", attribute.Label);
    }

    [Fact]
    public void Strips_version_suffixes_and_collapses_the_duplicates_they_create()
    {
        // #1/#2 mark version-specific variants of one underlying attribute.
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst", LstFixture.File_(
            definitions: new[] { "NUMBER INTEGER RIGHT TRUE 5" },
            bindings: new[] { "BOLT = NUT.NUMBER#1", "BOLT = NUT.NUMBER#2" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("BOLT"));

        Assert.Equal("NUT.NUMBER", attribute.Name);
    }

    [Theory]
    [InlineData("USERDEFINED.[Base plate].SPACE_B", "USERDEFINED.SPACE_B")]
    [InlineData("NUT.NUMBER#1", "NUT.NUMBER")]
    [InlineData("WEIGHT", "WEIGHT")]
    [InlineData("ASSEMBLY.MAINPART.PROFILE", "ASSEMBLY.MAINPART.PROFILE")]
    [InlineData("A..B", "A.B")]
    public void NormalizeName_produces_the_queryable_form(string raw, string expected)
        => Assert.Equal(expected, TemplateAttributeCatalog.NormalizeName(raw));

    [Theory]
    [InlineData("NUT.WEIGHT", "NUT")]
    [InlineData("ASSEMBLY.MAINPART.PROFILE", "ASSEMBLY")]
    [InlineData("WEIGHT", "")]
    public void GroupOf_returns_the_leading_segment(string name, string expected)
        => Assert.Equal(expected, TemplateAttributeCatalog.GroupOf(name));

    [Fact]
    public void Ignores_comments_but_not_quoted_text_that_looks_like_one()
    {
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst",
            "// leading comment\n" +
            "NAME CHARACTER LEFT TRUE 20 // trailing comment\n" +
            "[BINDINGS] // Do NOT remove this line\n" +
            "PART = NAME \"http://example.invalid\"\n");

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("PART"));

        Assert.Equal("NAME", attribute.Name);
        Assert.Equal("http://example.invalid", attribute.Label);
    }

    [Fact]
    public void Skips_malformed_lines_rather_than_failing_the_file()
    {
        using var fixture = new LstFixture();
        var file = fixture.Write("contentattributes.lst",
            "NAME CHARACTER LEFT TRUE 20\n" +
            "GARBAGE_WITHOUT_A_DATATYPE\n" +
            "ALSO NOT_A_DATATYPE LEFT\n" +
            "[BINDINGS]\n" +
            "PART = NAME\n" +
            "= NAME\n" +
            "not a content type = NAME\n" +
            "PART =\n");

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);

        Assert.Equal(new[] { "NAME" }, catalog.ForContentType("PART").Select(a => a.Name));
        Assert.Null(catalog.LoadError);
    }

    [Fact]
    public void First_file_wins_when_two_declare_the_same_datatype()
    {
        // Files arrive in Tekla's override order (model, project, firm, system), so the earlier
        // one is the more specific.
        using var fixture = new LstFixture();
        var specific = fixture.Write("a.lst", LstFixture.File_(
            definitions: new[] { "VALUE INTEGER RIGHT TRUE 5" }));
        var general = fixture.Write("b.lst", LstFixture.File_(
            definitions: new[] { "VALUE CHARACTER LEFT TRUE 20" },
            bindings: new[] { "PART = VALUE" }));

        var catalog = TemplateAttributeCatalog.Load(new[] { specific, general }, NoDirectories);

        Assert.Equal(TemplateValueType.Integer, Assert.Single(catalog.ForContentType("PART")).ValueType);
    }

    [Fact]
    public void An_unreadable_file_is_reported_without_losing_the_others()
    {
        using var fixture = new LstFixture();
        var good = fixture.Write("good.lst", LstFixture.File_(
            definitions: new[] { "NAME CHARACTER LEFT TRUE 20" },
            bindings: new[] { "PART = NAME" }));
        var missing = System.IO.Path.Combine(fixture.Dir(), "does_not_exist.lst");

        var catalog = TemplateAttributeCatalog.Load(new[] { missing, good }, NoDirectories);

        Assert.NotNull(catalog.LoadError);
        Assert.Single(catalog.ForContentType("PART"));
    }

    [Fact]
    public void Empty_catalog_reports_itself_as_empty()
    {
        Assert.True(TemplateAttributeCatalog.Empty.IsEmpty);
        Assert.Empty(TemplateAttributeCatalog.Empty.ForContentType("PART"));
        Assert.Equal(0, TemplateAttributeCatalog.Empty.AttributeCount);
    }
}
