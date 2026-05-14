# TeklaLookup

Interactive Tekla Structures model and drawing exploration tool — adapted from the upstream
[RevitLookup](https://github.com/lookup-foundation/RevitLookup) project. Inspect any model object,
drawing object, catalog item, or property tree at runtime: drill into properties, UDAs, report
properties, geometry, and reference-model contents; visualize geometry primitives directly in the
Tekla viewport; monitor live model/drawing events.

## Build

```powershell
dotnet build source/TeklaLookup.App
```

Outputs `source/TeklaLookup.App/bin/Debug/TeklaLookup.exe`. Run it directly — Tekla Structures
2026 must be installed (the app config is patched at build time to load Tekla assemblies from the
standard install path).

## Layout

- [source/TeklaLookup.App/](source/TeklaLookup.App/) — the WPF host (net48), Tekla integration,
  decomposition engine, UI shell.
- [source/LookupEngine.UI/](source/LookupEngine.UI/) — Fluent WPF control library (re-namespaced
  WPF-UI fork), tracked as a git submodule.
- [docs/TeklaLookup-Adaptation-Plan.md](docs/TeklaLookup-Adaptation-Plan.md) — progress log and
  remaining work for the RevitLookup → TeklaLookup adaptation.

## License

See [License.md](License.md). Upstream RevitLookup is MIT-licensed.
