using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services;

namespace TeklaLookup.App.ViewModels;

/// <summary>
/// Drives an <see cref="Views.InspectorWindow"/> — a standalone drill-down pane seeded with a
/// single root target, used for multi-pane comparison alongside the main window. Reuses the
/// same collector / visualizer / decomposer instances as the main view-model so overlays and
/// identifier resolution stay coherent across windows.
/// </summary>
public sealed class InspectorViewModel : BaseViewModel
{
    private readonly TeklaObjectsCollector _collector;
    private readonly DrawingsCollector _drawingsCollector;
    private readonly GeometryVisualizer _visualizer;
    private readonly ObjectDecomposer _decomposer;
    private readonly JsonObjectDumper _jsonDumper = new();

    private string _title;
    private string _status = "Ready.";
    private string? _currentTargetTitle;

    public InspectorViewModel(
        object target,
        string title,
        TeklaObjectsCollector collector,
        DrawingsCollector drawingsCollector,
        GeometryVisualizer visualizer,
        ObjectDecomposer decomposer)
    {
        _collector = collector;
        _drawingsCollector = drawingsCollector;
        _visualizer = visualizer;
        _decomposer = decomposer;
        _title = $"TeklaLookup — {title}";

        NavigateBackCommand = new RelayCommand(_ => NavigateBack(), _ => CanGoBack);
        RefreshCommand = new RelayCommand(_ => Refresh(), _ => Trail.Count > 0);
        DumpFrameJsonCommand = new RelayCommand(_ => DumpFrameJson(), _ => Trail.Count > 0);
        NavigateToFrameCommand = new RelayCommand(p => NavigateToFrame(p as DecompositionFrame));
        DrillIntoCommand = new RelayCommand(p => DrillInto(p as PropertyEntry),
            p => (p as PropertyEntry)?.IsDrillable == true);
        OpenInNewWindowCommand = new RelayCommand(p => OpenInNewWindow(p as PropertyEntry),
            p => (p as PropertyEntry)?.IsDrillable == true);

        CopyPropertyValueCommand = new RelayCommand(p => CopyToClipboard((p as PropertyEntry)?.Value));
        CopyPropertyNameCommand  = new RelayCommand(p => CopyToClipboard((p as PropertyEntry)?.Name));
        CopyPropertyLineCommand  = new RelayCommand(p =>
        {
            if (p is PropertyEntry e) CopyToClipboard($"{e.Name} = {e.Value}");
        });
        HighlightCommand = new RelayCommand(p => Highlight(p as PropertyEntry),
            p => (p as PropertyEntry)?.IsHighlightable == true);
        SelectPropertyInTeklaCommand = new RelayCommand(
            p => SelectPropertyInTekla(p as PropertyEntry),
            p => HasShowableTarget(p as PropertyEntry));

        PropertiesView = CollectionViewSource.GetDefaultView(Properties);
        PropertiesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyEntry.Category)));

        Trail.Add(new DecompositionFrame(target, title));
        RenderCurrent();
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string? CurrentTargetTitle
    {
        get => _currentTargetTitle;
        private set => SetProperty(ref _currentTargetTitle, value);
    }

    public bool CanGoBack => Trail.Count > 1;

    public ObservableCollection<DecompositionFrame> Trail { get; } = new();
    public ObservableCollection<PropertyEntry> Properties { get; } = new();
    public ICollectionView PropertiesView { get; }

    public ICommand NavigateBackCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DumpFrameJsonCommand { get; }
    public ICommand NavigateToFrameCommand { get; }
    public ICommand DrillIntoCommand { get; }
    public ICommand OpenInNewWindowCommand { get; }
    public ICommand CopyPropertyValueCommand { get; }
    public ICommand CopyPropertyNameCommand { get; }
    public ICommand CopyPropertyLineCommand { get; }
    public ICommand HighlightCommand { get; }
    public ICommand SelectPropertyInTeklaCommand { get; }

    private void DrillInto(PropertyEntry? entry)
    {
        if (entry is null || !entry.IsDrillable || entry.RawValue is null) return;
        try
        {
            var target = entry.RawValue;
            if (target is IEnumerable enumerable && target is not string && target is not IDictionary)
                target = enumerable.Cast<object?>().ToList();

            Trail.Add(new DecompositionFrame(target, $"{entry.Name} : {entry.ValueType}"));
            RenderCurrent();
            Status = $"Decomposed {entry.Name}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to drill into {entry.Name}: {ex.Message}";
        }
    }

    private void OpenInNewWindow(PropertyEntry? entry)
    {
        if (entry is null || !entry.IsDrillable || entry.RawValue is null) return;
        var target = entry.RawValue;
        if (target is IEnumerable enumerable && target is not string && target is not IDictionary)
            target = enumerable.Cast<object?>().ToList();
        InspectorWindowFactory.Open(target, $"{entry.Name} : {entry.ValueType}",
            _collector, _drawingsCollector, _visualizer, _decomposer);
    }

    /// <summary>
    /// Re-runs decomposition on the current frame, picking up any changes made in Tekla since
    /// the frame was opened. For model / drawing objects we call <c>Select()</c> first to pull
    /// fresh database state, since Tekla wrappers cache property values until refreshed.
    /// </summary>
    private void Refresh()
    {
        if (Trail.Count == 0) return;
        try
        {
            var frame = Trail[Trail.Count - 1];
            switch (frame.Target)
            {
                case Tekla.Structures.Model.ModelObject mo:
                    try { mo.Select(); } catch { /* best effort */ }
                    break;
                case Tekla.Structures.Drawing.DrawingObject d:
                    try { d.Select(); } catch { /* best effort */ }
                    break;
            }
            RenderCurrent();
            Status = $"Refreshed {frame.Title}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to refresh: {ex.Message}";
        }
    }

    /// <summary>
    /// Dumps the object currently shown in this pane (the active trail frame) to a curated JSON file,
    /// shaped for recreating a similar object via the Tekla API. See <see cref="JsonObjectDumper"/>.
    /// </summary>
    private void DumpFrameJson()
    {
        if (Trail.Count == 0) return;
        var frame = Trail[Trail.Count - 1];

        try
        {
            var json = _jsonDumper.DumpFrame(frame.Target).ToIndentedString();
            var path = JsonDumpFile.Save(json, $"{frame.Title}.json");
            Status = path is null
                ? "JSON dump cancelled."
                : $"Saved JSON dump of {frame.Title} to {path}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to dump JSON: {ex.Message}";
        }
    }

    private void NavigateBack()
    {
        if (Trail.Count <= 1) return;
        Trail.RemoveAt(Trail.Count - 1);
        RenderCurrent();
    }

    private void NavigateToFrame(DecompositionFrame? frame)
    {
        if (frame is null) return;
        var index = Trail.IndexOf(frame);
        if (index < 0) return;
        while (Trail.Count > index + 1)
            Trail.RemoveAt(Trail.Count - 1);
        RenderCurrent();
    }

    private void RenderCurrent()
    {
        Properties.Clear();
        if (Trail.Count == 0)
        {
            CurrentTargetTitle = null;
        }
        else
        {
            var frame = Trail[Trail.Count - 1];
            CurrentTargetTitle = frame.Title;
            try
            {
                foreach (var entry in _decomposer.Decompose(frame.Target))
                    Properties.Add(entry);
            }
            catch (Exception ex)
            {
                Status = $"Failed to decompose object: {ex.Message}";
            }
        }
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void Highlight(PropertyEntry? entry)
    {
        if (entry is null) return;
        try
        {
            Status = _visualizer.TryHighlight(entry.RawValue)
                ? $"Drew overlay for {entry.Name}."
                : $"Can't visualize {entry.Name} ({entry.ValueType}).";
        }
        catch (Exception ex)
        {
            Status = $"Failed to draw overlay: {ex.Message}";
        }
    }

    private void SelectPropertyInTekla(PropertyEntry? entry)
    {
        if (entry?.RawValue is null) return;
        var modelObjects = CollectModelObjects(entry).ToList();
        var drawingObjects = CollectDrawingObjects(entry).ToList();
        if (modelObjects.Count == 0 && drawingObjects.Count == 0) return;
        try
        {
            if (modelObjects.Count > 0)
                _collector.SelectInModel(modelObjects);
            if (drawingObjects.Count > 0)
                _drawingsCollector.SelectInDrawing(drawingObjects);
            Status = $"Selected {modelObjects.Count + drawingObjects.Count} object(s) in Tekla.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to show in Tekla: {ex.Message}";
        }
    }

    private bool HasShowableTarget(PropertyEntry? entry)
        => CollectModelObjects(entry).Any() || CollectDrawingObjects(entry).Any();

    private IEnumerable<Tekla.Structures.Model.ModelObject> CollectModelObjects(PropertyEntry? entry)
    {
        if (entry?.RawValue is null) yield break;
        foreach (var mo in ToModelObjects(entry.RawValue))
            yield return mo;
    }

    private IEnumerable<Tekla.Structures.Model.ModelObject> ToModelObjects(object value)
    {
        switch (value)
        {
            case Tekla.Structures.Model.ModelObject mo:
                yield return mo;
                yield break;
            case Tekla.Structures.Identifier id:
                var resolved = _collector.ResolveIdentifier(id);
                if (resolved is not null) yield return resolved;
                yield break;
            case string:
                yield break;
            case IEnumerable list:
                foreach (var item in list)
                {
                    if (item is null) continue;
                    foreach (var m in ToModelObjects(item))
                        yield return m;
                }
                yield break;
        }
    }

    private static IEnumerable<Tekla.Structures.Drawing.DrawingObject> CollectDrawingObjects(PropertyEntry? entry)
    {
        if (entry?.RawValue is null) yield break;
        foreach (var d in ToDrawingObjects(entry.RawValue))
            yield return d;
    }

    private static IEnumerable<Tekla.Structures.Drawing.DrawingObject> ToDrawingObjects(object value)
    {
        switch (value)
        {
            case Tekla.Structures.Drawing.DrawingObject d:
                yield return d;
                yield break;
            case string:
                yield break;
            case IEnumerable list:
                foreach (var item in list)
                {
                    if (item is null) continue;
                    foreach (var d in ToDrawingObjects(item))
                        yield return d;
                }
                yield break;
        }
    }

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { System.Windows.Clipboard.SetText(text); }
        catch { /* best effort */ }
    }
}

internal static class InspectorWindowFactory
{
    public static void Open(
        object target,
        string title,
        TeklaObjectsCollector collector,
        DrawingsCollector drawingsCollector,
        GeometryVisualizer visualizer,
        ObjectDecomposer decomposer)
    {
        var vm = new InspectorViewModel(target, title, collector, drawingsCollector, visualizer, decomposer);
        var window = new Views.InspectorWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        window.Show();
    }
}
