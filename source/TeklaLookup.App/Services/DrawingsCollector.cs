using System.Collections;
using System.Collections.Generic;
using Tekla.Structures.Drawing;

namespace TeklaLookup.App.Services;

/// <summary>
/// Reads drawings from the running Tekla session via <see cref="DrawingHandler"/>. Pure query
/// surface — no commits, no SetActiveDrawing side effects.
/// </summary>
public sealed class DrawingsCollector
{
    private readonly DrawingHandler _handler = new();

    public bool IsConnected => _handler.GetConnectionStatus();

    /// <summary>Iterates every drawing in the model database.</summary>
    public IEnumerable<Drawing> GetAllDrawings()
    {
        var enumerator = _handler.GetDrawings();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is Drawing d)
                yield return d;
        }
    }

    public Drawing? GetActiveDrawing() => _handler.GetActiveDrawing();

    /// <summary>
    /// Returns the drawing objects the user currently has selected in the active drawing editor.
    /// Empty when no drawing is open or no objects are selected.
    /// </summary>
    public IEnumerable<DrawingObject> GetSelectedDrawingObjects()
    {
        // Tekla 2022: GetDrawingObjectSelector().GetSelected() throws a TypeInitializationException
        // from an internal selection type ("TargetDrawing == null") when no drawing is open in the
        // editor. LoadSelected always probes this surface, so guard on the active drawing first to
        // keep model-only selection loads working.
        if (_handler.GetActiveDrawing() is null)
            yield break;

        var enumerator = _handler.GetDrawingObjectSelector().GetSelected();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is DrawingObject d)
                yield return d;
        }
    }

    /// <summary>Switches the Tekla UI to the given drawing (opens it in the editor).</summary>
    public void OpenInTekla(Drawing drawing) => _handler.SetActiveDrawing(drawing, true);

    public sealed class DrawingSelectionResult
    {
        public int Attempted { get; set; }       // items handed to Tekla
        public int Selected { get; set; }        // items Tekla actually highlighted
        public int Skipped { get; set; }         // belonged to another drawing
        public bool BatchAccepted { get; set; }  // first SelectObjects(...) call return
        public bool FellBackToSingles { get; set; }
    }

    /// <summary>
    /// Selects the given drawing objects in the currently active drawing editor. Items whose
    /// parent drawing isn't the active one are skipped. Caller should ensure the relevant
    /// drawing is open first (e.g. via <see cref="OpenInTekla"/>).
    /// </summary>
    public DrawingSelectionResult SelectInDrawing(IEnumerable<DrawingObject> drawingObjects)
    {
        var result = new DrawingSelectionResult();
        var active = _handler.GetActiveDrawing();
        if (active is null) return result;

        var list = new ArrayList();
        foreach (var obj in drawingObjects)
        {
            var owner = obj.GetDrawing();
            if (owner is null || !owner.IsSameDatabaseObject(active))
            {
                result.Skipped++;
                continue;
            }
            list.Add(obj);
        }

        result.Attempted = list.Count;
        if (list.Count == 0) return result;

        var selector = _handler.GetDrawingObjectSelector();
        result.BatchAccepted = selector.SelectObjects(list, false);
        if (result.BatchAccepted)
        {
            result.Selected = list.Count;
            return result;
        }

        // Fall back to one-by-one selection so a single poisonous item doesn't drop the whole
        // batch. Each successful SelectObjects(extend=true) accumulates into the live selection.
        result.FellBackToSingles = true;
        selector.UnselectAllObjects();
        var single = new ArrayList(1) { null };
        foreach (DrawingObject obj in list)
        {
            single[0] = obj;
            if (selector.SelectObjects(single, true))
                result.Selected++;
        }
        return result;
    }
}
