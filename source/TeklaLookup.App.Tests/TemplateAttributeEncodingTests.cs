using System;
using System.Linq;
using System.Text;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// Encoding detection for <c>.lst</c> files. Stock installations mix three encodings: plain
/// ASCII, UTF-8 with a BOM (the Template Editor's newer output), and legacy ANSI — Finland's
/// <c>FI_contentattributes_userdefined.lst</c> and the USA environment's
/// <c>contentattributes_external.lst</c> ship as ANSI, and firm files generated from
/// <c>objects.inp</c> usually do too. A parser that assumes UTF-8 turns every ANSI byte into
/// U+FFFD, corrupting quoted labels and any attribute name carrying a local character.
/// </summary>
public sealed class TemplateAttributeEncodingTests
{
    private static readonly string[] NoDirectories = Array.Empty<string>();

    private const string Content =
        "NAME CHARACTER LEFT TRUE 20\n" +
        "[BINDINGS] // Do NOT remove this line\n" +
        "PART = NAME \"Teräs\"\n";

    [Fact]
    public void A_utf8_file_without_bom_keeps_multibyte_label_characters()
    {
        using var fixture = new LstFixture();
        var file = fixture.WriteBytes("contentattributes.lst",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(Content));

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("PART"));

        Assert.Equal("NAME", attribute.Name);
        Assert.Equal("Teräs", attribute.Label);
    }

    [Fact]
    public void A_utf8_file_with_bom_does_not_leak_the_bom_into_the_first_line()
    {
        using var fixture = new LstFixture();
        var file = fixture.WriteBytes("contentattributes.lst",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
                .GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(Content))
                .ToArray());

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("PART"));

        // A leaked BOM would glue itself onto "NAME" and make the definition unparseable.
        Assert.Equal("NAME", attribute.Name);
        Assert.Equal("Teräs", attribute.Label);
    }

    [Fact]
    public void A_legacy_ansi_file_is_not_decoded_as_broken_utf8()
    {
        // 0xE4 is 'ä' in the Western ANSI code pages and an invalid lead byte in UTF-8. What the
        // machine's own ANSI code page maps it to is the machine's business — the contract is
        // that the file parses and nothing is replaced with U+FFFD.
        using var fixture = new LstFixture();
        var ansi = Encoding.ASCII.GetBytes(Content).ToArray();
        ansi[Array.IndexOf(ansi, (byte)'?')] = 0xE4; // ASCII encoder turned the 'ä' into '?'
        var file = fixture.WriteBytes("contentattributes.lst", ansi);

        var catalog = TemplateAttributeCatalog.Load(new[] { file }, NoDirectories);
        var attribute = Assert.Single(catalog.ForContentType("PART"));

        Assert.Equal("NAME", attribute.Name);
        Assert.NotNull(attribute.Label);
        Assert.DoesNotContain('�', attribute.Label!);
    }
}
