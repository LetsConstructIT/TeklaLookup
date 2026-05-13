using System;
using System.Collections.Generic;
using System.Linq;

namespace TeklaLookup.App.Models;

/// <summary>
/// One entry in the in-memory session history: a frozen snapshot of the trail the user navigated
/// to. Holds the original <see cref="DecompositionFrame"/> instances so restoring is just a Trail
/// swap — no path-replay or root resolution needed for an in-session feature.
/// </summary>
public sealed class HistoryEntry
{
    public HistoryEntry(IEnumerable<DecompositionFrame> frames)
    {
        Frames = frames.ToList();
        Timestamp = DateTime.Now;
    }

    public DateTime Timestamp { get; }
    public IReadOnlyList<DecompositionFrame> Frames { get; }

    public string DisplayPath => string.Join("  ›  ", Frames.Select(f => f.Title));

    public override string ToString() => DisplayPath;
}
