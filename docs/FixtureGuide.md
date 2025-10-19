# Fixture Regeneration Guide

This guide explains how to maintain the rendered VBA fixtures, keep their hashes deterministic, and troubleshoot drift detected by CI.

## Fixture Matrix
- `hello_world` (callgraph)
- `cross_module_calls` (callgraph + module-structure)
- `events_and_forms` (callgraph)

## Normal Check Workflow
```powershell
pwsh ./tools/render-fixture.ps1
```
The script renders each fixture into `out/fixtures/...` and compares SHA256 hashes against the golden copies under `tests/fixtures/render`. Any mismatch fails with a diff hint, e.g.:
````
{Fixture}/{Mode} {Kind}: Hash mismatch (expected <baseline>, actual <new>)
Diff hint: git diff --no-index -- <golden> <actual>
````

## Regeneration Workflow
Run when behaviour changes intentionally:
```powershell
pwsh ./tools/render-fixture.ps1 -Update -Note "reason for refresh"
```
- Copies regenerated IR/diagram/diagnostics/VSDX into `tests/fixtures/render/...`.
- Appends SHA256 hashes to `plan docs/fixtures_log.md` for traceability.
- Use descriptive notes (`palette tweak`, `callgraph edge ordering fix`, etc.).
- Commit updated fixtures + ledger entry in the same change.

## Troubleshooting
- **Missing golden file**: run with `-Update` to seed the baseline.
- **Hash mismatch**: inspect the diff hint, verify the change is expected, then update the baseline.
- **Unexpected Visio run**: the script forces `VDG_SKIP_RUNNER=1`. Ensure no local env overrides flip it.
- **CLI build errors**: run `dotnet build Visio-Diagram-Generator.sln -c Release` and retry.

## CI Expectations
- CI calls `render-fixture.ps1` in check mode to block drift.
- On failure, it uploads the actual artifacts for inspection.
- Do not hand-edit fixture outputs; always regenerate through the script.
- Intentional baseline updates are self-reviewed: run `-Update`, confirm ledger + metadata entries, and capture the reason in the note field.

## Quick Reference
- Fixture ledger: `plan docs/fixtures_log.md`.
- Golden outputs root: `tests/fixtures/render/`.
- Temp run outputs: `out/fixtures/` (safe to delete).
- Metadata snapshot: `plan docs/fixtures_metadata.json` (commit hash + commands).

Keep this guide updated as the fixture matrix or regeneration process evolves.

Baseline golden fixture updates are self-reviewed and logged by the project maintainer. Any changes must be intentional and annotated with a sanitized note via the update script.