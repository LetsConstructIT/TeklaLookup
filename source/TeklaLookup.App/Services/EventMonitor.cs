using System;
using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;
using TSDrawingEvents = Tekla.Structures.Drawing.Events;
using TSModelEvents = Tekla.Structures.Model.Events;

namespace TeklaLookup.App.Services;

/// <summary>
/// Wraps Tekla's model + drawing event subscriptions and emits a single normalized
/// <see cref="EventLogEntry"/> per change. Callbacks run on Tekla's background thread — the caller
/// is responsible for marshalling <see cref="EntryLogged"/> handlers to the UI thread.
/// </summary>
public sealed class EventMonitor : IDisposable
{
    private readonly TSModelEvents _modelEvents = new();
    private readonly TSDrawingEvents _drawingEvents = new();
    private readonly Model _model = new();
    private bool _running;

    public event Action<EventLogEntry>? EntryLogged;

    public void Start()
    {
        if (_running) return;
        _modelEvents.ModelObjectChanged += OnModelObjectChanged;
        _modelEvents.SelectionChange    += OnSelectionChange;
        _modelEvents.ModelSave          += OnModelSaved;
        _modelEvents.ModelLoadInfo      += OnModelLoaded;
        _modelEvents.Register();

        _drawingEvents.DrawingInserted += OnDrawingInserted;
        _drawingEvents.DrawingUpdated  += OnDrawingUpdated;
        _drawingEvents.DrawingDeleted  += OnDrawingDeleted;
        _drawingEvents.DrawingChanged  += OnDrawingChanged;
        _drawingEvents.Register();

        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;
        try
        {
            _modelEvents.ModelObjectChanged -= OnModelObjectChanged;
            _modelEvents.SelectionChange    -= OnSelectionChange;
            _modelEvents.ModelSave          -= OnModelSaved;
            _modelEvents.ModelLoadInfo      -= OnModelLoaded;
            _modelEvents.UnRegister();

            _drawingEvents.DrawingInserted -= OnDrawingInserted;
            _drawingEvents.DrawingUpdated  -= OnDrawingUpdated;
            _drawingEvents.DrawingDeleted  -= OnDrawingDeleted;
            _drawingEvents.DrawingChanged  -= OnDrawingChanged;
            _drawingEvents.UnRegister();
        }
        finally
        {
            _running = false;
        }
    }

    public void Dispose() => Stop();

    private void OnModelObjectChanged(List<ChangeData> changes)
    {
        foreach (var change in changes)
        {
            var guid = string.Empty;
            if (change.Object != null)
            {
                try { guid = _model.GetGUIDByIdentifier(change.Object.Identifier).ToString(); }
                catch { /* identifier no longer resolvable (rare) */ }
            }

            Emit(new EventLogEntry
            {
                Source = "Model",
                Kind = change.Type.ToString(),
                ObjectType = change.Object?.GetType().Name ?? "(deleted)",
                ObjectId = change.Object?.Identifier.ID.ToString() ?? string.Empty,
                Detail = guid,
                Guid = guid,
            });
        }
    }

    private void OnSelectionChange()
    {
        var count = 0;
        try
        {
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            var enumerator = selector.GetSelectedObjects();
            while (enumerator.MoveNext())
                count++;
        }
        catch { /* selection query is best-effort */ }

        Emit(new EventLogEntry
        {
            Source = "Model",
            Kind = "SelectionChange",
            Detail = count == 0 ? "(empty)" : $"{count} object(s) selected",
        });
    }

    private void OnModelSaved()
        => Emit(new EventLogEntry { Source = "Model", Kind = "ModelSave" });

    private void OnModelLoaded(string info)
        => Emit(new EventLogEntry { Source = "Model", Kind = "ModelLoadInfo", Detail = info });

    private void OnDrawingInserted()
        => Emit(new EventLogEntry { Source = "Drawing", Kind = "Inserted" });

    private void OnDrawingUpdated(Tekla.Structures.Drawing.Drawing drawing,
                                  Tekla.Structures.Drawing.Events.DrawingUpdateTypeEnum type)
        => Emit(new EventLogEntry
        {
            Source = "Drawing",
            Kind = $"Updated ({type})",
            ObjectType = drawing?.GetType().Name ?? string.Empty,
            Detail = drawing?.Name ?? string.Empty,
        });

    private void OnDrawingDeleted()
        => Emit(new EventLogEntry { Source = "Drawing", Kind = "Deleted" });

    private void OnDrawingChanged()
        => Emit(new EventLogEntry { Source = "Drawing", Kind = "StatusChanged" });

    private void Emit(EventLogEntry entry) => EntryLogged?.Invoke(entry);
}
