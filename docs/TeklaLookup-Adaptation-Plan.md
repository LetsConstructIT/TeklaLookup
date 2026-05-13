# TeklaLookup — Progress & Remaining Work

This document tracks the adaptation of RevitLookup → TeklaLookup. Items marked **✅** are done.
Everything below the "Done so far" section lists work that is still missing or only partial.

---

## Done so far

### Project scaffolding
- ✅ Standalone WPF host project at `source/TeklaLookup.App/` targeting `net48`, output `TeklaLookup.exe`.
- ✅ Tekla 2026 NuGet refs with `ExcludeAssets="runtime"`.
- ✅ Local `Directory.Build.props` / `Directory.Packages.props` isolating the subtree from the repo-root Revit-focused props.
- ✅ `TSAppConfigPatcherTask` wired with `TeklaVersion=2026.0`; supporting GAC DLLs (`System.Memory`, `System.Buffers`, `System.Threading.Tasks.Extensions`, `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Extensions.Logging.Abstractions`) copied from Tekla's bin into the output.
- ✅ VS Code `.vscode/launch.json` + `tasks.json` for F5 debugging via the `clr` (.NET Framework) debugger.

### Tekla integration layer
- ✅ `TeklaObjectsCollector` — `GetAllObjects`, `GetObjectsOfType`, `GetObjectsOfTypes(Type[])`, `GetSelectedObjects`, `PickObject` / `PickObjects`, `SelectInModel(...)` (highlight in viewport), `GetGuid` (Identifier → GUID).
- ✅ `TeklaObjectSnapshot` DTO with type-specific `Summary` (Part / Assembly / BoltGroup / Weld / Reinforcement / Component).
- ✅ `TypeName` decorated with subtype enum for `Beam`, `PolyBeam`, `ContourPlate`, `Assembly` (e.g. "Beam · COLUMN").

### Decomposition engine
- ✅ `ObjectDecomposer` — reflection over public instance properties; UDA hashtable (`GetAllUserProperties`); common report properties (`NAME`, `PROFILE`, `WEIGHT`, `LENGTH`, …); single-pass `IEnumerator` / `IEnumerable` materialized to a `List<object?>` for safe re-iteration and drill-down.
- ✅ Tekla-aware value formatter (`TeklaValueFormatter`) — concise one-line summaries for `Identifier`, `Point`, `Vector`, `CoordinateSystem`, `Line`, `LineSegment`, `GeometricPlane`, `Distance`, `Angle`, `Profile`, `Material`.
- ✅ Type-extension registry with `TeklaTypeExtension<T>` base and concrete classes:
  - `ModelExtensions` (model root: `ConnectionStatus`, `ModelInfo`, `ProjectInfo`, `Phases`, `WorkPlaneHandler`, `ClashCheckHandler`, `Catalogs`, `TeklaStructuresInfo`, `TeklaStructuresFiles`)
  - `WorkPlaneHandlerExtensions` (`CurrentTransformationPlane`)
  - `CatalogHandlerExtensions` (Profiles, Materials, Bolts, Rebars, Components, Drawings, Shapes, Meshes)
  - `ModelObjectExtensions` (`Phase`, `Children`)
  - `PartExtensions` (`Solid`, `CoordinateSystem`, `CenterLine`, `ReferenceLine`, `Assembly`, `Bolts`, `Welds`, `Reinforcements`)
  - `AssemblyExtensions` (`MainPart`, `Secondaries`, `SubAssemblies`)
  - `BoltGroupExtensions` (`BoltPositions`, `PartToBeBolted`, `PartToBoltTo`, `OtherPartsToBolt`)
  - `BaseWeldExtensions` / `PolygonWeldExtensions` (`MainObject`, `SecondaryObject`, `Solid`, `WeldGeometries`, `Polygon`)
  - `ReinforcementExtensions`, `BaseRebarGroupExtensions`, `RebarGroupExtensions`, `SingleRebarExtensions`, `RebarSetExtensions` (`Solid`, `Assembly`, `FatherPour(Unit)`, `NumberOfRebars`, `RebarGeometries`, `StartPoint`/`EndPoint`, `Polygons`, `Reinforcements`/`RebarModifiers`/`RebarSetAdditions`/`RebarLegSurfaces`/`LegFaces`/`Guidelines`)
  - `ComponentExtensions`, `ConnectionExtensions`, `DetailExtensions`, `SeamExtensions` (`PrimaryObject`, `SecondaryObjects`, `ReferencePoint`, `InputPolygon`, `Components`, `Booleans`, `ComponentInput`, `Assembly`)
  - `ReferenceModelExtensions` / `ReferenceModelObjectExtensions` (`Children`, `ConvertedObjects`, `CurrentRevision`, `Revisions`, `ReferenceModel`, `Father`)

