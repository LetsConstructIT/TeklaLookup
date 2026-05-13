using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TeklaLookup.App.Services;

[DataContract]
public sealed class UserSettings
{
    [DataMember(EmitDefaultValue = false)] public string? Theme { get; set; }
    [DataMember(EmitDefaultValue = false)] public bool IsTopmost { get; set; }
    [DataMember(EmitDefaultValue = false)] public double? WindowLeft { get; set; }
    [DataMember(EmitDefaultValue = false)] public double? WindowTop { get; set; }
    [DataMember(EmitDefaultValue = false)] public double? WindowWidth { get; set; }
    [DataMember(EmitDefaultValue = false)] public double? WindowHeight { get; set; }
    [DataMember(EmitDefaultValue = false)] public bool IsMaximized { get; set; }
}

/// <summary>
/// Loads and persists <see cref="UserSettings"/> in <c>%LOCALAPPDATA%\TeklaLookup\settings.json</c>.
/// Single-process, single-user — no locking. Errors are swallowed so a corrupt settings file never
/// blocks startup; the user just loses one session's worth of preferences.
/// </summary>
public static class SettingsStore
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeklaLookup");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static UserSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            using var stream = File.OpenRead(FilePath);
            var serializer = new DataContractJsonSerializer(typeof(UserSettings));
            if (serializer.ReadObject(stream) is UserSettings loaded)
                Current = loaded;
        }
        catch
        {
            // Corrupt or unreadable settings — fall back to defaults rather than crash on startup.
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            using var stream = File.Create(FilePath);
            var serializer = new DataContractJsonSerializer(typeof(UserSettings));
            serializer.WriteObject(stream, Current);
        }
        catch
        {
            // Best-effort: a write failure shouldn't crash the app.
        }
    }
}
