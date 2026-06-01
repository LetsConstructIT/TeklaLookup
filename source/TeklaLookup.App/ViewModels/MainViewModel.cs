using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using TeklaLookup.App.Mappers;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services;

namespace TeklaLookup.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly TeklaObjectsCollector _collector = new();
    private readonly DrawingsCollector _drawingsCollector = new();
    private readonly ObjectDecomposer _decomposer = new();
    private readonly GeometryVisualizer _visualizer = new();
    private readonly JsonObjectDumper _jsonDumper = new();

    private string _title = "TeklaLookup";
    private string _status = "Ready.";
    private bool _isBusy;
    private bool _isTopmost = SettingsStore.Current.IsTopmost;
    private TeklaObjectSnapshot? _selectedObject;
    private string? _currentTargetTitle;
    private string _searchText = string.Empty;

    public MainViewModel()
    {
        LoadSelectedCommand = new RelayCommand(_ => LoadSelected(), _ => !IsBusy);
        LoadAllCommand = new RelayCommand(_ => LoadAll(), _ => !IsBusy);
        LoadByTypeCommand = new RelayCommand(_ => LoadByType(), _ => !IsBusy);
        SearchByIdCommand = new RelayCommand(_ => SearchById(), _ => !IsBusy);
        LoadModelRootCommand = new RelayCommand(_ => LoadModelRoot(), _ => !IsBusy);
        LoadActiveViewCommand = new RelayCommand(_ => LoadActiveView(), _ => !IsBusy);
        LoadReferenceModelsCommand = new RelayCommand(_ => LoadReferenceModels(), _ => !IsBusy);
        LoadDrawingsCommand = new RelayCommand(_ => LoadDrawings(), _ => !IsBusy);
        LoadActiveDrawingCommand = new RelayCommand(_ => LoadActiveDrawing(), _ => !IsBusy);
        PickObjectCommand = new RelayCommand(_ => PickObject(), _ => !IsBusy);
        PickObjectsCommand = new RelayCommand(_ => PickObjects(), _ => !IsBusy);
        NavigateBackCommand = new RelayCommand(_ => NavigateBack(), _ => CanGoBack);
        RefreshCommand = new RelayCommand(_ => Refresh(), _ => !IsBusy && Trail.Count > 0);
        NavigateToFrameCommand = new RelayCommand(p => NavigateToFrame(p as DecompositionFrame));
        DrillIntoCommand = new RelayCommand(p => DrillInto(p as PropertyEntry), p => (p as PropertyEntry)?.IsDrillable == true);
        OpenInNewWindowCommand = new RelayCommand(p => OpenInNewWindow(p as PropertyEntry), p => (p as PropertyEntry)?.IsDrillable == true);
        OpenSnapshotInNewWindowCommand = new RelayCommand(
            p => OpenSnapshotsInNewWindows(p),
            p => ExtractSnapshotsWithSource(p).Any());
        RestoreHistoryCommand = new RelayCommand(p => RestoreHistory(p as HistoryEntry));
        ClearHistoryCommand = new RelayCommand(_ => History.Clear(), _ => History.Count > 0);

        CopyTextCommand = new RelayCommand(p => CopyToClipboard(p as string));
        CopyPropertyValueCommand = new RelayCommand(p => CopyToClipboard((p as PropertyEntry)?.Value));
        CopyPropertyNameCommand = new RelayCommand(p => CopyToClipboard((p as PropertyEntry)?.Name));
        CopyPropertyLineCommand = new RelayCommand(p =>
        {
            if (p is PropertyEntry e) CopyToClipboard($"{e.Name} = {e.Value}");
        });
        CopySnapshotGuidCommand = new RelayCommand(p => CopyToClipboard(JoinSnapshots(p, s => s.Guid)));
        CopySnapshotIdCommand = new RelayCommand(p => CopyToClipboard(JoinSnapshots(p, s => s.IdentifierId.ToString())));
        CopySnapshotSummaryCommand = new RelayCommand(p => CopyToClipboard(JoinSnapshots(p, s => s.Summary)));
        ShowInTeklaCommand = new RelayCommand(
            p => ShowInTekla(p),
            p => ExtractSnapshotsWithSource(p).Any());
        DumpJsonCommand = new RelayCommand(
            p => DumpJson(p),
            p => ExtractSnapshotsWithSource(p).Any());
        DumpFrameJsonCommand = new RelayCommand(_ => DumpFrameJson(), _ => Trail.Count > 0);
        HighlightCommand = new RelayCommand(p => Highlight(p as PropertyEntry),
            p => (p as PropertyEntry)?.IsHighlightable == true);
        ClearHighlightsCommand = new RelayCommand(_ => _visualizer.Clear());
        SelectPropertyInTeklaCommand = new RelayCommand(
            p => SelectPropertyInTekla(p as PropertyEntry),
            p => HasShowableTarget(p as PropertyEntry));
        ApplyThemeCommand = new RelayCommand(p => ApplyTheme(p as string));
        OpenEventMonitorCommand = new RelayCommand(_ => OpenEventMonitor());

        PropertiesView = CollectionViewSource.GetDefaultView(Properties);
        PropertiesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyEntry.Category)));

        ObjectsView = CollectionViewSource.GetDefaultView(Objects);
        ObjectsView.Filter = MatchesSearch;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsTopmost
    {
        get => _isTopmost;
        set
        {
            if (SetProperty(ref _isTopmost, value))
            {
                SettingsStore.Current.IsTopmost = value;
                SettingsStore.Save();
            }
        }
    }

    public string? CurrentTargetTitle
    {
        get => _currentTargetTitle;
        private set => SetProperty(ref _currentTargetTitle, value);
    }

    public bool CanGoBack => Trail.Count > 1;

    public ObservableCollection<DecompositionFrame> Trail { get; } = new();

    /// <summary>
    /// Most-recent-first list of trails the user has navigated to during this session. Capped at
    /// <see cref="MaxHistory"/> entries — far enough back to retrace a deep drill-down, not so far
    /// that the dropdown becomes unscannable.
    /// </summary>
    public ObservableCollection<HistoryEntry> History { get; } = new();
    private const int MaxHistory = 30;

    public TeklaObjectSnapshot? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
                OpenFromSelection(value);
        }
    }

    public ObservableCollection<TeklaObjectSnapshot> Objects { get; } = new();
    public ObservableCollection<PropertyEntry> Properties { get; } = new();
    public ICollectionView PropertiesView { get; }
    public ICollectionView ObjectsView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ObjectsView.Refresh();
        }
    }

    private bool MatchesSearch(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (item is not TeklaObjectSnapshot s) return false;

        var needle = _searchText.Trim();
        return Contains(s.TypeName, needle)
            || Contains(s.Summary, needle)
            || Contains(s.Guid, needle)
            || s.IdentifierId.ToString().Contains(needle);
    }

    private static bool Contains(string? value, string needle)
        => !string.IsNullOrEmpty(value)
           && value!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    public ICommand LoadSelectedCommand { get; }
    public ICommand LoadAllCommand { get; }
    public ICommand LoadByTypeCommand { get; }
    public ICommand SearchByIdCommand { get; }
    public ICommand LoadModelRootCommand { get; }
    public ICommand LoadActiveViewCommand { get; }
    public ICommand LoadReferenceModelsCommand { get; }
    public ICommand LoadDrawingsCommand { get; }
    public ICommand LoadActiveDrawingCommand { get; }
    public ICommand PickObjectCommand { get; }
    public ICommand PickObjectsCommand { get; }
    public ICommand NavigateBackCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateToFrameCommand { get; }
    public ICommand DrillIntoCommand { get; }
    public ICommand OpenInNewWindowCommand { get; }
    public ICommand OpenSnapshotInNewWindowCommand { get; }
    public ICommand RestoreHistoryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand CopyTextCommand { get; }
    public ICommand CopyPropertyValueCommand { get; }
    public ICommand CopyPropertyNameCommand { get; }
    public ICommand CopyPropertyLineCommand { get; }
    public ICommand CopySnapshotGuidCommand { get; }
    public ICommand CopySnapshotIdCommand { get; }
    public ICommand CopySnapshotSummaryCommand { get; }
    public ICommand ShowInTeklaCommand { get; }
    public ICommand DumpJsonCommand { get; }
    public ICommand DumpFrameJsonCommand { get; }
    public ICommand HighlightCommand { get; }
    public ICommand ClearHighlightsCommand { get; }
    public ICommand SelectPropertyInTeklaCommand { get; }
    public ICommand ApplyThemeCommand { get; }
    public ICommand OpenEventMonitorCommand { get; }

    private void LoadSelected()
    {
        // Cover both surfaces: a user with the drawing editor active wants their drawing-object
        // selection; a user in the model wants their model-object selection. Concatenating is
        // safe — only one surface is active at a time in practice.
        Run("selection", () =>
        {
            var model = _collector.GetSelectedObjects().Select(o => o.ToSnapshot(_collector));
            var drawing = _drawingsCollector.GetSelectedDrawingObjects().Select(d => d.ToSnapshot());
            return model.Concat(drawing);
        });
    }

    private void LoadAll()
    {
        Run("all model objects", () => _collector.GetAllObjects()
            .Select(o => o.ToSnapshot(_collector)));
    }

    /// <summary>
    /// Pushes the running Tekla <see cref="Tekla.Structures.Model.Model"/> as a single decomposition
    /// frame — root entry point to project info, phases, work plane, catalogs, etc.
    /// </summary>
    private void LoadModelRoot()
    {
        try
        {
            IsBusy = true;
            Objects.Clear();
            Trail.Clear();
            var model = new Tekla.Structures.Model.Model();
            Trail.Add(new DecompositionFrame(model, "Model (root)"));
            RenderCurrent();
            CaptureHistory();
            Status = "Decomposing model root.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to open model root: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Pushes the active Tekla model view as a single decomposition frame.
    /// </summary>
    private void LoadActiveView()
    {
        try
        {
            IsBusy = true;
            Objects.Clear();
            Trail.Clear();
            var view = Tekla.Structures.Model.UI.ViewHandler.GetActiveView();
            if (view is null)
            {
                Status = "No active Tekla view.";
                return;
            }
            Trail.Add(new DecompositionFrame(view, $"Active view: {view.Name}"));
            RenderCurrent();
            CaptureHistory();
            Status = $"Decomposing active view '{view.Name}'.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to open active view: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadReferenceModels()
    {
        Run("reference model(s)", () =>
            _collector.GetObjectsOfTypes(new[] { typeof(Tekla.Structures.Model.ReferenceModel) })
                      .Select(o => o.ToSnapshot(_collector)));
    }

    private void LoadDrawings()
    {
        Run("drawing(s)", () => _drawingsCollector.GetAllDrawings().Select(d => d.ToSnapshot()));
    }

    /// <summary>
    /// Pushes the currently active Tekla drawing as a single decomposition frame. Falls back to
    /// loading all drawings when no editor is open — the user can then pick one.
    /// </summary>
    private void LoadActiveDrawing()
    {
        try
        {
            IsBusy = true;
            Objects.Clear();
            Trail.Clear();
            var drawing = _drawingsCollector.GetActiveDrawing();
            if (drawing is null)
            {
                Status = "No drawing is currently active in the editor.";
                return;
            }
            Trail.Add(new DecompositionFrame(drawing, $"{drawing.GetType().Name}: {drawing.Name}"));
            RenderCurrent();
            CaptureHistory();
            Status = $"Decomposing active drawing '{drawing.Name}'.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to open active drawing: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadByType()
    {
        var dialog = new Views.LoadFilterDialog { Owner = System.Windows.Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true) return;
        var types = dialog.SelectedTypes;
        if (types.Length == 0)
        {
            Status = "No types selected.";
            return;
        }

        Run($"{types.Length} type(s)", () => _collector.GetObjectsOfTypes(types)
            .Select(o => o.ToSnapshot(_collector)));
    }

    private void SearchById()
    {
        var dialog = new Views.SearchByIdDialog { Owner = System.Windows.Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true) return;

        var tokens = dialog.Tokens;
        if (tokens.Length == 0)
        {
            Status = "No IDs or GUIDs entered.";
            return;
        }

        try
        {
            IsBusy = true;
            Objects.Clear();
            ClearDecomposition();

            var results = _collector.ResolveByTokens(tokens).ToList();
            var misses = new List<string>();
            foreach (var r in results)
            {
                if (r.IsResolved)
                    Objects.Add(r.Object!.ToSnapshot(_collector));
                else
                    misses.Add(r.Token);
            }

            if (misses.Count == 0)
                Status = $"Resolved {Objects.Count} of {tokens.Length} token(s).";
            else
                Status = $"Resolved {Objects.Count} of {tokens.Length}; missed: {string.Join(", ", misses.Take(5))}{(misses.Count > 5 ? "…" : "")}";

            // With a single result there's nothing to choose between — surface its details
            // immediately instead of making the user click the lone row.
            if (Objects.Count == 1)
                SelectedObject = Objects[0];
        }
        catch (Exception ex)
        {
            Status = $"Failed to resolve: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PickObject()
    {
        Run("picked object", () =>
        {
            var picked = _collector.PickObject();
            return picked is null
                ? Enumerable.Empty<TeklaObjectSnapshot>()
                : new[] { picked.ToSnapshot(_collector) };
        });
    }

    private void PickObjects()
    {
        Run("picked objects", () => _collector.PickObjects()
            .Select(o => o.ToSnapshot(_collector)));
    }

    private void Run(string label, Func<IEnumerable<TeklaObjectSnapshot>> producer)
    {
        const int progressTick = 200;
        try
        {
            IsBusy = true;
            Objects.Clear();
            ClearDecomposition();
            Status = $"Loading {label}…";
            PumpDispatcher();

            var buffer = new List<TeklaObjectSnapshot>();
            foreach (var snapshot in producer())
            {
                buffer.Add(snapshot);
                if (buffer.Count % progressTick == 0)
                {
                    Status = $"Loading {label}… {buffer.Count} so far";
                    PumpDispatcher();
                }
            }

            Status = buffer.Count == 0
                ? $"No {label} found."
                : $"Populating grid with {buffer.Count} {label}…";
            PumpDispatcher();

            foreach (var snapshot in buffer)
                Objects.Add(snapshot);

            Status = $"Loaded {Objects.Count} {label}.";

            // With a single result there's nothing to choose between — surface its details
            // immediately instead of making the user click the lone row.
            if (Objects.Count == 1)
                SelectedObject = Objects[0];
        }
        catch (Exception ex)
        {
            Status = $"Failed to load {label}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Yields to the WPF message pump so the busy overlay and status text repaint while we keep
    /// running on the UI thread (Tekla enumerators are not thread-safe, so we can't push the load
    /// off to a worker thread).
    /// </summary>
    private static void PumpDispatcher()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OpenEventMonitor()
    {
        var window = new Views.EventMonitorWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        window.Show();
    }

    private void ApplyTheme(string? themeName)
    {
        if (!Enum.TryParse<Wpf.Ui.Appearance.ApplicationTheme>(themeName, true, out var theme))
        {
            Status = $"Unknown theme: {themeName}";
            return;
        }
        try
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme);
            SettingsStore.Current.Theme = theme.ToString();
            SettingsStore.Save();
            Status = $"Applied {theme} theme.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to apply theme: {ex.Message}";
        }
    }

    private static string? JoinSnapshots(object? parameter, Func<TeklaObjectSnapshot, string?> projector)
    {
        IEnumerable<TeklaObjectSnapshot> snapshots = parameter switch
        {
            TeklaObjectSnapshot single => new[] { single },
            System.Collections.IEnumerable list => list.OfType<TeklaObjectSnapshot>(),
            _ => Enumerable.Empty<TeklaObjectSnapshot>(),
        };
        var values = snapshots.Select(projector)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
        return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { System.Windows.Clipboard.SetText(text); }
        catch { /* clipboard occasionally races other apps — best effort */ }
    }

    private IEnumerable<Tekla.Structures.Model.ModelObject> CollectModelObjects(PropertyEntry? entry)
    {
        if (entry?.RawValue is null) yield break;

        foreach (var mo in ToModelObjects(entry.RawValue))
            yield return mo;
    }

    /// <summary>
    /// Flattens a raw property value into the model objects it represents. Handles a direct
    /// <see cref="Tekla.Structures.Model.ModelObject"/>, a <see cref="Tekla.Structures.Identifier"/>
    /// (resolved through the collector), or any enumerable mixing the two.
    /// </summary>
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
                yield break; // strings are IEnumerable<char> — don't treat as object lists
            case System.Collections.IEnumerable list:
                foreach (var item in list)
                {
                    if (item is null) continue;
                    foreach (var m in ToModelObjects(item))
                        yield return m;
                }
                yield break;
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

            var drawingResult = drawingObjects.Count > 0
                ? _drawingsCollector.SelectInDrawing(drawingObjects)
                : null;

            Status = BuildShowStatus(modelObjects.Count, drawingObjects.Count, drawingResult);
        }
        catch (Exception ex)
        {
            Status = $"Failed to show in Tekla: {ex.Message}";
        }
    }

    private static string BuildShowStatus(
        int modelObjects,
        int drawingObjects,
        DrawingsCollector.DrawingSelectionResult? drawing)
    {
        if (modelObjects == 0 && drawingObjects == 0) return "Nothing to show in Tekla.";
        var parts = new List<string>();
        if (modelObjects > 0) parts.Add($"selected {modelObjects} object(s) in model");
        if (drawing is not null)
        {
            if (drawing.Selected > 0)
            {
                var label = drawing.FellBackToSingles
                    ? $"selected {drawing.Selected} of {drawing.Attempted} object(s) in active drawing (per-item fallback)"
                    : $"selected {drawing.Selected} object(s) in active drawing";
                parts.Add(label);
            }
            else if (drawing.Attempted > 0)
            {
                parts.Add($"Tekla rejected {drawing.Attempted} drawing object(s)");
            }
            if (drawing.Skipped > 0)
                parts.Add($"{drawing.Skipped} object(s) skipped (their drawing isn't open)");
        }
        return string.Join("; ", parts) + ".";
    }

    private bool HasShowableTarget(PropertyEntry? entry)
        => CollectModelObjects(entry).Any() || CollectDrawingObjects(entry).Any();

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
            case System.Collections.IEnumerable list:
                foreach (var item in list)
                {
                    if (item is null) continue;
                    foreach (var d in ToDrawingObjects(item))
                        yield return d;
                }
                yield break;
        }
    }

    private void Highlight(PropertyEntry? entry)
    {
        if (entry is null) return;
        try
        {
            if (_visualizer.TryHighlight(entry.RawValue))
                Status = $"Drew overlay for {entry.Name}.";
            else
                Status = $"Can't visualize {entry.Name} ({entry.ValueType}).";
        }
        catch (Exception ex)
        {
            Status = $"Failed to draw overlay: {ex.Message}";
        }
    }

    private void ShowInTekla(object? parameter)
    {
        var snapshots = ExtractSnapshotsWithSource(parameter).ToList();
        if (snapshots.Count == 0) return;

        var modelObjects = snapshots
            .Select(s => s.Source)
            .OfType<Tekla.Structures.Model.ModelObject>()
            .ToList();
        var drawings = snapshots
            .Select(s => s.Source)
            .OfType<Tekla.Structures.Drawing.Drawing>()
            .ToList();

        try
        {
            if (modelObjects.Count > 0)
                _collector.SelectInModel(modelObjects);
            // SetActiveDrawing only takes one — open the first drawing in the selection and tell
            // the user if we ignored extras. Selecting many drawings at once isn't supported by
            // the Tekla UI (only one editor window can be active).
            if (drawings.Count > 0)
                _drawingsCollector.OpenInTekla(drawings[0]);

            Status = (modelObjects.Count, drawings.Count) switch
            {
                (0, 0) => "Nothing to show in Tekla.",
                (var m, 0) => m == 1
                    ? $"Selected {snapshots[0].TypeName} #{snapshots[0].IdentifierId} in Tekla."
                    : $"Selected {m} objects in Tekla.",
                (0, var d) => d == 1
                    ? $"Opened drawing '{drawings[0].Name}'."
                    : $"Opened '{drawings[0].Name}' ({d - 1} more not opened — Tekla shows one drawing at a time).",
                (var m, var d) => $"Selected {m} object(s) and opened drawing '{drawings[0].Name}'.",
            };
        }
        catch (Exception ex)
        {
            Status = $"Failed to show in Tekla: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes a curated, creation-focused JSON dump of the selected object(s) to a file. The dump is
    /// shaped for pasting into an LLM that recreates a similar object via the Tekla API — see
    /// <see cref="JsonObjectDumper"/>. A single selection produces a JSON object; multiple produce an array.
    /// </summary>
    private void DumpJson(object? parameter)
    {
        var snapshots = ExtractSnapshotsWithSource(parameter).ToList();
        if (snapshots.Count == 0) return;

        try
        {
            var sources = snapshots.Select(s => s.Source!).ToList();
            var json = (sources.Count == 1 ? _jsonDumper.Dump(sources[0]) : _jsonDumper.DumpMany(sources))
                .ToIndentedString();

            var suggestedName = snapshots.Count == 1
                ? $"{snapshots[0].TypeName}_{snapshots[0].IdentifierId}.json"
                : $"TeklaLookup_{snapshots.Count}_objects.json";

            var path = JsonDumpFile.Save(json, suggestedName);
            if (path is null)
            {
                Status = "JSON dump cancelled.";
                return;
            }

            Status = snapshots.Count == 1
                ? $"Saved JSON dump of {snapshots[0].TypeName} #{snapshots[0].IdentifierId} to {path}."
                : $"Saved JSON dump of {snapshots.Count} objects to {path}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to dump JSON: {ex.Message}";
        }
    }

    /// <summary>
    /// Dumps the object currently shown in the decomposition pane (the active trail frame) — including
    /// any sub-object the user has drilled into — to a curated JSON file. See <see cref="JsonObjectDumper"/>.
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

    private static IEnumerable<TeklaObjectSnapshot> ExtractSnapshotsWithSource(object? parameter)
    {
        if (parameter is TeklaObjectSnapshot single)
        {
            if (single.Source != null) yield return single;
            yield break;
        }
        if (parameter is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                if (item is TeklaObjectSnapshot s && s.Source != null) yield return s;
        }
    }

    private void OpenFromSelection(TeklaObjectSnapshot? snapshot)
    {
        Trail.Clear();
        if (snapshot?.Source is null)
        {
            RenderCurrent();
            return;
        }

        var title = $"{snapshot.TypeName} #{snapshot.IdentifierId}";
        Trail.Add(new DecompositionFrame(snapshot.Source, title));
        RenderCurrent();
        CaptureHistory();
    }

    private void DrillInto(PropertyEntry? entry)
    {
        if (entry is null || !entry.IsDrillable || entry.RawValue is null) return;

        try
        {
            IsBusy = true;
            Status = $"Drilling into {entry.Name}…";
            PumpDispatcher();

            // Materialize single-pass enumerables (e.g. Tekla's ModelObjectEnumerator) so the
            // frame can be re-rendered after navigating back. Some catalogs (Profiles, Materials)
            // contain tens of thousands of items and the foreach can stall for several seconds
            // — pump the dispatcher every 500 items so the busy overlay and status text update.
            var target = entry.RawValue;
            if (target is System.Collections.IEnumerable enumerable && target is not string && target is not System.Collections.IDictionary)
            {
                const int progressTick = 500;
                var items = new System.Collections.Generic.List<object?>();
                foreach (var item in enumerable)
                {
                    items.Add(item);
                    if (items.Count % progressTick == 0)
                    {
                        Status = $"Drilling into {entry.Name}… {items.Count} items so far";
                        PumpDispatcher();
                    }
                }
                target = items;
            }

            var title = $"{entry.Name} : {entry.ValueType}";
            Trail.Add(new DecompositionFrame(target, title));
            RenderCurrent();
            CaptureHistory();
            Status = $"Decomposed {entry.Name}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to drill into {entry.Name}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens a drillable property in a standalone <see cref="Views.InspectorWindow"/> seeded with
    /// the property's raw value as its root frame, so the user can browse it alongside the main
    /// window without losing their current context. The inspector reuses the same collector /
    /// visualizer instances so overlays and identifier resolution stay coherent across windows.
    /// </summary>
    private void OpenInNewWindow(PropertyEntry? entry)
    {
        if (entry is null || !entry.IsDrillable || entry.RawValue is null) return;
        try
        {
            var target = entry.RawValue;
            if (target is System.Collections.IEnumerable enumerable && target is not string && target is not System.Collections.IDictionary)
                target = enumerable.Cast<object?>().ToList();

            InspectorWindowFactory.Open(target, $"{entry.Name} : {entry.ValueType}",
                _collector, _drawingsCollector, _visualizer, _decomposer);
            Status = $"Opened {entry.Name} in a new window.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to open new window: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens each selected snapshot in its own <see cref="Views.InspectorWindow"/>. Useful for
    /// side-by-side comparison of two parts, two assemblies, etc. without losing the main grid.
    /// </summary>
    /// <summary>
    /// Snapshots the current <see cref="Trail"/> into <see cref="History"/>. If an identical trail
    /// is already present we move it to the top (MRU) instead of duplicating. Called from any
    /// code path that grows the trail (new root load or drill-in) — back/breadcrumb navigations
    /// don't capture, since the prior state is already in history.
    /// </summary>
    private void CaptureHistory()
    {
        if (Trail.Count == 0) return;

        for (var i = 0; i < History.Count; i++)
        {
            if (!History[i].Frames.SequenceEqual(Trail)) continue;
            if (i > 0) History.Move(i, 0);
            return;
        }

        History.Insert(0, new HistoryEntry(Trail));
        while (History.Count > MaxHistory)
            History.RemoveAt(History.Count - 1);
    }

    private void RestoreHistory(HistoryEntry? entry)
    {
        if (entry is null) return;
        try
        {
            IsBusy = true;
            Trail.Clear();
            foreach (var frame in entry.Frames)
                Trail.Add(frame);
            RenderCurrent();
            CaptureHistory();
            Status = $"Restored trail: {entry.DisplayPath}";
        }
        catch (Exception ex)
        {
            Status = $"Failed to restore history: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenSnapshotsInNewWindows(object? parameter)
    {
        var snapshots = ExtractSnapshotsWithSource(parameter).ToList();
        if (snapshots.Count == 0) return;
        try
        {
            foreach (var snapshot in snapshots)
            {
                var title = $"{snapshot.TypeName} #{snapshot.IdentifierId}";
                InspectorWindowFactory.Open(snapshot.Source!, title,
                    _collector, _drawingsCollector, _visualizer, _decomposer);
            }
            Status = snapshots.Count == 1
                ? $"Opened {snapshots[0].TypeName} #{snapshots[0].IdentifierId} in a new window."
                : $"Opened {snapshots.Count} objects in new windows.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to open new window: {ex.Message}";
        }
    }

    /// <summary>
    /// Re-runs decomposition on the current frame, picking up any changes the user made in Tekla
    /// since the frame was opened (UDA edits, attribute changes, geometry edits). For model and
    /// drawing objects we also call <c>Select()</c> first to pull fresh database state, since
    /// Tekla's wrapper objects cache their property values until refreshed.
    /// </summary>
    private void Refresh()
    {
        if (Trail.Count == 0) return;
        try
        {
            IsBusy = true;
            var frame = Trail[Trail.Count - 1];
            RefreshTarget(frame.Target);
            Status = $"Refreshing {frame.Title}…";
            PumpDispatcher();
            RenderCurrent();
            Status = $"Refreshed {frame.Title}.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to refresh: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void RefreshTarget(object? target)
    {
        switch (target)
        {
            case Tekla.Structures.Model.ModelObject mo:
                try { mo.Select(); } catch { /* best effort */ }
                break;
            case Tekla.Structures.Drawing.DrawingObject d:
                try { d.Select(); } catch { /* best effort */ }
                break;
        }
    }

    private void NavigateBack()
    {
        if (Trail.Count <= 1) return;
        try
        {
            IsBusy = true;
            Trail.RemoveAt(Trail.Count - 1);
            PumpDispatcher();
            RenderCurrent();
        }
        finally { IsBusy = false; }
    }

    private void NavigateToFrame(DecompositionFrame? frame)
    {
        if (frame is null) return;
        var index = Trail.IndexOf(frame);
        if (index < 0) return;
        try
        {
            IsBusy = true;
            // Drop everything past the clicked frame
            while (Trail.Count > index + 1)
                Trail.RemoveAt(Trail.Count - 1);
            PumpDispatcher();
            RenderCurrent();
        }
        finally { IsBusy = false; }
    }

    private void ClearDecomposition()
    {
        Trail.Clear();
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
                // Decomposing a frame can be expensive when the target is a long list of catalog
                // items — each one runs through reflection plus the extension registry. Periodic
                // pumps keep the busy overlay (when IsBusy=true) and status text alive.
                const int progressTick = 100;
                var rendered = 0;
                foreach (var entry in _decomposer.Decompose(frame.Target))
                {
                    Properties.Add(entry);
                    rendered++;
                    if (rendered % progressTick == 0)
                    {
                        Status = $"Decomposing {frame.Title}… {rendered} properties so far";
                        PumpDispatcher();
                    }
                }
            }
            catch (Exception ex)
            {
                Status = $"Failed to decompose object: {ex.Message}";
            }
        }

        OnPropertyChanged(nameof(CanGoBack));
    }
}