### UI shell
- ✅ Fluent look via `LookupEngine.UI` (re-namespaced WPF-UI fork): themed `DataGrid`, `FluentWindow` with Mica backdrop, rounded corners, custom `TitleBar`.
- ✅ Toolbar: primary **Load selected** button + **More ▾** dropdown (Decompose model root / Load by type… / Load all) + **Pick ▾** dropdown (One / Many) + **Clear overlays** + **Event monitor…**. Right side: 🎨 **Theme** dropdown (Light / Dark / High contrast / Auto) and 📌 **Pin on top** toggle.
- ✅ Left grid: search filter (TypeName / Summary / GUID / Id), `Type · subtype` column, `Summary` column, multi-select (Ctrl/Shift).
- ✅ Right grid: grouped by `Category` (Properties / Extensions / User Properties / Report / Items), wrapped Value column, tooltips, themed selection.
- ✅ Drill-down with breadcrumb `Trail`; click any segment to jump back; Alt+Left for back; Enter on a focused property row to drill in.
- ✅ Context menus:
  - Left grid: **Select in Tekla** (multi-select aware — highlights every selected row's object in one call), Copy GUID / ID / summary (for the focused row). Right-click promotes an unselected row into the selection.
  - Right grid: Drill in, Show in viewport (geometry), Show in Tekla (ModelObject), Copy value / name / "Name = Value".

### Geometry visualization
- ✅ `GeometryVisualizer` wrapping `GraphicsDrawer` with overlay-id tracking and a **Clear overlays** button.
- ✅ Supported types: `Point`, `Vector`, `Line`, `LineSegment`, `Tekla.Structures.Model.Plane`, `TransformationPlane` (RGB axis tripod), `Solid`, `Contour`, `Polygon`, `IList<Point>`.

### Filtered loading
- ✅ Load-by-type dialog (`LoadFilterDialog`) with checkboxes for Beam, ContourPlate, PolyBeam, Assembly, BoltGroup, BaseWeld, Reinforcement, BaseComponent, ReferenceModel. Backed by `ModelObjectSelector.GetAllObjectsWithType(Type[])`.

### Event monitor
- ✅ Separate Fluent window opened from the main toolbar.
- ✅ Subscribes to model events (`ModelObjectChanged` with `Add`/`Modify`/`Delete` rows, `SelectionChange` with live selection count, `ModelSave`, `ModelLoadInfo`) and drawing events (`DrawingInserted`/`Updated`/`Deleted`/`StatusChanged`).
- ✅ Background-thread callbacks marshalled to the UI thread via `Dispatcher`.
- ✅ Auto-scroll to newest row; entries never dropped (no cap).
- ✅ Right-click → **Select in Tekla** resolves `Identifier`/GUID back to a live `ModelObject` and calls `ModelObjectSelector.Select(...)`.
- ✅ Multi-select (Ctrl/Shift) batches all selected rows into a single Tekla selection call; right-clicking an unselected row promotes it into the selection first.
- ✅ Lifecycle: subscribe on window load, unregister on close (`UnRegister()` + `-=` handlers); `App.Exit` backstop disposes any still-open monitor windows.

---

## Still missing

### Shell & navigation
- ❌ Left-side `NavigationView` shell with sections (Dashboard / Database / Selection / View / Settings / Event Monitor) — the way RevitLookup organizes its surface.
- ❌ Dashboard landing page.
- ⚠️ No dedicated settings page yet, but **preferences now persist** to `%LOCALAPPDATA%\TeklaLookup\settings.json` (theme, pin-on-top, window left/top/width/height, maximized state). The window also refuses to restore to a position that's off every connected monitor.
- ⚠️ Session history (in-memory only) — toolbar **History ▾** dropdown surfaces up to 30 most-recently-viewed trails, captured at every trail-growth point (model root / active view / active drawing load, selection change, drill-in). Identical trails are MRU-bumped instead of duplicated. Each entry stores the live `DecompositionFrame` instances, so restoring is a straight Trail swap — no path replay or root resolution. Cleared on app exit; no persistence and no explicit bookmarks yet.

### Decomposition entry points
- ✅ Decompose active model view — toolbar entry pushes `ViewHandler.GetActiveView()` as a single decomposition frame.
- ✅ Load reference models — toolbar entry loads every `ReferenceModel` into the left grid; drilling exposes children / converted objects / revisions via the new `ReferenceModelExtensions`.
- ✅ Search-by-ID/GUID page — `SearchByIdDialog` accepts a free-form paste of integer IDs and GUIDs (any whitespace/comma/semicolon delimited); `TeklaObjectsCollector.ResolveByTokens` resolves each one against the live model. Misses are reported in the status line.

### Live integration
- ✅ Event monitor (see Done section).
- ✅ Static helpers as root entries — `TeklaStructuresInfo` (program version, build number, revision date, user, plugin folder, app-data folders, registry key, copyright) surfaced via a `TeklaStructuresInfoSnapshot`; `TeklaStructuresFiles` instantiated and exposed directly. Both reachable from **Decompose model root**.

### Drawings side
- ✅ `DrawingsCollector` wraps `DrawingHandler.GetDrawings()` / `GetActiveDrawing()` / `SetActiveDrawing()`.
- ✅ Toolbar entries **Load drawings** and **Decompose active drawing**; right-click → **Show in Tekla** on a drawing snapshot now calls `SetActiveDrawing(...)` (first drawing wins — Tekla only shows one editor at a time).
- ✅ Drawing-class extensions: `Drawing` (Sheet / PlotFileName / PlotFileNameExt), `AssemblyDrawing` / `SinglePartDrawing` / `CastUnitDrawing` (the typed identifier + sheet number, plus `CastUnitById`).
- ✅ Drawing-object extensions: `DrawingObject` (Drawing / View / RelatedObjects), `ViewBase` (OriginalDrawing / AllObjects / Objects / AxisAlignedBoundingBox), `Symbol`, `Text` (AABB/OBB/inner objects), `DimensionBase` (DimensionSet), and `Mark` (registered explicitly even though all useful members are reflected, so future curated additions have a home).
- ⚠️ `TeklaObjectSnapshot.Source` was widened from `ModelObject?` to `object?` so drawings can ride the same list/decomposition plumbing; `ShowInTekla` splits the collection by source type and dispatches appropriately.

### Type-extension coverage
- ✅ `BaseWeld` / `PolygonWeld` — main/secondary object accessors, solid, weld geometries, contour polygon.
- ✅ `Reinforcement` / `BaseRebarGroup` / `RebarGroup` / `SingleRebar` / `RebarSet` — solid, parent pour/assembly, rebar geometries, distribution endpoints, legs/guidelines.
- ✅ `Component` / `Connection` / `Detail` / `Seam` — primary/secondary objects, component input, sub-components, booleans, reference point, input polygon.
- ✅ `ReferenceModel` / `ReferenceModelObject` — children, converted objects, revisions, father, owning reference model.
- ✅ `ContourPlate` / `PolyBeam` curated extensions (`ContourPolycurve`, `CenterLinePolycurve`, `PolybeamCoordinateSystems`). `Beam` has no method-only data accessors, so reflection already covers everything on it — no extension needed.
- ✅ `ProjectInfo` curated extension (`ProjectBasePoint`, `CurrentCoordsysBasePoint`, `BasePoints`, `IntegerUserProperties`, `DoubleUserProperties`, `StringUserProperties`). `Phase` and `ModelInfo` have no method-only data accessors — reflection already covers everything on them, no extension needed.

### Catalog item drill-downs
- ✅ `ProfileItem` / `ShapeItem` / `ComponentItem` curated extensions (`HighAccuracyCrossSection`, `ProfileItemSubTypes`, `IsProfileUserDefined`, `IsProfileUserParametric`, `InstanceCount`, `Version`). `MaterialItem` / `BoltItem` / `RebarItem` / `MeshItem` are pure-property types — reflection already surfaces everything, no extension needed. `GetUserPropertyItems(...)` lives on `CatalogHandler`, not on individual items, so the plan's original mention of per-item UDAs didn't apply.

### Visualization
- ❌ `Mesh` (we have `DrawMeshLines` / `DrawMeshSurface` available on `GraphicsDrawer`, just not wired).
- ❌ `OrientedBoundingBox` (12 edges).
- ❌ Optional text labels on overlays (e.g. "X" / "Y" / "Z" tips on the work-plane tripod via `DrawTextToView`).

### Plumbing
- ❌ DI host — RevitLookup uses `Microsoft.Extensions.Hosting` with a service provider; we use `new()` everywhere.
- ❌ Logging — Serilog in RevitLookup; we only write to a single `Status` text line.
- ✅ Persisted settings — `SettingsStore` + `UserSettings` DataContract serialized as JSON via `DataContractJsonSerializer` (no extra NuGet). Saved on theme change, pin-on-top toggle, and window close.

### UX
- ✅ Row icons — a 28px glyph column at the start of every grid. Left grid maps each `TeklaObjectSnapshot` to a domain icon (Beam/PolyBeam → Cube, ContourPlate → Rectangle, Assembly → Stack, BoltGroup → Diamond, Weld → Connector, Reinforcement → LayerDiagonal, Component → PuzzlePiece, Drawing → DocumentBulletList, ReferenceModel → DocumentLink, Mark/Text/Symbol → TextDescription, Dimension → Form). Right grid maps each `PropertyEntry` to its category (Properties → Tag, Extensions → Sparkle, User Properties → PersonNote, Report → ClipboardTextLtr, Items → List), with error rows getting Warning and null values getting Subtract regardless of category. Both converters live in `RowIconConverters.cs` and are registered as application resources for reuse across MainWindow and InspectorWindow.
- ✅ Open-in-new-window for a drilled value — right-click → **Open in new window** on any drillable property row spawns a standalone `InspectorWindow` seeded with that value as its root frame. New windows share the main view-model's collector / drawings collector / geometry visualizer / decomposer, and the inspector's own context menu can recursively spawn further inspectors for side-by-side comparison.
- ✅ Multi-select in the left grid + **Select in Tekla** (selects every highlighted row's object in one call; right-click promotes the row under the cursor).

### Distribution
- ❌ TSEP packaging — currently the user runs `bin/Debug/TeklaLookup.exe` directly; needs a `Build-Tsep.ps1`-style script per the sibling `Guid-Manager` repo's pattern.
- ❌ No installer / Applications & Components ribbon entry.

---

## Suggested next slices (in priority order)

1. ✅ **Type extensions for `BaseWeld`, `Reinforcement`, `BaseComponent`, `ReferenceModel`** — done.
2. ✅ **Decompose active view + reference models** — done.
3. ✅ **Persisted settings** — done.
4. ✅ **Drawing-side decomposition** — done.
5. **TSEP packaging** — only worth doing once the feature surface stabilizes.
