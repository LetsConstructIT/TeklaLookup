namespace TeklaLookup.App.Models;

/// <summary>
/// One step in the drill-down history: the object whose properties are on screen + a display title.
/// </summary>
public sealed class DecompositionFrame
{
    public DecompositionFrame(object target, string title)
    {
        Target = target;
        Title = title;
    }

    public object Target { get; }
    public string Title { get; }
}
