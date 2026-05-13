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
        PluginsFolder         = Try(() => TSInfo.GetPluginsFolder());
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
}
