using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TeklaLookup.App.Services;

[DataContract]
public sealed class PinnedScope
{
    [DataMember] public string Scope { get; set; } = string.Empty;
    [DataMember] public string[] Names { get; set; } = new string[0];
}

[DataContract]
public sealed class PinnedAttributeData
{
    [DataMember] public PinnedScope[] Scopes { get; set; } = new PinnedScope[0];
}

/// <summary>
/// Remembers which attributes the user starred, per content type, in
/// <c>%LOCALAPPDATA%\TeklaLookup\pinned-attributes.json</c>.
/// </summary>
/// <remarks>
/// Kept out of <see cref="UserSettings"/> deliberately: these lists grow to hundreds of entries and
/// are written on every star click, whereas settings.json is a small blob flushed on exit.
/// Names are stored verbatim; one that no longer resolves after an environment change is ignored
/// at read time rather than pruned, so switching back restores the pin.
/// <para>
/// Matching is case-SENSITIVE on purpose. A part exposes both a CLR property <c>Profile</c> and a
/// template attribute <c>PROFILE</c>; under a case-insensitive comparison, starring one would
/// silently star the other.
/// </para>
/// </remarks>
public static class PinnedAttributeStore
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeklaLookup");
    private static readonly string FilePath = Path.Combine(Folder, "pinned-attributes.json");

    private static readonly Dictionary<string, HashSet<string>> Pins =
        new(StringComparer.Ordinal);

    private static readonly string[] NoNames = new string[0];

    public static void Load()
    {
        try
        {
            Pins.Clear();
            if (!File.Exists(FilePath)) return;

            using var stream = File.OpenRead(FilePath);
            var serializer = new DataContractJsonSerializer(typeof(PinnedAttributeData));
            if (serializer.ReadObject(stream) is not PinnedAttributeData data) return;

            foreach (var scope in data.Scopes ?? new PinnedScope[0])
            {
                if (string.IsNullOrEmpty(scope?.Scope)) continue;
                Pins[scope!.Scope] = new HashSet<string>(
                    scope.Names ?? NoNames, StringComparer.Ordinal);
            }
        }
        catch
        {
            // Corrupt or unreadable pins — start empty rather than block startup.
            Pins.Clear();
        }
    }

    /// <summary>Names pinned for a content type, or empty. Never null.</summary>
    public static IReadOnlyCollection<string> ForScope(string? scope)
    {
        if (string.IsNullOrEmpty(scope)) return NoNames;
        return Pins.TryGetValue(scope!, out var names) ? names : (IReadOnlyCollection<string>)NoNames;
    }

    public static bool IsPinned(string? scope, string? name)
    {
        if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(name)) return false;
        return Pins.TryGetValue(scope!, out var names) && names.Contains(name!);
    }

    /// <summary>Flips the pin and persists immediately. Returns the new state.</summary>
    public static bool Toggle(string? scope, string? name)
    {
        if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(name)) return false;

        if (!Pins.TryGetValue(scope!, out var names))
            Pins[scope!] = names = new HashSet<string>(StringComparer.Ordinal);

        var pinned = names.Add(name!);
        if (!pinned) names.Remove(name!);
        if (names.Count == 0) Pins.Remove(scope!);

        Save();
        return pinned;
    }

    public static int CountForScope(string? scope) => ForScope(scope).Count;

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var data = new PinnedAttributeData
            {
                Scopes = Pins
                    .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new PinnedScope
                    {
                        Scope = p.Key,
                        Names = p.Value.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(),
                    })
                    .ToArray(),
            };

            using var stream = File.Create(FilePath);
            var serializer = new DataContractJsonSerializer(typeof(PinnedAttributeData));
            serializer.WriteObject(stream, data);
        }
        catch
        {
            // Best effort: a write failure costs the pin at next launch, nothing more.
        }
    }
}
