# Changelog

## 1.2 — 2026-08-27

### Template (report) attributes

Tekla Lookup now reads the full set of template attributes an object carries — the same
attributes the Template Editor and reports see — driven by the environment's own
`contentattributes*.lst` definitions instead of a hardcoded name list.

- **Every declared attribute, grouped like Tekla groups them.** Attributes of related objects
  (`NUT.WEIGHT`, `ASSEMBLY.MAINPART.PROFILE`, …) appear under their own sub-categories.
- **Three scopes, chosen from the toolbar**, because cost is wildly uneven:
  - *Pinned only* — template attributes off, except the ones you starred. Fastest.
  - *Associated* (default) — the object's own attributes plus its constituents: a bolt's nut,
    washer and hole; a part's profile, material and phase. Same cost bracket as reading the
    object alone.
  - *Full (with related)* — also traverses to other model objects (assembly, cast unit,
    drawings, connection secondaries). Complete, and much slower.
- **Pinned favorites.** Star an attribute and it stays at the top in every scope — pins are
  shared across objects of the same content type (a pin made on a beam applies to columns and
  plates too) and persist between sessions. A pinned attribute is reported even when the object
  has no value for it.
- **Values fetched in bulk** — one `GetAllReportProperties` call per datatype bucket, so even
  large attribute sets stay affordable.

### Environment discovery — works across environments and installations

Attribute definitions are found the way Tekla finds them: model folder, then `XS_PROJECT`,
`XS_FIRM`, `XS_SYSTEM` and `XS_TPLED_INI`, then the installation's Template Editor settings —
first file of a given name wins, container files' `[INCLUDE]` directives are followed, and one
unreadable file never costs the rest of the environment. Verified against the stock 2026
environments (default, USA, UK, Finland, Korea, ConstrusoftEuropean, blank_project):

- `XS_TPLED_INI` — the option every stock environment uses to name its Template Editor settings
  folder — is honored, so discovery no longer depends on a search path happening to reach the
  right folder.
- The USA environment's `…\Templates\settings\` layout is found (previously its role and
  project attribute files were silently missed).
- Legacy ANSI files decode correctly (encoding is detected: BOM, then strict UTF-8, then the
  system code page) — several stock files and most firm files generated from `objects.inp`
  ship as ANSI.
- The installation root falls back to the `XS_DIR` environment variable when the advanced
  option API doesn't answer.
- Discovery is warmed up on a background thread at startup, so an unreachable network share in
  `XS_FIRM` can no longer freeze the first property read.
- When nothing is found, the property pane says where it looked instead of showing an empty
  list.

### Performance

- Property list population sped up noticeably; collection updates are batched instead of firing
  per row.

### Drawings

- Drawing details are read when *Load selected* is invoked with the drawing selected in
  Document Manager.
- Fixed refreshing of drawing details, and the list summary now updates on refresh.
- Fixed a crash on older Tekla versions when no drawing is active.

### Under the hood

- New test project (78 tests): `.lst` parsing, `[INCLUDE]` resolution, encoding detection, tier
  classification, scope selection — plus opt-in tests that run against a real installed
  environment (`TEKLALOOKUP_TEST_ENVIRONMENT` / `TEKLALOOKUP_TEST_TPLED`).

### Known limitations

- Precast-oriented content types (`CAST_UNIT`, `POUR_UNIT`, `SIMILAR_*`, `REBAR_ASSEMBLY`) are
  not yet mapped — an assembly always shows the `ASSEMBLY` attribute set.
- The attribute catalog is cached per model: switching roles or regenerating
  `contentattributes_userdefined.lst` mid-session requires an app restart to pick up.
- All `contentattributes*.lst` files in a discovered folder are loaded, including integration
  files (ArchiCAD, MagiCAD, Revit) the environment's container doesn't include — harmless, but
  you may see attributes Tekla's own attribute tree wouldn't offer.

## 1.1

- Interim `.tsep` build; its changes are folded into the 1.2 notes above.

## 1.0 — 2026-06

- Initial release: live inspection of Tekla model and drawing objects over the Open API,
  packaged as a `.tsep` extension with a launcher macro.
