# Tekla Lookup — TSEP installer

Packages Tekla Lookup as a `.tsep` for install via Tekla's Extensions Manager
or distribution through Tekla Warehouse.

Tekla Lookup ships as a **standalone WPF application** (`TeklaLookup.exe`) plus a
**launcher macro** — not as an in-process plugin. The macro is what makes "Tekla
Lookup" show up in the Applications & components catalog; running it starts the
`.exe`, which connects to the open model over the Tekla Open API.

## Build

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File build-tsep.ps1
```

Output: `dist\TeklaLookup.<version>.tsep`.

The script:
1. `dotnet publish`es `TeklaLookup.App` (Release, net48/x64) plus its dependency
   closure (including the `LookupEngine.UI` control library) into
   `installer\BuildDrop`.
2. Strips anything that must not ship — the Tekla Open API DLLs (they come from
   the user's install; refs are compile-only), XML docs, and test assemblies.
   Keeps the generic Release `TeklaLookup.exe.config` (binding redirects).

   **Must be a Release build.** Tekla resolves the Open API runtime assemblies
   itself when the app is launched inside its environment. In Release the app is
   compiled against the 2021 Open API floor (see `Directory.Build.props` —
   `TeklaOpenApiVersion`) so one binary is forward-compatible across every later
   Tekla version. A few members added after 2021 are invoked by reflection at
   runtime (`source/.../Services/OptionalTeklaApi.cs`), so the single shipped
   binary lights those features up on whatever Tekla defines them and stays
   functional on 2021. In Debug,
   `TSAppConfigPatcherTask` patches `exe.config` to point at *this* machine's
   Tekla install so you can F5-debug — packaging that would hard-code a local
   path and break on every other machine. `build-tsep.ps1` refuses any
   non-Release configuration.
3. Runs Tekla's `TeklaExtensionPackage.BatchBuilder.exe package` against
   `Manifest.xml`, which also packages the launcher macro from `Macro\`.

Pass `-TeklaVersion 2026.0` to use a different installed BatchBuilder; the
package still declares support for Tekla 2021.0 – 2099.1 regardless.

## What gets installed

| Source | Installed to |
|---|---|
| App binaries (`BuildDrop\`) | `<env>\extensions\TeklaLookup\` |
| Launcher macro (`Macro\Tekla Lookup.cs`) | `<env>\macros\modeling\` |

Tekla lists **"Tekla Lookup"** in the *Applications & components* catalog (from
the macro filename). Running it starts `TeklaLookup.exe` from
`%XSDATADIR%\Environments\common\extensions\TeklaLookup\` — the standard macro
launcher pattern (see the Tekla skill's `tsep-packaging.md`) — fire-and-forget,
since it's a modeless inspector window the user keeps open while working.

## Files

| File | Purpose |
|---|---|
| `Manifest.xml` | TEP 2.0 package definition (identity, versions, install paths). |
| `TeklaLookup.png` | Icon shown in Extensions Manager (rendered from `branding\AppIcon.svg`). |
| `Macro\Tekla Lookup.cs` | Launcher macro — catalog entry that starts the app. |
| `Macro\Tekla Lookup.png` | 96×96 catalog thumbnail; same base name as the macro (Tekla pairs them by name). |
| `BuildDrop/` | Build output staging — generated, git-ignored. |

Regenerate the two PNGs from the app icon design with:

```powershell
pwsh -STA -ExecutionPolicy Bypass -File branding\Build-InstallerIcons.ps1
```

## Notes

- **GUIDs are stable.** `UpgradeCode` (Product) and the Component `Guid` in
  `Manifest.xml` identify the package across upgrades — never change them.
- **Versioning.** Bump `Version` in `<Product>` on each release; the Extensions
  Manager uses it to detect upgrades. The TSEP 3.0 builder names the output
  `<ProductId>.<Version>.tsep` automatically.
- **BatchBuilder CLI.** Tekla 2025+ is verb-based (`package -i … -o …`); older
  docs showing a bare `-i/-o` invocation fail with "No verb selected".
- `.pdb` files are shipped intentionally so exception logging yields line numbers
  on support tickets. Drop them from the `prune` list in `build-tsep.ps1` if you
  prefer a leaner package.

## Install

In Tekla: *Applications & components* panel → gear icon → *Manage extensions* →
*Install extension* → select the `.tsep`. Restart Tekla when prompted.
