# Project State — 2025-09-04

## What works
- JSON → VSDX via **net48** runner: PASS (two nodes + one connector baseline).
- Connectors created via **ConnectorToolDataObject**; no connectors stencil.
- Stencils opened **hidden + read-only** and cached; drawing/page guaranteed.

## Architecture (current)
- **Runner**: `src\VDG.VisioRuntime` (**net48**, COM interop host).
- **CLI**: `src\VisioDiagramGenerator.CliFs` (**net8.0-windows**), shells out to runner.
- **Shared**: netstandard2.0 library for pure logic (models, pipeline helpers).

## New defaults
- **PageSizingMode = "auto"** (autosizing page). `fixed` & `fit-one-page` supported without blocking generation.

## How to run (dev)
```powershell
# Runner direct (for quick sanity)
$json = (Resolve-Path ".\samples\sample_diagram.json").Path
$out  = Join-Path (Split-Path -Parent $json) "mydiagram.vsdx"
& "src\VDG.VisioRuntime\bin\x64\Release\net48\VDG.VisioRuntime.exe" $json $out

# CLI (net8) — invokes runner
dotnet run --project .\src\VisioDiagramGenerator.CliFs -- generate --config $json --out $out
```

## Next
- Prompt 5 (CLI): config load + JSON Schema validation; friendly error table + `%TEMP%/vdg-config-errors.json`.
- Wire `ApplyPageSizing(mode, w, h)` in runtime implementation (currently a stub in pseudocode).
- Add tests for: connector glue, stencil caching, page sizing modes, path normalization.


# Project State — 2025-09-03

## What works
- `sample_diagram.json → mydiagram.vsdx` pipeline: **PASS**. A Visio doc with two nodes and one connector is generated, and the file is saved under `samples\mydiagram.vsdx`. Verified with absolute path anchoring in the CLI.

## Key runtime changes
- Hidden, read‑only stencil loading + cache population in `LoadStencil`.
- Guaranteed drawing & page (`EnsureDocumentAndPage`).
- Connectors via `ConnectorToolDataObject` (no `CONNECTORS_U.VSSX`).
- Robust `SaveAsVsdx` (select drawing doc, path normalization, mkdir).

## Repro steps
```powershell
# from repo root
$json = (Resolve-Path ".\samples\sample_diagram.json").Path
$out  = Join-Path (Split-Path -Parent $json) "mydiagram.vsdx"
& "src\VDG.VisioRuntime\bin\x64\Release\net48\VDG.VisioRuntime.exe" $json $out
```

## Previously observed failures (now mitigated)
- `KeyNotFoundException` in `VisioStaHost.JobBase.Wait()` due to missing stencil cache entries.
- COM error “Visio is unable to write to this directory or disk drive” when saving a stencil or resolving a relative path into a non‑writable working directory.

## Next
- Proceed to **Prompt 5** with the stable runtime.
- Add tests around: master drop; connector glue; text setting; `SaveAsVsdx` (relative & absolute); loading multiple stencils and dedupe.
