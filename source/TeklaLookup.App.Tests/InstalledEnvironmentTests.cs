using System;
using System.IO;
using System.Linq;
using TeklaLookup.App.Services.TemplateAttributes;
using Xunit;

namespace TeklaLookup.App.Tests;

/// <summary>
/// Sanity checks against a real Tekla installation, so the synthetic fixtures elsewhere cannot
/// drift away from what environments actually ship.
/// </summary>
/// <remarks>
/// Opt-in, because the rest of the suite must run on a machine with no Tekla on it. Point
/// <c>TEKLALOOKUP_TEST_ENVIRONMENT</c> at a folder holding <c>contentattributes*.lst</c> (an
/// environment's <c>template\settings</c>) and, when the environment uses container files,
/// <c>TEKLALOOKUP_TEST_TPLED</c> at the installation's
/// <c>bin\applications\Tekla\Tools\TplEd\settings</c>. Without those the tests report as skipped.
/// </remarks>
public sealed class InstalledEnvironmentTests
{
    private const string EnvironmentVariable = "TEKLALOOKUP_TEST_ENVIRONMENT";
    private const string TplEdVariable = "TEKLALOOKUP_TEST_TPLED";

    private static string? Folder(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? null : path;
    }

    private static TemplateAttributeCatalog? LoadInstalled()
    {
        var environment = Folder(EnvironmentVariable);
        if (environment is null) return null;

        var tplEd = Folder(TplEdVariable);
        var searched = tplEd is null ? new[] { environment } : new[] { environment, tplEd };

        var files = searched
            .SelectMany(d => Directory.GetFiles(d, "*contentattributes*.lst"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return TemplateAttributeCatalog.Load(files, searched);
    }

    [Fact]
    public void A_real_environment_yields_a_populated_catalog()
    {
        var catalog = LoadInstalled();
        Assert.SkipWhen(catalog is null, $"Set {EnvironmentVariable} to run this test.");

        Assert.False(catalog!.IsEmpty);
        Assert.NotEmpty(catalog.ForContentType("PART"));
        Assert.NotEmpty(catalog.ForContentType("BOLT"));
    }

    [Fact]
    public void A_bolt_exposes_its_nut_and_washer_as_associated_attributes()
    {
        // The case the feature was built for, and the one most likely to be broken by a change to
        // group classification.
        var catalog = LoadInstalled();
        Assert.SkipWhen(catalog is null, $"Set {EnvironmentVariable} to run this test.");

        var bolt = catalog!.ForContentType("BOLT");

        Assert.Contains(bolt, a => a.Group == "NUT" && a.IsAssociated);
        Assert.Contains(bolt, a => a.Group == "WASHER" && a.IsAssociated);
    }

    [Fact]
    public void The_associated_tier_stays_a_small_fraction_of_the_full_one()
    {
        // The performance contract. If this ratio collapses, the default view has quietly become
        // the slow one again.
        var catalog = LoadInstalled();
        Assert.SkipWhen(catalog is null, $"Set {EnvironmentVariable} to run this test.");

        var bolt = catalog!.ForContentType("BOLT");
        var associated = bolt.Count(a =>
            TemplateAttributeSelection.IsSelected(a, TemplateAttributeScope.Associated, null));
        var full = bolt.Count(a =>
            TemplateAttributeSelection.IsSelected(a, TemplateAttributeScope.Full, null));

        Assert.True(associated < full / 4,
            $"Associated ({associated}) should be far smaller than Full ({full}).");
    }

    [Fact]
    public void Parsed_names_are_queryable_rather_than_authored()
    {
        var catalog = LoadInstalled();
        Assert.SkipWhen(catalog is null, $"Set {EnvironmentVariable} to run this test.");

        var suspicious = new[] { "PART", "BOLT", "ASSEMBLY", "WELD", "DRAWING" }
            .SelectMany(ct => catalog!.ForContentType(ct))
            .Where(a => a.Name.IndexOfAny(new[] { '[', ']', '#', ' ', '"' }) >= 0
                     || a.Name.EndsWith(".", StringComparison.Ordinal))
            .Select(a => a.Name)
            .Take(5)
            .ToArray();

        Assert.Empty(suspicious);
    }
}
