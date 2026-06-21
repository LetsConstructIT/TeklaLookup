using TeklaLookup.App.Services;
using TSInfo = Tekla.Structures.TeklaStructuresInfo;

namespace TeklaLookup.App.Models;

/// <summary>
/// Materializes Tekla's static-only <see cref="TSInfo"/> helpers into a regular object whose
/// properties reflection can walk through. Each property is read once at construction time.
/// </summary>
public sealed class TeklaStructuresInfoSnapshot
{
    public TeklaStructuresInfoSnapshot()
    {
        CurrentProgramVersion = Try(() => TSInfo.GetCurrentProgramVersion());
        BuildNumber           = Try(() => TSInfo.GetBuildNumber());
        RevisionDate          = Try(() => TSInfo.GetRevisionDate());
        CurrentUser           = Try(() => TSInfo.GetCurrentUser());
        // GetPluginsFolder was added in Tekla 2022; resolve it at runtime so the 2021-floor build
        // still surfaces it when running on a newer Tekla, and reads as a clear note on 2021.
        PluginsFolder         = TryStatic(typeof(TSInfo), "GetPluginsFolder", "2022");
        LocalAppDataFolder    = Try(() => TSInfo.GetLocalAppDataFolder());
        CommonAppDataFolder   = Try(() => TSInfo.GetCommonAppDataFolder());
        FullTSRegistryKeyText = Try(() => TSInfo.GetFullTSRegistryKeyText());
        CopyRightText         = Try(() => TSInfo.GetCopyRightText());
    }

    public string CurrentProgramVersion { get; }
    public string BuildNumber { get; }
    public string RevisionDate { get; }
    public string CurrentUser { get; }
    public string PluginsFolder { get; }
    public string LocalAppDataFolder { get; }
    public string CommonAppDataFolder { get; }
    public string FullTSRegistryKeyText { get; }
    public string CopyRightText { get; }

    private static string Try(System.Func<string> producer)
    {
        try { return producer() ?? string.Empty; }
        catch (System.Exception ex) { return $"<error: {ex.Message}>"; }
    }

    // Invokes a public static parameterless string-returning helper by reflection. If this Tekla
    // version doesn't define it (the member was added in <paramref name="sinceVersion"/>), returns a
    // note saying so rather than a blank that looks like an empty result.
    private static string TryStatic(System.Type type, string method, string sinceVersion)
    {
        var m = OptionalTeklaApi.Method(type, method);
        if (m is null) return $"<requires Tekla {sinceVersion} or newer>";
        return Try(() => m.Invoke(null, null) as string ?? string.Empty);
    }
}
