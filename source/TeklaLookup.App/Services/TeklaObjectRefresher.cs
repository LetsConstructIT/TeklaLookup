namespace TeklaLookup.App.Services;

/// <summary>
/// Pulls fresh database state into a Tekla wrapper object. Wrappers cache their property values
/// from the moment they were fetched, so anything the user changes in Tekla afterwards (lock
/// status, UDAs, geometry) stays invisible until the instance is re-selected.
/// </summary>
public static class TeklaObjectRefresher
{
    /// <summary>
    /// Re-reads <paramref name="target"/> from the database when it is a Tekla object that
    /// supports it. Best effort — a failed <c>Select()</c> (deleted object, closed model) leaves
    /// the cached values in place rather than breaking the surrounding refresh.
    /// </summary>
    public static void Refresh(object? target)
    {
        switch (target)
        {
            case Tekla.Structures.Model.ModelObject modelObject:
                try { modelObject.Select(); } catch { /* best effort */ }
                break;
            // DatabaseObject, not DrawingObject: Drawing and DrawingObject are *siblings* under it,
            // so matching the narrower type silently skips whole-drawing frames.
            case Tekla.Structures.Drawing.DatabaseObject databaseObject:
                try { databaseObject.Select(); } catch { /* best effort */ }
                break;
        }
    }
}
